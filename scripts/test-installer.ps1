param([string]$Version='1.0.1',[string]$PreviousVersion='1.0.0',[string]$PreviousSetup='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$stage=Join-Path $root 'artifacts/安装 测试/YiKe'
$logs=Join-Path $root ('artifacts/installer-qa-'+$Version)
$fixture=Join-Path $root 'tests/YiKe.Tests/bin/Release/net10.0-windows/win-x64/YiKe.Tests.exe'
$userDir=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'YiKe'
$uninstallKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{3C0A80D1-1257-472C-BC71-91BEE87C63F9}_is1'
$runKey='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startMenuLink=Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Microsoft/Windows/Start Menu/Programs/译刻/译刻.lnk'
$desktopLink=Join-Path ([Environment]::GetFolderPath('Desktop')) '译刻.lnk'
if((Test-Path $uninstallKey) -or (Test-Path (Join-Path $userDir 'settings.json')) -or (Get-ItemProperty -Path $runKey -ErrorAction SilentlyContinue).YiKe){throw 'Refusing to overwrite an existing YiKe installation, startup entry or user settings.'}
New-Item -ItemType Directory -Path $logs -Force | Out-Null
if(!$PreviousSetup){$PreviousSetup=Join-Path $root "dist/YiKe-Setup-$PreviousVersion-win-x64.exe"}
if(!(Test-Path -LiteralPath $PreviousSetup)){throw 'Supply the actual previously released installer with -PreviousSetup; do not manufacture a historical build from current source.'}
if($PreviousVersion -eq '1.0.0' -and (Get-FileHash $PreviousSetup).Hash -ne '33F0731311162CBB389D1F89814E43DE17F8F5BBCDC2A0DC421080EE50421965'){throw 'The 1.0.0 fixture differs from the previously delivered installer'}
if((Test-Path -LiteralPath $startMenuLink) -or (Test-Path -LiteralPath $desktopLink)){throw 'Refusing to replace pre-existing shortcuts'}
function Setup-Path($ver){if($ver -eq $PreviousVersion){return $PreviousSetup};return (Join-Path $root "dist/YiKe-Setup-$ver-win-x64.exe")}
function Run-Setup($ver,$name,[string[]]$Options=@()) {
 $exe=Setup-Path $ver
 $arguments=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$stage+'"'),('/LOG="'+(Join-Path $logs ($name+'.log'))+'"'))+$Options
 $p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
 if($p.ExitCode){throw "Setup $name failed: $($p.ExitCode)"}
}
function Assert($condition,$message){if(!$condition){throw $message}}
function Verify-Data{& $fixture --install-data verify; if($LASTEXITCODE){throw 'Fixture verification failed'}}
function Uninstall($name){$p=Start-Process -FilePath (Join-Path $stage 'unins000.exe') -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $logs ($name+'.log'))+'"') -WindowStyle Hidden -PassThru -Wait;if($p.ExitCode){throw "Uninstall failed: $($p.ExitCode)"}}
Run-Setup $PreviousVersion 'install-old'
Assert ((Get-ItemPropertyValue $uninstallKey -Name DisplayVersion) -eq $PreviousVersion) 'Wrong old version registration'
Assert (Test-Path -LiteralPath $startMenuLink) 'Start Menu entry missing'
Assert (!(Test-Path -LiteralPath $desktopLink)) 'Desktop shortcut should default to unchecked'
& $fixture --install-data seed; if($LASTEXITCODE){throw 'Could not seed synthetic data'}
$before=(Get-FileHash (Join-Path $userDir 'settings.json')).Hash
Set-ItemProperty -Path $runKey -Name YiKe -Value ('"'+(Join-Path $stage 'YiKe.exe')+'"')
$watch=[Diagnostics.Stopwatch]::StartNew();$app=Start-Process -FilePath (Join-Path $stage 'YiKe.exe') -WindowStyle Hidden -PassThru
$idle=$app.WaitForInputIdle(15000);$watch.Stop();$app.Refresh()
Assert (!$app.HasExited) 'Self-contained old application did not start'
$memory=$app.WorkingSet64
Run-Setup $Version 'upgrade-while-running'
Assert ($app.WaitForExit(10000)) 'Old application did not gracefully exit during upgrade'
Verify-Data
Assert ((Get-FileHash (Join-Path $userDir 'settings.json')).Hash -eq $before) 'Upgrade changed settings bytes'
 $finalWatch=[Diagnostics.Stopwatch]::StartNew()
 $finalInfo=[Diagnostics.ProcessStartInfo]::new((Join-Path $stage 'YiKe.exe'));$finalInfo.UseShellExecute=$false;$finalInfo.CreateNoWindow=$true;$finalInfo.WorkingDirectory=$root;$finalInfo.Environment['DOTNET_ROOT']=Join-Path $logs 'runtime-not-installed'
 $finalApp=[Diagnostics.Process]::Start($finalInfo);$finalIdle=$finalApp.WaitForInputIdle(15000);$finalWatch.Stop();$finalApp.Refresh();Assert (!$finalApp.HasExited) 'Final self-contained app did not start';$finalMemory=$finalApp.WorkingSet64
 & (Join-Path $stage 'YiKe.exe') --shutdown
 Assert ($finalApp.WaitForExit(10000)) 'Final app did not exit normally'
