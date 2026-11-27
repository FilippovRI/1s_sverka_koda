param([string]$Out = 'D:\typovaya-up-out-mf2')
$repo = Split-Path $PSScriptRoot -Parent
$build = Join-Path $repo 'scripts\build_mf2_detached.ps1'
$pidF = Join-Path $Out 'build_pid.txt'
$p = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $build
) -WorkingDirectory $Out -WindowStyle Hidden -PassThru
$p.Id | Set-Content -Encoding ASCII $pidF
Write-Output "build_pid=$($p.Id) log=$Out\build_mf2.log"