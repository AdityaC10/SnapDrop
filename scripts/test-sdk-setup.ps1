$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SdkSetup.psm1') -Force
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$snapDropRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$snapDropTestRoot = Join-Path $snapDropRoot ('artifacts\sdk-setup-tests\' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($snapDropTestRoot) | Out-Null
$snapDropPassed = 0

function Assert-SetupTest([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function Assert-SetupThrows([scriptblock]$Action) {
    $threw = $false
    try { & $Action | Out-Null } catch { $threw = $true }
    Assert-SetupTest $threw 'Expected this operation to reject its input.'
}
function Invoke-SetupCase([string]$Name, [scriptblock]$Action) {
    try { & $Action }
    catch {
        [pscustomobject]@{
            Suite = 'SDK bootstrap guard tests'; Passed = $script:snapDropPassed; Failed = 1; FailedCase = $Name
            ActualSdkInstalled = $false; ApplicationBuildOrTestsExecuted = $false
            FixtureDirectory = $snapDropTestRoot; CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $snapDropRoot 'artifacts\sdk-setup-tests.json') -Encoding UTF8
        throw
    }
    $script:snapDropPassed++
    Write-Output "PASS: $Name"
}
function New-TestArchive([string]$Name, [hashtable]$Files, [switch]$Symlink) {
    $path = Join-Path $snapDropTestRoot ($Name + '.zip')
    $zip = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $Files.Keys) {
            $entry = $zip.CreateEntry($name)
            if ($Symlink -and $name -eq 'link') { $entry.ExternalAttributes = -1610612736 }
            $writer = [IO.StreamWriter]::new($entry.Open())
            try { $writer.Write([string]$Files[$name]) } finally { $writer.Dispose() }
        }
    } finally { $zip.Dispose() }
    return [pscustomobject]@{ Path = $path; Hash = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash }
}

$snapDropGood = New-TestArchive 'valid' @{ 'dotnet.exe' = 'non-executable test fixture'; 'sdk/10.0.404/marker.txt' = 'verified nested content' }
$snapDropMetadata = [pscustomobject]@{
    'latest-sdk' = '10.0.404'
    releases = @([pscustomobject]@{ sdk = [pscustomobject]@{
        version = '10.0.404'; files = @([pscustomobject]@{ rid = 'win-x64'; name = 'dotnet-sdk-win-x64.zip'; url = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.404/sdk.zip'; hash = $snapDropGood.Hash })
    } })
}

Invoke-SetupCase 'Select stable SDK metadata without requiring an sdks array' {
    $package = Get-SnapDropSdkPackage $snapDropMetadata
    Assert-SetupTest ($package.Version -eq '10.0.404') 'Wrong SDK selected.'
    Assert-SetupTest ($package.Sha512 -eq $snapDropGood.Hash.ToLowerInvariant()) 'Checksum not preserved.'
}
Invoke-SetupCase 'Reject preview SDK metadata' {
    $bad = $snapDropMetadata | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $bad.'latest-sdk' = '10.0.404-preview.1'
    Assert-SetupThrows { Get-SnapDropSdkPackage $bad }
}
Invoke-SetupCase 'Reject non-Microsoft download host' {
    $bad = $snapDropMetadata | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $bad.releases[0].sdk.files[0].url = 'https://untrusted.invalid/sdk.zip'
    Assert-SetupThrows { Get-SnapDropSdkPackage $bad }
}
Invoke-SetupCase 'Reject non-HTTPS download' {
    $bad = $snapDropMetadata | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $bad.releases[0].sdk.files[0].url = 'http://builds.dotnet.microsoft.com/sdk.zip'
    Assert-SetupThrows { Get-SnapDropSdkPackage $bad }
}
Invoke-SetupCase 'Reject malformed release checksum' {
    $bad = $snapDropMetadata | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $bad.releases[0].sdk.files[0].hash = 'not-a-checksum'
    Assert-SetupThrows { Get-SnapDropSdkPackage $bad }
}
Invoke-SetupCase 'Checksum mismatch creates no staging files' {
    $destination = Join-Path $snapDropTestRoot 'mismatch'
    Assert-SetupThrows { Expand-SnapDropSdkArchive $snapDropGood.Path ('0' * 128) $destination $snapDropTestRoot }
    Assert-SetupTest (-not [IO.Directory]::Exists($destination)) 'Unverified bytes were extracted.'
}
Invoke-SetupCase 'Verified archive extracts exact nested content' {
    $destination = Join-Path $snapDropTestRoot 'valid-extraction'
    Expand-SnapDropSdkArchive $snapDropGood.Path $snapDropGood.Hash $destination $snapDropTestRoot | Out-Null
    Assert-SetupTest ([IO.File]::ReadAllText((Join-Path $destination 'sdk\10.0.404\marker.txt')) -eq 'verified nested content') 'Nested data differed.'
}
Invoke-SetupCase 'Existing destination is never overwritten or merged' {
    $destination = Join-Path $snapDropTestRoot 'existing'
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    [IO.File]::WriteAllText((Join-Path $destination 'keep.txt'), 'keep')
    Assert-SetupThrows { Expand-SnapDropSdkArchive $snapDropGood.Path $snapDropGood.Hash $destination $snapDropTestRoot }
    Assert-SetupTest ([IO.File]::ReadAllText((Join-Path $destination 'keep.txt')) -eq 'keep') 'Existing data changed.'
    Assert-SetupTest (-not [IO.File]::Exists((Join-Path $destination 'dotnet.exe'))) 'An installation was merged.'
}
Invoke-SetupCase 'Destination outside the intended tools boundary is rejected' {
    $outside = Join-Path $snapDropTestRoot '..\outside-install'
    Assert-SetupThrows { Expand-SnapDropSdkArchive $snapDropGood.Path $snapDropGood.Hash $outside $snapDropTestRoot }
}
Invoke-SetupCase 'Parent traversal entry is rejected before any extraction' {
    $zip = New-TestArchive 'traversal' @{ 'dotnet.exe' = 'fixture'; '../escaped.txt' = 'must not escape' }
    $destination = Join-Path $snapDropTestRoot 'traversal-extraction'
    Assert-SetupThrows { Expand-SnapDropSdkArchive $zip.Path $zip.Hash $destination $snapDropTestRoot }
    Assert-SetupTest (-not [IO.Directory]::Exists($destination)) 'Traversal rejection wrote partial payloads.'
    Assert-SetupTest (-not [IO.File]::Exists((Join-Path $snapDropTestRoot 'escaped.txt'))) 'Archive escaped staging.'
}
Invoke-SetupCase 'Rooted and alternate-stream entries are rejected' {
    foreach ($name in @('/rooted.txt', 'name:stream')) {
        $zip = New-TestArchive ([Guid]::NewGuid().ToString('N')) @{ 'dotnet.exe' = 'fixture'; $name = 'unsafe' }
        $destination = Join-Path $snapDropTestRoot ([Guid]::NewGuid().ToString('N'))
        Assert-SetupThrows { Expand-SnapDropSdkArchive $zip.Path $zip.Hash $destination $snapDropTestRoot }
        Assert-SetupTest (-not [IO.Directory]::Exists($destination)) 'Rejected archive created a destination.'
    }
}
Invoke-SetupCase 'Linked archive entry is rejected' {
    $zip = New-TestArchive 'symlink' @{ 'dotnet.exe' = 'fixture'; 'link' = '../target' } -Symlink
    Assert-SetupThrows { Expand-SnapDropSdkArchive $zip.Path $zip.Hash (Join-Path $snapDropTestRoot 'symlink-extraction') $snapDropTestRoot }
}
Invoke-SetupCase 'Archive without root dotnet.exe is rejected' {
    $zip = New-TestArchive 'not-sdk' @{ 'other.txt' = 'fixture' }
    Assert-SetupThrows { Expand-SnapDropSdkArchive $zip.Path $zip.Hash (Join-Path $snapDropTestRoot 'not-sdk-extraction') $snapDropTestRoot }
}

Invoke-SetupCase 'Both Windows and ZIP directory separators are handled' {
    $zip = New-TestArchive 'directories' @{ 'dotnet.exe' = 'fixture'; 'first/' = ''; 'second\' = ''; 'first/marker.txt' = 'one'; 'second\marker.txt' = 'two' }
    $destination = Join-Path $snapDropTestRoot 'directories-extraction'
    Expand-SnapDropSdkArchive $zip.Path $zip.Hash $destination $snapDropTestRoot | Out-Null
    Assert-SetupTest ([IO.File]::ReadAllText((Join-Path $destination 'first\marker.txt')) -eq 'one') 'ZIP directory separator failed.'
    Assert-SetupTest ([IO.File]::ReadAllText((Join-Path $destination 'second\marker.txt')) -eq 'two') 'Windows directory separator failed.'
}
Invoke-SetupCase 'Ambiguous Windows paths and device names are rejected' {
    foreach ($name in @('nested/.. /escape.txt', 'nested./marker.txt', 'NUL.txt', 'COM1')) {
        $zip = New-TestArchive ([Guid]::NewGuid().ToString('N')) @{ 'dotnet.exe' = 'fixture'; $name = 'unsafe' }
        $destination = Join-Path $snapDropTestRoot ([Guid]::NewGuid().ToString('N'))
        Assert-SetupThrows { Expand-SnapDropSdkArchive $zip.Path $zip.Hash $destination $snapDropTestRoot }
        Assert-SetupTest (-not [IO.Directory]::Exists($destination)) 'Ambiguous archive was partially extracted.'
    }
}
Invoke-SetupCase 'Case-insensitive duplicate archive paths are rejected' {
    $path = Join-Path $snapDropTestRoot 'duplicate.zip'
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try { $archive.CreateEntry('dotnet.exe') | Out-Null; $archive.CreateEntry('DOTNET.EXE') | Out-Null } finally { $archive.Dispose() }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA512).Hash
    $destination = Join-Path $snapDropTestRoot 'duplicate-extraction'
    Assert-SetupThrows { Expand-SnapDropSdkArchive $path $hash $destination $snapDropTestRoot }
    Assert-SetupTest (-not [IO.Directory]::Exists($destination)) 'Duplicate archive was partially extracted.'
}

$snapDropResult = [pscustomobject]@{
    Suite = 'SDK bootstrap guard tests'; Passed = $snapDropPassed; Failed = 0
    ActualSdkInstalled = $false; ApplicationBuildOrTestsExecuted = $false
    FixtureDirectory = $snapDropTestRoot; CompletedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
}
$snapDropResult | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $snapDropRoot 'artifacts\sdk-setup-tests.json') -Encoding UTF8
Write-Output "$snapDropPassed SDK setup guard tests passed. Fixtures were not executed as an SDK; application compilation/tests remain separate."
