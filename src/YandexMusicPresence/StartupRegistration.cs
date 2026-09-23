using Microsoft.Win32;

namespace YandexMusicPresence;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "YandexMusicPresence";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue(ValueName) as string, QuotedExecutablePath(), StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Не удалось открыть настройки автозапуска Windows.");
        if (enabled) key.SetValue(ValueName, QuotedExecutablePath(), RegistryValueKind.String);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string QuotedExecutablePath() => $"\"{Application.ExecutablePath}\"";
}
