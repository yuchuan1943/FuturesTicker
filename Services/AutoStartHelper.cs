using System.IO;
using Microsoft.Win32;

namespace FuturesTicker.Services;

public static class AutoStartHelper
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "FuturesTicker";

    public static void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                key.SetValue(AppName, GetExePath());
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Best effort.
        }
    }

    private static string GetExePath()
    {
        var path = Environment.ProcessPath ?? "";
        // When launched via `dotnet FuturesTicker.dll`, point autostart at the apphost exe instead.
        if (Path.GetFileNameWithoutExtension(path)
                .Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(AppContext.BaseDirectory, "FuturesTicker.exe");
        }
        return path;
    }
}
