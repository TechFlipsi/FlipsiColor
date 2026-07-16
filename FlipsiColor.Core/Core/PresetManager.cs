using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using FlipsiColor.Utils;

namespace FlipsiColor.Core;

/// <summary>
/// Ein Korrektur-Preset — speichert alle PipelineParams + Korrektur-Werte.
/// Kann gespeichert, geladen, umbenannt, dupliziert und gelöscht werden.
/// </summary>
public sealed class KorrekturPreset
{
    /// <summary>Name des Presets (z.B. "Cinematic Warm").</summary>
    public string Name { get; set; } = "";

    /// <summary>True wenn es ein mitgeliefertes Standard-Preset ist.</summary>
    public bool IstStandard { get; set; }

    /// <summary>Die Pipeline-Parameter für dieses Preset.</summary>
    public PipelineParams Parameter { get; set; } = new();

    /// <summary>Der zugehörige Betriebsmodus als String (Ask/SmartLearn/Turbo).</summary>
    public string Modus { get; set; } = "Ask";

    /// <summary>Intensität-Index (0=Leicht, 1=Mittel, 2=Stark).</summary>
    public int IntensitaetIndex { get; set; } = 1;

    /// <summary>Hochskalieren-Faktor (1=aus, 2, 3, 4).</summary>
    public int HochskalierenFaktor { get; set; } = 1;

    /// <summary>Gesichtswiederherstellung aktiv.</summary>
    public bool GesichtswiederherstellungAktiv { get; set; }

    /// <summary>Objektivkorrektur aktiv.</summary>
    public bool ObjektivkorrekturAktiv { get; set; } = true;

    /// <summary>Style-LUT Pfad (optional).</summary>
    public string? StyleLutPfad { get; set; }
}

