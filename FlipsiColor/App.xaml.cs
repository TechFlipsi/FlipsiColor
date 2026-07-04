using System;
using System.Windows;

using FlipsiColor.Core;
using FlipsiColor.UI;
using FlipsiColor.Utils;

namespace FlipsiColor;

/// <summary>
/// FlipsiColor App — KI-gestützte Bild- &amp; Videofarbkorrektur
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Logger initialisieren
        Logger.Init();

        var log = Serilog.Log.ForContext<App>();
        log.Information("╔══════════════════════════════════╗");
        log.Information("║  FlipsiColor v0.4.0 gestartet    ║");
        log.Information("║  KI-Farb- & Videokorrektur        ║");
        log.Information("╚══════════════════════════════════╝");

        // Theme anwenden
        var settings = Settings.Laden();
        ThemeManager.ApplyTheme(settings.Theme);

        // Hauptfenster
        var mainWindow = new MainWindow();
        mainWindow.Show();

        log.Information("App gestartet. GPU: {Gpu}", GPUInfo.GpuVerfuegbar ? GPUInfo.GpuName : "CPU-Only");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Close();
        base.OnExit(e);
    }
}