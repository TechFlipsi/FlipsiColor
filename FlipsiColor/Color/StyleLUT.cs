using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OpenCvSharp;

using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.Color;

/// <summary>
/// Style-LUT — KI-gelernte Farb-Lookup-Table für einheitliche Bildstile
/// Unterstützt 1D-LUT (via Cv2.LUT) und 3D-.cube-LUTs (trilineare Interpolation).
/// </summary>
public sealed class StyleLUT
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<StyleLUT>();

    /// <summary>1D-LUT (256 Einträge, CV_8UC3) — für IdentityFallback oder einfache LUTs</summary>
    private Mat? _lut1d;

    /// <summary>3D-LUT (CV_32FC3, Größe×Größe×Größe) — für .cube-Dateien</summary>
    private Mat? _lut3d;
    private int _lutGroesse;
    private float _domainMin = 0.0f;
    private float _domainMax = 1.0f;
    private string? _stilName;

    /// <summary>
    /// Erstellt eine Identity-LUT (keine Veränderung)
    /// </summary>
    public void IdentityErstellen(int groesse = 33)
    {
        _lut1d = new Mat(1, 256, MatType.CV_8UC3);
        var indexer = _lut1d.GetGenericIndexer<Vec3b>();
        for (int i = 0; i < 256; i++)
        {
            indexer[0, i] = new Vec3b((byte)i, (byte)i, (byte)i);
        }
        _lut3d = null;
        _stilName = "Identity";
        Log.Debug("Identity-LUT erstellt (1D, 256 Einträge)");
    }

    /// <summary>
    /// Wendet die LUT auf ein Bild an.
    /// Wenn eine 3D-LUT geladen ist → trilineare Interpolation.
    /// Sonst → 1D-LUT via Cv2.LUT.
    /// </summary>
    public Mat Anwenden(Mat bild)
    {
        if (bild.Empty())
            return bild;

        // 3D-LUT hat Vorrang
        if (_lut3d != null)
            return Anwenden3D(bild);

        if (_lut1d != null)
        {
            var result = new Mat();
            Cv2.LUT(bild, _lut1d, result);
            return result;
        }

        // Keine LUT geladen — Bild unverändert zurückgeben
        return bild.Clone();
    }

    /// <summary>
    /// Wendet die 3D-LUT mit trilinearer Interpolation auf ein BGR-Bild an.
    /// </summary>
    private Mat Anwenden3D(Mat bild)
    {
        if (_lut3d == null)
            return bild.Clone();

        int h = bild.Height;
        int w = bild.Width;
        var result = new Mat(h, w, MatType.CV_8UC3);

        // Bild als float einlesen für Interpolation [0,1]
        Mat bild32 = new();
        bild.ConvertTo(bild32, MatType.CV_32FC3, 1.0 / 255.0);

        // LUT-Daten als Array auslesen (Größe³ × 3 floats, RGB-Reihenfolge aus .cube)
        int n = _lutGroesse;
        var lutData = new float[n * n * n * 3];
        _lut3d.GetArray(out lutData);

        // Bild-Daten auslesen
        var imgData = new float[h * w * 3];
        bild32.GetArray(out imgData);
        bild32.Dispose();

        float range = _domainMax - _domainMin;
        if (range <= 0) range = 1.0f;

        var outData = new byte[h * w * 3];

        for (int idx = 0; idx < h * w; idx++)
        {
            // Bild ist BGR, LUT ist RGB → Reihenfolge tauschen für Lookup
            float b = imgData[idx * 3 + 0];
            float g = imgData[idx * 3 + 1];
            float r = imgData[idx * 3 + 2];

            // Auf LUT-Domain mappen [domainMin, domainMax] → [0, n-1]
            float fr = ClampFloat((r - _domainMin) / range * (n - 1), 0, n - 1);
            float fg = ClampFloat((g - _domainMin) / range * (n - 1), 0, n - 1);
            float fb = ClampFloat((b - _domainMin) / range * (n - 1), 0, n - 1);

            // Trilineare Interpolation
            int r0 = (int)Math.Floor(fr), r1 = Math.Min(r0 + 1, n - 1);
            int g0 = (int)Math.Floor(fg), g1 = Math.Min(g0 + 1, n - 1);
            int b0 = (int)Math.Floor(fb), b1 = Math.Min(b0 + 1, n - 1);

            float dr = fr - r0, dg = fg - g0, db = fb - b0;

            // 8 Eckpunkte aus der LUT holen (RGB-Reihenfolge in der LUT)
            // Index-Berechnung: LUT ist als (n, n, n, 3) gespeichert → idx = ((r*n + g)*n + b)*3
            var (lr0, lg0, lb0) = LutSample(lutData, n, r0, g0, b0);
            var (lr1, lg0_1, lb0_1) = LutSample(lutData, n, r1, g0, b0);
            var (lr0_2, lg1, lb0_2) = LutSample(lutData, n, r0, g1, b0);
            var (lr1_2, lg1_1, lb0_3) = LutSample(lutData, n, r1, g1, b0);
            var (lr0_3, lg0_3, lb1) = LutSample(lutData, n, r0, g0, b1);
            var (lr1_3, lg0_4, lb1_1) = LutSample(lutData, n, r1, g0, b1);
            var (lr0_4, lg1_2, lb1_2) = LutSample(lutData, n, r0, g1, b1);
            var (lr1_4, lg1_3, lb1_3) = LutSample(lutData, n, r1, g1, b1);

            // Interpolation entlang R
            float c00r = lr0 * (1 - dr) + lr1 * dr;
            float c00g = lg0 * (1 - dr) + lg0_1 * dr;
            float c00b = lb0 * (1 - dr) + lb0_1 * dr;

            float c01r = lr0_3 * (1 - dr) + lr1_3 * dr;
            float c01g = lg0_3 * (1 - dr) + lg0_4 * dr;
            float c01b = lb1 * (1 - dr) + lb1_1 * dr;

            float c10r = lr0_2 * (1 - dr) + lr1_2 * dr;
            float c10g = lg1 * (1 - dr) + lg1_1 * dr;
            float c10b = lb0_2 * (1 - dr) + lb0_3 * dr;

            float c11r = lr0_4 * (1 - dr) + lr1_4 * dr;
            float c11g = lg1_2 * (1 - dr) + lg1_3 * dr;
            float c11b = lb1_2 * (1 - dr) + lb1_3 * dr;

            // Interpolation entlang G
            float c0r = c00r * (1 - dg) + c10r * dg;
            float c0g = c00g * (1 - dg) + c10g * dg;
            float c0b = c00b * (1 - dg) + c10b * dg;

            float c1r = c01r * (1 - dg) + c11r * dg;
            float c1g = c01g * (1 - dg) + c11g * dg;
            float c1b = c01b * (1 - dg) + c11b * dg;

            // Interpolation entlang B
            float outr = c0r * (1 - db) + c1r * db;
            float outg = c0g * (1 - db) + c1g * db;
            float outb = c0b * (1 - db) + c1b * db;

            // Zurück nach [0,255] und BGR-Reihenfolge
            outData[idx * 3 + 0] = ClampByte(outb * 255.0f);
            outData[idx * 3 + 1] = ClampByte(outg * 255.0f);
            outData[idx * 3 + 2] = ClampByte(outr * 255.0f);
        }

        result.SetArray(outData);
        return result;
    }

    /// <summary>
    /// Holt einen RGB-Sample aus der 3D-LUT an Position (r, g, b).
    /// Die LUT ist in RGB-Reihenfolge gespeichert (R ist der langsamste Index).
    /// </summary>
    private static (float R, float G, float B) LutSample(float[] lut, int n, int r, int g, int b)
    {
        int idx = ((r * n + g) * n + b) * 3;
        return (lut[idx], lut[idx + 1], lut[idx + 2]);
    }

    private static float ClampFloat(float v, float min, float max) =>
        v < min ? min : (v > max ? max : v);

    private static byte ClampByte(float v) =>
        v < 0 ? (byte)0 : (v > 255 ? (byte)255 : (byte)Math.Round(v));

    /// <summary>
    /// Lädt eine .cube LUT-Datei (Adobe 3D-LUT Format).
    /// Unterstützt: LUT_3D_SIZE, TITLE, DOMAIN_MIN/MAX, RGB-Triplets.
    /// Erstellt eine 3D-LUT als Mat (CV_32FC3) für trilineare Interpolation.
    /// FIX #1: Pfad-Validierung gegen Path-Traversal.
    /// FIX #5: LUT-Größe begrenzt — verhindert Memory-Exhaustion durch manipulierte Dateien.
    /// </summary>
    public bool Laden(string pfad)
    {
        // FIX #1: Pfad-Validierung gegen Path-Traversal
        var lutEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cube", ".lut" };
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad, lutEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("LUT-Laden: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        try
        {
            var lines = File.ReadAllLines(validierterPfad);
            int groesse = 0;
            _domainMin = 0.0f;
            _domainMax = 1.0f;
            _stilName = Path.GetFileNameWithoutExtension(pfad);

            var values = new List<float>(groesse * groesse * groesse * 3);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    continue;

                // TITLE "Name" — ignorieren, Name aus Dateiname
                if (trimmed.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase))
                    continue;

                // LUT_3D_SIZE N
                if (trimmed.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sz))
                        groesse = sz;
                    continue;
                }

                // DOMAIN_MIN float
                if (trimmed.StartsWith("DOMAIN_MIN", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float dmin))
                        _domainMin = dmin;
                    continue;
                }

                // DOMAIN_MAX float
                if (trimmed.StartsWith("DOMAIN_MAX", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float dmax))
                        _domainMax = dmax;
                    continue;
                }

                // RGB-Triplet: drei Float-Werte
                var numParts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (numParts.Length == 3)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (float.TryParse(numParts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                            values.Add(v);
                        else
                        {
                            Log.Error("LUT-Datei hat ungültigen Wert: '{Wert}' in Zeile: {Zeile}", numParts[i], trimmed);
                            return false;
                        }
                    }
                }
            }

            if (groesse <= 0)
            {
                Log.Error("LUT-Datei enthält keine LUT_3D_SIZE-Direktive");
                return false;
            }

            // FIX #5: LUT-Größe begrenzen — verhindert Memory-Exhaustion durch manipulierte Dateien
            // Typische LUTs sind 17-65 Einträge; max 200 als Sicherheitsgrenze
            if (groesse > 200)
            {
                Log.Error("LUT-Datei: Größe {Groesse} überschreitet Maximum (200) — abgelehnt", groesse);
                return false;
            }

            int expectedCount = groesse * groesse * groesse * 3;
            if (values.Count != expectedCount)
            {
                Log.Error("LUT-Datei hat falsche Anzahl Werte: {Anzahl} erwartet {Erwartet} (Größe {Groesse}³)",
                    values.Count, expectedCount, groesse);
                return false;
            }

            // 3D-LUT als Mat (CV_32FC3) speichern — ein zusammenhängendes Array
            _lutGroesse = groesse;
            _lut3d = new Mat(groesse * groesse, groesse, MatType.CV_32FC3);
            _lut3d.SetArray(values.ToArray());
            _lut1d = null; // 1D-LUT verwerfen

            Log.Information("3D-LUT geladen: {Name} ({Groesse}³, Domain [{Min}-{Max}])",
                _stilName, groesse, _domainMin, _domainMax);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Fehler beim Laden der LUT-Datei: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }
}