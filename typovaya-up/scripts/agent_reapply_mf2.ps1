param(
    [string]$From = 'D:\typovaya-up-out-mf',
    [string]$Out = 'D:\typovaya-up-out-mf2'
)
$ErrorActionPreference = 'Continue'
$repo = Split-Path $PSScriptRoot -Parent
$log = Join-Path $Out 'agent_reapply.log'
New-Item -ItemType Directory -Force -Path $Out | Out-Null
function Log-A([string]$Line) {
    Add-Content -LiteralPath $log -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Line" -Encoding UTF8
    Write-Host $Line
}
if (Test-Path $Out) {
    Get-ChildItem -LiteralPath $Out -Force | Remove-Item -Recurse -Force -EA SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $Out | Out-Null
Log-A "robocopy $From -> $Out (no restored)"
robocopy $From $Out /E /XD restored load /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
$oscript = (Get-Command oscript -EA SilentlyContinue).Source
if (-not $oscript) { $oscript = 'oscript' }
if (-not (Test-Path (Join-Path $Out 'restored\Configuration.xml'))) {
    Log-A 'robocopy restored only'
    robocopy (Join-Path $From 'restored') (Join-Path $Out 'restored') /E /NFL /NDL /NJH /NJS | Out-Null
}
Log-A 'oscript mf_overlay_restored'
& $oscript (Join-Path $repo 'tasks\mf_overlay_restored.os') '--out' $Out 2>&1 | Tee-Object -FilePath (Join-Path $Out 'reapply_oscript.log')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
New-Item -ItemType Directory -Force -Path (Join-Path $Out 'load') | Out-Null
Copy-Item (Join-Path $repo 'scripts\_load_restored_ib3.ps1') (Join-Path $Out 'load\_load_restored_ib3.ps1') -Force
$watch = Join-Path $Out '_watch_then_load.ps1'
Copy-Item (Join-Path $repo 'scripts\_watch_then_load_mf2.ps1') $watch -Force
'0' | Set-Content -Encoding ASCII (Join-Path $Out 'exitcode.txt')
$w = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
    '-NoProfile','-ExecutionPolicy','Bypass','-File',$watch,'-OutRoot',$Out
) -WorkingDirectory $Out -PassThru
Log-A "watcher_pid=$($w.Id)"
$w.Id | Set-Content -Encoding ASCII (Join-Path $Out 'watcher_pid.txt')
exit 0