namespace FlipsiColor.Core;

/// <summary>
/// Korrektur-Intensität (Leicht/Mittel/Stark)
/// </summary>
public enum Intensitaet
{
    Leicht,
    Mittel,
    Stark
}

/// <summary>
/// Betriebsmodus der App
/// </summary>
public enum BetriebsModus
{
    /// <summary>Ask — User bestätigt jeden Vorschlag</summary>
    Ask,
    /// <summary>SmartLearn — KI lernt aus User-Entscheidungen</summary>
    SmartLearn,
    /// <summary>Turbo — KI korrigiert automatisch</summary>
    Turbo
}

/// <summary>
/// Video-Backend für die Video-Processing-Pipeline.
/// FFmpeg = Standard (Frame-basiert mit OpenCvSharp), VapourSynth = optional
/// (Frame-Level-Processing mit Python-Filter-Pipelines, piped an FFmpeg).
/// </summary>
public enum VideoBackend
{
    /// <summary>FFmpeg — Standard-Backend, immer verfügbar.</summary>
    FFmpeg,
    /// <summary>VapourSynth — optionales Backend für Frame-Level-Filter-Pipelines.</summary>
    VapourSynth
}

/// <summary>
/// Farbmanagement-Modus — bestimmt wie Farben verwaltet und transformiert werden.
/// Standard = ProPhoto RGB Arbeitsfarbraum (bestehend).
/// OpenColorIO = OCIO-basiertes Farbmanagement mit Config-Datei (.ocio).
/// </summary>
public enum ColorManagementMode
{
    /// <summary>Standard — ProPhoto RGB + StyleLUT (bestehendes Verhalten).</summary>
    Standard,
    /// <summary>OpenColorIO — industrie-standard Farbmanagement via .ocio Config.</summary>
    OpenColorIO
}

/// <summary>
/// OCIO-Engine — wie die OCIO-Transform angewendet wird.
/// LUTBaking = bakt .cube LUT via ociobakelut CLI → nutzt bestehende StyleLUT (keine native Lib nötig).
/// Native = direkter OCIO-Processor via OCIOSharp C# Bindings (höchste Präzision, benötigt libOpenColorIO).
/// </summary>
public enum OCIOEngine
{
    /// <summary>LUT-Baking — bakt .cube LUT via ociobakelut, nutzt bestehende StyleLUT-Pipeline. Keine native Library nötig.</summary>
    LUTBaking,
    /// <summary>Native — direkter OCIO-Processor via C# Bindings. Höchste Präzision, benötigt libOpenColorIO.</summary>
    Native
}

/// <summary>
/// Pipeline-Parameter für Bild- und Videokorrektur
/// </summary>
public sealed class PipelineParams
{
    public float WeissabgleichTemp { get; set; } = 5500.0f;
    public float WeissabgleichTint { get; set; } = 0.0f;
    public float Belichtung { get; set; } = 0.0f;
    public float Kontrast { get; set; } = 0.0f;
    public float Lichter { get; set; } = 0.0f;
    public float Schatten { get; set; } = 0.0f;
    public float Saettigung { get; set; } = 0.0f;
    public float Vibranz { get; set; } = 0.0f;
    public float SchaerfeBetrag { get; set; } = 0.0f;
    public float LuminanzRauschen { get; set; } = 0.0f;
    public float ChrominanzRauschen { get; set; } = 0.0f;
    public bool ObjektivkorrekturAktiv { get; set; } = true;
    public bool GesichtswiederherstellungAktiv { get; set; } = false;
    public int HochskalierenFaktor { get; set; } = 1;
    public bool DistortionGridAktiv { get; set; } = false;
    public bool ColorCalibrationAktiv { get; set; } = false;

    /// <summary>
    /// Intensität der KI-Korrektur — steuert Modell-Stärke
    /// (z.B. NAFNet noise level, CodeFormer fidelity weight).
    /// </summary>
    public Intensitaet Intensitaet { get; set; } = Intensitaet.Mittel;

    /// <summary>
    /// Betriebsmodus — Ask (manuell), SmartLearn (KI-Vorschläge),
    /// Turbo (vollautomatisch).
    /// </summary>
    public BetriebsModus Modus { get; set; } = BetriebsModus.Ask;

    /// <summary>
    /// Pfad zu einer .cube Style-LUT-Datei. Wenn gesetzt,
    /// wird die LUT nach der Sättigung angewendet.
    /// </summary>
    public string? StyleLutPfad { get; set; }

    /// <summary>
    /// Name des KI-Stils für AiLUTTransform (z.B. "Vintage", "Warm", "Cool").
    /// Wenn gesetzt, wird AiLUTTransform statt manueller Sättigung verwendet.
    /// </summary>
    public string? AiStilName { get; set; }

    // ── EXIF-Daten (werden von ImagePipeline.BildLaden gefüllt) ──

