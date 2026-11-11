$ErrorActionPreference = 'Stop'
$snapDropRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$snapDropLocalDotnet = Join-Path $snapDropRoot '.tools\dotnet\dotnet.exe'
$snapDropDotnet = if ($env:SNAPDROP_DOTNET) { $env:SNAPDROP_DOTNET } elseif (Test-Path -LiteralPath $snapDropLocalDotnet) { $snapDropLocalDotnet } else { 'dotnet' }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
if (-not $env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $snapDropRoot '.tools\dotnet-home' }
if (-not $env:NUGET_PACKAGES) { $env:NUGET_PACKAGES = Join-Path $snapDropRoot '.tools\nuget-packages' }
if (Test-Path -LiteralPath $snapDropDotnet -PathType Leaf) {
    $env:DOTNET_ROOT = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($snapDropDotnet))
    $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
}

function Invoke-SnapDropDotnet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & $snapDropDotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet exited with code $LASTEXITCODE. Command: $($Arguments -join ' ')" }
}

function Test-SnapDropSdk {
    try { $snapDropSdkVersion = & $snapDropDotnet --version 2>$null }
    catch { throw 'Install a stable .NET 10 x64 SDK from https://dotnet.microsoft.com/download/dotnet/10.0. The currently available dotnet host could not resolve the required SDK.' }
    if ($LASTEXITCODE -ne 0 -or $snapDropSdkVersion -notmatch '^10\.0\.\d+$') {
        throw 'Install a stable .NET 10 x64 SDK from https://dotnet.microsoft.com/download/dotnet/10.0, or set SNAPDROP_DOTNET to a permitted local dotnet.exe. A runtime alone cannot build SnapDrop.'
    }
}
