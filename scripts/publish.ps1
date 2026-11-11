param([switch]$Installer, [string]$SignTool, [string]$CertificateThumbprint, [string]$TimestampUrl)
. (Join-Path $PSScriptRoot 'common.ps1')
Push-Location $snapDropRoot
try {
    Test-SnapDropSdk
    & (Join-Path $PSScriptRoot 'build.ps1')
    & (Join-Path $PSScriptRoot 'test.ps1') -NoBuild
    $snapDropPublish = Join-Path $snapDropRoot 'artifacts\publish\win-x64'
    $snapDropRelease = Join-Path $snapDropRoot 'artifacts\release'
    New-Item -ItemType Directory -Force -Path $snapDropRelease | Out-Null
    # Publish to a fresh versioned directory; never delete an unchecked computed path.
    $snapDropRun = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $snapDropPublish = Join-Path $snapDropPublish $snapDropRun
    # Bind native arguments as an array: PowerShell otherwise treats repeated -p:
    # MSBuild properties as its own PipelineVariable common parameter.
    Invoke-SnapDropDotnet -Arguments @('publish', 'src/SnapDrop.App/SnapDrop.App.csproj', '--configuration', 'Release', '--runtime', 'win-x64', '--self-contained', 'true', '-p:RestoreLockedMode=true', '-p:PublishTrimmed=false', '-p:PublishSingleFile=false', '--output', $snapDropPublish)
    $snapDropSigned = $false
    if ($CertificateThumbprint) {
        if (-not $SignTool -or -not $TimestampUrl) { throw 'Provide SignTool and an HTTPS TimestampUrl together with CertificateThumbprint.' }
        if ($TimestampUrl -notmatch '^https://') { throw 'The timestamp URL must use HTTPS.' }
        Get-ChildItem -LiteralPath $snapDropPublish -Filter 'SnapDrop.*' -File | Where-Object { $_.Extension -in '.dll','.exe' } | ForEach-Object {
            & $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $_.FullName
            if ($LASTEXITCODE -ne 0) { throw 'Code signing failed.' }
            & $SignTool verify /pa $_.FullName
            if ($LASTEXITCODE -ne 0) { throw 'Signature verification failed.' }
        }
        $snapDropSigned = $true
    }
    $snapDropLabel = if ($snapDropSigned) { 'signed' } else { 'unsigned' }
    $snapDropZip = Join-Path $snapDropRelease "SnapDrop-0.1.0-win-x64-$snapDropLabel-$snapDropRun.zip"
    Compress-Archive -Path (Join-Path $snapDropPublish '*') -DestinationPath $snapDropZip
    if ($Installer) {
        $snapDropIscc = $env:SNAPDROP_ISCC
        if (-not $snapDropIscc) {
            $snapDropIsccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
            if ($snapDropIsccCommand) { $snapDropIscc = $snapDropIsccCommand.Source }
        }
        if (-not $snapDropIscc) { throw 'Install and pin the selected Inno Setup compiler, then set SNAPDROP_ISCC to ISCC.exe. The portable ZIP has been created.' }
        & $snapDropIscc "/DPublishDir=$snapDropPublish" "/DReleaseDir=$snapDropRelease" "/DArtifactLabel=$snapDropLabel-$snapDropRun" packaging/installer/SnapDrop.iss
        if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
        if ($snapDropSigned) {
            $snapDropSetup = Join-Path $snapDropRelease "SnapDrop-0.1.0-win-x64-setup-$snapDropLabel-$snapDropRun.exe"
            & $SignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $snapDropSetup
            if ($LASTEXITCODE -ne 0) { throw 'Installer signing failed.' }
            & $SignTool verify /pa $snapDropSetup
            if ($LASTEXITCODE -ne 0) { throw 'Installer signature verification failed.' }
        }
    }
    Get-ChildItem -LiteralPath $snapDropRelease -File | Where-Object { $_.Extension -in '.zip','.exe' } |
        ForEach-Object { $snapDropHash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256; "$($snapDropHash.Hash)  $($_.Name)" } |
        Set-Content -LiteralPath (Join-Path $snapDropRelease 'SHA256SUMS.txt') -Encoding ascii
    Write-Output "Created $snapDropLabel release candidate artifacts in $snapDropRelease. Interactive, performance, and clean-machine gates remain separate."
} finally { Pop-Location }
