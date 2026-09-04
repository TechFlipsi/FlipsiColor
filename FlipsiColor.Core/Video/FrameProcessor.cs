using System;
using OpenCvSharp;

using FlipsiColor.Core;
using FlipsiColor.Image;
using FlipsiColor.Utils;

namespace FlipsiColor.Video;

/// <summary>
/// Frame-Verarbeitung — wendet PipelineParams auf einen einzelnen Frame an
/// </summary>
public sealed class FrameProcessor : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<FrameProcessor>();
    private bool _disposed;

    /// <summary>
    /// Verarbeitet einen Frame mit den gegebenen Pipeline-Parametern
    /// </summary>
    public Mat Verarbeiten(Mat frame, PipelineParams param)
    {
        if (frame.Empty()) return frame;

        var result = frame.Clone();
        Mat? arbeit = null;

        try
        {
            // 0. Low-Light-Enhancement (Issue #20, NightLift-Port) — vor der Belichtung,
            //     gleiche logische Position wie in der ImagePipeline (vor Schritt 1 Weißabgleich).
            //     Rein klassisch (kein ONNX), Video-Parität zur Bild-Pipeline.
            if (param.LowLightAktiv)
            {
                var lowLightVerfahren = param.LowLightVerfahren;
                if (string.IsNullOrWhiteSpace(lowLightVerfahren) ||
                    !LowLightEnhancer.VerfuegbareVerfahren.Contains(
                        lowLightVerfahren.Trim().ToLowerInvariant()))
                {
                    Log.Warning("LowLight: Ungültiges Verfahren '{Verfahren}' — falle auf 'auto' zurück",
                        lowLightVerfahren);
                    lowLightVerfahren = "auto";
                }

                try
                {
                    var analyse = LowLightEnhancer.Analysieren(result);
                    param.LowLightErkannteStufe = analyse.Stufe.ToString();
                    Log.Debug("LowLight: Stufe={Stufe} Mean={Mean:F1} DarkRatio={Dark:F3} Verfahren={Verfahren}",
                        analyse.Stufe, analyse.MeanLuminanz, analyse.DarkRatio, lowLightVerfahren);

                    var neu = LowLightEnhancer.Aufhellen(result, lowLightVerfahren);
                    result.Dispose();
                    result = neu;
                }
                catch (Exception ex)
                {
                    // Frame-Aufhellung fehlgeschlagen — KLARER ABBRUCH (kein stiller Fallback!):
                    // Der User hat LowLight explizit aktiviert. Der äußere Catch unten wirft
                    // bei aktivem LowLight weiter, damit der User den Fehler angezeigt bekommt.
                    Log.Error("LowLight-Frame-Aufhellung fehlgeschlagen: {Fehler}",
                        SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
                    throw;
                }
            }

            // Belichtung
            if (Math.Abs(param.Belichtung) > 0.01f)
            {
                arbeit = new Mat();
                result.ConvertTo(arbeit, -1, 1.0, param.Belichtung * 50);
                result.Dispose();
                result = arbeit;
                arbeit = null;
            }

            // Kontrast
            if (Math.Abs(param.Kontrast) > 0.01f)
            {
                arbeit = new Mat();
                var alpha = 1.0 + param.Kontrast * 0.5;
                result.ConvertTo(arbeit, -1, alpha, 128 * (1 - alpha));
                result.Dispose();
                result = arbeit;
                arbeit = null;
            }

            // Sättigung
            if (Math.Abs(param.Saettigung) > 0.01f)
            {
                Mat? hsv = null;
                Mat[]? channels = null;
                try
                {
                    hsv = new Mat();
                    Cv2.CvtColor(result, hsv, ColorConversionCodes.BGR2HSV);
                    channels = hsv.Split();
                    var neuG = channels[1] + new Scalar(param.Saettigung * 50, param.Saettigung * 50, param.Saettigung * 50, 0);
                    channels[1].Dispose();
                    channels[1] = neuG;
                    Cv2.Merge(channels, hsv);
                    foreach (var c in channels) c.Dispose();
                    channels = null;
                    arbeit = new Mat();
                    Cv2.CvtColor(hsv, arbeit, ColorConversionCodes.HSV2BGR);
                    hsv.Dispose();
                    hsv = null;
                    result.Dispose();
                    result = arbeit;
                    arbeit = null;
                }
                catch
                {
                    if (channels != null) foreach (var c in channels) c.Dispose();
                    hsv?.Dispose();
                    throw;
                }
            }
        }
        catch (Exception ex) when (param.LowLightAktiv)
        {
            // LowLight explizit gewählt → kein stiller Fallback: Frame-Fehler nach außen
            // werfen (VideoPipeline/VM zeigen dem User den Fehler, statt still zu speichern).
            arbeit?.Dispose();
            Log.Error(ex, "Frame-Verarbeitung fehlgeschlagen (LowLight aktiv) — Fehler wird weitergeworfen");
            throw;
        }
        catch (Exception ex)
        {
            arbeit?.Dispose();
            Log.Warning(ex, "Frame-Verarbeitung fehlgeschlagen");
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}