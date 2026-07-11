using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using FlipsiColor.Utils;

namespace FlipsiColor.Color;

/// <summary>
/// ChArUco-Kamerakalibrierung — Alternative zum Schachbrett-Verfahren in
/// <see cref="DistortionGridCorrector"/> für Kameras/Objektive die nicht in
/// der Lensfun-Datenbank enthalten sind.
///
/// ChArUco-Boards kombinieren ein Schachbrett mit ArUco-Markern und bieten
/// folgende Vorteile gegenüber reinen Schachbrett-Mustern:
///   - Subpixel-genau dank Corner-SubPix-Verfeinerung
///   - Robuster bei teilweiser Okklusion (nicht alle Marker müssen sichtbar sein)
///   - Funktioniert mit beliebigen Board-Größen
///
/// Implementiert nach Issue #5 von MarcoRavich.
/// Verwendet die OpenCvSharp4 ArUco-API (OpenCV 4.7+ Detector-Class-Design):
///   - CharucoBoard-Klasse zum Erstellen des Boards
///   - CharucoDetector.DetectBoard für Corner-Detektion
///   - Board.MatchImagePoints + Cv2.CalibrateCamera für Kalibrierung
///
/// Referenzen (von MarcoRavich verlinkt):
///   - nullboundary/CharucoCalibration: DICT_4X4_50, 12×8 Board, calibrateCameraCharuco
///   - Rosatus/CameraCalibrationWorkbench: Schachbrett + Fisheye-Support, SB-Detektion
///   - yumashino/Camera-Calibration-with-ChArUco-Board: ChArUco-Detection-Workflow
/// </summary>
public sealed class CharucoCalibrator : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<CharucoCalibrator>();

    // ChArUco-Board-Parameter
    // Standard: 7×5 Squares (wie nullboundary: 12×8 ist möglich, aber 7×5 ist kompakter)
    private int _squaresX = 7;
    private int _squaresY = 5;
    private float _squareSize = 0.035f;  // 35mm (wie nullboundary)
    private float _markerSize = 0.0175f; // 17.5mm (wie nullboundary)
    private PredefinedDictionaryType _dictType = PredefinedDictionaryType.Dict4x4_50;

    // Kalibrierungsergebnisse
    private double[,]? _kameraMatrix;
    private double[]? _distortionKoeffizienten;
    private Size _bildGroesse;

    private bool _disposed;

    /// <summary>
    /// Gibt an, ob eine gültige Kalibrierung vorliegt.
    /// </summary>
    public bool IstKalibriert => _kameraMatrix is not null && _distortionKoeffizienten is not null;

    /// <summary>
    /// Setzt die ChArUco-Board-Parameter.
    /// Muss vor <see cref="Kalibrieren"/> bzw. <see cref="BoardGenerieren"/> aufgerufen werden.
    /// </summary>
    /// <param name="squaresX">Anzahl der Quadrate in X-Richtung (Standard: 7).</param>
    /// <param name="squaresY">Anzahl der Quadrate in Y-Richtung (Standard: 5).</param>
    /// <param name="squareSize">Quadrat-Größe in Metern (Standard: 0.035 = 35mm).</param>
    /// <param name="markerSize">Marker-Größe in Metern (muss kleiner als squareSize sein, Standard: 0.0175 = 17.5mm).</param>
    /// <param name="dictType">ArUco-Dictionary-Typ (Standard: DICT_4X4_50 wie nullboundary).</param>
    public void SetzeBoardParameter(int squaresX, int squaresY, float squareSize, float markerSize,
        PredefinedDictionaryType? dictType = null)
    {
        if (squaresX < 4 || squaresY < 4)
            throw new ArgumentException("ChArUco-Board muss mindestens 4×4 Quadrate haben");
        if (markerSize >= squareSize)
            throw new ArgumentException("Marker-Größe muss kleiner als Square-Größe sein");

        _squaresX = squaresX;
        _squaresY = squaresY;
        _squareSize = squareSize;
        _markerSize = markerSize;
        if (dictType.HasValue)
            _dictType = dictType.Value;

        Log.Debug("ChArUco-Board-Parameter gesetzt: {X}×{Y} Squares, Square={Sq}m, Marker={Mk}m, Dict={Dict}",
            squaresX, squaresY, squareSize, markerSize, _dictType);
    }

    /// <summary>
    /// Generiert ein ChArUco-Board-Bild und speichert es als PNG.
    /// Kann ausgedruckt und als Kalibrierungs-Referenz verwendet werden.
    /// Nutzt CharucoBoard.GenerateImage (OpenCV 4.7+ API).
    /// </summary>
    /// <param name="ausgabePfad">Zieldatei-Pfad (.png oder .jpg).</param>
    /// <param name="pixelBreite">Breite des Ausgabebilds in Pixeln (Standard: 1400).</param>
    /// <param name="pixelHoehe">Höhe des Ausgabebilds in Pixeln (Standard: 1000).</param>
    /// <returns>true bei Erfolg.</returns>
    public bool BoardGenerieren(string ausgabePfad, int pixelBreite = 1400, int pixelHoehe = 1000)
    {
        var erlaubeEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp" };
        var validierterPfad = SecurityValidator.ValidiereAusgabePfad(ausgabePfad, erlaubeEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("ChArUco BoardGenerieren: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        try
        {
            var dict = Cv2.Aruco.GetPredefinedDictionary(_dictType);
            using var board = new CharucoBoard(_squaresX, _squaresY, _squareSize, _markerSize, dict);

            using Mat boardImage = new();
            board.GenerateImage(new Size(pixelBreite, pixelHoehe), boardImage);
            Cv2.ImWrite(validierterPfad, boardImage);

            Log.Information("ChArUco-Board generiert: {X}×{Y} Squares → {Pfad} ({W}×{H}px)",
                _squaresX, _squaresY, validierterPfad, pixelBreite, pixelHoehe);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChArUco-Board-Generierung fehlgeschlagen");
            return false;
        }
    }

    /// <summary>
    /// Kalibriert die Linsenverzerrung anhand eines oder mehrerer ChArUco-Referenzbilder.
    /// Workflow (OpenCV 4.7+ API):
    ///   1. CharucoBoard + CharucoDetector erstellen
    ///   2. Für jedes Bild: DetectBoard → charucoCorners + charucoIds
    ///   3. Subpixel-Verfeinerung mit CornerSubPix
    ///   4. Board.MatchImagePoints → objPoints + imgPoints
    ///   5. Cv2.CalibrateCamera → Kamera-Matrix + Distortion-Koeffizienten
    ///
    /// Empfohlen: 5-15 Bilder aus verschiedenen Winkeln (wie nullboundary: decimator%3).
    /// </summary>
    /// <param name="referenzBildPfade">Liste von Pfaden zu ChArUco-Board-Fotos.</param>
    /// <returns>true bei erfolgreicher Kalibrierung.</returns>
    public bool Kalibrieren(IEnumerable<string> referenzBildPfade)
    {
        var pfade = new List<string>(referenzBildPfade);
        if (pfade.Count == 0)
        {
            Log.Error("ChArUco-Kalibrierung: keine Referenzbilder übergeben");
            return false;
        }

        Log.Information("Starte ChArUco-Kalibrierung mit {Anzahl} Referenzbildern", pfade.Count);

        var dict = Cv2.Aruco.GetPredefinedDictionary(_dictType);
        using var board = new CharucoBoard(_squaresX, _squaresY, _squareSize, _markerSize, dict);
        var detector = new CharucoDetector(board);

        // Sammle alle erkannten Corners und IDs
        var alleCharucoCorners = new List<Point2f[]>();
        var alleCharucoIds = new List<int[]>();
        Size imageSize = new();
        int erfolgreich = 0;

        foreach (var pfad in pfade)
        {
            var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad);
            if (validierterPfad == null)
            {
                Log.Warning("ChArUco: Pfad-Validierung fehlgeschlagen für ein Bild — übersprungen");
                continue;
            }

            using Mat bild = Cv2.ImRead(validierterPfad, ImreadModes.Color);
            if (bild.Empty())
            {
                Log.Warning("ChArUco: Bild konnte nicht geladen werden — übersprungen");
                continue;
            }

            imageSize = bild.Size();
            _bildGroesse = imageSize;

            using Mat grau = new();
            Cv2.CvtColor(bild, grau, ColorConversionCodes.BGR2GRAY);

            // ChArUco-Corners detektieren via CharucoDetector.DetectBoard
            detector.DetectBoard(grau,
                out Point2f[] charucoCorners,
                out int[] charucoIds,
                out Point2f[][] markerCorners,
                out int[] markerIds);

            if (charucoCorners.Length < 4)
            {
                Log.Warning("ChArUco: Zu wenige Corners ({Anzahl}) in Bild {Index} — übersprungen",
                    charucoCorners.Length, erfolgreich + 1);
                continue;
            }

            // Subpixel-Verfeinerung (wie nullboundary und Rosatus)
            Cv2.CornerSubPix(grau, charucoCorners,
                new Size(11, 11), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.01));

            alleCharucoCorners.Add(charucoCorners);
            alleCharucoIds.Add(charucoIds);
            erfolgreich++;

            Log.Debug("ChArUco: Bild {Index} — {Marker} Marker, {Corners} Corners",
                erfolgreich, markerIds.Length, charucoCorners.Length);
        }

        if (erfolgreich < 3)
        {
            Log.Error("ChArUco-Kalibrierung: nur {Anzahl} gültige Bilder (Minimum 3 erforderlich)", erfolgreich);
            return false;
        }

        // MatchImagePoints + CalibrateCamera (OpenCV 4.7+ Pattern)
        // Statt calibrateCameraCharuco (deprecated in 4.7+) nutzen wir:
        //   board.MatchImagePoints → objPoints + imgPoints
        //   Cv2.CalibrateCamera → Kamera-Matrix + Distortion
        var alleObjPoints = new List<Mat>();
        var alleImgPoints = new List<Mat>();

        try
        {
            for (int i = 0; i < alleCharucoCorners.Count; i++)
            {
                using Mat idsMat = Mat.FromArray(alleCharucoIds[i]);
                Mat objPoints = new();
                Mat imgPoints = new();
                board.MatchImagePoints(alleCharucoCorners[i], idsMat, objPoints, imgPoints);
                alleObjPoints.Add(objPoints);
                alleImgPoints.Add(imgPoints);
            }

            // Kamera-Matrix initialisieren
            using Mat kameraMat = Mat.Eye(3, 3, MatType.CV_64FC1);
            kameraMat.Set<double>(0, 2, imageSize.Width / 2.0);
            kameraMat.Set<double>(1, 2, imageSize.Height / 2.0);
            kameraMat.Set<double>(0, 0, imageSize.Width);
            kameraMat.Set<double>(1, 1, imageSize.Width);

            using Mat distMat = new Mat(1, 5, MatType.CV_64FC1, Scalar.All(0));

            double rms = Cv2.CalibrateCamera(
                alleObjPoints.ToArray(),
                alleImgPoints.ToArray(),
                imageSize,
                kameraMat,
                distMat,
                out Mat[] rvecs,
                out Mat[] tvecs,
                CalibrationFlags.None,
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 1e-6));

            // rvecs/tvecs freigeben
            foreach (Mat m in rvecs) m.Dispose();
            foreach (Mat m in tvecs) m.Dispose();

            Log.Information("ChArUco-Kalibrierung abgeschlossen — RMS: {Rms:F4} ({Anzahl} Bilder)", rms, erfolgreich);

            // Ergebnisse extrahieren
            _kameraMatrix = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    _kameraMatrix[i, j] = kameraMat.At<double>(i, j);

            int distAnzahl = distMat.Cols * distMat.Rows;
            _distortionKoeffizienten = new double[distAnzahl];
            for (int i = 0; i < distAnzahl; i++)
                _distortionKoeffizienten[i] = distMat.At<double>(0, i);

            Log.Debug("ChArUco: Kamera-Matrix und {Anzahl} Distortion-Koeffizienten gespeichert", distAnzahl);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChArUco CalibrateCamera fehlgeschlagen");
            _kameraMatrix = null;
            _distortionKoeffizienten = null;
            return false;
        }
        finally
        {
            foreach (var m in alleObjPoints) m.Dispose();
            foreach (var m in alleImgPoints) m.Dispose();
        }
    }

    /// <summary>
    /// Convenience-Überladung für ein einzelnes Referenzbild.
    /// Für beste Ergebnisse sollten 5-15 Bilder aus verschiedenen Winkeln verwendet werden.
    /// </summary>
    /// <param name="referenzBildPfad">Pfad zum ChArUco-Board-Foto.</param>
    /// <returns>true bei erfolgreicher Kalibrierung.</returns>
    public bool Kalibrieren(string referenzBildPfad) => Kalibrieren(new[] { referenzBildPfad });

    /// <summary>
    /// Entzerrt ein Bild mit den kalibrierten Koeffizienten (cv2.undistort).
    /// Identisch zur DistortionGridCorrector.Korrigieren-Methode.
    /// </summary>
    /// <param name="bild">Eingabebild (beliebiges Format).</param>
    /// <returns>Entzerrtes Bild; bei fehlender Kalibrierung oder Fehler das Original.</returns>
    public Mat Korrigieren(Mat bild)
    {
        if (!IstKalibriert)
        {
            Log.Warning("ChArUco: Korrigieren aufgerufen ohne vorherige Kalibrierung — Bild unverändert");
            return bild;
        }

        if (bild.Empty())
        {
            Log.Warning("ChArUco: Korrigieren — leeres Eingabebild");
            return bild;
        }

        try
        {
            using Mat kameraMat = new(3, 3, MatType.CV_64FC1);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    kameraMat.Set<double>(i, j, _kameraMatrix![i, j]);

            using Mat distMat = new(1, _distortionKoeffizienten!.Length, MatType.CV_64FC1);
            for (int i = 0; i < _distortionKoeffizienten.Length; i++)
                distMat.Set<double>(0, i, _distortionKoeffizienten[i]);

            Mat ergebnis = new();
            Cv2.Undistort(bild, ergebnis, kameraMat, distMat);

            Log.Information("ChArUco-Korrektur angewendet: {Breite}×{Hoehe}",
                ergebnis.Width, ergebnis.Height);
            return ergebnis;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChArUco Undistort fehlgeschlagen — gebe Original zurück");
            return bild;
        }
    }

    /// <summary>
    /// Speichert die Kalibrierung als JSON-Datei.
    /// Format ist kompatibel mit DistortionGridCorrector — Kalibrierungen können
    /// zwischen beiden Methoden ausgetauscht werden.
    /// </summary>
    /// <param name="pfad">Zieldatei-Pfad (.json).</param>
    /// <returns>true bei Erfolg.</returns>
    public bool Speichern(string pfad)
    {
        if (!IstKalibriert)
        {
            Log.Warning("ChArUco Speichern: keine Kalibrierung vorhanden");
            return false;
        }

        var jsonEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json" };
        var validierterPfad = SecurityValidator.ValidiereAusgabePfad(pfad, jsonEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("ChArUco Speichern: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        try
        {
            var daten = new CharucoKalibrierungsDaten
            {
                BildBreite = _bildGroesse.Width,
                BildHoehe = _bildGroesse.Height,
                KameraMatrix = _kameraMatrix!,
                DistortionKoeffizienten = _distortionKoeffizienten!,
                Methode = "ChArUco",
                SquaresX = _squaresX,
                SquaresY = _squaresY,
                DictionaryType = _dictType.ToString()
            };

            string json = JsonSerializer.Serialize(daten, KalibrierungsJsonOptionen);
            File.WriteAllText(validierterPfad, json);
            Log.Information("ChArUco-Kalibrierung gespeichert: {Pfad}", validierterPfad);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("ChArUco Speichern fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Lädt eine Kalibrierung aus einer JSON-Datei.
    /// Akzeptiert sowohl ChArUco- als auch DistortionGridCorrector-Dateien (format-kompatibel).
    /// </summary>
    /// <param name="pfad">Quelldatei-Pfad (.json).</param>
    /// <returns>true bei Erfolg.</returns>
    public bool Laden(string pfad)
    {
        var jsonEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".json" };
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad, jsonEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("ChArUco Laden: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        try
        {
            string json = File.ReadAllText(validierterPfad);
            var daten = JsonSerializer.Deserialize<CharucoKalibrierungsDaten>(json, KalibrierungsJsonOptionen);

            if (daten is null || daten.KameraMatrix is null || daten.DistortionKoeffizienten is null)
            {
                Log.Error("ChArUco: Kalibrierungsdatei ungültig");
                return false;
            }

            if (daten.KameraMatrix.GetLength(0) != 3 || daten.KameraMatrix.GetLength(1) != 3)
            {
                Log.Error("ChArUco: Kamera-Matrix muss 3×3 sein");
                return false;
            }

            if (daten.BildBreite <= 0 || daten.BildBreite > 100000 ||
                daten.BildHoehe <= 0 || daten.BildHoehe > 100000)
            {
                Log.Error("ChArUco: Bild-Dimensionen außerhalb gültiger Bereich");
                return false;
            }

            if (daten.DistortionKoeffizienten.Length < 1 || daten.DistortionKoeffizienten.Length > 20)
            {
                Log.Error("ChArUco: Distortion-Koeffizienten-Anzahl ungültig");
                return false;
            }

            _bildGroesse = new Size(daten.BildBreite, daten.BildHoehe);
            _kameraMatrix = daten.KameraMatrix;
            _distortionKoeffizienten = daten.DistortionKoeffizienten;

            Log.Information("ChArUco-Kalibrierung geladen ({Breite}×{Hoehe}, Methode={Methode})",
                _bildGroesse.Width, _bildGroesse.Height, daten.Methode ?? "unbekannt");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("ChArUco Laden fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    private static readonly JsonSerializerOptions KalibrierungsJsonOptionen = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class CharucoKalibrierungsDaten
    {
        public int BildBreite { get; set; }
        public int BildHoehe { get; set; }
        public double[,] KameraMatrix { get; set; } = new double[3, 3];
        public double[] DistortionKoeffizienten { get; set; } = [];
        public string? Methode { get; set; }
        public int SquaresX { get; set; }
        public int SquaresY { get; set; }
        public string? DictionaryType { get; set; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _kameraMatrix = null;
        _distortionKoeffizienten = null;
    }
}