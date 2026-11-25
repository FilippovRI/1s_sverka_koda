# Сравнение отчёта 1cedtcli validate (TSV) с CheckModules (211/43 калибровка).
# Сопоставляет «переменная/процедура не определена» по объекту и имени символа.
param(
    [Parameter(Mandatory)]
    [string]$EdtTsv,
    [string]$Mode = 'Calibrate',
    [string]$MainCheck = 'D:\typovaya-up-tmp-forms-cm-classa\check_modules_main_classa2.txt',
    [string]$ExtCheck = 'D:\typovaya-up-tmp-forms-cm-classa\check_modules_ext_classa2.txt',
    [string]$OutDir = 'D:\typovaya-up-edt-tmp'
)
$ErrorActionPreference = 'Stop'

function Get-CheckFindings([string]$Path, [string]$Scope) {
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Warning "нет отчёта CheckModules: $Path"
        return @()
    }
    $lines = [IO.File]::ReadAllLines($Path, [Text.Encoding]::UTF8)
    $rx = '^\{(?<obj>.+?)\((?<loc>[^)]+)\)\}:\s*(?<msg>.+)$'
    $items = @()
    foreach ($line in $lines) {
        if ($line -notmatch $rx) { continue }
        $obj = $Matches.obj
        $msg = $Matches.msg.Trim()
        $msg = ($msg -replace '\s+\(Проверка:.*$', '').Trim()
        $sym = $null
        if ($msg -match '^(Переменная не определена|Процедура или функция не определена|Процедура не определена)\s*\((?<s>[^)]+)\)') {
            $kind = $Matches[1]
            $sym = $Matches.s
        }
        elseif ($msg -match 'не определ') {
            $kind = $msg
        }
        else {
            continue
        }
        $key = ("{0}|{1}|{2}" -f $obj, $kind, $sym)
        $items += [pscustomobject]@{
            Scope = $Scope
            Object = $obj
            Kind = $kind
            Symbol = $sym
            Message = $msg
            Key = $key
            Source = 'CheckModules'
        }
    }
    $items | Sort-Object Key -Unique
}

