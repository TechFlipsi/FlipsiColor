using System;
using OpenCvSharp;

using FlipsiColor.Core;
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