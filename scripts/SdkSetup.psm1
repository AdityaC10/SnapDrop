Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

function Get-SnapDropSdkPackage {
    param([Parameter(Mandatory = $true)][psobject]$Metadata)
    $version = [string]$Metadata.'latest-sdk'
    if ($version -notmatch '^10\.0\.\d+$') { throw 'The release metadata did not identify a stable .NET 10 SDK.' }
    foreach ($release in @($Metadata.releases)) {
        $sdks = @()
        if ($release.PSObject.Properties['sdk']) { $sdks += @($release.sdk) }
        if ($release.PSObject.Properties['sdks']) { $sdks += @($release.sdks) }
        foreach ($sdk in $sdks) {
            if ($null -eq $sdk -or [string]$sdk.version -ne $version) { continue }
            foreach ($file in @($sdk.files)) {
                if ($file.rid -ne 'win-x64' -or $file.name -notlike '*.zip') { continue }
                $uri = [Uri]$file.url
                $hosts = @('builds.dotnet.microsoft.com', 'download.visualstudio.microsoft.com', 'dotnetcli.blob.core.windows.net', 'dotnetcli.azureedge.net')
                if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https' -or -not $uri.IsDefaultPort -or
                    $uri.UserInfo -or $hosts -notcontains $uri.DnsSafeHost -or $uri.AbsolutePath -notlike '*.zip') {
                    throw 'The SDK package URL is not an approved Microsoft HTTPS download URL.'
                }
                if ([string]$file.hash -notmatch '^[a-fA-F0-9]{128}$') { throw 'The SDK SHA-512 checksum is missing or malformed.' }
                return [pscustomobject]@{ Version = $version; Url = $uri.AbsoluteUri; Sha512 = ([string]$file.hash).ToLowerInvariant() }
            }
        }
    }
    throw 'No Windows x64 ZIP matching the latest stable .NET 10 SDK was found.'
}

function Assert-SnapDropSdkArchive {
    param([Parameter(Mandatory = $true)][string]$ArchivePath,
          [Parameter(Mandatory = $true)][string]$Sha512)
    if ($Sha512 -notmatch '^[a-fA-F0-9]{128}$') { throw 'Supply the 128-character SHA-512 checksum from official Microsoft release metadata.' }
    if (-not [IO.File]::Exists($ArchivePath)) { throw 'The SDK archive is unavailable.' }
    $actual = (Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA512).Hash
    if (-not $actual.Equals($Sha512, [StringComparison]::OrdinalIgnoreCase)) { throw 'SDK checksum mismatch. Nothing was installed.' }
}

function Assert-SnapDropSetupPath {
    param([string]$Path, [string]$BoundaryRoot)
    $root = [IO.Path]::GetFullPath($BoundaryRoot).TrimEnd([char[]]'\/')
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The SDK installation path must remain below its workspace tools directory.'
    }
    $directory = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($full))
    while ($null -ne $directory) {
        if ($directory.Exists -and ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'SDK setup does not follow linked directories.'
        }
        $directory = $directory.Parent
    }
    return $full
}

function Expand-SnapDropSdkArchive {
    param([Parameter(Mandatory = $true)][string]$ArchivePath,
          [Parameter(Mandatory = $true)][string]$Sha512,
          [Parameter(Mandatory = $true)][string]$Destination,
          [Parameter(Mandatory = $true)][string]$BoundaryRoot)
    $destinationPath = Assert-SnapDropSetupPath $Destination $BoundaryRoot
    if (Test-Path -LiteralPath $destinationPath) { throw 'The SDK destination already exists. It was not changed.' }
    Assert-SnapDropSdkArchive $ArchivePath $Sha512
    Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($ArchivePath))
    try {
        $targets = [Collections.Generic.List[object]]::new()
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        [long]$total = 0
        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace('/', '\')
            if ([string]::IsNullOrWhiteSpace($name) -or [IO.Path]::IsPathRooted($name) -or $name.Contains(':')) {
                throw 'The SDK archive contains an invalid rooted or alternate-stream path.'
            }
            foreach ($part in $name.Split([char]'\')) {
                if ($part.EndsWith('.') -or $part.EndsWith(' ')) {
                    throw 'The SDK archive contains an ambiguous Windows path component.'
                }
                if (($part -split '\.')[0] -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$') {
                    throw 'The SDK archive contains a reserved Windows device name.'
                }
            }
            if ((($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000 -or ($entry.ExternalAttributes -band 0x400)) {
                throw 'The SDK archive contains an unsupported linked entry.'
            }
            $target = Assert-SnapDropSetupPath ([IO.Path]::Combine($destinationPath, $name)) $destinationPath
            if (-not $names.Add($target)) { throw 'The SDK archive contains duplicate destination paths.' }
            $total += $entry.Length
            if ($total -gt 4GB -or $targets.Count -ge 100000) { throw 'The SDK archive exceeds the extraction limits.' }
            $targets.Add([pscustomobject]@{ Entry = $entry; Path = $target; Directory = $name.EndsWith('\') })
        }
        if (-not $names.Contains([IO.Path]::Combine($destinationPath, 'dotnet.exe'))) { throw 'The SDK archive does not contain a root dotnet.exe.' }
        # Validate every entry before creating the staging directory or writing any payload.
        [IO.Directory]::CreateDirectory($destinationPath) | Out-Null
        foreach ($target in $targets) {
            if ($target.Directory) { [IO.Directory]::CreateDirectory($target.Path) | Out-Null; continue }
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target.Path)) | Out-Null
            $entryStream = $target.Entry.Open()
            try {
                $payloadStream = [IO.FileStream]::new($target.Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                try { $entryStream.CopyTo($payloadStream); $payloadStream.Flush($true) } finally { $payloadStream.Dispose() }
            } finally { $entryStream.Dispose() }
        }
    } finally { $archive.Dispose() }
    return $destinationPath
}

Export-ModuleMember -Function Get-SnapDropSdkPackage,Assert-SnapDropSdkArchive,Expand-SnapDropSdkArchive
