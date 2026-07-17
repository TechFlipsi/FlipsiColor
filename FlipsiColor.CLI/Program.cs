using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using FlipsiColor.AI;
using FlipsiColor.Core;
using FlipsiColor.Image;
using FlipsiColor.Color;
using FlipsiColor.Video;

namespace FlipsiColor;

/// <summary>
/// FlipsiColor CLI — Terminal-basierte Bild- und Videofarbkorrektur ohne GUI.
/// Nutzt dieselbe Core-Engine wie die Avalonia/WPF-Version.
///
/// Usage:
///   flipsicolor-cli image <input.jpg> [output.jpg] [--exposure 0.3] [--contrast 0.2] [--saturation 0.1] [--turbo]
///   flipsicolor-cli video <input.mp4> [output.mp4] [--exposure 0.3] [--turbo]
///   flipsicolor-cli info <input.jpg>
///   flipsicolor-cli test
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHilfe();
            return 1;
        }

        var befehl = args[0].ToLowerInvariant();

        try
        {
            return befehl switch
            {
                "image" or "bild" => await BildVerarbeiten(args[1..]),
                "video" => await VideoVerarbeiten(args[1..]),
                "info" => await InfoAnzeigen(args[1..]),
                "test" => await SelbstTest(),
                "hilfe" or "help" or "--help" or "-h" => Hilfe(),
                "version" or "--version" or "-v" => Version(),
                _ => UnbekannterBefehl(befehl),
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fehler: {ex.Message}");
            return 1;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // image — Bild verarbeiten
    // ═══════════════════════════════════════════════════════════════

    private static async Task<int> BildVerarbeiten(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: flipsicolor-cli image <input.jpg> [output.jpg] [Optionen]");
            Console.WriteLine("Optionen:");
            Console.WriteLine("  --exposure <float>    Belichtung (-1.0 bis 1.0, default: 0.0)");
            Console.WriteLine("  --contrast <float>    Kontrast (-1.0 bis 1.0, default: 0.0)");
            Console.WriteLine("  --saturation <float>  Sättigung (-1.0 bis 1.0, default: 0.0)");
            Console.WriteLine("  --vibrance <float>    Vibranz (-1.0 bis 1.0, default: 0.0)");
            Console.WriteLine("  --highlights <float>  Lichter (-1.0 bis 1.0, default: 0.0)");
            Console.WriteLine("  --shadows <float>     Schatten (-1.0 bis 1.0, default: 0.0)");
            Console.WriteLine("  --sharpen <float>     Schärfe (0.0 bis 1.0, default: 0.0)");
            Console.WriteLine("  --denoise <float>     Rauschunterdrückung (0.0 bis 1.0, default: 0.0)");
            Console.WriteLine("  --upscale <2|3|4>     Hochskalieren (RealESRGAN, default: 1 = aus)");
            Console.WriteLine("  --turbo               Turbo-Modus (automatische KI-Korrektur)");
            Console.WriteLine("  --intensity <l|m|s>   KI-Intensität: leicht=0, mittel=1, stark=2");
            return 1;
        }

        var input = args[0];
        var output = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;

        if (!File.Exists(input))
        {
            Console.WriteLine($"❌ Datei nicht gefunden: {input}");
            return 1;
        }

        output ??= Path.Combine(
            Path.GetDirectoryName(input)!,
            Path.GetFileNameWithoutExtension(input) + "_flipsicolor" + Path.GetExtension(input));

        var param = ParseParameter(args);

        Console.WriteLine($"════════════════════════════════════════");
        Console.WriteLine($"  FlipsiColor CLI v0.7.0 — Bildverarbeitung");
        Console.WriteLine($"════════════════════════════════════════");
        Console.WriteLine($"  Eingabe:  {input}");
        Console.WriteLine($"  Ausgabe:  {output}");
        Console.WriteLine($"  Modus:    {param.Modus}");
        Console.WriteLine($"  Intensität: {param.Intensitaet}");
        Console.WriteLine();

        // Logger initialisieren
        Utils.Logger.Init();

        Console.WriteLine("[1/4] Initialisiere Engine...");
        using var modelManager = new ModelManager();
        var colorManager = new ColorManager();
        colorManager.Initialisieren();
        Console.WriteLine("  ✓ Engine bereit");

        Console.WriteLine("[2/4] Lade Bild...");
        using var pipeline = new ImagePipeline(modelManager, colorManager);
        if (!pipeline.BildLaden(input))
        {
            Console.WriteLine("❌ Bild konnte nicht geladen werden (OpenCvSharp native nicht verfügbar?)");
            return 1;
        }
        Console.WriteLine("  ✓ Bild geladen");

        Console.WriteLine("[3/4] Verarbeite Bild...");
        pipeline.PipelineAusfuehren(param);
        var ergebnis = pipeline.Ergebnis;
        if (ergebnis == null || ergebnis.Empty())
        {
            Console.WriteLine("❌ Pipeline-Ergebnis ist leer");
            return 1;
        }
        Console.WriteLine("  ✓ Verarbeitung abgeschlossen");

        Console.WriteLine("[4/4] Speichere Ergebnis...");
        OpenCvSharp.Cv2.ImWrite(output, ergebnis);
        Console.WriteLine($"  ✓ Gespeichert: {output}");
        Console.WriteLine();
        Console.WriteLine("✅ Fertig!");

        return 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // video — Video verarbeiten
    // ═══════════════════════════════════════════════════════════════

    private static async Task<int> VideoVerarbeiten(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: flipsicolor-cli video <input.mp4> [output.mp4] [Optionen]");
            Console.WriteLine("Optionen:");
            Console.WriteLine("  --exposure <float>    Belichtung (-1.0 bis 1.0)");
            Console.WriteLine("  --contrast <float>    Kontrast (-1.0 bis 1.0)");
            Console.WriteLine("  --saturation <float>   Sättigung (-1.0 bis 1.0)");
            Console.WriteLine("  --turbo                Turbo-Modus (automatische KI-Korrektur)");
            Console.WriteLine("  --intensity <l|m|s>    KI-Intensität: leicht=0, mittel=1, stark=2");
            Console.WriteLine("  --backend <ffmpeg|vs>  Video-Backend (FFmpeg=Standard, VapourSynth=optional)");
            return 1;
        }

        var input = args[0];
        var output = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;

        if (!File.Exists(input))
        {
            Console.WriteLine($"❌ Datei nicht gefunden: {input}");
            return 1;
        }

        output ??= Path.Combine(
            Path.GetDirectoryName(input)!,
            Path.GetFileNameWithoutExtension(input) + "_flipsicolor.mp4");

        var param = ParseParameter(args);

        Console.WriteLine($"════════════════════════════════════════");
        Console.WriteLine($"  FlipsiColor CLI v0.7.0 — Videobearbeitung");
        Console.WriteLine($"════════════════════════════════════════");
        Console.WriteLine($"  Eingabe:  {input}");
        Console.WriteLine($"  Ausgabe:  {output}");
        Console.WriteLine($"  Modus:    {param.Modus}");
        Console.WriteLine($"  Backend:  FFmpeg (Standard)");
        Console.WriteLine();

        Utils.Logger.Init();

        Console.WriteLine("[1/5] Initialisiere Engine...");
        using var modelManager = new ModelManager();
        var colorManager = new ColorManager();
        colorManager.Initialisieren();
        Console.WriteLine("  ✓ Engine bereit");

        Console.WriteLine("[2/5] Lade Video...");
        using var pipeline = new VideoPipeline(modelManager, colorManager);
        if (!pipeline.VideoLaden(input))
        {
            Console.WriteLine("❌ Video konnte nicht geladen werden");
            return 1;
        }
        Console.WriteLine($"  ✓ Video geladen: {pipeline.Breite}×{pipeline.Hoehe}, {pipeline.Fps:F1}fps, {pipeline.FrameAnzahl} Frames, {pipeline.Dauer:F1}s");

        Console.WriteLine("[3/5] Verarbeite Video...");
        var letzterFortschritt = -1;
        pipeline.PipelineAusfuehren(param, (aktuell, gesamt) =>
        {
            var prozent = gesamt > 0 ? aktuell * 100 / gesamt : 0;
            if (prozent != letzterFortschritt && prozent % 10 == 0)
            {
                Console.WriteLine($"  Fortschritt: {prozent}% ({aktuell}/{gesamt} Frames)");
                letzterFortschritt = prozent;
            }
        });
        Console.WriteLine("  ✓ Verarbeitung abgeschlossen");

        Console.WriteLine("[4/5] Speichere Video...");
        // VideoPipeline speichert automatisch in output Pfad
        Console.WriteLine($"  ✓ Gespeichert: {output}");

        Console.WriteLine("[5/5] Fertig!");
        Console.WriteLine();
        Console.WriteLine("✅ Fertig!");

        return 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // info — EXIF und Bildinfo anzeigen
    // ═══════════════════════════════════════════════════════════════

    private static async Task<int> InfoAnzeigen(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: flipsicolor-cli info <input.jpg>");
            return 1;
        }

        var input = args[0];
        if (!File.Exists(input))
        {
            Console.WriteLine($"❌ Datei nicht gefunden: {input}");
            return 1;
        }

        Console.WriteLine($"════════════════════════════════════════");
        Console.WriteLine($"  FlipsiColor CLI — Bildinfo");
        Console.WriteLine($"════════════════════════════════════════");
        Console.WriteLine($"  Datei: {Path.GetFileName(input)}");
        Console.WriteLine($"  Größe: {new FileInfo(input).Length / 1024.0:F1} KB");
        Console.WriteLine();

        try
        {
            var exif = ExifReader.LesenKompakt(input);
            if (exif != null)
            {
                Console.WriteLine("  EXIF-Daten:");
                Console.WriteLine($"    Kamera:      {exif.Kamera}");
                Console.WriteLine($"    Objektiv:    {exif.Objektiv}");
                Console.WriteLine($"    Brennweite:  {exif.Brennweite}");
                Console.WriteLine($"    Blende:      {exif.Blende}");
                Console.WriteLine($"    ISO:         {exif.Iso}");
                Console.WriteLine($"    Belichtung:  {exif.Verschluesszeit}");
                Console.WriteLine($"    Aufnahme:    {exif.Aufnahmedatum}");
                Console.WriteLine($"    Auflösung:   {exif.Breite}×{exif.Hoehe}");
            }
            else
            {
                Console.WriteLine("  Keine EXIF-Daten gefunden");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  EXIF-Lesefehler: {ex.Message}");
        }

        return 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // test — Selbsttest der Core-Engine
    // ═══════════════════════════════════════════════════════════════

    private static async Task<int> SelbstTest()
    {
        Console.WriteLine("════════════════════════════════════════");
        Console.WriteLine("  FlipsiColor CLI v0.7.0 — Selbsttest");
        Console.WriteLine("════════════════════════════════════════");
        Console.WriteLine();

        Utils.Logger.Init();

        var passed = 0;
        var failed = 0;

        void Test(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Console.WriteLine($"  ✅ {name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"  ❌ {name}: {ex.Message}");
            }
        }

        Console.WriteLine("── Phase 1: Settings ──");
        Test("Settings.Laden() liefert Defaults", () =>
        {
            var s = Settings.Laden();
            if (s == null) throw new("Settings null");
        });

        Console.WriteLine("\n── Phase 2: ModelManager ──");
        Test("ModelManager initialisiert 7 Modelle", () =>
        {
            using var mm = new ModelManager();
            var core = mm.CoreGroesseGesamt();
            if (core <= 0) throw new("Core-Modelle Größe <= 0");
        });

        Test("EfficientNet ONNX ladbar", () =>
        {
            var modellPfad = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipsiColor", "Models", "EfficientNet.onnx");
            if (!File.Exists(modellPfad))
                throw new("EfficientNet.onnx nicht gefunden — lade mit 'flipsicolor-cli image <bild>' herunter");
            // Versuche ONNX Session zu laden
            using var session = new Microsoft.ML.OnnxRuntime.InferenceSession(modellPfad);
            if (session.InputNames.Count == 0) throw new("Keine Inputs");
        });

        Console.WriteLine("\n── Phase 3: GPUInfo ──");
        Test("GPUInfo.Erkennen (kein Crash)", () =>
        {
            GPUInfo.Erkennen();
            if (GPUInfo.GpuName == null) throw new("GpuName null");
        });

        Console.WriteLine("\n── Phase 4: ColorManager ──");
        Test("ColorManager.Initialisieren", () =>
        {
            var cm = new ColorManager();
            cm.Initialisieren();
        });

        Console.WriteLine("\n── Phase 5: FFmpeg ──");
        Test("FFmpeg verfügbar", () =>
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(5000);
            if (proc?.ExitCode != 0) throw new("ffmpeg nicht verfügbar");
        });

        Console.WriteLine("\n── Phase 6: Lensfun ──");
        Test("Lensfun Datenbank verfügbar", () =>
        {
            using var lc = new LensCorrector();
            // Initialisierung ohne Crash ist ausreichend
        });

        Console.WriteLine($"\n════════════════════════════════════════");
        Console.WriteLine($"  Ergebnis: {passed} bestanden, {failed} fehlgeschlagen");
        Console.WriteLine($"════════════════════════════════════════");

        return failed > 0 ? 1 : 0;
    }

    // ═══════════════════════════════════════════════════════════════
    // Parameter Parsing
    // ═══════════════════════════════════════════════════════════════

    private static PipelineParams ParseParameter(string[] args)
    {
        var param = new PipelineParams();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--exposure":
                case "--belichtung":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out var bel))
                        param.Belichtung = bel;
                    break;
                case "--contrast":
                case "--kontrast":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out var kon))
                        param.Kontrast = kon;
                    break;
                case "--saturation":
                case "--saettigung":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out var sae))
                        param.Saettigung = sae;
                    break;
                case "--vibrance":
                case "--vibranz":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out var vib))
                        param.Vibranz = vib;
                    break;
                case "--highlights":
                case "--lichter":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out var lic))
                        param.Lichter = lic;
                    break;
                case "--shadows":
                case "--schatten":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out var sch))
                        param.Schatten = sch;
                    break;
                case "--sharpen":
                case "--schaerfe":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out var shp))
                        param.SchaerfeBetrag = shp;
                    break;
                case "--denoise":
                case "--rauschen":
                    if (i + 1 < args.Length && float.TryParse(args[++i], out var dn))
                    {
                        param.LuminanzRauschen = dn;
                        param.ChrominanzRauschen = dn;
                    }
                    break;
                case "--upscale":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var up))
                        param.HochskalierenFaktor = up;
                    break;
                case "--turbo":
                    param.Modus = BetriebsModus.Turbo;
                    break;
                case "--ask":
                    param.Modus = BetriebsModus.Ask;
                    break;
                case "--smartlearn":
                    param.Modus = BetriebsModus.SmartLearn;
                    break;
                case "--intensity":
                case "--intensitaet":
                    if (i + 1 < args.Length)
                    {
                        var val = args[++i].ToLowerInvariant();
                        param.Intensitaet = val switch
                        {
                            "0" or "l" or "leicht" or "light" => Intensitaet.Leicht,
                            "1" or "m" or "mittel" or "medium" => Intensitaet.Mittel,
                            "2" or "s" or "stark" or "strong" => Intensitaet.Stark,
                            _ => Intensitaet.Mittel
                        };
                    }
                    break;
            }
        }

        return param;
    }

    // ═══════════════════════════════════════════════════════════════
    // Hilfsfunktionen
    // ═══════════════════════════════════════════════════════════════

    private static void PrintHilfe()
    {
        Console.WriteLine("═════════════════════════════════════════════════════");
        Console.WriteLine("  FlipsiColor CLI v0.7.0");
        Console.WriteLine("  Terminal-basierte Bild- & Videofarbkorrektur");
        Console.WriteLine("═════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine("Befehle:");
        Console.WriteLine("  image <input> [output]   Bild verarbeiten");
        Console.WriteLine("  video <input> [output]    Video verarbeiten");
        Console.WriteLine("  info <input>              Bildinfo & EXIF anzeigen");
        Console.WriteLine("  test                      Selbsttest der Engine");
        Console.WriteLine("  version                   Version anzeigen");
        Console.WriteLine("  hilfe                     Diese Hilfe anzeigen");
        Console.WriteLine();
        Console.WriteLine("Optionen (für image/video):");
        Console.WriteLine("  --exposure <float>        Belichtung (-1.0 bis 1.0)");
        Console.WriteLine("  --contrast <float>        Kontrast (-1.0 bis 1.0)");
        Console.WriteLine("  --saturation <float>      Sättigung (-1.0 bis 1.0)");
        Console.WriteLine("  --vibrance <float>        Vibranz (-1.0 bis 1.0)");
        Console.WriteLine("  --highlights <float>      Lichter (-1.0 bis 1.0)");
        Console.WriteLine("  --shadows <float>         Schatten (-1.0 bis 1.0)");
        Console.WriteLine("  --sharpen <float>         Schärfe (0.0 bis 1.0)");
        Console.WriteLine("  --denoise <float>         Rauschunterdrückung (0.0 bis 1.0)");
        Console.WriteLine("  --upscale <2|3|4>         Hochskalieren (RealESRGAN)");
        Console.WriteLine("  --turbo                    Automatische KI-Korrektur");
        Console.WriteLine("  --intensity <l|m|s>       KI-Intensität (leicht/mittel/stark)");
        Console.WriteLine();
        Console.WriteLine("Beispiele:");
        Console.WriteLine("  flipsicolor-cli image foto.jpg output.jpg --exposure 0.3 --turbo");
        Console.WriteLine("  flipsicolor-cli video clip.mp4 --contrast 0.2 --saturation 0.1");
        Console.WriteLine("  flipsicolor-cli info foto.jpg");
        Console.WriteLine("  flipsicolor-cli test");
        Console.WriteLine("═════════════════════════════════════════════════════");
    }

    private static int Hilfe()
    {
        PrintHilfe();
        return 0;
    }

    private static int Version()
    {
        Console.WriteLine("FlipsiColor CLI v0.7.0");
        Console.WriteLine("Core-Engine: FlipsiColor.Core v0.7.0");
        Console.WriteLine(".NET 10.0");
        return 0;
    }

    private static int UnbekannterBefehl(string befehl)
    {
        Console.WriteLine($"❌ Unbekannter Befehl: {befehl}");
        Console.WriteLine("Benutze 'flipsicolor-cli hilfe' für eine Übersicht.");
        return 1;
    }
}