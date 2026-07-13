using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

using FlipsiColor.Core;
using FlipsiColor.Utils;

namespace FlipsiColor.Color;

/// <summary>
/// OCIO-Manager — OpenColorIO Farbmanagement-Integration.
///
/// Zwei Engine-Modi:
/// 1. LUTBaking (Standard): Bakt die OCIO-Transform via ociobakelut CLI in eine .cube LUT
///    und wendet sie über die bestehende StyleLUT-Klasse an. Keine native Library nötig.
/// 2. Native (Optional): Direkter OCIO-Processor über C# Bindings (OCIOSharp/libOpenColorIO).
///    Höchste Präzision, erfordert aber libOpenColorIO.so/.dll zur Laufzeit.
///
/// Fallback-Strategie: Wenn OCIO nicht verfügbar ist → Standard Farbmanagement (ProPhoto RGB).
/// </summary>
public sealed class OCIOManager : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<OCIOManager>();

    private readonly OCIOConfigParser _parser = new();
    private OCIOConfigParser.OCIOConfigDaten? _configDaten;
    private string? _gebackteLutPfad;
    private string? _letzterTransformHash;
    private bool _disposed;

    /// <summary>True wenn eine gültige OCIO Config geladen wurde.</summary>
    public bool IstBereit => _configDaten != null;

    /// <summary>True wenn ociobakelut CLI im PATH verfügbar ist.</summary>
    public static bool IstLutBakingVerfuegbar => IstBefehlVerfuegbar("ociobakelut");

    /// <summary>True wenn libOpenColorIO zur Laufzeit gefunden wurde.</summary>
    public static bool IstNativeVerfuegbar => PruefeNativeVerfuegbarkeit();

    /// <summary>
    /// Lädt eine OCIO Config-Datei und parst die verfügbaren Optionen.
    /// </summary>
    public bool ConfigLaden(string configPfad)
    {
        _configDaten = _parser.Laden(configPfad);
        if (_configDaten == null)
        {
            Log.Warning("OCIO: Config konnte nicht geladen werden");
            return false;
        }

        // Config-Cache invalidieren
        _gebackteLutPfad = null;
        _letzterTransformHash = null;

        Log.Information("OCIO Config geladen: {Pfad} — {CS} Color Spaces, {Displays} Displays",
            SecurityValidator.BereinigePfadFuerLog(configPfad),
            _configDaten.ColorSpaces.Count, _configDaten.Displays.Count);

        return true;
    }

    /// <summary>
    /// Gibt die verfügbaren Color Spaces zurück (für UI-Dropdowns).
    /// </summary>
    public List<(string Name, string Family, bool IsData)> ColorSpacesAuflisten()
    {
        return _configDaten?.ColorSpaces ?? new List<(string, string, bool)>();
    }

    /// <summary>
    /// Gibt die verfügbaren Displays zurück (für UI-Dropdowns).
    /// </summary>
    public List<string> DisplaysAuflisten()
    {
        return _configDaten?.Displays ?? new List<string>();
    }

    /// <summary>
    /// Gibt die verfügbaren Views für ein Display zurück (für UI-Dropdowns).
    /// </summary>
    public List<string> ViewsAuflisten(string display)
    {
        if (_configDaten == null || !_configDaten.Views.TryGetValue(display, out var views))
            return new List<string>();
        return views;
    }

    /// <summary>
    /// Gibt die verfügbaren Looks zurück (für UI-Dropdowns).
    /// </summary>
    public List<(string Name, string Description)> LooksAuflisten()
    {
        return _configDaten?.Looks ?? new List<(string, string)>();
    }

    /// <summary>
    /// Bakt die OCIO-Transform (Source → Display/View + Look) in eine .cube LUT-Datei.
    /// Verwendet ociobakelut CLI. Die LUT wird gecacht — nur bei geändertem Transform neu gebacken.
    ///
    /// Aufruf:
    ///   ociobakelut --inputspace "ACEScg" --outputspace "sRGB" --display "sRGB" --view "Filmic" \
    ///              --look "LookName" --format cube output.cube
    /// </summary>
    /// <param name="param">Pipeline-Parameter mit OCIO-Konfiguration.</param>
    /// <returns>Pfad zur gebackenen .cube LUT, oder null bei Fehler.</returns>
    public string? LutBacken(PipelineParams param)
    {
        if (_configDaten == null)
        {
            Log.Warning("OCIO: Keine Config geladen — LUT-Baking übersprungen");
            return null;
        }

        if (!IstLutBakingVerfuegbar)
        {
            Log.Warning("OCIO: ociobakelut nicht im PATH — LUT-Baking nicht verfügbar");
            return null;
        }

        // Transform-Hash für Caching berechnen
        var transformKey = $"{_configDaten.ConfigPfad}|{param.OCIOSourceColorSpace}|{param.OCIODisplay}|{param.OCIOView}|{param.OCIOLook}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(transformKey));
        var hashHex = Convert.ToHexString(hash)[..16];

        // Cache-Hit: gleicher Transform wie letztes Mal
        if (_letzterTransformHash == hashHex && _gebackteLutPfad != null && File.Exists(_gebackteLutPfad))
        {
            Log.Debug("OCIO: LUT-Cache-Hit (Hash={Hash})", hashHex);
            return _gebackteLutPfad;
        }

        // Temporäre LUT-Datei
        var tempDir = Path.Combine(Path.GetTempPath(), "FlipsiColor-OCIO");
        Directory.CreateDirectory(tempDir);
        var lutPfad = Path.Combine(tempDir, $"ocio_{hashHex}.cube");

        try
        {
            // ociobakelut Argumente bauen
            var args = new System.Collections.Generic.List<string>
            {
                "--inputspace", param.OCIOSourceColorSpace ?? "ACEScg",
                "--outputspace", param.OCIODisplay ?? "sRGB"
            };

            // Display/View (OCIO 2.x Display Transform)
            if (!string.IsNullOrEmpty(param.OCIODisplay) && !string.IsNullOrEmpty(param.OCIOView))
            {
                args.AddRange(new[] { "--display", param.OCIODisplay, "--view", param.OCIOView });
            }

            // Optional: Look
            if (!string.IsNullOrEmpty(param.OCIOLook))
            {
                args.AddRange(new[] { "--look", param.OCIOLook });
            }

            // Format + Ausgabedatei
            args.AddRange(new[] { "--format", "cube", lutPfad });

            // OCIO-Config via Umgebungsvariable setzen
            var psi = new ProcessStartInfo
            {
                FileName = "ociobakelut",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

            // OCIO-Config-Pfad via Umgebungsvariable
            psi.EnvironmentVariables["OCIO"] = _configDaten.ConfigPfad;

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(30000); // 30s Timeout

            if (proc.ExitCode != 0 || !File.Exists(lutPfad))
            {
                Log.Error("OCIO: ociobakelut fehlgeschlagen (Exit={Exit}): {Error}", proc.ExitCode, stderr);
                return null;
            }

            _gebackteLutPfad = lutPfad;
            _letzterTransformHash = hashHex;

            Log.Information("OCIO: LUT gebacken — {Pfad} (Hash={Hash})",
                SecurityValidator.BereinigePfadFuerLog(lutPfad), hashHex);

            return lutPfad;
        }
        catch (Exception ex)
        {
            Log.Error("OCIO: LUT-Baking fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return null;
        }
    }

    /// <summary>
    /// Gibt den Pfad zur aktuell gebackenen LUT zurück (ohne neu zu backen).
    /// </summary>
    public string? AktuelleLutPfad => _gebackteLutPfad;

    /// <summary>
    /// Prüft ob ociobakelut im PATH verfügbar ist.
    /// </summary>
    private static bool IstBefehlVerfuegbar(string befehl)
    {
        try
        {
            var checker = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = new ProcessStartInfo
            {
                FileName = checker,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add(befehl);
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prüft ob libOpenColorIO zur Laufzeit geladen werden kann.
    /// Windows: OpenColorIO.dll, Linux: libOpenColorIO.so
    /// </summary>
    private static bool PruefeNativeVerfuegbarkeit()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var pfade = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenColorIO", "bin", "OpenColorIO.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "OpenColorIO", "bin", "OpenColorIO.dll"),
                };

                foreach (var p in pfade)
                    if (File.Exists(p)) return true;

                return System.Runtime.InteropServices.NativeLibrary.TryLoad("OpenColorIO", out _);
            }
            else
            {
                // Linux: System-Bibliothek suchen
                var libPfade = new[] { "/usr/lib/libOpenColorIO.so", "/usr/local/lib/libOpenColorIO.so", "/usr/lib/x86_64-linux-gnu/libOpenColorIO.so" };
                foreach (var p in libPfade)
                    if (File.Exists(p)) return true;

                return System.Runtime.InteropServices.NativeLibrary.TryLoad("libOpenColorIO.so", out _);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gibt einen Hinweis-Text für das UI zurück, wenn OCIO nicht verfügbar ist.
    /// </summary>
    public static string InstallationsHinweis(OCIOEngine engine)
    {
        if (engine == OCIOEngine.LUTBaking)
        {
            return OperatingSystem.IsWindows()
                ? "ociobakelut nicht im PATH.\n\nInstallation:\n1. OpenColorIO von opencolorio.org herunterladen\n2. Bin-Verzeichnis zum PATH hinzufügen\n\nAlternative: Standard Farbmanagement verwenden."
                : "ociobakelut nicht installiert.\n\nInstallation:\n  sudo apt install opencolorio-tools  (Ubuntu/Debian)\n  oder: pip install opencolorio\n\nAlternative: Standard Farbmanagement verwenden.";
        }
        else
        {
            return OperatingSystem.IsWindows()
                ? "libOpenColorIO nicht gefunden.\n\nInstallation:\n1. OpenColorIO von opencolorio.org herunterladen\n2. OpenColorIO.dll in System32 oder App-Verzeichnis kopieren\n\nAlternative: LUT-Baking-Modus verwenden."
                : "libOpenColorIO nicht installiert.\n\nInstallation:\n  sudo apt install libopencolorio-dev  (Ubuntu/Debian)\n\nAlternative: LUT-Baking-Modus verwenden.";
        }
    }

    /// <summary>
    /// Erstellt eine vereinfachte Standard-ACES-Config im App-Verzeichnis,
    /// falls keine eigene Config geladen wurde.
    /// </summary>
    public static string? DefaultConfigErstellen()
    {
        try
        {
            var appDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipsiColor", "OCIO");

            Directory.CreateDirectory(appDir);
            var configPfad = Path.Combine(appDir, "default.ocio");

            if (File.Exists(configPfad))
                return configPfad; // Bereits vorhanden

            // Vereinfachte ACES-kompatible Config
            var config = """
ocio_config_version: 2

environment:
  {}

search_path: "."
strictparsing: false

roles:
  color_picking: sRGB
  color_timing: ACEScg
  compositing_log: ACEScct
  data: Raw
  matte_paint: sRGB
  reference: ACES2065-1
  rendering: ACEScg
  scene_linear: ACEScg

colorspaces:
  - !<ColorSpace>
    name: Raw
    family: Utility
    isdata: true

  - !<ColorSpace>
    name: sRGB
    family: Utility

  - !<ColorSpace>
    name: linear Rec.709
    family: Utility

  - !<ColorSpace>
    name: Rec.709
    family: Utility

  - !<ColorSpace>
    name: ACES2065-1
    family: ACES

  - !<ColorSpace>
    name: ACEScg
    family: ACES

  - !<ColorSpace>
    name: ACEScct
    family: ACES

displays:
  - !<Display>
    name: sRGB
    views:
      - !<View>
        name: Filmic
        colorspace: sRGB
      - !<View>
        name: Raw
        colorspace: Raw

looks:
  - !<Look>
    name: Neutral
    process_space: ACEScg
""";

            File.WriteAllText(configPfad, config);
            Log.Information("OCIO: Default-Config erstellt: {Pfad}",
                SecurityValidator.BereinigePfadFuerLog(configPfad));
            return configPfad;
        }
        catch (Exception ex)
        {
            Log.Error("OCIO: Default-Config konnte nicht erstellt werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}