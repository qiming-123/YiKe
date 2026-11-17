param([string]$Version='1.7.8')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
. "$PSScriptRoot/release-common.ps1"
$receipt=Get-Content (Join-Path $root "artifacts/build-receipt-$Version.json") -Raw | ConvertFrom-Json
if((Get-InputFingerprint (Get-SourceEntries $root)) -ne $receipt.inputSha256){throw 'Run a full build before testing release rejection paths'}
$dist=Join-Path $root 'dist'
$scratch=Join-Path $root ('artifacts/release-negative-'+[guid]::NewGuid().ToString('N'))
$passed=0
function Expect-Failure([string]$Name,[string]$Expected,[scriptblock]$Action) {
 try { & $Action | Out-Null; throw "Did not reject: $Name" }
 catch { if($_.Exception.Message -notlike ('*'+$Expected+'*')){throw}; Write-Host "PASS $Name"; $script:passed++ }
}
function Reset-Case {
 New-Item -ItemType Directory -Path $scratch -Force | Out-Null
 foreach($name in @("YiKe-Setup-$Version-win-x64.exe","YiKe-Portable-$Version-win-x64.zip","YiKe-Source-$Version.zip",'BUILD-MANIFEST.json','SHA256SUMS.txt','README.md','RELEASE_NOTES.md')){Copy-Item -LiteralPath (Join-Path $dist $name) -Destination $scratch -Force}
}
function Refresh-Checksum([string]$Name) {
 $hash=(Get-FileHash -LiteralPath (Join-Path $scratch $Name)).Hash.ToLowerInvariant()
 $lines=@(Get-Content (Join-Path $scratch 'SHA256SUMS.txt') | ForEach-Object {if($_.EndsWith('  '+$Name)){$hash+'  '+$Name}else{$_}})
 $lines | Set-Content (Join-Path $scratch 'SHA256SUMS.txt') -Encoding ascii
}
function Change-Source([switch]$Reindex) {
 $path=Join-Path $scratch "YiKe-Source-$Version.zip"
 $zip=[IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Update)
 try {
  $entry=$zip.GetEntry('src/YiKe/UI/Styles.xaml');$input=$entry.Open()
  try{$reader=[IO.StreamReader]::new($input);$text=$reader.ReadToEnd()}finally{$input.Dispose()}
  $text+="`n<!-- Deliberate negative integrity test -->"
  $entry.Delete();$entry=$zip.CreateEntry('src/YiKe/UI/Styles.xaml');$output=$entry.Open()
  try{$bytes=[Text.Encoding]::UTF8.GetBytes($text);$output.Write($bytes)}finally{$output.Dispose()}
  if($Reindex){
   $entry=$zip.GetEntry('SOURCE-MANIFEST.json');$input=$entry.Open()
   try{$reader=[IO.StreamReader]::new($input);$index=$reader.ReadToEnd() | ConvertFrom-Json}finally{$input.Dispose()}
   ($index.files | Where-Object path -eq 'src/YiKe/UI/Styles.xaml').sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
   $entry.Delete();$output=$zip.CreateEntry('SOURCE-MANIFEST.json').Open()
   try{$data=[Text.Encoding]::UTF8.GetBytes(($index | ConvertTo-Json -Depth 6));$output.Write($data)}finally{$output.Dispose()}
  }
 }finally{$zip.Dispose()}
 Refresh-Checksum "YiKe-Source-$Version.zip"
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
 & "$PSScriptRoot/verify-release.ps1" -Version $Version
 $passed++; Write-Host 'PASS unmodified release'
 Reset-Case
 Get-Content (Join-Path $scratch 'SHA256SUMS.txt') | Where-Object { ! $_.EndsWith('  README.md') } | Set-Content (Join-Path $scratch 'incomplete.txt')
 Move-Item -LiteralPath (Join-Path $scratch 'incomplete.txt') -Destination (Join-Path $scratch 'SHA256SUMS.txt') -Force
 Expect-Failure 'Missing checksum rejected' 'Missing release checksums' { & "$PSScriptRoot/verify-release.ps1" -Version $Version -Directory $scratch }
 Reset-Case
 $zip=[IO.Compression.ZipFile]::Open((Join-Path $scratch "YiKe-Portable-$Version-win-x64.zip"),[IO.Compression.ZipArchiveMode]::Update)
 try{$stream=$zip.CreateEntry('../outside.txt').Open();$stream.WriteByte(65);$stream.Dispose()}finally{$zip.Dispose()}
 Refresh-Checksum "YiKe-Portable-$Version-win-x64.zip"
 Expect-Failure 'ZIP traversal rejected despite updated outer checksum' 'Unsafe or personal archive entry' { & "$PSScriptRoot/verify-release.ps1" -Version $Version -Directory $scratch }
 Reset-Case
 Change-Source
 Expect-Failure 'Changed source rejected despite updated outer checksum' 'Source hash mismatch' { & "$PSScriptRoot/verify-release.ps1" -Version $Version -Directory $scratch }
 Reset-Case
 Change-Source -Reindex
 Expect-Failure 'Reindexed changed source rejected by build identity' 'Source archive differs from executable build inputs' { & "$PSScriptRoot/verify-release.ps1" -Version $Version -Directory $scratch }
 if($receipt.localPackage){
  Reset-Case
  $changedManifest=Get-Content (Join-Path $scratch 'BUILD-MANIFEST.json') -Raw | ConvertFrom-Json
  $changedManifest.localPackage.model='deliberately-changed-model'
  $changedManifest|ConvertTo-Json -Depth 8|Set-Content (Join-Path $scratch 'BUILD-MANIFEST.json') -Encoding utf8
  Refresh-Checksum 'BUILD-MANIFEST.json'
  Expect-Failure 'Changed offline identity rejected despite updated checksum' 'Local package differs from pinned source manifest' { & "$PSScriptRoot/verify-release.ps1" -Version $Version -Directory $scratch }
 }
 $project=Join-Path $root 'src/YiKe/YiKe.csproj';$original=[IO.File]::ReadAllBytes($project)
 try {
  [IO.File]::AppendAllText($project,"`n<!-- Deliberate package-only rejection test -->")
  Expect-Failure 'PackageOnly rejects changed executable inputs' 'Build receipt mismatch' { & "$PSScriptRoot/build-release.ps1" -Version $Version -PackageOnly }
 }finally{[IO.File]::WriteAllBytes($project,$original)}
 [ordered]@{version=$Version;passed=$passed;failed=0;testedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $root 'artifacts/release-verification-tests.json') -Encoding utf8
 Write-Host "RELEASE RESULT $passed passed, 0 failed"
}finally{
 $resolved=[IO.Path]::GetFullPath($scratch)
 $allowed=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar
 if(!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing out-of-workspace cleanup'}
 if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
