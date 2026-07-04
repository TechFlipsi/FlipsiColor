using System;
using System.Windows;

using FlipsiColor.AI;
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

            // ModelManager erstellen und Modell-Check durchführen
            var modelManager = new ModelManager();
            log.Information("Modelle: {Heruntergeladen}/{Gesamt} vorhanden ({Erforderlich} erforderlich)",
                modelManager.ModelleHeruntergeladen, modelManager.ModelleGesamt, modelManager.ModelleErforderlich);

            // LoadingWindow anzeigen wenn Modelle fehlen
            if (!modelManager.AlleErforderlichenModelleVorhanden())
            {
                log.Information("Erforderliche Modelle fehlen — LoadingWindow wird angezeigt");
                var loadingWindow = new LoadingWindow(modelManager);
                loadingWindow.Show();

                // Modelle asynchron herunterladen
                _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(async () =>
                {
                    await loadingWindow.LadeModelleAsync();
                    loadingWindow.Close();

                    // Hauptfenster anzeigen
                    var mainWindow = new MainWindow
                    {
                        DataContext = new MainViewModel()
                    };
                    mainWindow.Show();
                    log.Information("MainWindow angezeigt (nach Modell-Download).");
                });
            }
            else
            {
                // Alle Modelle vorhanden — direkt Hauptfenster
                var mainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };
                mainWindow.Show();
                log.Information("MainWindow angezeigt. Alle Modelle vorhanden.");
            }

            // Auto-Updater initialisieren
            if (settings.AutoUpdatePruefen)
            {
                var autoUpdater = new AutoUpdater { AutoUpdate = true };
                autoUpdater.Pruefen();
                log.Information("Auto-Updater initialisiert (automatisches Update aktiviert)");
            }
        }
        catch (Exception ex)
        {
            // Silent Crash verhindern — Fehler zeigen und loggen
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