# Сборка релиза AVTOKarta: MSBuild -> ConfuserEx -> копия -> переподпись tamper.sig -> проверка.
# Запуск:  .\build-release.ps1            (полный конвейер с обфускацией, если найден .crproj)
#           .\build-release.ps1 -NoObfuscation   (только сборка + переподпись)

param(
    [switch]$NoObfuscation
)
$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.UTF8Encoding]::new()
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$msb = 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msb)) { $msb = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' }
if (-not (Test-Path $msb)) { throw 'MSBuild не найден' }

$proj    = 'AVTOKarta\AVTOKarta.csproj'
$outDir  = 'AVTOKarta\bin\Release'
$exePath = Join-Path $outDir 'AVTOKarta.exe'
$lg      = Join-Path $root 'LicGen\bin\Release\LicGen.exe'

$confuserCli = 'C:\Users\apari\AppData\Local\Temp\opencode\confuser\ext\Confuser.CLI.exe'
$crproj = Get-ChildItem -Path $root -Filter *.crproj -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $crproj) {
    foreach ($cand in @(
        'C:\Users\apari\AppData\Local\Temp\opencode\confuser\avtokarta.crproj',
        "$env:TEMP\confuser\avtokarta.crproj")) {
        if (Test-Path $cand) { $crproj = Get-Item $cand; break }
    }
}

Write-Host '==> 1. MSBuild (Release x86)'
& $msb $proj /p:Configuration=Release /p:Platform=x86 /m /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw 'Сборка завершилась с ошибками' }

$didObfuscate = $false
if (-not $NoObfuscation -and $crproj -and (Test-Path $confuserCli)) {
    Write-Host '==> 2. Обфускация ConfuserEx'
    & $confuserCli -n $crproj.FullName
    if ($LASTEXITCODE -ne 0) { throw 'Обфускация завершилась с ошибками' }
    $confused = Join-Path $outDir 'Confused\AVTOKarta.exe'
    if (Test-Path $confused) {
        Copy-Item $confused $exePath -Force
        $didObfuscate = $true
        Write-Host '    обфускация применена, exe заменён'
    } else {
        Write-Warning 'Confused\AVTOKarta.exe не найден — обфускация пропущена'
    }
} else {
    Write-Warning '==> 2. Обфускация пропущена (нет .crproj/Confuser.CLI или задан -NoObfuscation)'
}

if (-not (Test-Path $lg)) { throw 'LicGen.exe не найден: ' + $lg }

Write-Host '==> 3. Переподпись tamper.sig'
& $lg --sig-issue $exePath
if ($LASTEXITCODE -ne 0) { throw 'sig-issue не удался' }
& $lg --sig-check $exePath
if ($LASTEXITCODE -ne 0) { throw 'sig-check не пройден' }

if ($didObfuscate) { Write-Host '==> 4. Готово (обфусцировано и подписано):' } else { Write-Host '==> 3. Готово:' }
Write-Host ('    ' + (Join-Path $root $exePath))