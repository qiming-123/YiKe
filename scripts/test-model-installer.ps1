param([string]$Version='1.7.8',[string]$PublishDir='',[switch]$RunLocalCheck)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
if(!$PublishDir){$PublishDir=Join-Path $root ('artifacts/publish-'+$Version)}
$tag=[guid]::NewGuid().ToString('N').Substring(0,8)
$target=Join-Path $root ('artifacts/model-installer-'+$tag)
$base='model-installer-check-'+$tag
$compiler=Join-Path $root '.tools/inno/ISCC.exe'
& $compiler /Qp /DInstallerSmoke=1 ('/DAppVersion='+$Version) ('/DPublishDir='+$PublishDir) ('/F'+$base) (Join-Path $root 'installer/YiKe.iss')
if($LASTEXITCODE){throw 'Model installer fixture compilation failed'}
$setup=Join-Path $root ('artifacts/'+$base+'.exe')
$userRoot=Join-Path $env:LOCALAPPDATA 'YiKe'
function UserHashes {
 $hashes=@{}
 foreach($file in @(Get-ChildItem -LiteralPath $userRoot -Filter 'settings.json' -File -ErrorAction SilentlyContinue)+@(Get-ChildItem -LiteralPath (Join-Path $userRoot 'credentials') -Filter '*.bin' -File -ErrorAction SilentlyContinue)){$hashes[$file.Name]=(Get-FileHash -LiteralPath $file.FullName).Hash}
 return $hashes
}
$before=UserHashes
$appKey='Software\Microsoft\Windows\CurrentVersion\Uninstall\{3C0A80D1-1257-472C-BC71-91BEE87C63F9}_is1'
function Read-ActualInstall {
 $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($appKey)
 try{if($key){return $key.GetValue('InstallLocation')}}finally{if($key){$key.Dispose()}}
}
$originalInstall=Read-ActualInstall
$results=@()
function Install-Choice([string]$Choice,[string]$Exe=$setup,[string]$Destination=$target,[bool]$Failure=$false){
 $log=Join-Path $root ('artifacts/'+$base+'-'+$Choice+$(if($Failure){'-rejected'})+'.log')
 $p=Start-Process -FilePath $Exe -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOCALMODELS='+$Choice),('/DIR="'+$Destination+'"'),('/LOG="'+$log+'"')) -WindowStyle Hidden -Wait -PassThru
 if((!$Failure -and $p.ExitCode -ne 0) -or ($Failure -and $p.ExitCode -eq 0)){throw ('Unexpected installer exit code for '+$Choice+': '+$p.ExitCode)}
 if(!$Failure -and (Get-Content -LiteralPath $log -Raw) -match 'Downloading temporary file from https:'){throw 'An already verified model was downloaded again'}
 return $log
}
$null=Install-Choice 'none'
if(@(Get-ChildItem -LiteralPath (Join-Path $target 'local-runtime/models') -Filter '*.gguf' -ErrorAction SilentlyContinue).Count){throw 'No-model install copied a model'}
$results+=@{check='skip models installs no GGUF';passed=$true}
$models=Join-Path $target 'local-runtime/models';New-Item -ItemType Directory -Path $models -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $root 'local-runtime/models/Qwen3-0.6B-Q4_0.gguf') -Destination $models
$null=Install-Choice 'light'
$plan=Join-Path $target 'local-model.ini'
if((Get-Content $plan -Raw) -notmatch 'Selection=light'){throw 'Explicit light preference was not saved'}
if(Test-Path -LiteralPath (Join-Path $models 'Qwen3-1.7B-Q4_K_M.gguf')){throw 'Light-only install added a standard model'}
$results+=@{check='reuse verified light model and save light preference';passed=$true}
if($RunLocalCheck){
 & (Join-Path $root 'tests/YiKe.Tests/bin/Release/net10.0-windows/win-x64/YiKe.Tests.exe') --local-live (Join-Path $target 'local-runtime') (Join-Path $root 'artifacts/single-light-local-live.json')
 if($LASTEXITCODE){throw 'Single-model installation could not run its real local engine'}
 $live=Get-Content (Join-Path $root 'artifacts/single-light-local-live.json') -Raw|ConvertFrom-Json
 if($live.model -ne 'Qwen3-0.6B-Q4_0' -or $live.simulatedHardware){throw 'Explicit light profile was not used on this physical computer'}
 $results+=@{check='light-only installation translates on real high-memory hardware';passed=$true}
}
Copy-Item -LiteralPath (Join-Path $root 'local-runtime/models/Qwen3-1.7B-Q4_K_M.gguf') -Destination $models
$null=Install-Choice 'standard'
if((Get-Content $plan -Raw) -notmatch 'Selection=standard'){throw 'Standard choice not saved'}
$results+=@{check='reuse verified standard model and save standard preference';passed=$true}
$null=Install-Choice 'auto'
if((Get-Content $plan -Raw) -notmatch 'Selection=auto'){throw 'Dual-model automatic choice not saved'}
$results+=@{check='reuse both models with automatic hardware selection';passed=$true}
$oldPlan=(Get-FileHash -LiteralPath $plan).Hash
$null=Install-Choice 'none'
if((Get-FileHash -LiteralPath $plan).Hash -ne $oldPlan){throw 'Skipping downloads changed an existing preference'}
$results+=@{check='skip during upgrade preserves installed models and preference';passed=$true}
$null=Install-Choice 'invalid' -Failure $true
$results+=@{check='invalid silent model selection is rejected';passed=$true}
$badBase=$base+'-bad'
& $compiler /Qp /DInstallerSmoke=1 /DInstallerSmokeBadDownload=1 ('/DAppVersion='+$Version) ('/DPublishDir='+$PublishDir) ('/F'+$badBase) (Join-Path $root 'installer/YiKe.iss')
if($LASTEXITCODE){throw 'Download rejection fixture compilation failed'}
$badTarget=$target+'-rejected'
$badLog=Install-Choice 'light' (Join-Path $root ('artifacts/'+$badBase+'.exe')) $badTarget $true
if(Test-Path -LiteralPath (Join-Path $badTarget 'YiKe.exe')){throw 'Failed download installed application files'}
if((Get-Content $badLog -Raw) -notmatch '(?i)(hash|SHA-256).*(mismatch|incorrect|不匹配|failed)|哈希值不正确|expected.*SHA'){throw 'The negative download did not reach a checksum rejection'}
$results+=@{check='wrong download SHA-256 is rejected before installing files';passed=$true}
$uninstall=Start-Process -FilePath (Join-Path $target 'unins000.exe') -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -Wait -PassThru
if($uninstall.ExitCode -ne 0){throw 'Fixture uninstall failed'}
$after=UserHashes
$same=$before.Count -eq $after.Count;foreach($name in $before.Keys){if($before[$name] -ne $after[$name]){$same=$false}}
if(!$same -or (Read-ActualInstall) -ne $originalInstall){throw 'Installer fixture changed real user state'}
$results+=@{check='real user data and production install registration preserved';passed=$true}
[ordered]@{version=$Version;fixtureAppId='YiKe.Installer.ModelSmoke';passed=$results.Count;failed=0;userDataByteIdentical=$same;productionInstallRegistrationUnchanged=$true;checks=$results;checkedUtc=[DateTimeOffset]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $root 'artifacts/model-installer-results.json') -Encoding utf8
Write-Host ('MODEL INSTALLER RESULT '+$results.Count+' passed, 0 failed')
