param(
    [string]$OutRoot = 'D:\typovaya-up-out-mf2'
)
$ErrorActionPreference = 'Continue'
$bin = 'C:\Program Files\1cv8\8.5.1.1150\bin'
$ibcmd = Join-Path $bin 'ibcmd.exe'
$v8 = Join-Path $bin '1cv8.exe'
$db = 'C:\Users\rst_filippov\Documents\InfoBase3'
$data = 'D:\typovaya-up-ibcmd-data'
$src = Join-Path $OutRoot 'restored'
$ext = Join-Path $OutRoot 'extension'
$loadDir = Join-Path $OutRoot 'load'
$log = Join-Path $loadDir 'load_restored.log'
$exitF = Join-Path $loadDir 'exitcode.txt'
$checkOut = Join-Path $loadDir 'check_modules.txt'
$extName = 'УП_Доработки'
New-Item -ItemType Directory -Force -Path $loadDir | Out-Null

function Log-L([string]$Line) {
    Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Line" -Encoding UTF8
}

if (-not (Test-Path (Join-Path $src 'Configuration.xml'))) {
    Log-L 'ERROR: нет restored\Configuration.xml'
    '1' | Set-Content -Encoding ASCII $exitF
    exit 1
}

$code = 0
try {
    Log-L "import restored -> $db"
    & $ibcmd infobase config import "--db-path=$db" "--data=$data" $src 2>&1 | ForEach-Object { Log-L "$_" }
    if ($LASTEXITCODE -ne 0) { throw "import exit=$LASTEXITCODE" }
    Log-L 'apply restored'
    & $ibcmd infobase config apply "--db-path=$db" "--data=$data" --force 2>&1 | ForEach-Object { Log-L "$_" }
    if ($LASTEXITCODE -ne 0) { throw "apply exit=$LASTEXITCODE" }

    if (Test-Path (Join-Path $ext 'Configuration.xml')) {
        Log-L "extension import $extName"
        & $ibcmd infobase config import "--db-path=$db" "--data=$data" "--extension=$extName" $ext 2>&1 | ForEach-Object { Log-L "$_" }
        if ($LASTEXITCODE -ne 0) { throw "ext import exit=$LASTEXITCODE" }
        Log-L 'extension apply'
        & $ibcmd infobase config apply "--db-path=$db" "--data=$data" "--extension=$extName" --force 2>&1 | ForEach-Object { Log-L "$_" }
        if ($LASTEXITCODE -ne 0) { throw "ext apply exit=$LASTEXITCODE" }
    }

    Log-L "CheckModules /Out $checkOut"
    $arg = @(
        'DESIGNER',
        "/F$db",
        '/DisableStartupMessages',
        '/CheckModules',
        "-Out$checkOut"
    )
    & $v8 @arg 2>&1 | ForEach-Object { Log-L "v8: $_" }
    $cv = $LASTEXITCODE
    Log-L "CheckModules exit=$cv"
    if ($cv -ne 0) { $code = $cv }
} catch {
    Log-L "ERROR: $_"
    if ($code -eq 0) { $code = 1 }
    if ($LASTEXITCODE) { $code = $LASTEXITCODE }
}
$code | Set-Content -Encoding ASCII $exitF
Log-L "finished exitcode=$code"
exit $code