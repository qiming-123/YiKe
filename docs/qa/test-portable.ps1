# Optional acceptance procedure; run after a full build, only with no existing YiKe user data.
param([string]$Version='1.0.1')
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot)
$scratch=Join-Path $root ('artifacts/便携 验收-'+[guid]::NewGuid().ToString('N'))
$original=Join-Path $scratch '原 目录/译刻'
$moved=Join-Path $scratch '移动 后/译刻'
$onlyExe=Join-Path $scratch '仅 EXE'
$fixture=Join-Path $root 'tests/YiKe.Tests/bin/Release/net10.0-windows/win-x64/YiKe.Tests.exe'
$userDir=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'YiKe'
$userFile=Join-Path $userDir 'settings.json'
$results=[Collections.Generic.List[object]]::new()
$seeded=$false
function Assert($value,[string]$message){if(!$value){throw $message}}
function Verify-Fixture { & $fixture --install-data verify; if($LASTEXITCODE){throw 'Fixture data did not survive portable operation'} }
function Check-Run([string]$Directory,[string]$Case) {
 $exe=Join-Path $Directory 'YiKe.exe'
 $info=[Diagnostics.ProcessStartInfo]::new($exe);$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WorkingDirectory=$root
 $info.Environment['DOTNET_ROOT']=Join-Path $scratch 'runtime-not-installed'
 $timer=[Diagnostics.Stopwatch]::StartNew();$process=[Diagnostics.Process]::Start($info)
 try {
  Assert ($process.WaitForInputIdle(15000)) 'Application did not become idle'
  Assert (!$process.HasExited) 'Application exited unexpectedly'
  $timer.Stop();$process.Refresh();$memory=$process.WorkingSet64
  & $exe --shutdown
  Assert ($process.WaitForExit(10000)) 'Application did not exit normally'
  Verify-Fixture
  $results.Add([ordered]@{case=$Case;passed=$true;inputIdleMs=$timer.ElapsedMilliseconds;workingSetBytes=$memory})
 }finally{if(!$process.HasExited){& $exe --shutdown;Assert ($process.WaitForExit(10000)) 'Owned QA application remains running'};$process.Dispose()}
}
Push-Location $root
try {
 Assert (!(Test-Path -LiteralPath $userFile)) 'Existing YiKe settings must be preserved'
 Assert (!(Test-Path -LiteralPath (Join-Path $userDir 'credentials'))) 'Existing YiKe credentials must be preserved'
 & ./scripts/verify-release.ps1 -Version $Version
 & $fixture --install-data seed; if($LASTEXITCODE){throw 'Could not seed synthetic data'};$seeded=$true
 New-Item -ItemType Directory -Path $original -Force | Out-Null
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $root "dist/YiKe-Portable-$Version-win-x64.zip"),$original)
 $manifest=Get-Content dist/BUILD-MANIFEST.json -Raw | ConvertFrom-Json
 Assert ((Get-FileHash (Join-Path $original 'YiKe.exe')).Hash.ToLowerInvariant() -eq $manifest.appSha256) 'Portable EXE does not match the manifest'
 New-Item -ItemType Directory -Path (Join-Path $original 'data') | Out-Null
 Copy-Item -LiteralPath $userFile -Destination (Join-Path $original 'data/settings.json')
 $initial=(Get-FileHash $userFile).Hash
 Check-Run $original 'Extracted ZIP in Chinese/space path, different working directory, invalid DOTNET_ROOT'
 Assert ((Get-FileHash $userFile).Hash -eq $initial) 'Portable startup changed valid user data'
 New-Item -ItemType Directory -Path (Split-Path $moved) -Force | Out-Null
 $allowed=[IO.Path]::GetFullPath($scratch)+[IO.Path]::DirectorySeparatorChar
 foreach($path in @($original,$moved)){if(![IO.Path]::GetFullPath($path).StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing an out-of-workspace directory move'}}
 Move-Item -LiteralPath $original -Destination $moved
 Assert ((Test-Path -LiteralPath (Join-Path $moved 'portable.flag')) -and (Test-Path -LiteralPath (Join-Path $moved 'data/settings.json'))) 'Whole-folder move lost portable state'
 Check-Run $moved 'Whole portable directory moved'
 $portableFile=Join-Path $moved 'data/settings.json'
 [IO.File]::SetAttributes($portableFile,[IO.FileAttributes]::ReadOnly)
 $settings=Get-Content $userFile -Raw | ConvertFrom-Json;$settings.Revision++;$settings.FontSize=18
 $settings | ConvertTo-Json -Depth 5 | Set-Content $userFile -Encoding utf8
 $newer=(Get-FileHash $userFile).Hash
 Check-Run $moved 'Read-only older portable copy retains the newer same-ID user revision'
 Assert ((Get-FileHash $userFile).Hash -eq $newer) 'An older portable copy replaced the newer user revision'
 [IO.File]::SetAttributes($portableFile,[IO.FileAttributes]::Normal)
 New-Item -ItemType Directory -Path $onlyExe | Out-Null
 Copy-Item -LiteralPath (Join-Path $moved 'YiKe.exe') -Destination $onlyExe
 Check-Run $onlyExe 'Only EXE copied without portable.flag, current user fallback remains valid'
 Assert ((Get-FileHash $userFile).Hash -eq $newer) 'EXE-only startup changed the user revision'
 [ordered]@{version=$Version;appSha256=$manifest.appSha256;passed=$results.Count;failed=0;results=$results;realApiTested=$false;cleanMachine=$false;testedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json -Depth 6 | Set-Content docs/PORTABLE_RESULTS.json -Encoding utf8
 Write-Host "PORTABLE RESULT $($results.Count) passed, 0 failed"
}finally {
 if($seeded){& $fixture --install-data cleanup; if($LASTEXITCODE){throw 'Synthetic data cleanup failed'}}
 $resolved=[IO.Path]::GetFullPath($scratch);$allowed=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar
 if(!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing an out-of-workspace cleanup'}
 if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
 Pop-Location
}
