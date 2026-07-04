using System;
using Avalonia;
using Avalonia.Styling;

namespace FlipsiColor;

/// <summary>
/// Theme-Manager — Dark/Light/System Theme-Unterstützung für Avalonia.
/// Verwendet Application.Current.RequestedThemeVariant für Live-Switch.
/// </summary>
public static class ThemeManager
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "ThemeManager");

    public static string AktuellesTheme { get; private set; } = "System";

    /// <summary>
    /// Wendet ein Theme an ("Dark", "Light", "System").
    /// </summary>
    public static void ApplyTheme(string theme)
    {
        AktuellesTheme = theme;
        var effectiveTheme = theme;

        if (theme == "System")
        {
            effectiveTheme = DetectSystemTheme();
        }

        var app = Application.Current;
        if (app == null) return;

        // Avalonia: ThemeVariant für Live-Switch
        if (effectiveTheme == "Dark")
        {
            app.RequestedThemeVariant = ThemeVariant.Dark;
        }
        else
        {
            app.RequestedThemeVariant = ThemeVariant.Light;
        }

        Log.Information("Design geändert: {Theme} (effektiv: {Effective})", theme, effectiveTheme);
    }

    /// <summary>
    /// Erkennt das System-Theme. Auf Windows via Registry, auf Linux/macOS Fallback "Dark".
    /// </summary>
    private static string DetectSystemTheme()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int value)
                    return value == 0 ? "Dark" : "Light";
            }
            catch
            {
                // Fallback
            }
        }

        // Linux/macOS Fallback: Dark (bessere Lesbarkeit)
        return "Dark";
    }
}