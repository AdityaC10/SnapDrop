using System.Windows;
using System.Windows.Input;
using SnapDrop.Core;

namespace SnapDrop.Windows;

public sealed class ScreenshotDragService(IScreenshotRepository repository)
{
    public async Task<DragDropEffects> StartAsync(UIElement source, ScreenshotRecord item)
    {
        if (!source.Dispatcher.CheckAccess()) throw new InvalidOperationException("File drag must start on the UI STA.");
        using var lease = repository.AcquireLease(item.Id);
        await repository.UpdateAsync(item.Id, current => current with { LastTransferAtUtc = DateTimeOffset.UtcNow });
        if (Mouse.LeftButton != MouseButtonState.Pressed || !source.IsVisible) return DragDropEffects.None;
        var data = new DataObject(DataFormats.FileDrop, new[] { repository.GetOriginalPath(item) });
        try { return DragDrop.DoDragDrop(source, data, DragDropEffects.Copy); }
        finally { await repository.UpdateAsync(item.Id, current => current with { LastTransferAtUtc = DateTimeOffset.UtcNow }); }
    }
}
