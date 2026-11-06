$ErrorActionPreference = 'Stop'

# Run this from a Visual Studio Developer PowerShell with the Sony ORBIS
# platform installed. The solution builds the RTM and PRX; without
# dependencies/self/make_fself.exe it leaves the PRX unconverted to SPRX.
$solution = Join-Path $PSScriptRoot 'bo3-entitlements.sln'
$msbuild = Get-Command msbuild.exe -ErrorAction Stop
& $msbuild.Source $solution /m /p:Configuration=Release '/p:Platform=Any CPU'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
