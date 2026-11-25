# Проверка выгрузок typovaya-up через 1cedtcli (без загрузки в ИБ и CheckModules).
# Не трогает D:\EDT-projects\УП, эталонные load-папки и исходные выгрузки.
# Требует Java 25 (axiom-jdk из 1CE) и ≥ MinFreeGB свободно на D:.
param(
    [ValidateSet('Version', 'Calibrate', 'OverlayPackage', 'ValidateOnly')]
    [string]$Mode = 'Calibrate',
    [string]$Workspace = 'D:\typovaya-up-edt-ws',
    [string]$WorkRoot = 'D:\typovaya-up-edt-tmp',
    [string]$CalibRestored = 'D:\typovaya-up-forms-cm-classa\load\restored',
    [string]$CalibExtension = 'D:\typovaya-up-forms-cm-classa\load\extension',
    [string]$PackageRestored = 'D:\typovaya-up-forms-cm-uprest\restored',
    [string]$PackageExtension = 'D:\typovaya-up-forms-cm-uprest\extension',
    [string]$ProjectName = 'typovaya-up',
    [string]$ExtProjectName = 'UP_Dorabotki',
    [string]$PlatformVersion = '8.5.1',
    [string]$Xmx = '4096m',
    [int]$TimeoutSec = 14400,
    [double]$MinFreeGB = 15,
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$UserEdtWorkspace = 'D:\EDT-projects\УП'

function Get-DFreeGB {
    return [math]::Round((Get-PSDrive D).Free / 1GB, 2)
}

function Assert-Disk([string]$Step) {
    $free = Get-DFreeGB
    if ($free -lt $MinFreeGB) {
        throw "STOP: на D: свободно $free ГБ (нужно ≥ $MinFreeGB). Шаг: $Step"
    }
    Write-Host "D free=${free}GB ($Step)"
}

function Find-EdtCli {
    $roots = @(
        (Join-Path $env:LOCALAPPDATA '1C\1cedtstart\installations'),
        'C:\Program Files\1C\1CE'
    )
    $found = @()
    foreach ($r in $roots) {
        if (Test-Path $r) {
            $found += Get-ChildItem $r -Recurse -Filter '1cedtcli.exe' -ErrorAction SilentlyContinue
        }
    }
    if (-not $found) { throw '1cedtcli.exe не найден. ring не установлен, CLI только из EDT.' }
    return ($found | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}

function Find-AxiomJava {
    $root = 'C:\Program Files\1C\1CE\components'
    $j = Get-ChildItem $root -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'axiom-jdk-full-25*' } |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if (-not $j) { throw 'Axiom JDK 25 не найден в C:\Program Files\1C\1CE\components' }
    $vm = Join-Path $j.FullName 'bin\javaw.exe'
    if (-not (Test-Path $vm)) { throw "нет $vm" }
    return $vm
}

function Write-EdtIni([string]$CliPath, [string]$VmPath, [string]$DestIni) {
    $installIni = Join-Path (Split-Path $CliPath -Parent) '1cedt.ini'
    if (-not (Test-Path $installIni)) { throw "нет $installIni" }
    $lines = Get-Content -LiteralPath $installIni
    $out = New-Object System.Collections.Generic.List[string]
    $inserted = $false
    foreach ($line in $lines) {
        if (-not $inserted -and $line -eq '-vmargs') {
            $out.Add('-vm')
            $out.Add($VmPath)
            $inserted = $true
        }
        $out.Add($line)
    }
    if (-not $inserted) { throw 'в 1cedt.ini нет секции -vmargs, некуда вставить -vm' }
    $out | Set-Content -LiteralPath $DestIni -Encoding ASCII
    Write-Host "ini=$DestIni vm=$VmPath"
}

function Invoke-EdtCli {
    param(
        [string]$Cli,
        [string]$Ini,
        [string]$Data,
        [string[]]$Command,
        [string]$LogOut,
        [int]$Timeout = $TimeoutSec
    )
    Assert-Disk ("before: " + ($Command -join ' '))
    $arg = @('-data', $Data, '-timeout', "$Timeout", '-nl', 'ru_RU', '-ini-file', $Ini, '-vmargs', "-Xmx$Xmx", '-command') + $Command
    Write-Host ("EDT: 1cedtcli " + ($arg -join ' '))
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $Cli -ArgumentList $arg -Wait -PassThru -NoNewWindow `
        -RedirectStandardOutput $LogOut `
        -RedirectStandardError ($LogOut + '.err')
    $sw.Stop()
    Write-Host ("EDT exit=$($p.ExitCode) elapsed_sec=$([int]$sw.Elapsed.TotalSeconds) log=$LogOut")
    Assert-Disk ("after: " + ($Command -join ' '))
    if ($p.ExitCode -ne 0) {
        if (Test-Path ($LogOut + '.err')) { Get-Content ($LogOut + '.err') -Tail 40 }
        if (Test-Path $LogOut) { Get-Content $LogOut -Tail 40 }
        throw "1cedtcli exit=$($p.ExitCode) cmd=$($Command -join ' ')"
    }
}

function Start-DiskWatchdog([string]$PidFile, [string]$WatchLog) {
    $self = $PID
    $code = @"
`$min = $MinFreeGB
`$log = '$WatchLog'
`$parent = $self
while (Get-Process -Id `$parent -ErrorAction SilentlyContinue) {
    `$free = [math]::Round((Get-PSDrive D).Free / 1GB, 2)
    `$ws = 0
    if (Test-Path -LiteralPath '$Workspace') {
        `$ws = [math]::Round(((Get-ChildItem -LiteralPath '$Workspace' -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1GB), 2)
    }
    Add-Content -LiteralPath `$log -Encoding UTF8 -Value ("`$(Get-Date -Format 'HH:mm:ss') free=`$free GB workspace=`$ws GB")
    if (`$free -lt `$min) {
        Add-Content -LiteralPath `$log -Encoding UTF8 -Value "STOP: D free `$free GB < `$min GB — останавливаю свой 1cedtcli"
        Get-CimInstance Win32_Process | Where-Object {
            `$_.Name -match '1cedtcli|1cedtc' -or (`$_.Name -eq 'javaw.exe' -and `$_.CommandLine -match 'typovaya-up-edt-ws')
        } | ForEach-Object {
            Add-Content -LiteralPath `$log -Encoding UTF8 -Value ("kill `$(`$_.ProcessId) `$(`$_.Name)")
            Stop-Process -Id `$_.ProcessId -Force -ErrorAction SilentlyContinue
        }
        break
    }
    Start-Sleep -Seconds 30
}
"@
    $wd = Join-Path $WorkRoot 'disk_watchdog.ps1'
    Set-Content -LiteralPath $wd -Value $code -Encoding UTF8
    $p = Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $wd) -WindowStyle Hidden -PassThru
    $p.Id | Set-Content -LiteralPath $PidFile -Encoding ASCII
    Write-Host "watchdog pid=$($p.Id) log=$WatchLog"
    return $p
}

# --- main ---
if ((Resolve-Path -LiteralPath $Workspace -ErrorAction SilentlyContinue) -and
    ((Resolve-Path $Workspace).Path.TrimEnd('\') -ieq $UserEdtWorkspace)) {
    throw "Отказ: workspace совпадает с проектом пользователя $UserEdtWorkspace"
}

New-Item -ItemType Directory -Force -Path $Workspace, $WorkRoot | Out-Null
Assert-Disk 'start'
$cli = Find-EdtCli
$vm = Find-AxiomJava
$ini = Join-Path $WorkRoot '1cedt-cli.ini'
Write-EdtIni -CliPath $cli -VmPath $vm -DestIni $ini
Write-Host "cli=$cli"
Write-Host "mode=$Mode platform=$PlatformVersion xmx=$Xmx"

$proj = Join-Path $Workspace $ProjectName
$extProj = Join-Path $Workspace $ExtProjectName
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$watchPid = Join-Path $WorkRoot 'watchdog.pid'
$watchLog = Join-Path $WorkRoot "watchdog-$stamp.log"
$wdProc = $null

try {
    if ($Mode -ne 'Version') { $wdProc = Start-DiskWatchdog $watchPid $watchLog }

    if ($Mode -eq 'Version') {
        Invoke-EdtCli -Cli $cli -Ini $ini -Data $Workspace -Command @('version') -LogOut (Join-Path $WorkRoot 'cli_version.out') -Timeout 300
        Invoke-EdtCli -Cli $cli -Ini $ini -Data $Workspace -Command @('platform-versions') -LogOut (Join-Path $WorkRoot 'cli_platform_versions.out') -Timeout 300
        Write-Host 'OK Version'
        Get-Content (Join-Path $WorkRoot 'cli_version.out') -Tail 5
        Get-Content (Join-Path $WorkRoot 'cli_platform_versions.out') -Tail 8
        return
    }

    $build = @()
    if (-not $SkipBuild) { $build = @('--build', 'true') }

    if ($Mode -eq 'Calibrate') {
        if (-not (Test-Path (Join-Path $CalibRestored 'Configuration.xml'))) {
            throw "нет калибровочной выгрузки: $CalibRestored"
        }
        if (-not (Test-Path (Join-Path $CalibExtension 'Configuration.xml'))) {
            throw "нет калибровочного расширения: $CalibExtension"
        }
        Invoke-EdtCli -Cli $cli -Ini $ini -Data $Workspace `
            -Command (@('import', '--configuration-files', $CalibRestored, '--project-name', $ProjectName, '--version', $PlatformVersion) + $build) `
            -LogOut (Join-Path $WorkRoot "import-main-$stamp.log")
        Invoke-EdtCli -Cli $cli -Ini $ini -Data $Workspace `
            -Command (@('import', '--configuration-files', $CalibExtension, '--project-name', $ExtProjectName, '--base-project-name', $ProjectName, '--version', $PlatformVersion) + $build) `
            -LogOut (Join-Path $WorkRoot "import-ext-$stamp.log")
    }
    elseif ($Mode -eq 'OverlayPackage') {
        if (-not (Test-Path $proj)) { throw "сначала Calibrate, нет проекта $proj" }
        Invoke-EdtCli -Cli $cli -Ini $ini -Data $Workspace `
            -Command (@('import', '--configuration-files', $PackageRestored, '--project-name', $ProjectName, '--version', $PlatformVersion) + $build) `
            -LogOut (Join-Path $WorkRoot "overlay-main-$stamp.log")
        Invoke-EdtCli -Cli $cli -Ini $ini -Data $Workspace `
            -Command (@('import', '--configuration-files', $PackageExtension, '--project-name', $ExtProjectName, '--base-project-name', $ProjectName, '--version', $PlatformVersion) + $build) `
            -LogOut (Join-Path $WorkRoot "overlay-ext-$stamp.log")
    }

    # 1cedtcli 2026.2 принимает только одно имя после --project-name-list.
    $tsvList = @()
    foreach ($name in @($ProjectName, $ExtProjectName)) {
        $tsv = Join-Path $WorkRoot "validate-$Mode-$name-$stamp.tsv"
        Invoke-EdtCli -Cli $cli -Ini $ini -Data $Workspace `
            -Command @('validate', '--file', $tsv, '--project-name-list', $name) `
            -LogOut (Join-Path $WorkRoot "validate-$Mode-$name-$stamp.log")
        Write-Host "validate tsv=$tsv"
        if (Test-Path $tsv) { $tsvList += $tsv }
    }
    $cmp = Join-Path $PSScriptRoot 'compare_edt_checkmodules.ps1'
    if ((Test-Path $cmp) -and $tsvList.Count -gt 0) {
        foreach ($tsv in $tsvList) {
            & $cmp -EdtTsv $tsv -Mode $Mode -OutDir $WorkRoot
        }
    }
    Write-Host "OK $Mode free=$(Get-DFreeGB)GB"
}
finally {
    if ($wdProc -and -not $wdProc.HasExited) {
        Stop-Process -Id $wdProc.Id -Force -ErrorAction SilentlyContinue
    }
}
