using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FlipsiColor;

/// <summary>
/// Lokalisierung — JSON-basierte Übersetzung (Issue #9 Vorschlag von MarcoRavich).
/// UI-Strings werden aus externen JSON-Dateien im Assets/i18n/ Ordner geladen.
/// Contributors können neue Sprachen hinzufügen, indem sie eine JSON-Datei kopieren und übersetzen.
/// </summary>
public static class Lokalisierung
{
    /// <summary>
    /// Aktuelle Sprache (z.B. "de", "en").
    /// </summary>
    public static string Sprache { get; private set; } = "de";

    /// <summary>
    /// Event das bei Sprachwechsel ausgelöst wird.
    /// </summary>
    public static event EventHandler? SpracheGeaendert;

    /// <summary>
    /// Verfügbare Sprachen — automatisch aus dem i18n-Ordner ermittelt.
    /// </summary>
    public static List<string> VerfuegbareSprachen { get; private set; } = new() { "de", "en" };

    private static Dictionary<string, string> _uebersetzungen = new();
    private static readonly Dictionary<string, Dictionary<string, string>> _cache = new();

    /// <summary>
    /// Initialisiert die Lokalisierung — lädt die Standard-Sprache.
    /// </summary>
    public static void Initialisieren(string startSprache = "de")
    {
        VerfuegbareSprachenErmitteln();
        SpracheSetzen(startSprache);
    }

    /// <summary>
    /// Setzt die Sprache und löst das SpracheGeaendert-Event aus.
    /// </summary>
    public static void SpracheSetzen(string sprache)
    {
        if (string.IsNullOrEmpty(sprache)) sprache = "de";

        var dict = SpracheLaden(sprache);
        if (dict == null && sprache != "de")
        {
            sprache = "de";
            dict = SpracheLaden(sprache);
        }

        if (dict == null) return;

        var alteSprache = Sprache;
        _uebersetzungen = dict;
        Sprache = sprache;

        if (alteSprache != sprache)
        {
            SpracheGeaendert?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gibt den übersetzten Text für den gegebenen Schlüssel zurück.
    /// Fällt auf den Schlüssel selbst zurück, wenn keine Übersetzung gefunden wird.
    /// </summary>
    public static string T(string schluessel)
    {
        return _uebersetzungen.TryGetValue(schluessel, out var wert) ? wert : schluessel;
    }

    private static Dictionary<string, string>? SpracheLaden(string sprache)
    {
        if (_cache.TryGetValue(sprache, out var cached))
            return cached;

        var dict = JsonLaden(sprache);
        if (dict != null)
            _cache[sprache] = dict;
        return dict;
    }

    private static Dictionary<string, string>? JsonLaden(string sprache)
    {
        // Avalonia: Assets sind als AvaloniaResource eingebettet — versuche zuerst Dateisystem, dann Embedded
        string basisPfad = AppDomain.CurrentDomain.BaseDirectory;
        string dateiPfad = Path.Combine(basisPfad, "Assets", "i18n", $"{sprache}.json");

        if (!File.Exists(dateiPfad))
        {
            // Fallback: Assembly-Verzeichnis
            var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(assemblyPath))
                dateiPfad = Path.Combine(assemblyPath, "Assets", "i18n", $"{sprache}.json");
        }

        if (!File.Exists(dateiPfad))
        {
            System.Diagnostics.Debug.WriteLine($"Lokalisierung: JSON-Datei nicht gefunden: {dateiPfad}");
            return null;
        }

        try
        {
            var json = File.ReadAllText(dateiPfad);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return dict ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Lokalisierung: Fehler beim Laden von {sprache}.json: {ex.Message}");
            return null;
        }
    }

    private static void VerfuegbareSprachenErmitteln()
    {
        var pfad = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "i18n");
        if (!Directory.Exists(pfad)) return;

        var sprachen = new List<string>();
        foreach (var datei in Directory.GetFiles(pfad, "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(datei);
            if (!string.IsNullOrEmpty(name))
                sprachen.Add(name);
        }
        sprachen.Sort();
        if (sprachen.Count > 0)
            VerfuegbareSprachen = sprachen;
    }
}