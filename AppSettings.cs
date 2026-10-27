using Microsoft.Win32;

namespace HerdrTray;

/// <summary>Per-user options that outlive the window they affect.</summary>
internal sealed class AppSettings
{
    private const string KeyPath = @"Software\HerdrTray";
    private const string KeepAwakeValue = "KeepAwake";
    private const string AlwaysOnTopValue = "AlwaysOnTop";

    public bool KeepAwake { get; set; } = true;

    public bool AlwaysOnTop { get; set; }

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
        }
        catch
        {
            // The running instance already uses the new values; persistence is best-effort.
        }
    }

    private static bool ReadFlag(RegistryKey key, string name, bool fallback) =>
        key.GetValue(name) is int value ? value != 0 : fallback;
}
