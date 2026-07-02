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
}