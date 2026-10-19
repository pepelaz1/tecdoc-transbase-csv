using System.Text;
using System.Text.RegularExpressions;

// Только имитатор интерфейса tbi для проверки .NET-обёртки. Файлы BDF не читает.
if (Environment.GetEnvironmentVariable("FAKE_TBI_FAIL") == "1") return 17;

if (args.Length is < 4 or > 5 || args[0] != "-s" || args[1] != "-f") return 2;
if (!args[3].StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return 3;

var sql = await File.ReadAllTextAsync(args[2]);
var match = Regex.Match(sql,
    "^SPOOL INTO '(?<path>(?:''|[^'])+)' FORMAT DSV CODEPAGE UTF8 DELIM ';' QUOTE '\"' SELECT \\* FROM articles_linkages;\\s*$",
    RegexOptions.CultureInvariant);
if (!match.Success) return 4;

var output = match.Groups["path"].Value.Replace("''", "'", StringComparison.Ordinal);
await File.WriteAllTextAsync(output,
    "1001;2001;\"Легковой автомобиль\"\n1002;2002;\"Грузовик\"\n",
    new UTF8Encoding(false));
return 0;
