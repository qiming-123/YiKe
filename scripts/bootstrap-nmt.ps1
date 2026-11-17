$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$stage=Join-Path $root '.tools/nmt-eval'
$lock=Get-Content "$root/assets/nmt-source-lock.json" -Raw | ConvertFrom-Json
New-Item -ItemType Directory $stage -Force | Out-Null
function Get-PinnedFile($entry,[string]$file){
 if((Test-Path -LiteralPath $file) -and (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -eq $entry.sha256){return}
 Invoke-WebRequest $entry.url -OutFile $file
 if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256){throw "SHA256 mismatch: $file"}
}
Get-PinnedFile $lock.python "$stage/python.zip"
Expand-Archive "$stage/python.zip" "$stage/python" -Force
$signature=Get-AuthenticodeSignature "$stage/python/python.exe"
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Python Software Foundation'){throw 'Python publisher verification failed'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($package in $lock.packages){
 $file=Join-Path $stage ([IO.Path]::GetFileName(([uri]$package.url).AbsolutePath))
 Get-PinnedFile $package $file
 [IO.Compression.ZipFile]::ExtractToDirectory($file,"$stage/python/Lib/site-packages",$true)
}
Add-Content "$stage/python/python312._pth" 'Lib/site-packages'
Get-PinnedFile $lock.model "$stage/model.zip"
Expand-Archive "$stage/model.zip" "$stage/model" -Force
& "$stage/python/python.exe" -I -B -c 'import ctranslate2, sentencepiece; print(ctranslate2.__version__)'
if($LASTEXITCODE){throw 'NMT runtime import failed'}
& "$PSScriptRoot/stage-nmt.ps1"
