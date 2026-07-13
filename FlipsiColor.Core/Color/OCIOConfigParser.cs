using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using FlipsiColor.Utils;

namespace FlipsiColor.Color;

/// <summary>
/// OCIO Config-Parser — liest .ocio Config-Dateien (YAML-Format) und extrahiert
/// verfügbare Color Spaces, Displays, Views, Looks und Roles.
///
/// .ocio Dateien sind YAML-konfiguriert. Dieser Parser ist ein vereinfachter
/// YAML-Reader der nur die für FlipsiColor relevanten Abschnitte extrahiert:
/// - colorspaces (Name + Family + IsData + Encoding)
/// - displays + views (Display/View Transform Paare)
/// - looks (Creative Grades)
/// - roles (Aliases wie scene_linear, color_picking, etc.)
///
/// Vollständiges YAML-Parsing wäre Overkill — wir brauchen nur Listen von Namen.
/// </summary>
public sealed class OCIOConfigParser
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<OCIOConfigParser>();

    /// <summary>
    /// Geparste OCIO Config-Daten
    /// </summary>
    public sealed class OCIOConfigDaten
    {
        /// <summary>Alle verfügbaren Color Spaces (Name → Family)</summary>
        public List<(string Name, string Family, bool IsData)> ColorSpaces { get; set; } = new();

        /// <summary>Verfügbare Displays (z.B. sRGB, Rec.2020, HDR)</summary>
        public List<string> Displays { get; set; } = new();

        /// <summary>Verfügbare Views pro Display (z.B. Filmic, ACES, Raw)</summary>
        public Dictionary<string, List<string>> Views { get; set; } = new();

        /// <summary>Verfügbare Looks (Creative Grades)</summary>
        public List<(string Name, string Description)> Looks { get; set; } = new();

        /// <summary>Roles (Aliases → Color Space Name)</summary>
        public Dictionary<string, string> Roles { get; set; } = new();

        /// <summary>Pfad zur .ocio Config-Datei</summary>
        public string ConfigPfad { get; set; } = string.Empty;

        /// <summary>Version der OCIO Config (z.B. "2.0")</summary>
        public string Version { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parst eine .ocio Config-Datei und gibt die verfügbaren Optionen zurück.
    /// Wirft keine Exceptions bei Parse-Fehlern — gibt null zurück.
    /// </summary>
    public OCIOConfigDaten? Laden(string configPfad)
    {
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(configPfad,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ocio", ".config" });

        if (validierterPfad == null)
        {
            Log.Warning("OCIO Config: Pfad-Validierung fehlgeschlagen");
            return null;
        }

        try
        {
            if (!File.Exists(validierterPfad))
            {
                Log.Warning("OCIO Config: Datei nicht gefunden: {Pfad}",
                    SecurityValidator.BereinigePfadFuerLog(validierterPfad));
                return null;
            }

            var lines = File.ReadAllLines(validierterPfad);
            var daten = ParseLines(lines);
            daten.ConfigPfad = validierterPfad;

            Log.Information("OCIO Config geladen: {ColorSpaces} Color Spaces, {Displays} Displays, {Looks} Looks",
                daten.ColorSpaces.Count, daten.Displays.Count, daten.Looks.Count);

            return daten;
        }
        catch (Exception ex)
        {
            Log.Error("OCIO Config konnte nicht geladen werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return null;
        }
    }

    /// <summary>
    /// Parst die Zeilen einer .ocio Config-Datei.
    /// Vereinfachter YAML-Reader — extrahiert nur die relevanten Abschnitte.
    /// </summary>
    private OCIOConfigDaten ParseLines(string[] lines)
    {
        var daten = new OCIOConfigDaten();
        var currentSection = "";
        var currentDisplay = "";

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Kommentare und Leerzeilen überspringen
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            var indent = line.Length - line.TrimStart().Length;

            // Top-Level Schlüssel erkennen (Indent 0, enthält ':')
            if (indent == 0 && trimmed.Contains(':'))
            {
                var colonIdx = trimmed.IndexOf(':');
                var key = trimmed[..colonIdx].Trim();
                var value = trimmed[(colonIdx + 1)..].Trim();

                currentSection = key;

                switch (key)
                {
                    case "ocio_config_version":
                        daten.Version = value.Trim('"', '\'');
                        break;

                    case "displays":
                        // Displays-Abschnitt folgt in eingerückten Zeilen
                        break;

                    case "colorspaces":
                        // Color Spaces folgen als Liste
                        break;

                    case "looks":
                        // Looks folgen als Liste
                        break;

                    case "roles":
                        // Roles folgen als Key-Value Paare
                        break;
                }
            }

            // Color Spaces parsen (Liste von "- name: ...")
            if (currentSection == "colorspaces" && indent > 0)
            {
                if (trimmed.StartsWith("- !"))
                {
                    // OCIO 2.x Format: "- !<ColorSpace> name: ..."
                    // Nächste Zeilen enthalten name, family, etc.
                }
                else if (trimmed.StartsWith("- name:") || trimmed.StartsWith("- !<ColorSpace>"))
                {
                    var nameMatch = Regex.Match(trimmed, @"name:\s*(.+?)(?:\s+#|$)");
                    if (nameMatch.Success)
                    {
                        var name = nameMatch.Groups[1].Value.Trim().Trim('"', '\'');
                        daten.ColorSpaces.Add((name, "", false));
                    }
                }
                else if (trimmed.StartsWith("name:") && daten.ColorSpaces.Count > 0)
                {
                    // Letztem Color Space den Namen geben (YAML mit separatem name: Feld)
                    var name = trimmed["name:".Length..].Trim().Trim('"', '\'');
                    if (!string.IsNullOrEmpty(name))
                    {
                        var last = daten.ColorSpaces[^1];
                        daten.ColorSpaces[^1] = (name, last.Family, last.IsData);
                    }
                }
                else if (trimmed.StartsWith("family:"))
                {
                    var family = trimmed["family:".Length..].Trim().Trim('"', '\'');
                    if (daten.ColorSpaces.Count > 0)
                    {
                        var last = daten.ColorSpaces[^1];
                        daten.ColorSpaces[^1] = (last.Name, family, last.IsData);
                    }
                }
                else if (trimmed.StartsWith("isdata:"))
                {
                    var isData = trimmed["isdata:".Length..].Trim().Trim('"', '\'');
                    if (daten.ColorSpaces.Count > 0)
                    {
                        var last = daten.ColorSpaces[^1];
                        var isDataBool = isData.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                         isData.Equals("yes", StringComparison.OrdinalIgnoreCase);
                        daten.ColorSpaces[^1] = (last.Name, last.Family, isDataBool);
                    }
                }
            }

            // Roles parsen (Key: Value Paare)
            if (currentSection == "roles" && indent > 0)
            {
                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx > 0)
                {
                    var role = trimmed[..colonIdx].Trim();
                    var cs = trimmed[(colonIdx + 1)..].Trim().Trim('"', '\'');
                    if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(cs))
                        daten.Roles[role] = cs;
                }
            }

            // Displays/Views parsen
            if (currentSection == "displays" && indent > 0)
            {
                if (trimmed.StartsWith("- !<Display>"))
                {
                    // OCIO 2.x Display Format
                }
                else if (trimmed.StartsWith("- name:"))
                {
                    var nameMatch = Regex.Match(trimmed, @"name:\s*(.+?)(?:\s+#|$)");
                    if (nameMatch.Success)
                    {
                        currentDisplay = nameMatch.Groups[1].Value.Trim().Trim('"', '\'');
                        daten.Displays.Add(currentDisplay);
                        daten.Views[currentDisplay] = new List<string>();
                    }
                }
                else if (trimmed.StartsWith("views:") && !string.IsNullOrEmpty(currentDisplay))
                {
                    // Views folgen in nächsten Zeilen
                }
                else if (trimmed.StartsWith("- !<View>") && !string.IsNullOrEmpty(currentDisplay))
                {
                    // OCIO 2.x View Format
                }
                else if (trimmed.StartsWith("- name:") == false &&
                         trimmed.StartsWith("- ") &&
                         !string.IsNullOrEmpty(currentDisplay) &&
                         daten.Views.ContainsKey(currentDisplay))
                {
                    // Einfaches View-Format: "- Filmic" oder "- !<View> name: Filmic"
                    var viewName = trimmed.TrimStart('-', ' ');
                    if (viewName.StartsWith("!<View>"))
                        viewName = viewName["!<View>".Length..].Trim();
                    viewName = viewName.Trim('"', '\'');
                    if (!string.IsNullOrEmpty(viewName))
                        daten.Views[currentDisplay].Add(viewName);
                }
            }

            // Looks parsen
            if (currentSection == "looks" && indent > 0)
            {
                if (trimmed.StartsWith("- name:"))
                {
                    var nameMatch = Regex.Match(trimmed, @"name:\s*(.+?)(?:\s+#|$)");
                    if (nameMatch.Success)
                    {
                        var name = nameMatch.Groups[1].Value.Trim().Trim('"', '\'');
                        daten.Looks.Add((name, ""));
                    }
                }
                else if (trimmed.StartsWith("description:"))
                {
                    var desc = trimmed["description:".Length..].Trim().Trim('"', '\'');
                    if (daten.Looks.Count > 0)
                    {
                        var last = daten.Looks[^1];
                        daten.Looks[^1] = (last.Name, desc);
                    }
                }
            }
        }

        // Wenn keine Color Spaces gefunden wurden, füge Standard-CS hinzu
        if (daten.ColorSpaces.Count == 0)
        {
            Log.Warning("OCIO Config: Keine Color Spaces gefunden — verwende Fallback-Liste");
            daten.ColorSpaces.AddRange(new[]
            {
                ("ACEScg", "ACES", false),
                ("ACES2065-1", "ACES", false),
                ("sRGB", "Utility", false),
                ("linear Rec.709", "Utility", false),
                ("Rec.709", "Utility", false),
            });
        }

        // Wenn keine Displays gefunden wurden, füge Standard hinzu
        if (daten.Displays.Count == 0)
        {
            daten.Displays.Add("sRGB");
            daten.Views["sRGB"] = new List<string> { "Filmic", "ACES", "Raw" };
        }

        return daten;
    }
}