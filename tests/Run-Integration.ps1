$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mainProject = Join-Path $root 'tecdoc-transbase-csv.csproj'
$fakeProject = Join-Path $PSScriptRoot 'FakeTbi/FakeTbi.csproj'
$mainExe = Join-Path $root 'bin/Release/net8.0/tecdoc-transbase-csv.exe'
$fakeExe = Join-Path $PSScriptRoot 'FakeTbi/bin/Release/net8.0/FakeTbi.exe'

dotnet build $mainProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать основную программу.' }
dotnet build $fakeProject -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Не удалось собрать имитатор tbi.' }

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('transbase-csv-test-' + [guid]::NewGuid().ToString('N'))
$database = Join-Path $testRoot 'database'
$output = Join-Path $testRoot 'articles_linkages.csv'
[IO.Directory]::CreateDirectory($database) | Out-Null

try {
    & $mainExe --database $database --table articles_linkages --output $output --tbi $fakeExe
    if ($LASTEXITCODE -ne 0) { throw 'Программа не выполнила экспорт через имитатор.' }
    $expected = "1001;2001;`"Легковой автомобиль`"`n1002;2002;`"Грузовик`"`n"
    $actual = [IO.File]::ReadAllText($output, [Text.Encoding]::UTF8)
    if ($actual -cne $expected) { throw 'Содержимое CSV отличается от ожидаемого.' }

    & $mainExe --database $database --table articles_linkages --output $output --tbi $fakeExe 2>$null
    if ($LASTEXITCODE -eq 0) { throw 'Программа перезаписала существующий CSV.' }
    if ([IO.File]::ReadAllText($output, [Text.Encoding]::UTF8) -cne $expected) {
        throw 'Исходный CSV изменён после отказа от перезаписи.'
    }

    $failureOutput = Join-Path $testRoot 'failure.csv'
    $env:FAKE_TBI_FAIL = '1'
    try {
        & $mainExe --database $database --table articles_linkages --output $failureOutput --tbi $fakeExe 2>$null
        if ($LASTEXITCODE -eq 0 -or (Test-Path $failureOutput)) {
            throw 'Ошибка tbi не была обработана.'
        }
    }
    finally { Remove-Item Env:FAKE_TBI_FAIL -ErrorAction SilentlyContinue }

    Write-Host 'Интеграционная проверка пройдена (имитатор tbi; BDF не проверялся).'
}
finally { Remove-Item -LiteralPath $testRoot -Recurse -Force }
