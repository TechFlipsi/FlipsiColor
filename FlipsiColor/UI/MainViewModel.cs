using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using FlipsiColor.AI;
using FlipsiColor.Color;
using FlipsiColor.Core;
using FlipsiColor.Image;
using FlipsiColor.Video;

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

    [ObservableProperty] private string _title = "FlipsiColor v0.3.0";
    [ObservableProperty] private bool _gpuVerfuegbar;
    [ObservableProperty] private string _gpuName = "";
    [ObservableProperty] private bool _updateVerfuegbar;
    [ObservableProperty] private string _neueVersion = "";
    [ObservableProperty] private string _aktuellerModus = "Ask";
    [ObservableProperty] private bool _bildGeladen;
    [ObservableProperty] private string _bildPfad = "";
    [ObservableProperty] private string _statusText = "Bereit";
    [ObservableProperty] private bool _pipelineLaeuft;
    [ObservableProperty] private BitmapSource? _pipelineBild;

    // DJI Auto-Merge
    private readonly DjiAutoMerge _djiAutoMerge = new();
    [ObservableProperty] private ObservableCollection<DjiAutoMerge.ClipGruppe> _clipGruppen = [];
    [ObservableProperty] private bool _djiAutoMergeAktiv;
    [ObservableProperty] private bool _djiMergeLaeuft;
    [ObservableProperty] private double _djiMergeFortschritt;
    [ObservableProperty] private string _djiOrdner = "";
    [ObservableProperty] private DjiAutoMerge.ClipGruppe? _ausgewaehlteGruppe;

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
    [ObservableProperty] private bool _distortionGridAktiv;
    [ObservableProperty] private bool _colorCalibrationAktiv;
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

        // Restore saved theme
        try
        {
            var settings = Settings.Laden();
            AktuellesTheme = settings.Theme;
        }
        catch
        {
            AktuellesTheme = "Dark";
        }

        // Auto-Updater Events
        _autoUpdater.UpdateVerfuegbarChanged += (_, verfuegbar) =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateVerfuegbar = verfuegbar);
        _autoUpdater.NeueVersionChanged += (_, version) =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => NeueVersion = version);
    }

    /// <summary>Öffentliche Methode für Drag&Drop (von Code-Behind)</summary>
    public bool LoadBild(string pfad)
    {
        try
        {
            if (_imagePipeline.BildLaden(pfad))
            {
                BildPfad = pfad;
                BildGeladen = true;
                PipelineBild = null; // Original wird erst nach Pipeline angezeigt
                StatusText = $"Geladen: {System.IO.Path.GetFileName(pfad)}";
                return true;
            }
            else
            {
                System.Windows.MessageBox.Show("Bild konnte nicht geladen werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    [RelayCommand]
    private void BildOeffnen()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Bilddateien|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.cr2;*.cr3;*.nef;*.arw;*.dng;*.orf;*.rw2|Alle Dateien|*.*",
                Title = "Bild öffnen"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadBild(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PipelineAusfuehrenAsync()
    {
        if (!BildGeladen || PipelineLaeuft) return;

        PipelineLaeuft = true;
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
            ObjektivkorrekturAktiv = Objektivkorrektur,
            DistortionGridAktiv = DistortionGridAktiv,
            ColorCalibrationAktiv = ColorCalibrationAktiv
        };

        try
        {
            await Task.Run(() => _imagePipeline.PipelineAusfuehren(param));

            // Convert Mat → BitmapSource on UI thread
            var mat = _imagePipeline.Ergebnis;
            if (mat != null && !mat.Empty())
            {
                PipelineBild = MatToBitmapSourceConverter.ConvertMat(mat);
            }
            StatusText = "Pipeline abgeschlossen";
        }
        catch (Exception ex)
        {
            StatusText = $"Pipeline-Fehler: {ex.Message}";
            System.Windows.MessageBox.Show($"Pipeline fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            PipelineLaeuft = false;
        }
    }

    [RelayCommand]
    private void Zuruecksetzen()
    {
        try
        {
            Belichtung = 0; Kontrast = 0; Saettigung = 0; Vibranz = 0;
            Lichter = 0; Schatten = 0; Schaerfe = 0; RauschenLuma = 0; RauschenChroma = 0;
            Objektivkorrektur = true;
            DistortionGridAktiv = false;
            ColorCalibrationAktiv = false;
            StatusText = "Parameter zurückgesetzt";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DistortionGridKalibrieren()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Bilddateien|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp|Alle Dateien|*.*",
                Title = "Schachbrett-Referenzbild für Distortion-Grid-Kalibrierung öffnen"
            };

            if (dialog.ShowDialog() == true)
            {
                var erfolg = _imagePipeline.KalibriereDistortionGrid(dialog.FileName);
                StatusText = erfolg
                    ? "Distortion-Grid-Kalibrierung erfolgreich"
                    : "Distortion-Grid-Kalibrierung fehlgeschlagen";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ColorCalibrationKalibrieren()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Bilddateien|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp|Alle Dateien|*.*",
                Title = "ColorChecker- oder Graukarten-Referenzbild für Farbkalibrierung öffnen"
            };

            if (dialog.ShowDialog() == true)
            {
                var erfolg = _imagePipeline.KalibriereColor(dialog.FileName);
                StatusText = erfolg
                    ? "Farbkalibrierung erfolgreich"
                    : "Farbkalibrierung fehlgeschlagen";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
    }

    [RelayCommand]
    private void UpdatePruefen()
    {
        try { _autoUpdater.Pruefen(); }
        catch (Exception ex) { StatusText = $"Update-Prüfung fehlgeschlagen: {ex.Message}"; }
    }

    [RelayCommand]
    private void UpdateStarten()
    {
        try { _autoUpdater.UpdateStarten(); }
        catch (Exception ex) { StatusText = $"Update fehlgeschlagen: {ex.Message}"; }
    }

    [RelayCommand]
    private void UpdateIgnorieren()
    {
        try { _autoUpdater.Ignorieren(); }
        catch (Exception ex) { StatusText = $"Fehler: {ex.Message}"; }
    }

    [RelayCommand]
    private void ThemeWechseln(string theme)
    {
        try
        {
            AktuellesTheme = theme;
            ThemeManager.ApplyTheme(theme);

            // Theme in Settings speichern
            var settings = Settings.Laden();
            settings.Theme = theme;
            settings.Speichern();
        }
        catch (Exception ex)
        {
            StatusText = $"Theme-Fehler: {ex.Message}";
        }
    }

    // ===== DJI Auto-Merge Commands =====

    [RelayCommand]
    private void DjiOrdnerOeffnen()
    {
        try
        {
            var ordner = FolderPicker.OpenFolder("Ordner mit DJI Video-Clips auswählen");

            if (!string.IsNullOrEmpty(ordner))
            {
                DjiOrdner = ordner;
                ClipGruppen = new ObservableCollection<DjiAutoMerge.ClipGruppe>(
                    _djiAutoMerge.ClipsGruppieren(DjiOrdner));
                StatusText = $"{ClipGruppen.Count} Clip-Gruppen erkannt in {DjiOrdner}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler beim Scannen: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DjiMergeAusfuehrenAsync()
    {
        if (AusgewaehlteGruppe == null || DjiMergeLaeuft) return;

        DjiMergeLaeuft = true;
        StatusText = $"Füge {AusgewaehlteGruppe.ClipAnzahl} Clips zusammen...";

        var fortschritt = new Progress<double>(p => DjiMergeFortschritt = p);

        try
        {
            var ausgabeOrdner = System.IO.Path.Combine(DjiOrdner, "FlipsiColor_Merged");

            if (DjiAutoMergeAktiv)
            {
                // Zusammenfügen + Farbkorrektur
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

                var ergebnis = await _djiAutoMerge.ClipsZusammenfuegenMitFarbkorrekturAsync(
                    AusgewaehlteGruppe, ausgabeOrdner, _modelManager, _colorManager, param, fortschritt);

                if (ergebnis != null)
                    StatusText = $"Fertig: {System.IO.Path.GetFileName(ergebnis)} (Farbkorrektur angewendet)";
                else
                    StatusText = "Zusammenfügen mit Farbkorrektur fehlgeschlagen";
            }
            else
            {
                // Nur Zusammenfügen
                var ergebnis = await _djiAutoMerge.ClipsZusammenfuegenAsync(
                    AusgewaehlteGruppe, ausgabeOrdner, fortschritt);

                if (ergebnis != null)
                    StatusText = $"Fertig: {System.IO.Path.GetFileName(ergebnis)} (ohne Farbkorrektur)";
                else
                    StatusText = "Zusammenfügen fehlgeschlagen";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"DJI Merge Fehler: {ex.Message}";
        }
        finally
        {
            DjiMergeLaeuft = false;
        }
    }

    [RelayCommand]
    private async Task DjiAlleMergenAsync()
    {
        if (ClipGruppen.Count == 0 || DjiMergeLaeuft) return;

        DjiMergeLaeuft = true;
        var erledigt = 0;
        var gesamt = ClipGruppen.Count;
        StatusText = $"Verarbeite {gesamt} Clip-Gruppen...";

        try
        {
            var ausgabeOrdner = System.IO.Path.Combine(DjiOrdner, "FlipsiColor_Merged");
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

            foreach (var gruppe in ClipGruppen)
            {
                if (DjiAutoMergeAktiv)
                {
                    var fortschritt = new Progress<double>(p =>
                        DjiMergeFortschritt = (erledigt + p) / gesamt);
                    await _djiAutoMerge.ClipsZusammenfuegenMitFarbkorrekturAsync(
                        gruppe, ausgabeOrdner, _modelManager, _colorManager, param, fortschritt);
                }
                else
                {
                    var fortschritt = new Progress<double>(p =>
                        DjiMergeFortschritt = (erledigt + p) / gesamt);
                    await _djiAutoMerge.ClipsZusammenfuegenAsync(
                        gruppe, ausgabeOrdner, fortschritt);
                }

                erledigt++;
                DjiMergeFortschritt = (double)erledigt / gesamt;
            }

            StatusText = $"Alle {gesamt} Gruppen verarbeitet (Ausgabe: {ausgabeOrdner})";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler bei Bulk-Merge: {ex.Message}";
        }
        finally
        {
            DjiMergeLaeuft = false;
        }
    }

    public void Dispose()
    {
        _imagePipeline.Dispose();
        _modelManager.Dispose();
        _autoUpdater.Dispose();
        _djiAutoMerge.Dispose();
    }
}