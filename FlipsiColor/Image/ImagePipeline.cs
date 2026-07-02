using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;

using FlipsiColor.AI;
using FlipsiColor.Color;
using FlipsiColor.Core;
using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.Image;

/// <summary>
/// Bild-Pipeline — kompletter Verarbeitungsworkflow mit KI-Modell-Verkabelung.
/// Schritte: Weißabgleich → Belichtung → Kontrast → Lichter/Schatten →
/// Sättigung/Vibranz (oder AiLUT) → StyleLUT → Schärfe →
/// Rauschunterdrückung (oder KI-Denoising) → Objektivkorrektur (EXIF) →
/// Distortion-Grid → Farbkalibrierung → Gesichtswiederherstellung (CodeFormer) →
/// Hochskalieren (RealESRGAN) → Szenen-Klassifizierung (EfficientNet, Turbo/SmartLearn).
/// </summary>
public sealed class ImagePipeline : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ImagePipeline>();
    private Mat? _aktuellesBild;
    private Mat? _originalBild;
    private string? _bildPfad;
    private bool _disposed;
    private readonly ModelManager _modelManager;
    private readonly ColorManager _colorManager;
    private readonly WhiteBalance _whiteBalance;
    private readonly LensCorrector _lensCorrector;
    private readonly StyleLUT _styleLut;
    private readonly DistortionGridCorrector _distortionGridCorrector;
    private readonly ColorCalibration _colorCalibration;
    private readonly InferenceEngine _inferenceEngine;
    private readonly Pipeline _pipelineLogik;

    // EXIF-Daten — werden in BildLaden gelesen, für LensCorrector verwendet
    private string? _exifKamera;
    private string? _exifObjektiv;
    private float _exifBrennweite = 35f;
    private float _exifBlende = 5.6f;

    // Tile-Größe für Upscaling (vermeidet OOM bei großen Bildern)
    private const int UpscaleTileGroesse = 512;

    public event EventHandler? BildGeladen;
    public event EventHandler? PipelineAbgeschlossen;

    public ImagePipeline(ModelManager modelManager, ColorManager colorManager)
    {
        _modelManager = modelManager;
        _colorManager = colorManager;
        _whiteBalance = new WhiteBalance();
        _lensCorrector = new LensCorrector();
        _styleLut = new StyleLUT();
        _distortionGridCorrector = new DistortionGridCorrector();
        _colorCalibration = new ColorCalibration();
        _inferenceEngine = new InferenceEngine(modelManager);
        _pipelineLogik = new Pipeline();
        _lensCorrector.Initialisieren();
    }

    /// <summary>
    /// Lädt ein Bild (PNG, JPG, TIFF, BMP + RAW) und liest EXIF-Daten
    /// für die Objektivkorrektur.
    /// FIX #1: Pfad-Validierung gegen Path-Traversal und UNC-Pfade.
    /// FIX #9: Log-Ausgaben bereinigt — keine sensiblen Pfad-Anteile.
    /// </summary>
    public bool BildLaden(string pfad)
    {
        // FIX #1: Pfad-Validierung gegen Path-Traversal und UNC-Pfade
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad);
        if (validierterPfad == null)
        {
            Log.Warning("BildLaden: Pfad-Validierung fehlgeschlagen");
            return false;
        }
        pfad = validierterPfad;

        try
        {
            var ext = Path.GetExtension(pfad).ToLowerInvariant();

            if (ext is ".cr2" or ".cr3" or ".nef" or ".arw" or ".dng" or ".orf" or ".rw2" or ".raw")
            {
                _aktuellesBild = RawDecoder.Decode(pfad);
            }
            else
            {
                _aktuellesBild = Cv2.ImRead(pfad, ImreadModes.Color);
            }

            if (_aktuellesBild == null || _aktuellesBild.Empty())
            {
                Log.Error("Bild konnte nicht geladen werden");
                return false;
            }

            _originalBild = _aktuellesBild.Clone();
            _bildPfad = pfad;

            // EXIF-Daten lesen — für LensCorrector
            ExifLesenUndSpeichern(pfad);

            Log.Information("Bild geladen: {Breite}x{Hoehe}, {Kanaele} Kanäle",
                _aktuellesBild.Width, _aktuellesBild.Height, _aktuellesBild.Channels());
            BildGeladen?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Fehler beim Laden: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Liest EXIF-Daten (Kamera, Objektiv, Brennweite, Blende) nach dem Laden
    /// und speichert sie für die Objektivkorrektur (LensCorrector).
    /// FIX #9: Keine sensiblen Pfad-Anteile in Log-Ausgaben.
    /// </summary>
    private void ExifLesenUndSpeichern(string pfad)
    {
        // Standard-Fallback-Werte
        _exifKamera = null;
        _exifObjektiv = null;
        _exifBrennweite = 35f;
        _exifBlende = 5.6f;

        if (!File.Exists(pfad))
            return;

        try
        {
            var exif = ExifReader.LesenKompakt(pfad);

            if (!string.IsNullOrWhiteSpace(exif.Kamera))
                _exifKamera = exif.Kamera;

            if (!string.IsNullOrWhiteSpace(exif.Objektiv))
                _exifObjektiv = exif.Objektiv;

            _exifBrennweite = TryParseBrennweite(exif.Brennweite, _exifBrennweite);
            _exifBlende = TryParseBlende(exif.Blende, _exifBlende);

            if (_exifKamera != null || _exifObjektiv != null)
            {
                Log.Information("EXIF gelesen — Kamera={Kamera}, Objektiv={Objektiv}, " +
                                "Brennweite={Brennweite}mm, Blende=f/{Blende}",
                    _exifKamera ?? "?", _exifObjektiv ?? "?", _exifBrennweite, _exifBlende);
            }
            else
            {
                Log.Debug("EXIF: keine Kamera/Objektiv-Daten gefunden — verwende Fallback");
            }
        }
        catch (Exception ex)
        {
            Log.Warning("EXIF-Lesung fehlgeschlagen — verwende Fallback-Werte: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }
    }

    /// <summary>
    /// Führt die komplette Pipeline mit den gegebenen Parametern aus.
    /// Integriert KI-Modelle basierend auf Parametern und Betriebsmodus.
    /// FIX #7: Pipeline-Parameter werden vor der Verarbeitung auf sichere Bereiche begrenzt.
    /// </summary>
    public void PipelineAusfuehren(PipelineParams param)
    {
        // FIX #7: Parameter auf sichere Bereiche begrenzen — verhindert Overflow/Underflow
        param.Belichtung = SecurityValidator.BegrenzeParameter(param.Belichtung, -2f, 2f);
        param.Kontrast = SecurityValidator.BegrenzeParameter(param.Kontrast, -1f, 1f);
        param.Saettigung = SecurityValidator.BegrenzeParameter(param.Saettigung, -1f, 1f);
        param.Vibranz = SecurityValidator.BegrenzeParameter(param.Vibranz, -1f, 1f);
        param.Lichter = SecurityValidator.BegrenzeParameter(param.Lichter, -1f, 1f);
        param.Schatten = SecurityValidator.BegrenzeParameter(param.Schatten, -1f, 1f);
        param.SchaerfeBetrag = SecurityValidator.BegrenzeParameter(param.SchaerfeBetrag, 0f, 2f);
        param.LuminanzRauschen = SecurityValidator.BegrenzeParameter(param.LuminanzRauschen, 0f, 1f);
        param.ChrominanzRauschen = SecurityValidator.BegrenzeParameter(param.ChrominanzRauschen, 0f, 1f);
        param.WeissabgleichTemp = SecurityValidator.BegrenzeParameter(param.WeissabgleichTemp, 2000f, 12000f);
        param.WeissabgleichTint = SecurityValidator.BegrenzeParameter(param.WeissabgleichTint, -100f, 100f);
        param.HochskalierenFaktor = SecurityValidator.BegrenzeIntParameter(param.HochskalierenFaktor, 1, 4);

        if (_originalBild == null || _originalBild.Empty())
        {
            Log.Warning("Kein Bild geladen — Pipeline übersprungen");
            return;
        }

        Log.Information("Pipeline startet: Modus={Modus} Intensität={Int} Belichtung={Bel} " +
                        "Kontrast={Kontr} Sättigung={Saet} Hochskalieren={Up} Gesichter={Face}",
            param.Modus, param.Intensitaet, param.Belichtung, param.Kontrast,
            param.Saettigung, param.HochskalierenFaktor, param.GesichtswiederherstellungAktiv);

        // Pipeline-Logik-Intensität/Modus synchronisieren
        _pipelineLogik.Intensitaet = param.Intensitaet;
        _pipelineLogik.Modus = param.Modus;

        var bild = _originalBild.Clone();

        try
        {
            // ── Turbo / SmartLearn: Szene klassifizieren, Parameter anpassen ──
            if (param.Modus is BetriebsModus.Turbo or BetriebsModus.SmartLearn)
            {
                bild = SzenenKlassifizierenUndParameterAnpassen(bild, param);
            }

            // EXIF-Daten in Parameter übernehmen (für LensCorrector)
            param.ExifKamera = _exifKamera;
            param.ExifObjektiv = _exifObjektiv;
            param.ExifBrennweite = _exifBrennweite > 0 ? _exifBrennweite : null;
            param.ExifBlende = _exifBlende > 0 ? _exifBlende : null;

            // 1. Weißabgleich
            if (Math.Abs(param.WeissabgleichTemp - 5500.0f) > 1 || Math.Abs(param.WeissabgleichTint) > 1)
            {
                var neuesBild = WeissabgleichAnwenden(bild, (float)param.WeissabgleichTemp, (float)param.WeissabgleichTint);
                bild.Dispose();
                bild = neuesBild;
            }
            else
            {
                // AutoWB anwenden wenn keine manuelle Temperatur gesetzt
                var wbResult = _whiteBalance.AutoWb(bild);
                if (Math.Abs(wbResult.Temperatur - 5500.0) > 50 || Math.Abs(wbResult.Tint) > 1)
                {
                    var neuesBild = WeissabgleichAnwenden(bild, (float)wbResult.Temperatur, (float)wbResult.Tint);
                    bild.Dispose();
                    bild = neuesBild;
                }
            }

            // 2. Belichtung
            if (Math.Abs(param.Belichtung) > 0.01f)
            {
                var neuesBild = BelichtungAnwenden(bild, param.Belichtung);
                bild.Dispose();
                bild = neuesBild;
            }

            // 3. Kontrast
            if (Math.Abs(param.Kontrast) > 0.01f)
            {
                var neuesBild = KontrastAnwenden(bild, param.Kontrast);
                bild.Dispose();
                bild = neuesBild;
            }

            // 4. Lichter & Schatten
            if (Math.Abs(param.Lichter) > 0.01f)
            {
                var neuesBild = LichterSchattenAnwenden(bild, param.Lichter, true);
                bild.Dispose();
                bild = neuesBild;
            }
            if (Math.Abs(param.Schatten) > 0.01f)
            {
                var neuesBild = LichterSchattenAnwenden(bild, param.Schatten, false);
                bild.Dispose();
                bild = neuesBild;
            }

            // 5. Sättigung & Vibranz — oder KI-LUT (AiLUTTransform)
            if (!string.IsNullOrEmpty(param.AiStilName))
            {
                // KI-basierte Stil-Transformation statt manuelle Sättigung
                var kiResult = AiLutTransformAnwenden(bild, param.AiStilName, param.Intensitaet);
                if (kiResult != null)
                {
                    bild.Dispose();
                    bild = kiResult;
                    Log.Information("AiLUTTransform angewendet: Stil={Stil}", param.AiStilName);
                }
                else if (Math.Abs(param.Saettigung) > 0.01f || Math.Abs(param.Vibranz) > 0.01f)
                {
                    // Fallback auf manuelle Sättigung wenn Modell nicht verfügbar
                    var neuesBild = SaettigungAnwenden(bild, param.Saettigung, param.Vibranz);
                    bild.Dispose();
                    bild = neuesBild;
                }
            }
            else if (Math.Abs(param.Saettigung) > 0.01f || Math.Abs(param.Vibranz) > 0.01f)
            {
                var neuesBild = SaettigungAnwenden(bild, param.Saettigung, param.Vibranz);
                bild.Dispose();
                bild = neuesBild;
            }

            // 5b. StyleLUT — nach Sättigung anwenden, falls geladen
            if (!string.IsNullOrEmpty(param.StyleLutPfad))
            {
                var lutResult = StyleLutAnwenden(bild, param.StyleLutPfad);
                if (lutResult != null)
                {
                    bild.Dispose();
                    bild = lutResult;
                    Log.Information("StyleLUT angewendet: {Pfad}", param.StyleLutPfad);
                }
            }

            // 6. Schärfe
            if (Math.Abs(param.SchaerfeBetrag) > 0.01f)
            {
                var neuesBild = SchaerfeAnwenden(bild, param.SchaerfeBetrag);
                bild.Dispose();
                bild = neuesBild;
            }

            // 7. Rauschunterdrückung — KI-Denoising wenn LuminanzRauschen > 0.3,
            //    sonst klassische GaussianBlur-basierte Methode
            if (param.LuminanzRauschen > 0.3f || param.ChrominanzRauschen > 0.3f)
            {
                var kiResult = KiDenoisingAnwenden(bild, param.LuminanzRauschen, param.Intensitaet);
                if (kiResult != null)
                {
                    bild.Dispose();
                    bild = kiResult;
                    Log.Information("KI-Denoising angewendet (NAFNet/RestormerLight)");
                }
                else
                {
                    // Fallback auf klassische Rauschunterdrückung
                    var neuesBild = RauschunterdrueckungAnwenden(bild, param.LuminanzRauschen, param.ChrominanzRauschen);
                    bild.Dispose();
                    bild = neuesBild;
                }
            }
            else if (param.LuminanzRauschen > 0.01f || param.ChrominanzRauschen > 0.01f)
            {
                var neuesBild = RauschunterdrueckungAnwenden(bild, param.LuminanzRauschen, param.ChrominanzRauschen);
                bild.Dispose();
                bild = neuesBild;
            }

            // 8. Objektivkorrektur — EXIF-Daten verwenden
            if (param.ObjektivkorrekturAktiv)
            {
                var (kamera, objektiv, brennweite, blende) = ExifFuerObjektivkorrekturLesen(param);
                var neuesBild = _lensCorrector.Korrigieren(bild, kamera, objektiv, brennweite, blende);
                bild.Dispose();
                bild = neuesBild;
            }

            // 9. Distortion-Grid-Korrektur (nach Objektivkorrektur)
            if (param.DistortionGridAktiv && _distortionGridCorrector.IstKalibriert)
            {
                var neuesBild = _distortionGridCorrector.Korrigieren(bild);
                if (neuesBild != bild)
                {
                    bild.Dispose();
                    bild = neuesBild;
                }
            }

            // 10. Farbkalibrierung (nach Distortion-Grid)
            if (param.ColorCalibrationAktiv && _colorCalibration.IstKalibriert)
            {
                var neuesBild = _colorCalibration.Anwenden(bild);
                if (neuesBild != bild)
                {
                    bild.Dispose();
                    bild = neuesBild;
                }
            }

            // 11. Gesichtswiederherstellung (CodeFormer) — wenn aktiviert
            if (param.GesichtswiederherstellungAktiv)
            {
                var fidelity = _pipelineLogik.CodeFormerFidelityWeight();
                var faceResult = CodeFormerAnwenden(bild, fidelity);
                if (faceResult != null)
                {
                    bild.Dispose();
                    bild = faceResult;
                    Log.Information("CodeFormer angewendet (Fidelity={Fidelity})", fidelity);
                }
            }

            // 12. Hochskalieren (RealESRGAN) — wenn Faktor > 1
            if (param.HochskalierenFaktor > 1)
            {
                var upscaled = RealEsrganUpscaling(bild, param.HochskalierenFaktor);
                if (upscaled != null)
                {
                    bild.Dispose();
                    bild = upscaled;
                    Log.Information("RealESRGAN Upscaling: {Faktor}x → {Breite}x{Hoehe}",
                        param.HochskalierenFaktor, bild.Width, bild.Height);
                }
            }

            _aktuellesBild?.Dispose();
            _aktuellesBild = bild;

            Log.Information("Pipeline abgeschlossen");
            PipelineAbgeschlossen?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error("Pipeline-Fehler: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            bild.Dispose();
        }
    }

    // ── KI-Modell-Verkabelung ──

    /// <summary>
    /// Szenen-Klassifizierung mit EfficientNet + automatische Parameter-Anpassung.
    /// Wird im Turbo- und SmartLearn-Modus aufgerufen.
    /// </summary>
    private Mat SzenenKlassifizierenUndParameterAnpassen(Mat bild, PipelineParams param)
    {
        try
        {
            // Bild → float[] BGR HWC für InferenceEngine
            var bgr = bild;
            // Resize auf 256x256 für schnellere Inferenz (EfficientNet skaliert intern auf 224)
            using var resized = new Mat();
            Cv2.Resize(bgr, resized, new OpenCvSharp.Size(256, 256), 0, 0, InterpolationFlags.Linear);

            resized.ConvertTo(resized, MatType.CV_32FC3);
            var imgData = new float[256 * 256 * 3];
            resized.GetArray(out imgData);

            var szene = _inferenceEngine.SzeneKlassifizierenAsync(imgData, 256, 256).GetAwaiter().GetResult();
            param.ErkannteSzene = szene;

            Log.Information("Szenen-Klassifizierung: {Szene}", szene);

            if (param.Modus == BetriebsModus.Turbo)
            {
                // Turbo: Parameter vollständig aus Szenen-Typ ableiten
                var autoParams = _pipelineLogik.StandardParamsFuerSzene(szene);

                // Intensität/Modus beibehalten, KI-Parameter übernehmen
                param.Saettigung = autoParams.Saettigung;
                param.Kontrast = autoParams.Kontrast;
                param.Belichtung = autoParams.Belichtung;
                param.SchaerfeBetrag = autoParams.SchaerfeBetrag;
                param.LuminanzRauschen = autoParams.LuminanzRauschen;
                param.ChrominanzRauschen = autoParams.ChrominanzRauschen;
                param.Vibranz = autoParams.Vibranz;
                param.GesichtswiederherstellungAktiv = autoParams.GesichtswiederherstellungAktiv;
                param.WeissabgleichTemp = autoParams.WeissabgleichTemp;

                Log.Information("Turbo: Auto-Parameter aus Szene '{Szene}' gesetzt", szene);
            }
            else if (param.Modus == BetriebsModus.SmartLearn)
            {
                // SmartLearn: Parameter als Vorschlag übernehmen, falls User-Werte bei 0
                if (Math.Abs(param.Saettigung) < 0.01f) param.Saettigung = _pipelineLogik.StandardParamsFuerSzene(szene).Saettigung;
                if (Math.Abs(param.Kontrast) < 0.01f) param.Kontrast = _pipelineLogik.StandardParamsFuerSzene(szene).Kontrast;
                if (Math.Abs(param.Belichtung) < 0.01f) param.Belichtung = _pipelineLogik.StandardParamsFuerSzene(szene).Belichtung;

                Log.Information("SmartLearn: Vorschläge aus Szene '{Szene}' angewendet (nur für leere Parameter)", szene);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Szenen-Klassifizierung fehlgeschlagen — fahre mit manuellen Parametern fort");
        }

        return bild;
    }

    /// <summary>
    /// KI-Denoising mit NAFNet (bevorzugt) oder RestormerLight (Fallback).
    /// Vorverarbeitung: Mat → float[] NCHW, Inferenz, Postprocessing: float[] → Mat.
    /// </summary>
    private Mat? KiDenoisingAnwenden(Mat bild, float rauschLevel, Intensitaet intensitaet)
    {
        try
        {
            // NAFNet bevorzugen, RestormerLight als Alternative
            var modellId = ModellId.NAFNet;
            if (!_modelManager.ModellSicherstellenAsync(modellId).GetAwaiter().GetResult())
            {
                modellId = ModellId.RestormerLight;
                if (!_modelManager.ModellSicherstellenAsync(modellId).GetAwaiter().GetResult())
                {
                    Log.Warning("KI-Denoising: Kein Modell verfügbar — Fallback auf klassisch");
                    return null;
                }
            }

            var session = _modelManager.Session(modellId);
            if (session == null) return null;

            // Noise-Level basierend auf Intensität
            float noiseLevel = intensitaet switch
            {
                Intensitaet.Leicht => Math.Max(5, rauschLevel * 25),
                Intensitaet.Stark => Math.Min(75, rauschLevel * 75),
                _ => Math.Min(50, rauschLevel * 50)
            };

            // Bild vorbereiten: resize auf 256x256 für Modell, dann Inferenz
            using var input = new Mat();
            Cv2.Resize(bild, input, new OpenCvSharp.Size(256, 256), 0, 0, InterpolationFlags.Linear);

            input.ConvertTo(input, MatType.CV_32FC3, 1.0 / 255.0);
            var channels = Cv2.Split(input);
            try
            {
                int h = 256, w = 256;
                var chwData = new float[3 * h * w];
                for (int c = 0; c < 3; c++)
                {
                    var chanArr = new float[h * w];
                    channels[c].GetArray(out chanArr);
                    for (int i = 0; i < h * w; i++)
                        chwData[c * h * w + i] = chanArr[i];
                }

                // Inferenz — Modell erwartet [1, 3, H, W]
                var output = _inferenceEngine.Inferenz(modellId, chwData, [1, 3, h, w]);
                if (output.Length == 0) return null;

                // Postprocessing: NCHW float → HWC → BGR Mat (8-bit)
                var resultMat = new Mat(h, w, MatType.CV_32FC3);
                var hwcData = new float[h * w * 3];
                for (int c = 0; c < 3; c++)
                    for (int i = 0; i < h * w; i++)
                        hwcData[i * 3 + c] = output[c * h * w + i];

                resultMat.SetArray(hwcData);
                resultMat.ConvertTo(resultMat, MatType.CV_8UC3, 255.0);

                // Auf Originalgröße zurückskalieren
                var fullResult = new Mat();
                Cv2.Resize(resultMat, fullResult, new OpenCvSharp.Size(bild.Width, bild.Height), 0, 0, InterpolationFlags.Lanczos4);
                resultMat.Dispose();
                return fullResult;
            }
            finally
            {
                foreach (var c in channels) c.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "KI-Denoising fehlgeschlagen — Fallback auf klassisch");
            return null;
        }
    }

    /// <summary>
    /// RealESRGAN Upscaling mit tile-basiertem Ansatz (vermeidet OOM bei großen Bildern).
    /// Jeder Tile wird separat hochskaliert und dann zusammengefügt.
    /// </summary>
    private Mat? RealEsrganUpscaling(Mat bild, int faktor)
    {
        try
        {
            if (!_modelManager.ModellSicherstellenAsync(ModellId.RealESRGAN).GetAwaiter().GetResult())
            {
                Log.Warning("RealESRGAN: Modell nicht verfügbar — Fallback auf klassisches Upscaling");
                return KlassischesUpscaling(bild, faktor);
            }

            var session = _modelManager.Session(ModellId.RealESRGAN);
            if (session == null) return KlassischesUpscaling(bild, faktor);

            int breite = bild.Width;
            int hoehe = bild.Height;
            int tileW = Math.Min(UpscaleTileGroesse, breite);
            int tileH = Math.Min(UpscaleTileGroesse, hoehe);
            int outW = breite * faktor;
            int outH = hoehe * faktor;

            var result = new Mat(outH, outW, bild.Type());

            // Tile-basierte Verarbeitung
            for (int y = 0; y < hoehe; y += tileH)
            {
                for (int x = 0; x < breite; x += tileW)
                {
                    int tw = Math.Min(tileW, breite - x);
                    int th = Math.Min(tileH, hoehe - y);
                    var roi = new Rect(x, y, tw, th);
                    using var tile = new Mat(bild, roi);

                    // Tile → float[] NCHW
                    tile.ConvertTo(tile, MatType.CV_32FC3, 1.0 / 255.0);
                    var tileChannels = Cv2.Split(tile);
                    try
                    {
                        var chwData = new float[3 * th * tw];
                        for (int c = 0; c < 3; c++)
                        {
                            var chanArr = new float[th * tw];
                            tileChannels[c].GetArray(out chanArr);
                            for (int i = 0; i < th * tw; i++)
                                chwData[c * th * tw + i] = chanArr[i];
                        }

                        var output = _inferenceEngine.Inferenz(ModellId.RealESRGAN, chwData, [1, 3, th, tw]);
                        if (output.Length == 0) continue;

                        // Postprocessing → Mat
                        int upTileW = tw * faktor;
                        int upTileH = th * faktor;
                        var upTile = new Mat(upTileH, upTileW, MatType.CV_32FC3);
                        var hwcData = new float[upTileH * upTileW * 3];

                        if (output.Length == 3 * upTileH * upTileW)
                        {
                            for (int c = 0; c < 3; c++)
                                for (int i = 0; i < upTileH * upTileW; i++)
                                    hwcData[i * 3 + c] = output[c * upTileH * upTileW + i];
                        }
                        else
                        {
                            // Modell-Ausgabe stimmt nicht mit erwarteter Größe überein — Fallback
                            Log.Warning("RealESRGAN: unerwartete Output-Größe {Len} für Tile {W}x{H}x{F}",
                                output.Length, tw, th, faktor);
                            upTile.Dispose();
                            continue;
                        }

                        upTile.SetArray(hwcData);
                        upTile.ConvertTo(upTile, MatType.CV_8UC3, 255.0);

                        // Tile ins Ergebnis kopieren
                        var destRoi = new Rect(x * faktor, y * faktor, upTileW, upTileH);
                        using var destMat = new Mat(result, destRoi);
                        upTile.CopyTo(destMat);
                        upTile.Dispose();
                    }
                    finally
                    {
                        foreach (var c in tileChannels) c.Dispose();
                    }
                }
            }

            Log.Debug("RealESRGAN: {Breite}x{Hoehe} → {OutW}x{OutH} (Tile {TileW}x{TileH})",
                breite, hoehe, outW, outH, tileW, tileH);
            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RealESRGAN Upscaling fehlgeschlagen — Fallback auf klassisch");
            return KlassischesUpscaling(bild, faktor);
        }
    }

    /// <summary>
    /// Klassisches Upscaling mit Lanczos4-Interpolation als Fallback.
    /// </summary>
    private static Mat KlassischesUpscaling(Mat bild, int faktor)
    {
        var result = new Mat();
        Cv2.Resize(bild, result, new OpenCvSharp.Size(bild.Width * faktor, bild.Height * faktor),
            0, 0, InterpolationFlags.Lanczos4);
        return result;
    }

    /// <summary>
    /// CodeFormer Gesichtswiederherstellung.
    /// Nutzt Pipeline.CodeFormerFidelityWeight() basierend auf Intensität.
    /// </summary>
    private Mat? CodeFormerAnwenden(Mat bild, float fidelityWeight)
    {
        try
        {
            if (!_modelManager.ModellSicherstellenAsync(ModellId.CodeFormer).GetAwaiter().GetResult())
            {
                Log.Warning("CodeFormer: Modell nicht verfügbar — Gesichtswiederherstellung übersprungen");
                return null;
            }

            var session = _modelManager.Session(ModellId.CodeFormer);
            if (session == null) return null;

            // CodeFormer erwartet typischerweise 512x512 Input
            const int inputSize = 512;
            using var input = new Mat();
            Cv2.Resize(bild, input, new OpenCvSharp.Size(inputSize, inputSize), 0, 0, InterpolationFlags.Linear);

            input.ConvertTo(input, MatType.CV_32FC3, 1.0 / 255.0);
            var channels = Cv2.Split(input);
            try
            {
                var chwData = new float[3 * inputSize * inputSize];
                for (int c = 0; c < 3; c++)
                {
                    var chanArr = new float[inputSize * inputSize];
                    channels[c].GetArray(out chanArr);
                    for (int i = 0; i < inputSize * inputSize; i++)
                        chwData[c * inputSize * inputSize + i] = chanArr[i];
                }

                // CodeFormer hat typischerweise zwei Inputs: Bild + fidelity_weight
                // Wir verwenden die generische Inferenz mit nur dem Bild-Input
                var output = _inferenceEngine.Inferenz(ModellId.CodeFormer, chwData, [1, 3, inputSize, inputSize]);
                if (output.Length == 0) return null;

                // Postprocessing: NCHW → HWC → BGR
                var resultMat = new Mat(inputSize, inputSize, MatType.CV_32FC3);
                var hwcData = new float[inputSize * inputSize * 3];
                for (int c = 0; c < 3; c++)
                    for (int i = 0; i < inputSize * inputSize; i++)
                        hwcData[i * 3 + c] = output[c * inputSize * inputSize + i];

                resultMat.SetArray(hwcData);
                resultMat.ConvertTo(resultMat, MatType.CV_8UC3, 255.0);

                // Auf Originalgröße zurückskalieren
                var fullResult = new Mat();
                Cv2.Resize(resultMat, fullResult, new OpenCvSharp.Size(bild.Width, bild.Height),
                    0, 0, InterpolationFlags.Lanczos4);
                resultMat.Dispose();

                // Fidelity-gewichtete Mischung: hohes Weight = mehr CodeFormer, niedrig = mehr Original
                var gemischt = new Mat();
                Cv2.AddWeighted(bild, 1.0 - fidelityWeight, fullResult, fidelityWeight, 0, gemischt);
                fullResult.Dispose();
                return gemischt;
            }
            finally
            {
                foreach (var c in channels) c.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CodeFormer fehlgeschlagen — übersprungen");
            return null;
        }
    }

    /// <summary>
    /// AiLUTTransform — KI-basierte Farbstil-Transformation.
    /// Lädt das Modell und wendet es als stilbasierte LUT an.
    /// </summary>
    private Mat? AiLutTransformAnwenden(Mat bild, string stilName, Intensitaet intensitaet)
    {
        try
        {
            if (!_modelManager.ModellSicherstellenAsync(ModellId.AiLUTTransform).GetAwaiter().GetResult())
            {
                Log.Warning("AiLUTTransform: Modell nicht verfügbar — Fallback auf manuelle Sättigung");
                return null;
            }

            var session = _modelManager.Session(ModellId.AiLUTTransform);
            if (session == null) return null;

            // AiLUT erwartet 256x256 Input
            const int inputSize = 256;
            using var input = new Mat();
            Cv2.Resize(bild, input, new OpenCvSharp.Size(inputSize, inputSize), 0, 0, InterpolationFlags.Linear);

            input.ConvertTo(input, MatType.CV_32FC3, 1.0 / 255.0);
            var channels = Cv2.Split(input);
            try
            {
                var chwData = new float[3 * inputSize * inputSize];
                for (int c = 0; c < 3; c++)
                {
                    var chanArr = new float[inputSize * inputSize];
                    channels[c].GetArray(out chanArr);
                    for (int i = 0; i < inputSize * inputSize; i++)
                        chwData[c * inputSize * inputSize + i] = chanArr[i];
                }

                var output = _inferenceEngine.Inferenz(ModellId.AiLUTTransform, chwData, [1, 3, inputSize, inputSize]);
                if (output.Length == 0) return null;

                // Postprocessing
                var resultMat = new Mat(inputSize, inputSize, MatType.CV_32FC3);
                var hwcData = new float[inputSize * inputSize * 3];
                for (int c = 0; c < 3; c++)
                    for (int i = 0; i < inputSize * inputSize; i++)
                        hwcData[i * 3 + c] = output[c * inputSize * inputSize + i];

                resultMat.SetArray(hwcData);
                resultMat.ConvertTo(resultMat, MatType.CV_8UC3, 255.0);

                // Intensitäts-gewichtete Mischung
                float blend = intensitaet switch
                {
                    Intensitaet.Leicht => 0.5f,
                    Intensitaet.Stark => 1.0f,
                    _ => 0.75f
                };

                // Auf Originalgröße zurückskalieren
                var fullResult = new Mat();
                Cv2.Resize(resultMat, fullResult, new OpenCvSharp.Size(bild.Width, bild.Height),
                    0, 0, InterpolationFlags.Lanczos4);
                resultMat.Dispose();

                var gemischt = new Mat();
                Cv2.AddWeighted(bild, 1.0 - blend, fullResult, blend, 0, gemischt);
                fullResult.Dispose();
                return gemischt;
            }
            finally
            {
                foreach (var c in channels) c.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AiLUTTransform fehlgeschlagen — Fallback auf manuelle Sättigung");
            return null;
        }
    }

    /// <summary>
    /// Wendet eine geladene .cube StyleLUT auf das Bild an.
    /// </summary>
    private Mat? StyleLutAnwenden(Mat bild, string lutPfad)
    {
        try
        {
            // FIX #1: LUT-Pfad gegen Path-Traversal validieren
            var lutEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".cube", ".lut" };
            var validierterPfad = SecurityValidator.ValidiereDateiPfad(lutPfad, lutEndungen);
            if (validierterPfad == null)
            {
                Log.Warning("StyleLUT: Pfad-Validierung fehlgeschlagen");
                return null;
            }

            // LUT laden (wird gecacht — nur beim ersten Mal geladen)
            if (!_styleLut.Laden(validierterPfad))
            {
                Log.Warning("StyleLUT: Laden fehlgeschlagen");
                return null;
            }

            return _styleLut.Anwenden(bild);
        }
        catch (Exception ex)
        {
            Log.Warning("StyleLUT-Anwendung fehlgeschlagen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return null;
        }
    }

    /// <summary>Gibt das aktuelle Ergebnis-Bild zurück</summary>
    public Mat? Ergebnis => _aktuellesBild;

    // ── Pipeline-Schritte ──

    private static Mat WeissabgleichAnwenden(Mat bild, float temperatur, float tint)
    {
        // Farbtemperatur-basierte Anpassung der R/B Kanäle
        var bgr = bild.Split();
        float faktorR, faktorB;
        if (temperatur > 5500)
        {
            faktorR = 1.0f + (temperatur - 5500) / 15000f;
            faktorB = 1.0f - (temperatur - 5500) / 30000f;
        }
        else
        {
            faktorR = 1.0f - (5500 - temperatur) / 10000f;
            faktorB = 1.0f + (5500 - temperatur) / 15000f;
        }

        bgr[2] *= faktorR;
        bgr[0] *= faktorB;

        var result = new Mat();
        Cv2.Merge(bgr, result);
        foreach (var c in bgr) c.Dispose();
        return result;
    }

    private static Mat BelichtungAnwenden(Mat bild, float belichtung)
    {
        // FIX #7: Begrenzung verhindert Overflow — belichtung * 50 muss im float-Bereich bleiben
        // belichtung ist bereits auf [-2, 2] begrenzt → belichtung * 50 = [-100, 100] (sicher)
        var result = new Mat();
        bild.ConvertTo(result, -1, 1.0, SecurityValidator.BegrenzeParameter(belichtung, -2f, 2f) * 50);
        return result;
    }

    private static Mat KontrastAnwenden(Mat bild, float kontrast)
    {
        // FIX #7: Begrenzung verhindert extreme alpha-Werte
        // kontrast ist auf [-1, 1] begrenzt → alpha = [0.5, 1.5] (sicher)
        var result = new Mat();
        var alpha = 1.0 + SecurityValidator.BegrenzeParameter(kontrast, -1f, 1f) * 0.5;
        bild.ConvertTo(result, -1, alpha, 128 * (1 - alpha));
        return result;
    }

    /// <summary>
    /// Maskenbasierte selektive Lichter/Schatten-Korrektur.
    /// Für Lichter: Maske mit Cv2.Threshold (>200) — nur helle Pixel werden korrigiert.
    /// Für Schatten: Maske mit Cv2.Threshold (<55, invertiert) — nur dunkle Pixel werden korrigiert.
    /// Die Korrektur wird via BitwiseAnd + AddWeighted nur auf maskierte Pixel angewendet.
    /// </summary>
    private static Mat LichterSchattenAnwenden(Mat bild, float wert, bool lichter)
    {
        // Graustufen-Version für die Maskenberechnung
        var gray = new Mat();
        Cv2.CvtColor(bild, gray, ColorConversionCodes.BGR2GRAY);

        var mask = new Mat();
        if (lichter)
        {
            // Lichter: Pixel > 200 markieren (helle Bereiche)
            Cv2.Threshold(gray, mask, 200, 255, ThresholdTypes.Binary);
        }
        else
        {
            // Schatten: Pixel < 55 markieren (dunkle Bereiche)
            // Threshold mit THRESH_BINARY_INV: alles unter 55 → 255, alles darüber → 0
            Cv2.Threshold(gray, mask, 55, 255, ThresholdTypes.BinaryInv);
        }
        gray.Dispose();

        // Korrigierte Version des gesamten Bildes berechnen
        var korrigiert = new Mat();
        if (lichter)
        {
            // Lichter abschwächen/aufhellen je nach Vorzeichen von wert
            // positiver wert → aufhellen, negativer → abdunkeln
            bild.ConvertTo(korrigiert, -1, 1.0, wert * 20);
        }
        else
        {
            // Schatten aufhellen/abdunkeln
            bild.ConvertTo(korrigiert, -1, 1.0, wert * 20);
        }

        // Maske auf 3 Kanäle erweitern für BitwiseAnd mit BGR-Bild
        var mask3ch = new Mat();
        Cv2.CvtColor(mask, mask3ch, ColorConversionCodes.GRAY2BGR);

        // Nur maskierte Pixel aus dem korrigierten Bild übernehmen
        var maskiertKorrigiert = new Mat();
        Cv2.BitwiseAnd(korrigiert, mask3ch, maskiertKorrigiert);

        // Invertierte Maske für die nicht-maskierten Pixel
        var invMask = new Mat();
        Cv2.BitwiseNot(mask3ch, invMask);

        var maskiertOriginal = new Mat();
        Cv2.BitwiseAnd(bild, invMask, maskiertOriginal);

        // Beide Teile zusammenführen
        var result = new Mat();
        Cv2.Add(maskiertKorrigiert, maskiertOriginal, result);

        // Hilfs-Mats freigeben
        mask.Dispose();
        mask3ch.Dispose();
        korrigiert.Dispose();
        maskiertKorrigiert.Dispose();
        invMask.Dispose();
        maskiertOriginal.Dispose();

        return result;
    }

    private static Mat SaettigungAnwenden(Mat bild, float saettigung, float vibranz)
    {
        var hsv = new Mat();
        Cv2.CvtColor(bild, hsv, ColorConversionCodes.BGR2HSV);
        var channels = hsv.Split();

        // S-Kanal anpassen
        var sDelta = saettigung * 0.5f + vibranz * 0.25f;
        channels[1] = channels[1] + new Scalar(sDelta * 50, sDelta * 50, sDelta * 50, 0);
        Cv2.Merge(channels, hsv);
        foreach (var c in channels) c.Dispose();

        var result = new Mat();
        Cv2.CvtColor(hsv, result, ColorConversionCodes.HSV2BGR);
        hsv.Dispose();
        return result;
    }

    private static Mat SchaerfeAnwenden(Mat bild, float betrag)
    {
        var result = new Mat();
        var amount = betrag * 0.5;
        Cv2.GaussianBlur(bild, result, new OpenCvSharp.Size(0, 0), 3);
        Cv2.AddWeighted(bild, 1.0 + amount, result, -amount, 0, result);
        return result;
    }

    /// <summary>
    /// Rauschunterdrückung mit getrennter Luma- und Chroma-Behandlung.
    /// Pipeline: BGR → YUV → GaussianBlur auf Y (Luma) und separat auf U,V (Chroma) → merge → BGR.
    /// Luma-Rauschunterdrückung bewahrt Details, Chroma-Rauschunterdrückung reduziert Farbrauschen.
    /// </summary>
    private static Mat RauschunterdrueckungAnwenden(Mat bild, float luma, float chroma)
    {
        // BGR → YUV (Y=Luma, U/V=Chroma)
        var yuv = new Mat();
        Cv2.CvtColor(bild, yuv, ColorConversionCodes.BGR2YUV);

        var channels = Cv2.Split(yuv);

        try
        {
            // Luma-Rauschunterdrückung (Y-Kanal)
            if (luma > 0.01f)
            {
                var sigmaLuma = Math.Max(3, (int)(luma * 10));
                var yFiltered = new Mat();
                Cv2.GaussianBlur(channels[0], yFiltered, new OpenCvSharp.Size(0, 0), sigmaLuma);
                channels[0].Dispose();
                channels[0] = yFiltered;
            }

            // Chroma-Rauschunterdrückung (U- und V-Kanal separat)
            if (chroma > 0.01f)
            {
                var sigmaChroma = Math.Max(3, (int)(chroma * 15));
                // U-Kanal (Chroma Blau-Gelb)
                var uFiltered = new Mat();
                Cv2.GaussianBlur(channels[1], uFiltered, new OpenCvSharp.Size(0, 0), sigmaChroma);
                channels[1].Dispose();
                channels[1] = uFiltered;

                // V-Kanal (Chroma Rot-Cyan)
                var vFiltered = new Mat();
                Cv2.GaussianBlur(channels[2], vFiltered, new OpenCvSharp.Size(0, 0), sigmaChroma);
                channels[2].Dispose();
                channels[2] = vFiltered;
            }

            // Kanäle wieder zusammenführen
            var yuvFiltered = new Mat();
            Cv2.Merge(channels, yuvFiltered);

            // YUV → BGR
            var result = new Mat();
            Cv2.CvtColor(yuvFiltered, result, ColorConversionCodes.YUV2BGR);

            yuvFiltered.Dispose();
            return result;
        }
        finally
        {
            foreach (var c in channels)
                c.Dispose();
        }
    }

    /// <summary>
    /// Liest EXIF-Daten (Kamera, Objektiv, Brennweite, Blende) aus den in BildLaden
    /// gespeicherten Werten oder den PipelineParams. Fällt auf hardcoded Werte zurück.
    /// </summary>
    private (string Kamera, string Objektiv, float Brennweite, float Blende) ExifFuerObjektivkorrekturLesen(PipelineParams param)
    {
        // Hardcoded Fallback-Werte
        const string fallbackKamera = "Canon";
        const string fallbackObjektiv = "EF-S 18-55mm";

        // EXIF aus PipelineParams (von BildLaden gefüllt)
        string kamera = !string.IsNullOrWhiteSpace(param.ExifKamera) ? param.ExifKamera :
                        !string.IsNullOrWhiteSpace(_exifKamera) ? _exifKamera! : fallbackKamera;
        string objektiv = !string.IsNullOrWhiteSpace(param.ExifObjektiv) ? param.ExifObjektiv :
                         !string.IsNullOrWhiteSpace(_exifObjektiv) ? _exifObjektiv! : fallbackObjektiv;
        float brennweite = param.ExifBrennweite ?? _exifBrennweite;
        float blende = param.ExifBlende ?? _exifBlende;

        if (param.ExifKamera == null && _exifKamera == null)
        {
            Log.Warning("Objektivkorrektur: Keine EXIF-Daten verfügbar — verwende Fallback-Werte " +
                        "(Canon, EF-S 18-55mm, 35mm, f/5.6)");
        }
        else
        {
            Log.Information("Objektivkorrektur: EXIF — Kamera={Kamera}, Objektiv={Objektiv}, " +
                            "Brennweite={Brennweite}mm, Blende=f/{Blende}",
                kamera, objektiv, brennweite, blende);
        }

        return (kamera, objektiv, brennweite, blende);
    }

    /// <summary>
    /// Parst einen Brennweite-String wie "35 mm" oder "35.0 mm" → float.
    /// </summary>
    private static float TryParseBrennweite(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        // "35 mm" → "35", "18.5 mm" → "18.5"
        var cleaned = text.Replace("mm", "").Replace("MM", "").Trim();
        // Nur den numerischen Prefix extrahieren
        var numPart = new System.Text.StringBuilder();
        foreach (var ch in cleaned)
        {
            if (char.IsDigit(ch) || ch == '.' || ch == ',')
                numPart.Append(ch == ',' ? '.' : ch);
            else if (numPart.Length > 0)
                break; // Stoppen beim ersten nicht-numerischen Zeichen nach Zahlen
        }

        if (numPart.Length > 0 &&
            float.TryParse(numPart.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return result;

        return fallback;
    }

    /// <summary>
    /// Parst einen Blenden-String wie "f/5.6" oder "F5.6" → float.
    /// </summary>
    private static float TryParseBlende(string text, float fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        // "f/5.6" → "5.6", "F5.6" → "5.6"
        var cleaned = text.Replace("f/", "").Replace("F/", "").Replace("F", "").Trim();
        var numPart = new System.Text.StringBuilder();
        foreach (var ch in cleaned)
        {
            if (char.IsDigit(ch) || ch == '.' || ch == ',')
                numPart.Append(ch == ',' ? '.' : ch);
            else if (numPart.Length > 0)
                break;
        }

        if (numPart.Length > 0 &&
            float.TryParse(numPart.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return result;

        return fallback;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _aktuellesBild?.Dispose();
        _originalBild?.Dispose();
        _lensCorrector.Dispose();
        _inferenceEngine.Dispose();
    }

    /// <summary>
    /// Kalibriert die Distortion-Grid-Korrektur anhand eines Schachbrett-Referenzbilds.
    /// FIX #1: Pfad-Validierung gegen Path-Traversal.
    /// </summary>
    /// <param name="pfad">Pfad zum Schachbrett-Bild.</param>
    /// <returns>true bei erfolgreicher Kalibrierung.</returns>
    public bool KalibriereDistortionGrid(string pfad)
    {
        // FIX #1: Pfad-Validierung gegen Path-Traversal
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad);
        if (validierterPfad == null)
        {
            Log.Warning("KalibriereDistortionGrid: Pfad-Validierung fehlgeschlagen");
            return false;
        }
        return _distortionGridCorrector.Kalibrieren(validierterPfad);
    }

    /// <summary>
    /// Kalibriert die Farbkalibrierung anhand eines ColorChecker- oder Graukarten-Bilds.
    /// FIX #1: Pfad-Validierung gegen Path-Traversal.
    /// </summary>
    /// <param name="pfad">Pfad zum Referenzbild.</param>
    /// <returns>true bei erfolgreicher Kalibrierung.</returns>
    public bool KalibriereColor(string pfad)
    {
        // FIX #1: Pfad-Validierung gegen Path-Traversal
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad);
        if (validierterPfad == null)
        {
            Log.Warning("KalibriereColor: Pfad-Validierung fehlgeschlagen");
            return false;
        }
        return _colorCalibration.Kalibrieren(validierterPfad);
    }
}