/// <summary>
/// PresetManager — verwaltet Korrektur-Presets als JSON-Dateien in LocalAppData.
/// Speichert, lädt, löscht und dupliziert Presets.
/// Stellt Standard-Presets bei der ersten Ausführung bereit.
/// FIX #5: Typ-sichere JSON-Serialisierung (kein TypeNameHandling).
/// Pitfall #17: Verwendet AppContext.BaseDirectory, nicht Assembly.Location.
/// </summary>
public sealed class PresetManager : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<PresetManager>();

    private static readonly JsonSerializerOptions JsonOptionen = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _presetVerzeichnis;

    public PresetManager()
    {
        _presetVerzeichnis = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlipsiColor", "presets");
    }

    /// <summary>Verzeichnis für Preset-Dateien.</summary>
    public string Verzeichnis => _presetVerzeichnis;

    /// <summary>
    /// Stellt sicher, dass das Preset-Verzeichnis existiert.
    /// Erstellt Standard-Presets bei der ersten Ausführung.
    /// </summary>
    public void Initialisieren()
    {
        try
        {
            if (!Directory.Exists(_presetVerzeichnis))
                Directory.CreateDirectory(_presetVerzeichnis);

            // Standard-Presets erstellen wenn Verzeichnis leer ist
            if (Directory.GetFiles(_presetVerzeichnis, "*.json").Length == 0)
            {
                foreach (var preset in StandardPresetsErstellen())
                {
                    SpeicherePreset(preset);
                }
                Log.Information("Standard-Presets erstellt in {Verzeichnis}", _presetVerzeichnis);
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Preset-Verzeichnis konnte nicht initialisiert werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }
    }

    /// <summary>
    /// Listet alle verfügbaren Presets auf.
    /// </summary>
    public List<KorrekturPreset> ListePresets()
    {
        var presets = new List<KorrekturPreset>();
        try
        {
            if (!Directory.Exists(_presetVerzeichnis))
                return presets;

            foreach (var datei in Directory.GetFiles(_presetVerzeichnis, "*.json"))
            {
                var preset = LadePresetAusDatei(datei);
                if (preset != null)
                    presets.Add(preset);
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Presets konnten nicht gelistet werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }

        return presets.OrderBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Speichert ein Preset als JSON-Datei.
    /// Der Dateiname wird aus dem Namen generiert (bereinigt für Dateisystem).
    /// </summary>
    public bool SpeicherePreset(KorrekturPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            Log.Warning("Preset-Name darf nicht leer sein");
            return false;
        }

        try
        {
            Directory.CreateDirectory(_presetVerzeichnis);
            var dateiname = BereinigeDateiname(preset.Name) + ".json";
            var pfad = Path.Combine(_presetVerzeichnis, dateiname);
            var json = JsonSerializer.Serialize(preset, JsonOptionen);
            File.WriteAllText(pfad, json);
            Log.Debug("Preset gespeichert: {Name}", preset.Name);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Preset konnte nicht gespeichert werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Lädt ein Preset anhand seines Namens.
    /// </summary>
    public KorrekturPreset? LadePreset(string name)
    {
        return LadePresetByName(name);
    }

    /// <summary>
    /// Lädt ein Preset anhand seines Namens.
    /// </summary>
    public KorrekturPreset? LadePresetByName(string name)
    {
        try
        {
            var dateiname = BereinigeDateiname(name) + ".json";
            var pfad = Path.Combine(_presetVerzeichnis, dateiname);
            if (!File.Exists(pfad))
            {
                Log.Warning("Preset nicht gefunden: {Name}", name);
                return null;
            }
            return LadePresetAusDatei(pfad);
        }
        catch (Exception ex)
        {
            Log.Error("Preset konnte nicht geladen werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return null;
        }
    }

    /// <summary>
    /// Löscht ein Preset anhand seines Namens.
    /// </summary>
    public bool LoeschePreset(string name)
    {
        try
        {
            var dateiname = BereinigeDateiname(name) + ".json";
            var pfad = Path.Combine(_presetVerzeichnis, dateiname);
            if (File.Exists(pfad))
            {
                File.Delete(pfad);
                Log.Debug("Preset gelöscht: {Name}", name);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Error("Preset konnte nicht gelöscht werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Dupliziert ein bestehendes Preset unter einem neuen Namen.
    /// </summary>
    public bool DuplizierePreset(string originalName, string neuerName)
    {
        var preset = LadePresetByName(originalName);
        if (preset == null)
        {
            Log.Warning("Preset nicht gefunden zum Duplizieren: {Name}", originalName);
            return false;
        }

        var kopie = new KorrekturPreset
        {
            Name = neuerName,
            IstStandard = false,
            Parameter = KloneParameter(preset.Parameter),
            Modus = preset.Modus,
            IntensitaetIndex = preset.IntensitaetIndex,
            HochskalierenFaktor = preset.HochskalierenFaktor,
            GesichtswiederherstellungAktiv = preset.GesichtswiederherstellungAktiv,
            ObjektivkorrekturAktiv = preset.ObjektivkorrekturAktiv,
            StyleLutPfad = preset.StyleLutPfad
        };
        return SpeicherePreset(kopie);
    }

    /// <summary>
    /// Benennt ein Preset um (löscht das alte, speichert das neue).
    /// </summary>
    public bool BenennePresetUm(string alterName, string neuerName)
    {
        var preset = LadePresetByName(alterName);
        if (preset == null) return false;

        preset.Name = neuerName;
        preset.IstStandard = false;

        // Alte Datei löschen, neue speichern
        LoeschePreset(alterName);
        return SpeicherePreset(preset);
    }

    // ── Hilfsmethoden ──

    /// <summary>
    /// Lädt ein Preset aus einer JSON-Datei.
    /// </summary>
    private static KorrekturPreset? LadePresetAusDatei(string pfad)
    {
        try
        {
            var json = File.ReadAllText(pfad);
            var preset = JsonSerializer.Deserialize<KorrekturPreset>(json, JsonOptionen);
            if (preset != null && string.IsNullOrEmpty(preset.Name))
            {
                // Name aus Dateiname ableiten falls leer
                preset.Name = Path.GetFileNameWithoutExtension(pfad);
            }
            return preset;
        }
        catch (Exception ex)
        {
            Log.Warning("Preset-Datei konnte nicht geladen werden ({Datei}): {Fehler}",
                Path.GetFileName(pfad),
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return null;
        }
    }

    /// <summary>
    /// Bereinigt einen Preset-Namen für die Verwendung als Dateiname.
    /// </summary>
    private static string BereinigeDateiname(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = name.Trim();
        foreach (var c in invalid)
            result = result.Replace(c, '_');
        return result;
    }

    /// <summary>
    /// Erstellt eine Kopie der PipelineParams.
    /// </summary>
    private static PipelineParams KloneParameter(PipelineParams source)
    {
        var json = JsonSerializer.Serialize(source, JsonOptionen);
        return JsonSerializer.Deserialize<PipelineParams>(json, JsonOptionen) ?? new PipelineParams();
    }

    /// <summary>
    /// Erstellt die Standard-Presets für die erste Ausführung.
    /// </summary>
    private static List<KorrekturPreset> StandardPresetsErstellen()
    {
        return
        [
            new KorrekturPreset
            {
                Name = "Cinematic Warm",
                IstStandard = true,
                Modus = "Turbo",
                IntensitaetIndex = 2,
                Parameter = new PipelineParams
                {
                    Belichtung = 5.0f,
                    Kontrast = 15.0f,
                    Saettigung = 10.0f,
                    Vibranz = 5.0f,
                    Lichter = -10.0f,
                    Schatten = 15.0f
                },
                GesichtswiederherstellungAktiv = false,
                ObjektivkorrekturAktiv = true,
                HochskalierenFaktor = 1
            },
            new KorrekturPreset
            {
                Name = "Portrait-Natur",
                IstStandard = true,
                Modus = "Ask",
                IntensitaetIndex = 1,
                Parameter = new PipelineParams
                {
                    Belichtung = 3.0f,
                    Kontrast = 5.0f,
                    Saettigung = -5.0f,
                    Vibranz = 10.0f,
                    Lichter = -5.0f,
                    Schatten = 10.0f
                },
                GesichtswiederherstellungAktiv = true,
                ObjektivkorrekturAktiv = true,
                HochskalierenFaktor = 2
            },
            new KorrekturPreset
            {
                Name = "Winter-Kalt",
                IstStandard = true,
                Modus = "Turbo",
                IntensitaetIndex = 1,
                Parameter = new PipelineParams
                {
                    Belichtung = 8.0f,
                    Kontrast = 20.0f,
                    Saettigung = -15.0f,
                    Vibranz = 0.0f,
                    Lichter = 5.0f,
                    Schatten = 20.0f
                },
                GesichtswiederherstellungAktiv = false,
                ObjektivkorrekturAktiv = true,
                HochskalierenFaktor = 1
            },
            new KorrekturPreset
            {
                Name = "Vintage",
                IstStandard = true,
                Modus = "Ask",
                IntensitaetIndex = 0,
                Parameter = new PipelineParams
                {
                    Belichtung = -5.0f,
                    Kontrast = 10.0f,
                    Saettigung = -20.0f,
                    Vibranz = 5.0f,
                    Lichter = 10.0f,
                    Schatten = -10.0f
                },
                GesichtswiederherstellungAktiv = false,
                ObjektivkorrekturAktiv = false,
                HochskalierenFaktor = 1
            },
            new KorrekturPreset
            {
                Name = "Schwarzweiss",
                IstStandard = true,
                Modus = "Turbo",
                IntensitaetIndex = 1,
                Parameter = new PipelineParams
                {
                    Belichtung = 0.0f,
                    Kontrast = 25.0f,
                    Saettigung = -100.0f,
                    Vibranz = 0.0f,
                    Lichter = 5.0f,
                    Schatten = 10.0f
                },
                GesichtswiederherstellungAktiv = false,
                ObjektivkorrekturAktiv = true,
                HochskalierenFaktor = 1
            }
        ];
    }

    public void Dispose()
    {
        // Keine disposbaren Ressourcen
    }
}