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
}