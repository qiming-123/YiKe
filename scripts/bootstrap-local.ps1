# Build-time only. End users receive the model, engine and CRT in the full package.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$lock=Get-Content (Join-Path $root 'local-model.lock.json') -Raw | ConvertFrom-Json
$manifest=Get-Content (Join-Path $root 'assets/local-package.json') -Raw | ConvertFrom-Json
. "$PSScriptRoot/local-runtime-common.ps1"
try {$null=Assert-LocalRuntime $root;Write-Host 'Local runtime already verified';exit 0}catch{}
$downloads=Join-Path $root '.tools/downloads';New-Item -ItemType Directory -Path $downloads -Force|Out-Null
function Download-Pinned($Spec,[string]$Path) {
 if((Test-Path -LiteralPath $Path) -and (Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant() -eq $Spec.sha256){return}
 & curl.exe --fail --location --retry 2 --retry-all-errors --connect-timeout 20 --speed-time 60 --speed-limit 1024 --max-time 3600 --output $Path $Spec.url
 if(!(Test-Path -LiteralPath $Path) -or (Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant() -ne $Spec.sha256){throw 'Download or pinned SHA256 validation failed'}
}
$engineArchive=Join-Path $downloads ('llama-'+$lock.engine.version+'.zip');Download-Pinned $lock.engine $engineArchive
$engineStage=Join-Path $root ('.tools/llama-stage-'+[guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $engineArchive -DestinationPath $engineStage
$crtArchive=Join-Path $downloads ('vc-redist-'+$lock.crt.version+'.exe');Download-Pinned $lock.crt $crtArchive
$signature=Get-AuthenticodeSignature $crtArchive
if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation'){throw 'Microsoft CRT signature rejected'}
$crtStage=Join-Path $root ('.tools/crt-stage-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $crtStage | Out-Null
$bytes=[IO.File]::ReadAllBytes($crtArchive)
if($lock.crt.cabOffset+$lock.crt.cabBytes -gt $bytes.Length){throw 'Invalid pinned CAB bounds'}
$cab=[byte[]]::new($lock.crt.cabBytes);[Array]::Copy($bytes,$lock.crt.cabOffset,$cab,0,$cab.Length)
$cabPath=Join-Path $crtStage 'payload.cab';[IO.File]::WriteAllBytes($cabPath,$cab)
& expand.exe '-F:*' $cabPath $crtStage | Out-Null;if($LASTEXITCODE){throw 'CRT container extraction failed'}
$minimum=Join-Path $crtStage 'minimum';New-Item -ItemType Directory -Path $minimum|Out-Null
& expand.exe '-F:*' (Join-Path $crtStage $lock.crt.minimumCab) $minimum | Out-Null;if($LASTEXITCODE){throw 'CRT DLL extraction failed'}
$engineDirectory=Join-Path $root 'local-runtime/engine';New-Item -ItemType Directory -Path $engineDirectory -Force|Out-Null
foreach($file in $manifest.files | Where-Object path -Like 'engine/*'){
 $name=Split-Path $file.path -Leaf
 $crtFile=$lock.crt.files|Where-Object name -eq $name|Select-Object -First 1
 $source=if($crtFile){Join-Path $minimum $crtFile.id}else{Join-Path $engineStage $name}
 if((Get-FileHash -LiteralPath $source).Hash.ToLowerInvariant() -ne $file.sha256){throw "Runtime DLL checksum mismatch: $name"}
 Copy-Item -LiteralPath $source -Destination (Join-Path $engineDirectory $name) -Force
}
$modelDirectory=Join-Path $root 'local-runtime/models';New-Item -ItemType Directory -Path $modelDirectory -Force|Out-Null
Download-Pinned $lock.model (Join-Path $modelDirectory $lock.model.file)
Download-Pinned $lock.lightModel (Join-Path $modelDirectory $lock.lightModel.file)
$null=Assert-LocalRuntime $root
Write-Host 'Full offline model, CPU engine and app-local Microsoft CRT verified.'
