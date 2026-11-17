param([string]$Version='1.8.3',[string]$Directory='')
$ErrorActionPreference='Stop'
. "$PSScriptRoot/release-common.ps1"
. "$PSScriptRoot/signature-common.ps1"
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid release version'}
if(!$Directory){$Directory=Join-Path (Split-Path $PSScriptRoot) 'dist'}
$expected=@("YiKe-Setup-$Version-win-x64.exe","YiKe-Portable-$Version-win-x64.zip","YiKe-Source-$Version.zip",'BUILD-MANIFEST.json','README.md','RELEASE_NOTES.md')
$found=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($line in Get-Content (Join-Path $Directory 'SHA256SUMS.txt')){
 if($line -notmatch '^([0-9a-f]{64})  ([^/\\]+)$'){throw 'Invalid checksums line'}
 $hash=$Matches[1];$name=$Matches[2]
 if($name -notin $expected -or !$found.Add($name)){throw 'Unexpected or duplicate checksum'}
 if((Get-FileHash -LiteralPath (Join-Path $Directory $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash){throw "Hash mismatch: $name"}
}
if($found.Count -ne $expected.Count){throw 'Missing release checksums'}
$manifest=Get-Content (Join-Path $Directory 'BUILD-MANIFEST.json') -Raw | ConvertFrom-Json
if($manifest.format -ne 2 -or $manifest.version -ne $Version -or $manifest.architecture -ne 'win-x64' -or !$manifest.selfContained -or $manifest.sdk -ne '10.0.402' -or $manifest.runtime -ne '10.0.13'){throw 'Build manifest identity mismatch'}
if($manifest.installerModelMode -ne 'optional-download' -or $manifest.portableModelMode -ne 'bundled-both'){throw 'Model distribution mode mismatch'}
$setup=Join-Path $Directory "YiKe-Setup-$Version-win-x64.exe"
if((Get-FileHash $setup).Hash.ToLowerInvariant() -ne $manifest.setupSha256){throw 'Installer identity or signature changed'}
if($manifest.signing){
 $verified=Assert-AuthenticodeFile -Path $setup -ExpectedCertificateSha256 $manifest.signing.certificateSha256 -AllowSelfSigned:$manifest.signing.allowSelfSigned
 if($verified.subject -ne $manifest.signing.subject -or $verified.timestamped -ne $manifest.signing.setup.timestamped){throw 'Installer signing identity changed'}
}elseif((Get-AuthenticodeSignature $setup).Status.ToString() -ne $manifest.signature){throw 'Installer signature changed'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$portableLicenses=@{}
$zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $Directory "YiKe-Portable-$Version-win-x64.zip"))
try {
 Assert-SafeZip $zip
 $rootNames=@('YiKe.exe','portable.flag','README.md','RELEASE_NOTES.md','LICENSE','THIRD_PARTY_NOTICES.md','DOTNET-LICENSE.txt','DOTNET-RUNTIME-NOTICES.txt','DOTNET-WPF-LICENSE.txt','DOTNET-WPF-NOTICES.txt')
 foreach($name in $rootNames){if(!$zip.GetEntry($name)){throw "Incomplete portable archive: $name"}}
 foreach($entry in $zip.Entries){if($entry.FullName -notin $rootNames -and !$entry.FullName.StartsWith('docs/') -and !$entry.FullName.StartsWith('translation-runtime/') -and !($manifest.localPackage -and ($entry.FullName.StartsWith('local-runtime/') -or $entry.FullName.StartsWith('licenses/')))){throw 'Unexpected portable content'}}
 if($manifest.localPackage){
  $localNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  foreach($file in $manifest.localPackage.files){
   if($file.path -notmatch '^(engine|models)/[A-Za-z0-9_.-]+$' -or !$localNames.Add('local-runtime/'+$file.path)){throw 'Invalid local package manifest'}
   $entry=$zip.GetEntry('local-runtime/'+$file.path);if(!$entry -or $entry.Length -ne $file.bytes){throw 'Missing local model or engine'}
   $stream=$entry.Open();try{if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() -ne $file.sha256){throw 'Local model or engine hash mismatch'}}finally{$stream.Dispose()}
  }
  foreach($entry in $zip.Entries){if($entry.FullName.StartsWith('local-runtime/') -and !$entry.FullName.EndsWith('/') -and !$localNames.Contains($entry.FullName)){throw 'Unexpected offline runtime content'}}
  foreach($license in @('QWEN-LICENSE.txt','LLAMA-LICENSE.txt','LLVM-OPENMP-LICENSE.txt','MSVC-LICENSE.rtf')){if(!$zip.GetEntry('licenses/'+$license)){throw 'Missing local runtime license'}}
  foreach($entry in $zip.Entries | Where-Object {$_.FullName.StartsWith('licenses/') -and !$_.FullName.EndsWith('/')}){
   $stream=$entry.Open();try{$portableLicenses[$entry.FullName]=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()}finally{$stream.Dispose()}
  }
 }
 if(!$manifest.nmtPackage){throw 'Missing NMT identity'}
 $nmtNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($file in $manifest.nmtPackage.files){
  if(!$nmtNames.Add('translation-runtime/'+$file.path)){throw 'Duplicate NMT identity'}
  $item=$zip.GetEntry('translation-runtime/'+$file.path); if(!$item -or $item.Length -ne $file.bytes){throw 'Missing NMT file'}
  $stream=$item.Open();try{if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() -ne $file.sha256){throw 'NMT hash mismatch'}}finally{$stream.Dispose()}
 }
 foreach($entry in $zip.Entries){if($entry.FullName.StartsWith('translation-runtime/') -and !$entry.FullName.EndsWith('/') -and !$nmtNames.Contains($entry.FullName)){throw 'Unexpected NMT file'}}
 $app=$zip.GetEntry('YiKe.exe');$stream=$app.Open()
 try{if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() -ne $manifest.appSha256 -or $app.Length -ne $manifest.appBytes){throw 'Portable application identity mismatch'}}finally{$stream.Dispose()}
}finally{$zip.Dispose()}
$source=[IO.Compression.ZipFile]::OpenRead((Join-Path $Directory "YiKe-Source-$Version.zip"))
try {
 Assert-SafeZip $source
 foreach($name in @('src/YiKe/YiKe.csproj','src/YiKe/UI/Styles.xaml','tests/YiKe.Tests/Program.cs','installer/YiKe.iss','scripts/build-release.ps1','.gitignore','SOURCE-MANIFEST.json')){if(!$source.GetEntry($name)){throw "Incomplete source archive: $name"}}
 $stream=$source.GetEntry('SOURCE-MANIFEST.json').Open()
 try{$reader=[IO.StreamReader]::new($stream);$index=$reader.ReadToEnd() | ConvertFrom-Json}finally{$stream.Dispose()}
 if($index.format -ne 1 -or $index.files.Count -lt 20){throw 'Invalid source manifest'}
 $nmtAsset=$source.GetEntry('assets/nmt-package.json'); if(!$nmtAsset){throw 'Missing pinned NMT manifest'}
 $stream=$nmtAsset.Open();try{$reader=[IO.StreamReader]::new($stream);$expectedNmt=$reader.ReadToEnd()|ConvertFrom-Json}finally{$stream.Dispose()}
 if($expectedNmt.files.Count -ne $manifest.nmtPackage.files.Count){throw 'NMT package differs from source'}
 foreach($file in $expectedNmt.files){$actual=@($manifest.nmtPackage.files|Where-Object path -eq $file.path);if($actual.Count -ne 1 -or $actual[0].sha256 -ne $file.sha256 -or $actual[0].bytes -ne $file.bytes){throw 'NMT package differs from source'}}
 $localAsset=$source.GetEntry('assets/local-package.json')
 if($localAsset){
  $stream=$localAsset.Open();try{$reader=[IO.StreamReader]::new($stream);$expectedLocal=$reader.ReadToEnd()|ConvertFrom-Json}finally{$stream.Dispose()}
  if(!$manifest.localPackage -or $manifest.localPackage.model -ne $expectedLocal.model -or $manifest.localPackage.engine -ne $expectedLocal.engine -or $manifest.localPackage.files.Count -ne $expectedLocal.files.Count){throw 'Local package differs from pinned source manifest'}
  foreach($file in $expectedLocal.files){$actual=@($manifest.localPackage.files|Where-Object path -eq $file.path);if($actual.Count -ne 1 -or $actual[0].bytes -ne $file.bytes -or $actual[0].sha256 -ne $file.sha256){throw 'Local package differs from pinned source manifest'}}
  $licenseSources=@($index.files|Where-Object path -Like 'assets/local-licenses/*')
  if($portableLicenses.Count -ne $licenseSources.Count){throw 'Local runtime license set changed'}
  foreach($license in $licenseSources){$name='licenses/'+$license.path.Substring('assets/local-licenses/'.Length);if($portableLicenses[$name] -ne $license.sha256){throw 'Local runtime license hash mismatch'}}
 }elseif($manifest.localPackage){throw 'Local runtime is not bound to source'}
 $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($item in $index.files){
  if(!$names.Add($item.path)){throw 'Duplicate source manifest entry'}
  $entry=$source.GetEntry($item.path);if(!$entry){throw "Missing source: $($item.path)"}
  $stream=$entry.Open()
  try{if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() -ne $item.sha256){throw "Source hash mismatch: $($item.path)"}}finally{$stream.Dispose()}
 }
 foreach($entry in $source.Entries){if(!$entry.FullName.EndsWith('/') -and $entry.FullName -ne 'SOURCE-MANIFEST.json' -and !$names.Contains($entry.FullName)){throw 'Unindexed source content'}}
 if((Get-InputFingerprint $index.files) -ne $manifest.inputSha256){throw 'Source archive differs from executable build inputs'}
}finally{$source.Dispose()}
Write-Host 'PASS complete checksums, archive safety, source integrity, executable input identity and signature status.'
