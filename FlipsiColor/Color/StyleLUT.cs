using System;
using OpenCvSharp;

using FlipsiColor.Utils;

namespace FlipsiColor.Color;

/// <summary>
/// Style-LUT — KI-gelernte Farb-Lookup-Table für einheitliche Bildstile
/// </summary>
public sealed class StyleLUT
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<StyleLUT>();
    private Mat? _lut;
    private string? _stilName;

    /// <summary>
    /// Erstellt eine Identity-LUT (keine Veränderung)
    /// </summary>
    public void IdentityErstellen(int groesse = 33)
    {
        _lut = new Mat(groesse * groesse * groesse, 1, MatType.CV_8UC3);
        var indexer = _lut.GetGenericIndexer<Vec3b>();
        for (int r = 0; r < groesse; r++)
        for (int g = 0; g < groesse; g++)
        for (int b = 0; b < groesse; b++)
        {
            int idx = (r * groesse * groesse + g * groesse + b);
            indexer[idx] = new Vec3b(
                (byte)(b * 255 / (groesse - 1)),
                (byte)(g * 255 / (groesse - 1)),
                (byte)(r * 255 / (groesse - 1)));
        }
        _stilName = "Identity";
        Log.Debug("Identity-LUT erstellt ({Groesse}³)", groesse);
    }

    /// <summary>
    /// Wendet die LUT auf ein Bild an
    /// </summary>
    public Mat Anwenden(Mat bild)
    {
        if (_lut == null || bild.Empty())
            return bild;

        var result = new Mat();
        Cv2.LUT(bild, _lut, result);
        return result;
    }

    /// <summary>
    /// Lädt eine .cube LUT-Datei
    /// </summary>
    public bool Laden(string pfad)
    {
        try
        {
            var lines = System.IO.File.ReadAllLines(pfad);
            int groesse = 33;
            var values = new System.Collections.Generic.List<float>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#") || trimmed.StartsWith("TITLE") || trimmed.StartsWith("DOMAIN"))
                    continue;
                if (trimmed.StartsWith("LUT_3D_SIZE"))
                {
                    int.TryParse(trimmed.Split(' ').Last(), out groesse);
                    continue;
                }

                var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 3)
                {
                    foreach (var p in parts)
                    {
                        if (float.TryParse(p, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float v))
                            values.Add(v);
                    }
                }
            }

            if (values.Count != groesse * groesse * groesse * 3)
            {
                Log.Error("LUT-Datei hat falsche Anzahl Werte: {Anzahl} erwartet {Erwartet}",
                    values.Count, groesse * groesse * groesse * 3);
                return false;
            }

            _lut = new Mat(groesse * groesse * groesse, 1, MatType.CV_8UC3);
            var indexer = _lut.GetGenericIndexer<Vec3b>();
            for (int i = 0; i < values.Count / 3; i++)
            {
                indexer[i] = new Vec3b(
                    (byte)Math.Clamp(values[i * 3 + 2] * 255, 0, 255),
                    (byte)Math.Clamp(values[i * 3 + 1] * 255, 0, 255),
                    (byte)Math.Clamp(values[i * 3] * 255, 0, 255));
            }

            _stilName = System.IO.Path.GetFileNameWithoutExtension(pfad);
            Log.Information("LUT geladen: {Name} ({Groesse}³)", _stilName, groesse);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Laden der LUT-Datei: {Pfad}", pfad);
            return false;
        }
    }
}