function Get-EdtFindings([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "нет TSV: $Path" }
    $raw = [IO.File]::ReadAllText($Path)
    $enc = [Text.Encoding]::UTF8
    $lines = [IO.File]::ReadAllLines($Path, $enc)
    if ($lines.Count -eq 0) { return @() }
    $sep = "`t"
    $header = $lines[0].Split($sep)
    function Col([string[]]$h, [string[]]$names) {
        foreach ($n in $names) {
            for ($i = 0; $i -lt $h.Length; $i++) {
                if ($h[$i].Trim() -ieq $n) { return $i }
            }
        }
        return -1
    }
    $iMsg = Col $header @('Description', 'Message', 'Описание', 'Сообщение', 'description', 'message')
    $iRes = Col $header @('Resource', 'Path', 'Location', 'Ресурс', 'Путь', 'resource', 'path')
    $iSev = Col $header @('Severity', 'Level', 'Важность', 'severity')
    $iPrj = Col $header @('Project', 'Проект', 'project')
    $start = 1
    if ($iMsg -lt 0) {
        # без заголовка: пробуем все колонки
        $start = 0
    }
    $items = @()
    for ($li = $start; $li -lt $lines.Count; $li++) {
        if ([string]::IsNullOrWhiteSpace($lines[$li])) { continue }
        $c = $lines[$li].Split($sep)
        $msg = if ($iMsg -ge 0 -and $iMsg -lt $c.Length) { $c[$iMsg] } else { $lines[$li] }
        $res = if ($iRes -ge 0 -and $iRes -lt $c.Length) { $c[$iRes] } else { '' }
        $prj = if ($iPrj -ge 0 -and $iPrj -lt $c.Length) { $c[$iPrj] } else { '' }
        $joined = "$msg $res"
        if ($joined -notmatch 'не определ|not defined|cannot be resolved|is not found|не найден') { continue }
        $sym = $null
        $kind = 'прочее'
        if ($msg -match 'Переменная не определена\s*\((?<s>[^)]+)\)') { $kind = 'Переменная не определена'; $sym = $Matches.s }
        elseif ($msg -match 'Процедура или функция не определена\s*\((?<s>[^)]+)\)') { $kind = 'Процедура или функция не определена'; $sym = $Matches.s }
        elseif ($msg -match 'Процедура не определена\s*\((?<s>[^)]+)\)') { $kind = 'Процедура не определена'; $sym = $Matches.s }
        elseif ($msg -match "Переменная\s+['«""](?<s>[^'»""]+)['»""]\s+не определена") { $kind = 'Переменная не определена'; $sym = $Matches.s }
        elseif ($msg -match 'Variable .+ is not defined|variable\s+[«"'']?(?<s>[\wА-Яа-я_]+)[»"'']?') { $kind = 'Переменная не определена'; $sym = $Matches.s }
        elseif ($msg -match 'Method .+ is not found|procedure .+ not defined|(?<s>[\wА-Яа-я_]+)\s+is not found') { $kind = 'Процедура или функция не определена'; $sym = $Matches.s }
        $obj = $res
        $key = ("{0}|{1}|{2}" -f $obj, $kind, $sym)
        $items += [pscustomobject]@{
            Scope = $prj
            Object = $obj
            Kind = $kind
            Symbol = $sym
            Message = $msg
            Key = $key
            Source = 'EDT'
            Raw = $lines[$li]
        }
    }
    $items
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$cmMain = @(Get-CheckFindings $MainCheck 'main')
$cmExt = @(Get-CheckFindings $ExtCheck 'ext')
$cm = $cmMain + $cmExt
$edt = @(Get-EdtFindings $EdtTsv)

# Символьный ключ (имя + вид), без полного пути — EDT и CheckModules по-разному пишут объект
function SymKey($x) { '{0}|{1}' -f $x.Kind, $x.Symbol }
$cmSym = $cm | Where-Object { $_.Symbol } | Group-Object { SymKey $_ } | ForEach-Object { $_.Name }
$edtSym = $edt | Where-Object { $_.Symbol } | Group-Object { SymKey $_ } | ForEach-Object { $_.Name }
$cmSet = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($s in @($cmSym)) { if ($s) { [void]$cmSet.Add([string]$s) } }
$edtSet = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($s in @($edtSym)) { if ($s) { [void]$edtSet.Add([string]$s) } }
$both = $cmSym | Where-Object { $edtSet.Contains($_) } | Sort-Object -Unique
$onlyCm = $cmSym | Where-Object { -not $edtSet.Contains($_) } | Sort-Object -Unique
$onlyEdt = $edtSym | Where-Object { -not $cmSet.Contains($_) } | Sort-Object -Unique

$report = Join-Path $OutDir ("compare-$Mode-{0}.md" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
$lines = @(
    "# Сравнение EDT validate и CheckModules ($Mode)",
    '',
    "- TSV: ``$EdtTsv``",
    "- CheckModules main: $($cmMain.Count) уник. (ожидание калибровки 211)",
    "- CheckModules ext: $($cmExt.Count) уник. (ожидание калибровки 43)",
    "- EDT (переменная/процедура не определена): $($edt.Count) строк, $($edtSym.Count) уник. символов",
    "- Совпало по виду+символу: $($both.Count)",
    "- Только в CheckModules: $($onlyCm.Count)",
    "- Только в EDT: $($onlyEdt.Count)",
    ''
)
function Add-List($title, $items) {
    $script:lines += "## $title"
    $script:lines += ''
    if (-not $items -or $items.Count -eq 0) { $script:lines += '_нет_'; $script:lines += ''; return }
    foreach ($i in $items) { $script:lines += "- ``$i``" }
    $script:lines += ''
}
Add-List 'Совпали' $both
Add-List 'Только CheckModules' $onlyCm
Add-List 'Только EDT' $onlyEdt
$lines | Set-Content -LiteralPath $report -Encoding UTF8
Write-Host "compare: cm=$($cm.Count) edt_rows=$($edt.Count) both=$($both.Count) onlyCm=$($onlyCm.Count) onlyEdt=$($onlyEdt.Count)"
Write-Host "report=$report"
# краткая сводка виновных объектов EDT-only
if ($onlyEdt) {
    Write-Host 'EDT-only symbols:'
    $onlyEdt | Select-Object -First 30 | ForEach-Object { Write-Host "  $_" }
}
