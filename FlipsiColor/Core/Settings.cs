using System;
using System.IO;
using System.Text.Json;

using FlipsiColor.Utils;

namespace FlipsiColor.Core;

/// <summary>
/// Settings — Anwendungs-Einstellungen (JSON-basiert)
/// </summary>
public sealed class Settings
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<Settings>();
    private static readonly string SettingsPfad = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlipsiColor", "settings.json");

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

    // Fenster
    public int FensterBreite { get; set; } = 1200;
    public int FensterHoehe { get; set; } = 800;

    public static Settings Laden()
    {
        try
        {
            if (File.Exists(SettingsPfad))
            {
                var json = File.ReadAllText(SettingsPfad);
                var settings = JsonSerializer.Deserialize<Settings>(json);
                if (settings != null)
                {
                    Log.Debug("Einstellungen geladen: {Pfad}", SettingsPfad);
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Einstellungen konnten nicht geladen werden — Defaults");
        }

        return new Settings();
    }

    public void Speichern()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPfad)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPfad, json);
            Log.Debug("Einstellungen gespeichert: {Pfad}", SettingsPfad);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Einstellungen konnten nicht gespeichert werden");
        }
    }
}