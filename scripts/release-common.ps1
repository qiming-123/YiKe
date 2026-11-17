# Shared, deterministic identity checks. Documentation is deliberately independent of executable inputs.
function Get-ReleaseSourceFiles([string]$Root) {
 $files=@(Get-ChildItem (Join-Path $Root 'src'),(Join-Path $Root 'tests'),(Join-Path $Root 'scripts'),(Join-Path $Root 'installer'),(Join-Path $Root 'assets'),(Join-Path $Root 'docs'),(Join-Path $Root 'tools') -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
 $files+=Get-Item (Join-Path $Root 'README.md'),(Join-Path $Root 'RELEASE_NOTES.md'),(Join-Path $Root 'THIRD_PARTY_NOTICES.md'),(Join-Path $Root 'LICENSE'),(Join-Path $Root 'global.json'),(Join-Path $Root 'build-tools.json'),(Join-Path $Root 'local-model.lock.json'),(Join-Path $Root '.gitignore')
 return $files
}
function Test-ExecutableInput([string]$Relative) { return $Relative -match '^(src|tests|scripts|installer|assets|tools)/|^(global\.json|build-tools\.json|local-model\.lock\.json|LICENSE)$' }
function Get-InputFingerprint([object[]]$Entries) {
 [string[]]$lines=@($Entries | Where-Object { Test-ExecutableInput $_.path } | ForEach-Object { $_.path+':'+$_.sha256.ToLowerInvariant() })
 [Array]::Sort($lines,[StringComparer]::Ordinal)
 return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($lines -join "`n")))).ToLowerInvariant()
}
function Get-SourceEntries([string]$Root) {
 return @(Get-ReleaseSourceFiles $Root | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($Root,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} })
}
function New-ReleaseZip([string]$Directory,[string]$Destination) {
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 if(Test-Path -LiteralPath $Destination){[IO.File]::Delete($Destination)}
 [IO.Compression.ZipFile]::CreateFromDirectory($Directory,$Destination,[IO.Compression.CompressionLevel]::Optimal,$false)
}
function Assert-SafeZip($Zip) {
 $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
 foreach($entry in $Zip.Entries){
  $name=$entry.FullName
  $modelBinary=$name -eq 'translation-runtime/model/model.bin'
  if(!$names.Add($name) -or $name -match '(^/|^[A-Za-z]:|\\|(^|/)\.\.(/|$)|(^|/)(settings\.json|credentials)(/|$)|^data(/|$)|\.(yike|pfx|p12|pvk|key|snk|jks|keystore|pem)$)' -or (!$modelBinary -and $name -match '\.bin$')){throw "Unsafe or personal archive entry: $name"}
 }
}
