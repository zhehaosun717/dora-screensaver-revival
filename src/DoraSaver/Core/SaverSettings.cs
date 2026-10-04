using Microsoft.Win32;

namespace DoraSaver.Core;

/// <param name="AllMonitors">Play on every monitor (only the primary one plays sound) instead of only the primary.</param>
internal sealed record SaverSettings(bool PlaySound, LayoutMode Layout, bool AllMonitors = true)
{
    public static SaverSettings DefaultsFor(SaverInfo saver) => new(PlaySound: false, saver.DefaultLayout, AllMonitors: true);
}

internal interface ISettingsStore
{
    SaverSettings Load(SaverInfo saver);

    void Save(SaverInfo saver, SaverSettings settings);
}

/// <summary>Per-user settings under HKCU\Software\DoraSaver\&lt;id&gt;.</summary>
internal sealed class RegistrySettingsStore : ISettingsStore
{
    private const string RootKey = @"Software\DoraSaver";
    private const string PlaySoundValue = "PlaySound";
    private const string LayoutValue = "Layout";
    private const string AllMonitorsValue = "AllMonitors";

    public SaverSettings Load(SaverInfo saver)
    {
        SaverSettings defaults = SaverSettings.DefaultsFor(saver);
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey($@"{RootKey}\{saver.Id}");
            if (key is null)
            {
                return defaults;
            }

            bool playSound = key.GetValue(PlaySoundValue) is int sound ? sound != 0 : defaults.PlaySound;
            LayoutMode layout = SaverCatalog.TryParseLayout(key.GetValue(LayoutValue) as string, out LayoutMode parsed)
                ? parsed
                : defaults.Layout;
            bool allMonitors = key.GetValue(AllMonitorsValue) is int all ? all != 0 : defaults.AllMonitors;
            return new SaverSettings(playSound, layout, allMonitors);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            Log.Error($"Reading settings for {saver.Id} failed; using defaults", ex);
            return defaults;
        }
    }

    public void Save(SaverInfo saver, SaverSettings settings)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{RootKey}\{saver.Id}", writable: true);
        key.SetValue(PlaySoundValue, settings.PlaySound ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(LayoutValue, SaverCatalog.ToQueryValue(settings.Layout), RegistryValueKind.String);
        key.SetValue(AllMonitorsValue, settings.AllMonitors ? 1 : 0, RegistryValueKind.DWord);
    }
}
