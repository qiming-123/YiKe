$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$lock=Get-Content (Join-Path $root 'local-model.lock.json') -Raw|ConvertFrom-Json
$lines=@('// Generated from local-model.lock.json; do not edit by hand.')
foreach($pair in @(@('Light',$lock.lightModel),@('Standard',$lock.model))){
 $name=$pair[0];$model=$pair[1]
 if($model.file -notmatch '^[A-Za-z0-9_.-]+\.gguf$' -or $model.sha256 -notmatch '^[0-9a-f]{64}$' -or $model.url -notmatch '^https://huggingface\.co/ggml-org/[A-Za-z0-9.-]+/resolve/[0-9a-f]{40}/[A-Za-z0-9_.-]+\.gguf\?download=true$'){throw 'Invalid pinned model download'}
 $lines+='#define '+$name+'ModelFile "'+$model.file+'"'
 $lines+='#define '+$name+'ModelUrl "'+$model.url+'"'
 $lines+='#define '+$name+'ModelSha256 "'+$model.sha256+'"'
 $lines+='#define '+$name+'ModelBytes '+$model.bytes
}
[IO.File]::WriteAllLines((Join-Path $root 'installer/model-downloads.iss'),$lines,[Text.UTF8Encoding]::new($false))
