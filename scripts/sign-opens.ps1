param([Parameter(Mandatory,Position=0)][string]$Path,[string]$TimestampServer='http://timestamp.digicert.com')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/signature-common.ps1"
$public=[Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path (Split-Path $PSScriptRoot) 'assets/opens-codesign.cer'))
$certificate=Get-Item -LiteralPath ('Cert:/CurrentUser/My/'+$public.Thumbprint) -ErrorAction Stop
if(!$certificate.HasPrivateKey -or $certificate.Subject -ne 'CN=opens'){throw 'The opens signing private key is unavailable in the current user personal store.'}
$file=(Resolve-Path -LiteralPath $Path).Path
$root=Split-Path $PSScriptRoot
$spec=(Get-Content (Join-Path $root 'build-tools.json') -Raw|ConvertFrom-Json).signingTools
foreach($name in @('signtool.exe','mssign32.dll')){
 $pathTool=Join-Path $root ('.tools/signtool/'+$name)
 $expected=if($name -eq 'signtool.exe'){$spec.signtoolSha256}else{$spec.mssign32Sha256}
 if(!(Test-Path -LiteralPath $pathTool) -or (Get-FileHash -LiteralPath $pathTool).Hash.ToLowerInvariant() -ne $expected){throw 'Run scripts/bootstrap-signing.ps1 to obtain the pinned Microsoft signing tools.'}
}
$arguments=@('sign','/sha1',$certificate.Thumbprint,'/s','My','/fd','SHA256','/d','YiKe - opens')
if($TimestampServer){$arguments+=@('/tr',$TimestampServer,'/td','SHA256')}
& (Join-Path $root '.tools/signtool/signtool.exe') @arguments $file
if($LASTEXITCODE){throw 'Microsoft SignTool failed'}
$verified=Assert-AuthenticodeFile -Path $file -ExpectedCertificateSha256 (Get-CertificateSha256 $public) -AllowSelfSigned
if($TimestampServer -and !$verified.timestamped){throw 'Requested timestamp was not attached; signing stopped.'}
if($TimestampServer){$null=Get-AuthenticodeAlgorithms $file}
Write-Host ('Signed as opens (self-signed; trust={0}; timestamp={1}): {2}' -f $verified.trust,$verified.timestamped,[IO.Path]::GetFileName($file))
