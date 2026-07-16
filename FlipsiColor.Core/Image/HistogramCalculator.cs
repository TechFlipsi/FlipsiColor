using System;
using System.Collections.Generic;

using OpenCvSharp;

namespace FlipsiColor.Image;

/// <summary>
/// Histogramm-Daten — R/G/B Kanäle + Luminanz (Issue #14).
/// Jeder Array enthält 256 Werte (Bins) für 8-bit Bilder.
/// </summary>
public sealed class HistogramData
{
    /// <summary>Histogramm des Rot-Kanals (256 Bins, 0-255).</summary>
    public float[] Rot { get; set; } = new float[256];

    /// <summary>Histogramm des Grün-Kanals (256 Bins, 0-255).</summary>
    public float[] Gruen { get; set; } = new float[256];

    /// <summary>Histogramm des Blau-Kanals (256 Bins, 0-255).</summary>
    public float[] Blau { get; set; } = new float[256];

    /// <summary>Luminanz-Histogramm (256 Bins, 0-255). 0.299R + 0.587G + 0.114B.</summary>
    public float[] Luminanz { get; set; } = new float[256];

    /// <summary>Breite des Bilds (für Aspekt-Verhältnis im UI).</summary>
    public int Breite { get; set; }

    /// <summary>Höhe des Bilds.</summary>
    public int Hoehe { get; set; }

    /// <summary>Maximaler Wert über alle Kanäle (für Normalisierung im UI).</summary>
    public float MaxWert { get; set; }
}

/// <summary>
/// HistogramCalculator — berechnet RGB- und Luminanz-Histogramme mit OpenCV (Cv2.CalcHist).
/// Issue #14: Echtzeit-Histogramm für die Sidebar.
/// Pitfall: At&lt;T&gt; statt GetGenericIndexer (obsolete in OpenCvSharp 4.13).
/// </summary>
public static class HistogramCalculator
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "HistogramCalculator");

    /// <summary>
    /// Berechnet ein vollständiges Histogramm (R, G, B, Luminanz) aus einem BGR Mat.
    /// </summary>
    /// <param name="bild">Eingabebild im BGR-Format (8-bit, 3 Kanäle).</param>
    /// <returns>HistogramData mit 4×256 Bins oder null bei Fehler.</returns>
    public static HistogramData? Berechnen(Mat? bild)
    {
        if (bild == null || bild.Empty())
            return null;

        try
        {
            var data = new HistogramData
            {
                Breite = bild.Width,
                Hoehe = bild.Height
            };

            // BGR → Split in 3 Kanäle
            var kanal = bild.Split(); // [B, G, R] in OpenCV BGR-Reihenfolge
            try
            {
                // OpenCV BGR: Index 0=Blau, 1=Grün, 2=Rot
                data.Blau = BerechneKanalHistogramm(kanal[0]);
                data.Gruen = BerechneKanalHistogramm(kanal[1]);
                data.Rot = BerechneKanalHistogramm(kanal[2]);

                // Luminanz: 0.299R + 0.587G + 0.114B → Graustufen-Konvertierung
                using var grau = new Mat();
                Cv2.CvtColor(bild, grau, ColorConversionCodes.BGR2GRAY);
                data.Luminanz = BerechneKanalHistogramm(grau);

                // Maximalen Wert für Normalisierung finden
                float max = 0;
                foreach (var v in data.Rot) if (v > max) max = v;
                foreach (var v in data.Gruen) if (v > max) max = v;
                foreach (var v in data.Blau) if (v > max) max = v;
                foreach (var v in data.Luminanz) if (v > max) max = v;
                data.MaxWert = max > 0 ? max : 1;
            }
            finally
            {
                foreach (var k in kanal) k.Dispose();
            }

            return data;
        }
        catch (Exception ex)
        {
            Log.Warning("Histogramm-Berechnung fehlgeschlagen: {Fehler}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Berechnet ein Histogramm für einen einzelnen Kanal mit 256 Bins.
    /// </summary>
    private static float[] BerechneKanalHistogramm(Mat kanal)
    {
        var images = new[] { kanal };
        var channels = new[] { 0 };
        var histSize = new[] { 256 };
        var ranges = new[] { new Rangef(0f, 256f) };

        using var mask = new Mat();
        using var hist = new Mat();
        // CalcHist: 8 Parameter (images, channels, mask, hist, dims, histSize, ranges, uniform, accumulate)
        Cv2.CalcHist(images, channels, mask, hist, 1, histSize, ranges, true, false);

        var result = new float[256];
        // OCVS002: Cols/Rows are P/Invoke calls — cache before loop
        var cols = hist.Cols;
        var rows = hist.Rows;
        var total = cols * rows;

        // Konvertiere zu CV_32FC1 für sicheren Zugriff
        using var mat32f = new Mat(rows, cols, MatType.CV_32FC1);
        if (hist.Type() != MatType.CV_32FC1)
            hist.ConvertTo(mat32f, MatType.CV_32FC1);
        else
            hist.CopyTo(mat32f);

        // At<T> statt GetGenericIndexer (obsolete in OpenCvSharp 4.13)
        for (var i = 0; i < 256 && i < total; i++)
        {
            var row = i / cols;
            var col = i % cols;
            result[i] = mat32f.At<float>(row, col);
        }

        return result;
    }

    /// <summary>
    /// Konvertiert ein Histogramm in Points für eine Polyline (WPF/Avalonia).
    /// </summary>
    public static List<(double X, double Y)> HistogrammZuPunkten(float[] werte, double breite, double hoehe, float maxWert)
    {
        var punkte = new List<(double X, double Y)>();
        var max = maxWert > 0 ? maxWert : 1;
        var scaleX = breite / 255.0;

        for (var i = 0; i < werte.Length; i++)
        {
            var x = i * scaleX;
            var y = hoehe - (werte[i] / max) * hoehe;
            punkte.Add((x, y));
        }

        return punkte;
    }
}