    /// <summary>EXIF: Kamera-Modell (für LensCorrector)</summary>
    public string? ExifKamera { get; set; }

    /// <summary>EXIF: Objektiv-Bezeichnung (für LensCorrector)</summary>
    public string? ExifObjektiv { get; set; }

    /// <summary>EXIF: Brennweite in mm (für LensCorrector)</summary>
    public float? ExifBrennweite { get; set; }

    /// <summary>EXIF: Blendenwert (für LensCorrector)</summary>
    public float? ExifBlende { get; set; }

    /// <summary>Von EfficientNet erkannte Szene (z.B. "Landschaft", "Porträt")</summary>
    public string? ErkannteSzene { get; set; }

    // ── Manuelle Kamera/Objektiv-Auswahl (statt EXIF) ──

    /// <summary>
    /// Manuell ausgewählter Kamera-Hersteller (überschreibt EXIF-Daten).
    /// Wenn null/leer → EXIF-Daten werden verwendet (bestehendes Verhalten).
    /// </summary>
    public string? ManuelleKamera { get; set; }

    /// <summary>
    /// Manuell ausgewähltes Objektiv (überschreibt EXIF-Daten).
    /// Wenn null/leer → EXIF-Daten werden verwendet (bestehendes Verhalten).
    /// </summary>
    public string? ManuellesObjektiv { get; set; }

    // ── Pro-Funktions KI-Toggles (v0.5.0) ──
    // Wenn true → KI-Modell wird verwendet. Wenn false → klassische/fallback Methode.
    // Default: alle true (bestehendes Verhalten für User die keine Änderungen wollen).

    /// <summary>
    /// KI-Denoising (NAFNet) aktivieren. Wenn false → klassische Gaussian/Median-Filterung.
    /// </summary>
    public bool KIDenoisingAktiv { get; set; } = true;

    /// <summary>
    /// KI-Schärfung (RestormerLight) aktivieren. Wenn false → klassische Convolution-Schärfung.
    /// </summary>
    public bool KISchaerfungAktiv { get; set; } = true;

    /// <summary>
    /// KI-Hochskalieren (RealESRGAN/RealHATGAN) aktivieren. Wenn false → Bicubic-Upscaling.
    /// </summary>
    public bool KIUpscalingAktiv { get; set; } = true;

    /// <summary>
    /// KI-Gesichtswiederherstellung (CodeFormer) aktivieren. Wenn false → keine Gesichtskorrektur.
    /// </summary>
    public bool KIGesichtswiederherstellungAktiv { get; set; } = true;

    /// <summary>
    /// KI-Farbstil (AiLUTTransform) aktivieren. Wenn false → manuelle Sättigung/Vibranz.
    /// </summary>
    public bool KIFarbstilAktiv { get; set; } = true;

    /// <summary>
    /// KI-Szenenklassifizierung (EfficientNet) aktivieren. Wenn false → keine automatische Szenen-Erkennung.
    /// Nur relevant im Turbo/SmartLearn-Modus.
    /// </summary>
    public bool KISzenenklassifizierungAktiv { get; set; } = true;

    // ── OpenColorIO (v0.5.0) ──

    /// <summary>
    /// Farbmanagement-Modus. Standard = ProPhoto RGB, OpenColorIO = OCIO-basiert.
    /// </summary>
    public ColorManagementMode ColorManagement { get; set; } = ColorManagementMode.Standard;

    /// <summary>
    /// Pfad zur .ocio Config-Datei. Nur relevant wenn ColorManagement == OpenColorIO.
    /// </summary>
    public string? OCIOConfigPfad { get; set; }

    /// <summary>
    /// Source Color Space (z.B. "ACEScg", "sRGB", "linear Rec.709").
    /// Nur relevant wenn ColorManagement == OpenColorIO.
    /// </summary>
    public string? OCIOSourceColorSpace { get; set; }

    /// <summary>
    /// Display (z.B. "sRGB", "Rec.2020"). Nur relevant wenn ColorManagement == OpenColorIO.
    /// </summary>
    public string? OCIODisplay { get; set; }

    /// <summary>
    /// View Transform (z.B. "Filmic", "ACES"). Nur relevant wenn ColorManagement == OpenColorIO.
    /// </summary>
    public string? OCIOView { get; set; }

    /// <summary>
    /// Optionaler OCIO Look (Creative Grade). Leer = kein Look.
    /// Nur relevant wenn ColorManagement == OpenColorIO.
    /// </summary>
    public string? OCIOLook { get; set; }

    /// <summary>
    /// OCIO-Engine: LUTBaking (Standard, keine native Lib) oder Native (höchste Präzision).
    /// Nur relevant wenn ColorManagement == OpenColorIO.
    /// </summary>
    public OCIOEngine OCIOEngineMode { get; set; } = OCIOEngine.LUTBaking;
}