# Экспорт таблицы TecDoc / Transbase в CSV

Небольшая консольная программа на .NET 8 запускает штатную утилиту **Transbase `tbi`** и выгружает одну SQL-таблицу в UTF-8 CSV с разделителем `;`. Результат формирует сама Transbase без загрузки всей таблицы в память .NET.

## Что требуется

- Установленная совместимая версия Transbase с `tbi`.
- Полный каталог базы данных, а не один файл `.bdf`, либо адрес работающего сервера Transbase.
- Имя таблицы в реальной схеме базы. Обозначение TecDoc «таблица 400» и имя `articles_linkages` ещё нужно сопоставить с конкретной базой.

**Пока нет подтверждения работы на базе TecDoc.** Открытый образец Transbase `.bdf` найти не удалось, установленной Transbase в среде разработки нет. Произвольный файл с расширением `.bdf` не является настоящей базой и не годится для такой проверки. Проверки ниже подтверждают только сборку, генерацию команды и взаимодействие с тестовым имитатором `tbi`. Для приёмки нужен совместимый комплект базы TecDoc и Transbase.

## Сборка и запуск

```powershell
dotnet build -c Release
dotnet run -c Release -- --self-test
dotnet run -c Release -- --database "C:\TecDoc\Database" --table articles_linkages --output "C:\Exports\articles_linkages.csv" --tbi "C:\Transbase\bin\tbi.exe"
```

Для подключения к серверу можно указать `--database "//localhost:2024/tecdoc"`. Параметр `--user` необязателен; `tbi` может запросить пароль в терминале. Программа не принимает пароль в командной строке, чтобы не показывать его в списке процессов.

Существующий выходной файл не перезаписывается. Экспорт сначала выполняется во временный файл рядом с целевым, и только после успешного завершения `tbi` файл переименовывается. Выгрузка DSV не содержит строку с названиями столбцов. Перед передачей результата нужно проверить схему, число строк, кодировку и ожидаемый формат CSV заказчика.

## Проверки

```powershell
dotnet run -c Release -- --self-test
powershell -ExecutionPolicy Bypass -File tests/Run-Integration.ps1
```

Вторая команда запускает приложение с **имитатором `tbi`**, который создаёт маленький CSV без чтения `.bdf`. Она проверяет запуск внешней программы, передачу SQL-скрипта, создание и перенос CSV, защиту от перезаписи и обработку ошибок. Это **не** тест совместимости с TecDoc или настоящей Transbase.

## Документация формата и инструментов

- [Transbase TBI: параметры `-s`, `-f` и код завершения](https://www.transaction.de/fileadmin/public/transbase/8.3/docu/tbi.pdf).
- [Transbase SQL: `SPOOL INTO`, DSV и UTF-8](https://www.transaction.de/en/transbase/doku-v83/sql-reference-manual.html).
- [Transbase System Guide: каталог частной базы и её файлы](https://www.transaction.de/transbase/doku-v83/system-guide.html).
- [TecDoc Data Format 2.7: таблица 400 — Article Linkage](https://tec-doc-services.com/download/TecDoc-Data-Format_Version_2.7_EN.pdf). Это описание обменного формата, а не схемы конкретной базы Transbase.

Файлы TecDoc и данные заказчика в репозиторий не включены.
