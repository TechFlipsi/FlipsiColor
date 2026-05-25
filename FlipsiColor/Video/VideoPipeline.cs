using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using FlipsiColor.Core;
using FlipsiColor.Utils;

namespace FlipsiColor.Video;

/// <summary>
/// Video-Pipeline — lädt Videos, verarbeitet Frame für Frame, exportiert mit FFMPEG
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
    /// Lädt ein Video und liest Metadaten via FFMPEG ffprobe
    /// </summary>
    public bool VideoLaden(string pfad)
    {
        if (!File.Exists(pfad))
        {
            Log.Error("Video nicht gefunden: {Pfad}", pfad);
            return false;
        }

        _videoPfad = pfad;

        try
        {
            // ffprobe für Metadaten
            var probe = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height,r_frame_rate,nb_frames,duration -of csv=p=0 \"{pfad}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
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
                pfad, _breite, _hoehe, _fps, _frameAnzahl, _dauer);
            VideoGeladen?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Video konnte nicht geladen werden: {Pfad}", pfad);
            return false;
        }
    }

    /// <summary>
    /// Führt die Pipeline auf jedem Frame aus
    /// </summary>
    public void PipelineAusfuehren(PipelineParams param, Action<int, int>? fortschrittCallback = null)
    {
        if (string.IsNullOrEmpty(_videoPfad))
        {
            Log.Warning("Kein Video geladen — Pipeline übersprungen");
            return;
        }

        Log.Information("Video-Pipeline startet...");

        // FFMPEG: Video lesen → Frame für Frame dekodieren → Pipeline → encodieren
        // Aktuell: Prozess-basierter Ansatz
        var tempDir = Path.Combine(Path.GetTempPath(), "FlipsiColor-Video");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Frame-Extraktion via FFMPEG
            var extract = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{_videoPfad}\" -vsync 0 \"{tempDir}/frame_%06d.png\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            extract.Start();
            extract.WaitForExit(300000); // 5 Min Timeout

            var frameFiles = Directory.GetFiles(tempDir, "frame_*.png");
            Log.Information("{Anzahl} Frames extrahiert", frameFiles.Length);

            // Jeden Frame durch ImagePipeline schicken
            using var imagePipeline = new Image.ImagePipeline(_modelManager, _colorManager);

            for (int i = 0; i < frameFiles.Length; i++)
            {
                if (imagePipeline.BildLaden(frameFiles[i]))
                {
                    imagePipeline.PipelineAusfuehren(param);
                    var ergebnis = imagePipeline.Ergebnis;
                    if (ergebnis != null && !ergebnis.Empty())
                    {
                        Cv2.ImWrite(frameFiles[i], ergebnis);
                    }
                }
                fortschrittCallback?.Invoke(i + 1, frameFiles.Length);
            }

            // Video zurück encodieren
            var outputPfad = Path.Combine(
                Path.GetDirectoryName(_videoPfad)!,
                Path.GetFileNameWithoutExtension(_videoPfad) + "_korrigiert.mp4");

            var encode = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-framerate {_fps} -i \"{tempDir}/frame_%06d.png\" -c:v libx264 -crf 18 \"{outputPfad}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            encode.Start();
            encode.WaitForExit(300000);

            // Temp-Dateien aufräumen
            foreach (var f in frameFiles) File.Delete(f);

            Log.Information("Video-Pipeline abgeschlossen: {Output}", outputPfad);
            PipelineAbgeschlossen?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Video-Pipeline fehlgeschlagen");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}