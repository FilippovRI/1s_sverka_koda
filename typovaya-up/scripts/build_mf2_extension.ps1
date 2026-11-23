param(
    [string]$Out = 'D:\typovaya-up-out-mf2',
    [string]$CacheFrom = 'D:\typovaya-up-out-v2'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$oscript = (Get-Command oscript -EA SilentlyContinue).Source
if (-not $oscript) { $oscript = 'oscript' }
$summary = Join-Path $Out 'summary.json'
if (-not (Test-Path $summary)) { throw "нет $summary" }
$j = Get-Content $summary -Raw -Encoding UTF8 | ConvertFrom-Json
$cacheDir = Join-Path $Out 'cache'
$cacheDst = Join-Path $cacheDir 'compare.json'
if (-not (Test-Path $cacheDst)) {
    $cacheSrc = Join-Path $CacheFrom 'cache\compare.json'
    if (-not (Test-Path $cacheSrc)) { throw "нет compare.json: $cacheSrc" }
    New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
  Copy-Item -LiteralPath $cacheSrc -Destination $cacheDst
}
$args = @(
    (Join-Path $repo 'src\Главный.os'),
    '--исходная', $j.meta.source,
    '--поставщик', $j.meta.vendor,
    '--настройки', $j.meta.settings,
    '--выход', $Out,
    '--только-extension',
    '--без-отчета'
)
& $oscript @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$ext = Join-Path $Out 'extension'
$bsl = (Get-ChildItem -LiteralPath $ext -Recurse -Filter '*.bsl' -File -EA SilentlyContinue | Measure-Object).Count
Write-Host "extension: $ext modules.bsl=$bsl"
exit 0