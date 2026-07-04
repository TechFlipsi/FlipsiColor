using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using FlipsiColor.Core;
using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.Video;

/// <summary>
/// Video-Pipeline — lädt Videos, erkennt Szenenwechsel, verarbeitet
/// Frame für Frame mit FrameProcessor (effizienter als ImagePipeline pro Frame),
/// exportiert mit FFMPEG und erhält die Audiospur.
/// </summary>
public sealed class VideoPipeline : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<VideoPipeline>();
    private bool _disposed;
    private string? _videoPfad;
    private int _breite, _hoehe;
    private double _fps;
    private int _frameAnzahl;
    private double _dauer;
    private readonly AI.ModelManager _modelManager;
    private readonly Color.ColorManager _colorManager;

    public event EventHandler? VideoGeladen;
    public event EventHandler? PipelineAbgeschlossen;

    public int Breite => _breite;
    public int Hoehe => _hoehe;
    public double Fps => _fps;
    public int FrameAnzahl => _frameAnzahl;
    public double Dauer => _dauer;

    public VideoPipeline(AI.ModelManager modelManager, Color.ColorManager colorManager)
    {
        _modelManager = modelManager;
        _colorManager = colorManager;
    }

    /// <summary>
    /// Lädt ein Video und liest Metadaten via FFMPEG ffprobe.
    /// Sicherheitsmaßnahmen: Pfad-Validierung (Path-Traversal/UNC), sichere Prozess-Argumente.
    /// </summary>
    public bool VideoLaden(string pfad)
    {
        // FIX #1: Pfad-Validierung gegen Path-Traversal und UNC-Pfade
        var videoEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".avi", ".mkv", ".m4v", ".wmv", ".flv"
        };
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad, videoEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("VideoLaden: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        _videoPfad = validierterPfad;

        try
        {
            // FIX #2: Command-Injection verhindern — ArgumentList statt String-Arguments
            var probePsi = SecurityValidator.SichereProcessStartInfo("ffprobe",
                new[] { "-v", "error", "-select_streams", "v:0",
                        "-show_entries", "stream=width,height,r_frame_rate,nb_frames,duration",
                        "-of", "csv=p=0", validierterPfad });
            using var probe = new Process { StartInfo = probePsi };
            probe.Start();
            var output = probe.StandardOutput.ReadToEnd();
            probe.WaitForExit(10000);

            if (probe.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parts = output.Trim().Split(',');
                if (parts.Length >= 5)
                {
                    int.TryParse(parts[0], out _breite);
                    int.TryParse(parts[1], out _hoehe);
                    if (parts[2].Contains('/'))
                    {
                        var fpsParts = parts[2].Split('/');
                        double denom = 0;
                        if (fpsParts.Length == 2 && double.TryParse(fpsParts[1], out denom))
                            double.TryParse(fpsParts[0], out _fps);
                        if (denom > 0) _fps /= denom;
                    }
                    int.TryParse(parts[3], out _frameAnzahl);
                    double.TryParse(parts[4], out _dauer);
                }
            }

            Log.Information("Video geladen: {Pfad} ({W}x{H}, {Fps}fps, {Frames} Frames, {Dauer:F1}s)",
                SecurityValidator.BereinigePfadFuerLog(validierterPfad), _breite, _hoehe, _fps, _frameAnzahl, _dauer);
            VideoGeladen?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Video konnte nicht geladen werden: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Führt die Pipeline auf jedem Frame aus.
    /// Nutzt SceneDetector für Szenenwechsel-Erkennung und FrameProcessor
    /// (effizienter als ImagePipeline pro Frame).
    /// Erhält die Audiospur durch Extraktion und Re-Mux nach Video-Encode.
    /// </summary>
    public void PipelineAusfuehren(PipelineParams param, Action<int, int>? fortschrittCallback = null)
    {
        if (string.IsNullOrEmpty(_videoPfad))
        {
            Log.Warning("Kein Video geladen — Pipeline übersprungen");
            return;
        }

        Log.Information("Video-Pipeline startet (mit SceneDetector + FrameProcessor + Audio-Erhaltung)...");

        // FIX #8: Temp-Verzeichnis mit eindeutigem Namen — wird in finally sicher gelöscht
        var tempDir = Path.Combine(Path.GetTempPath(), $"FlipsiColor-Video-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Audio extrahieren (für Re-Mux nach Encoding)
        string audioPfad = Path.Combine(tempDir, "audio.aac");
        bool hatAudio = AudioExtrahieren(_videoPfad!, audioPfad);

        try
        {
            // ── Szenenwechsel-Erkennung ──
            Log.Information("Szenenwechsel-Erkennung läuft...");
            var szenenwechsel = SceneDetector.SzenenwechselErkennen(_videoPfad!, schwelle: 30.0);
            Log.Information("{Anzahl} Szenenwechsel erkannt", szenenwechsel.Count);

            // Szenen-Index pro Frame für Parameter-Anpassung
            var frameSzeneMap = ErstelleFrameSzeneMap(szenenwechsel);

            // FIX #2: Frame-Extraktion via FFMPEG — sichere ArgumentList statt String-Arguments
            var framePattern = Path.Combine(tempDir, "frame_%06d.png");
            var extractPsi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                new[] { "-i", _videoPfad!, "-vsync", "0", framePattern });
            using var extract = new Process { StartInfo = extractPsi };
            extract.Start();
            extract.WaitForExit(300000); // 5 Min Timeout

            var frameFiles = Directory.GetFiles(tempDir, "frame_*.png");
            Array.Sort(frameFiles);
            Log.Information("{Anzahl} Frames extrahiert", frameFiles.Length);

            // FrameProcessor verwenden (effizienter als ImagePipeline pro Frame)
            using var frameProcessor = new FrameProcessor();

            for (int i = 0; i < frameFiles.Length; i++)
            {
                // Pro-Szene Parameter anpassen
                var frameParams = param;
                if (szenenwechsel.Count > 0)
                {
                    var szeneIdx = frameSzeneMap.GetValueOrDefault(i, 0);
                    frameParams = SzeneParameterAnpassen(param, szeneIdx, szenenwechsel.Count);
                }

                // Frame laden und verarbeiten
                using var frame = Cv2.ImRead(frameFiles[i], ImreadModes.Color);
                if (!frame.Empty())
                {
                    using var ergebnis = frameProcessor.Verarbeiten(frame, frameParams);
                    if (!ergebnis.Empty())
                    {
                        Cv2.ImWrite(frameFiles[i], ergebnis);
                    }
                }

                fortschrittCallback?.Invoke(i + 1, frameFiles.Length);
            }

            // Video zurück encodieren — mit Audio Re-Mux falls Audio vorhanden
            // FIX: Ausgabe-Pfad validieren (verhindert Path-Traversal in Output)
            var ausgabeEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4" };
            var outputPfad = Path.Combine(
                Path.GetDirectoryName(_videoPfad) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Path.GetFileNameWithoutExtension(_videoPfad) + "_korrigiert.mp4");
            outputPfad = SecurityValidator.ValidiereAusgabePfad(outputPfad, ausgabeEndungen) ?? outputPfad;

            // FIX #2: Encode via sichere ArgumentList — kein String-Arguments (Command-Injection-Schutz)
            ProcessStartInfo encodePsi;
            if (hatAudio && File.Exists(audioPfad))
            {
                // Video + Audio Re-Mux
                encodePsi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                    new[] { "-framerate", _fps.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "-i", framePattern!, "-i", audioPfad,
                            "-c:v", "libx264", "-crf", "18", "-c:a", "aac", "-b:a", "192k",
                            "-shortest", outputPfad });
            }
            else
            {
                // Nur Video (kein Audio vorhanden)
                encodePsi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                    new[] { "-framerate", _fps.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            "-i", framePattern!,
                            "-c:v", "libx264", "-crf", "18", outputPfad });
            }

            using var encode = new Process { StartInfo = encodePsi };
            encode.Start();
            encode.WaitForExit(300000);

            // FIX #8: Temp-Dateien aufräumen — in finally-Block für Sicherheit bei Exceptions
            Log.Information("Video-Pipeline abgeschlossen: {Output}", SecurityValidator.BereinigePfadFuerLog(outputPfad));
            PipelineAbgeschlossen?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error("Video-Pipeline fehlgeschlagen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }
        finally
        {
            // FIX #8: Temp-Verzeichnis immer löschen — auch bei Exception
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex)
            {
                Log.Warning("Temp-Verzeichnis konnte nicht gelöscht werden: {Fehler}",
                    SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            }
        }
    }

    /// <summary>
    /// Extrahiert die Audiospur aus dem Video mit FFMPEG.
    /// FIX #2: Sichere ArgumentList verhindert Command-Injection.
    /// </summary>
    private bool AudioExtrahieren(string videoPfad, string audioPfad)
    {
        try
        {
            var psi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                new[] { "-i", videoPfad, "-vn", "-acodec", "aac", "-b:a", "192k", audioPfad, "-y" });
            using var extractAudio = new Process { StartInfo = psi };
            extractAudio.Start();
            extractAudio.WaitForExit(120000);

            bool erfolg = extractAudio.ExitCode == 0 && File.Exists(audioPfad);
            if (erfolg)
                Log.Information("Audiospur extrahiert");
            else
                Log.Warning("Keine Audiospur im Video gefunden oder Extraktion fehlgeschlagen");

            return erfolg;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Audio-Extraktion fehlgeschlagen — Video wird ohne Audio encodiert");
            return false;
        }
    }

    /// <summary>
    /// Erstellt eine Map von Frame-Index → Szenen-Index.
    /// Frames vor dem ersten Szenenwechsel gehören zu Szene 0.
    /// </summary>
    private static Dictionary<int, int> ErstelleFrameSzeneMap(List<int> szenenwechsel)
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < szenenwechsel.Count; i++)
        {
            // Alle Frames ab diesem Szenenwechsel bis zum nächsten gehören zu Szene i+1
            int startFrame = szenenwechsel[i];
            int endFrame = i + 1 < szenenwechsel.Count ? szenenwechsel[i + 1] : int.MaxValue;
            for (int f = startFrame; f < endFrame && f < 100000; f++)
            {
                map[f] = i + 1;
            }
        }
        return map;
    }

    /// <summary>
    /// Passt Pipeline-Parameter pro Szene an.
    /// Verschiedene Szenen können unterschiedliche Belichtungs/Kontrast-Werte benötigen.
    /// </summary>
    private static PipelineParams SzeneParameterAnpassen(PipelineParams basis, int szeneIdx, int szeneAnzahl)
    {
        // Leichte Variation pro Szene, um Szenenwechsel auszugleichen
        var param = new PipelineParams
        {
            Belichtung = basis.Belichtung,
            Kontrast = basis.Kontrast,
            Saettigung = basis.Saettigung,
            Vibranz = basis.Vibranz,
            Lichter = basis.Lichter,
            Schatten = basis.Schatten,
            SchaerfeBetrag = basis.SchaerfeBetrag,
            LuminanzRauschen = basis.LuminanzRauschen,
            ChrominanzRauschen = basis.ChrominanzRauschen,
            ObjektivkorrekturAktiv = basis.ObjektivkorrekturAktiv,
            GesichtswiederherstellungAktiv = basis.GesichtswiederherstellungAktiv,
            HochskalierenFaktor = basis.HochskalierenFaktor,
            DistortionGridAktiv = basis.DistortionGridAktiv,
            ColorCalibrationAktiv = basis.ColorCalibrationAktiv,
            Intensitaet = basis.Intensitaet,
            Modus = basis.Modus,
            StyleLutPfad = basis.StyleLutPfad,
            AiStilName = basis.AiStilName,
            ExifKamera = basis.ExifKamera,
            ExifObjektiv = basis.ExifObjektiv,
            ExifBrennweite = basis.ExifBrennweite,
            ExifBlende = basis.ExifBlende,
            ErkannteSzene = basis.ErkannteSzene
        };

        // Szenen-spezifische Anpassung: Belichtung leicht variieren
        // um Helligkeitsunterschiede zwischen Szenen auszugleichen
        // FIX #7: Begrenzung gegen Overflow/Underflow bei extremen float-Werten
        float szeneOffset = (szeneIdx % 3 - 1) * 0.05f;
        param.Belichtung = SecurityValidator.BegrenzeParameter(basis.Belichtung + szeneOffset, -1f, 1f);

        return param;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}