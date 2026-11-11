using System.Runtime.InteropServices;

namespace SnapDrop.Windows;

public static class StartupRegistration
{
    private static string? ShortcutPath
    {
        get
        {
            var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return string.IsNullOrWhiteSpace(folder) ? null : Path.Combine(folder, "SnapDrop.lnk");
        }
    }
    public static bool IsEnabled => ShortcutPath is { } path && File.Exists(path);
    public static void SetEnabled(bool enabled)
    {
        var shortcutPath = ShortcutPath;
        if (!enabled) { if (shortcutPath is not null) File.Delete(shortcutPath); return; }
        if (shortcutPath is null) throw new IOException("Windows did not provide a Startup folder. Startup registration was not changed.");
        var executable = Path.Combine(AppContext.BaseDirectory, "SnapDrop.App.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("The SnapDrop executable is unavailable. Install or publish the app before enabling startup.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows shortcut services are unavailable."));
            dynamic shellDispatch = shell!;
            shortcut = shellDispatch.CreateShortcut(shortcutPath);
            dynamic link = shortcut;
            link.TargetPath = executable;
            link.WorkingDirectory = AppContext.BaseDirectory;
            link.Description = "SnapDrop screenshot companion";
            link.Save();
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }
}
