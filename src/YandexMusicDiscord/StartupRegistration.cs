using Microsoft.Win32;

namespace YandexMusicDiscord;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "YandexMusicDiscord";
    private const string LegacyValueName = "YandexMusicPresence";

    public static void MigrateLegacyIfNeeded()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null || !IsLegacyExecutableValue(key.GetValue(LegacyValueName) as string)) return;

        var currentValue = key.GetValue(ValueName) as string;
        if (currentValue is not null &&
            !string.Equals(currentValue, QuotedExecutablePath(), StringComparison.OrdinalIgnoreCase)) return;

        if (currentValue is null)
            key.SetValue(ValueName, QuotedExecutablePath(), RegistryValueKind.String);
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
    }

    internal static bool IsLegacyExecutableValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var path = value.Trim();
        if (path.StartsWith('"') && path.EndsWith('"')) path = path[1..^1];
        return string.Equals(Path.GetFileName(path), LegacyValueName + ".exe", StringComparison.OrdinalIgnoreCase);
    }

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
