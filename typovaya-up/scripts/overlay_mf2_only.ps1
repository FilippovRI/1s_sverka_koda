param(
    [string]$From = 'D:\typovaya-up-out-mf',
    [string]$Out = 'D:\typovaya-up-out-mf2'
)
$ErrorActionPreference = 'Continue'
$repo = Split-Path $PSScriptRoot -Parent
$log = Join-Path $Out 'overlay_mf2.log'
New-Item -ItemType Directory -Force -Path $Out | Out-Null
function Log-L([string]$Line) {
    Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Line" -Encoding UTF8
}
$oscript = (Get-Command oscript -EA SilentlyContinue).Source
if (-not $oscript) { $oscript = 'oscript' }
if (-not (Test-Path (Join-Path $Out 'summary.json'))) {
    Log-L "robocopy $From -> $Out (no restored)"
    robocopy $From $Out /E /XD restored /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
}
Log-L 'robocopy restored /MIR'
robocopy (Join-Path $From 'restored') (Join-Path $Out 'restored') /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
Log-L 'mf_overlay_restored --forms-from-up'
& $oscript (Join-Path $repo 'tasks\mf_overlay_restored.os') '--out' $Out '--forms-from-up' 2>&1 | ForEach-Object { Log-L "$_" }
Log-L "done exit=$LASTEXITCODE"
exit $LASTEXITCODE