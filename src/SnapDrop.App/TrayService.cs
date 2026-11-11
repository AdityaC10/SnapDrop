using System.Drawing;
using Forms = System.Windows.Forms;

namespace SnapDrop.App;

public sealed class TrayService : IDisposable
{
    private readonly App _app;
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _ownedIcon;
    private readonly Forms.ToolStripMenuItem _pause;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Forms.ToolStripMenuItem _recentMenu;
    private readonly Forms.ToolStripMenuItem _startup;
    private readonly Forms.ToolStripMenuItem _recover;
    private readonly Forms.ToolStripMenuItem _discard;

    public TrayService(App app)
    {
        _app = app;
        _ownedIcon = new Icon(Path.Combine(AppContext.BaseDirectory, "SnapDrop.ico"));
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add(new Forms.ToolStripMenuItem("SnapDrop · screenshot companion") { Enabled = false });
        _menu.Items.Add("Take screenshot", null, async (_, _) =>
        {
            try { await app.RequestCaptureAsync(); } catch (Exception ex) { app.Notify(App.FriendlyError(ex), ex); }
        });
        _recentMenu = new Forms.ToolStripMenuItem("Recent screenshots");
        _recentMenu.DropDownOpening += async (_, _) => await PopulateRecentAsync();
        _menu.Items.Add(_recentMenu);
        _menu.Items.Add("Open SnapDrop", null, (_, _) => app.ShowMain("Home"));
        _menu.Items.Add("Recent captures", null, (_, _) => app.ShowRecent());
        _menu.Items.Add("Settings", null, (_, _) => app.ShowSettings());
        _recover = new Forms.ToolStripMenuItem("Recover unsaved image");
        _recover.Click += async (_, _) => await app.ShowMemoryRecoveryAsync();
        _discard = new Forms.ToolStripMenuItem("Discard unsaved image…");
        _discard.Click += (_, _) => app.DiscardMemoryRecovery();
        _menu.Items.Add(_recover); _menu.Items.Add(_discard);
        _pause = new Forms.ToolStripMenuItem("Pause clipboard monitoring");
        _pause.Click += async (_, _) =>
        {
            try { await app.TogglePauseAsync(); }
            catch (Exception ex) { app.Notify("Monitoring could not be changed. Restart SnapDrop to retry.", ex); }
        };
        _menu.Items.Add(_pause);
        _startup = new Forms.ToolStripMenuItem("Start with Windows");
        _startup.Click += async (_, _) =>
        {
            try { await app.SaveSettingsAsync(app.Settings with { StartWithWindows = !app.Settings.StartWithWindows }); }
            catch (Exception ex) { app.Notify("Startup preference could not be saved. " + App.FriendlyError(ex), ex); }
        };
        _menu.Items.Add(_startup);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Exit", null, async (_, _) => await app.ExitAsync());
        _icon = new Forms.NotifyIcon { Icon = _ownedIcon, ContextMenuStrip = _menu, Visible = true, Text = "SnapDrop" };
        _icon.DoubleClick += (_, _) => app.ShowMain("Home");
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) _menu.Show(Forms.Cursor.Position); };
        _menu.Opening += (_, _) => Refresh();
        _icon.BalloonTipClicked += (_, _) => app.ShowSettings();
        Refresh();
    }

    public void Refresh()
    {
        _recover.Enabled = _app.HasMemoryRecovery;
        _discard.Enabled = _app.HasMemoryRecovery;
        _pause.Text = _app.IsPaused ? "Resume clipboard monitoring" : "Pause clipboard monitoring";
        _startup.Checked = _app.Settings.StartWithWindows;
        _menu.Renderer = new PaletteRenderer();
        _menu.ForeColor = PaletteRenderer.Resource("TextBrush");
        _menu.BackColor = PaletteRenderer.Resource("SurfaceBrush");
        _icon.Text = !_app.Settings.MonitoringEnabled ? "SnapDrop — clipboard images disabled" :
            _app.IsPaused ? "SnapDrop — clipboard monitoring paused" : _app.MonitoringActive ? "SnapDrop — clipboard images enabled" : "SnapDrop — clipboard monitoring inactive";
    }

    private async Task PopulateRecentAsync()
    {
        foreach (Forms.ToolStripItem old in _recentMenu.DropDownItems) old.Dispose();
        _recentMenu.DropDownItems.Clear();
        try
        {
            var records = (await _app.Repository.LoadAsync()).Take(3).ToArray();
            foreach (var record in records)
            {
                var name = record.ManagedFileName.Length > 44 ? record.ManagedFileName[..41] + "…" : record.ManagedFileName;
                var entry = new Forms.ToolStripMenuItem(name);
                entry.DropDownItems.Add("Open preview", null, async (_, _) =>
                { try { await _app.OpenViewerAsync(record); } catch (Exception ex) { _app.Notify(App.FriendlyError(ex), ex); } });
                entry.DropDownItems.Add("Copy image", null, async (_, _) =>
                {
                    try { using var item = await _app.CreateItemAsync(record, IntPtr.Zero, true); await _app.CopyAsync(item); }
                    catch (Exception ex) { _app.Notify(App.FriendlyError(ex), ex); }
                });
                entry.DropDownItems.Add("Show recovery folder", null, (_, _) =>
                { try { App.OpenFolder(Path.GetDirectoryName(_app.Repository.GetOriginalPath(record))!); } catch (Exception ex) { _app.Notify(App.FriendlyError(ex), ex); } });
                _recentMenu.DropDownItems.Add(entry);
            }
            if (records.Length == 0) _recentMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("No screenshots yet") { Enabled = false });
        }
        catch (Exception ex) { _app.Notify("Recent screenshots could not be loaded.", ex); }
    }

    private sealed class PaletteRenderer() : Forms.ToolStripProfessionalRenderer(new PaletteColors())
    {
        public static Color Resource(string key)
        {
            var brush = System.Windows.Application.Current.TryFindResource(key) as System.Windows.Media.SolidColorBrush;
            var color = brush?.Color ?? System.Windows.Media.Colors.Black;
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }
        private sealed class PaletteColors : Forms.ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => Resource("SurfaceBrush");
            public override Color ImageMarginGradientBegin => Resource("SurfaceBrush");
            public override Color ImageMarginGradientMiddle => Resource("SurfaceBrush");
            public override Color ImageMarginGradientEnd => Resource("SurfaceBrush");
            public override Color MenuItemSelected => Resource("HoverBrush");
            public override Color MenuItemBorder => Resource("FocusBrush");
            public override Color MenuBorder => Resource("BorderBrush");
        }
    }

    public void ShowNotice(string message)
    {
        _icon.BalloonTipTitle = "SnapDrop";
        _icon.BalloonTipText = message.Length > 240 ? message[..240] : message;
        _icon.ShowBalloonTip(5000);
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _ownedIcon.Dispose(); }
}
