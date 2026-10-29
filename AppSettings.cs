using Microsoft.Win32;

namespace HerdrTray;

/// <summary>Per-user options that outlive the window they affect.</summary>
internal sealed class AppSettings
{
    private const string KeyPath = @"Software\HerdrTray";
    private const string KeepAwakeValue = "KeepAwake";
    private const string AlwaysOnTopValue = "AlwaysOnTop";
    private const string StripEnabledValue = "StripEnabled";
    private const string StripXValue = "StripX";
    private const string StripYValue = "StripY";

    public bool KeepAwake { get; set; } = true;

    public bool AlwaysOnTop { get; set; }

    public bool StripEnabled { get; set; }

    /// <summary>Progress strip window position; null keeps the default corner placement.</summary>
    public int? StripX { get; set; }

    /// <summary>Progress strip window position; null keeps the default corner placement.</summary>
    public int? StripY { get; set; }

    public static AppSettings Load()
    {
        var settings = new AppSettings();
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            if (key is not null)
            {
                settings.KeepAwake = ReadFlag(key, KeepAwakeValue, settings.KeepAwake);
                settings.AlwaysOnTop = ReadFlag(key, AlwaysOnTopValue, settings.AlwaysOnTop);
                settings.StripEnabled = ReadFlag(key, StripEnabledValue, settings.StripEnabled);
                settings.StripX = ReadInt(key, StripXValue, settings.StripX);
                settings.StripY = ReadInt(key, StripYValue, settings.StripY);
            }
        }
        catch
        {
            // Defaults are good enough when the per-user key cannot be read.
        }

        return settings;
    }

    public void Save()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key.SetValue(KeepAwakeValue, KeepAwake ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(AlwaysOnTopValue, AlwaysOnTop ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(StripEnabledValue, StripEnabled ? 1 : 0, RegistryValueKind.DWord);
            // StripX/StripY are not written here: the strip moves without this shared instance
            // knowing, so an option save would revert a dragged position to a stale value.
        }
        catch
        {
            // The running instance already uses the new values; persistence is best-effort.
        }
    }

    /// <summary>
    /// Persists only the strip position. The strip updates it without the shared settings
    /// instance, so the regular Save must never rewrite the location it still holds.
    /// </summary>
    public static void SaveStripLocation(int x, int y)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath);
            key.SetValue(StripXValue, x, RegistryValueKind.DWord);
            key.SetValue(StripYValue, y, RegistryValueKind.DWord);
        }
        catch
        {
            // Persistence stays best-effort, as with the other values.
        }
    }

    private static bool ReadFlag(RegistryKey key, string name, bool fallback) =>
        key.GetValue(name) is int value ? value != 0 : fallback;

    private static int? ReadInt(RegistryKey key, string name, int? fallback) =>
        key.GetValue(name) is int value ? value : fallback;
}
