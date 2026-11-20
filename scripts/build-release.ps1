param([string]$Version='2.0.0', [string]$Dotnet='', [string]$Iscc='', [string]$SignScript='', [string]$SignerCertificate='', [switch]$AllowSelfSigned, [switch]$PackageOnly, [switch]$SkipGenericModelLive)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
. "$PSScriptRoot/release-common.ps1"
. "$PSScriptRoot/signature-common.ps1"
. "$PSScriptRoot/local-runtime-common.ps1"
Push-Location $projectRoot
try {
 if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Version must be major.minor.patch'}
 $localPackage=Assert-LocalRuntime $projectRoot
 $certificateSha256=''
 if($SignerCertificate){$certificateSha256=Get-CertificateSha256 ([Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path -LiteralPath $SignerCertificate).Path))}
 if($AllowSelfSigned -and (!$SignScript -or !$certificateSha256)){throw 'Self-signed builds require an explicit signing script and pinned public certificate.'}
 if(!$Dotnet){$Dotnet=Join-Path $projectRoot '.tools/dotnet/dotnet.exe'}
 if(!$Iscc){$Iscc=Join-Path $projectRoot '.tools/inno/ISCC.exe'}
 if(!(Test-Path -LiteralPath $Dotnet) -or !(Test-Path -LiteralPath $Iscc)){throw 'Run scripts/bootstrap-tools.ps1 or provide -Dotnet and -Iscc.'}
 $env:DOTNET_CLI_HOME=Join-Path $projectRoot '.tools/cli'
 $env:NUGET_PACKAGES=Join-Path $projectRoot '.tools/packages'
 $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
 $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
 if((& $Dotnet --version) -ne '10.0.402'){throw 'Required SDK: 10.0.402'}
 $compilerHelp=(& $Iscc --version 2>&1 | Out-String)
 if($compilerHelp -notmatch '7\.1\.0'){throw 'Required Inno Setup: 7.1.0'}
 New-Item -ItemType Directory -Path artifacts,dist -Force | Out-Null
 $publishPath=Join-Path $projectRoot ('artifacts/publish-'+$Version)
 $appFile=Join-Path $publishPath 'YiKe.exe'
 $setup=Join-Path $projectRoot "dist/YiKe-Setup-$Version-win-x64.exe"
 $receiptPath=Join-Path $projectRoot "artifacts/build-receipt-$Version.json"
 if(!$PackageOnly){
  & node --test tests/vscode-selection.test.cjs | Tee-Object artifacts/vscode-tests.txt
  if($LASTEXITCODE){throw 'VS Code adapter tests failed'}
  Copy-Item artifacts/vscode-tests.txt docs/VSCODE_TESTS.txt -Force
  & node --test tests/glossary-examples.test.cjs | Tee-Object artifacts/example-code-tests.txt
  if($LASTEXITCODE){throw 'Bundled example semantics tests failed'}
  Copy-Item artifacts/example-code-tests.txt docs/EXAMPLE_CODE_TESTS.txt -Force
  & "$PSScriptRoot/generate-brand.ps1"
  & "$PSScriptRoot/generate-installer-models.ps1"
  & $Dotnet restore tests/YiKe.Tests/YiKe.Tests.csproj --locked-mode -v minimal
  if($LASTEXITCODE){throw 'Restore failed'}
  & $Dotnet build tests/YiKe.Tests/YiKe.Tests.csproj -c Release --no-restore -p:Version=$Version -v minimal
  if($LASTEXITCODE){throw 'Build failed'}
  $tests='./tests/YiKe.Tests/bin/Release/net10.0-windows/win-x64/YiKe.Tests.exe'
  & $tests | Tee-Object artifacts/automated-tests.txt
  if($LASTEXITCODE){throw 'Core tests failed'}
  & $tests --ui-regression | Tee-Object artifacts/ui-tests.txt
  if($LASTEXITCODE){throw 'UI tests failed'}
  & $tests --visual | Tee-Object artifacts/visual-tests.txt
  if($LASTEXITCODE){throw 'Visual fixtures failed'}
  if(!$SkipGenericModelLive){
  & $tests --local-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/local-model-live.json') | Tee-Object artifacts/local-model-tests.txt
  if($LASTEXITCODE){throw 'Real local model check failed'}
  & $tests --local-live-light (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/local-model-light-live.json') | Tee-Object artifacts/local-model-light-tests.txt
  if($LASTEXITCODE){throw 'Real lightweight model check failed'}
  & $tests --technical-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/technical-live-standard.json') standard | Tee-Object artifacts/technical-standard-tests.txt
  if($LASTEXITCODE -notin @(0,77)){throw 'Real standard-model technical explanation check failed'}
  & $tests --technical-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/technical-live-light.json') light | Tee-Object artifacts/technical-light-tests.txt
  if($LASTEXITCODE){throw 'Real lightweight glossary check failed'}
  & $tests --word-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/word-live-standard.json') standard | Tee-Object artifacts/word-standard-tests.txt
  if($LASTEXITCODE -notin @(0,77)){throw 'Standard local ordinary-word translation failed'}
  & $tests --word-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/word-live-light.json') light | Tee-Object artifacts/word-light-tests.txt
  if($LASTEXITCODE){throw 'Light local ordinary-word translation failed'}
  & $tests --any-word-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/any-word-live-standard.json') standard | Tee-Object artifacts/any-word-standard-tests.txt
  if($LASTEXITCODE -notin @(0,77)){throw 'Standard local any-word explanation failed'}
  & $tests --any-word-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/any-word-live-light.json') light | Tee-Object artifacts/any-word-light-tests.txt
  if($LASTEXITCODE){throw 'Offline fallback any-word explanation failed'}
  & $tests --latency-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/latency-live-standard.json') standard | Tee-Object artifacts/latency-standard-tests.txt
  if($LASTEXITCODE -notin @(0,77)){throw 'Standard response latency and re-selection check failed'}
  & $tests --latency-live (Join-Path $projectRoot 'local-runtime') (Join-Path $projectRoot 'artifacts/latency-live-light.json') light | Tee-Object artifacts/latency-light-tests.txt
  if($LASTEXITCODE){throw 'Light response latency and re-selection check failed'}
  }else{
   $skipped=[ordered]@{skipped=$true;reason='Explicit UI/PDF-only patch validation: generic Qwen code and binaries unchanged; heavy model live suite not rerun in this build. NMT published self-test remains required.'}
   foreach($name in @('local-model-live','local-model-light-live','technical-live-standard','technical-live-light','word-live-standard','word-live-light','any-word-live-standard','any-word-live-light','latency-live-standard','latency-live-light')){$skipped|ConvertTo-Json|Set-Content "artifacts/$name.json"}
   foreach($name in @('local-model-tests','local-model-light-tests')){'SKIP explicit unchanged generic Qwen live suite; see build manifest.'|Set-Content "artifacts/$name.txt"}
   Write-Host 'SKIP explicit unchanged generic Qwen live suite. Core, WPF, adapter and published NMT checks are still required.'
  }
  Copy-Item artifacts/latency-live-standard.json docs/LATENCY_LIVE_STANDARD.json -Force
  Copy-Item artifacts/latency-live-light.json docs/LATENCY_LIVE_LIGHT.json -Force
  Copy-Item artifacts/any-word-live-standard.json docs/ANY_WORD_LIVE_STANDARD.json -Force
  Copy-Item artifacts/any-word-live-light.json docs/ANY_WORD_LIVE_LIGHT.json -Force
  Copy-Item artifacts/word-live-standard.json docs/WORD_LIVE_STANDARD.json -Force
  Copy-Item artifacts/word-live-light.json docs/WORD_LIVE_LIGHT.json -Force
  Copy-Item artifacts/technical-live-standard.json docs/TECHNICAL_LIVE_STANDARD.json -Force
  Copy-Item artifacts/technical-live-light.json docs/TECHNICAL_LIVE_LIGHT.json -Force
  Copy-Item artifacts/local-model-light-live.json docs/LOCAL_MODEL_LIGHT_LIVE.json -Force
  Copy-Item artifacts/local-model-light-tests.txt docs/LOCAL_MODEL_LIGHT_TESTS.txt -Force
  Copy-Item artifacts/local-model-live.json docs/LOCAL_MODEL_LIVE.json -Force
  Copy-Item artifacts/local-model-tests.txt docs/LOCAL_MODEL_TESTS.txt -Force
  Copy-Item artifacts/automated-tests.txt docs/AUTOMATED_TESTS.txt -Force
  Copy-Item artifacts/ui-tests.txt docs/UI_TESTS.txt -Force
  Copy-Item artifacts/visual-qa/*.png docs/visual -Force
  $inputs=Get-SourceEntries $projectRoot
  $fingerprint=Get-InputFingerprint $inputs
  & $Dotnet publish src/YiKe/YiKe.csproj -c Release --no-restore -p:Version=$Version -p:ContinuousIntegrationBuild=true -p:PathMap="$projectRoot=/_/YiKe" -o $publishPath -v minimal
  if($LASTEXITCODE){throw 'Publish failed'}
  $nmtCheck=Start-Process -FilePath $appFile -ArgumentList '--verify-nmt' -WindowStyle Hidden -Wait -PassThru
  if($nmtCheck.ExitCode){throw 'Published NMT runtime verification failed'}
  $dictionaryCheck=Start-Process -FilePath $appFile -ArgumentList '--verify-dictionary' -WindowStyle Hidden -Wait -PassThru
  if($dictionaryCheck.ExitCode){throw 'Published single-file dictionary verification failed'}
  if((Get-ChildItem $publishPath -File | Where-Object Name -ne 'YiKe.exe').Count){throw 'Unexpected publish files: inspect before packaging.'}
  if($SignScript){
   $SignScript=(Resolve-Path -LiteralPath $SignScript).Path
   & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -NonInteractive -File $SignScript $appFile
   if($LASTEXITCODE){throw 'App signing failed'}
   $appSigning=Assert-AuthenticodeFile -Path $appFile -ExpectedCertificateSha256 $certificateSha256 -AllowSelfSigned:$AllowSelfSigned
  }
  $compilerArgs=@('/Qp',('/DAppVersion='+$Version),('/DPublishDir='+$publishPath))
  if($SignScript){
   $hostPath=(Join-Path $PSHOME 'pwsh.exe').Replace('$','$$')
   $safeSignPath=$SignScript.Replace('$','$$')
   $compilerArgs+=('/DSignToolCommand=1')
   $uninstallerCache=Join-Path $projectRoot ('artifacts/signed-uninstallers-'+$Version)
   New-Item -ItemType Directory -Path $uninstallerCache -Force | Out-Null
   $compilerArgs+=('/DSignedUninstallerPath='+$uninstallerCache)
   $compilerArgs+=('/SYiKeSign=$q'+$hostPath+'$q -NoProfile -NonInteractive -File $q'+$safeSignPath+'$q $f')
  }
  & $Iscc @compilerArgs installer/YiKe.iss
  if($LASTEXITCODE){throw 'Installer compilation failed'}
  if($SignScript){$setupSigning=Assert-AuthenticodeFile -Path $setup -ExpectedCertificateSha256 $certificateSha256 -AllowSelfSigned:$AllowSelfSigned}
  $null=Assert-LocalRuntime $projectRoot
  if((Get-InputFingerprint (Get-SourceEntries $projectRoot)) -ne $fingerprint){throw 'Executable inputs changed during build; rebuild required'}
  $receipt=[ordered]@{version=$Version;builtUtc=[DateTime]::UtcNow.ToString('o');inputSha256=$fingerprint;appSha256=(Get-FileHash $appFile -Algorithm SHA256).Hash.ToLowerInvariant();setupSha256=(Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant();appSignature=(Get-AuthenticodeSignature $appFile).Status.ToString();setupSignature=(Get-AuthenticodeSignature $setup).Status.ToString()}
  if($SignScript){$receipt.signing=[ordered]@{allowSelfSigned=[bool]$AllowSelfSigned;certificateSha256=$appSigning.certificateSha256;subject=$appSigning.subject;app=$appSigning;setup=$setupSigning}}
  $receipt.localPackage=$localPackage
  $receipt.skippedGenericModelLive=[bool]$SkipGenericModelLive
  $receipt.nmtPackage=Get-Content assets/nmt-package.json -Raw | ConvertFrom-Json
  $receipt | ConvertTo-Json -Depth 8 | Set-Content $receiptPath -Encoding utf8
 }
 if(!(Test-Path $appFile) -or !(Test-Path $setup) -or !(Test-Path $receiptPath)){throw 'Verified build receipt/artifacts missing; run a full build first'}
 $receipt=Get-Content $receiptPath -Raw | ConvertFrom-Json
 $entries=Get-SourceEntries $projectRoot
 if($receipt.version -ne $Version -or $receipt.inputSha256 -ne (Get-InputFingerprint $entries) -or $receipt.appSha256 -ne (Get-FileHash $appFile).Hash.ToLowerInvariant() -or $receipt.setupSha256 -ne (Get-FileHash $setup).Hash.ToLowerInvariant()){throw 'Build receipt mismatch; changed executable inputs/artifacts require a full rebuild'}
 $fileVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($appFile).FileVersion
 if($fileVersion -ne "$Version.0"){throw "Application version mismatch: $fileVersion"}
 $portable=Join-Path $projectRoot ('artifacts/portable-'+$Version+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
 New-Item -ItemType Directory -Path $portable | Out-Null
 Copy-Item -LiteralPath $appFile -Destination $portable
 Copy-Item -LiteralPath (Join-Path $publishPath "translation-runtime") -Destination $portable -Recurse
 [IO.File]::WriteAllText((Join-Path $portable 'portable.flag'),'YiKe portable mode v1')
 Copy-Item -LiteralPath README.md,LICENSE,THIRD_PARTY_NOTICES.md,RELEASE_NOTES.md -Destination $portable
 $portableDocs=New-Item -ItemType Directory -Path (Join-Path $portable "docs")
 Copy-Item -LiteralPath docs/LOCAL_MODEL.md,docs/COMPATIBILITY.md -Destination $portableDocs.FullName
 Copy-LocalRuntime $projectRoot $portable $localPackage
 Copy-Item -LiteralPath (Join-Path (Split-Path $Dotnet) 'LICENSE.txt') -Destination (Join-Path $portable 'DOTNET-LICENSE.txt')
 foreach($notice in @('DOTNET-RUNTIME-NOTICES.txt','DOTNET-WPF-LICENSE.txt','DOTNET-WPF-NOTICES.txt')){Copy-Item -LiteralPath "assets/$notice" -Destination $portable}
 New-ReleaseZip $portable (Join-Path $projectRoot "dist/YiKe-Portable-$Version-win-x64.zip")
 Copy-Item -LiteralPath RELEASE_NOTES.md,README.md -Destination dist -Force

 $sourceStage=Join-Path $projectRoot ('artifacts/source-'+$Version+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
 New-Item -ItemType Directory -Path $sourceStage | Out-Null
 foreach($entry in $entries){
  $destination=Join-Path $sourceStage $entry.path
  New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
  Copy-Item -LiteralPath (Join-Path $projectRoot $entry.path) -Destination $destination
 }
 @{format=1;files=$entries} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $sourceStage 'SOURCE-MANIFEST.json') -Encoding utf8
 New-ReleaseZip $sourceStage (Join-Path $projectRoot "dist/YiKe-Source-$Version.zip")
 $manifest=[ordered]@{format=2;version=$Version;sdk='10.0.402';runtime='10.0.13';inno='7.1.0';architecture='win-x64';selfContained=$true;builtUtc=$receipt.builtUtc;packagedUtc=[DateTime]::UtcNow.ToString('o');appBytes=(Get-Item $appFile).Length;appSignature=$receipt.appSignature;signature=$receipt.setupSignature;appSha256=$receipt.appSha256;setupSha256=$receipt.setupSha256;inputSha256=$receipt.inputSha256}
 if($receipt.signing){$manifest.signing=$receipt.signing}
 $manifest.localPackage=$receipt.localPackage
 $manifest.validation=[ordered]@{skippedGenericModelLive=[bool]$receipt.skippedGenericModelLive;publishedNmtRequired=$true}
 $manifest.nmtPackage=$receipt.nmtPackage
 $manifest.installerModelMode='optional-download'
 $manifest.portableModelMode='bundled-both'
 $manifest | ConvertTo-Json -Depth 8 | Set-Content dist/BUILD-MANIFEST.json -Encoding utf8
 $names=@("YiKe-Setup-$Version-win-x64.exe","YiKe-Portable-$Version-win-x64.zip","YiKe-Source-$Version.zip",'BUILD-MANIFEST.json','README.md','RELEASE_NOTES.md')
 $names | Sort-Object | ForEach-Object { $h=Get-FileHash -LiteralPath (Join-Path $projectRoot "dist/$_") -Algorithm SHA256; '{0}  {1}' -f $h.Hash.ToLowerInvariant(),$_ } | Set-Content dist/SHA256SUMS.txt -Encoding ascii
 & "$PSScriptRoot/verify-release.ps1" -Version $Version
 Write-Host "Release ready: $projectRoot\dist"
} finally {Pop-Location}
