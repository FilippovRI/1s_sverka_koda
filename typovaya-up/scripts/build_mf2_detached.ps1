param(
    [string]$From = 'D:\typovaya-up-out-mf',
    [string]$Out = 'D:\typovaya-up-out-mf2'
)
$ErrorActionPreference = 'Continue'
$repo = Split-Path $PSScriptRoot -Parent
$log = Join-Path $Out 'build_mf2.log'
New-Item -ItemType Directory -Force -Path $Out | Out-Null
function Log-B([string]$Line) {
    Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Line" -Encoding UTF8
}
Get-Process oscript -EA SilentlyContinue | ForEach-Object {
    Log-B "stop oscript pid=$($_.Id)"
    Stop-Process -Id $_.Id -Force -EA SilentlyContinue
}
Log-B 'robocopy restored /MIR from mf'
robocopy (Join-Path $From 'restored') (Join-Path $Out 'restored') /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
$oscript = (Get-Command oscript -EA SilentlyContinue).Source
if (-not $oscript) { $oscript = 'oscript' }
Log-B 'mf_overlay_restored --forms-from-up'
& $oscript (Join-Path $repo 'tasks\mf_overlay_restored.os') '--out' $Out '--forms-from-up' 2>&1 | ForEach-Object { Log-B "$_" }
$ov = $LASTEXITCODE
if ($ov -ne 0) {
    Log-B "overlay failed exit=$ov"
    exit $ov
}
Log-B "overlay pipeline finished exit=$LASTEXITCODE"
exit $LASTEXITCODE