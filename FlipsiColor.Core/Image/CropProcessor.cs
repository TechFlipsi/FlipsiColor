using System;

using OpenCvSharp;

namespace FlipsiColor.Image;

/// <summary>
/// CropProcessor — Zuschneiden und Horizont begradigen (Issue #17).
/// Verwendet Cv2.WarpAffine für Rotation, ROI (Region of Interest) für Crop.
/// </summary>
public static class CropProcessor
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "CropProcessor");

    /// <summary>
    /// Schneidet einen rechteckigen Bereich aus dem Bild (Crop via ROI).
    /// </summary>
    /// <param name="bild">Eingabebild.</param>
    /// <param name="x">X-Position der oberen linken Ecke.</param>
    /// <param name="y">Y-Position der oberen linken Ecke.</param>
    /// <param name="breite">Breite des Crop-Bereichs.</param>
    /// <param name="hoehe">Höhe des Crop-Bereichs.</param>
    /// <returns>Gecropptes Mat oder null bei Fehler.</returns>
    public static Mat? Crop(Mat bild, int x, int y, int breite, int hoehe)
    {
        if (bild == null || bild.Empty())
        {
            Log.Warning("Crop: Bild ist leer");
            return null;
        }

        // Parameter an Bildgrenzen begrenzen
        x = Math.Clamp(x, 0, bild.Width - 1);
        y = Math.Clamp(y, 0, bild.Height - 1);
        breite = Math.Clamp(breite, 1, bild.Width - x);
        hoehe = Math.Clamp(hoehe, 1, bild.Height - y);

        try
        {
            var roi = new Rect(x, y, breite, hoehe);
            return new Mat(bild, roi);
        }
        catch (Exception ex)
        {
            Log.Warning("Crop fehlgeschlagen: {Fehler}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Dreht das Bild um einen beliebigen Winkel (in Grad) um den Mittelpunkt.
    /// Verwendet Cv2.WarpAffine + Cv2.GetRotationMatrix2D.
    /// </summary>
    /// <param name="bild">Eingabebild.</param>
    /// <param name="winkelGrad">Rotationswinkel in Grad (positiv = gegen Uhrzeiger).</param>
    /// <returns>Rotiertes Mat oder null bei Fehler.</returns>
    public static Mat? Rotieren(Mat bild, double winkelGrad)
    {
        if (bild == null || bild.Empty())
        {
            Log.Warning("Rotieren: Bild ist leer");
            return null;
        }

        if (Math.Abs(winkelGrad) < 0.01)
            return bild.Clone();

        try
        {
            var mitte = new Point2f(bild.Width / 2.0f, bild.Height / 2.0f);
            var rotMatrix = Cv2.GetRotationMatrix2D(mitte, winkelGrad, 1.0);

            // Neue Bildgröße berechnen (verhindert Abschneiden bei Rotation)
            var cos = Math.Abs(rotMatrix.At<double>(0, 0));
            var sin = Math.Abs(rotMatrix.At<double>(0, 1));
            var neueBreite = (int)(bild.Width * cos + bild.Height * sin);
            var neueHoehe = (int)(bild.Width * sin + bild.Height * cos);

            // Translation anpassen damit Bild zentriert bleibt
            rotMatrix.At<double>(0, 2) += (neueBreite - bild.Width) / 2.0;
            rotMatrix.At<double>(1, 2) += (neueHoehe - bild.Height) / 2.0;

            var ergebnis = new Mat();
            Cv2.WarpAffine(bild, ergebnis, rotMatrix, new OpenCvSharp.Size(neueBreite, neueHoehe),
                InterpolationFlags.Cubic); // Cubic, nicht Bicubic (obsolete in OpenCvSharp 4.13)
            rotMatrix.Dispose();
            return ergebnis;
        }
        catch (Exception ex)
        {
            Log.Warning("Rotation fehlgeschlagen: {Fehler}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Dreht das Bild um 90 Grad (rechts oder links).
    /// </summary>
    /// <param name="bild">Eingabebild.</param>
    /// <param name="rechts">True = 90° rechts, False = 90° links.</param>
    /// <returns>Rotiertes Mat oder null bei Fehler.</returns>
    public static Mat? Rotieren90(Mat bild, bool rechts)
    {
        if (bild == null || bild.Empty())
            return null;

        try
        {
            var ergebnis = new Mat();
            Cv2.Rotate(bild, ergebnis, rechts ? RotateFlags.Rotate90Clockwise : RotateFlags.Rotate90Counterclockwise);
            return ergebnis;
        }
        catch (Exception ex)
        {
            Log.Warning("Rotieren90 fehlgeschlagen: {Fehler}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Berechnet einen Crop-Bereich basierend auf einem Aspect-Ratio.
    /// </summary>
    /// <param name="bildBreite">Breite des Originalbilds.</param>
    /// <param name="bildHoehe">Höhe des Originalbilds.</param>
    /// <param name="ratioBreite">Gewünschtes Seitenverhältnis (z.B. 16 für 16:9).</param>
    /// <param name="ratioHoehe">Gewünschtes Seitenverhältnis (z.B. 9 für 16:9).</param>
    /// <returns>Rect mit dem Crop-Bereich, zentriert im Bild.</returns>
    public static Rect AspectRatioCropBerechnen(int bildBreite, int bildHoehe, double ratioBreite, double ratioHoehe)
    {
        var zielRatio = ratioBreite / ratioHoehe;
        var bildRatio = (double)bildBreite / bildHoehe;

        int cropBreite, cropHoehe;

        if (bildRatio > zielRatio)
        {
            // Bild ist breiter → Höhe ist begrenzend
            cropHoehe = bildHoehe;
            cropBreite = (int)(bildHoehe * zielRatio);
        }
        else
        {
            // Bild ist höher → Breite ist begrenzend
            cropBreite = bildBreite;
            cropHoehe = (int)(bildBreite / zielRatio);
        }

        var x = (bildBreite - cropBreite) / 2;
        var y = (bildHoehe - cropHoehe) / 2;

        return new Rect(x, y, cropBreite, cropHoehe);
    }
}