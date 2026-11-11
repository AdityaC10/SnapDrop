param([switch]$UpdateLocks)
. (Join-Path $PSScriptRoot 'common.ps1')
Push-Location $snapDropRoot
try {
    Test-SnapDropSdk
    $snapDropProjects = Get-ChildItem -Path src,tests,tools -Filter '*.csproj' -Recurse
    $snapDropMissingLocks = @($snapDropProjects | Where-Object { -not (Test-Path -LiteralPath (Join-Path $_.DirectoryName 'packages.lock.json')) })
    if ($UpdateLocks -or $snapDropMissingLocks.Count -gt 0) {
        Write-Warning 'Generating dependency lock files. Review and commit them before claiming a reproducible release.'
        Invoke-SnapDropDotnet restore SnapDrop.sln --use-lock-file
    } else { Invoke-SnapDropDotnet restore SnapDrop.sln --locked-mode }
    Invoke-SnapDropDotnet build SnapDrop.sln --configuration Release --no-restore
} finally { Pop-Location }
