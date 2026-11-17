$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$lock=Get-Content (Join-Path $root 'build-tools.json') -Raw | ConvertFrom-Json
$toolDir=Join-Path $root '.tools'
New-Item -ItemType Directory -Path $toolDir -Force | Out-Null
if(!(Test-Path (Join-Path $toolDir 'dotnet/dotnet.exe'))){
 $zip=Join-Path $toolDir 'dotnet-sdk.zip';Invoke-WebRequest $lock.sdkUrl -OutFile $zip
 if((Get-FileHash -LiteralPath $zip -Algorithm SHA512).Hash -ne $lock.sdkSha512){throw 'Microsoft SDK SHA512 mismatch'}
 Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $toolDir 'dotnet') -Force
}
if(!(Test-Path (Join-Path $toolDir 'inno/ISCC.exe'))){
 $setup=Join-Path $toolDir 'inno-setup.exe';Invoke-WebRequest $lock.innoUrl -OutFile $setup
 if((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne $lock.innoSha256){throw 'Inno Setup SHA256 mismatch'}
 $signature=Get-AuthenticodeSignature $setup
 if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*Pyrsys*'){throw 'Inno Setup publisher verification failed'}
 $p=Start-Process -FilePath $setup -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER',('/DIR="'+(Join-Path $toolDir 'inno')+'"'),'/NOICONS' -WindowStyle Hidden -PassThru -Wait
 if($p.ExitCode){throw "Inno Setup tool install failed: $($p.ExitCode)"}
}
Write-Host 'Pinned build tools ready. No system PATH changes.'
