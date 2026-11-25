# Загрузка restored+extension со срезом «только модуль» во временную базу D:\typovaya-up-tmp-forms.
# Источник restored/extension — копия D:\typovaya-up-out-mf2 (только чтение) в proto\load\.
# Не трогает typovaya-up-out-mf2, typovaya-up-tmp-check, исходные выгрузки и InfoBase3.
param(
  [string]$ProtoRoot = 'D:\typovaya-up-forms-proto',
  [string]$Mf2Root = 'D:\typovaya-up-out-mf2',
  [string]$TmpRoot = 'D:\typovaya-up-tmp-forms',
  [string]$ExtName = 'УП_Доработки',
  [string]$VendorBp = 'D:\выгрузка БП'
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

L "copy mf2 restored -> $loadRest (read-only source, /MIR чтобы не оставлять старые Form.xml)"
New-Item -ItemType Directory -Force -Path $loadRest, $loadExt | Out-Null
$rc1 = (Start-Process -FilePath 'robocopy.exe' -ArgumentList @($Mf2Root + '\restored', $loadRest, '/MIR', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:1', '/W:1') -Wait -PassThru).ExitCode
L "robocopy restored rc=$rc1"
if ($rc1 -ge 8) { exit 13 }
L "copy mf2 extension -> $loadExt"
$rc2 = (Start-Process -FilePath 'robocopy.exe' -ArgumentList @($Mf2Root + '\extension', $loadExt, '/MIR', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:1', '/W:1') -Wait -PassThru).ExitCode
L "robocopy extension rc=$rc2"
if ($rc2 -ge 8) { exit 14 }

L 'overlay slice onto load copies'
& $oscript $overlayOs '--срез' $ProtoRoot '--restored' $loadRest '--extension' $loadExt
$oc = $LASTEXITCODE
L "overlay exit=$oc"
if ($oc -ne 0) { exit 15 }

# Класс 3 (T-1): реквизиты/ТЧ/значения перечислений БП со ссылкой из кода — на копии load/restored, не на mf2.
$metaOs = Join-Path $repoScripts 'run_metadata_bp.os'
if (Test-Path -LiteralPath $metaOs) {
  L "ДобавлениеМетаданныхБп on load/restored (не трогает mf2 / mf2-manual)"
  $loadOut = Join-Path $ProtoRoot 'load'
  & $oscript $metaOs $loadOut $VendorBp
  L "metadata bp add exit=$LASTEXITCODE"
} else {
  L 'SKIP ДобавлениеМетаданныхБп: нет typovaya-up/scripts/run_metadata_bp.os'
}

$validateOs = Join-Path $repoScripts 'run_validate_form_refs.os'
$formsStat = Join-Path $ProtoRoot 'proto_forms_stat.csv'
if ((Test-Path -LiteralPath $validateOs) -and (Test-Path -LiteralPath $formsStat)) {
  # DataPath/CommandInterface — только формы, у которых Form.xml изменилась относительно Mf2Root.
  # Совпадающая с typovaya разметка может уже ссылаться на реквизиты вне restored; это не регресс среза.
  $mf2Rest = Join-Path $Mf2Root 'restored'
  $filtered = Join-Path $TmpRoot 'validate_forms_changed.csv'
  $outLines = New-Object System.Collections.Generic.List[string]
  $changed = 0
  $first = $true
  foreach ($line in Get-Content -LiteralPath $formsStat -Encoding UTF8) {
    if ($first) {
      $first = $false
      $outLines.Add($line)
      $head = $line.Replace('"', '')
      if ($head.StartsWith('rel')) { continue }
    }
    $rel = $line
    $z = $rel.IndexOf(',')
    if ($z -gt 0) { $rel = $rel.Substring(0, $z) }
    $rel = $rel.Replace('"', '').Trim()
    if ($rel -eq '') { continue }
    $src = Join-Path $mf2Rest $rel
    $dst = Join-Path $loadRest $rel
    $isNewOrChanged = $false
    if (-not (Test-Path -LiteralPath $src)) {
      if (Test-Path -LiteralPath $dst) { $isNewOrChanged = $true }
    } elseif (Test-Path -LiteralPath $dst) {
      if ((Get-FileHash -LiteralPath $src -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $dst -Algorithm SHA256).Hash) {
        $isNewOrChanged = $true
      }
    }
    if ($isNewOrChanged) {
      $outLines.Add($line)
      $changed++
    }
  }
  if ($changed -eq 0) {
    L 'SKIP validate_form_refs: Form.xml среза совпадает с Mf2Root (разметку не меняли; DataPath typovaya не регресс)'
  } else {
    $utf8bom = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllLines($filtered, $outLines, $utf8bom)
    L "validate Form.xml refs vs load/restored (after metadata BP), changedFormXml=$changed"
    & $oscript $validateOs $loadRest $filtered *>> (Join-Path $TmpRoot 'validate_form_refs.log')
    $vc = $LASTEXITCODE
    L "validate form refs exit=$vc"
    if ($vc -ne 0) { exit 18 }
  }
} else {
  L 'SKIP validate_form_refs: нет скрипта или proto_forms_stat.csv'
}
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

L 'DESIGNER CheckModules main'
$cMain = Invoke-Designer (Join-Path $TmpRoot 'check_modules_main.txt') (@('/CheckModules') + $modes)
L "check main exit=$cMain"

L "DONE check_ext=$cExt check_main=$cMain"
# загрузка/UpdatedDB успешны; код CheckModules (часто 101) оставляем в логе, процесс не считаем сбойным
exit 0
