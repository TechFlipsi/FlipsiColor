using System;
using System.Windows;

namespace FlipsiColor.UI;

/// <summary>
/// Theme-Manager — Dark/Light/System Theme-Unterstützung
/// </summary>
public class ThemeManager
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ThemeManager>();

    public static string AktuellesTheme { get; private set; } = "System";

    /// <summary>
    /// Wendet ein Theme an ("Dark", "Light", "System")
    /// </summary>
    public static void ApplyTheme(string theme)
    {
        AktuellesTheme = theme;
        var effectiveTheme = theme;

        if (theme == "System")
        {
            effectiveTheme = DetectSystemTheme();
        }

        var app = System.Windows.Application.Current;
        if (app == null) return;

        var darkDict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/DarkTheme.xaml", UriKind.Absolute)
        };
        var lightDict = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/LightTheme.xaml", UriKind.Absolute)
        };

        // Altes Theme entfernen
        var themeDicts = app.Resources.MergedDictionaries
            .Where(d => d.Source?.OriginalString?.Contains("Theme") == true)
            .ToList();
        foreach (var d in themeDicts)
            app.Resources.MergedDictionaries.Remove(d);

        // Neues Theme hinzufügen
        if (effectiveTheme == "Dark")
        {
            app.Resources.MergedDictionaries.Add(darkDict);
        }
        else
        {
            app.Resources.MergedDictionaries.Add(lightDict);
        }

        Log.Information("Theme geändert: {Theme} (effektiv: {Effective})", theme, effectiveTheme);
    }

    /// <summary>
    /// Erkennt das Windows-System-Theme
    /// </summary>
    private static string DetectSystemTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
                return value == 0 ? "Dark" : "Light";
        }
        catch (Exception ex)
        {
            Log.Debug("Theme-Fallback: {Fehler}", ex.Message);
        }
        return "Light";
    }
}