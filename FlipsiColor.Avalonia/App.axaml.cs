using System;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using FlipsiColor.AI;
using FlipsiColor.Core;
using FlipsiColor.Utils;

namespace FlipsiColor;

/// <summary>
/// FlipsiColor App — KI-gestützte Bild- &amp; Videofarbkorrektur (Avalonia UI, cross-platform)
/// </summary>
public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Logger initialisieren
        Logger.Init();

        var log = Serilog.Log.ForContext<App>();
        log.Information("╔══════════════════════════════════╗");
        log.Information("║  FlipsiColor v0.5.4 gestartet    ║");
        log.Information("║  KI-Farb- & Videokorrektur       ║");
        log.Information("║  Avalonia UI — Cross-Platform    ║");
        log.Information("╚══════════════════════════════════╝");

        try
        {
            // Settings laden — Sprache + Theme anwenden
            var settings = Settings.Laden();
            // Sprache initialisieren (JSON-basiert, Issue #9)
            Lokalisierung.Initialisieren(settings.Sprache);

            // Theme anwenden
            ThemeManager.ApplyTheme(settings.Theme);

            // GPU-Erkennung
            GPUInfo.Erkennen();
            log.Information("App gestartet. GPU: {Gpu}", GPUInfo.GpuVerfuegbar ? GPUInfo.GpuName : "CPU-Only");

            // ModelManager erstellen und Modell-Check durchführen
            var modelManager = new ModelManager();
            log.Information("Modelle: {Heruntergeladen}/{Gesamt} vorhanden ({Erforderlich} erforderlich)",
                modelManager.ModelleHeruntergeladen, modelManager.ModelleGesamt, modelManager.ModelleErforderlich);

            // Hauptfenster anzeigen
            var mainWindow = new Views.MainWindow
            {
                DataContext = new ViewModels.MainViewModel(),
            };
            mainWindow.Show();

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
            log.Error(ex, "Fatal error during startup");
            Console.WriteLine($"FlipsiColor fatal error: {ex}");
        }

        base.OnFrameworkInitializationCompleted();
    }
}