using System;
using System.IO;
using System.Text.Json;

using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.Core;

/// <summary>
/// Settings — Anwendungs-Einstellungen (JSON-basiert).
/// FIX #5: Typ-sichere Deserialisierung mit System.Text.Json (kein TypeNameHandling).
/// FIX #9: Keine sensiblen Daten in Log-Ausgaben.
/// </summary>
public sealed class Settings
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<Settings>();
    private static readonly string SettingsPfad = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlipsiColor", "settings.json");

    // FIX #5: JsonSerializerOptions ohne TypeNameHandling — verhindert Typ-Confusion
    private static readonly JsonSerializerOptions JsonOptionen = new()
    {
        WriteIndented = true,
        // FIX #5: Kein JsonSerializer für beliebige Typen — nur explizite Settings-Klasse
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Theme
    public string Theme { get; set; } = "System"; // "Dark", "Light", "System"

    // Sprache
    public string Sprache { get; set; } = "de";

    // Auto-Update
    public bool AutoUpdatePruefen { get; set; } = true;
    public UpdateKanal UpdateKanal { get; set; } = UpdateKanal.Stable;

    // Pipeline Defaults
    public float StandardBelichtung { get; set; } = 0.0f;
    public float StandardKontrast { get; set; } = 0.0f;
    public float StandardSaettigung { get; set; } = 0.0f;

    // Betriebsmodus
    public BetriebsModus Modus { get; set; } = BetriebsModus.Ask;

    // Video-Backend: FFmpeg (Standard) oder VapourSynth (optional, Frame-Level-Processing)
    public VideoBackend VideoBackend { get; set; } = VideoBackend.FFmpeg;

    // ── Farbmanagement (v0.5.0) ──
    public ColorManagementMode ColorManagement { get; set; } = ColorManagementMode.Standard;
    public string? OCIOConfigPfad { get; set; }
    public string? OCIOSourceColorSpace { get; set; } = "ACEScg";
    public string? OCIODisplay { get; set; } = "sRGB";
    public string? OCIOView { get; set; } = "Filmic";
    public string? OCIOLook { get; set; }
    public OCIOEngine OCIOEngineMode { get; set; } = OCIOEngine.LUTBaking;

    // ── Pro-Funktions KI-Toggles (v0.5.0) ──
    // Default: alle true — bestehendes Verhalten für User die nichts ändern wollen.
    public bool KIDenoisingAktiv { get; set; } = true;
    public bool KISchaerfungAktiv { get; set; } = true;
    public bool KIUpscalingAktiv { get; set; } = true;
    public bool KIGesichtswiederherstellungAktiv { get; set; } = true;
    public bool KIFarbstilAktiv { get; set; } = true;
    public bool KISzenenklassifizierungAktiv { get; set; } = true;

    // Fenster
    public int FensterBreite { get; set; } = 1200;
    public int FensterHoehe { get; set; } = 800;

    /// <summary>Ignorierte Update-Version (User hat "Ignorieren" geklickt)</summary>
    public string? IgnorierteUpdateVersion { get; set; }

    public static Settings Laden()
    {
        try
        {
            if (File.Exists(SettingsPfad))
            {
                var json = File.ReadAllText(SettingsPfad);
                // FIX #5: Typ-sichere Deserialisierung — nur Settings-Typ erlaubt
                var settings = JsonSerializer.Deserialize<Settings>(json, JsonOptionen);
                if (settings != null)
                {
                    // FIX #7: Werte auf sichere Bereiche begrenzen — verhindert extreme Werte
                    settings.FensterBreite = Math.Clamp(settings.FensterBreite, 400, 7680);
                    settings.FensterHoehe = Math.Clamp(settings.FensterHoehe, 300, 4320);
                    Log.Debug("Einstellungen geladen");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Einstellungen konnten nicht geladen werden — Defaults: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }

        return new Settings();
    }

    public void Speichern()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPfad)!);
            var json = JsonSerializer.Serialize(this, JsonOptionen);
            File.WriteAllText(SettingsPfad, json);
            Log.Debug("Einstellungen gespeichert");
        }
        catch (Exception ex)
        {
            Log.Error("Einstellungen konnten nicht gespeichert werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }
    }
}