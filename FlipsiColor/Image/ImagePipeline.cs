using System;
using System.IO;
using System.Threading.Tasks;
using OpenCvSharp;

using FlipsiColor.AI;
using FlipsiColor.Color;
using FlipsiColor.Core;

namespace FlipsiColor.Image;

/// <summary>
/// Bild-Pipeline — kompletter Verarbeitungsworkflow
/// </summary>
public sealed class ImagePipeline : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ImagePipeline>();
    private Mat? _aktuellesBild;
    private Mat? _originalBild;
    private bool _disposed;
    private readonly ModelManager _modelManager;
    private readonly ColorManager _colorManager;
    private readonly WhiteBalance _whiteBalance;
    private readonly LensCorrector _lensCorrector;
    private readonly StyleLUT _styleLut;

    public event EventHandler? BildGeladen;
    public event EventHandler? PipelineAbgeschlossen;

    public ImagePipeline(ModelManager modelManager, ColorManager colorManager)
    {
        _modelManager = modelManager;
        _colorManager = colorManager;
        _whiteBalance = new WhiteBalance();
        _lensCorrector = new LensCorrector();
        _styleLut = new StyleLUT();
        _lensCorrector.Initialisieren();
    }

    /// <summary>
    /// Lädt ein Bild (PNG, JPG, TIFF, BMP + RAW)
    /// </summary>
    public bool BildLaden(string pfad)
    {
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
                Log.Error("Bild konnte nicht geladen werden: {Pfad}", pfad);
                return false;
            }

            _originalBild = _aktuellesBild.Clone();
            Log.Information("Bild geladen: {Pfad} ({Breite}x{Hoehe}, {Kanaele})",
                pfad, _aktuellesBild.Width, _aktuellesBild.Height, _aktuellesBild.Channels());
            BildGeladen?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Laden: {Pfad}", pfad);
            return false;
        }
    }

    /// <summary>
    /// Führt die komplette Pipeline mit den gegebenen Parametern aus
    /// </summary>
    public void PipelineAusfuehren(PipelineParams param)
    {
        if (_originalBild == null || _originalBild.Empty())
        {
            Log.Warning("Kein Bild geladen — Pipeline übersprungen");
            return;
        }

        Log.Information("Pipeline startet: Belichtung={Bel} Kontrast={Kontr} Sättigung={Saet}",
            param.Belichtung, param.Kontrast, param.Saettigung);

        var bild = _originalBild.Clone();

        try
        {
            // 1. Weißabgleich
            if (Math.Abs(param.WeissabgleichTemp - 5500.0f) > 1 || Math.Abs(param.WeissabgleichTint) > 1)
            {
                var wbResult = _whiteBalance.AutoWb(bild);
                var neuesBild = WeissabgleichAnwenden(bild, (float)wbResult.Temperatur, (float)wbResult.Tint);
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

            // 5. Sättigung & Vibranz
            if (Math.Abs(param.Saettigung) > 0.01f || Math.Abs(param.Vibranz) > 0.01f)
            {
                var neuesBild = SaettigungAnwenden(bild, param.Saettigung, param.Vibranz);
                bild.Dispose();
                bild = neuesBild;
            }

            // 6. Schärfe
            if (Math.Abs(param.SchaerfeBetrag) > 0.01f)
            {
                var neuesBild = SchaerfeAnwenden(bild, param.SchaerfeBetrag);
                bild.Dispose();
                bild = neuesBild;
            }

            // 7. Rauschunterdrückung
            if (param.LuminanzRauschen > 0.01f || param.ChrominanzRauschen > 0.01f)
            {
                var neuesBild = RauschunterdrueckungAnwenden(bild, param.LuminanzRauschen, param.ChrominanzRauschen);
                bild.Dispose();
                bild = neuesBild;
            }

            // 8. Objektivkorrektur
            if (param.ObjektivkorrekturAktiv)
            {
                var neuesBild = _lensCorrector.Korrigieren(bild, "Canon", "EF-S 18-55mm", 35f, 5.6f);
                bild.Dispose();
                bild = neuesBild;
            }

            _aktuellesBild?.Dispose();
            _aktuellesBild = bild;

            Log.Information("Pipeline abgeschlossen");
            PipelineAbgeschlossen?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Pipeline-Fehler");
            bild.Dispose();
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
        var result = new Mat();
        bild.ConvertTo(result, -1, 1.0, belichtung * 50);
        return result;
    }

    private static Mat KontrastAnwenden(Mat bild, float kontrast)
    {
        var result = new Mat();
        var alpha = 1.0 + kontrast * 0.5;
        bild.ConvertTo(result, -1, alpha, 128 * (1 - alpha));
        return result;
    }

    private static Mat LichterSchattenAnwenden(Mat bild, float wert, bool lichter)
    {
        var result = new Mat();
        if (lichter)
        {
            // Lichter aufhellen: Helle Pixel weiter aufhellen
            var mask = new Mat();
            Cv2.Threshold(bild, mask, 200, 255, ThresholdTypes.Binary);
            bild.CopyTo(result);
            result.ConvertTo(result, -1, 1.0, wert * 20);
            // TODO: Maskenbasierte Anwendung für selektive Korrektur
            mask.Dispose();
        }
        else
        {
            bild.CopyTo(result);
            result.ConvertTo(result, -1, 1.0, wert * 20);
        }
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
        Cv2.GaussianBlur(bild, result, new Size(0, 0), 3);
        Cv2.AddWeighted(bild, 1.0 + amount, result, -amount, 0, result);
        return result;
    }

    private static Mat RauschunterdrueckungAnwenden(Mat bild, float luma, float chroma)
    {
        var result = new Mat();
        var h = Math.Max(3, (int)(luma * 10));
        Cv2.GaussianBlur(bild, result, new Size(0, 0), h);
        // TODO: Chroma-Rauschunterdrückung im YUV-Farbraum
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _aktuellesBild?.Dispose();
        _originalBild?.Dispose();
        _lensCorrector.Dispose();
    }
}