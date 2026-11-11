using System.Windows.Media.Imaging;
using System.IO;
using SnapDrop.Core;
using SnapDrop.Windows;
using Xunit;

namespace SnapDrop.Windows.Tests;

public sealed class ImageTests
{
    [Fact]
    public async Task PngRoundTripPreservesOpaquePixelsAndNativeDimensions()
    {
        var frame = new CapturedFrame { Width = 2, Height = 2, Stride = 8,
            Pixels = [0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 255] };
        using var output = new MemoryStream();
        await new WpfImageCodec().EncodeAsync(frame, output, ExportFormat.Png, cancellationToken: TestContext.Current.CancellationToken);
        output.Position = 0;
        var bitmap = BitmapDecoder.Create(output, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var decoded = WpfImageCodec.Snapshot(bitmap);
        Assert.Equal(frame.Width, decoded.Width); Assert.Equal(frame.Height, decoded.Height); Assert.Equal(frame.Pixels, decoded.Pixels);
    }

    [Fact]
    public async Task JpegIsActuallyJpegAndTransparentPixelsBecomeWhite()
    {
        var frame = new CapturedFrame { Width = 1, Height = 1, Stride = 4, Pixels = [0, 0, 0, 0] };
        using var output = new MemoryStream(); await new WpfImageCodec().EncodeAsync(frame, output, ExportFormat.Jpeg, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0xFF, output.GetBuffer()[0]); Assert.Equal(0xD8, output.GetBuffer()[1]);
        output.Position = 0;
        var decoded = WpfImageCodec.Snapshot(BitmapDecoder.Create(output, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0]);
        Assert.All(decoded.Pixels.Take(3), channel => Assert.True(channel >= 245));
    }

    public static bool DesktopEnabled => Environment.GetEnvironmentVariable("SNAPDROP_INTERACTIVE_TESTS") == "1";

    [Fact(SkipUnless = nameof(DesktopEnabled))]
    [Trait("Category", "InteractiveWindows")]
    public async Task OwnClipboardCopyDoesNotCreateAnotherCapture()
    {
        using var clipboard = new ClipboardService(); await clipboard.Ready;
        var captures = 0;
        clipboard.FrameReceived += (frame, _) => { Interlocked.Increment(ref captures); frame.Dispose(); clipboard.ReleaseFrame(); };
        await clipboard.SetEnabledAsync(true);
        await clipboard.CopyAsync(new CapturedFrame { Width = 1, Height = 1, Stride = 4, Pixels = [0, 0, 255, 255] });
        await Task.Delay(400, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, captures);
    }
}
