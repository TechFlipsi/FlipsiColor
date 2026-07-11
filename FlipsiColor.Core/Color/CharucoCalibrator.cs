using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCvSharp;
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
///   - Funktioniert mit beliebigen Board-Größen (nicht nur Standard 9×6)
///
/// Implementiert nach Issue #5 von MarcoRavich:
///   - Nutzt OpenCV ArUco-Detection (Cv2.Aruco.DetectMarkers)
///   - Generiert ChArUco-Board-Parameter dynamisch (SquaresX × SquaresY)
///   - Kalibriert via Cv2.Aruco.CalibrateCameraCharuco
///   - Speichert/Lädt Ergebnisse als JSON (kompatibel mit DistortionGridCorrector-Format)
///
/// Referenzen:
///   - @Rosatus' Camera Calibration Workbench (github.com/Rosatus/CameraCalibrationWorkbench)
///   - @yumashino's Camera Calibration with ChArUco Board (github.com/yumashino/Camera-Calibration-with-ChArUco-Board)
///   - @nullboundary's CharucoCalibration (github.com/nullboundary/CharucoCalibration)
/// </summary>
public sealed class CharucoCalibrator : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<CharucoCalibrator>();

    // ChArUco-Board-Parameter (Standard: 7×5 Squares, 40px Square-Größe, 30px Marker-Größe)
    private int _squaresX = 7;
    private int _squaresY = 5;
    private float _squareSize = 40f;   // in Pixeln (für Board-Generierung)
    private float _markerSize = 30f;  // in Pixeln
    private int _markerDictId = 0;    // DICT_ARUCO_ORIGINAL = 0 (Standard)

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
    /// <param name="squareSize">Quadrat-Größe in mm (für Kalibrierung) oder Pixeln (für Generierung).</param>
    /// <param name="markerSize">Marker-Größe in mm oder Pixeln (muss kleiner als squareSize sein).</param>
    /// <param name="markerDictId">ArUco-Dictionary-ID (Standard: 0 = DICT_ARUCO_ORIGINAL).</param>
    public void SetzeBoardParameter(int squaresX, int squaresY, float squareSize, float markerSize, int markerDictId = 0)
    {
        if (squaresX < 4 || squaresY < 4)
            throw new ArgumentException("ChArUco-Board muss mindestens 4×4 Quadrate haben");
        if (markerSize >= squareSize)
            throw new ArgumentException("Marker-Größe muss kleiner als Square-Größe sein");

        _squaresX = squaresX;
        _squaresY = squaresY;
        _squareSize = squareSize;
        _markerSize = markerSize;
        _markerDictId = markerDictId;
        Log.Debug("ChArUco-Board-Parameter gesetzt: {X}×{Y} Squares, Square={Sq}px, Marker={Mk}px, Dict={Dict}",
            squaresX, squaresY, squareSize, markerSize, markerDictId);
    }

    /// <summary>
    /// Generiert ein ChArUco-Board-Bild und speichert es als PNG.
    /// Kann ausgedruckt und als Kalibrierungs-Referenz verwendet werden.
    /// </summary>
    /// <param name="ausgabePfad">Zieldatei-Pfad (.png oder .jpg).</param>
    /// <param name="pixelProMm">Pixel pro Millimeter für die Ausgabe (Standard: 10 = 300 DPI bei 75µm).</param>
    /// <returns>true bei Erfolg.</returns>
    public bool BoardGenerieren(string ausgabePfad, int pixelProMm = 10)
    {
        // Pfad-Validierung
        var erlaubeEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp" };
        var validierterPfad = SecurityValidator.ValidiereAusgabePfad(ausgabePfad, erlaubeEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("ChArUco BoardGenerieren: Pfad-Validierung fehlgeschlagen");
            return false;
        }

        try
        {
            // Board-Pixel-Dimensionen berechnen
            int pixelWidth = (int)(_squaresX * _squareSize * pixelProMm / 10f);
            int pixelHeight = (int)(_squaresY * _squareSize * pixelProMm / 10f);

            // ArUco-Dictionary erstellen
            var dict = Cv2.Aruco.GetPredefinedDictionary(_markerDictId);

            // ChArUco-Board erstellen
            var board = new ArucoBoard(_squaresX, _squaresY, _squareSize, _markerSize, dict);

            // Board-Bild generieren
            using Mat boardImage = new();
            board.Draw(pixelWidth, pixelHeight, boardImage);
            Cv2.ImWrite(validierterPfad, boardImage);

            Log.Information("ChArUco-Board generiert: {X}×{Y} Squares → {Pfad} ({W}×{H}px)",
                _squaresX, _squaresY, validierterPfad, pixelWidth, pixelHeight);
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
    /// Erkennt ArUco-Marker → interpoliert Schachbrett-Corners → CalibrateCameraCharuco.
    /// </summary>
    /// <param name="referenzBildPfade">Liste von Pfaden zu ChArUco-Board-Fotos (mindestens 1, empfohlen 5-15).</param>
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

        var dict = Cv2.Aruco.GetPredefinedDictionary(_markerDictId);
        var board = new ArucoBoard(_squaresX, _squaresY, _squareSize, _markerSize, dict);

        var alleCorners = new List<Mat>();
        var alleIds = new List<int[]>();
        var alleObjektPunkte = new List<Mat>();
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

            // ArUco-Marker detektieren
            Cv2.Aruco.DetectMarkers(grau, dict, out Point2f[][] corners, out int[] ids);

            if (ids.Length == 0)
            {
                Log.Warning("ChArUco: Keine ArUco-Marker gefunden in Bild {Index} — übersprungen", erfolgreich);
                continue;
            }

            // InterpolateCornersCharuco — liefert die Schachbrett-Corners des ChArUco-Boards
            Cv2.Aruco.InterpolateCornersCharuco(corners, ids, grau, board, out Point2f[] charucoCorners, out int[] charucoIds);

            if (charucoCorners.Length < 4)
            {
                Log.Warning("ChArUco: Zu wenige Corners interpoliert ({Anzahl}) — übersprungen", charucoCorners.Length);
                continue;
            }

            // Subpixel-Verfeinerung
            Cv2.CornerSubPix(grau, charucoCorners,
                new Size(11, 11), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.01));

            // Punkte für CalibrateCameraCharuco sammeln
            using Mat cornerMat = Mat.FromArray(charucoCorners);
            alleCorners.Add(cornerMat.Clone());
            alleIds.Add(charukoIds);
            erfolgreich++;

            Log.Debug("ChArUco: Bild {Index} — {Anzahl} Marker, {Corners} Corners",
                erfolgreich, ids.Length, charucoCorners.Length);
        }

        if (erfolgreich < 1)
        {
            Log.Error("ChArUco-Kalibrierung: keine gültigen Referenzbilder verarbeitet");
            return false;
        }

        // Kamera-Matrix initialisieren
        using Mat kameraMat = Mat.Eye(3, 3, MatType.CV_64FC1);
        kameraMat.Set<double>(0, 2, imageSize.Width / 2.0);
        kameraMat.Set<double>(1, 2, imageSize.Height / 2.0);
        kameraMat.Set<double>(0, 0, imageSize.Width);
        kameraMat.Set<double>(1, 1, imageSize.Width);

        using Mat distMat = new Mat(1, 5, MatType.CV_64FC1, Scalar.All(0));

        try
        {
            // CalibrateCameraCharuco — die spezialisierte ChArUco-Kalibrierung
            double rms = Cv2.Aruco.CalibrateCameraCharuco(
                alleCorners.ToArray(),
                alleIds.ToArray(),
                board,
                imageSize,
                kameraMat,
                distMat);

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

            // Aufräumen
            foreach (var m in alleCorners) m.Dispose();

            Log.Debug("ChArUco: Kamera-Matrix und {Anzahl} Distortion-Koeffizienten gespeichert", distAnzahl);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChArUco CalibrateCameraCharuco fehlgeschlagen");
            _kameraMatrix = null;
            _distortionKoeffizienten = null;
            foreach (var m in alleCorners) m.Dispose();
            return false;
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
            var daten = new CharukoKalibrierungsDaten
            {
                BildBreite = _bildGroesse.Width,
                BildHoehe = _bildGroesse.Height,
                KameraMatrix = _kameraMatrix!,
                DistortionKoeffizienten = _distortionKoeffizienten!,
                Methode = "ChArUco",
                SquaresX = _squaresX,
                SquaresY = _squaresY,
                MarkerDictId = _markerDictId
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
            var daten = JsonSerializer.Deserialize<CharukoKalibrierungsDaten>(json, KalibrierungsJsonOptionen);

            if (daten is null || daten.KameraMatrix is null || daten.DistortionKoeffizienten is null)
            {
                Log.Error("ChArUco: Kalibrierungsdatei ungültig");
                return false;
            }

            // Matrix-Dimensionen validieren
            if (daten.KameraMatrix.GetLength(0) != 3 || daten.KameraMatrix.GetLength(1) != 3)
            {
                Log.Error("ChArUco: Kamera-Matrix muss 3×3 sein");
                return false;
            }

            // Bild-Dimensionen validieren
            if (daten.BildBreite <= 0 || daten.BildBreite > 100000 ||
                daten.BildHoehe <= 0 || daten.BildHoehe > 100000)
            {
                Log.Error("ChArUco: Bild-Dimensionen außerhalb gültiger Bereich");
                return false;
            }

            // Distortion-Koeffizienten-Anzahl begrenzen
            if (daten.DistortionKoeffizienten.Length < 1 || daten.DistortionKoeffizienten.Length > 20)
            {
                Log.Error("ChArUco: Distortion-Koeffizienten-Anzahl ungültig");
                return false;
            }

            _bildGroesse = new Size(daten.BildBreite, daten.BildHoehe);
            _kameraMatrix = daten.KameraMatrix;
            _distortionKoeffizienten = daten.DistortionKoeffizienten;

            // ChArUco-spezifische Felder (falls vorhanden)
            if (daten.SquaresX > 0) _squaresX = daten.SquaresX;
            if (daten.SquaresY > 0) _squaresY = daten.SquaresY;
            if (daten.MarkerDictId >= 0) _markerDictId = daten.MarkerDictId;

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

    /// <summary>
    /// JSON-Serialisierungsoptionen.
    /// </summary>
    private static readonly JsonSerializerOptions KalibrierungsJsonOptionen = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// DTO für JSON-Serialisierung der ChArUco-Kalibrierungsdaten.
    /// Erweitert das DistortionGridCorrector-Format um ChArUco-spezifische Metadaten.
    /// </summary>
    private sealed class CharukoKalibrierungsDaten
    {
        public int BildBreite { get; set; }
        public int BildHoehe { get; set; }
        public double[,] KameraMatrix { get; set; } = new double[3, 3];
        public double[] DistortionKoeffizienten { get; set; } = [];

        /// <summary>Kalibrierungs-Methode ("ChArUco" oder "Schachbrett" für DistortionGridCorrector-Dateien).</summary>
        public string? Methode { get; set; }

        /// <summary>ChArUco-Board Squares in X-Richtung.</summary>
        public int SquaresX { get; set; }

        /// <summary>ChArUco-Board Squares in Y-Richtung.</summary>
        public int SquaresY { get; set; }

        /// <summary>ArUco-Dictionary-ID.</summary>
        public int MarkerDictId { get; set; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _kameraMatrix = null;
        _distortionKoeffizienten = null;
    }
}