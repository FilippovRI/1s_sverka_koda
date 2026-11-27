param(
    [string]$OutRoot = 'D:\typovaya-up-out-mf2',
    [switch]$WithExtension,
    [switch]$CheckModules
)
$ErrorActionPreference = 'Continue'
$bin = 'C:\Program Files\1cv8\8.5.1.1150\bin'
$ibcmd = Join-Path $bin 'ibcmd.exe'
$v8 = Join-Path $bin '1cv8.exe'
$repo = Split-Path $PSScriptRoot -Parent
$tmp = 'D:\typovaya-up-tmp-check'
$db = Join-Path $tmp 'ib'
$data = Join-Path $tmp 'ibcmd-data'
$log = Join-Path $tmp 'import_restored.log'
$src = Join-Path $OutRoot 'restored'
New-Item -ItemType Directory -Force -Path $tmp, $data | Out-Null
if (Test-Path $db) { Remove-Item -LiteralPath $db -Recurse -Force -EA SilentlyContinue }
& $ibcmd infobase create "--db-path=$db" "--data=$data" 2>&1 | Out-Null
$logLines = @()
& $ibcmd infobase config import "--db-path=$db" "--data=$data" $src 2>&1 | ForEach-Object { $logLines += $_ }
$code = $LASTEXITCODE
$logLines | Set-Content -LiteralPath $log -Encoding UTF8
$oscript = (Get-Command oscript -EA SilentlyContinue).Source
if (-not $oscript) { $oscript = 'oscript' }
& $oscript (Join-Path $repo 'src\СводкаОшибокImport.os') $log
if ($code -eq 0) {
    & $ibcmd infobase config apply "--db-path=$db" "--data=$data" --force 2>&1 | Add-Content -LiteralPath $log -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { $code = $LASTEXITCODE }
}
$ext = Join-Path $OutRoot 'extension'
$extName = 'УП_Доработки'
if ($WithExtension -and $code -eq 0 -and (Test-Path (Join-Path $ext 'Configuration.xml'))) {
    Add-Content -LiteralPath $log -Value '--- extension import ---' -Encoding UTF8
    & $ibcmd infobase config import "--db-path=$db" "--data=$data" "--extension=$extName" $ext 2>&1 | Add-Content -LiteralPath $log -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { $code = $LASTEXITCODE }
    if ($code -eq 0) {
        & $ibcmd infobase config apply "--db-path=$db" "--data=$data" "--extension=$extName" --force 2>&1 | Add-Content -LiteralPath $log -Encoding UTF8
        if ($LASTEXITCODE -ne 0) { $code = $LASTEXITCODE }
    }
}
$checkOut = Join-Path $tmp 'check_modules.txt'
if ($CheckModules -and $code -eq 0 -and (Test-Path $v8)) {
    Add-Content -LiteralPath $log -Value '--- CheckModules ---' -Encoding UTF8
    & $v8 DESIGNER "/F$db" '/DisableStartupMessages' '/CheckModules' "-Out$checkOut" 2>&1 | Add-Content -LiteralPath $log -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { $code = $LASTEXITCODE }
    Add-Content -LiteralPath $log -Value "CheckModules exit=$LASTEXITCODE out=$checkOut" -Encoding UTF8
}
Remove-Item -LiteralPath $db, $data -Recurse -Force -EA SilentlyContinue
Write-Host "temp import restored exit=$code log=$log"
if ($CheckModules -and (Test-Path $checkOut)) { Write-Host "check_modules=$checkOut" }
exit $code