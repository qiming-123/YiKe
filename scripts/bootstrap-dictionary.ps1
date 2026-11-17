param([string]$Csv='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$source=Get-Content (Join-Path $root 'assets/word-dictionary-source.json') -Raw | ConvertFrom-Json
$expected=Get-Content (Join-Path $root 'assets/word-dictionary.json') -Raw | ConvertFrom-Json
$output=Join-Path $root 'assets/word-dictionary.dat'
if ((Test-Path -LiteralPath $output) -and (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expected.sha256) { Write-Host 'Dictionary already verified.'; return }
if (!$Csv) {
    $Csv=Join-Path $root '.tools/ecdict.csv'
    New-Item -ItemType Directory -Path (Split-Path $Csv) -Force | Out-Null
    if (!(Test-Path -LiteralPath $Csv)) { Invoke-WebRequest $source.csvUrl -OutFile $Csv }
}
if ((Get-FileHash -LiteralPath $Csv -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.csvSha256) { throw 'Dictionary source SHA256 mismatch' }
& node (Join-Path $PSScriptRoot 'build-word-details.cjs') $Csv
if ($LASTEXITCODE) { throw 'Dictionary builder failed' }
if ((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected.sha256) { throw 'Generated dictionary SHA256 mismatch' }
Write-Host 'Dictionary generated and verified.'
