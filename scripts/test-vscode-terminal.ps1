param([string]$Code='C:\Users\liu\AppData\Local\Programs\Microsoft VS Code\Code.exe')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$testRoot=Join-Path $projectRoot ('artifacts/terminal-live-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$workspace=Join-Path $testRoot 'yike-terminal-fixture'
$extensions=Join-Path $testRoot 'extensions'
$profile=Join-Path $testRoot 'profile'
New-Item -ItemType Directory -Path $workspace,$extensions,(Join-Path $profile 'User') -Force | Out-Null
'{"telemetry.telemetryLevel":"off","update.mode":"none","extensions.autoUpdate":false,"workbench.startupEditor":"none","workbench.welcome.enabled":false,"workbench.welcomePage.experimentalOnboarding":false,"window.title":"yike-terminal-fixture - Terminal QA - Visual Studio Code"}' | Set-Content -LiteralPath (Join-Path $profile 'User/settings.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $projectRoot 'src/YiKe.VSCode') -Destination (Join-Path $extensions 'yike-local.yike-selection-1.1.0') -Recurse
Copy-Item -LiteralPath (Join-Path $projectRoot 'tests/vscode-terminal-fixture') -Destination (Join-Path $extensions 'yike-local.terminal-fixture-1.0.0') -Recurse
$start=[Diagnostics.ProcessStartInfo]::new($Code)
$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$null=$start.Environment.Remove('ELECTRON_RUN_AS_NODE')
$start.Environment['YIKE_TERMINAL_FIXTURE']=$workspace
foreach($argument in @('--new-window','--user-data-dir',$profile,'--extensions-dir',$extensions)){$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start)
try {
 & (Join-Path $projectRoot 'tests/YiKe.Tests/bin/Release/net10.0-windows/win-x64/YiKe.Tests.exe') --vscode-terminal-live $workspace (Join-Path $projectRoot 'docs/VSCODE_TERMINAL_LIVE.json')
 if($LASTEXITCODE){throw 'Actual terminal integration failed'}
} finally {
 # Close only the isolated, disposable test profile. Never close the user's Code windows.
 foreach($candidate in Get-CimInstance Win32_Process -Filter "Name='Code.exe'"){
  if($candidate.CommandLine -and $candidate.CommandLine.Contains($profile)){
   Stop-Process -Id $candidate.ProcessId -ErrorAction SilentlyContinue
  }
 }
 $process.Dispose()
}
