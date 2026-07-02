using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;

namespace FlipsiColor.Color;

/// <summary>
/// Distortion-Grid-Korrektur: Kalibriert Linsenverzerrung anhand eines
/// Schachbrett-Referenzbilds (OpenCV calibrateCamera) und entzerrt
/// anschließend beliebige Bilder (cv2.undistort).
/// </summary>
public sealed class DistortionGridCorrector
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<DistortionGridCorrector>();

    // Schachbrett-Mustergröße (innere Ecken pro Spalte/Reihe) — Standard 9×6
    private Size _musterGroesse = new(9, 6);

    // Kalibrierungsergebnisse
    private double[,]? _kameraMatrix;
    private double[]? _distortionKoeffizienten;
    private Size _bildGroesse;

    /// <summary>
    /// Gibt an, ob eine gültige Kalibrierung vorliegt.
    /// </summary>
    public bool IstKalibriert => _kameraMatrix is not null && _distortionKoeffizienten is not null;

    /// <summary>
    /// Setzt die erwartete Anzahl innerer Ecken des Schachbretts.
    /// Muss vor Kalibrieren() aufgerufen werden, falls das Standardmaß nicht passt.
    /// </summary>
    /// <param name="eckenProSpalte">Anzahl innerer Ecken in horizontaler Richtung.</param>
    /// <param name="eckenProReihe">Anzahl innerer Ecken in vertikaler Richtung.</param>
    public void SetzeMusterGroesse(int eckenProSpalte, int eckenProReihe)
    {
        _musterGroesse = new Size(eckenProSpalte, eckenProReihe);
        Log.Debug("Mustergröße gesetzt: {Breite}×{Hoehe} innere Ecken", eckenProSpalte, eckenProReihe);
    }

    /// <summary>
    /// Kalibriert die Linsenverzerrung anhand eines Schachbrett-Referenzbilds.
    /// Findet Corners, verfeinert diese subpixelgenau und berechnet
    /// Kamera-Matrix + Distortion-Koeffizienten via OpenCV calibrateCamera.
    /// </summary>
    /// <param name="referenzBildPfad">Pfad zum Schachbrett-Foto.</param>
    /// <returns>true bei erfolgreicher Kalibrierung.</returns>
    public bool Kalibrieren(string referenzBildPfad)
    {
        Log.Information("Starte Distortion-Grid-Kalibrierung: {Pfad}", referenzBildPfad);

        if (!File.Exists(referenzBildPfad))
        {
            Log.Error("Referenzbild nicht gefunden: {Pfad}", referenzBildPfad);
            return false;
        }

        using Mat bild = Cv2.ImRead(referenzBildPfad, ImreadModes.Color);
        if (bild.Empty())
        {
            Log.Error("Referenzbild konnte nicht geladen werden: {Pfad}", referenzBildPfad);
            return false;
        }

        _bildGroesse = bild.Size();
        Log.Debug("Referenzbild geladen: {Breite}×{Hoehe}", bild.Width, bild.Height);

        // In Graustufen konvertieren für Corner-Detection
        using Mat grau = new();
        Cv2.CvtColor(bild, grau, ColorConversionCodes.BGR2GRAY);

        // Schachbrett-Corners suchen
        if (!Cv2.FindChessboardCorners(grau, _musterGroesse, out Point2f[] corners,
                ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage | ChessboardFlags.FilterQuads))
        {
            Log.Error("Keine Schachbrett-Corners gefunden (Muster {Breite}×{Hoehe}) — " +
                      "evtl. falsche Mustergröße oder Bild ungeeignet",
                _musterGroesse.Width, _musterGroesse.Height);
            return false;
        }

        Log.Information("Schachbrett-Corners gefunden: {Anzahl}", corners.Length);

        // Subpixel-Verfeinerung der gefundenen Corners
        Cv2.CornerSubPix(grau, corners,
            new Size(11, 11), new Size(-1, -1),
            new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.01));
        Log.Debug("Subpixel-Verfeinerung abgeschlossen");

        // Objektpunkte (3D-Koordinaten der Schachbrett-Ecken im Weltkoordinatensystem, Z=0)
        int eckenGesamt = _musterGroesse.Width * _musterGroesse.Height;
        Point3f[] objektPunkte = new Point3f[eckenGesamt];
        int idx = 0;
        for (int y = 0; y < _musterGroesse.Height; y++)
        {
            for (int x = 0; x < _musterGroesse.Width; x++)
            {
                objektPunkte[idx++] = new Point3f(x, y, 0f);
            }
        }

        // Punkte in Mat konvertieren für CalibrateCamera
        using Mat objektMat = Mat.FromArray(objektPunkte);
        using Mat bildMat = Mat.FromArray(corners);
        Mat[] objektPunkteListe = [objektMat];
        Mat[] bildPunkteListe = [bildMat];

        // Kamera-Matrix mit Brennweiten-Schätzung initialisieren
        using Mat kameraMat = Mat.Eye(3, 3, MatType.CV_64FC1);
        kameraMat.Set<double>(0, 2, _bildGroesse.Width / 2.0);
        kameraMat.Set<double>(1, 2, _bildGroesse.Height / 2.0);
        kameraMat.Set<double>(0, 0, _bildGroesse.Width);
        kameraMat.Set<double>(1, 1, _bildGroesse.Width);

        // Distortion-Koeffizienten mit Null initialisieren (5 Standard-Koeffizienten)
        using Mat distMat = new Mat(1, 5, MatType.CV_64FC1, Scalar.All(0));

        try
        {
            // CalibrateCamera mit Mat[]-Überladung für rvecs/tvecs
            double rms = Cv2.CalibrateCamera(
                objektPunkteListe,
                bildPunkteListe,
                _bildGroesse,
                kameraMat,
                distMat,
                out Mat[] rvecs,
                out Mat[] tvecs,
                CalibrationFlags.None,
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 1e-6));

            Log.Information("Kalibrierung abgeschlossen — RMS-Reprojektionsfehler: {Rms:F4}", rms);
            Log.Debug("Rotation-Vektoren: {Anzahl}, Translations-Vektoren: {AnzahlT}",
                rvecs.Length, tvecs.Length);

            // rvecs/tvecs-Mats freigeben (werden nicht weiter benötigt)
            foreach (Mat m in rvecs) m.Dispose();
            foreach (Mat m in tvecs) m.Dispose();

            // Kamera-Matrix (3×3) extrahieren
            _kameraMatrix = new double[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    _kameraMatrix[i, j] = kameraMat.At<double>(i, j);
                }
            }

            // Distortion-Koeffizienten extrahieren (k1, k2, p1, p2, k3)
            int distAnzahl = distMat.Cols * distMat.Rows;
            _distortionKoeffizienten = new double[distAnzahl];
            for (int i = 0; i < distAnzahl; i++)
            {
                _distortionKoeffizienten[i] = distMat.At<double>(0, i);
            }

            Log.Debug("Kamera-Matrix und {Anzahl} Distortion-Koeffizienten gespeichert", distAnzahl);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CalibrateCamera fehlgeschlagen");
            _kameraMatrix = null;
            _distortionKoeffizienten = null;
            return false;
        }
    }

    /// <summary>
    /// Entzerrt ein Bild mit den kalibrierten Koeffizienten (cv2.undistort).
    /// </summary>
    /// <param name="bild">Eingabebild (beliebiges Format).</param>
    /// <returns>Entzerrtes Bild; bei fehlender Kalibrierung oder Fehler das Original.</returns>
    public Mat Korrigieren(Mat bild)
    {
        if (!IstKalibriert)
        {
            Log.Warning("Korrigieren aufgerufen ohne vorherige Kalibrierung — Bild unverändert");
            return bild;
        }

        if (bild.Empty())
        {
            Log.Warning("Korrigieren: leeres Eingabebild");
            return bild;
        }

        try
        {
            // Kamera-Matrix als Mat rekonstruieren
            using Mat kameraMat = new(3, 3, MatType.CV_64FC1);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    kameraMat.Set<double>(i, j, _kameraMatrix![i, j]);
                }
            }

            // Distortion-Koeffizienten als Mat rekonstruieren
            using Mat distMat = new(1, _distortionKoeffizienten!.Length, MatType.CV_64FC1);
            for (int i = 0; i < _distortionKoeffizienten.Length; i++)
            {
                distMat.Set<double>(0, i, _distortionKoeffizienten[i]);
            }

            // Undistort anwenden
            Mat ergebnis = new();
            Cv2.Undistort(bild, ergebnis, kameraMat, distMat);

            Log.Information("Distortion-Korrektur angewendet: {Breite}×{Hoehe}",
                ergebnis.Width, ergebnis.Height);
            return ergebnis;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Undistort fehlgeschlagen — gebe Original zurück");
            return bild;
        }
    }

    /// <summary>
    /// Speichert die Kalibrierung als JSON-Datei.
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

        try
        {
            KalibrierungsDaten daten = new()
            {
                BildBreite = _bildGroesse.Width,
                BildHoehe = _bildGroesse.Height,
                KameraMatrix = _kameraMatrix!,
                DistortionKoeffizienten = _distortionKoeffizienten!
            };

            string json = JsonSerializer.Serialize(daten, KalibrierungsJsonOptionen);
            File.WriteAllText(pfad, json);
            Log.Information("Kalibrierung gespeichert: {Pfad}", pfad);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Speichern der Kalibrierung fehlgeschlagen: {Pfad}", pfad);
            return false;
        }
    }

    /// <summary>
    /// Lädt eine Kalibrierung aus einer JSON-Datei.
    /// </summary>
    /// <param name="pfad">Quelldatei-Pfad.</param>
    /// <returns>true bei Erfolg.</returns>
    public bool Laden(string pfad)
    {
        if (!File.Exists(pfad))
        {
            Log.Error("Kalibrierungsdatei nicht gefunden: {Pfad}", pfad);
            return false;
        }

        try
        {
            string json = File.ReadAllText(pfad);
            KalibrierungsDaten? daten = JsonSerializer.Deserialize<KalibrierungsDaten>(
                json, KalibrierungsJsonOptionen);

            if (daten is null || daten.KameraMatrix is null || daten.DistortionKoeffizienten is null)
            {
                Log.Error("Kalibrierungsdatei ungültig: {Pfad}", pfad);
                return false;
            }

            _bildGroesse = new Size(daten.BildBreite, daten.BildHoehe);
            _kameraMatrix = daten.KameraMatrix;
            _distortionKoeffizienten = daten.DistortionKoeffizienten;

            Log.Information("Kalibrierung geladen: {Pfad} ({Breite}×{Hoehe})",
                pfad, _bildGroesse.Width, _bildGroesse.Height);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Laden der Kalibrierung fehlgeschlagen: {Pfad}", pfad);
            return false;
        }
    }

    /// <summary>
    /// JSON-Serialisierungsoptionen für Kalibrierungsdaten.
    /// </summary>
    private static readonly JsonSerializerOptions KalibrierungsJsonOptionen = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// DTO für JSON-Serialisierung der Kalibrierungsdaten.
    /// </summary>
    private sealed class KalibrierungsDaten
    {
        /// <summary>Bildbreite des Referenzbilds in Pixeln.</summary>
        public int BildBreite { get; set; }

        /// <summary>Bildhöhe des Referenzbilds in Pixeln.</summary>
        public int BildHoehe { get; set; }

        /// <summary>3×3 Kamera-Matrix (Brennweite + Hauptpunkt).</summary>
        public double[,] KameraMatrix { get; set; } = new double[3, 3];

        /// <summary>Distortion-Koeffizienten (k1, k2, p1, p2, k3, …).</summary>
        public double[] DistortionKoeffizienten { get; set; } = [];
    }
}