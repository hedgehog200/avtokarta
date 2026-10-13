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
if ($NoObfuscation) {
    Write-Warning '==> 2. Обфускация пропущена (задан -NoObfuscation)'
}
elseif (-not $crproj) {
    # Отсутствие профиля/CLI раньше приводило к ТИХОМУ пропуску обфускации —
    # можно было выпустить незащищённый exe, не заметив. Теперь падаем явно.
    throw ('Профиль обфускации .crproj не найден. Ожидается avtokarta.crproj в репозитории ' +
           'или %TEMP%\confuser\avtokarta.crproj. Либо запустите с -NoObfuscation.')
}
elseif (-not (Test-Path $confuserCli)) {
    throw ('Confuser.CLI.exe не найден: ' + $confuserCli + '. ' +
           'Скачайте ConfuserEx-CLI 1.6.0 (github.com/mkaring/ConfuserEx/releases) и распакуйте в ' +
           (Split-Path $confuserCli) + ', либо запустите с -NoObfuscation.')
}
else {
    Write-Host '==> 2. Обфускация ConfuserEx'
    & $confuserCli -n $crproj.FullName
    if ($LASTEXITCODE -ne 0) { throw 'Обфускация завершилась с ошибками' }
    $confused = Join-Path $outDir 'Confused\AVTOKarta.exe'
    if (-not (Test-Path $confused)) { throw ('Confused\AVTOKarta.exe не найден после обфускации: ' + $confused) }
    Copy-Item $confused $exePath -Force
    $didObfuscate = $true
    Write-Host '    обфускация применена, exe заменён'
}

if (-not (Test-Path $lg)) { throw 'LicGen.exe не найден: ' + $lg }

Write-Host '==> 3. Переподпись tamper.sig'
# LicGen — WinExe: оператор '&' не дожидается завершения и не возвращает код,
# поэтому подпись могла ещё идти, когда скрипт уже писал «Готово». Запускаем
# через Start-Process -Wait и проверяем реальный ExitCode.
$pIssue = Start-Process -FilePath $lg -ArgumentList "--sig-issue `"$exePath`"" -Wait -PassThru -NoNewWindow
if ($pIssue.ExitCode -ne 0) { throw ('sig-issue не удался (код ' + $pIssue.ExitCode + ')') }
$pCheck = Start-Process -FilePath $lg -ArgumentList "--sig-check `"$exePath`"" -Wait -PassThru -NoNewWindow
if ($pCheck.ExitCode -ne 0) { throw ('sig-check не пройден (код ' + $pCheck.ExitCode + ')') }

if ($didObfuscate) { Write-Host '==> 4. Готово (обфусцировано и подписано):' } else { Write-Host '==> 3. Готово:' }
Write-Host ('    ' + (Join-Path $root $exePath))