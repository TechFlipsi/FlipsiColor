using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using FlipsiColor.Core;
using FlipsiColor.Utils;

namespace FlipsiColor.Video;

/// <summary>
/// VapourSynth-Processor — optionales Video-Backend für Frame-Level-Processing.
///
/// VapourSynth ist eine Python-basierte Frame-Server-Bibliothek für Video-Filter-Pipelines.
/// Diese Klasse:
///   - Prüft zur Laufzeit ob VapourSynth installiert ist (vsscript.dll auf Windows,
///     libvapoursynth.so auf Linux). Die Software funktioniert auch OHNE VapourSynth.
///   - Generiert VapourSynth Python-Scripts für Filter-Pipelines (Belichtung, Kontrast,
///     Sättigung, Rauschunterdrückung, Schärfung) basierend auf <see cref="PipelineParams"/>.
///   - Integriert KI-Modelle über vs-mlrt (vs-onnxruntime): NAFNet (Entrauschen),
///     RestormerLight (Schärfung), RealESRGAN (Upscaling), RealHATGAN (Upscaling),
///     CodeFormer (Gesichtswiederherstellung), AiLUTTransform (Farbstil-Lernen).
///   - Piped VapourSynth-Output an FFmpeg zur Encoding-Pipeline (stdout → ffmpeg stdin).
///   - Ist vollständig cross-platform (Windows + Linux).
///
/// VapourSynth ist OPTIONAL — ist es nicht installiert, gibt <see cref="IstVerfuegbar"/> false
/// zurück und die VideoPipeline fällt auf FFmpeg-Backend zurück.
/// </summary>
public sealed class VapourSynthProcessor : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<VapourSynthProcessor>();
    private bool _disposed;
    private string? _vspyPath;
    private int _breite, _hoehe;
    private double _fps;
    private int _frameAnzahl;

    /// <summary>True wenn VapourSynth zur Laufzeit gefunden wurde (vsscript.dll / libvapoursynth.so).</summary>
    public bool IstVerfuegbar => PruefeVerfuegbarkeit();

    public int Breite => _breite;
    public int Hoehe => _hoehe;
    public double Fps => _fps;
    public int FrameAnzahl => _frameAnzahl;

    /// <summary>
    /// Modell-Verzeichnis: AppData/Local/FlipsiColor/Models/
    /// Gleiches Verzeichnis wie ModelManager — die Modelle werden vom ModelManager heruntergeladen.
    /// </summary>
    private static string ModellVerzeichnis => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlipsiColor", "Models");

    /// <summary>
    /// Erzeugt den Python-Pfad (Forward-Slash, raw-string kompatibel) für ein ONNX-Modell.
    /// </summary>
    /// <param name="modellName">Dateiname des Modells ohne .onnx Erweiterung.</param>
    /// <returns>Pfad im Format r'C:/Users/.../FlipsiColor/Models/NAFNet.onnx'.</returns>
    private static string ModellPythonPfad(string modellName)
    {
        var pfad = Path.Combine(ModellVerzeichnis, modellName + ".onnx");
        // Forward-Slash für Python-Pfad-Kompatibilität (Cross-Platform)
        return pfad.Replace('\\', '/');
    }

    /// <summary>
    /// Prüft zur Laufzeit ob VapourSynth installiert ist.
    /// Windows: sucht vsscript.dll in Standard-Installationspfaden und PATH.
    /// Linux:   sucht libvapoursynth.so in /usr/lib, /usr/local/lib und ldconfig.
    /// </summary>
    public static bool PruefeVerfuegbarkeit()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Windows: vsscript.dll in Standard-Installationspfaden suchen
                var suchPfade = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VapourSynth", "vsscript.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VapourSynth", "vsscript.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VapourSynth", "vsscript.dll")
                };

                foreach (var pfad in suchPfade)
                {
                    if (File.Exists(pfad))
                    {
                        Log.Debug("VapourSynth gefunden: {Pfad}", pfad);
                        return true;
                    }
                }

                // Zusätzlich: vspipe im PATH prüfen (funktioniert wenn VapourSynth im PATH ist)
                return IstBefehlVerfuegbar("vspipe");
            }
            else
            {
                // Linux/Unix: libvapoursynth.so in Standard-Bibliothekspfaden suchen
                var libPfade = new[]
                {
                    "/usr/lib/libvapoursynth.so",
                    "/usr/lib/x86_64-linux-gnu/libvapoursynth.so",
                    "/usr/local/lib/libvapoursynth.so",
                    "/usr/lib64/libvapoursynth.so"
                };

                foreach (var pfad in libPfade)
                {
                    if (File.Exists(pfad))
                    {
                        Log.Debug("VapourSynth gefunden: {Pfad}", pfad);
                        return true;
                    }
                }

                // ldconfig prüfen (am zuverlässigsten auf Linux)
                if (IstBefehlVerfuegbar("ldconfig"))
                {
                    var psi = SecurityValidator.SichereProcessStartInfo("ldconfig", new[] { "-p" });
                    using var proc = new Process { StartInfo = psi };
                    proc.Start();
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(5000);
                    if (output.Contains("libvapoursynth", StringComparison.Ordinal))
                    {
                        Log.Debug("VapourSynth via ldconfig gefunden");
                        return true;
                    }
                }

                // Fallback: vspipe im PATH
                return IstBefehlVerfuegbar("vspipe");
            }
        }
        catch (Exception ex)
        {
            Log.Debug("VapourSynth-Verfügbarkeitsprüfung fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Prüft ob ein Befehl (Executable) im PATH verfügbar ist.
    /// Windows: where <cmd>, Linux: which <cmd>.
    /// </summary>
    private static bool IstBefehlVerfuegbar(string befehl)
    {
        try
        {
            var checker = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = SecurityValidator.SichereProcessStartInfo(checker, new[] { befehl });
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch (Exception ex)
        {
            Log.Debug("Befehl nicht verfügbar: {Fehler}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Lädt Video-Metadaten via ffprobe (Breite, Höhe, FPS, Frame-Anzahl).
    /// Wird benötigt um das VapourSynth-Script korrekt zu parametrisieren.
    /// </summary>
    /// <param name="videoPfad">Pfad zur Video-Datei.</param>
    /// <returns>True wenn Metadaten erfolgreich gelesen wurden.</returns>
    public bool VideoLaden(string videoPfad)
    {
        var videoEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".m4v", ".wmv", ".flv", ".braw"
        };
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(videoPfad, videoEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("VapourSynthProcessor.VideoLaden: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        try
        {
            var probePsi = SecurityValidator.SichereProcessStartInfo("ffprobe",
                new[] { "-v", "error", "-select_streams", "v:0",
                        "-show_entries", "stream=width,height,r_frame_rate,nb_frames",
                        "-of", "csv=p=0", validierterPfad });
            using var probe = new Process { StartInfo = probePsi };
            probe.Start();
            var output = probe.StandardOutput.ReadToEnd();
            probe.WaitForExit(10000);

            if (probe.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parts = output.Trim().Split(',');
                if (parts.Length >= 4)
                {
                    int.TryParse(parts[0], out _breite);
                    int.TryParse(parts[1], out _hoehe);
                    if (parts[2].Contains('/'))
                    {
                        var fpsParts = parts[2].Split('/');
                        double denom = 0;
                        if (fpsParts.Length == 2 && double.TryParse(fpsParts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out denom))
                            double.TryParse(fpsParts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out _fps);
                        if (denom > 0) _fps /= denom;
                    }
                    int.TryParse(parts[3], out _frameAnzahl);
                }
            }

            _vspyPath = validierterPfad;
            Log.Information("VapourSynth: Video geladen ({W}x{H}, {Fps}fps, {Frames} Frames)",
                _breite, _hoehe, _fps, _frameAnzahl);
            return _breite > 0 && _hoehe > 0;
        }
        catch (Exception ex)
        {
            Log.Error("VapourSynth: Video konnte nicht geladen werden: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Generiert ein VapourSynth Python-Script für die Filter-Pipeline basierend auf
    /// den Pipeline-Parametern. Das Script wird von vspipe ausgeführt und per stdout
    /// an FFmpeg gepiped.
    ///
    /// Pipeline-Phasen:
    ///   1. Source-Load (ffms2 oder LWLibAVSource)
    ///   2. Klassische Filter (Belichtung, Kontrast, Sättigung, Rauschunterdrückung)
    ///   3. KI-Modelle via vs-mlrt (core.ort.Model):
    ///      a. NAFNet — Entrauschen (LuminanzRauschen > 0.01)
    ///      b. RestormerLight — Schärfung (SchaerfeBetrag > 0.01)
    ///      c. RealESRGAN / RealHATGAN — Upscaling (HochskalierenFaktor > 1)
    ///      d. CodeFormer — Gesichtswiederherstellung (GesichtswiederherstellungAktiv)
    ///      e. AiLUTTransform — Farbstil-Lernen (AiStilName nicht leer)
    ///   4. Output-Format: YUV420P8 für FFmpeg-Kompatibilität
    ///
    /// KI-Modelle benötigen 32-bit float RGB (RGBS). Konvertierung erfolgt automatisch:
    ///   clip = clip.resize.Bicubic(format=vs.RGBS)
    /// Nach der KI-Verarbeitung wird zurück nach YUV420P8 konvertiert.
    /// </summary>
    /// <param name="param">Pipeline-Parameter für Filter-Konfiguration.</param>
    /// <returns>Python-Script-Code als String, oder null bei Fehler.</returns>
    public string? GeneriereFilterScript(PipelineParams param)
    {
        if (string.IsNullOrEmpty(_vspyPath))
        {
            Log.Warning("VapourSynth: Kein Video geladen — Script-Generierung übersprungen");
            return null;
        }

        // Python-Pfad-Escaping (Backslash → Forward-Slash, einfache Quotes escapen)
        var videoPfadPython = _vspyPath!.Replace('\\', '/').Replace("'", "\\'");

        // Modell-Verfügbarkeit prüfen — nur Modelle einbinden die tatsächlich existieren
        bool nafnetVerfuegbar = File.Exists(Path.Combine(ModellVerzeichnis, "NAFNet.onnx"));
        bool restormerVerfuegbar = File.Exists(Path.Combine(ModellVerzeichnis, "RestormerLight.onnx"));
        bool realEsrganVerfuegbar = File.Exists(Path.Combine(ModellVerzeichnis, "RealESRGAN.onnx"));
        bool realHatGanVerfuegbar = File.Exists(Path.Combine(ModellVerzeichnis, "RealHATGAN.onnx"));
        bool codeFormerVerfuegbar = File.Exists(Path.Combine(ModellVerzeichnis, "CodeFormer.onnx"));
        bool aiLutVerfuegbar = File.Exists(Path.Combine(ModellVerzeichnis, "AiLUTTransform.onnx"));

        // Aktivierungs-Bedingungen für KI-Modelle
        // v0.5.0: Pro-Funktions KI-Toggles — wenn deaktiviert, wird das jeweilige Modell übersprungen
        bool nafnetAktiv = param.LuminanzRauschen > 0.01f && nafnetVerfuegbar && param.KIDenoisingAktiv;
        bool restormerAktiv = param.SchaerfeBetrag > 0.01f && restormerVerfuegbar && param.KISchaerfungAktiv;
        bool upscalingAktiv = param.HochskalierenFaktor > 1 && (realEsrganVerfuegbar || realHatGanVerfuegbar) && param.KIUpscalingAktiv;
        bool codeFormerAktiv = param.GesichtswiederherstellungAktiv && codeFormerVerfuegbar && param.KIGesichtswiederherstellungAktiv;
        bool aiLutAktiv = !string.IsNullOrWhiteSpace(param.AiStilName) && aiLutVerfuegbar && param.KIFarbstilAktiv;

        // Mindestens ein KI-Modell aktiv → ort-Plugin needed
        bool kiModelleAktiv = nafnetAktiv || restormerAktiv || upscalingAktiv || codeFormerAktiv || aiLutAktiv;

        if (kiModelleAktiv)
        {
            Log.Information("VapourSynth: KI-Modelle aktiviert — NAFNet={Naf} Restormer={Res} Upscaling={Ups} CodeFormer={Cf} AiLUT={Lut}",
                nafnetAktiv, restormerAktiv, upscalingAktiv, codeFormerAktiv, aiLutAktiv);
        }

        // Bestimme Upscaling-Modell: RealHATGAN (bessere Qualität) bevorzugt wenn verfügbar
        bool useRealHatGan = upscalingAktiv && realHatGanVerfuegbar;
        bool useRealEsrgan = upscalingAktiv && !useRealHatGan && realEsrganVerfuegbar;

        var sb = new StringBuilder();
        sb.AppendLine("# FlipsiColor VapourSynth Filter-Pipeline (auto-generated)");
        sb.AppendLine("# Cross-Platform: Windows + Linux");
        sb.AppendLine("# Enthält klassische Filter + KI-Modelle via vs-mlrt (vs-onnxruntime)");
        sb.AppendLine("import vapoursynth as vs");
        sb.AppendLine("import sys");
        sb.AppendLine();
        sb.AppendLine("core = vs.core");
        sb.AppendLine();

        // Source-Load: ffms2 (bevorzugt) mit Fallback auf LWLibAVSource
        sb.AppendLine("# ── Source laden ──");
        sb.AppendLine("try:");
        sb.AppendLine("    clip = core.ffms2.Source(r'" + videoPfadPython + "')");
        sb.AppendLine("except Exception:");
        sb.AppendLine("    try:");
        sb.AppendLine("        clip = core.lsmas.LWLibAVSource(r'" + videoPfadPython + "')");
        sb.AppendLine("    except Exception as e:");
        sb.AppendLine("        print(f'VapourSynth Source-Load fehlgeschlagen: {e}', file=sys.stderr)");
        sb.AppendLine("        sys.exit(1)");
        sb.AppendLine();

        // Breite/Höhe aus dem Clip auslesen (Fallback wenn ffprobe nichts geliefert)
        sb.AppendLine("# Metadaten aus dem Clip");
        sb.AppendLine("width = clip.width");
        sb.AppendLine("height = clip.height");
        sb.AppendLine();

        // ── Klassische Filter (vor KI-Modellen) ──

        // ── Belichtung (Brightness) ──
        if (Math.Abs(param.Belichtung) > 0.01f)
        {
            var brightness = (param.Belichtung * 50).ToString("F2", CultureInfo.InvariantCulture);
            sb.AppendLine("# ── Belichtung ──");
            sb.AppendLine($"clip = core.std.Levels(clip, min_in=0, max_in=255, gamma=1.0, min_out={brightness}, max_out=255, planes=[0,1,2])");
            sb.AppendLine();
        }

        // ── Kontrast ──
        if (Math.Abs(param.Kontrast) > 0.01f)
        {
            var gamma = (1.0 / (1.0 + param.Kontrast * 0.5)).ToString("F4", CultureInfo.InvariantCulture);
            sb.AppendLine("# ── Kontrast ──");
            sb.AppendLine($"clip = core.std.Levels(clip, min_in=0, max_in=255, gamma={gamma}, min_out=0, max_out=255, planes=[0,1,2])");
            sb.AppendLine();
        }

        // ── Sättigung (nur wenn AiLUTTransform nicht aktiv — AiLUT übernimmt Farbstil) ──
        if (Math.Abs(param.Saettigung) > 0.01f && !aiLutAktiv)
        {
            var sat = (1.0 + param.Saettigung * 0.5).ToString("F2", CultureInfo.InvariantCulture);
            sb.AppendLine("# ── Sättigung ──");
            sb.AppendLine("clip = core.std.Expr(clip, format=vs.RGB24) if clip.format.color_family != vs.RGB else clip");
            sb.AppendLine($"clip = core.std.Levels(clip, min_in=64, max_in=224, gamma={sat}, min_out=0, max_out=255, planes=[1,2])");
            sb.AppendLine();
        }

        // ── Luminanz-Rauschunterdrückung (klassisch via HQDN3D — nur wenn NAFNet NICHT aktiv) ──
        // NAFNet übernimmt das Entrauschen wenn aktiv, sonst klassischer HQDN3D-Filter
        if (param.LuminanzRauschen > 0.01f && !nafnetAktiv)
        {
            var strength = Math.Clamp((int)(param.LuminanzRauschen * 10), 1, 20).ToString(CultureInfo.InvariantCulture);
            sb.AppendLine("# ── Luminanz-Rauschunterdrückung (HQDN3D — klassisch) ──");
            sb.AppendLine($"clip = core.hqdn3d.Hqdn3d(clip, lum_spac={strength}, lum_tmp={strength}, chrom_spac=0, chrom_tmp=0)");
            sb.AppendLine();
        }

        // ── Chrominanz-Rauschunterdrückung ──
        if (param.ChrominanzRauschen > 0.01f)
        {
            var strength = Math.Clamp((int)(param.ChrominanzRauschen * 10), 1, 20).ToString(CultureInfo.InvariantCulture);
            sb.AppendLine("# ── Chrominanz-Rauschunterdrückung ──");
            sb.AppendLine($"clip = core.hqdn3d.Hqdn3d(clip, lum_spac=0, lum_tmp=0, chrom_spac={strength}, chrom_tmp={strength})");
            sb.AppendLine();
        }

        // ── Schärfung (klassisch via Convolution — nur wenn RestormerLight NICHT aktiv) ──
        // RestormerLight übernimmt die Schärfung wenn aktiv, sonst klassischer Convolution-Filter
        if (param.SchaerfeBetrag > 0.01f && !restormerAktiv)
        {
            sb.AppendLine("# ── Schärfung (klassisch — Convolution) ──");
            sb.AppendLine("clip = core.std.Convolution(clip, matrix=[0, -1, 0, -1, 5, -1, 0, -1, 0], divisor=1, planes=[0,1,2])");
            sb.AppendLine();
        }

        // ── KI-Modelle via vs-mlrt (vs-onnxruntime) ──
        // vs-mlrt API: core.ort.Model(clips, network_path, overlap, tilesize, provider, device_id, verbosity, builtin, builtindir, fp16)
        // clips: Liste von Input-Clips (32-bit float RGB oder GRAY)
        // network_path: Pfad zur ONNX-Datei (raw-string mit Forward-Slash)
        if (kiModelleAktiv)
        {
            sb.AppendLine("# ════════════════════════════════════════════");
            sb.AppendLine("# ── KI-Modelle via vs-mlrt (vs-onnxruntime) ──");
            sb.AppendLine("# ════════════════════════════════════════════");
            sb.AppendLine();

            // Provider bestimmen: CUDA auf Nvidia-GPU, sonst CPU
            // Auf Windows kann auch DML (DirectML) verwendet werden — hier CPU als Fallback
            sb.AppendLine("# Provider für ONNX Runtime: 'CUDA' für Nvidia, 'DML' für DirectML (Windows), '' für CPU");
            sb.AppendLine("provider = ''  # CPU als Standard — kann auf 'CUDA' oder 'DML' geändert werden");
            sb.AppendLine();

            // ── NAFNet: Entrauschen ──
            // Input: lq, Shape=[B,3,H,W], benötigt >=512x512, tilesize=[512,512], overlap=[32,32]
            if (nafnetAktiv)
            {
                var nafnetPfad = ModellPythonPfad("NAFNet");
                sb.AppendLine("# ── KI: NAFNet — Entrauschen ──");
                sb.AppendLine("# Input: lq (32-bit float RGB), Shape=[B,3,H,W], tilesize=[512,512], overlap=[32,32]");
                sb.AppendLine("try:");
                sb.AppendLine("    clip = clip.resize.Bicubic(format=vs.RGBS)");
                sb.AppendLine($"    clip = core.ort.Model([clip], r'{nafnetPfad}', tilesize=[512, 512], overlap=[32, 32], provider=provider, verbosity=2)");
                sb.AppendLine("    print('NAFNet: Entrauschen abgeschlossen', file=sys.stderr)");
                sb.AppendLine("except Exception as e:");
                sb.AppendLine("    print(f'NAFNet fehlgeschlagen: {e}', file=sys.stderr)");
                sb.AppendLine("    # Fallback: klassische Rauschunterdrückung");
                sb.AppendLine($"    clip = core.hqdn3d.Hqdn3d(clip, lum_spac={Math.Clamp((int)(param.LuminanzRauschen * 10), 1, 20)}, lum_tmp={Math.Clamp((int)(param.LuminanzRauschen * 10), 1, 20)}, chrom_spac=0, chrom_tmp=0)");
                sb.AppendLine();
            }

            // ── RestormerLight: Entschärfen / Schärfung ──
            // Input: input, Shape=[B,3,H,W], tilesize=[256,256], overlap=[16,16]
            if (restormerAktiv)
            {
                var restormerPfad = ModellPythonPfad("RestormerLight");
                sb.AppendLine("# ── KI: RestormerLight — Schärfung / Entschärfen ──");
                sb.AppendLine("# Input: input (32-bit float RGB), Shape=[B,3,H,W], tilesize=[256,256], overlap=[16,16]");
                sb.AppendLine("try:");
                sb.AppendLine("    clip = clip.resize.Bicubic(format=vs.RGBS)");
                sb.AppendLine($"    clip = core.ort.Model([clip], r'{restormerPfad}', tilesize=[256, 256], overlap=[16, 16], provider=provider, verbosity=2)");
                sb.AppendLine("    print('RestormerLight: Schärfung abgeschlossen', file=sys.stderr)");
                sb.AppendLine("except Exception as e:");
                sb.AppendLine("    print(f'RestormerLight fehlgeschlagen: {e}', file=sys.stderr)");
                sb.AppendLine("    # Fallback: klassische Schärfung");
                sb.AppendLine("    clip = core.std.Convolution(clip, matrix=[0, -1, 0, -1, 5, -1, 0, -1, 0], divisor=1, planes=[0,1,2])");
                sb.AppendLine();
            }

            // ── Upscaling: RealHATGAN (bevorzugt) oder RealESRGAN ──
            // Input: input, Shape=[B,3,H,W], 4x Upscaling
            if (useRealHatGan)
            {
                var realHatGanPfad = ModellPythonPfad("RealHATGAN");
                var zielBreite = _breite * param.HochskalierenFaktor;
                var zielHoehe = _hoehe * param.HochskalierenFaktor;
                sb.AppendLine("# ── KI: RealHATGAN — 4x Upscaling (bessere Qualität) ──");
                sb.AppendLine("# Input: input (32-bit float RGB), Shape=[B,3,H,W]");
                sb.AppendLine("try:");
                sb.AppendLine("    clip = clip.resize.Bicubic(format=vs.RGBS)");
                sb.AppendLine($"    clip = core.ort.Model([clip], r'{realHatGanPfad}', provider=provider, verbosity=2)");
                sb.AppendLine($"    clip = core.resize.Bicubic(clip, width={zielBreite}, height={zielHoehe}, format=vs.RGBS)");
                sb.AppendLine($"    width = {zielBreite}");
                sb.AppendLine($"    height = {zielHoehe}");
                sb.AppendLine("    print('RealHATGAN: Upscaling abgeschlossen', file=sys.stderr)");
                sb.AppendLine("except Exception as e:");
                sb.AppendLine("    print(f'RealHATGAN fehlgeschlagen: {e}', file=sys.stderr)");
                sb.AppendLine($"    clip = core.resize.Lanczos(clip, width={zielBreite}, height={zielHoehe})");
                sb.AppendLine();
            }
            else if (useRealEsrgan)
            {
                var realEsrganPfad = ModellPythonPfad("RealESRGAN");
                var zielBreite = _breite * param.HochskalierenFaktor;
                var zielHoehe = _hoehe * param.HochskalierenFaktor;
                sb.AppendLine("# ── KI: RealESRGAN — 4x Upscaling ──");
                sb.AppendLine("# Input: input (32-bit float RGB), Shape=[B,3,H,W]");
                sb.AppendLine("try:");
                sb.AppendLine("    clip = clip.resize.Bicubic(format=vs.RGBS)");
                sb.AppendLine($"    clip = core.ort.Model([clip], r'{realEsrganPfad}', provider=provider, verbosity=2)");
                sb.AppendLine($"    clip = core.resize.Bicubic(clip, width={zielBreite}, height={zielHoehe}, format=vs.RGBS)");
                sb.AppendLine($"    width = {zielBreite}");
                sb.AppendLine($"    height = {zielHoehe}");
                sb.AppendLine("    print('RealESRGAN: Upscaling abgeschlossen', file=sys.stderr)");
                sb.AppendLine("except Exception as e:");
                sb.AppendLine("    print(f'RealESRGAN fehlgeschlagen: {e}', file=sys.stderr)");
                sb.AppendLine($"    clip = core.resize.Lanczos(clip, width={zielBreite}, height={zielHoehe})");
                sb.AppendLine();
            }

            // ── CodeFormer: Gesichtswiederherstellung ──
            // Input: x, Shape=[B,3,512,512] + input w (float64 scalar, fidelity weight), fixed 512x512
            if (codeFormerAktiv)
            {
                var codeFormerPfad = ModellPythonPfad("CodeFormer");
                // Fidelity-Weight basierend auf Intensität: Leicht=0.7, Mittel=0.5, Stark=0.3
                // Niedrigerer Wert = stärkere Wiederherstellung
                var fidelityWeight = param.Intensitaet switch
                {
                    Intensitaet.Leicht => 0.7,
                    Intensitaet.Stark => 0.3,
                    _ => 0.5 // Mittel
                };
                sb.AppendLine("# ── KI: CodeFormer — Gesichtswiederherstellung ──");
                sb.AppendLine("# Input: x (32-bit float RGB, fixed 512x512) + w (fidelity weight)");
                sb.AppendLine($"# Fidelity-Weight: {fidelityWeight.ToString("F2", CultureInfo.InvariantCulture)} (Intensitaet={param.Intensitaet})");
                sb.AppendLine("try:");
                sb.AppendLine("    # Auf 512x512 skalieren für CodeFormer (fixed input size)");
                sb.AppendLine("    cf_input = core.resize.Bicubic(clip, width=512, height=512, format=vs.RGBS)");
                sb.AppendLine($"    cf_w = {fidelityWeight.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"    clip = core.ort.Model([cf_input, cf_w], r'{codeFormerPfad}', provider=provider, verbosity=2)");
                sb.AppendLine("    # Zurück auf Originalgröße skalieren");
                sb.AppendLine("    clip = core.resize.Bicubic(clip, width=width, height=height)");
                sb.AppendLine("    print('CodeFormer: Gesichtswiederherstellung abgeschlossen', file=sys.stderr)");
                sb.AppendLine("except Exception as e:");
                sb.AppendLine("    print(f'CodeFormer fehlgeschlagen: {e}', file=sys.stderr)");
                sb.AppendLine("    # CodeFormer ist optional — bei Fehler wird der Clip unverändert verwendet");
                sb.AppendLine();
            }

            // ── AiLUTTransform: Farbstil-Lernen ──
            // Input: image, Shape=[B,3,H,W] + input weights=[B,3]
            if (aiLutAktiv)
            {
                var aiLutPfad = ModellPythonPfad("AiLUTTransform");
                // Stil-Gewichte basierend auf Intensität
                // Die weights sind [B,3] — pro Kanal (R,G,B) ein Gewicht
                // Höhere Intensität = stärkere Stil-Übertragung
                var stilGewicht = param.Intensitaet switch
                {
                    Intensitaet.Leicht => 0.3,
                    Intensitaet.Stark => 1.0,
                    _ => 0.6 // Mittel
                };
                sb.AppendLine("# ── KI: AiLUTTransform — Farbstil-Lernen ──");
                sb.AppendLine($"# Stil: {param.AiStilName}");
                sb.AppendLine("# Input: image (32-bit float RGB) + weights=[B,3]");
                sb.AppendLine($"# Stil-Gewicht: {stilGewicht.ToString("F2", CultureInfo.InvariantCulture)} (Intensitaet={param.Intensitaet})");
                sb.AppendLine("try:");
                sb.AppendLine("    clip = clip.resize.Bicubic(format=vs.RGBS)");
                sb.AppendLine($"    # Stil-Gewichte als [B,3] — gleichmäßig auf R,G,B verteilt");
                sb.AppendLine($"    ai_weights = [{stilGewicht.ToString("F2", CultureInfo.InvariantCulture)}, {stilGewicht.ToString("F2", CultureInfo.InvariantCulture)}, {stilGewicht.ToString("F2", CultureInfo.InvariantCulture)}]");
                sb.AppendLine($"    clip = core.ort.Model([clip, ai_weights], r'{aiLutPfad}', provider=provider, verbosity=2)");
                sb.AppendLine("    print('AiLUTTransform: Farbstil angewendet', file=sys.stderr)");
                sb.AppendLine("except Exception as e:");
                sb.AppendLine("    print(f'AiLUTTransform fehlgeschlagen: {e}', file=sys.stderr)");
                sb.AppendLine("    # Fallback: klassische Sättigung");
                if (Math.Abs(param.Saettigung) > 0.01f)
                {
                    var sat = (1.0 + param.Saettigung * 0.5).ToString("F2", CultureInfo.InvariantCulture);
                    sb.AppendLine($"    clip = core.std.Levels(clip, min_in=64, max_in=224, gamma={sat}, min_out=0, max_out=255, planes=[1,2])");
                }
                sb.AppendLine();
            }

            // ── Nach KI-Verarbeitung: Format zurück konvertieren ──
            // KI-Modelle geben RGBS (32-bit float RGB) zurück → zurück nach YUV für weitere Verarbeitung
            sb.AppendLine("# ── Nach KI-Verarbeitung: Format-Konvertierung ──");
            sb.AppendLine("# KI-Modelle geben 32-bit float RGB (RGBS) zurück → konvertieren für Output");
            sb.AppendLine("if clip.format.id == vs.RGBS:");
            sb.AppendLine("    clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix_in='709', matrix='709')");
            sb.AppendLine();
        }

        // ── OpenColorIO (v0.5.0) ──
        // Wenn OCIO aktiviert ist, wird ein OCIO-Node in die Pipeline eingefügt.
        // Im VapourSynth-Script wird vs-ocio oder ein LUT-basierter Ansatz verwendet.
        if (param.ColorManagement == ColorManagementMode.OpenColorIO && !string.IsNullOrEmpty(param.OCIOConfigPfad))
        {
            var ocioConfigPython = param.OCIOConfigPfad!.Replace('\\', '/').Replace("'", "\\'");
            var sourceCS = param.OCIOSourceColorSpace ?? "ACEScg";
            var display = param.OCIODisplay ?? "sRGB";
            var view = param.OCIOView ?? "Filmic";
            var look = param.OCIOLook ?? "";

            sb.AppendLine("# ── OpenColorIO Transform (v0.5.0) ──");
            sb.AppendLine("try:");
            sb.AppendLine($"    import os");
            sb.AppendLine($"    os.environ['OCIO'] = r'{ocioConfigPython}'");
            sb.AppendLine($"    # OCIO Display Transform: {sourceCS} → {display}/{view}");
            if (!string.IsNullOrEmpty(look))
                sb.AppendLine($"    # Look: {look}");
            // Versuche vs-ocio Plugin, falle zurück auf LUT
            sb.AppendLine("    try:");
            sb.AppendLine("        import vsocio");
            sb.AppendLine($"        clip = vsocio.process(clip, src='{sourceCS}', display='{display}', view='{view}'" +
                (string.IsNullOrEmpty(look) ? "" : $", look='{look}'") + ")");
            sb.AppendLine("        print('OCIO: vs-ocio Transform angewendet', file=sys.stderr)");
            sb.AppendLine("    except ImportError:");
            sb.AppendLine("        # vs-ocio nicht verfügbar — versuche LUT-basierten Ansatz");
            sb.AppendLine("        print('OCIO: vs-ocio nicht verfügbar — überspringe (verwende LUT-Baking im C#-Code)', file=sys.stderr)");
            sb.AppendLine("except Exception as e:");
            sb.AppendLine("    print(f'OCIO Transform fehlgeschlagen: {e}', file=sys.stderr)");
            sb.AppendLine();
        }

        // Output-Format: YUV420P8 für FFmpeg-Kompatibilität
        sb.AppendLine("# ── Output-Format: YUV420P8 für FFmpeg ──");
        sb.AppendLine("if clip.format.id != vs.YUV420P8:");
        sb.AppendLine("    clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix_in='709', matrix='709')");
        sb.AppendLine();

        sb.AppendLine("# Output an stdout (für vspipe → FFmpeg pipe)");
        sb.AppendLine("clip.set_output(0)");

        return sb.ToString();
    }

    /// <summary>
    /// Führt die VapourSynth-Filter-Pipeline aus und piped das Ergebnis an FFmpeg
    /// zur Encoding-Pipeline (Video-Encode + Audio-Preservation).
    ///
    /// Pipeline:
    ///   vspipe script.vpy - --y4m | ffmpeg -i - -i audio.aac -c:v libx264 -c:a aac output.mp4
    ///
    /// <param name="param">Pipeline-Parameter für Filter-Konfiguration.</param>
    /// <param name="ausgabePfad">Ziel-Pfad für das korrigierte Video.</param>
    /// <param name="fortschrittCallback">Optional: Callback für Fortschritts-Updates (verarbeitet, gesamt).</param>
    /// <returns>True bei Erfolg, false bei Fehler.</returns>
    /// </summary>
    public bool PipelineAusfuehren(PipelineParams param, string ausgabePfad, Action<int, int>? fortschrittCallback = null)
    {
        if (string.IsNullOrEmpty(_vspyPath))
        {
            Log.Warning("VapourSynth: Kein Video geladen — Pipeline übersprungen");
            return false;
        }

        if (!IstVerfuegbar)
        {
            Log.Warning("VapourSynth ist nicht installiert — Pipeline kann nicht ausgeführt werden. Bitte FFmpeg als Backend verwenden.");
            return false;
        }

        // Ausgabe-Pfad validieren
        var ausgabeEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4" };
        var validierterOutput = SecurityValidator.ValidiereAusgabePfad(ausgabePfad, ausgabeEndungen);
        if (validierterOutput == null)
        {
            Log.Warning("VapourSynth: Ausgabe-Pfad-Validierung fehlgeschlagen");
            return false;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"FlipsiColor-VapourSynth-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. VapourSynth-Script generieren
            var script = GeneriereFilterScript(param);
            if (script == null)
            {
                Log.Error("VapourSynth: Script-Generierung fehlgeschlagen");
                return false;
            }

            var scriptPfad = Path.Combine(tempDir, "pipeline.vpy");
            File.WriteAllText(scriptPfad, script, Encoding.UTF8);
            Log.Information("VapourSynth-Script generiert: {Pfad}", scriptPfad);

            // 2. Audio extrahieren (für Re-Mux)
            string audioPfad = Path.Combine(tempDir, "audio.aac");
            bool hatAudio = AudioExtrahieren(_vspyPath!, audioPfad);

            // 3. vspipe → stdout → ffmpeg stdin (Y4M-Format)
            // vspipe gibt YUV4MPEG2 aus, FFmpeg liest von stdin
            var fpsStr = _fps.ToString("F4", CultureInfo.InvariantCulture);

            // vspipe Prozess starten
            var vspipePsi = SecurityValidator.SichereProcessStartInfo("vspipe",
                new[] { scriptPfad, "-", "--y4m" });
            vspipePsi.RedirectStandardOutput = true;
            vspipePsi.RedirectStandardError = true;
            using var vspipe = new Process { StartInfo = vspipePsi };

            // FFmpeg Prozess starten — liest von stdin
            var ffmpegArgs = new List<string>
            {
                "-i", "pipe:0",        // Video von stdin (vspipe output)
                "-f", "yuv4mpegp",     // Input-Format
                "-r", fpsStr           // Framerate setzen
            };

            if (hatAudio && File.Exists(audioPfad))
            {
                ffmpegArgs.AddRange(new[] { "-i", audioPfad });
            }

            ffmpegArgs.AddRange(new[]
            {
                "-c:v", "libx264", "-crf", "18",
                "-preset", "medium",
                "-pix_fmt", "yuv420p"
            });

            if (hatAudio && File.Exists(audioPfad))
            {
                ffmpegArgs.AddRange(new[] { "-c:a", "aac", "-b:a", "192k", "-shortest" });
            }

            ffmpegArgs.Add(validierterOutput);

            var ffmpegPsi = SecurityValidator.SichereProcessStartInfo("ffmpeg", ffmpegArgs);
            ffmpegPsi.RedirectStandardInput = true;
            ffmpegPsi.RedirectStandardError = true;
            using var ffmpeg = new Process { StartInfo = ffmpegPsi };

            // Beide Prozesse starten
            vspipe.Start();
            ffmpeg.Start();

            // Pipe: vspipe stdout → ffmpeg stdin
            // In einem Hintergrund-Task streamen, um Deadlocks zu vermeiden
            var pipeTask = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using var ffmpegInput = ffmpeg.StandardInput.BaseStream;
                    using var vspipeOutput = vspipe.StandardOutput.BaseStream;
                    vspipeOutput.CopyTo(ffmpegInput);
                    ffmpegInput.Flush();
                }
                catch (Exception ex)
                {
                    Log.Warning("VapourSynth→FFmpeg Pipe fehlgeschlagen: {Fehler}",
                        SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
                }
            });

            // vspipe stderr lesen (für Fehler-Logging)
            var vspipeErrorTask = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var errors = vspipe.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(errors))
                        Log.Warning("VapourSynth stderr: {Errors}", errors);
                }
                catch { /* ignore */ }
            });

            // FFmpeg stderr lesen (für Fehler-Logging + Fortschritt)
            var ffmpegErrorTask = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var errors = ffmpeg.StandardError.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(errors))
                        Log.Debug("FFmpeg stderr: {Errors}", errors);
                }
                catch { /* ignore */ }
            });

            // Warten bis beide fertig sind
            vspipe.WaitForExit(600000); // 10 Min Timeout
            pipeTask.Wait(60000);
            vspipeErrorTask.Wait(10000);

            ffmpeg.WaitForExit(600000);
            ffmpegErrorTask.Wait(10000);

            // Fortschritt: 0 → 1 (vereinfacht, da Frame-Counter von vspipe nicht direkt verfügbar)
            fortschrittCallback?.Invoke(1, 1);

            bool erfolg = File.Exists(validierterOutput) && vspipe.ExitCode == 0 && ffmpeg.ExitCode == 0;
            if (erfolg)
            {
                Log.Information("VapourSynth-Pipeline abgeschlossen: {Output}",
                    SecurityValidator.BereinigePfadFuerLog(validierterOutput));
            }
            else
            {
                Log.Error("VapourSynth-Pipeline fehlgeschlagen (vspipe={Vspipe}, ffmpeg={Ffmpeg})",
                    vspipe.ExitCode, ffmpeg.ExitCode);
            }

            return erfolg;
        }
        catch (Exception ex)
        {
            Log.Error("VapourSynth-Pipeline fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex)
            {
                Log.Warning("VapourSynth Temp-Verzeichnis konnte nicht gelöscht werden: {Fehler}",
                    SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            }
        }
    }

    /// <summary>
    /// Extrahiert die Audiospur aus dem Video mit FFmpeg (für Re-Mux nach VapourSynth-Processing).
    /// </summary>
    /// <param name="videoPfad">Pfad zur Video-Datei.</param>
    /// <param name="audioPfad">Ziel-Pfad für die extrahierte Audiospur.</param>
    /// <returns>True wenn Audio erfolgreich extrahiert wurde, false sonst.</returns>
    private bool AudioExtrahieren(string videoPfad, string audioPfad)
    {
        try
        {
            var psi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                new[] { "-i", videoPfad, "-vn", "-acodec", "aac", "-b:a", "192k", audioPfad, "-y" });
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            proc.WaitForExit(120000);

            bool erfolg = proc.ExitCode == 0 && File.Exists(audioPfad);
            if (erfolg)
                Log.Information("VapourSynth: Audiospur extrahiert");
            else
                Log.Warning("VapourSynth: Keine Audiospur gefunden oder Extraktion fehlgeschlagen");

            return erfolg;
        }
        catch (Exception ex)
        {
            Log.Warning("VapourSynth: Audio-Extraktion fehlgeschlagen — Video wird ohne Audio encodiert: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Gibt einen Hinweis-Text zurück, der im UI angezeigt wird wenn VapourSynth
    /// nicht installiert ist. Enthält Installations-Anleitung.
    /// </summary>
    public static string InstallationsHinweis()
    {
        if (OperatingSystem.IsWindows())
        {
            return "VapourSynth ist nicht installiert.\n\n" +
                   "Installation (Windows):\n" +
                   "1. VapourSynth von https://github.com/vapoursynth/vapoursynth/releases herunterladen\n" +
                   "2. Installer ausführen (x64)\n" +
                   "3. FFMS2-Plugin installieren (für Source-Load)\n" +
                   "4. vs-mlrt (vs-onnxruntime) Plugin installieren (für KI-Modelle)\n\n" +
                   "Alternative: FFmpeg als Video-Backend verwenden (Standard).";
        }
        else
        {
            return "VapourSynth ist nicht installiert.\n\n" +
                   "Installation (Linux):\n" +
                   "  pip install vapoursynth\n" +
                   "  pip install ffms2\n" +
                   "  pip install vs-mlrt (für KI-Modelle via ONNX Runtime)\n\n" +
                   "Alternative: FFmpeg als Video-Backend verwenden (Standard).";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}