function Assert-LocalRuntime([string]$Root) {
 $manifest=Get-Content (Join-Path $Root 'assets/local-package.json') -Raw | ConvertFrom-Json
 foreach($file in $manifest.files){
  if($file.path -notmatch '^(engine|models)/[A-Za-z0-9_.-]+$'){throw 'Unsafe local runtime path'}
  $path=Join-Path $Root ('local-runtime/'+$file.path)
  if(!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -ne $file.bytes -or (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $file.sha256){throw "Local runtime missing or changed: $($file.path). Run scripts/bootstrap-local.ps1."}
 }
 $engineFiles=@(Get-ChildItem (Join-Path $Root 'local-runtime/engine') -File)
 if($engineFiles.Count -ne @($manifest.files | Where-Object path -Like 'engine/*').Count){throw 'Unexpected local engine files'}
 return $manifest
}
function Copy-LocalRuntime([string]$Root,[string]$Destination,$Manifest) {
 foreach($file in $Manifest.files){
  $output=Join-Path $Destination ('local-runtime/'+$file.path)
  New-Item -ItemType Directory -Path (Split-Path $output) -Force | Out-Null
  Copy-Item -LiteralPath (Join-Path $Root ('local-runtime/'+$file.path)) -Destination $output
 }
 Copy-Item -LiteralPath (Join-Path $Root 'assets/local-licenses') -Destination (Join-Path $Destination 'licenses') -Recurse
}
