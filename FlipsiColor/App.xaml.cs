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

        try
        {
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

            // GPU-Erkennung
            GPUInfo.Erkennen();
            log.Information("App gestartet. GPU: {Gpu}", GPUInfo.GpuVerfuegbar ? GPUInfo.GpuName : "CPU-Only");

            // Hauptfenster — NICHT StartupUri verwenden (wir erstellen es manuell)
            var mainWindow = new MainWindow
            {
                DataContext = new MainViewModel()
            };
            mainWindow.Show();

            log.Information("MainWindow angezeigt.");
        }
        catch (Exception ex)
        {
            // Silent Crash verhindern — Fehler anzeigen und loggen
            System.Windows.MessageBox.Show(
                $"FlipsiColor konnte nicht gestartet werden:\n\n{ex}",
                "FlipsiColor — Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Close();
        base.OnExit(e);
    }
}