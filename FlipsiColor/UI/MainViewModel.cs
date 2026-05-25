using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlipsiColor.AI;
using FlipsiColor.Color;
using FlipsiColor.Core;
using FlipsiColor.Image;

namespace FlipsiColor.UI;

/// <summary>
/// Main ViewModel — steuert die komplette App
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ModelManager _modelManager;
    private readonly ColorManager _colorManager;
    private readonly ImagePipeline _imagePipeline;
    private readonly AutoUpdater _autoUpdater;

    [ObservableProperty] private string _title = "FlipsiColor v0.2.0";
    [ObservableProperty] private bool _gpuVerfuegbar;
    [ObservableProperty] private string _gpuName = "";
    [ObservableProperty] private bool _updateVerfuegbar;
    [ObservableProperty] private string _neueVersion = "";
    [ObservableProperty] private string _aktuellerModus = "Ask";
    [ObservableProperty] private bool _bildGeladen;
    [ObservableProperty] private string _bildPfad = "";
    [ObservableProperty] private string _statusText = "Bereit";

    // Pipeline Controls
    [ObservableProperty] private float _belichtung;
    [ObservableProperty] private float _kontrast;
    [ObservableProperty] private float _saettigung;
    [ObservableProperty] private float _vibranz;
    [ObservableProperty] private float _lichter;
    [ObservableProperty] private float _schatten;
    [ObservableProperty] private float _schaerfe;
    [ObservableProperty] private float _rauschenLuma;
    [ObservableProperty] private float _rauschenChroma;
    [ObservableProperty] private bool _objektivkorrektur = true;
    [ObservableProperty] private int _intensitaetIndex = 1; // Mittel

    // Theme
    [ObservableProperty] private string _aktuellesTheme = "System";

    public ObservableCollection<string> VerfuegbareSprachen { get; } = new() { "de", "en", "es", "fr", "it", "pt_BR", "ja" };

    public MainViewModel()
    {
        _modelManager = new ModelManager();
        _colorManager = new ColorManager();
        _imagePipeline = new ImagePipeline(_modelManager, _colorManager);
        _autoUpdater = new AutoUpdater();

        GPUInfo.Erkennen();
        GpuVerfuegbar = GPUInfo.GpuVerfuegbar;
        GpuName = GPUInfo.GpuName;

        _colorManager.Initialisieren();

        // Auto-Updater Events
        _autoUpdater.UpdateVerfuegbarChanged += (_, verfuegbar) =>
            Application.Current.Dispatcher.Invoke(() => UpdateVerfuegbar = verfuegbar);
        _autoUpdater.NeueVersionChanged += (_, version) =>
            Application.Current.Dispatcher.Invoke(() => NeueVersion = version);
    }

    [RelayCommand]
    private void BildOeffnen()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Bilddateien|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.cr2;*.cr3;*.nef;*.arw;*.dng;*.orf;*.rw2|Alle Dateien|*.*",
            Title = "Bild öffnen"
        };

        if (dialog.ShowDialog() == true)
        {
            if (_imagePipeline.BildLaden(dialog.FileName))
            {
                BildPfad = dialog.FileName;
                BildGeladen = true;
                StatusText = $"Geladen: {System.IO.Path.GetFileName(dialog.FileName)}";
            }
            else
            {
                MessageBox.Show("Bild konnte nicht geladen werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void PipelineAusfuehren()
    {
        if (!BildGeladen) return;

        StatusText = "Pipeline läuft...";
        var param = new PipelineParams
        {
            Belichtung = Belichtung,
            Kontrast = Kontrast,
            Saettigung = Saettigung,
            Vibranz = Vibranz,
            Lichter = Lichter,
            Schatten = Schatten,
            SchaerfeBetrag = Schaerfe,
            LuminanzRauschen = RauschenLuma,
            ChrominanzRauschen = RauschenChroma,
            ObjektivkorrekturAktiv = Objektivkorrektur
        };

        _imagePipeline.PipelineAusfuehren(param);
        StatusText = "Pipeline abgeschlossen";
    }

    [RelayCommand]
    private void Zuruecksetzen()
    {
        Belichtung = 0; Kontrast = 0; Saettigung = 0; Vibranz = 0;
        Lichter = 0; Schatten = 0; Schaerfe = 0; RauschenLuma = 0; RauschenChroma = 0;
        Objektivkorrektur = true;
        StatusText = "Parameter zurückgesetzt";
    }

    [RelayCommand]
    private void UpdatePruefen() => _autoUpdater.Pruefen();

    [RelayCommand]
    private void UpdateStarten() => _autoUpdater.UpdateStarten();

    [RelayCommand]
    private void UpdateIgnorieren() => _autoUpdater.Ignorieren();

    [RelayCommand]
    private void ThemeWechseln(string theme)
    {
        AktuellesTheme = theme;
        ThemeManager.ApplyTheme(theme);
    }

    public void Dispose()
    {
        _imagePipeline.Dispose();
        _modelManager.Dispose();
        _autoUpdater.Dispose();
    }
}