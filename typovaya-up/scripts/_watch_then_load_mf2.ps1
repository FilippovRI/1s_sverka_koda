param(
    [string]$OutRoot = 'D:\typovaya-up-out-mf2'
)
$ErrorActionPreference = 'Continue'
$out = $OutRoot
$watchLog = Join-Path $out 'watch.log'
$exitF = Join-Path $out 'exitcode.txt'
$loadScript = Join-Path $out 'load\_load_restored_ib3.ps1'
$maxPolls = 240

function Write-WatchLog([string]$Line) {
    Add-Content -LiteralPath $watchLog -Value "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Line" -Encoding UTF8
}

function Get-LatestArtifactTime {
    $best = (Get-Date '1970-01-01')
    Get-ChildItem -LiteralPath $out -Recurse -File -EA SilentlyContinue |
        Where-Object { $_.Name -ne 'watch.log' } |
        ForEach-Object { if ($_.LastWriteTime -gt $best) { $best = $_.LastWriteTime } }
    return $best
}

Write-WatchLog "watch start watcher_pid=$PID target=$exitF out=$out"
$lastDisk = Get-LatestArtifactTime
$deadline = (Get-Date).AddHours(4)

while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $exitF) {
        $code = (Get-Content -LiteralPath $exitF -Raw).Trim()
        Write-WatchLog "job exitcode=$code"
        if ($code -eq '0') {
            Write-WatchLog "launch load: $loadScript"
            $p = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
                '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $loadScript
            ) -WorkingDirectory (Join-Path $out 'load') -PassThru -Wait
            Write-WatchLog "load finished pid=$($p.Id) exit=$($p.ExitCode)"
        } else {
            Write-WatchLog 'skip load: exitcode is not 0'
        }
        Write-WatchLog 'watch done'
        exit 0
    }
    $idleMin = [math]::Round(((Get-Date) - $lastDisk).TotalMinutes, 1)
    $newest = Get-LatestArtifactTime
    if ($newest -gt $lastDisk) { $lastDisk = $newest }
    Write-WatchLog "poll idle_min=$idleMin last=$($lastDisk.ToString('HH:mm:ss'))"
    Start-Sleep -Seconds 30
}

Write-WatchLog 'watch timeout 4h'
exit 1