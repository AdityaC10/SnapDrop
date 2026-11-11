using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapDrop.Core;

namespace SnapDrop.Windows;

public sealed class WpfImageCodec : IImageEncoder, IOriginalTranscoder
{
    public int JpegQuality { get; set; } = 90;
    public async Task EncodeAsync(CapturedFrame frame, Stream output, ExportFormat format, CancellationToken cancellationToken = default)
    {
        using var retention = frame.Retain();
        await Task.Run(() =>
        {
            frame.Validate();
            cancellationToken.ThrowIfCancellationRequested();
            Save(ToBitmap(frame), output, format, JpegQuality);
            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task TranscodeAsync(string source, Stream output, ExportFormat format, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Save(LoadBitmap(source), output, format, JpegQuality);
            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);

    public static BitmapSource LoadBitmap(string path, int thumbnailWidth = 0)
    {
        using var input = File.OpenRead(path);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = input;
        if (thumbnailWidth > 0) image.DecodePixelWidth = thumbnailWidth;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static CapturedFrame Snapshot(BitmapSource source, DateTimeOffset? receivedAt = null, long receivedTimestamp = 0)
    {
        ImageLimits.Validate(source.PixelWidth, source.PixelHeight);
        var normalized = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = checked(normalized.PixelWidth * 4);
        var pixels = new byte[checked(stride * normalized.PixelHeight)];
        normalized.CopyPixels(pixels, stride, 0);
        return new() { Width = source.PixelWidth, Height = source.PixelHeight, Stride = stride, Pixels = pixels,
            ReceivedAtUtc = receivedAt ?? DateTimeOffset.UtcNow, ReceivedTimestamp = receivedTimestamp == 0 ? System.Diagnostics.Stopwatch.GetTimestamp() : receivedTimestamp };
    }

    public static BitmapSource ToBitmap(CapturedFrame frame)
    {
        var image = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, frame.Pixels, frame.Stride);
        image.Freeze();
        return image;
    }

    public static BitmapSource Thumbnail(CapturedFrame frame, int width = 560)
    {
        var image = ToBitmap(frame);
        if (image.PixelWidth <= width) return image;
        var resized = new TransformedBitmap(image, new ScaleTransform((double)width / image.PixelWidth, (double)width / image.PixelWidth));
        using var small = Snapshot(resized);
        return ToBitmap(small);
    }

    private static void Save(BitmapSource source, Stream output, ExportFormat format, int jpegQuality)
    {
        BitmapEncoder encoder;
        if (format == ExportFormat.Jpeg)
        {
            // Flatten straight-alpha BGRA directly; no render target or Dispatcher is needed on the worker.
            using var frame = Snapshot(source);
            var opaque = new byte[checked(frame.Width * frame.Height * 3)];
            for (var pixel = 0; pixel < frame.Width * frame.Height; pixel++)
            {
                var alpha = frame.Pixels[pixel * 4 + 3];
                for (var channel = 0; channel < 3; channel++)
                    opaque[pixel * 3 + channel] = (byte)((frame.Pixels[pixel * 4 + channel] * alpha + 255 * (255 - alpha) + 127) / 255);
            }
            source = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null, opaque, frame.Width * 3);
            encoder = new JpegBitmapEncoder { QualityLevel = Math.Clamp(jpegQuality, 1, 100) };
        }
        else encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(output);
    }
}
