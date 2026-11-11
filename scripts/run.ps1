param([switch]$NoBuild)
. (Join-Path $PSScriptRoot 'common.ps1')
Push-Location $snapDropRoot
try {
    Test-SnapDropSdk
    if (-not $NoBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
    Invoke-SnapDropDotnet run --project src/SnapDrop.App/SnapDrop.App.csproj --configuration Release --no-build
} finally { Pop-Location }
