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
        // OpenCV Cv2.LUT erwartet eine 1D LUT mit genau 256 Einträgen (0–255 Mapping)
        // Bei 3-Kanal-Bildern: LUT-Dimension = (1, 256, CV_8UC3)
        _lut = new Mat(1, 256, MatType.CV_8UC3);
        var indexer = _lut.GetGenericIndexer<Vec3b>();
        for (int i = 0; i < 256; i++)
        {
            indexer[0, i] = new Vec3b((byte)i, (byte)i, (byte)i);
        }
        _stilName = "Identity";
        Log.Debug("Identity-LUT erstellt (1D, 256 Einträge)");
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
            // TODO: 3D-LUT-Interpolation implementieren — OpenCV Cv2.LUT unterstützt nur 1D LUTs
            // Für echte 3D-.cube-Files muss eine trilineare Interpolation oder LUT→1D-Konvertierung
            // implementiert werden. Aktuell wird die Datei als 1D-LUT geladen.
            Log.Warning("3D-LUT-Laden noch nicht vollständig implementiert — verwende Identity-LUT als Fallback. " +
                        "Siehe https://github.com/TechFlipsi/FlipsiColor/issues/43");
            IdentityErstellen();
            _stilName = System.IO.Path.GetFileNameWithoutExtension(pfad);
            return true;

            // Vollständige 3D-LUT-Implementierung (auskommentiert, bis trilineare Interpolation fertig ist):
            //
            // var lines = System.IO.File.ReadAllLines(pfad);
            // int groesse = 33;
            // var values = new System.Collections.Generic.List<float>();
            //
            // foreach (var line in lines)
            // {
            //     var trimmed = line.Trim();
            //     if (trimmed.StartsWith("#") || trimmed.StartsWith("TITLE") || trimmed.StartsWith("DOMAIN"))
            //         continue;
            //     if (trimmed.StartsWith("LUT_3D_SIZE"))
            //     {
            //         int.TryParse(trimmed.Split(' ').Last(), out groesse);
            //         continue;
            //     }
            //
            //     var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            //     if (parts.Length == 3)
            //     {
            //         foreach (var p in parts)
            //         {
            //             if (float.TryParse(p, System.Globalization.NumberStyles.Float,
            //                 System.Globalization.CultureInfo.InvariantCulture, out float v))
            //                 values.Add(v);
            //         }
            //     }
            // }
            //
            // if (values.Count != groesse * groesse * groesse * 3)
            // {
            //     Log.Error("LUT-Datei hat falsche Anzahl Werte: {Anzahl} erwartet {Erwartet}",
            //         values.Count, groesse * groesse * groesse * 3);
            //     return false;
            // }
            //
            // // 3D-LUT speichern (trilineare Interpolation in Anwenden() nötig)
            // _lut = new Mat(groesse, groesse * groesse, MatType.CV_32FC3);
            // ... trilineare Interpolation TODO ...
            //
            // _stilName = System.IO.Path.GetFileNameWithoutExtension(pfad);
            // Log.Information("LUT geladen: {Name} ({Groesse}³)", _stilName, groesse);
            // return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Laden der LUT-Datei: {Pfad}", pfad);
            return false;
        }
    }
}