# Загрузка restored+extension со срезом «только модуль» во временную базу D:\typovaya-up-tmp-forms.
# Источник restored/extension — копия D:\typovaya-up-out-mf2 (только чтение) в proto\load\.
# Не трогает typovaya-up-out-mf2, typovaya-up-tmp-check, исходные выгрузки и InfoBase3.
param(
  [string]$ProtoRoot = 'D:\typovaya-up-forms-proto',
  [string]$Mf2Root = 'D:\typovaya-up-out-mf2',
  [string]$TmpRoot = 'D:\typovaya-up-tmp-forms',
  [string]$ExtName = 'УП_Доработки'
)
$ErrorActionPreference = 'Continue'
$bin = 'C:\Program Files\1cv8\8.5.1.1150\bin'
$ibcmd = Join-Path $bin 'ibcmd.exe'
$v8 = Join-Path $bin '1cv8.exe'
$oscript = 'C:\Users\rst_filippov\OneScript\bin\oscript.exe'
$repoScripts = Split-Path -Parent $MyInvocation.MyCommand.Path
$overlayOs = Join-Path $repoScripts 'overlay_forms_bp_proto.os'
$log = Join-Path $TmpRoot 'load.log'
$loadRest = Join-Path $ProtoRoot 'load\restored'
$loadExt = Join-Path $ProtoRoot 'load\extension'
$db = Join-Path $TmpRoot 'ib'
$data = Join-Path $TmpRoot 'ibcmd-data'

function L([string]$m) {
  $s = '[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss'), $m
  Add-Content -LiteralPath $log -Value $s -Encoding UTF8
  Write-Host $s
}

New-Item -ItemType Directory -Force -Path $TmpRoot | Out-Null
Set-Content -LiteralPath $log -Value '' -Encoding UTF8
L 'start'

if (-not (Test-Path -LiteralPath (Join-Path $Mf2Root 'restored\Configuration.xml'))) {
  L "FAIL: нет restored в $Mf2Root"
  exit 10
}
if (-not (Test-Path -LiteralPath (Join-Path $Mf2Root 'extension\Configuration.xml'))) {
  L "FAIL: нет extension в $Mf2Root"
  exit 11
}
if (-not (Test-Path -LiteralPath (Join-Path $ProtoRoot 'extension'))) {
  L "FAIL: нет среза extension в $ProtoRoot"
  exit 12
}

L "copy mf2 restored -> $loadRest (read-only source)"
New-Item -ItemType Directory -Force -Path $loadRest, $loadExt | Out-Null
$rc1 = (Start-Process -FilePath 'robocopy.exe' -ArgumentList @($Mf2Root + '\restored', $loadRest, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:1', '/W:1') -Wait -PassThru).ExitCode
L "robocopy restored rc=$rc1"
if ($rc1 -ge 8) { exit 13 }
L "copy mf2 extension -> $loadExt"
$rc2 = (Start-Process -FilePath 'robocopy.exe' -ArgumentList @($Mf2Root + '\extension', $loadExt, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:1', '/W:1') -Wait -PassThru).ExitCode
L "robocopy extension rc=$rc2"
if ($rc2 -ge 8) { exit 14 }

L 'overlay slice onto load copies'
& $oscript $overlayOs '--срез' $ProtoRoot '--restored' $loadRest '--extension' $loadExt
$oc = $LASTEXITCODE
L "overlay exit=$oc"
if ($oc -ne 0) { exit 15 }

foreach ($p in @($db, $data)) {
  if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
}
New-Item -ItemType Directory -Force -Path $data | Out-Null

L 'ibcmd create'
& $ibcmd infobase create "--db-path=$db" "--data=$data" *> (Join-Path $TmpRoot 'create.log')
L "create exit=$LASTEXITCODE"
if ($LASTEXITCODE -ne 0) { exit 16 }

L 'ibcmd import main (load/restored)'
& $ibcmd infobase config import "--db-path=$db" "--data=$data" $loadRest *> (Join-Path $TmpRoot 'import_main.log')
$c = $LASTEXITCODE
L "import main exit=$c"
if ($c -ne 0) { exit 17 }

L 'ibcmd apply main'
& $ibcmd infobase config apply "--db-path=$db" "--data=$data" --force *>> (Join-Path $TmpRoot 'import_main.log')
$c = $LASTEXITCODE
L "apply main exit=$c"
if ($c -ne 0) { exit 18 }

function Invoke-Designer([string]$OutFile, [string[]]$DesignerArgs) {
  $all = @('DESIGNER', "/F`"$db`"", '/DisableStartupDialogs', '/DisableStartupMessages', "/Out`"$OutFile`"") + $DesignerArgs
  $p = Start-Process -FilePath $v8 -ArgumentList $all -Wait -PassThru
  return $p.ExitCode
}

L 'DESIGNER LoadConfigFromFiles extension'
$c = Invoke-Designer (Join-Path $TmpRoot 'load_ext.log') @('/LoadConfigFromFiles', "`"$loadExt`"", '-Extension', $ExtName)
L "load ext exit=$c"
if ($c -ne 0) { exit 19 }

L 'DESIGNER UpdateDBCfg extension'
$c = Invoke-Designer (Join-Path $TmpRoot 'update_ext.log') @('/UpdateDBCfg', '-Extension', $ExtName)
L "updatedb ext exit=$c"
if ($c -ne 0) { exit 20 }

$modes = @('-ThinClient', '-WebClient', '-Server', '-ExternalConnection', '-ThickClientOrdinaryApplication')
L 'DESIGNER CheckModules extension'
$cExt = Invoke-Designer (Join-Path $TmpRoot 'check_modules_ext.txt') (@('/CheckModules') + $modes + @('-Extension', $ExtName))
L "check ext exit=$cExt"

L "DONE check_ext=$cExt"
exit $cExt
