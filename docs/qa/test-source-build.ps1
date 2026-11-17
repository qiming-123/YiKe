# Rebuild the executable inputs extracted from the distributed source ZIP, using the pinned local tools.
param([string]$Version='1.0.2',[int]$ExpectedCoreTests=60)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot)
$scratch=Join-Path $root ('artifacts/source-rebuild-'+[guid]::NewGuid().ToString('N'))
$source=Join-Path $scratch '源码 重建'
$dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'
$iscc=Join-Path $root '.tools/inno/ISCC.exe'
$manifest=Get-Content (Join-Path $root 'dist/BUILD-MANIFEST.json') -Raw | ConvertFrom-Json
$env:DOTNET_CLI_HOME=Join-Path $root '.tools/cli'
$env:NUGET_PACKAGES=Join-Path $root '.tools/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
& "$root/scripts/verify-release.ps1" -Version $Version
New-Item -ItemType Directory -Path $source -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $root "dist/YiKe-Source-$Version.zip"),$source)
Push-Location $source
try {
 & node --test tests/vscode-selection.test.cjs
 if($LASTEXITCODE){throw 'Source ZIP VS Code adapter tests failed'}
 & $dotnet restore tests/YiKe.Tests/YiKe.Tests.csproj --locked-mode -v minimal
 if($LASTEXITCODE){throw 'Source ZIP restore failed'}
 & $dotnet build tests/YiKe.Tests/YiKe.Tests.csproj -c Release --no-restore -p:Version=$Version -v minimal
 if($LASTEXITCODE){throw 'Source ZIP build failed'}
 $testOutput=& ./tests/YiKe.Tests/bin/Release/net10.0-windows/win-x64/YiKe.Tests.exe
 $testOutput | ForEach-Object {Write-Host $_}
 if($LASTEXITCODE -or !($testOutput -contains "RESULT $ExpectedCoreTests passed, 0 failed")){throw 'Extracted source tests failed or need an updated expected count'}
 $publish=Join-Path $source "artifacts/publish-$Version"
 & $dotnet publish src/YiKe/YiKe.csproj -c Release --no-restore -p:Version=$Version -p:ContinuousIntegrationBuild=true -p:PathMap="$source=/_/YiKe" -o $publish -v minimal
 if($LASTEXITCODE){throw 'Source ZIP publish failed'}
 & $iscc /Qp ('/DAppVersion='+$Version) ('/DPublishDir='+$publish) installer/YiKe.iss
 if($LASTEXITCODE){throw 'Source ZIP installer compilation failed'}
 $rebuilt=(Get-FileHash (Join-Path $publish 'YiKe.exe')).Hash.ToLowerInvariant()
 $result=[ordered]@{version=$Version;inputSha256=$manifest.inputSha256;sdk=(& $dotnet --version);sourceZipRestore=$true;sourceZipBuild=$true;coreTestsPassed=$ExpectedCoreTests;sourceZipPublish=$true;sourceZipInstallerCompiled=$true;originalAppSha256=$manifest.appSha256;rebuiltAppSha256=$rebuilt;byteIdenticalApplication=($rebuilt -eq $manifest.appSha256);independentMachine=$false;testedUtc=[DateTime]::UtcNow.ToString('o')}
 $result | ConvertTo-Json | Set-Content (Join-Path $root 'docs/SOURCE_BUILD_RESULTS.json') -Encoding utf8
 Write-Host "PASS source ZIP restore, build, $ExpectedCoreTests tests, self-contained publish and installer compilation."
}finally {
 Pop-Location
 $resolved=[IO.Path]::GetFullPath($scratch);$allowed=[IO.Path]::GetFullPath((Join-Path $root 'artifacts'))+[IO.Path]::DirectorySeparatorChar
 if(!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing an out-of-workspace cleanup'}
 if(Test-Path -LiteralPath $resolved){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