Assert ((Get-ItemPropertyValue $uninstallKey -Name DisplayVersion) -eq $Version) 'Upgrade version not registered'
Assert ((Get-FileHash (Join-Path $stage 'YiKe.exe')).Hash -eq (Get-FileHash (Join-Path $root "artifacts/publish-$Version/YiKe.exe")).Hash) 'Installed EXE differs from final publish'
Assert ((Get-ItemPropertyValue $runKey -Name YiKe) -eq ('"'+(Join-Path $stage 'YiKe.exe')+'"')) 'Startup preference not preserved'
Remove-ItemProperty -Path $runKey -Name YiKe
Run-Setup $Version 'repeat-same-version'
Assert (!(Get-ItemProperty $runKey -ErrorAction SilentlyContinue).YiKe) 'Disabled startup recreated'
Verify-Data
$down=Start-Process -FilePath (Setup-Path $PreviousVersion) -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $logs 'downgrade.log')+'"') -WindowStyle Hidden -PassThru -Wait
Assert ($down.ExitCode -ne 0) 'Downgrade was not rejected'
Assert ((Get-ItemPropertyValue $uninstallKey -Name DisplayVersion) -eq $Version) 'Downgrade changed registration'
# Explicit tasks override preservation; unrelated merged tasks preserve the actual current Run state.
Run-Setup $Version 'explicit-startup' @('/TASKS=desktopicon,startup')
Assert ((Get-ItemProperty $runKey).YiKe) 'Explicit startup task was ignored'
Assert (Test-Path -LiteralPath $desktopLink) 'Explicit desktop shortcut missing'
Remove-ItemProperty -Path $runKey -Name YiKe
Run-Setup $Version 'merge-desktop-preserves-startup' @('/MERGETASKS=desktopicon')
Assert (!(Get-ItemProperty $runKey).YiKe) 'Unrelated task recreated user-disabled startup'
Run-Setup $Version 'merge-startup' @('/MERGETASKS=startup')
Assert ((Get-ItemProperty $runKey).YiKe) 'Explicit merged startup task was ignored'
Run-Setup $Version 'merge-disable-startup' @('/MERGETASKS=!startup')
Assert (!(Get-ItemProperty $runKey).YiKe) 'Explicit startup deselection was ignored'
# Hold only our installed EXE, and disable Restart Manager closing applications for this deliberate failure.
$installed=Join-Path $stage 'YiKe.exe';$installedHash=(Get-FileHash $installed).Hash
$lock=[IO.File]::Open($installed,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
try{
 $failed=Start-Process -FilePath (Setup-Path $Version) -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS','/NOFORCECLOSEAPPLICATIONS',('/DIR="'+$stage+'"'),('/LOG="'+(Join-Path $logs 'locked-file-failure.log')+'"') -WindowStyle Hidden -PassThru -Wait
 Assert ($failed.ExitCode -ne 0) 'Locked executable install incorrectly reported success'
}finally{$lock.Dispose()}
Assert ((Get-FileHash $installed).Hash -eq $installedHash) 'Failed installation changed the existing EXE'
Assert ((Get-FileHash (Join-Path $userDir 'settings.json')).Hash -eq $before) 'Failed installation changed user settings'
Verify-Data
Run-Setup $Version 'recover-after-locked-file'
$runningBeforeUninstall=Start-Process -FilePath $installed -WindowStyle Hidden -PassThru
Assert ($runningBeforeUninstall.WaitForInputIdle(15000)) 'Application could not run after failed installation recovery'
Uninstall 'default-uninstall'
Assert ($runningBeforeUninstall.WaitForExit(10000)) 'Uninstall did not normally stop the application'
Assert (!(Test-Path (Join-Path $stage 'YiKe.exe'))) 'Application still exists after uninstall'
Assert (!(Test-Path $uninstallKey)) 'Uninstall registration still exists'
Assert (!(Test-Path -LiteralPath $startMenuLink)) 'Start Menu entry remains after uninstall'
Assert (!(Test-Path -LiteralPath $desktopLink)) 'Desktop shortcut remains after uninstall'
Verify-Data
Run-Setup $Version 'reinstall'
Verify-Data
Uninstall 'final-uninstall'
& $fixture --install-data cleanup; if($LASTEXITCODE){throw 'Synthetic fixture cleanup failed'}
$result=[ordered]@{version=$Version;previousVersion=$PreviousVersion;previousSetupSha256=(Get-FileHash $PreviousSetup).Hash;appSha256=(Get-FileHash (Join-Path $root "artifacts/publish-$Version/YiKe.exe")).Hash;coldStartInputIdleMs=$finalWatch.ElapsedMilliseconds;inputIdle=$finalIdle;workingSetBytes=$finalMemory;fixtureStartMs=$watch.ElapsedMilliseconds;install=$true;runningUpgrade=$true;sameVersion=$true;downgradeRejected=$true;defaultUninstallKeepsData=$true;reinstallRestoresData=$true;startupPreferencePreserved=$true;explicitTasks=$true;desktopShortcut=$true;lockedFileFailure=$true;recoveredAfterFailure=$true;runningUninstall=$true;installedExeSameAsPublish=$true;finalAppStartsWithNonexistentDotnetRoot=$true;interactiveDataDeletion='not-tested';testPath='Chinese and space path';testedUtc=[DateTime]::UtcNow.ToString('o')}
$result | ConvertTo-Json | Set-Content (Join-Path $logs 'RESULTS.json') -Encoding utf8
Write-Host 'PASS installation, running upgrade, repeat install, downgrade rejection, uninstall, reinstall, startup and data preservation.'
