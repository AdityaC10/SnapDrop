param([switch]$Interactive, [switch]$NoBuild)
. (Join-Path $PSScriptRoot 'common.ps1')
Push-Location $snapDropRoot
try {
    Test-SnapDropSdk
    if (-not $NoBuild) { & (Join-Path $PSScriptRoot 'build.ps1') }
    if ($Interactive) {
        if ($env:SNAPDROP_INTERACTIVE_TESTS -ne '1') { throw 'Interactive tests change the real clipboard. In a dedicated unlocked test session set SNAPDROP_INTERACTIVE_TESTS=1, then rerun with -Interactive.' }
        Invoke-SnapDropDotnet test tests/SnapDrop.Windows.Tests/SnapDrop.Windows.Tests.csproj --configuration Release --no-build --filter 'Category=InteractiveWindows' --logger trx --results-directory artifacts/TestResults
    } else {
        Invoke-SnapDropDotnet test SnapDrop.sln --configuration Release --no-build --filter 'Category!=InteractiveWindows' --logger trx --results-directory artifacts/TestResults
    }
} finally { Pop-Location }
