using System;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

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
        log.Information("║  FlipsiColor v0.4.0 gestartet    ║");
        log.Information("║  KI-Farb- & Videokorrektur       ║");
        log.Information("║  Avalonia UI — Cross-Platform    ║");
        log.Information("╚══════════════════════════════════╝");

        // Settings laden — Sprache + Theme anwenden
        var settings = Settings.Laden();
        Lokalisierung.SpracheSetzen(settings.Sprache);

        // Theme anwenden
        ThemeManager.ApplyTheme(settings.Theme);

        // GPU-Erkennung
        GPUInfo.Erkennen();
        log.Information("App gestartet. GPU: {Gpu}", GPUInfo.GpuVerfuegbar ? GPUInfo.GpuName : "CPU-Only");

        // Hauptfenster
        var mainWindow = new Views.MainWindow
        {
            DataContext = new ViewModels.MainViewModel(),
        };
        mainWindow.Show();

        base.OnFrameworkInitializationCompleted();
    }
}