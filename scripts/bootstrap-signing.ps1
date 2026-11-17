$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$spec=(Get-Content (Join-Path $root 'build-tools.json') -Raw|ConvertFrom-Json).signingTools
$package=Join-Path $root '.tools/windows-sdk-buildtools.nupkg'
New-Item -ItemType Directory -Path (Split-Path $package) -Force|Out-Null
if(!(Test-Path -LiteralPath $package) -or (Get-FileHash -LiteralPath $package -Algorithm SHA512).Hash.ToLowerInvariant() -ne $spec.sha512){Invoke-WebRequest -Uri $spec.url -OutFile $package}
if((Get-FileHash -LiteralPath $package -Algorithm SHA512).Hash.ToLowerInvariant() -ne $spec.sha512){throw 'Windows SDK package SHA512 mismatch'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($package)
$target=Join-Path $root '.tools/signtool'
New-Item -ItemType Directory -Path $target -Force|Out-Null
try{
 foreach($name in @('signtool.exe','mssign32.dll')){
  $matches=@($zip.Entries|Where-Object {$_.FullName.EndsWith('/x64/'+$name,[StringComparison]::OrdinalIgnoreCase)})
  if($matches.Count -ne 1){throw 'Unexpected Windows SDK archive layout'}
  $path=Join-Path $target $name
  [IO.Compression.ZipFileExtensions]::ExtractToFile($matches[0],$path,$true)
  $expected=if($name -eq 'signtool.exe'){$spec.signtoolSha256}else{$spec.mssign32Sha256}
  $signature=Get-AuthenticodeSignature -LiteralPath $path
  if((Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $expected -or $signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Microsoft Corporation*'){throw 'Windows SDK tool identity verification failed'}
 }
}finally{$zip.Dispose()}
Write-Host ('Verified Microsoft SignTool '+$spec.version+' in project-local .tools/signtool.')
