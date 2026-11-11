using SnapDrop.Core;
using Xunit;

namespace SnapDrop.Core.Tests;

public sealed class PolicyTests
{
    [Theory]
    [InlineData(0, 10)] [InlineData(-1, 1)] [InlineData(1, 0)] [InlineData(int.MaxValue, int.MaxValue)] [InlineData(8000, 8000)]
    public void RejectsInvalidOrExcessiveImages(int width, int height) => Assert.Throws<InvalidDataException>(() => ImageLimits.Validate(width, height));

    [Fact] public void Accepts4KNativeDimensions() => ImageLimits.Validate(3840, 2160);

    [Fact]
    public void RecoveryLeaseKeepsPixelsAfterPipelineReleasesItsOwner()
    {
        var frame = new CapturedFrame { Width = 1, Height = 1, Stride = 4, Pixels = [1, 2, 3, 255] };
        var recoveryLease = frame.Retain();
        frame.Dispose();
        frame.Validate(); Assert.Equal(4, frame.Pixels.Length);
        recoveryLease.Dispose(); Assert.Empty(frame.Pixels);
        Assert.Throws<ObjectDisposedException>(() => frame.Retain());
    }

    [Fact]
    public void BufferOwnersAndLeasesAreIdempotent()
    {
        var frame = new CapturedFrame { Width = 1, Height = 1, Stride = 4, Pixels = [1, 2, 3, 255] };
        var lease = frame.Retain(); lease.Dispose(); lease.Dispose();
        Assert.Equal(4, frame.Pixels.Length);
        frame.Dispose(); frame.Dispose(); Assert.Empty(frame.Pixels);
    }

    [Fact]
    public void RejectsMismatchedPixelBuffer()
    {
        var frame = new CapturedFrame { Width = 2, Height = 1, Stride = 8, Pixels = [0, 0, 0, 255] };
        Assert.Throws<InvalidDataException>(frame.Validate);
    }

    [Fact]
    public void NamesDoNotCollideForEqualTimestamps()
    {
        var time = DateTimeOffset.UtcNow;
        Assert.NotEqual(ScreenshotNaming.Create(time, Guid.NewGuid()), ScreenshotNaming.Create(time, Guid.NewGuid()));
    }

    [Theory] [InlineData("image.png", ExportFormat.Png)] [InlineData("image.JPEG", ExportFormat.Jpeg)] [InlineData("image.jpg", ExportFormat.Jpeg)]
    public void ChoosesEncodingFromSupportedExtension(string path, ExportFormat expected) => Assert.Equal(expected, ScreenshotNaming.FromPath(path));

    [Fact] public void DoesNotDisguiseUnsupportedEncoding() => Assert.Throws<InvalidDataException>(() => ScreenshotNaming.FromPath("image.gif"));

    [Fact]
    public void RetentionNeverDeletesKeptLeasedOrRecentItems()
    {
        var now = DateTimeOffset.UtcNow;
        var item = Item(now.AddDays(-10));
        Assert.True(RetentionPolicy.CanDelete(item, now, 7, false));
        Assert.False(RetentionPolicy.CanDelete(item with { IsKept = true }, now, 7, false));
        Assert.False(RetentionPolicy.CanDelete(item, now, 7, true));
        Assert.False(RetentionPolicy.CanDelete(item with { ReceivedAtUtc = now.AddDays(-6) }, now, 7, false));
    }

    [Fact]
    public void TransfersProtectExpiredFilesFor24Hours()
    {
        var now = DateTimeOffset.UtcNow;
        var item = Item(now.AddDays(-10)) with { LastTransferAtUtc = now.AddHours(-23) };
        Assert.False(RetentionPolicy.CanDelete(item, now, 7, false));
        Assert.True(RetentionPolicy.CanDelete(item, now.AddHours(1), 7, false));
    }

    [Theory] [InlineData(1)] [InlineData(31)] [InlineData(-1)]
    public void RejectsInvalidPreviewDuration(int duration) => Assert.Throws<InvalidDataException>(() => (new AppSettings { PreviewSeconds = duration }).Validate());

    [Fact]
    public void DefaultSettingsRequireExplicitMonitoringConsent()
    {
        var settings = new AppSettings().Validate();
        Assert.False(settings.MonitoringEnabled);
        Assert.False(settings.StartWithWindows);
        Assert.False(settings.AutomaticExport);
        Assert.False(settings.AutoOpenSaveAs);
        Assert.Equal(7, settings.RetentionDays);
    }

    [Fact]
    public void AutomaticExportNeedsDestination() => Assert.Throws<InvalidDataException>(() => (new AppSettings { AutomaticExport = true }).Validate());

    [Fact]
    public void RelativeConfiguredDestinationIsRejected() => Assert.Throws<InvalidDataException>(() => (new AppSettings { DefaultFolder = "relative-folder" }).Validate());

    [Fact]
    public void IndependentPauseReasonsDoNotResumeEachOther()
    {
        var clock = new TestClock();
        var timer = new PreviewLifetime(clock);
        timer.Reset(5);
        clock.Advance(TimeSpan.FromSeconds(3));
        timer.Pause("hover"); timer.Pause("drag");
        clock.Advance(TimeSpan.FromSeconds(20));
        timer.Resume("hover");
        Assert.True(timer.IsPaused);
        Assert.False(timer.Expired);
        timer.Resume("drag");
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.False(timer.Expired);
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.True(timer.Expired);
    }

    [Fact]
    public void NeverTimeoutStaysVisible()
    {
        var clock = new TestClock(); var timer = new PreviewLifetime(clock);
        timer.Reset(0); clock.Advance(TimeSpan.FromDays(3)); Assert.False(timer.Expired);
    }

    [Fact]
    public void FeedbackProvidesAtLeastTwoSeconds()
    {
        var clock = new TestClock(); var timer = new PreviewLifetime(clock);
        timer.Reset(5); clock.Advance(TimeSpan.FromSeconds(4.8)); timer.Pause("save");
        timer.Resume("save", true); clock.Advance(TimeSpan.FromSeconds(1)); Assert.False(timer.Expired);
        clock.Advance(TimeSpan.FromSeconds(1)); Assert.True(timer.Expired);
    }

    private static ScreenshotRecord Item(DateTimeOffset received)
    {
        var id = Guid.NewGuid();
        return new() { Id = id, ReceivedAtUtc = received, PixelWidth = 1, PixelHeight = 1, ManagedFileName = ScreenshotNaming.Create(received, id), OriginalFileBytes = 10 };
    }
}

public sealed class TestClock : TimeProvider
{
    private long _timestamp;
    private readonly DateTimeOffset _origin = DateTimeOffset.Parse("2026-11-11T00:00:00Z");
    public override long TimestampFrequency => 1000;
    public override long GetTimestamp() => _timestamp;
    public override DateTimeOffset GetUtcNow() => _origin.AddMilliseconds(_timestamp);
    public void Advance(TimeSpan time) => _timestamp += (long)time.TotalMilliseconds;
}
