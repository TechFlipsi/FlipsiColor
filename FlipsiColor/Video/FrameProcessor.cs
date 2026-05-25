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

        try
        {
            // Belichtung
            if (Math.Abs(param.Belichtung) > 0.01f)
            {
                var temp = new Mat();
                result.ConvertTo(temp, -1, 1.0, param.Belichtung * 50);
                result.Dispose();
                result = temp;
            }

            // Kontrast
            if (Math.Abs(param.Kontrast) > 0.01f)
            {
                var temp = new Mat();
                var alpha = 1.0 + param.Kontrast * 0.5;
                result.ConvertTo(temp, -1, alpha, 128 * (1 - alpha));
                result.Dispose();
                result = temp;
            }

            // Sättigung
            if (Math.Abs(param.Saettigung) > 0.01f)
            {
                var hsv = new Mat();
                Cv2.CvtColor(result, hsv, ColorConversionCodes.BGR2HSV);
                var channels = hsv.Split();
                channels[1] += param.Saettigung * 50;
                Cv2.Merge(channels, hsv);
                foreach (var c in channels) c.Dispose();
                Cv2.CvtColor(hsv, result, ColorConversionCodes.HSV2BGR);
                hsv.Dispose();
            }
        }
        catch (Exception ex)
        {
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