$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot
$dotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
& $dotnet run --project (Join-Path $projectRoot 'tools/YiKe.Brand/YiKe.Brand.csproj') -c Release -- (Join-Path $projectRoot 'assets')
if ($LASTEXITCODE) { throw 'Shared WPF brand export failed' }
