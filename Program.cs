using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

return await Exporter.RunAsync(args);

internal static class Exporter
{
    private static readonly Regex SafeName = new(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$",
        RegexOptions.CultureInvariant);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            Usage();
            return 0;
        }
        if (args.Length == 1 && args[0] == "--self-test")
            return SelfTest();

        try
        {
            var o = Parse(args);
            var database = DatabaseUrl(o.Database);
            var output = Path.GetFullPath(o.Output);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            if (File.Exists(output))
                throw new ArgumentException($"Output already exists: {output}");

            var csv = Path.Combine(Path.GetDirectoryName(output)!,
                $".{Path.GetFileName(output)}.{Guid.NewGuid():N}.tmp");
            var script = Path.Combine(Path.GetTempPath(), $"transbase-{Guid.NewGuid():N}.tbi");
            try
            {
                await File.WriteAllTextAsync(script, Script(csv, o.Table), new UTF8Encoding(false));
                var start = new ProcessStartInfo(o.Tbi)
                {
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(output)!
                };
                foreach (var arg in new[] { "-s", "-f", script, database })
                    start.ArgumentList.Add(arg);
                if (o.User is not null) start.ArgumentList.Add(o.User);

                Console.WriteLine($"Exporting {o.Table} from {database}...");
                Console.WriteLine("Transbase tbi may request credentials in this terminal.");
                using var process = Process.Start(start)
                    ?? throw new InvalidOperationException("Could not start Transbase tbi.");
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Transbase tbi failed: exit code {process.ExitCode}.");
                if (!File.Exists(csv))
                    throw new InvalidOperationException("Transbase tbi did not create a CSV file.");
                File.Move(csv, output);
                Console.WriteLine($"Saved: {output} ({new FileInfo(output).Length:N0} bytes)");
                return 0;
            }
            finally
            {
                File.Delete(script);
                File.Delete(csv);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Expected --option value pairs. Use --help.");
            if (!values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException($"Duplicate option: {args[i]}");
        }
        foreach (var key in values.Keys)
            if (key is not ("--database" or "--table" or "--output" or "--tbi" or "--user"))
                throw new ArgumentException($"Unknown option: {key}");
        if (!values.TryGetValue("--database", out var db) ||
            !values.TryGetValue("--table", out var table) ||
            !values.TryGetValue("--output", out var output))
            throw new ArgumentException("--database, --table and --output are required.");
        if (!SafeName.IsMatch(table))
            throw new ArgumentException("--table must be a SQL identifier, optionally schema.table.");
        if (string.IsNullOrWhiteSpace(db) || string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("Database and output cannot be empty.");
        return new(db, table, output, values.GetValueOrDefault("--tbi") ?? "tbi",
            values.GetValueOrDefault("--user"));
    }

    private static string DatabaseUrl(string input)
    {
        if (input.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("//", StringComparison.Ordinal)) return input;
        if (Path.GetExtension(input).Equals(".bdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A single .bdf is not a standalone database. Provide its complete Transbase database directory.");
        var path = Path.GetFullPath(input);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Database directory not found: {path}");
        return new Uri(path.TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');
    }

    private static string Script(string csv, string table)
    {
        var literal = csv.Replace("'", "''", StringComparison.Ordinal).Replace('\\', '/');
        return $"SPOOL INTO '{literal}' FORMAT DSV CODEPAGE UTF8 DELIM ';' QUOTE '\"' SELECT * FROM {table};{Environment.NewLine}";
    }

    private static int SelfTest()
    {
        var script = Script(@"C:\temp\a'b.csv", "articles_linkages");
        if (!script.Contains("a''b.csv") ||
            !script.Contains("SELECT * FROM articles_linkages") ||
            !script.Contains("CODEPAGE UTF8"))
            throw new InvalidOperationException("Script generation failed.");
        try
        {
            Parse(["--database", ".", "--table", "x;DROP TABLE y", "--output", "x.csv"]);
            throw new InvalidOperationException("Unsafe table name accepted.");
        }
        catch (ArgumentException) { }
        Console.WriteLine("Self-test passed: script generation and table validation.");
        Console.WriteLine("This does not test a real Transbase database.");
        return 0;
    }

    private static void Usage()
    {
        Console.WriteLine("TecDoc/Transbase table to CSV (requires official Transbase tbi)");
        Console.WriteLine("  --database <database-directory-or-URL> --table <table> --output <file.csv>");
        Console.WriteLine("  [--tbi <tbi.exe>] [--user <database-user>]");
        Console.WriteLine("  --self-test");
    }

    private sealed record Options(string Database, string Table, string Output, string Tbi, string? User);
}
