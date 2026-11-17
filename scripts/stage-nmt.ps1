param([string]$CrtDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(!$CrtDirectory){$CrtDirectory=Join-Path $root "local-runtime/engine"}
$source=Join-Path $root '.tools/nmt-eval'
$target=Join-Path $root 'translation-runtime'
if(Test-Path -LiteralPath $target){
 $resolved=[IO.Path]::GetFullPath((Resolve-Path -LiteralPath $target).Path)
 $workspace=[IO.Path]::GetFullPath($root)+[IO.Path]::DirectorySeparatorChar
 if(!$resolved.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -ne 'translation-runtime' -or ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unsafe runtime staging target'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
New-Item -ItemType Directory $target -Force | Out-Null
foreach($file in Get-ChildItem "$source/python" -Recurse -File){
 if($file.FullName -match '[\\/]__pycache__[\\/]' -or $file.Extension -eq '.pyc' -or $file.Name -eq 'cudnn64_9.dll'){continue}
 $relative=[IO.Path]::GetRelativePath("$source/python",$file.FullName)
 $destination=Join-Path $target $relative
 New-Item -ItemType Directory ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
 Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}
New-Item -ItemType Directory "$target/model" -Force | Out-Null
Copy-Item "$source/model/*" "$target/model" -Recurse -Force
Copy-Item "$root/src/YiKe/Translation/nmt-worker.py" "$target/nmt-worker.py" -Force
foreach($name in @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll','concrt140.dll')){Copy-Item (Join-Path $CrtDirectory $name) "$target/$name" -Force}
@'
English-Chinese translation: Helsinki-NLP / OPUS-MT, Apache-2.0.
Source https://huggingface.co/Helsinki-NLP/opus-mt-en-zh
Converted INT8 weights https://huggingface.co/Second-Dawn-Lab/owll-language-packs
Archive SHA256 44cb14586160b847d0999fd161d032f505bd9c50fb17c58348ec9fe7f39c40a0
Runtime package versions and download hashes: nmt-sources.json.
Python and dependency license notices are included beside their respective modules.
'@ | Set-Content "$target/ATTRIBUTION.txt"
Copy-Item "$root/assets/nmt-source-lock.json" "$target/nmt-sources.json" -Force
Copy-Item "$root/assets/nmt-licenses" "$target/licenses" -Recurse -Force
Copy-Item "$root/assets/local-licenses/QWEN-LICENSE.txt" "$target/model/APACHE-2.0.txt" -Force
$files=@(Get-ChildItem $target -Recurse -File | Sort-Object FullName | ForEach-Object {@{path=[IO.Path]::GetRelativePath($target,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
@{format=1;model='OPUS-MT en-zh INT8';files=$files} | ConvertTo-Json -Depth 5 | Set-Content "$root/assets/nmt-package.json"
