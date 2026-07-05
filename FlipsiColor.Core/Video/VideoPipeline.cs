using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
///
/// ARCHITEKTUR (Raw-Pipe-Streaming statt PNG-Extraktion):
/// 1. Audio extrahieren (vorher, separater FFMPEG-Prozess)
/// 2. Szenenwechsel-Erkennung (via SceneDetector)
/// 3. FFMPEG Decode-Prozess: raw BGR24 Frames über stdout
/// 4. Pro Frame: raw bytes → OpenCvSharp Mat → FrameProcessor.Verarbeiten → korrigiertes Mat → raw bytes
/// 5. FFMPEG Encode-Prozess: raw BGR24 Frames über stdin → libx264 output
/// 6. Audio Re-Mux am Ende
/// Vorteil: KEINE PNG-Dateien auf der Festplatte (vorher 18.000+ Dateien bei 10 Min Video).
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
    private readonly VapourSynthProcessor? _vsProcessor;
    private readonly Settings _settings;

    /// <summary>Wird ausgelöst, wenn ein Video erfolgreich geladen wurde.</summary>
    public event EventHandler? VideoGeladen;

    /// <summary>Wird ausgelöst, wenn die Pipeline vollständig abgeschlossen wurde.</summary>
    public event EventHandler? PipelineAbgeschlossen;

    public int Breite => _breite;
    public int Hoehe => _hoehe;
    public double Fps => _fps;
    public int FrameAnzahl => _frameAnzahl;
    public double Dauer => _dauer;

    /// <summary>
    /// Konstruktor — initialisiert VideoPipeline mit ModelManager, ColorManager und Settings.
    /// VapourSynth-Processor wird nur erstellt wenn in den Einstellungen aktiviert.
    /// </summary>
    /// <param name="modelManager">KI-Modell-Manager für ONNX-Modelle.</param>
    /// <param name="colorManager">Farbmanager für ICC-Profile.</param>
    /// <param name="settings">Einstellungen (für VideoBackend-Auswahl). Null = FFmpeg-Standard.</param>
    public VideoPipeline(AI.ModelManager modelManager, Color.ColorManager colorManager, Settings? settings = null)
    {
        _modelManager = modelManager;
        _colorManager = colorManager;
        _settings = settings ?? Settings.Laden();

        // VapourSynth-Processor nur erstellen wenn aktiviert
        if (_settings.VideoBackend == VideoBackend.VapourSynth)
        {
            _vsProcessor = new VapourSynthProcessor();
            if (_vsProcessor.IstVerfuegbar)
            {
                Log.Information("Video-Backend: VapourSynth (verfügbar)");
            }
            else
            {
                Log.Warning("Video-Backend: VapourSynth aktiviert aber nicht verfügbar — Fallback auf FFmpeg");
                _vsProcessor = null;
            }
        }
        else
        {
            Log.Information("Video-Backend: FFmpeg (Standard)");
        }
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
    /// Führt die Pipeline auf jedem Frame aus (Raw-Pipe-Streaming, keine PNG-Extraktion).
    /// Nutzt SceneDetector für Szenenwechsel-Erkennung und FrameProcessor für die eigentliche
    /// Bildverarbeitung. Erhält die Audiospur durch Extraktion und Re-Mux nach Video-Encode.
    ///
    /// Ablauf:
    /// 1. Audio extrahieren (vorher, separater FFMPEG-Prozess → temp-Datei)
    /// 2. Szenenwechsel-Erkennung (via SceneDetector)
    /// 3. FFMPEG Decode-Prozess: raw BGR24 Frames über stdout pipe
    /// 4. Pro Frame: raw bytes → OpenCvSharp Mat → FrameProcessor.Verarbeiten → korrigiertes Mat → raw bytes → Encode-stdin
    /// 5. FFMPEG Encode-Prozess: raw BGR24 Frames über stdin → libx264 → output.mp4
    /// 6. Audio Re-Mux am Ende (separater FFMPEG-Prozess)
    /// </summary>
    public void PipelineAusfuehren(PipelineParams param, Action<int, int>? fortschrittCallback = null)
    {
        if (string.IsNullOrEmpty(_videoPfad))
        {
            Log.Warning("Kein Video geladen — Pipeline übersprungen");
            return;
        }

        // ── Backend-Auswahl: VapourSynth wenn aktiviert + verfügbar ──
        if (_vsProcessor != null && _settings.VideoBackend == VideoBackend.VapourSynth)
        {
            Log.Information("Video-Pipeline startet mit VapourSynth-Backend (KI-Modelle + klassische Filter)...");

            // Ausgabe-Pfad berechnen
            var vsAusgabeEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4" };
            var vsOutputPfad = Path.Combine(
                Path.GetDirectoryName(_videoPfad) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Path.GetFileNameWithoutExtension(_videoPfad) + "_korrigiert.mp4");
            vsOutputPfad = SecurityValidator.ValidiereAusgabePfad(vsOutputPfad, vsAusgabeEndungen) ?? vsOutputPfad;

            // VapourSynth: Video laden + Pipeline ausführen
            if (_vsProcessor.VideoLaden(_videoPfad))
            {
                bool vsErfolg = _vsProcessor.PipelineAusfuehren(param, vsOutputPfad, fortschrittCallback);
                if (vsErfolg)
                {
                    Log.Information("VapourSynth-Pipeline abgeschlossen: {Output}",
                        SecurityValidator.BereinigePfadFuerLog(vsOutputPfad));
                    PipelineAbgeschlossen?.Invoke(this, EventArgs.Empty);
                    return;
                }
                else
                {
                    Log.Warning("VapourSynth-Pipeline fehlgeschlagen — Fallback auf FFmpeg-Backend");
                }
            }
            else
            {
                Log.Warning("VapourSynth konnte Video nicht laden — Fallback auf FFmpeg-Backend");
            }
        }

        Log.Information("Video-Pipeline startet (FFmpeg Raw-Pipe-Streaming + SceneDetector + FrameProcessor + Audio-Erhaltung)...");

        // FIX #8: Temp-Verzeichnis mit eindeutigem Namen — wird in finally sicher gelöscht.
        // Wird nur noch für Audio-Zwischenspeicherung benötigt (KEINE 18.000 PNG-Dateien mehr).
        var tempDir = Path.Combine(Path.GetTempPath(), $"FlipsiColor-Video-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Audio extrahieren (für Re-Mux nach Encoding)
        string audioPfad = Path.Combine(tempDir, "audio.aac");
        bool hatAudio = AudioExtrahieren(_videoPfad!, audioPfad);

        // Ausgabe-Pfad vorab berechnen (für Audio Re-Mux und finale Ausgabe)
        var ausgabeEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4" };
        var outputPfad = Path.Combine(
            Path.GetDirectoryName(_videoPfad) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Path.GetFileNameWithoutExtension(_videoPfad) + "_korrigiert.mp4");
        outputPfad = SecurityValidator.ValidiereAusgabePfad(outputPfad, ausgabeEndungen) ?? outputPfad;

        // Temporäre Video-Datei ohne Audio (wird später mit Audio gemuxt falls vorhanden)
        string videoOhneAudioPfad = Path.Combine(tempDir, "video_ohne_audio.mp4");

        // Prozesse werden in finally sauber beendet — Referenzen hier halten für Cleanup
        Process? decodeProzess = null;
        Process? encodeProzess = null;

        try
        {
            // ── Szenenwechsel-Erkennung ──
            Log.Information("Szenenwechsel-Erkennung läuft...");
            var szenenwechsel = SceneDetector.SzenenwechselErkennen(_videoPfad!, schwelle: 30.0);
            Log.Information("{Anzahl} Szenenwechsel erkannt", szenenwechsel.Count);

            // Szenen-Index pro Frame für Parameter-Anpassung
            var frameSzeneMap = ErstelleFrameSzeneMap(szenenwechsel);

            // ── FFMPEG Decode-Prozess: raw BGR24 Frames über stdout ──
            // Argumente: -i <video> -f rawvideo -pix_fmt bgr24 pipe:1
            // FIX #2: Sichere ArgumentList verhindert Command-Injection.
            var decodePsi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                new[] { "-i", _videoPfad!, "-f", "rawvideo", "-pix_fmt", "bgr24", "pipe:1" });
            decodePsi.RedirectStandardOutput = true;
            decodePsi.RedirectStandardError = true;
            decodeProzess = new Process { StartInfo = decodePsi };
            decodeProzess.Start();

            // ── FFMPEG Encode-Prozess: raw BGR24 Frames über stdin → libx264 ──
            // Argumente: -f rawvideo -pix_fmt bgr24 -s WxH -r FPS -i pipe:0 -c:v libx264 -crf 18 -pix_fmt yuv420p <output>
            var fpsStr = _fps.ToString("F6", CultureInfo.InvariantCulture);
            var aufloesung = $"{_breite}x{_hoehe}";
            var encodePsi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                new[] { "-y",
                        "-f", "rawvideo",
                        "-pix_fmt", "bgr24",
                        "-s", aufloesung,
                        "-r", fpsStr,
                        "-i", "pipe:0",
                        "-c:v", "libx264",
                        "-crf", "18",
                        "-pix_fmt", "yuv420p",
                        videoOhneAudioPfad });
            encodePsi.RedirectStandardInput = true;
            encodePsi.RedirectStandardError = true;
            encodeProzess = new Process { StartInfo = encodePsi };
            encodeProzess.Start();

            // ── Fehler-Streams asynchron lesen (verhindert Deadlock bei stderr-Puffer-Überlauf) ──
            var decodeErrorTask = Task.Run(() => decodeProzess.StandardError.ReadToEnd());
            var encodeErrorTask = Task.Run(() => encodeProzess.StandardError.ReadToEnd());

            // ── Frame-Verarbeitung: Raw-Pipe-Streaming ──
            // Jeder Frame = breite * hoehe * 3 bytes (BGR24).
            int frameGroesse = _breite * _hoehe * 3;
            byte[] framePuffer = new byte[frameGroesse];
            int gesamtFrames = _frameAnzahl > 0 ? _frameAnzahl : 0;
            int frameIndex = 0;

            using var frameProcessor = new FrameProcessor();
            using var decodeStream = decodeProzess.StandardOutput.BaseStream;
            using var encodeStream = encodeProzess.StandardInput.BaseStream;

            // Lese-Loop: Frame für Frame aus Decode-stdout
            while (true)
            {
                int gelesen = 0;
                while (gelesen < frameGroesse)
                {
                    int n = decodeStream.Read(framePuffer, gelesen, frameGroesse - gelesen);
                    if (n <= 0) break; // Ende des Streams (FFmpeg fertig oder Fehler)
                    gelesen += n;
                }

                if (gelesen < frameGroesse) break; // Unvollständiger Frame = Ende des Streams

                // Pro-Szene Parameter anpassen
                var frameParams = param;
                if (szenenwechsel.Count > 0)
                {
                    var szeneIdx = frameSzeneMap.GetValueOrDefault(frameIndex, 0);
                    frameParams = SzeneParameterAnpassen(param, szeneIdx, szenenwechsel.Count);
                }

                // Raw BGR24 bytes → OpenCvSharp Mat (KEIN PNG-Decode)
                // Konstruktor mit byte[] ist protected → leeres Mat erstellen und bytes via Marshal.Copy füllen
                using var frameMat = new Mat(_hoehe, _breite, MatType.CV_8UC3);
                if (!frameMat.Empty())
                {
                    System.Runtime.InteropServices.Marshal.Copy(framePuffer, 0, frameMat.Data, frameGroesse);
                }
                if (!frameMat.Empty())
                {
                    // FrameProcessor wendet Belichtung/Kontrast/Sättigung etc. an
                    using var ergebnisMat = frameProcessor.Verarbeiten(frameMat, frameParams);
                    if (!ergebnisMat.Empty())
                    {
                        // Korrigiertes Mat → raw bytes → Encode-stdin
                        // Sicherstellen, dass Format stimmt (CV_8UC3, BGR24)
                        Mat zuSchreiben = ergebnisMat;
                        if (ergebnisMat.Type() != MatType.CV_8UC3 || ergebnisMat.Width != _breite || ergebnisMat.Height != _hoehe)
                        {
                            // Format-Konvertierung falls FrameProcessor das Format geändert hat
                            var konvertiert = new Mat();
                            if (ergebnisMat.Type() != MatType.CV_8UC3)
                            {
                                ergebnisMat.ConvertTo(konvertiert, MatType.CV_8UC3);
                            }
                            else
                            {
                                Cv2.Resize(ergebnisMat, konvertiert, new Size(_breite, _hoehe));
                            }
                            zuSchreiben = konvertiert;
                        }

                        // Raw bytes aus Mat extrahieren und an Encode-Prozess pipe
                        byte[] outputBytes = new byte[frameGroesse];
                        MarshalCopy(zuSchreiben.Data, outputBytes, 0, frameGroesse);
                        encodeStream.Write(outputBytes, 0, frameGroesse);

                        if (zuSchreiben != ergebnisMat)
                            zuSchreiben.Dispose();
                    }
                    else
                    {
                        // Leeres Ergebnis → Original-Frame durchreichen
                        encodeStream.Write(framePuffer, 0, frameGroesse);
                    }
                }
                else
                {
                    // Leerer Frame → Nullen durchreichen (Video bleibt synchron)
                    encodeStream.Write(framePuffer, 0, frameGroesse);
                }

                frameIndex++;

                // Fortschritt-Callback: (verarbeiteteFrames, gesamtFrames)
                // Wenn gesamtFrames unbekannt (0), schätze anhand der Dauer
                if (gesamtFrames <= 0 && _dauer > 0 && _fps > 0)
                    gesamtFrames = (int)(_dauer * _fps);

                fortschrittCallback?.Invoke(frameIndex, gesamtFrames > 0 ? gesamtFrames : frameIndex);
            }

            // Encode-stdin schließen → FFMPEG beginnt mit dem finalen Encoding
            encodeProzess.StandardInput.Close();

            // Warten bis beide Prozesse fertig sind (Timeout: 10 Min für große Videos)
            encodeProzess.WaitForExit(600000);
            decodeProzess.WaitForExit(10000);

            // Fehler-Streams abholen (Tasks sind zu diesem Zeitpunkt fertig)
            string decodeError = decodeErrorTask.IsCompleted ? decodeErrorTask.Result : string.Empty;
            string encodeError = encodeErrorTask.IsCompleted ? encodeErrorTask.Result : string.Empty;

            if (encodeProzess.ExitCode != 0)
            {
                Log.Error("FFMPEG Encode fehlgeschlagen (ExitCode {Code}): {Fehler}",
                    encodeProzess.ExitCode, SecurityValidator.BereinigeExceptionFuerLog(encodeError));
                return;
            }

            Log.Information("Frame-Verarbeitung & Encoding abgeschlossen: {Frames} Frames verarbeitet", frameIndex);

            // ── Audio Re-Mux (falls Audio vorhanden) ──
            if (hatAudio && File.Exists(audioPfad))
            {
                Log.Information("Audio Re-Mux läuft...");
                var muxPsi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                    new[] { "-y",
                            "-i", videoOhneAudioPfad,
                            "-i", audioPfad,
                            "-c:v", "copy",
                            "-c:a", "aac",
                            "-b:a", "192k",
                            "-shortest",
                            outputPfad });
                using var muxProzess = new Process { StartInfo = muxPsi };
                muxProzess.Start();
                muxProzess.WaitForExit(120000);

                if (muxProzess.ExitCode != 0)
                {
                    Log.Warning("Audio Re-Mux fehlgeschlagen (ExitCode {Code}) — Video ohne Audio", muxProzess.ExitCode);
                    // Fallback: Video ohne Audio als Ausgabe kopieren
                    File.Copy(videoOhneAudioPfad, outputPfad, overwrite: true);
                }
                else
                {
                    Log.Information("Audio Re-Mux abgeschlossen");
                }
            }
            else
            {
                // Kein Audio vorhanden → Video ohne Audio direkt als Ausgabe kopieren
                File.Copy(videoOhneAudioPfad, outputPfad, overwrite: true);
            }

            Log.Information("Video-Pipeline abgeschlossen: {Output}", SecurityValidator.BereinigePfadFuerLog(outputPfad));
            PipelineAbgeschlossen?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error("Video-Pipeline fehlgeschlagen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }
        finally
        {
            // FIX #8: Prozesse sicher beenden — auch bei Exception
            try
            {
                if (encodeProzess != null && !encodeProzess.HasExited)
                {
                    encodeProzess.StandardInput.Close();
                    encodeProzess.WaitForExit(5000);
                    if (!encodeProzess.HasExited) encodeProzess.Kill();
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Encode-Prozess konnte nicht sauber beendet werden: {Fehler}",
                    SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            }

            try
            {
                if (decodeProzess != null && !decodeProzess.HasExited)
                {
                    decodeProzess.StandardOutput.Close();
                    decodeProzess.WaitForExit(5000);
                    if (!decodeProzess.HasExited) decodeProzess.Kill();
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Decode-Prozess konnte nicht sauber beendet werden: {Fehler}",
                    SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            }

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

    /// <summary>
    /// Kopiert rohe Byte-Daten aus einem unmanaged Zeiger (Mat.Data) in ein managed byte[].
    /// Verwendet System.Runtime.InteropServices.Marshal.Copy für effiziente Speicherkopie.
    /// </summary>
    private static void MarshalCopy(IntPtr src, byte[] dst, int offset, int length)
    {
        System.Runtime.InteropServices.Marshal.Copy(src, dst, offset, length);
    }

    /// <summary>
    /// Erstellt ein Test-Video für den TestRunner.
    /// Erzeugt ein 5 Sekunden langes 64x64 Test-Video via FFMPEG (colorbars + tone).
    /// Static-Methode — kein VideoPipeline-Instanz nötig.
    /// </summary>
    /// <param name="pfad">Zielpfad für das Test-Video (z.B. /tmp/test.mp4).</param>
    /// <returns>true wenn erfolgreich, false bei Fehler.</returns>
    public static bool ErstelleTestVideo(string pfad)
    {
        try
        {
            // Ausgabe-Pfad validieren (nur .mp4 erlaubt)
            var ausgabeEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4" };
            var validierterPfad = SecurityValidator.ValidiereAusgabePfad(pfad, ausgabeEndungen);
            if (validierterPfad == null)
            {
                Log.Warning("ErstelleTestVideo: Pfad-Validierung fehlgeschlagen");
                return false;
            }

            // FFMPEG: 5 Sekunden 64x64 colorbars + tone generator
            // -f lavfi -i testsrc=duration=5:size=64x64:rate=30 → Video
            // -f lavfi -i sine=frequency=440:duration=5 → Audio (Test-Ton)
            // -c:v libx264 -crf 23 -c:a aac → Encoding
            var psi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                new[] { "-y",
                        "-f", "lavfi",
                        "-i", "testsrc=duration=5:size=64x64:rate=30",
                        "-f", "lavfi",
                        "-i", "sine=frequency=440:duration=5",
                        "-c:v", "libx264",
                        "-crf", "23",
                        "-pix_fmt", "yuv420p",
                        "-c:a", "aac",
                        "-shortest",
                        validierterPfad });
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            proc.WaitForExit(30000);

            bool erfolg = proc.ExitCode == 0 && File.Exists(validierterPfad);
            if (erfolg)
                Log.Information("Test-Video erstellt: {Pfad}", SecurityValidator.BereinigePfadFuerLog(validierterPfad));
            else
                Log.Warning("Test-Video konnte nicht erstellt werden (ExitCode {Code})", proc.ExitCode);

            return erfolg;
        }
        catch (Exception ex)
        {
            Log.Warning("ErstelleTestVideo fehlgeschlagen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}