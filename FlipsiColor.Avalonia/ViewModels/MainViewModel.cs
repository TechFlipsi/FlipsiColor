using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;

using FlipsiColor.AI;
using FlipsiColor.Color;
using FlipsiColor.Core;
using FlipsiColor.Image;
using FlipsiColor.Video;

using VideoBackend = FlipsiColor.Core.VideoBackend;
using BatchJob = FlipsiColor.Image.BatchJob;

namespace FlipsiColor.ViewModels;

/// <summary>
/// Main ViewModel — steuert die komplette App (Avalonia UI Version).
/// Drag &amp; Drop: mehrere Dateien gleichzeitig (Bilder UND Videos), Datei-Liste im UI.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ModelManager _modelManager;
    private readonly ColorManager _colorManager;
    private readonly ImagePipeline _imagePipeline;
    private readonly VideoPipeline _videoPipeline;
    private readonly AutoUpdater _autoUpdater;
    private readonly ClipMerger _clipMerger;

    [ObservableProperty] private string _title = "FlipsiColor v0.7.0";
    [ObservableProperty] private bool _gpuVerfuegbar;
    [ObservableProperty] private string _gpuName = "";
    [ObservableProperty] private bool _updateVerfuegbar;
    [ObservableProperty] private string _neueVersion = "";
    [ObservableProperty] private string _aktuellerModus = "Fragen";
    [ObservableProperty] private int _modusIndex;
    [ObservableProperty] private bool _bildGeladen;
    [ObservableProperty] private string _bildPfad = "";
    [ObservableProperty] private string _statusText = Lokalisierung.T("App.Bereit");
    [ObservableProperty] private bool _pipelineLaeuft;
    [ObservableProperty] private Bitmap? _pipelineBild;

    // Video-Status
    [ObservableProperty] private bool _videoGeladen;
    [ObservableProperty] private string _videoPfad = "";
    [ObservableProperty] private bool _videoPipelineLaeuft;
    [ObservableProperty] private double _videoFortschritt;
    [ObservableProperty] private string _videoInfo = "";

    // Clip-Merge (allgemein, für alle Kameras)
    [ObservableProperty] private ObservableCollection<ClipMerger.ClipGruppe> _clipGruppen = [];
    [ObservableProperty] private bool _clipMergeAktiv;
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
    [ObservableProperty] private bool _distortionGridAktiv;
    [ObservableProperty] private bool _colorCalibrationAktiv;
    [ObservableProperty] private int _intensitaetIndex = 1; // Mittel

    // Upscaling & Gesichtswiederherstellung
    [ObservableProperty] private int _hochskalierenFaktor = 1;
    [ObservableProperty] private bool _gesichtswiederherstellungAktiv;

    // StyleLUT → Farbstil-LUT
    [ObservableProperty] private string? _styleLutPfad;
    [ObservableProperty] private string _styleLutName = "";

    // Theme → Design
    [ObservableProperty] private string _aktuellesTheme = "System";

    // Einstellungen
    [ObservableProperty] private int _spracheIndex; // 0=Deutsch, 1=Englisch
    [ObservableProperty] private int _designIndex; // 0=Dunkel, 1=Hell, 2=System
    [ObservableProperty] private bool _autoUpdateAktiv = true;
    [ObservableProperty] private string _modellVerzeichnis = "";

    // Video-Backend (FFmpeg / VapourSynth) — Einstellungen
    [ObservableProperty] private VideoBackend _videoBackend = VideoBackend.FFmpeg;
    [ObservableProperty] private bool _vapourSynthInstalliert;
    [ObservableProperty] private int _installationsFortschritt;
    [ObservableProperty] private string _installationsText = "";
    [ObservableProperty] private bool _installationLaeuft;
    [ObservableProperty] private bool _vapourSynthInstallUiSichtbar;
    [ObservableProperty] private bool _vapourSynthAktivSichtbar;

    // ===== Issue #12: Before/After Slider =====
    [ObservableProperty] private bool _beforeAfterAktiv;
    [ObservableProperty] private double _beforeAfterPosition = 0.5;

    // ===== Issue #13: Presets / Profile =====
    [ObservableProperty] private ObservableCollection<string> _presetNamen = [];
    [ObservableProperty] private string? _ausgewaehltesPreset;
    private readonly PresetManager _presetManager = new();

    // ===== Issue #14: Histogramm =====
    [ObservableProperty] private float[] _histogrammRot = new float[256];
    [ObservableProperty] private float[] _histogrammGruen = new float[256];
    [ObservableProperty] private float[] _histogrammBlau = new float[256];
    [ObservableProperty] private float[] _histogrammLuminanz = new float[256];
    [ObservableProperty] private float _histogrammMaxWert = 1;

    // ===== Issue #15: Batch-Verarbeitung =====
    [ObservableProperty] private ObservableCollection<BatchJob> _batchJobs = [];
    [ObservableProperty] private bool _batchLaeuft;
    [ObservableProperty] private double _batchFortschritt;
    [ObservableProperty] private string _batchStatus = "";
    [ObservableProperty] private string _batchZielOrdner = "";
    private readonly BatchProcessor _batchProcessor = new();

    // ===== Issue #16: Undo/Redo =====
    [ObservableProperty] private bool _kannUndo;
    [ObservableProperty] private bool _kannRedo;
    [ObservableProperty] private ObservableCollection<string> _undoHistory = [];
    private readonly UndoManager _undoManager = new();

    // ===== Issue #17: Crop & Straighten =====
    [ObservableProperty] private bool _cropModusAktiv;
    [ObservableProperty] private int _cropX;
    [ObservableProperty] private int _cropY;
    [ObservableProperty] private int _cropBreite;
    [ObservableProperty] private int _cropHoehe;
    [ObservableProperty] private double _cropRotation;
    [ObservableProperty] private int _cropAspectRatioIndex;

    // ===== Issue #18: Plugin-System =====
    [ObservableProperty] private ObservableCollection<PluginInfo> _pluginListe = [];
    [ObservableProperty] private string _pluginStatus = "";
    private readonly PluginManager _pluginManager = new();

    // ── Pro-Funktions KI-Toggles (v0.5.0) ──
    [ObservableProperty] private bool _kIDenoisingAktiv = true;
    [ObservableProperty] private bool _kISchaerfungAktiv = true;
    [ObservableProperty] private bool _kIUpscalingAktiv = true;
    [ObservableProperty] private bool _kIGesichtswiederherstellungAktiv = true;
    [ObservableProperty] private bool _kIFarbstilAktiv = true;
    [ObservableProperty] private bool _kISzenenklassifizierungAktiv = true;

    // ── OpenColorIO (v0.5.0) ──
    [ObservableProperty] private int _colorManagementIndex; // 0=Standard, 1=OpenColorIO
    [ObservableProperty] private string? _oCIOConfigPfad;
    [ObservableProperty] private string _oCIOConfigName = "";
    [ObservableProperty] private string? _oCIOSourceColorSpace = "ACEScg";
    [ObservableProperty] private string? _oCIODisplay = "sRGB";
    [ObservableProperty] private string? _oCIOView = "Filmic";
    [ObservableProperty] private string? _oCIOLook;
    [ObservableProperty] private int _oCIOEngineIndex; // 0=LUTBaking, 1=Native
    [ObservableProperty] private bool _oCIOPanelSichtbar; // true wenn ColorManagementIndex == 1
    [ObservableProperty] private ObservableCollection<string> _oCIOColorSpaces = [];
    [ObservableProperty] private ObservableCollection<string> _oCIODisplays = [];
    [ObservableProperty] private ObservableCollection<string> _oCIOViews = [];
    [ObservableProperty] private ObservableCollection<string> _oCIOLooks = [];

    /// <summary>
    /// Wird aufgerufen wenn ColorManagementIndex sich ändert (v0.5.0).
    /// Steuert die Sichtbarkeit des OCIO-Panels.
    /// </summary>
    partial void OnColorManagementIndexChanged(int value)
    {
        OCIOPanelSichtbar = value == 1;
    }

    /// <summary>
    /// Lädt eine OCIO Config-Datei und befüllt die Dropdown-Listen (v0.5.0).
    /// </summary>
    [RelayCommand]
    private void OCIOConfigLaden()
    {
        try
        {
            // Default-Config erstellen falls keine gewählt
            if (string.IsNullOrEmpty(OCIOConfigPfad))
            {
                var defaultPfad = Color.OCIOManager.DefaultConfigErstellen();
                if (defaultPfad == null)
                {
                    StatusText = "OCIO: Default-Config konnte nicht erstellt werden";
                    return;
                }
                OCIOConfigPfad = defaultPfad;
            }

            var parser = new Color.OCIOConfigParser();
            var daten = parser.Laden(OCIOConfigPfad!);
            if (daten == null)
            {
                StatusText = "OCIO: Config konnte nicht geladen werden";
                return;
            }

            OCIOConfigName = Path.GetFileName(OCIOConfigPfad);
            OCIOColorSpaces.Clear();
            foreach (var (name, _, _) in daten.ColorSpaces)
                OCIOColorSpaces.Add(name);

            OCIODisplays.Clear();
            foreach (var d in daten.Displays)
                OCIODisplays.Add(d);

            OCIOViews.Clear();
            if (!string.IsNullOrEmpty(OCIODisplay) && daten.Views.TryGetValue(OCIODisplay!, out var views))
                foreach (var v in views) OCIOViews.Add(v);

            OCIOLooks.Clear();
            OCIOLooks.Add(""); // Kein Look
            foreach (var (name, _) in daten.Looks)
                OCIOLooks.Add(name);

            StatusText = $"OCIO Config geladen: {daten.ColorSpaces.Count} Color Spaces, {daten.Displays.Count} Displays";
        }
        catch (Exception ex)
        {
            StatusText = $"OCIO Fehler: {ex.Message}";
        }
    }
    private readonly VapourSynthInstaller _vapourSynthInstaller = new();

    /// <summary>Wird aufgerufen wenn VideoBackend sich ändert — aktualisiert Sichtbarkeit.</summary>
    partial void OnVideoBackendChanged(VideoBackend value) => UpdateVapourSynthSichtbarkeit();

    /// <summary>Wird aufgerufen wenn VapourSynthInstalliert sich ändert — aktualisiert Sichtbarkeit.</summary>
    partial void OnVapourSynthInstalliertChanged(bool value) => UpdateVapourSynthSichtbarkeit();

    /// <summary>Aktualisiert die berechneten Sichtbarkeits-Properties für die Installations-UI.</summary>
    private void UpdateVapourSynthSichtbarkeit()
    {
        VapourSynthInstallUiSichtbar = VideoBackend == VideoBackend.VapourSynth && !VapourSynthInstalliert;
        VapourSynthAktivSichtbar = VideoBackend == VideoBackend.VapourSynth && VapourSynthInstalliert;
    }

    // Datei-Liste (Drag & Drop — mehrere Dateien)
    [ObservableProperty] private ObservableCollection<DateiEintrag> _dateiListe = [];

    // Callbacks für Dialoge (von Code-Behind gesetzt, da Avalonia StorageProvider)
    public Func<string, string, string?, Task<string?>>? DateiOeffnenCallback { get; set; }
    public Func<string, Task<string?>>? OrdnerOeffnenCallback { get; set; }
    public Action<string>? MeldungAnzeigenCallback { get; set; }
    public Action<string>? FehlerAnzeigenCallback { get; set; }

    // Sprach-Update Event
    public event EventHandler? SpracheGeaendert;

    public MainViewModel()
    {
        _modelManager = new ModelManager();
        _colorManager = new ColorManager();
        _imagePipeline = new ImagePipeline(_modelManager, _colorManager);
        _videoPipeline = new VideoPipeline(_modelManager, _colorManager);
        _autoUpdater = new AutoUpdater();
        _clipMerger = new ClipMerger();

        GPUInfo.Erkennen();
        GpuVerfuegbar = GPUInfo.GpuVerfuegbar;
        GpuName = GPUInfo.GpuName;

        _colorManager.Initialisieren();

        // Settings laden
        try
        {
            var settings = Settings.Laden();
            AktuellesTheme = settings.Theme;
            var effektiveSprache = string.IsNullOrEmpty(settings.Sprache) ? Lokalisierung.Sprache : settings.Sprache;
            var sprachenListe = new[] { "de", "en", "es", "fr", "it", "nl", "pl", "pt", "tr", "ru", "zh", "ja", "ko" };
            SpracheIndex = Array.IndexOf(sprachenListe, effektiveSprache);
            if (SpracheIndex < 0) SpracheIndex = 0;
            DesignIndex = settings.Theme switch { "Dark" => 0, "Light" => 1, _ => 2 };
            AutoUpdateAktiv = settings.AutoUpdatePruefen;
            ModellVerzeichnis = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipsiColor", "Models");
            Lokalisierung.SpracheSetzen(settings.Sprache);
            Lokalisierung.SpracheGeaendert += (_, _) => SpracheGeaendert?.Invoke(this, EventArgs.Empty);

            // Video-Backend aus Settings laden
            VideoBackend = settings.VideoBackend;
            VapourSynthInstalliert = _vapourSynthInstaller.IstInstalliert;
        }
        catch
        {
            AktuellesTheme = "Dark";
            SpracheIndex = 0;
            DesignIndex = 0;
        }

        // Auto-Updater Events — Dispatcher.UIThread.Post statt Dispatcher.Invoke
        // Pitfall: Dispatcher.Invoke() → Dispatcher.UIThread.Post() in Avalonia
        _autoUpdater.UpdateVerfuegbarChanged += (_, verfuegbar) =>
            Dispatcher.UIThread.Post(() => UpdateVerfuegbar = verfuegbar);
        _autoUpdater.NeueVersionChanged += (_, version) =>
            Dispatcher.UIThread.Post(() => NeueVersion = version);

        // ===== Issue #13: Presets initialisieren =====
        _presetManager.Initialisieren();
        PresetNamenLaden();

        // ===== Issue #14: Histogramm-Event abonnieren =====
        _imagePipeline.HistogrammAktualisiert += OnHistogrammAktualisiert;

        // ===== Issue #18: Plugins initialisieren =====
        _pluginManager.Initialisieren();
        PluginListeLaden();
    }

    /// <summary>IntensitaetIndex (0/1/2) in den Intensitaet-Enum.</summary>
    private Intensitaet IntensitaetFromIndex() => IntensitaetIndex switch
    {
        0 => Intensitaet.Leicht,
        2 => Intensitaet.Stark,
        _ => Intensitaet.Mittel
    };

    /// <summary>ModusIndex (0/1/2) in den BetriebsModus-Enum (deutsche Begriffe).</summary>
    private BetriebsModus ModusFromIndex() => ModusIndex switch
    {
        1 => BetriebsModus.SmartLearn,
        2 => BetriebsModus.Turbo,
        _ => BetriebsModus.Ask
    };

    // ===== Drag & Drop — mehrere Dateien (Bilder UND Videos) =====

    /// <summary>Fügt mehrere Dateien zur Datei-Liste hinzu (Drag &amp; Drop).</summary>
    public void DateienHinzufuegen(string[] dateien)
    {
        foreach (var datei in dateien)
        {
            if (!File.Exists(datei)) continue;
            var ext = Path.GetExtension(datei).ToLowerInvariant();
            if (!IstBildDatei(ext) && !IstVideoDatei(ext)) continue;

            // Duplikate vermeiden
            if (DateiListe.Any(d => d.Pfad == datei)) continue;

            DateiListe.Add(new DateiEintrag
            {
                Pfad = datei,
                Dateiname = Path.GetFileName(datei),
                IstBild = IstBildDatei(ext),
                IstVideo = IstVideoDatei(ext)
            });
        }

        // Erste Bilddatei automatisch laden
        var ersteBild = DateiListe.FirstOrDefault(d => d.IstBild);
        if (ersteBild != null && !BildGeladen)
        {
            LoadBild(ersteBild.Pfad);
        }

        StatusText = string.Format(Lokalisierung.T("Galerie.DateiZaehler"), DateiListe.Count);
    }

    private static bool IstBildDatei(string ext) => ext is ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff"
        or ".bmp" or ".cr2" or ".cr3" or ".nef" or ".arw" or ".dng" or ".orf" or ".rw2";

    private static bool IstVideoDatei(string ext) => ext is ".mp4" or ".mov" or ".avi" or ".mkv"
        or ".m4v" or ".wmv" or ".flv" or ".mxf" or ".mts" or ".m2ts";

    [RelayCommand]
    private void DateiEntfernen(DateiEintrag eintrag)
    {
        DateiListe.Remove(eintrag);
        StatusText = string.Format(Lokalisierung.T("Status.DateiEntfernt"), DateiListe.Count);
    }

    [RelayCommand]
    private void AlleDateienLeeren()
    {
        DateiListe.Clear();
        BildGeladen = false;
        PipelineBild = null;
        StatusText = Lokalisierung.T("Status.DateiListeGeleert");
    }

    /// <summary>Lädt ein Bild in die Pipeline (öffentlich für Drag &amp; Drop).</summary>
    public bool LoadBild(string pfad)
    {
        try
        {
            if (_imagePipeline.BildLaden(pfad))
            {
                BildPfad = pfad;
                BildGeladen = true;
                PipelineBild = null;
                StatusText = $"{Lokalisierung.T("Status.Geladen")}: {Path.GetFileName(pfad)}";
                return true;
            }
            else
            {
                FehlerAnzeigenCallback?.Invoke(Lokalisierung.T("Status.BildKonnteNichtGeladenWerden"));
                return false;
            }
        }
        catch (Exception ex)
        {
            FehlerAnzeigenCallback?.Invoke($"{Lokalisierung.T("Status.Fehler")}: {ex.Message}");
            return false;
        }
    }

    [RelayCommand]
    private async Task BildOeffnenAsync()
    {
        if (DateiOeffnenCallback == null) return;
        var filter = Lokalisierung.T("Filter.Bilder");
        var titel = Lokalisierung.T("Dialog.BildOeffnen");
        var pfad = await DateiOeffnenCallback(titel, filter, null);
        if (!string.IsNullOrEmpty(pfad))
            LoadBild(pfad);
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
            LuminanzRauschen = RauschenLuma / 100f,
            ChrominanzRauschen = RauschenChroma / 100f,
            ObjektivkorrekturAktiv = Objektivkorrektur,
            DistortionGridAktiv = DistortionGridAktiv,
            ColorCalibrationAktiv = ColorCalibrationAktiv,
            Intensitaet = IntensitaetFromIndex(),
            Modus = ModusFromIndex(),
            HochskalierenFaktor = HochskalierenFaktor,
            GesichtswiederherstellungAktiv = GesichtswiederherstellungAktiv,
            StyleLutPfad = StyleLutPfad,
            // v0.5.0: Pro-Funktions KI-Toggles
            KIDenoisingAktiv = KIDenoisingAktiv,
            KISchaerfungAktiv = KISchaerfungAktiv,
            KIUpscalingAktiv = KIUpscalingAktiv,
            KIGesichtswiederherstellungAktiv = KIGesichtswiederherstellungAktiv,
            KIFarbstilAktiv = KIFarbstilAktiv,
            KISzenenklassifizierungAktiv = KISzenenklassifizierungAktiv,
            // v0.5.0: OpenColorIO
            ColorManagement = ColorManagementIndex == 1 ? ColorManagementMode.OpenColorIO : ColorManagementMode.Standard,
            OCIOConfigPfad = OCIOConfigPfad,
            OCIOSourceColorSpace = OCIOSourceColorSpace,
            OCIODisplay = OCIODisplay,
            OCIOView = OCIOView,
            OCIOLook = OCIOLook,
            OCIOEngineMode = OCIOEngineIndex == 1 ? OCIOEngine.Native : OCIOEngine.LUTBaking
        };

        try
        {
            await Task.Run(() => _imagePipeline.PipelineAusfuehren(param));

            // Mat → Avalonia Bitmap auf UI-Thread
            var mat = _imagePipeline.Ergebnis;
            if (mat != null && !mat.Empty())
            {
                PipelineBild = MatToBitmapConverter.ConvertMat(mat);
            }
            StatusText = Lokalisierung.T("Status.PipelineAbgeschlossen");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.PipelineFehler")}: {ex.Message}";
            FehlerAnzeigenCallback?.Invoke($"{Lokalisierung.T("Status.PipelineFehlgeschlagen")}: {ex.Message}");
        }
        finally
        {
            PipelineLaeuft = false;
        }
    }

    [RelayCommand]
    private void Zuruecksetzen()
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
        StatusText = Lokalisierung.T("Status.ParameterZurueckgesetzt");
    }

    [RelayCommand]
    private async Task DistortionGridKalibrierenAsync()
    {
        if (DateiOeffnenCallback == null) return;
        var titel = Lokalisierung.T("Dialog.SchachbrettOeffnen");
        var filter = "*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp";
        var pfad = await DateiOeffnenCallback(titel, filter, null);
        if (string.IsNullOrEmpty(pfad)) return;

        try
        {
            var erfolg = _imagePipeline.KalibriereDistortionGrid(pfad);
            StatusText = erfolg
                ? Lokalisierung.T("Status.DistortionGridErfolgreich")
                : Lokalisierung.T("Status.DistortionGridFehlgeschlagen");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ColorCalibrationKalibrierenAsync()
    {
        if (DateiOeffnenCallback == null) return;
        var titel = Lokalisierung.T("Dialog.ColorCheckerOeffnen");
        var filter = "*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp";
        var pfad = await DateiOeffnenCallback(titel, filter, null);
        if (string.IsNullOrEmpty(pfad)) return;

        try
        {
            var erfolg = _imagePipeline.KalibriereColor(pfad);
            StatusText = erfolg
                ? Lokalisierung.T("Status.FarbkalibrierungErfolgreich")
                : Lokalisierung.T("Status.FarbkalibrierungFehlgeschlagen");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StyleLutLadenAsync()
    {
        if (DateiOeffnenCallback == null) return;
        var titel = Lokalisierung.T("Dialog.LutOeffnen");
        var filter = Lokalisierung.T("Filter.LUT");
        var pfad = await DateiOeffnenCallback(titel, filter, null);
        if (string.IsNullOrEmpty(pfad)) return;

        StyleLutPfad = pfad;
        StyleLutName = Path.GetFileNameWithoutExtension(pfad);
        StatusText = $"{Lokalisierung.T("Status.FarbstilLutGeladen")}: {StyleLutName}";
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
    private async Task VideoOeffnenAsync()
    {
        if (DateiOeffnenCallback == null) return;
        var filter = Lokalisierung.T("Filter.Videos");
        var titel = Lokalisierung.T("Dialog.VideoOeffnen");
        var pfad = await DateiOeffnenCallback(titel, filter, null);
        if (string.IsNullOrEmpty(pfad)) return;

        if (_videoPipeline.VideoLaden(pfad))
        {
            VideoPfad = pfad;
            VideoGeladen = true;
            VideoInfo = $"{_videoPipeline.Breite}x{_videoPipeline.Hoehe}, {_videoPipeline.Fps:F1}fps, {_videoPipeline.Dauer:F1}s";
            StatusText = $"{Lokalisierung.T("Status.VideoGeladen")}: {Path.GetFileName(pfad)}";
        }
        else
        {
            FehlerAnzeigenCallback?.Invoke(Lokalisierung.T("Status.VideoKonnteNichtGeladenWerden"));
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
            Modus = ModusFromIndex(),
            HochskalierenFaktor = 1,
            GesichtswiederherstellungAktiv = GesichtswiederherstellungAktiv,
            StyleLutPfad = StyleLutPfad,
            // v0.5.0: Pro-Funktions KI-Toggles
            KIDenoisingAktiv = KIDenoisingAktiv,
            KISchaerfungAktiv = KISchaerfungAktiv,
            KIUpscalingAktiv = KIUpscalingAktiv,
            KIGesichtswiederherstellungAktiv = KIGesichtswiederherstellungAktiv,
            KIFarbstilAktiv = KIFarbstilAktiv,
            KISzenenklassifizierungAktiv = KISzenenklassifizierungAktiv,
            // v0.5.0: OpenColorIO
            ColorManagement = ColorManagementIndex == 1 ? ColorManagementMode.OpenColorIO : ColorManagementMode.Standard,
            OCIOConfigPfad = OCIOConfigPfad,
            OCIOSourceColorSpace = OCIOSourceColorSpace,
            OCIODisplay = OCIODisplay,
            OCIOView = OCIOView,
            OCIOLook = OCIOLook,
            OCIOEngineMode = OCIOEngineIndex == 1 ? OCIOEngine.Native : OCIOEngine.LUTBaking
        };

        try
        {
            await Task.Run(() =>
            {
                _videoPipeline.PipelineAusfuehren(param, (aktueller, gesamt) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        VideoFortschritt = (double)aktueller / gesamt;
                        StatusText = $"{Lokalisierung.T("Status.VideoVerarbeitung")}: {aktueller}/{gesamt} Frames";
                    });
                });
            });

            StatusText = Lokalisierung.T("Status.VideoPipelineAbgeschlossen");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.VideoPipelineFehler")}: {ex.Message}";
            FehlerAnzeigenCallback?.Invoke($"{Lokalisierung.T("Status.VideoPipelineFehlgeschlagen")}: {ex.Message}");
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
    private void DesignWechseln(string theme)
    {
        try
        {
            AktuellesTheme = theme;
            ThemeManager.ApplyTheme(theme);
            DesignIndex = theme switch { "Dark" => 0, "Light" => 1, _ => 2 };

            var settings = Settings.Laden();
            settings.Theme = theme;
            settings.Speichern();
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.ThemeFehler")}: {ex.Message}";
        }
    }

    // ===== Einstellungen =====

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

        // UI-Texte aktualisieren
        SpracheGeaendert?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AutoUpdateAendern(bool aktiv)
    {
        AutoUpdateAktiv = aktiv;
        var settings = Settings.Laden();
        settings.AutoUpdatePruefen = aktiv;
        settings.Speichern();
    }

    // ===== Video-Backend Commands =====

    /// <summary>
    /// Wechselt das Video-Backend zwischen FFmpeg und VapourSynth.
    /// Speichert die Einstellung sofort. Bei VapourSynth-Auswahl wird geprüft,
    /// ob VapourSynth installiert ist — falls nicht, wird die Installations-UI angezeigt.
    /// FFmpeg ist sofort aktiv, kein Neustart erforderlich.
    /// </summary>
    [RelayCommand]
    private void WechselBackend(VideoBackend backend)
    {
        VideoBackend = backend;

        // Einstellung speichern
        try
        {
            var settings = Settings.Laden();
            settings.VideoBackend = backend;
            settings.Speichern();
            StatusText = Lokalisierung.T("Video.Backend.Gespeichert");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }

        // Bei VapourSynth: Installations-Status prüfen
        if (backend == VideoBackend.VapourSynth)
        {
            VapourSynthInstalliert = _vapourSynthInstaller.IstInstalliert;
            if (VapourSynthInstalliert)
            {
                InstallationsText = Lokalisierung.T("Video.Backend.Aktiv");
            }
        }
    }

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
        InstallationsText = Lokalisierung.T("Video.Backend.Installiere");
        StatusText = Lokalisierung.T("Video.Backend.Installiere");

        // Progress-Event abonnieren
        _vapourSynthInstaller.InstallationsFortschritt += OnVapourSynthInstallationsFortschritt;

        try
        {
            var erfolg = await _vapourSynthInstaller.InstallierenAsync();
            VapourSynthInstalliert = _vapourSynthInstaller.IstInstalliert;

            if (erfolg && VapourSynthInstalliert)
            {
                InstallationsFortschritt = 100;
                InstallationsText = Lokalisierung.T("Video.Backend.Aktiv");
                StatusText = Lokalisierung.T("Video.Backend.Aktiv");
            }
            else
            {
                InstallationsText = Lokalisierung.T("Video.Backend.InstallationFehlgeschlagen");
                StatusText = Lokalisierung.T("Video.Backend.InstallationFehlgeschlagen");
                FehlerAnzeigenCallback?.Invoke(Lokalisierung.T("Video.Backend.InstallationFehlgeschlagen"));
            }
        }
        catch (Exception ex)
        {
            InstallationsText = $"{Lokalisierung.T("Video.Backend.InstallationFehlgeschlagen")}: {ex.Message}";
            StatusText = $"{Lokalisierung.T("Video.Backend.InstallationFehlgeschlagen")}: {ex.Message}";
            FehlerAnzeigenCallback?.Invoke($"{Lokalisierung.T("Video.Backend.InstallationFehlgeschlagen")}: {ex.Message}");
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
        Dispatcher.UIThread.Post(() =>
        {
            InstallationsFortschritt = (int)Math.Clamp(e.Prozent, 0, 100);
            InstallationsText = e.Schritt;
        });
    }

    [RelayCommand]
    private async Task ModelleNeuHerunterladenAsync()
    {
        StatusText = Lokalisierung.T("Status.ModellNeuHerunterladen");
        try
        {
            // Modell-Verzeichnis leeren und neu herunterladen
            var modelDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipsiColor", "Models");
            if (Directory.Exists(modelDir))
            {
                foreach (var f in Directory.GetFiles(modelDir, "*.onnx"))
                    File.Delete(f);
            }

            await _modelManager.ModellSicherstellenAsync(ModellId.NAFNet);
            await _modelManager.ModellSicherstellenAsync(ModellId.RestormerLight);
            await _modelManager.ModellSicherstellenAsync(ModellId.AiLUTTransform);
            await _modelManager.ModellSicherstellenAsync(ModellId.EfficientNet);

            StatusText = Lokalisierung.T("Status.ModelleHeruntergeladen");
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
    }

    // ===== Clip-Merge Commands (allgemein, für alle Kameras) =====

    [RelayCommand]
    private async Task ClipOrdnerOeffnenAsync()
    {
        if (OrdnerOeffnenCallback == null) return;
        var titel = Lokalisierung.T("Dialog.ClipOrdner");
        var ordner = await OrdnerOeffnenCallback(titel);
        if (string.IsNullOrEmpty(ordner)) return;

        ClipOrdner = ordner;
        ClipGruppen = new ObservableCollection<ClipMerger.ClipGruppe>(
            _clipMerger.ClipsGruppieren(ClipOrdner));
        StatusText = $"{ClipGruppen.Count} {Lokalisierung.T("Status.ClipGruppenErkannt")} in {ClipOrdner}";
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

            if (ClipMergeAktiv)
            {
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
                    Modus = ModusFromIndex()
                };

                var ergebnis = await _clipMerger.ClipsZusammenfuegenMitFarbkorrekturAsync(
                    AusgewaehlteGruppe, ausgabeOrdner, _modelManager, _colorManager, param, fortschritt);

                if (ergebnis != null)
                    StatusText = $"{Lokalisierung.T("Status.Fertig")}: {Path.GetFileName(ergebnis)}";
                else
                    StatusText = Lokalisierung.T("Status.ZusammenfuegenMitFarbkorrekturFehlgeschlagen");
            }
            else
            {
                var ergebnis = await _clipMerger.ClipsZusammenfuegenAsync(
                    AusgewaehlteGruppe, ausgabeOrdner, fortschritt);

                if (ergebnis != null)
                    StatusText = $"{Lokalisierung.T("Status.Fertig")}: {Path.GetFileName(ergebnis)}";
                else
                    StatusText = Lokalisierung.T("Status.ZusammenfuegenFehlgeschlagen");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.MergeFehler")}: {ex.Message}";
        }
        finally
        {
            ClipMergeLaeuft = false;
        }
    }

    [RelayCommand]
    private async Task AlleClipsMergenAsync()
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
                Modus = ModusFromIndex()
            };

            foreach (var gruppe in ClipGruppen)
            {
                if (ClipMergeAktiv)
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

    // ===== Logs öffnen =====

    [RelayCommand]
    private void LogsOeffnen()
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipsiColor", "Logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            // Plattform-spezifisch: Ordner im Dateimanager öffnen
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = logDir,
                UseShellExecute = true,
                Verb = "open"
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Logs-Ordner konnte nicht geöffnet werden");
            StatusText = $"Fehler: {ex.Message}";
        }
    }

    // ===== Issue #12-18: Neue Feature Commands =====

    /// <summary>Issue #12: Before/After Slider umschalten.</summary>
    [RelayCommand]
    private void BeforeAfterToggle()
    {
        BeforeAfterAktiv = !BeforeAfterAktiv;
    }

    /// <summary>Issue #13: Preset-Namen laden.</summary>
    private void PresetNamenLaden()
    {
        var presets = _presetManager.ListePresets();
        PresetNamen.Clear();
        foreach (var p in presets)
            PresetNamen.Add(p.Name);
    }

    /// <summary>Issue #13: Preset anwenden.</summary>
    [RelayCommand]
    private void PresetAnwenden()
    {
        if (string.IsNullOrEmpty(AusgewaehltesPreset)) return;
        var preset = _presetManager.LadePreset(AusgewaehltesPreset);
        if (preset == null) return;

        Belichtung = preset.Parameter.Belichtung;
        Kontrast = preset.Parameter.Kontrast;
        Saettigung = preset.Parameter.Saettigung;
        Vibranz = preset.Parameter.Vibranz;
        Lichter = preset.Parameter.Lichter;
        Schatten = preset.Parameter.Schatten;
        GesichtswiederherstellungAktiv = preset.GesichtswiederherstellungAktiv;
        Objektivkorrektur = preset.ObjektivkorrekturAktiv;
        HochskalierenFaktor = preset.HochskalierenFaktor;
        StatusText = $"{Lokalisierung.T("Preset.Geladen")}: {preset.Name}";
    }

    /// <summary>Issue #13: Preset speichern.</summary>
    [RelayCommand]
    private void PresetSpeichern()
    {
        var name = "Preset " + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var preset = new KorrekturPreset
        {
            Name = name,
            Parameter = new PipelineParams
            {
                Belichtung = Belichtung,
                Kontrast = Kontrast,
                Saettigung = Saettigung,
                Vibranz = Vibranz,
                Lichter = Lichter,
                Schatten = Schatten
            },
            GesichtswiederherstellungAktiv = GesichtswiederherstellungAktiv,
            ObjektivkorrekturAktiv = Objektivkorrektur,
            HochskalierenFaktor = HochskalierenFaktor
        };
        _presetManager.SpeicherePreset(preset);
        PresetNamenLaden();
        StatusText = $"{Lokalisierung.T("Preset.Gespeichert")}: {name}";
    }

    /// <summary>Issue #14: Histogramm-Update vom ImagePipeline-Event.</summary>
    private void OnHistogrammAktualisiert(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var bild = _imagePipeline.Ergebnis ?? _imagePipeline.OriginalBild;
            var histData = HistogramCalculator.Berechnen(bild);
            if (histData == null) return;
            HistogrammRot = histData.Rot;
            HistogrammGruen = histData.Gruen;
            HistogrammBlau = histData.Blau;
            HistogrammLuminanz = histData.Luminanz;
            HistogrammMaxWert = histData.MaxWert;
        });
    }

    /// <summary>Issue #15: Dateien zur Batch-Queue hinzufügen.</summary>
    [RelayCommand]
    private async Task BatchDateienHinzufuegenAsync()
    {
        if (DateiOeffnenCallback == null) return;
        var filter = Lokalisierung.T("Filter.Bilder");
        var titel = Lokalisierung.T("Batch.Titel");
        var pfad = await DateiOeffnenCallback(titel, filter, null);
        if (string.IsNullOrEmpty(pfad)) return;

        if (string.IsNullOrEmpty(BatchZielOrdner))
            BatchZielOrdner = Path.GetDirectoryName(pfad) ?? "";
        _batchProcessor.DateienHinzufuegen(new[] { pfad }, BatchZielOrdner);
        BatchJobsAktualisieren();
    }

    /// <summary>Issue #15: Batch starten.</summary>
    [RelayCommand]
    private async Task BatchStartenAsync()
    {
        if (BatchLaeuft) return;
        BatchLaeuft = true;
        BatchStatus = Lokalisierung.T("Batch.Laeuft");
        try
        {
            await _batchProcessor.StartenAsync(async (job, progress) =>
            {
                if (job.IstBild)
                {
                    using var pipeline = new ImagePipeline(_modelManager, _colorManager);
                    if (pipeline.BildLaden(job.Quelle))
                    {
                        pipeline.PipelineAusfuehren(new PipelineParams());
                        var ergebnis = pipeline.Ergebnis;
                        if (ergebnis != null && !ergebnis.Empty())
                            Cv2.ImWrite(job.Ziel, ergebnis);
                    }
                }
                progress(1.0);
                await Task.CompletedTask;
            }, gesamt => Dispatcher.UIThread.Post(() =>
            {
                BatchFortschritt = gesamt;
                BatchStatus = string.Format(Lokalisierung.T("Batch.Fortschritt"),
                    _batchProcessor.Abgeschlossen, _batchProcessor.Gesamt);
            }));

            BatchStatus = $"{Lokalisierung.T("Batch.Abgeschlossen")} ({_batchProcessor.Abgeschlossen}/{_batchProcessor.Gesamt})";
        }
        catch (Exception ex)
        {
            BatchStatus = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
        finally
        {
            BatchLaeuft = false;
            BatchJobsAktualisieren();
        }
    }

    /// <summary>Issue #15: Batch pausieren.</summary>
    [RelayCommand]
    private void BatchPausieren()
    {
        _batchProcessor.Pausieren();
        BatchStatus = Lokalisierung.T("Batch.Pausiert");
    }

    /// <summary>Issue #15: Batch-Queue aktualisieren.</summary>
    private void BatchJobsAktualisieren()
    {
        BatchJobs.Clear();
        foreach (var job in _batchProcessor.Jobs)
            BatchJobs.Add(job);
    }

    /// <summary>Issue #16: Undo.</summary>
    [RelayCommand]
    private void Undo()
    {
        _undoManager.Undo();
        UndoStatusAktualisieren();
    }

    /// <summary>Issue #16: Redo.</summary>
    [RelayCommand]
    private void Redo()
    {
        _undoManager.Redo();
        UndoStatusAktualisieren();
    }

    /// <summary>Issue #16: Undo/Redo-Status aktualisieren.</summary>
    private void UndoStatusAktualisieren()
    {
        KannUndo = _undoManager.KannUndo;
        KannRedo = _undoManager.KannRedo;
        UndoHistory.Clear();
        foreach (var cmd in _undoManager.History)
            UndoHistory.Add($"[{cmd.Zeitstempel:HH:mm:ss}] {cmd.Beschreibung}");
    }

    /// <summary>Issue #17: Crop anwenden.</summary>
    [RelayCommand]
    private void CropAnwenden()
    {
        if (!BildGeladen) return;
        var ergebnis = _imagePipeline.Ergebnis;
        if (ergebnis == null || ergebnis.Empty()) return;

        try
        {
            Mat bearbeitet = ergebnis;

            if (Math.Abs(CropRotation) > 0.1)
            {
                var rotiert = CropProcessor.Rotieren(bearbeitet, CropRotation);
                if (rotiert != null)
                {
                    if (!ReferenceEquals(bearbeitet, ergebnis)) bearbeitet.Dispose();
                    bearbeitet = rotiert;
                }
            }

            if (CropBreite > 0 && CropHoehe > 0)
            {
                var gecroppt = CropProcessor.Crop(bearbeitet, CropX, CropY, CropBreite, CropHoehe);
                if (gecroppt != null)
                {
                    if (!ReferenceEquals(bearbeitet, ergebnis)) bearbeitet.Dispose();
                    bearbeitet = gecroppt;
                }
            }

            if (!ReferenceEquals(bearbeitet, ergebnis))
            {
                PipelineBild = MatToBitmapConverter.ConvertMat(bearbeitet);
                StatusText = Lokalisierung.T("Crop.Angewendet");
            }
        }
        catch (Exception ex)
        {
            StatusText = $"{Lokalisierung.T("Status.Fehler")}: {ex.Message}";
        }
    }

    /// <summary>Issue #17: Crop-Modus umschalten.</summary>
    [RelayCommand]
    private void CropModusToggle()
    {
        CropModusAktiv = !CropModusAktiv;
    }

    /// <summary>Issue #17: Um 90 Grad rotieren.</summary>
    [RelayCommand]
    private void Rotieren90()
    {
        if (!BildGeladen) return;
        var ergebnis = _imagePipeline.Ergebnis;
        if (ergebnis == null || ergebnis.Empty()) return;

        var rotiert = CropProcessor.Rotieren90(ergebnis, true);
        if (rotiert != null)
        {
            PipelineBild = MatToBitmapConverter.ConvertMat(rotiert);
            StatusText = Lokalisierung.T("Crop.Rotiert");
        }
    }

    /// <summary>Issue #18: Plugin-Liste laden.</summary>
    private void PluginListeLaden()
    {
        PluginListe.Clear();
        foreach (var info in _pluginManager.PluginInfos)
            PluginListe.Add(info);
        PluginStatus = PluginListe.Count > 0
            ? $"{PluginListe.Count} {Lokalisierung.T("Plugin.Geladen")}"
            : Lokalisierung.T("Plugin.Keine");
    }

    /// <summary>Issue #18: Plugin aktivieren/deaktivieren.</summary>
    [RelayCommand]
    private void PluginAktivieren(PluginInfo info)
    {
        if (info == null) return;
        _pluginManager.PluginAktivieren(info.Name, !info.Aktiviert);
        PluginListeLaden();
    }

    public void Dispose()
    {
        _imagePipeline.Dispose();
        _videoPipeline.Dispose();
        _modelManager.Dispose();
        _autoUpdater.Dispose();
        _clipMerger.Dispose();
        _presetManager.Dispose();
        _undoManager.Dispose();
        _pluginManager.Dispose();
    }
}

/// <summary>
/// Ein Eintrag in der Datei-Liste (Drag &amp; Drop).
/// </summary>
public sealed class DateiEintrag
{
    public string Pfad { get; init; } = "";
    public string Dateiname { get; init; } = "";
    public bool IstBild { get; init; }
    public bool IstVideo { get; init; }
    public string Icon => IstBild ? "🖼" : IstVideo ? "🎬" : "📄";
}