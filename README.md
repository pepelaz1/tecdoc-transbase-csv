# TecDoc Transbase table to CSV

A small .NET 8 console wrapper around the **official Transbase `tbi`** exporter. It exports one SQL table to UTF-8, semicolon-delimited DSV/CSV without loading the whole result into .NET memory.

## Important limitation

This does **not** parse a standalone `.bdf` binary. TecDoc's Transbase data must be opened as a complete database with a compatible Transbase installation. The database directory, and possibly its configuration, indexes, credentials and matching Transbase version, are required.

No public TecDoc Transbase sample database was available during development. `--self-test` checks script generation and input validation only; it **does not prove** that table 400 can be exported from the customer's data. A real acceptance test needs a legally shareable sample or access to the customer's full database directory and installed Transbase runtime. The project's “articles_linkages / table 400” names may describe different schemas; inspect the actual table list first.

## Build and run

```powershell
dotnet build -c Release
dotnet run -c Release -- --self-test
dotnet run -c Release -- --database "C:\TecDoc\Database" --table articles_linkages --output "C:\Exports\articles_linkages.csv" --tbi "C:\Transbase\bin\tbi.exe"
```

For a Transbase server, `--database "//localhost:2024/tecdoc"` can be used instead. `--user` is optional; `tbi` can prompt for credentials in the terminal. No password is accepted on the command line, to avoid exposing it in process lists. The tool refuses to overwrite an existing output file. It writes to a temporary file and renames it only after `tbi` reports success.

The Transbase DSV spooler does not generate a column-name header. If a downstream importer requires a header, confirm the actual column names and add it in a separate step. The export must be checked against the customer's expected TecDoc CSV layout before delivery.

## Sources

- [Transbase TBI command-line reference](https://www.transaction.de/fileadmin/public/transbase/8.3/docu/tbi.pdf): `-s`, `-f`, database URL and exit status.
- [Transbase SQL reference, DSV spooler](https://www.transaction.de/transbase/doku-v83/sql-reference-manual.html): `SPOOL INTO`, UTF-8, delimiters, quoting.
- [Transbase system guide](https://www.transaction.de/transbase/doku-v83/system-guide.html): private databases consist of multiple files and are addressed by a database directory.
- [TecDoc data format 2.7](https://tec-doc-services.com/download/TecDoc-Data-Format_Version_2.7_EN.pdf): table 400 is “Article Linkage”; exchange CSV uses semicolons and UTF-8. This exchange specification does not define a Transbase database schema.

No TecDoc database files or customer data are included.
