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
    /// Wird beim App-Start aufgerufen.
    /// </summary>
    public static void Initialisieren(string startSprache = "")
    {
        VerfuegbareSprachenErmitteln();
        SpracheSetzen(startSprache);
    }

    /// <summary>
    /// Erkennt die Systemsprache und mappt sie auf eine verfügbare Sprache.
    /// Fallback: English, dann Deutsch.
    /// </summary>
    private static string SystemspracheErkennen()
    {
        try
        {
            // Windows: CultureInfo.CurrentCulture.Name (z.B. "it-IT", "de-DE", "en-US")
            // Linux: Environment Variables LANG/LC_ALL
            var culture = System.Globalization.CultureInfo.CurrentUICulture.Name.ToLowerInvariant();

            // Erste 2 Zeichen = Sprachcode (z.B. "it", "de", "en", "fr", "es")
            var sprachCode = culture.Length >= 2 ? culture.Substring(0, 2) : "en";

            // Prüfen ob wir diese Sprache haben
            if (VerfuegbareSprachen.Contains(sprachCode))
                return sprachCode;

            // Fallback: English
            if (VerfuegbareSprachen.Contains("en"))
                return "en";

            // Letzter Fallback: Deutsch
            return "de";
        }
        catch
        {
            return "en";
        }
    }

    /// <summary>
    /// Setzt die Sprache und löst das SpracheGeaendert-Event aus.
    /// </summary>
    public static void SpracheSetzen(string sprache)
    {
        // Leere Sprache = Systemsprache erkennen (Issue #9 — MarcoRavich)
        if (string.IsNullOrEmpty(sprache))
            sprache = SystemspracheErkennen();

        // Sprache laden
        var dict = SpracheLaden(sprache);
        if (dict == null)
        {
            // Fallback auf Deutsch
            if (sprache != "de")
            {
                sprache = "de";
                dict = SpracheLaden(sprache);
            }
        }

        if (dict == null) return; // Nichts zu laden

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
    /// Fallback-Reihenfolge: aktuelle Sprache → English → Schlüssel selbst.
    /// </summary>
    public static string T(string schluessel)
    {
        if (_uebersetzungen.TryGetValue(schluessel, out var wert))
            return wert;

        // Fallback auf English
        if (Sprache != "en")
        {
            var enDict = SpracheLaden("en");
            if (enDict != null && enDict.TryGetValue(schluessel, out var enWert))
                return enWert;
        }

        return schluessel;
    }

    /// <summary>
    /// Lädt eine Sprache aus der JSON-Datei. Verwendet einen Cache.
    /// </summary>
    private static Dictionary<string, string>? SpracheLaden(string sprache)
    {
        if (_cache.TryGetValue(sprache, out var cached))
            return cached;

        var dict = JsonLaden(sprache);
        if (dict != null)
        {
            _cache[sprache] = dict;
        }
        return dict;
    }

    /// <summary>
    /// Lädt die JSON-Datei für die gegebene Sprache aus dem i18n-Ordner.
    /// </summary>
    private static Dictionary<string, string>? JsonLaden(string sprache)
    {
        // Pfad zum i18n-Ordner — relativ zur Assembly
        string basisPfad = PfadErmitteln();
        string dateiPfad = Path.Combine(basisPfad, "Assets", "i18n", $"{sprache}.json");

        if (!File.Exists(dateiPfad))
        {
            // Fallback: Embedded Resource (bei SingleFile-Publish)
            dateiPfad = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "i18n", $"{sprache}.json");
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

    /// <summary>
    /// Ermittelt den Basis-Pfad der Anwendung.
    /// </summary>
    private static string PfadErmitteln()
    {
        // Bei SingleFile-Publish: AppContext.BaseDirectory zeigt auf das extrahierte Verzeichnis
        return System.AppContext.BaseDirectory;
    }

    /// <summary>
    /// Ermittelt verfügbare Sprachen aus dem i18n-Ordner.
    /// </summary>
    private static void VerfuegbareSprachenErmitteln()
    {
        var pfad = Path.Combine(PfadErmitteln(), "Assets", "i18n");
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