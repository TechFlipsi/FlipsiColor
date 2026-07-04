using System;
using OpenCvSharp;

using FlipsiColor.Utils;

namespace FlipsiColor.Video;

/// <summary>
/// Szenenwechsel-Erkennung — Frame-Ähnlichkeitsanalyse
/// </summary>
public class SceneDetector
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<SceneDetector>();

    /// <summary>
    /// Erkennt Szenenwechsel in einer Sequenz von Frames
    /// </summary>
    public static List<int> SzenenwechselErkennen(string videoPfad, double schwelle = 30.0)
    {
        var szenenwechsel = new List<int>();

        try
        {
            using var capture = new VideoCapture(videoPfad);
            if (!capture.IsOpened())
            {
                Log.Error("Video konnte nicht geöffnet werden: {Pfad}", videoPfad);
                return szenenwechsel;
            }

            Mat? vorherigesFrame = null;
            int frameIdx = 0;

            while (true)
            {
                using var frame = new Mat();
                if (!capture.Read(frame) || frame.Empty())
                    break;

                // Zu Grau konvertieren
                using var grau = new Mat();
                Cv2.CvtColor(frame, grau, ColorConversionCodes.BGR2GRAY);

                if (vorherigesFrame != null)
                {
                    // Histogramm-Vergleich
                    using var hist1 = HistogrammBerechnen(vorherigesFrame);
                    using var hist2 = HistogrammBerechnen(grau);
                    double diff = Cv2.CompareHist(hist1, hist2, HistCompMethods.Correl);

                    // Niedrige Korrelation = Szenenwechsel
                    if (diff < (1.0 - schwelle / 100.0))
                    {
                        szenenwechsel.Add(frameIdx);
                        Log.Debug("Szenenwechsel bei Frame {Idx} (Korrelation: {Diff:F3})", frameIdx, diff);
                    }
                }

                vorherigesFrame?.Dispose();
                vorherigesFrame = grau.Clone(); // Clone weil grau disposed wird am Ende des using-Blocks
                frameIdx++;
            }

            vorherigesFrame?.Dispose();
            Log.Information("{Anzahl} Szenenwechsel in {Frames} Frames erkannt", szenenwechsel.Count, frameIdx);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Szenenwechsel-Erkennung fehlgeschlagen");
        }

        return szenenwechsel;
    }

    private static Mat HistogrammBerechnen(Mat grau)
    {
        var hist = new Mat();
        Cv2.CalcHist(
            new[] { grau },
            new[] { 0 },
            null,
            hist,
            1,
            new[] { 256 },
            new[] { new Rangef(0, 256) },
            false);
        Cv2.Normalize(hist, hist);
        return hist;
    }
}