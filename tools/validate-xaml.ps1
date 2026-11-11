param([switch]$Snapshots)
# Markup-only QA using the installed Windows .NET Framework WPF runtime.
# This does NOT compile or execute the .NET 10 app or prove event-handler behavior.
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run powershell.exe -NoProfile -STA -File tools/validate-xaml.ps1 [-Snapshots].' }
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$snapDropRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$snapDropXamlNamespace = 'http://schemas.microsoft.com/winfx/2006/xaml'
$snapDropEvents = @('KeyDown','Click','Checked','Unchecked','MouseLeftButtonDown','MouseLeftButtonUp','MouseMove','SelectionChanged','SizeChanged','MouseDoubleClick')

function Read-SnapDropXaml([string]$Path) {
    [xml]$snapDropXml = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    foreach ($snapDropNode in $snapDropXml.SelectNodes('//*')) {
        foreach ($snapDropAttribute in @($snapDropNode.Attributes)) {
            if (($snapDropAttribute.NamespaceURI -eq $snapDropXamlNamespace -and $snapDropAttribute.LocalName -eq 'Class') -or $snapDropEvents -contains $snapDropAttribute.LocalName) {
                $snapDropNode.RemoveAttributeNode($snapDropAttribute) | Out-Null
            }
        }
    }
    return [Windows.Markup.XamlReader]::Parse($snapDropXml.OuterXml)
}

$snapDropApplication = Read-SnapDropXaml (Join-Path $snapDropRoot 'src\SnapDrop.App\App.xaml')
$snapDropBitmap = [Windows.Media.Imaging.BitmapImage]::new()
$snapDropBitmap.BeginInit()
$snapDropBitmap.UriSource = [Uri](Join-Path $snapDropRoot 'packaging\assets\synthetic-grid.png')
$snapDropBitmap.CacheOption = [Windows.Media.Imaging.BitmapCacheOption]::OnLoad
$snapDropBitmap.EndInit()
$snapDropBitmap.Freeze()
$snapDropFixture = [pscustomobject]@{
    Thumbnail=$snapDropBitmap
    Details='Synthetic capture - 320 x 180'
    Status='Drag the image, copy it, or choose Save As.'
    RecoveryPolicy='Synthetic markup fixture; application code is not running.'
    HasExport=$false
    HasHistoryExports=$false
}
$snapDropReview = Join-Path $snapDropRoot '.impeccable\review'
if ($Snapshots) { New-Item -ItemType Directory -Force -Path $snapDropReview | Out-Null }
$snapDropEvidence = @()
foreach ($snapDropName in @('FloatingPreviewWindow','ScreenshotViewerWindow','SettingsWindow','RecentCapturesWindow')) {
    $snapDropSource = Join-Path $snapDropRoot "src\SnapDrop.App\$snapDropName.xaml"
    $snapDropWindow = Read-SnapDropXaml $snapDropSource
    $snapDropWindow.DataContext = $snapDropFixture
    if ($snapDropName -eq 'RecentCapturesWindow') {
        $snapDropWindow.FindName('CaptureList').ItemsSource = @($snapDropFixture)
        $snapDropWindow.FindName('PolicyText').Text = 'Synthetic fixture: recovery copies expire after 7 days unless kept.'
        $snapDropWindow.FindName('StatusText').Text = 'Markup only. Event handlers are not running.'
    }
    if ($snapDropName -eq 'SettingsWindow') {
        $snapDropWindow.FindName('PreviewSeconds').Text = '5'
        $snapDropWindow.FindName('RetentionDays').Text = '7'
        $snapDropWindow.FindName('Budget').Text = '1024'
        $snapDropWindow.FindName('DefaultFolder').Text = 'C:\Screenshots'
        $snapDropWindow.FindName('StatusText').Text = 'Synthetic markup fixture. Event handlers are not running.'
    }
    $snapDropContent = $snapDropWindow.Content
    $snapDropWidth = $snapDropWindow.Width
    $snapDropHeight = if ([double]::IsNaN($snapDropWindow.Height)) { [double]::PositiveInfinity } else { $snapDropWindow.Height }
    $snapDropContent.Measure([Windows.Size]::new($snapDropWidth,$snapDropHeight))
    if ([double]::IsInfinity($snapDropHeight)) { $snapDropHeight = [Math]::Ceiling($snapDropContent.DesiredSize.Height) }
    $snapDropContent.Arrange([Windows.Rect]::new(0,0,$snapDropWidth,$snapDropHeight))
    $snapDropContent.UpdateLayout()
    if ($Snapshots) {
        $snapDropRender = [Windows.Media.Imaging.RenderTargetBitmap]::new([int]$snapDropWidth,[int]$snapDropHeight,96,96,[Windows.Media.PixelFormats]::Pbgra32)
        $snapDropBackdrop = [Windows.Media.DrawingVisual]::new()
        $snapDropDrawing = $snapDropBackdrop.RenderOpen()
        $snapDropDrawing.DrawRectangle([Windows.SystemColors]::WindowBrush,$null,[Windows.Rect]::new(0,0,$snapDropWidth,$snapDropHeight))
        $snapDropDrawing.Close()
        $snapDropRender.Render($snapDropBackdrop)
        $snapDropRender.Render($snapDropContent)
        $snapDropEncoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
        $snapDropEncoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($snapDropRender))
        $snapDropOutput = Join-Path $snapDropReview "$snapDropName-xaml-fixture.png"
        $snapDropStream = [IO.File]::Create($snapDropOutput)
        try { $snapDropEncoder.Save($snapDropStream) } finally { $snapDropStream.Dispose() }
    }
    $snapDropEvidence += [pscustomobject]@{ Source=$snapDropSource; Sha256=(Get-FileHash -LiteralPath $snapDropSource -Algorithm SHA256).Hash; Width=$snapDropWidth; Height=$snapDropHeight; MarkupLoaded=$true; EventHandlersExecuted=$false }
    "$snapDropName loaded using Windows .NET Framework WPF. Event handlers were removed for this markup-only check."
}
if ($Snapshots) {
    [pscustomobject]@{ Kind='Markup-only fixtures, not .NET 10 application execution'; Framework=[Environment]::Version.ToString(); CreatedAt=[DateTimeOffset]::UtcNow.ToString('O'); Views=$snapDropEvidence } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $snapDropReview 'fixture-evidence.json') -Encoding UTF8
}
