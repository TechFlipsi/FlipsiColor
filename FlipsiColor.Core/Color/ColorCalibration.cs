using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.Color;

/// <summary>
/// Kalibrierungsmodus für die Farbkalibrierung.
/// </summary>
public enum KalibrierungsModus
{
    /// <summary>Automatische Erkennung (ColorChecker, Fallback Graukarte).</summary>
    Auto,

    /// <summary>Graukarten-Modus: neutralgraue Fläche → Weißabgleich-Korrektur.</summary>
    Graukarte,

    /// <summary>ColorChecker-Modus: 24 Felder → 3×3 Farb-Matrix via Least-Squares.</summary>
    ColorChecker
}

/// <summary>
/// Farbkalibrierung basierend auf einem Macbeth ColorChecker (24 Felder)
/// oder einer Graukarte im Referenzbild.
///
/// Im ColorChecker-Modus wird eine 3×3 Farb-Transfer-Matrix via Least-Squares
/// (OpenCvSharp Cv2.Solve, DECOMP_SVD) berechnet, die gemessene RGB-Werte
/// auf die Referenzwerte des ColorCheckers abbildet.
/// Im Graukarten-Modus wird eine diagonale Weißabgleich-Matrix berechnet.
/// </summary>
public sealed class ColorCalibration
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ColorCalibration>();

    /// <summary>
    /// Referenz-RGB-Werte (sRGB, 0–255) der 24 Macbeth-ColorChecker-Felder
    /// in Standard-Reihenfolge (Zeile für Zeile, oben links beginnend).
    /// Quelle: X-Rite / BabelColor referenzierte sRGB-Werte.
    /// </summary>
    private static readonly int[] ColorCheckerReferenz =
    [
        // Zeile 1
        115,  82,  68,   // 1  — Dark Skin
        194, 150, 130,   // 2  — Light Skin
         98, 122, 157,   // 3  — Blue Sky
         87, 108,  67,   // 4  — Foliage
        133, 128, 176,   // 5  — Blue Flower
        103, 189, 170,   // 6  — Bluish Green
        // Zeile 2
        214, 126,  44,   // 7  — Orange
         80,  91, 166,   // 8  — Purplish Blue
        193,  90,  99,   // 9  — Moderate Red
         94,  60, 108,   // 10 — Purple
        157, 188,  64,   // 11 — Yellow Green
        224, 163,  46,   // 12 — Orange Yellow
        // Zeile 3
         56,  61, 150,   // 13 — Blue
         70, 148,  73,   // 14 — Green
        175,  54,  60,   // 15 — Red
        231, 199,  31,   // 16 — Yellow
        187,  86, 149,   // 17 — Magenta
          8, 133, 161,   // 18 — Cyan
        // Zeile 4 (Graukeil)
        243, 243, 242,   // 19 — White
        200, 200, 200,   // 20 — Neutral 8
        160, 160, 160,   // 21 — Neutral 6.5
        122, 122, 121,   // 22 — Neutral 5
         85,  85,  85,   // 23 — Neutral 3.5
         52,  52,  52    // 24 — Black
    ];

    /// <summary>
    /// Anzahl der ColorChecker-Felder (4 Zeilen × 6 Spalten).
    /// </summary>
    private const int AnzahlFelder = 24;
    private const int Spalten = 6;
    private const int Zeilen = 4;

    /// <summary>
    /// Gewünschter Kalibrierungsmodus. Standardmäßig <see cref="KalibrierungsModus.Auto"/>.
    /// Kann vor dem Aufruf von <see cref="Kalibrieren"/> gesetzt werden.
    /// </summary>
    public KalibrierungsModus Modus { get; set; } = KalibrierungsModus.Auto;

    /// <summary>
    /// Gibt an, ob eine gültige Farb-Kalibrierung vorliegt.
    /// </summary>
    public bool IstKalibriert => _matrix is not null;

    /// <summary>
    /// Die berechnete 3×3 Farb-Transfer-Matrix (RGB-Eingang → RGB-Referenz).
    /// Null, solange nicht kalibriert.
    /// </summary>
    private double[,]? _matrix;

    /// <summary>
    /// Der effektiv verwendete Modus nach der letzten Kalibrierung.
    /// </summary>
    public KalibrierungsModus VerwendeterModus { get; private set; } = KalibrierungsModus.Auto;

    /// <summary>
    /// Lädt ein Referenzbild mit einem ColorChecker oder einer Graukarte,
    /// erkennt die Felder und berechnet die Farb-Transfer-Matrix.
    /// FIX #1: Pfad-Validierung gegen Path-Traversal.
    /// </summary>
    /// <param name="referenzBildPfad">Pfad zum Referenzbild.</param>
    /// <returns>true bei erfolgreicher Kalibrierung.</returns>
    public bool Kalibrieren(string referenzBildPfad)
    {
        // FIX #1: Pfad-Validierung gegen Path-Traversal
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(referenzBildPfad);
        if (validierterPfad == null)
        {
            Log.Warning("Farb-Kalibrierung: Pfad-Validierung fehlgeschlagen");
            return false;
        }
        referenzBildPfad = validierterPfad;

        Log.Information("Starte Farb-Kalibrierung (Modus={Modus})", Modus);

        using Mat bild = Cv2.ImRead(referenzBildPfad, ImreadModes.Color);
        if (bild.Empty())
        {
            Log.Error("Referenzbild konnte nicht geladen werden");
            return false;
        }

        Log.Debug("Referenzbild geladen: {Breite}×{Hoehe}, Kanäle={Kanaele}",
            bild.Width, bild.Height, bild.Channels());

        // Modus-Auflösung: Auto versucht ColorChecker, fällt auf Graukarte zurück
        bool erfolg = Modus switch
        {
            KalibrierungsModus.ColorChecker => ColorCheckerKalibrierung(bild),
            KalibrierungsModus.Graukarte => GraukartenKalibrierung(bild),
            KalibrierungsModus.Auto => ColorCheckerKalibrierung(bild) || GraukartenKalibrierung(bild),
            _ => GraukartenKalibrierung(bild)
        };

        if (erfolg)
        {
            Log.Information("Farb-Kalibrierung erfolgreich (verwendeter Modus={Modus})", VerwendeterModus);
            MatrixProtokollieren();
        }
        else
        {
            Log.Error("Farb-Kalibrierung fehlgeschlagen — keine Matrix berechnet");
        }

        return erfolg;
    }

    /// <summary>
    /// Wendet die berechnete Korrektur-Matrix auf ein Bild an.
    /// Das Bild wird von BGR nach RGB konvertiert, mit der 3×3 Matrix transformiert
    /// und zurück nach BGR konvertiert.
    /// </summary>
    /// <param name="bild">Eingabebild (BGR oder BGRA, 8-bit).</param>
    /// <returns>Korrigiertes Bild; bei fehlender Kalibrierung oder Fehler das Original.</returns>
    public Mat Anwenden(Mat bild)
    {
        if (!IstKalibriert)
        {
            Log.Warning("Anwenden aufgerufen ohne vorherige Kalibrierung — Bild unverändert");
            return bild;
        }

        if (bild.Empty())
        {
            Log.Warning("Anwenden: leeres Eingabebild");
            return bild;
        }

        try
        {
            // BGRA → BGR falls nötig (Alpha-Kanal wird für die Korrektur nicht benötigt)
            Mat bgr;
            Mat? bgrTemp = null;
            if (bild.Channels() == 4)
            {
                bgrTemp = new Mat();
                Cv2.CvtColor(bild, bgrTemp, ColorConversionCodes.BGRA2BGR);
                bgr = bgrTemp;
            }
            else if (bild.Channels() == 3)
            {
                bgr = bild;
            }
            else
            {
                Log.Warning("Anwenden: nicht unterstützte Kanalzahl {Kanaele}", bild.Channels());
                return bild;
            }

            // BGR → RGB (Matrix arbeitet in RGB-Reihenfolge)
            using Mat rgb = new();
            Cv2.CvtColor(bgr, rgb, ColorConversionCodes.BGR2RGB);

            // Nach Float konvertieren für die Matrix-Multiplikation
            using Mat rgb32 = new();
            rgb.ConvertTo(rgb32, MatType.CV_32FC3);

            // 3×3 Matrix als OpenCvSharp Mat (CV_32F für Cv2.Transform)
            using Mat m = new(3, 3, MatType.CV_32FC1);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    m.Set<float>(i, j, (float)_matrix![i, j]);

            // Lineare Transformation pro Pixel: dst = m * src
            using Mat transformiert = new();
            Cv2.Transform(rgb32, transformiert, m);

            // Zurück nach 8-Bit (saturate_cast clippt auf 0–255)
            using Mat transformiert8 = new();
            transformiert.ConvertTo(transformiert8, MatType.CV_8UC3);

            // RGB → BGR für die weitere Verarbeitung
            Mat ergebnis = new();
            Cv2.CvtColor(transformiert8, ergebnis, ColorConversionCodes.RGB2BGR);

            // Hilfs-Mats freigeben
            bgrTemp?.Dispose();

            Log.Debug("Farb-Korrektur angewendet: {Breite}×{Hoehe}", ergebnis.Width, ergebnis.Height);
            return ergebnis;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Farb-Korrektur fehlgeschlagen — gebe Original zurück");
            return bild;
        }
    }

    /// <summary>
    /// Speichert die Kalibrierung als JSON-Datei.
    /// FIX #1: Pfad-Validierung gegen Path-Traversal.
    /// </summary>
    /// <param name="pfad">Zieldatei-Pfad.</param>
    /// <returns>true bei Erfolg.</returns>
    public bool Speichern(string pfad)
    {
        if (!IstKalibriert)
        {
            Log.Warning("Speichern: keine Kalibrierung vorhanden");
            return false;
        }

        // FIX #1: Ausgabe-Pfad validieren
        var jsonEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json" };
        var validierterPfad = SecurityValidator.ValidiereAusgabePfad(pfad, jsonEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("Speichern: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        try
        {
            KalibrierungsDaten daten = new()
            {
                Modus = VerwendeterModus.ToString(),
                Matrix = _matrix!
            };

            string json = JsonSerializer.Serialize(daten, JsonOptionen);
            File.WriteAllText(validierterPfad, json);
            Log.Information("Farb-Kalibrierung gespeichert");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Speichern der Farb-Kalibrierung fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Lädt eine Kalibrierung aus einer JSON-Datei.
    /// FIX #1: Pfad-Validierung gegen Path-Traversal.
    /// FIX #5: Typ-sichere Deserialisierung — Matrix-Dimensionen werden validiert.
    /// </summary>
    /// <param name="pfad">Quelldatei-Pfad.</param>
    /// <returns>true bei Erfolg.</returns>
    public bool Laden(string pfad)
    {
        // FIX #1: Pfad-Validierung gegen Path-Traversal
        var jsonEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json" };
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad, jsonEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("Laden: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        try
        {
            string json = File.ReadAllText(validierterPfad);
            KalibrierungsDaten? daten = JsonSerializer.Deserialize<KalibrierungsDaten>(json, JsonOptionen);

            // FIX #5: Matrix-Dimensionen strikt validieren — verhindert Typ-Confusion
            if (daten is null || daten.Matrix is null || daten.Matrix.GetLength(0) != 3 || daten.Matrix.GetLength(1) != 3)
            {
                Log.Error("Kalibrierungsdatei ungültig (Matrix fehlt oder nicht 3×3)");
                return false;
            }

            _matrix = daten.Matrix;

            // Modus-String zurück parsen
            if (Enum.TryParse<KalibrierungsModus>(daten.Modus, ignoreCase: true, out var modus))
                VerwendeterModus = modus;
            else
                VerwendeterModus = KalibrierungsModus.Auto;

            Log.Information("Farb-Kalibrierung geladen (Modus={Modus})", VerwendeterModus);
            MatrixProtokollieren();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Laden der Farb-Kalibrierung fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    // ---------------------------------------------------------------------
    //  ColorChecker-Modus
    // ---------------------------------------------------------------------

    /// <summary>
    /// Erkennt die 24 Felder eines Macbeth ColorCheckers im Bild und
    /// berechnet eine 3×3 Farb-Transfer-Matrix via Least-Squares.
    /// </summary>
    /// <param name="bgr">Referenzbild im BGR-Format.</param>
    /// <returns>true bei Erfolg.</returns>
    private bool ColorCheckerKalibrierung(Mat bgr)
    {
        if (!ColorCheckerFelderErkennen(bgr, out double[] gemesseneRgb))
        {
            Log.Warning("ColorChecker-Felder konnten nicht erkannt werden");
            return false;
        }

        // Least-Squares: für jeden Ausgabekanal c lösen wir
        //   A * x_c = b_c  mit A (24×3) = gemessene [R,G,B], b_c (24×1) = Referenzkanal c
        //   M[c] = x_c^T
        using Mat a = new(AnzahlFelder, 3, MatType.CV_64FC1);
        using Mat bR = new(AnzahlFelder, 1, MatType.CV_64FC1);
        using Mat bG = new(AnzahlFelder, 1, MatType.CV_64FC1);
        using Mat bB = new(AnzahlFelder, 1, MatType.CV_64FC1);

        for (int i = 0; i < AnzahlFelder; i++)
        {
            a.Set<double>(i, 0, gemesseneRgb[i * 3 + 0]); // gemessenes R
            a.Set<double>(i, 1, gemesseneRgb[i * 3 + 1]); // gemessenes G
            a.Set<double>(i, 2, gemesseneRgb[i * 3 + 2]); // gemessenes B

            bR.Set<double>(i, 0, ColorCheckerReferenz[i * 3 + 0]);
            bG.Set<double>(i, 0, ColorCheckerReferenz[i * 3 + 1]);
            bB.Set<double>(i, 0, ColorCheckerReferenz[i * 3 + 2]);
        }

        using Mat xR = new();
        using Mat xG = new();
        using Mat xB = new();

        // Cv2.Solve mit SVD löst überbestimmte Systeme im Least-Squares-Sinn
        bool okR = Cv2.Solve(a, bR, xR, DecompTypes.SVD);
        bool okG = Cv2.Solve(a, bG, xG, DecompTypes.SVD);
        bool okB = Cv2.Solve(a, bB, xB, DecompTypes.SVD);

        if (!okR || !okG || !okB)
        {
            Log.Error("Least-Squares-Lösung fehlgeschlagen (R={R}, G={G}, B={B})", okR, okG, okB);
            return false;
        }

        // 3×3 Matrix zusammenbauen: Zeile 0 = R-Ausgang, Zeile 1 = G, Zeile 2 = B
        _matrix = new double[3, 3];
        for (int c = 0; c < 3; c++)
        {
            _matrix[0, c] = xR.At<double>(c, 0);
            _matrix[1, c] = xG.At<double>(c, 0);
            _matrix[2, c] = xB.At<double>(c, 0);
        }

        VerwendeterModus = KalibrierungsModus.ColorChecker;
        Log.Information("ColorChecker-Kalibrierung: 24 Felder, 3×3 Matrix via SVD berechnet");
        return true;
    }

    /// <summary>
    /// Erkennt die 24 Felder des ColorCheckers im Bild.
    /// Vorgehen: Größte 4-Eck-Kontur finden → Perspektive entzerren →
    /// 6×4-Raster abtasten → mittlere RGB-Werte pro Feld.
    /// Die Graukeil-Reihe wird zur Orientierung genutzt (niedrigste Farbvarianz).
    /// </summary>
    /// <param name="bgr">Referenzbild (BGR).</param>
    /// <param name="gemesseneRgb">Ausgabe: 24×3 RGB-Mittelwerte (0–255), Zeile für Zeile.</param>
    /// <returns>true, falls 24 Felder erfolgreich erkannt wurden.</returns>
    private bool ColorCheckerFelderErkennen(Mat bgr, out double[] gemesseneRgb)
    {
        gemesseneRgb = [];

        // Graustufen + Weichzeichnen für die Kantenerkennung
        using Mat grau = new();
        Cv2.CvtColor(bgr, grau, ColorConversionCodes.BGR2GRAY);
        using Mat weich = new();
        Cv2.GaussianBlur(grau, weich, new Size(5, 5), 0);

        // Canny-Kanten + Konturen
        using Mat kanten = new();
        Cv2.Canny(weich, kanten, 30, 150);
        Cv2.FindContours(kanten, out Point[][] konturen, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

        if (konturen.Length == 0)
        {
            Log.Warning("ColorChecker: keine Konturen gefunden");
            return false;
        }

        // Größte Kontur suchen, die sich durch 4 Eckpunkte approximieren lässt
        Point2f[] ecken = [];
        double maxFlaeche = 0;
        foreach (var kontur in konturen)
        {
            double flaeche = Cv2.ContourArea(kontur);
            if (flaeche < bgr.Width * bgr.Height * 0.05)
                continue; // zu kleine Konturen überspringen

            double peri = Cv2.ArcLength(kontur, true);
            Point[] approx = Cv2.ApproxPolyDP(kontur, peri * 0.02, true);
            if (approx.Length != 4)
                continue;

            if (flaeche > maxFlaeche)
            {
                maxFlaeche = flaeche;
                ecken = approx.Select(p => new Point2f(p.X, p.Y)).ToArray();
            }
        }

        if (ecken.Length != 4 || maxFlaeche <= 0)
        {
            Log.Warning("ColorChecker: keine geeignete 4-Eck-Kontur gefunden");
            return false;
        }

        Log.Debug("ColorChecker: Begrenzungs-Kontur gefunden, Fläche={Flaeche:F0}px", maxFlaeche);

        // Ecken sortieren: oben-links, oben-rechts, unten-rechts, unten-links
        Point2f[] sortiert = EckenSortieren(ecken);

        // Perspektive auf ein festes 6×4-Raster entzerren
        const int zellGroesse = 200;
        int rasterBreite = Spalten * zellGroesse;
        int rasterHoehe = Zeilen * zellGroesse;

        Point2f[] ziel =
        [
            new(0, 0),
            new(rasterBreite - 1, 0),
            new(rasterBreite - 1, rasterHoehe - 1),
            new(0, rasterHoehe - 1)
        ];

        using Mat perspektive = Cv2.GetPerspectiveTransform(sortiert, ziel);
        using Mat entzerrt = new();
        Cv2.WarpPerspective(bgr, entzerrt, perspektive, new Size(rasterBreite, rasterHoehe));

        // 6×4-Raster abtasten: mittlere 40 % jeder Zelle als ROI
        var rohWerte = new double[Zeilen * Spalten, 3];
        for (int zeile = 0; zeile < Zeilen; zeile++)
        {
            for (int spalte = 0; spalte < Spalten; spalte++)
            {
                int x0 = spalte * zellGroesse + zellGroesse * 3 / 10;
                int y0 = zeile * zellGroesse + zellGroesse * 3 / 10;
                int w = zellGroesse * 4 / 10;
                using Mat roi = entzerrt[new Rect(x0, y0, w, w)];
                Scalar mean = Cv2.Mean(roi);

                // BGR → RGB umrechnen (mean[0]=B, mean[1]=G, mean[2]=R)
                rohWerte[zeile * Spalten + spalte, 0] = mean[2]; // R
                rohWerte[zeile * Spalten + spalte, 1] = mean[1]; // G
                rohWerte[zeile * Spalten + spalte, 2] = mean[0]; // B
            }
        }

        // Orientierung prüfen: die Graukeil-Zeile (unterste im Standard-Layout)
        // hat die geringste Farb-Sättigung / Varianz über die Kanäle.
        int grauZeile = GraukeilZeileFinden(rohWerte);
        if (grauZeile == 0)
        {
            // Graukeil ist oben → Bild ist auf dem Kopf, Zeilen umkehren
            Log.Debug("ColorChecker: Orientierung vertikal umgedreht (Graukeil oben erkannt)");
            rohWerte = VertikalSpiegeln(rohWerte);
        }

        // Flaches Ausgabe-Array erstellen
        gemesseneRgb = new double[AnzahlFelder * 3];
        for (int i = 0; i < AnzahlFelder; i++)
        {
            gemesseneRgb[i * 3 + 0] = rohWerte[i, 0];
            gemesseneRgb[i * 3 + 1] = rohWerte[i, 1];
            gemesseneRgb[i * 3 + 2] = rohWerte[i, 2];
        }

        Log.Debug("ColorChecker: 24 Felder abgetastet (Graukeil in Zeile {Zeile})", grauZeile);
        return true;
    }

    /// <summary>
    /// Sortiert vier Eckpunkte in die Reihenfolge
    /// oben-links, oben-rechts, unten-rechts, unten-links.
    /// </summary>
    private static Point2f[] EckenSortieren(Point2f[] ecken)
    {
        // Nach Summe (tl=min) bzw. Differenz (tr=max X-Y) sortieren
        var nachSumme = ecken.OrderBy(p => p.X + p.Y).ToArray();
        var nachDiff = ecken.OrderBy(p => p.Y - p.X).ToArray();

        Point2f tl = nachSumme[0];            // kleinste Summe
        Point2f br = nachSumme[^1];           // größte Summe
        Point2f tr = nachDiff[0];             // kleinste Differenz (Y-X) → oben-rechts
        Point2f bl = nachDiff[^1];            // größte Differenz → unten-links
        return [tl, tr, br, bl];
    }

    /// <summary>
    /// Findet die Zeile mit der geringsten kanalübergreifenden Varianz
    /// (der Graukeil im Macbeth-ColorChecker-Standardlayout = Zeile 3).
    /// </summary>
    private static int GraukeilZeileFinden(double[,] werte)
    {
        int besteZeile = 0;
        double minVarianz = double.MaxValue;
        for (int zeile = 0; zeile < Zeilen; zeile++)
        {
            double varianz = 0;
            for (int spalte = 0; spalte < Spalten; spalte++)
            {
                double r = werte[zeile * Spalten + spalte, 0];
                double g = werte[zeile * Spalten + spalte, 1];
                double b = werte[zeile * Spalten + spalte, 2];
                double mittel = (r + g + b) / 3.0;
                varianz += Math.Abs(r - mittel) + Math.Abs(g - mittel) + Math.Abs(b - mittel);
            }
            if (varianz < minVarianz)
            {
                minVarianz = varianz;
                besteZeile = zeile;
            }
        }
        return besteZeile;
    }

    /// <summary>
    /// Spiegelt die Rasterwerte vertikal (Zeilenreihenfolge umkehren).
    /// </summary>
    private static double[,] VertikalSpiegeln(double[,] werte)
    {
        double[,] ergebnis = new double[AnzahlFelder, 3];
        for (int zeile = 0; zeile < Zeilen; zeile++)
        {
            int zielZeile = Zeilen - 1 - zeile;
            for (int spalte = 0; spalte < Spalten; spalte++)
            {
                ergebnis[zielZeile * Spalten + spalte, 0] = werte[zeile * Spalten + spalte, 0];
                ergebnis[zielZeile * Spalten + spalte, 1] = werte[zeile * Spalten + spalte, 1];
                ergebnis[zielZeile * Spalten + spalte, 2] = werte[zeile * Spalten + spalte, 2];
            }
        }
        return ergebnis;
    }

    // ---------------------------------------------------------------------
    //  Graukarten-Modus
    // ---------------------------------------------------------------------

    /// <summary>
    /// Graukarten-Modus: Berechnet einen Weißabgleich, indem der Bildmittelwert
    /// als neutralgrau angenommen wird. Die Korrektur-Matrix ist diagonal.
    /// </summary>
    /// <param name="bgr">Referenzbild im BGR-Format (vorzugsweise eine Graukarte).</param>
    /// <returns>true bei Erfolg.</returns>
    private bool GraukartenKalibrierung(Mat bgr)
    {
        try
        {
            Scalar mean = Cv2.Mean(bgr);
            // BGR-Reihenfolge: mean[0]=B, mean[1]=G, mean[2]=R
            double meanB = mean[0];
            double meanG = mean[1];
            double meanR = mean[2];

            // Neutraler Zielwert = Durchschnitt der drei Kanäle
            double ziel = (meanR + meanG + meanB) / 3.0;
            if (ziel < 1e-6)
            {
                Log.Error("Graukarten-Kalibrierung: Bild ist zu dunkel (Mittelwert={Mittel:F2})", ziel);
                return false;
            }

            // Diagonale Weißabgleich-Matrix (im RGB-Raum)
            double gainR = ziel / meanR;
            double gainG = ziel / meanG;
            double gainB = ziel / meanB;

            _matrix = new double[3, 3];
            _matrix[0, 0] = gainR;
            _matrix[1, 1] = gainG;
            _matrix[2, 2] = gainB;

            VerwendeterModus = KalibrierungsModus.Graukarte;
            Log.Information("Graukarten-Kalibrierung: R={R:F3} G={G:F3} B={B:F3} → Gain R={GR:F3} G={GG:F3} B={GB:F3}",
                meanR, meanG, meanB, gainR, gainG, gainB);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Graukarten-Kalibrierung fehlgeschlagen");
            return false;
        }
    }

    // ---------------------------------------------------------------------
    //  Hilfsmethoden
    // ---------------------------------------------------------------------

    /// <summary>
    /// Protokolliert die berechnete 3×3 Matrix auf Debug-Level.
    /// </summary>
    private void MatrixProtokollieren()
    {
        if (_matrix is null)
            return;
        Log.Debug("Farb-Matrix: R=[{R0:F4}, {R1:F4}, {R2:F4}]  G=[{G0:F4}, {G1:F4}, {G2:F4}]  B=[{B0:F4}, {B1:F4}, {B2:F4}]",
            _matrix[0, 0], _matrix[0, 1], _matrix[0, 2],
            _matrix[1, 0], _matrix[1, 1], _matrix[1, 2],
            _matrix[2, 0], _matrix[2, 1], _matrix[2, 2]);
    }

    /// <summary>
    /// JSON-Serialisierungsoptionen für die Kalibrierungsdaten.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptionen = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// DTO für die JSON-Serialisierung der Farb-Kalibrierungsdaten.
    /// </summary>
    private sealed class KalibrierungsDaten
    {
        /// <summary>Verwendeter Kalibrierungsmodus als String.</summary>
        public string Modus { get; set; } = nameof(KalibrierungsModus.Auto);

        /// <summary>3×3 Farb-Transfer-Matrix (RGB → RGB).</summary>
        public double[,] Matrix { get; set; } = new double[3, 3];
    }
}