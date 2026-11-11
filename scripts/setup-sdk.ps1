[CmdletBinding(DefaultParameterSetName = 'Online')]
param(
    [Parameter(ParameterSetName = 'Offline', Mandatory = $true)][string]$ArchivePath,
    [Parameter(ParameterSetName = 'Offline', Mandatory = $true)][string]$Sha512,
    [Parameter(ParameterSetName = 'Offline', Mandatory = $true)][ValidatePattern('^10\.0\.\d+$')][string]$SdkVersion
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'SdkSetup.psm1') -Force
$snapDropRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$snapDropTools = Join-Path $snapDropRoot '.tools'
$snapDropInstall = Join-Path $snapDropTools 'dotnet'
$snapDropExecutable = Join-Path $snapDropInstall 'dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $snapDropTools 'dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'

if (Test-Path -LiteralPath $snapDropExecutable) {
    Push-Location $snapDropRoot
    try {
        $snapDropInstalled = & $snapDropExecutable --version
        if ($LASTEXITCODE -eq 0 -and $snapDropInstalled -match '^10\.0\.\d+$') {
            Write-Output "Using existing workspace SDK $snapDropInstalled. This setup command does not replace an existing installation."
            exit 0
        }
    } finally { Pop-Location }
    throw 'An incomplete or incompatible workspace installation already exists. Inspect .tools/dotnet before choosing how to repair it; setup does not overwrite it.'
}
if (Test-Path -LiteralPath $snapDropInstall) { throw 'The workspace SDK destination already exists. It was not changed.' }
[IO.Directory]::CreateDirectory($snapDropTools) | Out-Null

if ($PSCmdlet.ParameterSetName -eq 'Offline') {
    $snapDropPackage = [pscustomobject]@{ Version = $SdkVersion; Sha512 = $Sha512 }
    $snapDropArchive = [IO.Path]::GetFullPath($ArchivePath)
} else {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Write-Output 'Reading official Microsoft .NET 10 release metadata...'
    try {
        $snapDropResponse = Invoke-WebRequest -UseBasicParsing -Uri 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json' -TimeoutSec 30
        $snapDropMetadata = $snapDropResponse.Content | ConvertFrom-Json
    } catch { throw 'Microsoft release metadata could not be downloaded. Run this command in a terminal with network access, or use the documented offline archive/checksum parameters. Nothing was installed.' }
    $snapDropPackage = Get-SnapDropSdkPackage $snapDropMetadata
    $snapDropDownloads = Join-Path $snapDropTools 'downloads'
    [IO.Directory]::CreateDirectory($snapDropDownloads) | Out-Null
    $snapDropArchive = Join-Path $snapDropDownloads "dotnet-sdk-$($snapDropPackage.Version)-win-x64.zip"
    $snapDropReusable = $false
    if ([IO.File]::Exists($snapDropArchive)) {
        try { Assert-SnapDropSdkArchive $snapDropArchive $snapDropPackage.Sha512; $snapDropReusable = $true } catch { Write-Output 'Cached archive failed verification; downloading a new copy.' }
    }
    if (-not $snapDropReusable) {
        $snapDropPartial = $snapDropArchive + '.partial'
        Write-Output "Downloading SDK $($snapDropPackage.Version) into the workspace..."
        Invoke-WebRequest -UseBasicParsing -Uri $snapDropPackage.Url -OutFile $snapDropPartial -TimeoutSec 600
        Assert-SnapDropSdkArchive $snapDropPartial $snapDropPackage.Sha512
        # Cache replacement happens only after verification; it never touches an installed SDK.
        [IO.File]::Copy($snapDropPartial, $snapDropArchive, $true)
        [IO.File]::Delete($snapDropPartial)
    }
}

$snapDropStage = Join-Path $snapDropTools ('dotnet-setup-' + [Guid]::NewGuid().ToString('N'))
Expand-SnapDropSdkArchive -ArchivePath $snapDropArchive -Sha512 $snapDropPackage.Sha512 -Destination $snapDropStage -BoundaryRoot $snapDropTools | Out-Null
Push-Location $snapDropRoot
try {
    $snapDropVersion = & (Join-Path $snapDropStage 'dotnet.exe') --version
    if ($LASTEXITCODE -ne 0 -or $snapDropVersion -ne $snapDropPackage.Version) {
        throw "The extracted SDK did not report the expected version. Staging files were retained at $snapDropStage."
    }
} finally { Pop-Location }
# Both paths are checked absolute children of the intended tools directory. Directory.Move
# fails if the destination appeared concurrently; it never merges or replaces an install.
$snapDropBoundary = [IO.Path]::GetFullPath($snapDropTools).TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
foreach ($snapDropPath in @($snapDropStage, $snapDropInstall)) {
    if (-not ([IO.Path]::GetFullPath($snapDropPath)).StartsWith($snapDropBoundary, [StringComparison]::OrdinalIgnoreCase)) { throw 'SDK install path escaped the workspace tools directory.' }
}
[IO.Directory]::Move($snapDropStage, $snapDropInstall)
Write-Output "Installed workspace SDK $($snapDropPackage.Version). No system installation or PATH change was made."
Write-Output 'Next: powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1'
