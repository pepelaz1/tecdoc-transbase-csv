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
                throw new ArgumentException($"Выходной файл уже существует: {output}");

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

                Console.WriteLine($"Экспорт таблицы {o.Table} из {database}...");
                Console.WriteLine("Transbase tbi может запросить учётные данные в этом терминале.");
                using var process = Process.Start(start)
                    ?? throw new InvalidOperationException("Не удалось запустить Transbase tbi.");
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Ошибка Transbase tbi: код выхода {process.ExitCode}.");
                if (!File.Exists(csv))
                    throw new InvalidOperationException("Transbase tbi не создал CSV-файл.");
                File.Move(csv, output);
                Console.WriteLine($"Сохранено: {output} ({new FileInfo(output).Length:N0} байт)");
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
            Console.Error.WriteLine($"Ошибка: {ex.Message}");
            return 1;
        }
    }

    private static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Ожидаются пары --параметр значение. Используйте --help.");
            if (!values.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException($"Параметр указан повторно: {args[i]}");
        }
        foreach (var key in values.Keys)
            if (key is not ("--database" or "--table" or "--output" or "--tbi" or "--user"))
                throw new ArgumentException($"Неизвестный параметр: {key}");
        if (!values.TryGetValue("--database", out var db) ||
            !values.TryGetValue("--table", out var table) ||
            !values.TryGetValue("--output", out var output))
            throw new ArgumentException("Обязательные параметры: --database, --table и --output.");
        if (!SafeName.IsMatch(table))
            throw new ArgumentException("--table должен содержать имя SQL-таблицы, возможно с префиксом схемы.");
        if (string.IsNullOrWhiteSpace(db) || string.IsNullOrWhiteSpace(output))
            throw new ArgumentException("Пути к базе и выходному файлу не могут быть пустыми.");
        return new(db, table, output, values.GetValueOrDefault("--tbi") ?? "tbi",
            values.GetValueOrDefault("--user"));
    }

    private static string DatabaseUrl(string input)
    {
        if (input.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("//", StringComparison.Ordinal)) return input;
        if (Path.GetExtension(input).Equals(".bdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Отдельный .bdf не является полной базой. Укажите каталог базы Transbase целиком.");
        var path = Path.GetFullPath(input);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Каталог базы не найден: {path}");
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
            throw new InvalidOperationException("Ошибка генерации SQL-скрипта.");
        try
        {
            Parse(["--database", ".", "--table", "x;DROP TABLE y", "--output", "x.csv"]);
            throw new InvalidOperationException("Принято недопустимое имя таблицы.");
        }
        catch (ArgumentException) { }
        Console.WriteLine("Самопроверка пройдена: генерация скрипта и проверка имени таблицы.");
        Console.WriteLine("Настоящая база Transbase при этом не проверяется.");
        return 0;
    }

    private static void Usage()
    {
        Console.WriteLine("Экспорт таблицы TecDoc/Transbase в CSV (нужна штатная утилита Transbase tbi)");
        Console.WriteLine("  --database <database-directory-or-URL> --table <table> --output <file.csv>");
        Console.WriteLine("  [--tbi <tbi.exe>] [--user <database-user>]");
        Console.WriteLine("  --self-test");
    }

    private sealed record Options(string Database, string Table, string Output, string Tbi, string? User);
}
