using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
    private readonly VideoPipeline _videoPipeline;
    private readonly AutoUpdater _autoUpdater;

    [ObservableProperty] private string _title = "FlipsiColor v0.6.0";
    [ObservableProperty] private bool _gpuVerfuegbar;
    [ObservableProperty] private string _gpuName = "";
    [ObservableProperty] private bool _updateVerfuegbar;
    [ObservableProperty] private string _neueVersion = "";
    [ObservableProperty] private string _aktuellerModus = "Ask";
    [ObservableProperty] private bool _bildGeladen;
    [ObservableProperty] private string _bildPfad = "";
    [ObservableProperty] private string _statusText = Lokalisierung.T("App.Bereit");
    [ObservableProperty] private bool _pipelineLaeuft;
    [ObservableProperty] private BitmapSource? _pipelineBild;

    // Video-Status
    [ObservableProperty] private bool _videoGeladen;
    [ObservableProperty] private string _videoPfad = "";
    [ObservableProperty] private bool _videoPipelineLaeuft;
    [ObservableProperty] private double _videoFortschritt;
    [ObservableProperty] private string _videoInfo = "";

    // Clip-Merge — generische Video-Clip-Zusammenführung für alle Kameras
    private readonly ClipMerger _clipMerger = new();
    [ObservableProperty] private ObservableCollection<ClipMerger.ClipGruppe> _clipGruppen = [];
    [ObservableProperty] private bool _clipMergeFarbkorrekturAktiv;
    [ObservableProperty] private bool _clipMergeLaeuft;
    [ObservableProperty] private double _clipMergeFortschritt;
    [ObservableProperty] private string _clipOrdner = "";
    [ObservableProperty] private ClipMerger.ClipGruppe? _ausgewaehlteGruppe;

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
    private bool _initialisiert;
    [ObservableProperty] private bool _distortionGridAktiv;
    [ObservableProperty] private bool _colorCalibrationAktiv;
    [ObservableProperty] private int _intensitaetIndex = 1; // Mittel

    // Manuelle Kamera/Objektiv-Auswahl für Objektivkorrektur
    [ObservableProperty] private ObservableCollection<string> _verfuegbareKameras = [];
    [ObservableProperty] private string? _ausgewaehlteKamera;
    [ObservableProperty] private ObservableCollection<string> _verfuegbareObjektive = [];
    [ObservableProperty] private string? _ausgewaehltesObjektiv;
    [ObservableProperty] private bool _lensfunInstalliert;
    private readonly LensfunInstaller _lensfunInstaller = new();

    /// <summary>
    /// Wird aufgerufen wenn Objektivkorrektur aktiviert/deaktiviert wird.
    /// Bei Aktivierung: prüft ob Lensfun installiert ist und startet ggf. Installation.
    /// Lädt außerdem die verfügbaren Kameras aus der Datenbank.
    /// </summary>
    partial void OnObjektivkorrekturChanged(bool value)
    {
        if (!_initialisiert) return;
        if (!value) return;

        // Lensfun-Installation prüfen
        LensfunInstalliert = _lensfunInstaller.IstInstalliert;
        if (!LensfunInstalliert)
        {
            System.Diagnostics.Debug.WriteLine("Lensfun nicht installiert — starte automatische Installation");
            _ = InstalliereLensfunAsync();
        }
        else
        {
            // Kameras aus Datenbank laden
            LadeVerfuegbareKameras();
        }
    }

    /// <summary>
    /// Wird aufgerufen wenn die ausgewählte Kamera sich ändert.
    /// Lädt die verfügbaren Objektive für diese Kamera.
    /// </summary>
    partial void OnAusgewaehlteKameraChanged(string? value)
    {
        VerfuegbareObjektive.Clear();
        if (!string.IsNullOrWhiteSpace(value))
        {
            LadeVerfuegbareObjektive(value);
        }
    }

    /// <summary>
    /// Lädt die verfügbaren Kamera-Hersteller aus der Lensfun-Datenbank.
    /// </summary>
    private void LadeVerfuegbareKameras()
    {
        try
        {
            using var corrector = new LensCorrector();
            if (corrector.Initialisieren())
            {
                var kameras = corrector.ListeKameras();
                VerfuegbareKameras.Clear();
                foreach (var k in kameras)
                    VerfuegbareKameras.Add(k);
                System.Diagnostics.Debug.WriteLine($"Lensfun: {kameras.Count} Kamera-Hersteller geladen");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lensfun: LadeVerfuegbareKameras fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Lädt die verfügbaren Objektive für eine Kamera aus der Lensfun-Datenbank.
    /// </summary>
    private void LadeVerfuegbareObjektive(string kamera)
    {
        try
        {
            using var corrector = new LensCorrector();
            if (corrector.Initialisieren())
            {
                var objektive = corrector.ListeObjektive(kamera);
                VerfuegbareObjektive.Clear();
                foreach (var o in objektive)
                    VerfuegbareObjektive.Add(o);
                System.Diagnostics.Debug.WriteLine($"Lensfun: {objektive.Count} Objektive für '{kamera}' geladen");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lensfun: LadeVerfuegbareObjektive fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Installiert Lensfun asynchron (Windows: ZIP herunterladen + entpacken).
    /// Nach erfolgreicher Installation werden die verfügbaren Kameras geladen.
    /// </summary>
    private async Task InstalliereLensfunAsync()
    {
        if (InstallationLaeuft) return;

        InstallationLaeuft = true;
        InstallationsText = Lokalisierung.T("Lensfun.Installiere");
        StatusText = Lokalisierung.T("Lensfun.Installiere");

        _lensfunInstaller.InstallationsFortschritt += OnLensfunInstallationsFortschritt;

        try
        {
            var erfolg = await _lensfunInstaller.InstallierenAsync();
            LensfunInstalliert = _lensfunInstaller.IstInstalliert;

            if (erfolg && LensfunInstalliert)
            {
                InstallationsText = Lokalisierung.T("Lensfun.Aktiv");
                StatusText = Lokalisierung.T("Lensfun.InstalliertKameras");
                LadeVerfuegbareKameras();
            }
            else
            {
                InstallationsText = Lokalisierung.T("Lensfun.InstallationFehlgeschlagen");
                StatusText = Lokalisierung.T("Lensfun.InstallationFehlgeschlagen");
            }
        }
        catch (Exception ex)
        {
            InstallationsText = string.Format(Lokalisierung.T("Lensfun.InstallationFehlgeschlagenMitFehler"), ex.Message);
            StatusText = string.Format(Lokalisierung.T("Lensfun.InstallationFehlgeschlagenMitFehler"), ex.Message);
        }
        finally
        {
            _lensfunInstaller.InstallationsFortschritt -= OnLensfunInstallationsFortschritt;
            InstallationLaeuft = false;
        }
    }

    /// <summary>
    /// Callback für den LensfunInstaller-Progress — aktualisiert UI auf dem UI-Thread.
    /// </summary>
    private void OnLensfunInstallationsFortschritt(object? sender, LensfunInstallFortschritt e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            InstallationsFortschritt = (int)Math.Clamp(e.Prozent, 0, 100);
            InstallationsText = e.Schritt;
        });
    }

    // Upscaling & Gesichtswiederherstellung
    [ObservableProperty] private int _hochskalierenFaktor = 1;
    [ObservableProperty] private bool _gesichtswiederherstellungAktiv;

    // StyleLUT
    [ObservableProperty] private string? _styleLutPfad;
    [ObservableProperty] private string _styleLutName = "";

    // Theme
    [ObservableProperty] private string _aktuellesTheme = "System";

    // Video-Backend (FFmpeg / VapourSynth) — Einstellungen
    [ObservableProperty] private VideoBackend _videoBackend = VideoBackend.FFmpeg;
    [ObservableProperty] private bool _vapourSynthInstalliert;
    [ObservableProperty] private int _installationsFortschritt;
    [ObservableProperty] private string _installationsText = "";
    [ObservableProperty] private bool _installationLaeuft;
    [ObservableProperty] private bool _vapourSynthInstallUiSichtbar;
    [ObservableProperty] private bool _vapourSynthAktivSichtbar;
    private readonly VapourSynthInstaller _vapourSynthInstaller = new();

    /// <summary>
    /// Wird aufgerufen wenn VideoBackend sich ändert.
    /// Bei VapourSynth: automatische Verifikation + Installation falls nötig.
    /// </summary>
    partial void OnVideoBackendChanged(VideoBackend value)
    {
        UpdateVapourSynthSichtbarkeit();

        if (!_initialisiert) return;

        // Einstellung speichern
        try
        {
            var settings = Settings.Laden();
            settings.VideoBackend = value;
            settings.Speichern();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"VideoBackend-Einstellung konnte nicht gespeichert werden: {ex.Message}");
        }

        // Nur bei VapourSynth prüfen/installieren
        if (value != VideoBackend.VapourSynth) return;
        if (InstallationLaeuft) return;

        // Verifikation: Ist VapourSynth installiert UND fehlerfrei?
        bool installiert = _vapourSynthInstaller.IstInstalliert;
        VapourSynthInstalliert = installiert;

        if (!installiert)
        {
            // Nicht installiert → automatisch Installation starten (fire-and-forget)
            System.Diagnostics.Debug.WriteLine("VapourSynth nicht installiert — starte automatische Installation");
            _ = InstalliereVapourSynthAsync();
        }
        else
        {
            // Installiert — Status setzen
            InstallationsText = Lokalisierung.T("Status.VapourSynthAktiv");
            StatusText = Lokalisierung.T("Status.VapourSynthAktiv");
        }
    }

    /// <summary>Wird aufgerufen wenn VapourSynthInstalliert sich ändert — aktualisiert Sichtbarkeit.</summary>
    partial void OnVapourSynthInstalliertChanged(bool value) => UpdateVapourSynthSichtbarkeit();

    /// <summary>Aktualisiert die berechneten Sichtbarkeits-Properties für die Installations-UI.</summary>
    private void UpdateVapourSynthSichtbarkeit()
    {
        VapourSynthInstallUiSichtbar = VideoBackend == VideoBackend.VapourSynth && !VapourSynthInstalliert;
        VapourSynthAktivSichtbar = VideoBackend == VideoBackend.VapourSynth && VapourSynthInstalliert;
    }

    // Drag & Drop Dateiliste
    [ObservableProperty] private ObservableCollection<DateiEintrag> _dateiListe = [];

    // Sprach-Index für ComboBox (0 = Deutsch, 1 = English)
    [ObservableProperty] private int _spracheIndex;

    // Event für Sprachwechsel — MainWindow lauscht darauf und baut die UI neu auf
    public event EventHandler? SpracheGeaendert;

    public ObservableCollection<string> VerfuegbareSprachen { get; } = new() { "de", "en", "es", "fr", "it", "pt_BR", "ja" };

    /// <summary>
    /// Unterstützte Bild-Dateiendungen für Drag &amp; Drop und Dateiauswahl.
    /// </summary>
    public static readonly HashSet<string> BildEndungen = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp",
        ".raw", ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2"
    };

    /// <summary>
    /// Unterstützte Video-Dateiendungen für Drag &amp; Drop und Dateiauswahl.
    /// </summary>
    public static readonly HashSet<string> VideoEndungen = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv"
    };

    public MainViewModel()
    {
        _modelManager = new ModelManager();
        _colorManager = new ColorManager();
        _imagePipeline = new ImagePipeline(_modelManager, _colorManager);
        _videoPipeline = new VideoPipeline(_modelManager, _colorManager);
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
            // Video-Backend aus Settings laden
            VideoBackend = settings.VideoBackend;
            VapourSynthInstalliert = _vapourSynthInstaller.IstInstalliert;

            // Sprache aus Settings laden und anwenden (Issue #9: leer = Systemsprache erkennen)
            var effektiveSprache = string.IsNullOrEmpty(settings.Sprache) ? Lokalisierung.Sprache : settings.Sprache;
            var sprachenListe = new[] { "de", "en", "es", "fr", "it", "nl", "pl", "pt", "tr", "ru", "zh", "ja", "ko" };
            SpracheIndex = Array.IndexOf(sprachenListe, effektiveSprache);
            if (SpracheIndex < 0) SpracheIndex = 0; // Fallback Deutsch
            Lokalisierung.SpracheSetzen(settings.Sprache);
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

        // Initialisierung abgeschlossen — ab jetzt dürfen Change-Handler feuern
        _initialisiert = true;
    }

    /// <summary>
    /// Konvertiert IntensitaetIndex (0/1/2) in den Intensitaet-Enum.
    /// </summary>
    private Intensitaet IntensitaetFromIndex() => IntensitaetIndex switch
    {
        0 => Intensitaet.Leicht,
        2 => Intensitaet.Stark,
        _ => Intensitaet.Mittel
    };

    /// <summary>
    /// Konvertiert AktuellerModus-String in den BetriebsModus-Enum.
    /// </summary>
    private BetriebsModus ModusFromString() => AktuellerModus switch
    {
        "Turbo" => BetriebsModus.Turbo,
        "SmartLearn" => BetriebsModus.SmartLearn,
        _ => BetriebsModus.Ask
    };

    // ===== Drag & Drop Dateiliste =====

    /// <summary>
    /// Fügt Dateien zur Dateiliste hinzu (von Drag &amp; Drop oder Ordner-Auswahl).
    /// Unterstützt einzelne Dateien und Ordner (rekursiv).
    /// </summary>
    public void DateienHinzufuegen(string[] pfade)
    {
        foreach (var pfad in pfade)
        {
            if (Directory.Exists(pfad))
            {
                // Ordner: rekursiv alle unterstützten Dateien laden
                var dateien = Directory.GetFiles(pfad, "*", SearchOption.AllDirectories)
                    .Where(f => IstUnterstuetzteDatei(f));
                foreach (var datei in dateien)
                    DateiListe.Add(new DateiEintrag(datei));
            }
            else if (File.Exists(pfad) && IstUnterstuetzteDatei(pfad))
            {
                DateiListe.Add(new DateiEintrag(pfad));
            }
        }

        StatusText = DateiListe.Count > 0
            ? string.Format(Lokalisierung.T("Galerie.DateiZaehler"), DateiListe.Count)
            : Lokalisierung.T("Status.KeineUnterstuetzteDateien");
    }

    /// <summary>
    /// Prüft ob eine Datei ein unterstütztes Bild- oder Video-Format ist.
    /// </summary>
    public static bool IstUnterstuetzteDatei(string pfad)
    {
        var ext = Path.GetExtension(pfad);
        return BildEndungen.Contains(ext) || VideoEndungen.Contains(ext);
    }

    /// <summary>
    /// Entfernt einen einzelnen Eintrag aus der Dateiliste.
    /// </summary>
    [RelayCommand]
    private void DateiEntfernen(DateiEintrag eintrag)
    {
        if (eintrag != null)
        {
            DateiListe.Remove(eintrag);
            StatusText = string.Format(Lokalisierung.T("Status.DateiEntfernt"), eintrag.Dateiname);
        }
    }

    /// <summary>
    /// Leert die komplette Dateiliste.
    /// </summary>
    [RelayCommand]
    private void DateiListeLeeren()
    {
        DateiListe.Clear();
        StatusText = Lokalisierung.T("Status.DateilisteGeleert");
    }

    /// <summary>Öffentliche Methode für Drag&amp;Drop (von Code-Behind)</summary>
    public bool LoadBild(string pfad)
    {
        try
        {
            if (_imagePipeline.BildLaden(pfad))
            {
                BildPfad = pfad;
                BildGeladen = true;
                PipelineBild = null; // Original wird erst nach Pipeline angezeigt
                StatusText = $"{Lokalisierung.T("Status.Geladen")}: {Path.GetFileName(pfad)}";
                return true;
            }
            else
            {
                System.Windows.MessageBox.Show(Lokalisierung.T("Status.BildKonnteNichtGeladenWerden"), Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(Lokalisierung.T("Status.FehlerBeimLaden"), ex.Message), Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                Filter = $"{Lokalisierung.T("Dialog.Bilddateien")}|{Lokalisierung.T("Filter.Bilder")}|{Lokalisierung.T("Dialog.AlleDateien")}|*.*",
                Title = Lokalisierung.T("Dialog.BildOeffnen"),
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                // Alle ausgewählten Dateien zur Galerie hinzufügen
                DateienHinzufuegen(dialog.FileNames);
                // Erste Datei als Vorschau laden
                LoadBild(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"{Lokalisierung.T("Status.Fehler")}: {ex.Message}", Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Öffnet den Datei-Dialog mit Multiselect — je nach aktivem Modus (Bild oder Video).
    /// Alle ausgewählten Dateien werden zur Galerie hinzugefügt.
    /// </summary>
    [RelayCommand]
    private void Oeffnen()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"{Lokalisierung.T("Dialog.Bilddateien")}|{Lokalisierung.T("Filter.Bilder")}|{Lokalisierung.T("Dialog.Videodateien")}|{Lokalisierung.T("Filter.Videos")}|{Lokalisierung.T("Dialog.AlleDateien")}|*.*",
                Title = Lokalisierung.T("Dialog.DateiOeffnen"),
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                // Alle ausgewählten Dateien zur Galerie hinzufügen
                DateienHinzufuegen(dialog.FileNames);

                // Erste Bilddatei als Vorschau laden
                var firstImage = dialog.FileNames
                    .FirstOrDefault(f => BildEndungen.Contains(System.IO.Path.GetExtension(f)));
                if (firstImage != null)
                    LoadBild(firstImage);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"{Lokalisierung.T("Status.Fehler")}: {ex.Message}", Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Exportiert das bearbeitete Bild.
    /// </summary>
    [RelayCommand]
    private void Export()
    {
        try
        {
            if (!BildGeladen || PipelineBild == null)
            {
                System.Windows.MessageBox.Show(Lokalisierung.T("Export.Hinweis"),
                    Lokalisierung.T("Export.Titel"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = Lokalisierung.T("Filter.Export"),
                Title = Lokalisierung.T("Dialog.BildExportieren"),
                FileName = System.IO.Path.GetFileNameWithoutExtension(BildPfad) + "_flipsicolor"
            };

            if (dialog.ShowDialog() == true)
            {
                var encoder = dialog.FilterIndex switch
                {
                    1 => (System.Windows.Media.Imaging.BitmapEncoder)new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 95 },
                    2 => new System.Windows.Media.Imaging.PngBitmapEncoder(),
                    3 => new System.Windows.Media.Imaging.TiffBitmapEncoder(),
                    _ => new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 95 }
                };

                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(PipelineBild));
                using var fs = System.IO.File.Create(dialog.FileName);
                encoder.Save(fs);
                StatusText = $"{Lokalisierung.T("Status.Exportiert")}: {dialog.FileName}";
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(string.Format(Lokalisierung.T("Status.ExportFehler"), ex.Message), Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task PipelineAusfuehrenAsync()
    {
        if (!BildGeladen || PipelineLaeuft) return;

        PipelineLaeuft = true;
        StatusText = Lokalisierung.T("Status.PipelineLaeuft");

        var param = new PipelineParams
        {
            Belichtung = Belichtung,
            Kontrast = Kontrast,
            Saettigung = Saettigung,
            Vibranz = Vibranz,
            Lichter = Lichter,
            Schatten = Schatten,
            SchaerfeBetrag = Schaerfe,
            LuminanzRauschen = RauschenLuma / 100f, // Slider 0-100 → 0.0-1.0
            ChrominanzRauschen = RauschenChroma / 100f,
            ObjektivkorrekturAktiv = Objektivkorrektur,
            DistortionGridAktiv = DistortionGridAktiv,
            ColorCalibrationAktiv = ColorCalibrationAktiv,
            // ── Verkabelte Parameter ──
            Intensitaet = IntensitaetFromIndex(),
            Modus = ModusFromString(),
            HochskalierenFaktor = HochskalierenFaktor,
            GesichtswiederherstellungAktiv = GesichtswiederherstellungAktiv,
            StyleLutPfad = StyleLutPfad,
            // Manuelle Kamera/Objektiv-Auswahl (null = EXIF verwenden)
            ManuelleKamera = AusgewaehlteKamera,
            ManuellesObjektiv = AusgewaehltesObjektiv
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
            StatusText = Lokalisierung.T("Status.PipelineAbgeschlossen");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.PipelineFehler")}: {ex.Message}";
            System.Windows.MessageBox.Show($"{Lokalisierung.T("Status.PipelineFehlgeschlagen")}: {ex.Message}", Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
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
            HochskalierenFaktor = 1;
            GesichtswiederherstellungAktiv = false;
            StyleLutPfad = null;
            StyleLutName = "";
            AusgewaehlteKamera = null;
            AusgewaehltesObjektiv = null;
            StatusText = Lokalisierung.T("Status.ParameterZurueckgesetzt");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DistortionGridKalibrieren()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"{Lokalisierung.T("Dialog.Bilddateien")}|{Lokalisierung.T("Filter.BilderStandard")}|{Lokalisierung.T("Dialog.AlleDateien")}|*.*",
                Title = Lokalisierung.T("Dialog.SchachbrettOeffnen")
            };

            if (dialog.ShowDialog() == true)
            {
                var erfolg = _imagePipeline.KalibriereDistortionGrid(dialog.FileName);
                StatusText = erfolg
                    ? Lokalisierung.T("Status.DistortionGridErfolgreich")
                    : Lokalisierung.T("Status.DistortionGridFehlgeschlagen");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ColorCalibrationKalibrieren()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"{Lokalisierung.T("Dialog.Bilddateien")}|{Lokalisierung.T("Filter.BilderStandard")}|{Lokalisierung.T("Dialog.AlleDateien")}|*.*",
                Title = Lokalisierung.T("Dialog.ColorCheckerOeffnen")
            };

            if (dialog.ShowDialog() == true)
            {
                var erfolg = _imagePipeline.KalibriereColor(dialog.FileName);
                StatusText = erfolg
                    ? Lokalisierung.T("Status.FarbkalibrierungErfolgreich")
                    : Lokalisierung.T("Status.FarbkalibrierungFehlgeschlagen");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StyleLutLaden()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"{Lokalisierung.T("Dialog.LUTDateien")}|{Lokalisierung.T("Filter.LUT")}|{Lokalisierung.T("Dialog.AlleDateien")}|*.*",
                Title = Lokalisierung.T("Dialog.LutOeffnen")
            };

            if (dialog.ShowDialog() == true)
            {
                StyleLutPfad = dialog.FileName;
                StyleLutName = Path.GetFileNameWithoutExtension(dialog.FileName);
                StatusText = $"{Lokalisierung.T("Status.FarbstilLutGeladen")}: {StyleLutName}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StyleLutEntfernen()
    {
        StyleLutPfad = null;
        StyleLutName = "";
        StatusText = Lokalisierung.T("Status.FarbstilLutEntfernt");
    }

    // ===== Video Commands =====

    [RelayCommand]
    private void VideoOeffnen()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"{Lokalisierung.T("Dialog.Videodateien")}|{Lokalisierung.T("Filter.Videos")}|{Lokalisierung.T("Dialog.AlleDateien")}|*.*",
                Title = Lokalisierung.T("Dialog.VideoOeffnen")
            };

            if (dialog.ShowDialog() == true)
            {
                if (_videoPipeline.VideoLaden(dialog.FileName))
                {
                    VideoPfad = dialog.FileName;
                    VideoGeladen = true;
                    VideoInfo = $"{_videoPipeline.Breite}x{_videoPipeline.Hoehe}, {_videoPipeline.Fps:F1}fps, {_videoPipeline.Dauer:F1}s";
                    StatusText = $"{Lokalisierung.T("Status.VideoGeladen")}: {Path.GetFileName(dialog.FileName)}";
                }
                else
                {
                    System.Windows.MessageBox.Show(Lokalisierung.T("Status.VideoKonnteNichtGeladenWerden"), Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"{Lokalisierung.T("Status.Fehler")}: {ex.Message}", Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task VideoPipelineAusfuehrenAsync()
    {
        if (!VideoGeladen || VideoPipelineLaeuft) return;

        VideoPipelineLaeuft = true;
        VideoFortschritt = 0;
        StatusText = Lokalisierung.T("Status.VideoPipelineLaeuft");

        var param = new PipelineParams
        {
            Belichtung = Belichtung,
            Kontrast = Kontrast,
            Saettigung = Saettigung,
            Vibranz = Vibranz,
            Lichter = Lichter,
            Schatten = Schatten,
            SchaerfeBetrag = Schaerfe,
            LuminanzRauschen = RauschenLuma / 100f,
            ChrominanzRauschen = RauschenChroma / 100f,
            ObjektivkorrekturAktiv = Objektivkorrektur,
            DistortionGridAktiv = DistortionGridAktiv,
            ColorCalibrationAktiv = ColorCalibrationAktiv,
            Intensitaet = IntensitaetFromIndex(),
            Modus = ModusFromString(),
            HochskalierenFaktor = 1, // Video: kein Upscaling (zu langsam)
            GesichtswiederherstellungAktiv = GesichtswiederherstellungAktiv,
            StyleLutPfad = StyleLutPfad,
            ManuelleKamera = AusgewaehlteKamera,
            ManuellesObjektiv = AusgewaehltesObjektiv
        };

        try
        {
            await Task.Run(() =>
            {
                _videoPipeline.PipelineAusfuehren(param, (aktueller, gesamt) =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        VideoFortschritt = (double)aktueller / gesamt;
                        StatusText = string.Format(Lokalisierung.T("Status.VideoVerarbeitungFrames"), aktueller, gesamt);
                    });
                });
            });

            StatusText = Lokalisierung.T("Status.VideoPipelineAbgeschlossen");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.VideoPipelineFehler")}: {ex.Message}";
            System.Windows.MessageBox.Show($"{Lokalisierung.T("Status.VideoPipelineFehlgeschlagen")}: {ex.Message}", Lokalisierung.T("Status.Fehler"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            VideoPipelineLaeuft = false;
        }
    }

    [RelayCommand]
    private void UpdatePruefen()
    {
        try { _autoUpdater.Pruefen(); }
        catch (Exception ex) { StatusText = $"{Lokalisierung.T("Status.UpdatePruefungFehlgeschlagen")}: {ex.Message}"; }
    }

    [RelayCommand]
    private void UpdateStarten()
    {
        try { _autoUpdater.UpdateStarten(); }
        catch (Exception ex) { StatusText = $"{Lokalisierung.T("Status.UpdateFehlgeschlagen")}: {ex.Message}"; }
    }

    [RelayCommand]
    private void UpdateIgnorieren()
    {
        try { _autoUpdater.Ignorieren(); }
        catch (Exception ex) { StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}"; }
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
            StatusText = $"{Lokalisierung.T("Status.ThemeFehler")}: {ex.Message}";
        }
    }

    /// <summary>
    /// Wechselt die UI-Sprache. Speichert die Einstellung und löst das
    /// SpracheGeaendert-Event aus — MainWindow baut die UI neu auf.
    /// </summary>
    [RelayCommand]
    private void SpracheAendern(int index)
    {
        // Index → Sprachcode Mapping (13 Sprachen, Issue #9)
        var sprachen = new[] { "de", "en", "es", "fr", "it", "nl", "pl", "pt", "tr", "ru", "zh", "ja", "ko" };
        var sprache = index >= 0 && index < sprachen.Length ? sprachen[index] : "de";
        Lokalisierung.SpracheSetzen(sprache);
        SpracheIndex = index;

        var settings = Settings.Laden();
        settings.Sprache = sprache;
        settings.Speichern();

        // UI-Texte aktualisieren — MainWindow lauscht auf dieses Event
        SpracheGeaendert?.Invoke(this, EventArgs.Empty);
    }

    // ===== Video-Backend =====
    // WechselBackend-Command wird NICHT mehr benötigt — RadioButtons binden
    // direkt an VideoBackend (TwoWay). OnVideoBackendChanged übernimmt alles.

    /// <summary>
    /// Installiert VapourSynth + Plugins asynchron.
    /// Aktualisiert die Progress-Bar und den Status-Text während der Installation.
    /// Nach erfolgreicher Installation wird "VapourSynth aktiv" angezeigt.
    /// </summary>
    [RelayCommand]
    private async Task InstalliereVapourSynthAsync()
    {
        if (InstallationLaeuft) return;

        InstallationLaeuft = true;
        InstallationsFortschritt = 0;
        InstallationsText = Lokalisierung.T("Status.Installiere");
        StatusText = Lokalisierung.T("Status.VapourSynthInstalliere");

        // Progress-Event abonnieren
        _vapourSynthInstaller.InstallationsFortschritt += OnVapourSynthInstallationsFortschritt;

        try
        {
            var erfolg = await _vapourSynthInstaller.InstallierenAsync();
            VapourSynthInstalliert = _vapourSynthInstaller.IstInstalliert;

            if (erfolg && VapourSynthInstalliert)
            {
                InstallationsFortschritt = 100;
                InstallationsText = Lokalisierung.T("Status.VapourSynthAktiv");
                StatusText = Lokalisierung.T("Status.VapourSynthAktiv");
            }
            else
            {
                InstallationsText = Lokalisierung.T("Status.VapourSynthInstallationFehlgeschlagen");
                StatusText = Lokalisierung.T("Status.VapourSynthInstallationFehlgeschlagen");
                System.Windows.MessageBox.Show(Lokalisierung.T("Status.VapourSynthInstallationFehlgeschlagen"), Lokalisierung.T("Status.Fehler"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            InstallationsText = $"{Lokalisierung.T("Status.VapourSynthInstallationFehlgeschlagen")}: {ex.Message}";
            StatusText = $"{Lokalisierung.T("Status.VapourSynthInstallationFehlgeschlagen")}: {ex.Message}";
            System.Windows.MessageBox.Show($"{Lokalisierung.T("Status.VapourSynthInstallationFehlgeschlagen")}: {ex.Message}", Lokalisierung.T("Status.Fehler"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _vapourSynthInstaller.InstallationsFortschritt -= OnVapourSynthInstallationsFortschritt;
            InstallationLaeuft = false;
        }
    }

    /// <summary>
    /// Callback für den VapourSynthInstaller-Progress — aktualisiert UI auf dem UI-Thread.
    /// </summary>
    private void OnVapourSynthInstallationsFortschritt(object? sender, VapourSynthInstallFortschritt e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            InstallationsFortschritt = (int)Math.Clamp(e.Prozent, 0, 100);
            InstallationsText = e.Schritt;
        });
    }

    // ===== Clip-Merge Commands =====

    [RelayCommand]
    private void ClipOrdnerOeffnen()
    {
        try
        {
            var ordner = FolderPicker.OpenFolder(Lokalisierung.T("Dialog.ClipOrdner"));

            if (!string.IsNullOrEmpty(ordner))
            {
                ClipOrdner = ordner;
                ClipGruppen = new ObservableCollection<ClipMerger.ClipGruppe>(
                    _clipMerger.ClipsGruppieren(ClipOrdner));
                StatusText = string.Format(Lokalisierung.T("Status.ClipGruppenErkanntIn"), ClipGruppen.Count, ClipOrdner);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.FehlerBeimScannen")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ClipMergeAusfuehrenAsync()
    {
        if (AusgewaehlteGruppe == null || ClipMergeLaeuft) return;

        ClipMergeLaeuft = true;
        StatusText = $"{Lokalisierung.T("Status.ClipsZusammenfuegen")} ({AusgewaehlteGruppe.ClipAnzahl})";

        var fortschritt = new Progress<double>(p => ClipMergeFortschritt = p);

        try
        {
            var ausgabeOrdner = Path.Combine(ClipOrdner, "FlipsiColor_Merged");

            if (ClipMergeFarbkorrekturAktiv)
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
                    LuminanzRauschen = RauschenLuma / 100f,
                    ChrominanzRauschen = RauschenChroma / 100f,
                    ObjektivkorrekturAktiv = Objektivkorrektur,
                    Intensitaet = IntensitaetFromIndex(),
                    Modus = ModusFromString()
                };

                var ergebnis = await _clipMerger.ClipsZusammenfuegenMitFarbkorrekturAsync(
                    AusgewaehlteGruppe, ausgabeOrdner, _modelManager, _colorManager, param, fortschritt);

                if (ergebnis != null)
                    StatusText = string.Format(Lokalisierung.T("Status.FertigFarbkorrektur"), Path.GetFileName(ergebnis));
                else
                    StatusText = Lokalisierung.T("Status.ZusammenfuegenMitFarbkorrekturFehlgeschlagen");
            }
            else
            {
                // Nur Zusammenfügen
                var ergebnis = await _clipMerger.ClipsZusammenfuegenAsync(
                    AusgewaehlteGruppe, ausgabeOrdner, fortschritt);

                if (ergebnis != null)
                    StatusText = string.Format(Lokalisierung.T("Status.FertigOhneFarbkorrektur"), Path.GetFileName(ergebnis));
                else
                    StatusText = Lokalisierung.T("Status.ZusammenfuegenFehlgeschlagen");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.ClipMergeFehler")}: {ex.Message}";
        }
        finally
        {
            ClipMergeLaeuft = false;
        }
    }

    [RelayCommand]
    private async Task ClipAlleMergenAsync()
    {
        if (ClipGruppen.Count == 0 || ClipMergeLaeuft) return;

        ClipMergeLaeuft = true;
        var erledigt = 0;
        var gesamt = ClipGruppen.Count;
        StatusText = string.Format(Lokalisierung.T("Status.VerarbeiteGruppen"), gesamt);

        try
        {
            var ausgabeOrdner = Path.Combine(ClipOrdner, "FlipsiColor_Merged");
            var param = new PipelineParams
            {
                Belichtung = Belichtung,
                Kontrast = Kontrast,
                Saettigung = Saettigung,
                Vibranz = Vibranz,
                Lichter = Lichter,
                Schatten = Schatten,
                SchaerfeBetrag = Schaerfe,
                LuminanzRauschen = RauschenLuma / 100f,
                ChrominanzRauschen = RauschenChroma / 100f,
                ObjektivkorrekturAktiv = Objektivkorrektur,
                Intensitaet = IntensitaetFromIndex(),
                Modus = ModusFromString()
            };

            foreach (var gruppe in ClipGruppen)
            {
                if (ClipMergeFarbkorrekturAktiv)
                {
                    var fortschritt = new Progress<double>(p =>
                        ClipMergeFortschritt = (erledigt + p) / gesamt);
                    await _clipMerger.ClipsZusammenfuegenMitFarbkorrekturAsync(
                        gruppe, ausgabeOrdner, _modelManager, _colorManager, param, fortschritt);
                }
                else
                {
                    var fortschritt = new Progress<double>(p =>
                        ClipMergeFortschritt = (erledigt + p) / gesamt);
                    await _clipMerger.ClipsZusammenfuegenAsync(
                        gruppe, ausgabeOrdner, fortschritt);
                }

                erledigt++;
                ClipMergeFortschritt = (double)erledigt / gesamt;
            }

            StatusText = $"{Lokalisierung.T("Status.AlleGruppenVerarbeitet")} ({ausgabeOrdner})";
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.FehlerBeimBulkMerge")}: {ex.Message}";
        }
        finally
        {
            ClipMergeLaeuft = false;
        }
    }

    public void Dispose()
    {
        _imagePipeline.Dispose();
        _videoPipeline.Dispose();
        _modelManager.Dispose();
        _autoUpdater.Dispose();
        _clipMerger.Dispose();
    }
}

/// <summary>
/// Ein Eintrag in der Drag &amp; Drop Dateiliste.
/// </summary>
public sealed class DateiEintrag : System.ComponentModel.INotifyPropertyChanged
{
    /// <summary>Vollständiger Dateipfad.</summary>
    public string Pfad { get; }

    /// <summary>Dateiname ohne Pfad.</summary>
    public string Dateiname => Path.GetFileName(Pfad);

    /// <summary>Dateityp (Bild oder Video).</summary>
    public string Typ { get; }

    /// <summary>True wenn es sich um eine Bilddatei handelt.</summary>
    public bool IstBild { get; }

    /// <summary>True wenn es sich um eine Videodatei handelt.</summary>
    public bool IstVideo { get; }

    /// <summary>Dateigröße als formatierter String.</summary>
    public string Groesse { get; }

    private System.Windows.Media.Imaging.BitmapSource? _thumbnail;
    /// <summary>Live-Bildvorschau (Thumbnail) für die Galerie.</summary>
    public System.Windows.Media.Imaging.BitmapSource? Thumbnail
    {
        get => _thumbnail;
        private set
        {
            _thumbnail = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Thumbnail)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Erstellt einen neuen DateiEintrag aus einem Dateipfad.
    /// Lädt asynchron ein Thumbnail für Bilddateien.
    /// </summary>
    /// <param name="pfad">Vollständiger Pfad zur Datei.</param>
    public DateiEintrag(string pfad)
    {
        Pfad = pfad;
        var ext = Path.GetExtension(pfad);
        IstBild = MainViewModel.BildEndungen.Contains(ext);
        IstVideo = MainViewModel.VideoEndungen.Contains(ext);
        Typ = IstBild ? Lokalisierung.T("Toolbar.Bild") :
              IstVideo ? Lokalisierung.T("Toolbar.Video") : "?";

        try
        {
            var bytes = new FileInfo(pfad).Length;
            Groesse = bytes switch
            {
                < 1024 => $"{bytes} B",
                < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
                < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
                _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
            };
        }
        catch
        {
            Groesse = "—";
        }

        // Thumbnail asynchron laden (nur bei Bildern)
        if (IstBild)
        {
            _ = LadeThumbnailAsync();
        }
    }

    /// <summary>
    /// Lädt ein kleines Thumbnail-Bild asynchron aus der Datei.
    /// Respektiert EXIF-Orientierung damit Hochformat-Bilder nicht quer gedreht werden.
    /// </summary>
    private async Task LadeThumbnailAsync()
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                // EXIF-Orientierung lesen
                var rotation = System.Windows.Media.Imaging.Rotation.Rotate0;
                try
                {
                    using var stream = new FileStream(Pfad, FileMode.Open, FileAccess.Read);
                    var frame = System.Windows.Media.Imaging.BitmapFrame.Create(
                        stream,
                        System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                        System.Windows.Media.Imaging.BitmapCacheOption.None);
                    var metadata = frame.Metadata as System.Windows.Media.Imaging.BitmapMetadata;
                    if (metadata != null && metadata.ContainsQuery("System.Photo.Orientation"))
                    {
                        var orient = metadata.GetQuery("System.Photo.Orientation");
                        if (orient is ushort u)
                        {
                            rotation = u switch
                            {
                                3 => System.Windows.Media.Imaging.Rotation.Rotate180,
                                6 => System.Windows.Media.Imaging.Rotation.Rotate90,
                                8 => System.Windows.Media.Imaging.Rotation.Rotate270,
                                _ => System.Windows.Media.Imaging.Rotation.Rotate0
                            };
                        }
                    }
                }
                catch { /* Metadata nicht lesbar — kein Rotation */ }

                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(Pfad);
                bitmap.DecodePixelWidth = 400; // Höhere Auflösung für scharfe Thumbnails
                bitmap.Rotation = rotation;
                bitmap.EndInit();
                bitmap.Freeze();
                Thumbnail = bitmap;
            }
            catch
            {
                // Thumbnail konnte nicht geladen werden — Dateiname wird angezeigt
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }
}