using System;
using System.Collections.Generic;
using System.Linq;

using OpenCvSharp;

namespace FlipsiColor.Image;

/// <summary>
/// Low-Light-Stufen — Klassifizierung der Dunkelheit eines Bildes.
/// Analog zu NightLift (github.com/LIUZHIYON/NightLift-, MIT): extreme/severe/moderate/mild.
/// </summary>
public enum LowLightStufe
{
    /// <summary>Extrem dunkel — dark_ratio &gt; 0,7 UND Mittel-Luminanz &lt; 40.</summary>
    Extrem,
    /// <summary>Sehr dunkel — dark_ratio &gt; 0,5 ODER Mittel-Luminanz &lt; 60.</summary>
    Stark,
    /// <summary>Mäßig dunkel — dark_ratio &gt; 0,3 ODER Mittel-Luminanz &lt; 90.</summary>
    Mittel,
    /// <summary>Leicht dunkel / normal.</summary>
    Leicht
}

/// <summary>
/// Ergebnis der Low-Light-Analyse eines Bildes.
/// </summary>
public sealed record LowLightAnalyse
{
    /// <summary>Mittlere Luminanz (0–255).</summary>
    public double MeanLuminanz { get; init; }

    /// <summary>Median-Luminanz (0–255).</summary>
    public double MedianLuminanz { get; init; }

    /// <summary>Anteil dunkler Pixel (Luminanz &lt; 50) — 0,0 bis 1,0.</summary>
    public double DarkRatio { get; init; }

    /// <summary>RMS-Kontrast (Standardabweichung der Luminanz).</summary>
    public double RmsKontrast { get; init; }

    /// <summary>Dynamikbereich (P95 − P5 der Luminanz).</summary>
    public double Dynamikbereich { get; init; }

    /// <summary>Erkannte Dunkelheits-Stufe.</summary>
    public LowLightStufe Stufe { get; init; }

    public override string ToString()
        => $"Stufe={Stufe}, Mean={MeanLuminanz:F1}, Median={MedianLuminanz:F1}, " +
           $"DarkRatio={DarkRatio:F3}, RmsKontrast={RmsKontrast:F1}, DynBereich={Dynamikbereich:F1}";
}

/// <summary>
/// Low-Light-Enhancement — klassische OpenCV-Verfahren zur Aufhellung dunkler Bilder.
/// C#-Port des NightLift-Projekts (github.com/LIUZHIYON/NightLift-, MIT-Lizenz), Issue #20.
/// Alle Parameter sind dem Python-Original (engine/enhancer.py) exakt übernommen;
/// Rechen-Puffer als float[] entsprechend numpy float32.
///
/// Verfahren (via Aufhellen(bild, verfahren)):
///   auto          — Analyse + Stufen-Strategie (empfohlen)
///   clahe         — CLAHE auf LAB-L-Kanal (clipLimit 2.0, 8×8)
///   gamma         — Gamma-Korrektur 0.4
///   autolevels    — Histogramm-Stretch (P0.5/P99.5 pro Kanal)
///   msrcp         — Multi-Scale Retinex, chromaticity preserving (15/80/250, α=125, β=46)
///   ssr           — Single-Scale Retinex (sigma 80)
///   dehaze        — Dark Channel Prior (omega 0.85, Fenster 15, t0 0.1)
///   weissabgleich — Gray-World-Weißabgleich
///   helligkeit    — adaptive Helligkeit (+30) / Kontrast (1.3)
///   kombiniert    — Gray-World-WB + CLAHE 1.5 + Gamma 0.7 + Unsharp (1.5/−0.5)
///   stark         — MSRCP (10/60/180, α=140, β=50) + CLAHE 2.5 + Gamma 0.6
///
/// Rein klassisch (kein ONNX). Alle Verfahren geben ein NEUES Mat zurück und
/// disposen das Input-Mat NICHT — der Aufrufer besitzt beide Mats.
/// </summary>
public sealed class LowLightEnhancer
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<LowLightEnhancer>();

    /// <summary>Alle unterstützten Verfahren (kleingeschrieben) — Reihenfolge entspricht der UI-Darstellung.</summary>
    public static readonly IReadOnlyList<string> VerfuegbareVerfahren = new[]
    {
        "auto", "clahe", "gamma", "autolevels", "msrcp", "ssr", "dehaze",
        "weissabgleich", "helligkeit", "kombiniert", "stark"
    };

    // ═══════════════════════════════════════════════════════════
    // Analyse — NightLift analyze_image()
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Analysiert die Luminanz-Lage des Bildes und klassifiziert die Dunkelheits-Stufe.
    /// BGR → Grau, dann Mean/Median, DarkRatio (&lt;50), RMS-Kontrast, Dynamikbereich (P95−P5).
    /// </summary>
    public static LowLightAnalyse Analysieren(Mat bild)
    {
        ArgumentNullException.ThrowIfNull(bild);

        if (bild.Empty())
            return new LowLightAnalyse { Stufe = LowLightStufe.Leicht };

        var pixel = GrauWerteExtrahieren(bild);
        int n = pixel.Length;
        if (n == 0)
            return new LowLightAnalyse { Stufe = LowLightStufe.Leicht };

        double mean = 0;
        int dunkelZaehler = 0;
        for (int i = 0; i < n; i++)
        {
            mean += pixel[i];
            if (pixel[i] < 50)
                dunkelZaehler++;
        }
        mean /= n;
        double darkRatio = dunkelZaehler / (double)n;

        // RMS-Kontrast = Standardabweichung der Luminanz (NightLift: gray.std())
        double varianzSumme = 0;
        for (int i = 0; i < n; i++)
        {
            double d = pixel[i] - mean;
            varianzSumme += d * d;
        }
        double rmsKontrast = Math.Sqrt(varianzSumme / n);

        // Median + Dynamikbereich (P95 − P5) aus einer einzigen Sortierung
        // (NightLift: np.median / np.percentile(gray, 5/95))
        var sortiert = (byte[])pixel.Clone();
        Array.Sort(sortiert);
        double median = PerzentilSortiert(sortiert, 50.0);
        double p5 = PerzentilSortiert(sortiert, 5.0);
        double p95 = PerzentilSortiert(sortiert, 95.0);
        double dynamikbereich = p95 - p5;

        return new LowLightAnalyse
        {
            MeanLuminanz = mean,
            MedianLuminanz = median,
            DarkRatio = darkRatio,
            RmsKontrast = rmsKontrast,
            Dynamikbereich = dynamikbereich,
            Stufe = StufeBestimmen(darkRatio, mean)
        };
    }

    /// <summary>
    /// Stufen-Klassifizierung — exakt aus NightLift analyze_image():
    /// extreme: dark_ratio &gt; 0.7 UND mean &lt; 40; severe: dark_ratio &gt; 0.5 ODER mean &lt; 60;
    /// moderate: dark_ratio &gt; 0.3 ODER mean &lt; 90; mild: Rest.
    /// </summary>
    public static LowLightStufe StufeBestimmen(double darkRatio, double meanLuminanz)
    {
        if (darkRatio > 0.7 && meanLuminanz < 40)
            return LowLightStufe.Extrem;
        if (darkRatio > 0.5 || meanLuminanz < 60)
            return LowLightStufe.Stark;
        if (darkRatio > 0.3 || meanLuminanz < 90)
            return LowLightStufe.Mittel;
        return LowLightStufe.Leicht;
    }

    /// <summary>
    /// Extrahiert die Grau-Luminanz als CV_8U-Byte-Werte.
    /// Unterstützt 1/3/4 Kanäle; 16U wird mit 1/257 auf 8U skaliert,
    /// Float-Eingaben mit ×255 (normierter [0,1]-Bereich angenommen).
    /// </summary>
    private static byte[] GrauWerteExtrahieren(Mat bild)
    {
        using var grauRoh = new Mat();
        if (bild.Channels() == 1)
            bild.CopyTo(grauRoh);
        else if (bild.Channels() == 3)
            Cv2.CvtColor(bild, grauRoh, ColorConversionCodes.BGR2GRAY);
        else if (bild.Channels() == 4)
            Cv2.CvtColor(bild, grauRoh, ColorConversionCodes.BGRA2GRAY);
        else
            throw new ArgumentException($"Analysieren: nicht unterstützte Kanalzahl {bild.Channels()}");

        using var grau = new Mat();
        var tiefe = grauRoh.Depth(); // Depth() ist eine Methode (siehe RawDecoder-Vorbild)
        if (tiefe == MatType.CV_8U)
        {
            grauRoh.CopyTo(grau);
        }
        else if (tiefe == MatType.CV_16U)
        {
            // 16-Bit-Quellen (z.B. RAW) linear auf 8 Bit runterskalieren
            grauRoh.ConvertTo(grau, MatType.CV_8UC1, 1.0 / 257.0, 0);
        }
        else
        {
            // Float-Bilder: normierter [0,1]-Bereich angenommen
            grauRoh.ConvertTo(grau, MatType.CV_8UC1, 255.0, 0);
        }

        // Single-Channel CV_8U → GetArray ist sicher (Multichannel-Falle gilt nicht)
        grau.GetArray(out byte[] pixel);
        return pixel;
    }

    /// <summary>Perzentil eines sortierten Arrays — linear interpoliert wie np.percentile (default 'linear').</summary>
    private static double PerzentilSortiert(byte[] sortiert, double prozent)
    {
        int n = sortiert.Length;
        if (n == 0) return 0;
        if (n == 1) return sortiert[0];

        double pos = prozent / 100.0 * (n - 1);
        int unten = (int)Math.Floor(pos);
        int oben = Math.Min(unten + 1, n - 1);
        double frac = pos - unten;
        return sortiert[unten] * (1.0 - frac) + sortiert[oben] * frac;
    }

    // ═══════════════════════════════════════════════════════════
    // Einheitlicher Einstieg — NightLift enhance_image()
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Hellt ein Bild mit dem benannten Verfahren auf. Gibt ein NEUES Mat zurück;
    /// das Input-Mat wird NICHT disposet (Aufrufer besitzt beide).
    /// Ungültige Verfahren fallen auf 'auto' zurück (mit Warning) — das Feature wird
    /// bei aktiviertem LowLightAktiv niemals still ausgelassen.
    /// </summary>
    public static Mat Aufhellen(Mat bild, string verfahren)
    {
        ArgumentNullException.ThrowIfNull(bild);

        var name = (verfahren ?? "auto").Trim().ToLowerInvariant();
        if (!VerfuegbareVerfahren.Contains(name))
        {
            Log.Warning("LowLight: Unbekanntes Verfahren '{Verfahren}' — falle auf 'auto' zurück", verfahren);
            name = "auto";
        }

        if (name == "auto")
            return AutoAufhellen(bild);

        return name switch
        {
            "clahe" => EnhanceClahe(bild, clipLimit: 2.0, tileGroesse: 8),
            "gamma" => EnhanceGamma(bild, gamma: 0.4),
            "autolevels" => EnhanceAutoLevels(bild, lowPercent: 0.5, highPercent: 99.5),
            "msrcp" => EnhanceMsrcp(bild, skalen: new[] { 15.0, 80.0, 250.0 }, alpha: 125.0, beta: 46.0),
            "ssr" => EnhanceSsr(bild, sigma: 80.0),
            "dehaze" => EnhanceDehaze(bild, omega: 0.85, fensterGroesse: 15, t0: 0.1),
            "weissabgleich" => AutoWhiteBalance(bild),
            "helligkeit" => EnhanceBrightnessContrast(bild, helligkeit: 30, kontrast: 1.3),
            "kombiniert" => EnhanceKombiniert(bild),
            "stark" => EnhanceStark(bild),
            _ => AutoAufhellen(bild)
        };
    }

    /// <summary>'auto' — Analyse + Stufen-Strategie (NightLift enhance_auto).</summary>
    public static Mat AutoAufhellen(Mat bild)
    {
        var analyse = Analysieren(bild);
        Log.Debug("LowLight auto: {Analyse}", analyse);
        return AufhellenNachStufe(bild, analyse);
    }

    /// <summary>Wendet die Stufen-Strategie an — NightLift enhance_auto() mit exakten Parametern.</summary>
    public static Mat AufhellenNachStufe(Mat bild, LowLightAnalyse analyse)
    {
        ArgumentNullException.ThrowIfNull(bild);
        ArgumentNullException.ThrowIfNull(analyse);

        switch (analyse.Stufe)
        {
            case LowLightStufe.Extrem:
            {
                // Extrem dunkel: WB → MSRCP(8/50/150, α=160, β=55) → CLAHE 3.0 → Gamma 0.35
                var schritt1 = AutoWhiteBalance(bild);
                var schritt2 = EnhanceMsrcp(schritt1, skalen: new[] { 8.0, 50.0, 150.0 }, alpha: 160.0, beta: 55.0);
                schritt1.Dispose();
                var schritt3 = EnhanceClahe(schritt2, clipLimit: 3.0, tileGroesse: 8);
                schritt2.Dispose();
                var schritt4 = EnhanceGamma(schritt3, gamma: 0.35);
                schritt3.Dispose();
                return schritt4;
            }
            case LowLightStufe.Stark:
            {
                // Sehr dunkel: WB → CLAHE 2.5 → Gamma 0.45 → moderate Schärfung (1.4/−0.4, σ=2.5)
                var schritt1 = AutoWhiteBalance(bild);
                var schritt2 = EnhanceClahe(schritt1, clipLimit: 2.5, tileGroesse: 8);
                schritt1.Dispose();
                var schritt3 = EnhanceGamma(schritt2, gamma: 0.45);
                schritt2.Dispose();
                var schritt4 = UnsharpMaskAnwenden(schritt3, betrag: 1.4, sigma: 2.5);
                schritt3.Dispose();
                return schritt4;
            }
            case LowLightStufe.Mittel:
            {
                // Mäßig dunkel: WB → CLAHE (1.5 + dark_ratio) → Gamma (0.55 + dark_ratio×0.2) → Unsharp 1.3/−0.3
                var gammaWert = 0.55 + analyse.DarkRatio * 0.2;
                var claheClip = 1.5 + analyse.DarkRatio * 1.0;
                var schritt1 = AutoWhiteBalance(bild);
                var schritt2 = EnhanceClahe(schritt1, clipLimit: claheClip, tileGroesse: 8);
                schritt1.Dispose();
                var schritt3 = EnhanceGamma(schritt2, gamma: gammaWert);
                schritt2.Dispose();
                var schritt4 = UnsharpMaskAnwenden(schritt3, betrag: 1.3, sigma: 2.0);
                schritt3.Dispose();
                return schritt4;
            }
            default: // LowLightStufe.Leicht
            {
                // Leicht dunkel: WB → CLAHE 1.2 → Gamma 0.75 nur wenn mean < 100
                var schritt1 = AutoWhiteBalance(bild);
                var schritt2 = EnhanceClahe(schritt1, clipLimit: 1.2, tileGroesse: 8);
                schritt1.Dispose();
                if (analyse.MeanLuminanz < 100)
                {
                    var schritt3 = EnhanceGamma(schritt2, gamma: 0.75);
                    schritt2.Dispose();
                    return schritt3;
                }
                return schritt2;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Verfahren 1: CLAHE — LAB-L-Kanal (enhance_clahe)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// CLAHE auf dem L-Kanal im LAB-Farbraum — NightLift enhance_clahe (clip 2.0, 8×8).
    /// </summary>
    public static Mat EnhanceClahe(Mat bild, double clipLimit = 2.0, int tileGroesse = 8)
    {
        ArgumentNullException.ThrowIfNull(bild);

        using var lab = new Mat();
        Cv2.CvtColor(bild, lab, ColorConversionCodes.BGR2Lab);

        // Falle Multichannel-Mat: IMMER Cv2.Split/Cv2.Merge, nie GetArray/SetArray direkt
        var kanaele = Cv2.Split(lab);
        try
        {
            using var lAlt = kanaele[0];
            using var clahe = Cv2.CreateCLAHE(clipLimit, new OpenCvSharp.Size(tileGroesse, tileGroesse));
            var lNeu = new Mat();
            clahe.Apply(lAlt, lNeu);
            kanaele[0] = lNeu;

            using var labEq = new Mat();
            Cv2.Merge(kanaele, labEq);

            var ergebnis = new Mat();
            Cv2.CvtColor(labEq, ergebnis, ColorConversionCodes.Lab2BGR);
            return ergebnis;
        }
        finally
        {
            foreach (var k in kanaele) k.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Verfahren 2: Gamma-Korrektur (enhance_gamma)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Gamma-Korrektur via 256er-LUT — NightLift enhance_gamma (Default 0.5, Registry gamma=0.4).
    /// gamma &lt; 1 hellt dunkle Bereiche auf.
    /// </summary>
    public static Mat EnhanceGamma(Mat bild, double gamma = 0.5)
    {
        ArgumentNullException.ThrowIfNull(bild);

        if (gamma <= 0 || gamma > 10)
        {
            Log.Warning("LowLight: Ungültiger Gamma-Wert {Gamma} — verwende 0.5", gamma);
            gamma = 0.5;
        }

        // LUT-Tabelle: table[i] = (i/255)^gamma * 255, geclippt auf [0, 255]
        using var lut = new Mat(1, 256, MatType.CV_8UC1);
        for (int i = 0; i < 256; i++)
        {
            var wert = Math.Pow(i / 255.0, gamma) * 255.0;
            lut.Set(0, i, (byte)Math.Round(Math.Clamp(wert, 0, 255)));
        }

        var ergebnis = new Mat();
        Cv2.LUT(bild, lut, ergebnis);
        return ergebnis;
    }

    // ═══════════════════════════════════════════════════════════
    // Verfahren 3: Auto-Levels — Histogramm-Stretch (enhance_auto_levels)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Auto-Levels: pro Kanal P0.5/P99.5 als Stützpunkte, Streckung auf [0,255].
    /// Schneidet Extrem-Pixel ab, um Rauschverstärkung zu vermeiden — NightLift enhance_auto_levels.
    /// </summary>
    public static Mat EnhanceAutoLevels(Mat bild, double lowPercent = 0.5, double highPercent = 99.5)
    {
        ArgumentNullException.ThrowIfNull(bild);

        var kanaele = Cv2.Split(bild);
        try
        {
            for (int c = 0; c < kanaele.Length; c++)
            {
                kanaele[c].GetArray(out byte[] werte);
                if (werte.Length == 0) continue;

                var sortiert = (byte[])werte.Clone();
                Array.Sort(sortiert);
                double lowVal = PerzentilSortiert(sortiert, lowPercent);
                double highVal = PerzentilSortiert(sortiert, highPercent);
                if (highVal <= lowVal)
                    highVal = lowVal + 1;

                double spanne = highVal - lowVal;
                for (int i = 0; i < werte.Length; i++)
                {
                    var gestreckt = (werte[i] - lowVal) / spanne * 255.0;
                    werte[i] = (byte)Math.Round(Math.Clamp(gestreckt, 0, 255));
                }

                kanaele[c].SetArray(werte);
            }

            var ergebnis = new Mat();
            Cv2.Merge(kanaele, ergebnis);
            return ergebnis;
        }
        finally
        {
            foreach (var k in kanaele) k.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Verfahren 4: MSRCP — Multi-Scale Retinex, chromaticity preserving (enhance_msrcp)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Multi-Scale Retinex mit Farberhaltung (Jobson et al.) — NightLift enhance_msrcp.
    /// 1. Farbanteile (chromaticity) des Originals extrahieren
    /// 2. Intensitätskanal mit Multi-Scale-Retinex verstärken
    /// 3. Farbanteile auf die neue Intensität anwenden
    /// Puffer als float[] — entspricht numpy float32 des Originals.
    /// </summary>
    public static Mat EnhanceMsrcp(Mat bild, double[]? skalen = null, double alpha = 125.0, double beta = 46.0)
    {
        ArgumentNullException.ThrowIfNull(bild);

        var skalenArray = skalen ?? new[] { 15.0, 80.0, 250.0 };
        if (skalenArray.Length == 0)
        {
            Log.Warning("LowLight MSRCP: leere Skalen-Liste — verwende NightLift-Default 15/80/250");
            skalenArray = new[] { 15.0, 80.0, 250.0 };
        }

        int zeilen = bild.Rows, spalten = bild.Cols;
        int n = zeilen * spalten;

        // img_f = img.astype(float32) + 1.0 (+1 verhindert log(0))
        var bArr = new float[n];
        var gArr = new float[n];
        var rArr = new float[n];

        var kanaele = Cv2.Split(bild);
        try
        {
            kanaele[0].GetArray(out byte[] bBytes);
            kanaele[1].GetArray(out byte[] gBytes);
            kanaele[2].GetArray(out byte[] rBytes);
            for (int i = 0; i < n; i++)
            {
                bArr[i] = bBytes[i] + 1.0f;
                gArr[i] = gBytes[i] + 1.0f;
                rArr[i] = rBytes[i] + 1.0f;
            }
        }
        finally
        {
            foreach (var k in kanaele) k.Dispose();
        }

        // Intensität = Summe der Kanäle / 3
        var intensitaet = new float[n];
        for (int i = 0; i < n; i++)
            intensitaet[i] = (bArr[i] + gArr[i] + rArr[i]) / 3.0f;

        // Farbanteile (chromaticity): img_f / max(intensität, 1)
        var chromB = new float[n];
        var chromG = new float[n];
        var chromR = new float[n];
        for (int i = 0; i < n; i++)
        {
            var sicher = intensitaet[i] < 1.0f ? 1.0f : intensitaet[i];
            chromB[i] = bArr[i] / sicher;
            chromG[i] = gArr[i] / sicher;
            chromR[i] = rArr[i] / sicher;
        }

        // Multi-Scale-Retinex: Mittel über log(I) − log(I_blur(σ)) je Skala
        var retinex = new float[n];
        using (var intensMat = MatVonFloats(intensitaet, zeilen, spalten))
        {
            foreach (var sigma in skalenArray)
            {
                using var blur = new Mat();
                Cv2.GaussianBlur(intensMat, blur, new OpenCvSharp.Size(0, 0), sigma);
                blur.GetArray(out float[] blurWerte);

                for (int i = 0; i < n; i++)
                {
                    var blurred = blurWerte[i] < 1.0f ? 1.0 : blurWerte[i];
                    retinex[i] += (float)(Math.Log(intensitaet[i]) - Math.Log(blurred));
                }
            }
        }
        float invSkalen = 1.0f / skalenArray.Length;
        for (int i = 0; i < n; i++)
            retinex[i] *= invSkalen;

        // Intensitätskorrektur: exp(retinex × α/255 + log(β))
        var intensNeu = new float[n];
        for (int i = 0; i < n; i++)
            intensNeu[i] = (float)Math.Exp(retinex[i] * alpha / 255.0 + Math.Log(beta));

        // Normalisieren auf [0, 255]; degeneriert → 128
        float iMin = float.MaxValue, iMax = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            if (intensNeu[i] < iMin) iMin = intensNeu[i];
            if (intensNeu[i] > iMax) iMax = intensNeu[i];
        }
        if (iMax > iMin)
        {
            float spanne = iMax - iMin;
            for (int i = 0; i < n; i++)
                intensNeu[i] = (intensNeu[i] - iMin) / spanne * 255.0f;
        }
        else
        {
            for (int i = 0; i < n; i++)
                intensNeu[i] = 128.0f;
        }

        // Farbanteile wieder anwenden: result = intensNeu × chromaticity, Clip [0, 255]
        var outB = new byte[n];
        var outG = new byte[n];
        var outR = new byte[n];
        for (int i = 0; i < n; i++)
        {
            outB[i] = (byte)Math.Round(Math.Clamp(intensNeu[i] * chromB[i], 0, 255));
            outG[i] = (byte)Math.Round(Math.Clamp(intensNeu[i] * chromG[i], 0, 255));
            outR[i] = (byte)Math.Round(Math.Clamp(intensNeu[i] * chromR[i], 0, 255));
        }

        using var matB = MatVonBytes(outB, zeilen, spalten);
        using var matG = MatVonBytes(outG, zeilen, spalten);
        using var matR = MatVonBytes(outR, zeilen, spalten);

        var ergebnis = new Mat();
        Cv2.Merge(new[] { matB, matG, matR }, ergebnis);
        return ergebnis;
    }

    // ═══════════════════════════════════════════════════════════
    // Verfahren 5: Gray-World-Weißabgleich (auto_white_balance)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Gray-World-Weißabgleich: passt Kanal-Gains so an, dass der Szenen-Mittelwert grau ist —
    /// NightLift auto_white_balance. Konstruktionsbedingt mittelerhaltend
    /// (alle Kanal-Mittel werden auf denselben Grau-Wert gehoben).
    /// </summary>
    public static Mat AutoWhiteBalance(Mat bild)
    {
        ArgumentNullException.ThrowIfNull(bild);

        var mean = bild.Mean(); // [0]=B, [1]=G, [2]=R
        double avgB = mean[0], avgG = mean[1], avgR = mean[2];
        double gray = (avgB + avgG + avgR) / 3.0;

        var kanaele = Cv2.Split(bild);
        try
        {
            for (int c = 0; c < kanaele.Length; c++)
            {
                var avg = c == 0 ? avgB : c == 1 ? avgG : avgR;
                if (avg > 0 && gray > 0)
                {
                    var skaliert = new Mat();
                    kanaele[c].ConvertTo(skaliert, -1, gray / avg, 0);
                    kanaele[c].Dispose();
                    kanaele[c] = skaliert; // ConvertTo clippt auf [0, 255] bei CV_8U
                }
            }

            var ergebnis = new Mat();
            Cv2.Merge(kanaele, ergebnis);
            return ergebnis;
        }
        finally
        {
            foreach (var k in kanaele) k.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Verfahren 6: Helligkeit/Kontrast adaptiv (enhance_brightness_contrast)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Adaptive Helligkeit/Kontrast-Anpassung: result = img × kontrast + helligkeit —
    /// NightLift enhance_brightness_contrast (Default +30 / 1.3).
    /// </summary>
    public static Mat EnhanceBrightnessContrast(Mat bild, int helligkeit = 30, double kontrast = 1.3)
    {
        ArgumentNullException.ThrowIfNull(bild);

        var ergebnis = new Mat();
        bild.ConvertTo(ergebnis, -1, kontrast, helligkeit); // clippt automatisch auf [0, 255]
        return ergebnis;
    }

    // ═══════════════════════════════════════════════════════════
    // Verfahren 7: SSR — Single-Scale Retinex (enhance_ssr)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Single-Scale Retinex: Reflexionsanteil = log(Original) − log(Gauß-Blur) —
    /// NightLift enhance_ssr (σ=80). Pro Kanal unabhängig auf [0, 255] normalisiert.
    /// </summary>
    public static Mat EnhanceSsr(Mat bild, double sigma = 80.0)
    {
        ArgumentNullException.ThrowIfNull(bild);

        int zeilen = bild.Rows, spalten = bild.Cols;
        int n = zeilen * spalten;

        var kanaele = Cv2.Split(bild);
        var outKanaele = new Mat[3];
        try
        {
            for (int c = 0; c < 3; c++)
            {
                kanaele[c].GetArray(out byte[] werte);
                var imgF = new float[n];
                for (int i = 0; i < n; i++)
                    imgF[i] = werte[i] + 1.0f; // +1 verhindert log(0)

                using var imgMat = MatVonFloats(imgF, zeilen, spalten);
                using var blurMat = new Mat();
                Cv2.GaussianBlur(imgMat, blurMat, new OpenCvSharp.Size(0, 0), sigma);
                blurMat.GetArray(out float[] blurWerte);

                // retinex = log(img) − log(blur), pro Kanal min-max normalisiert
                var retinex = new float[n];
                float rMin = float.MaxValue, rMax = float.MinValue;
                for (int i = 0; i < n; i++)
                {
                    var blurred = blurWerte[i] < 1.0f ? 1.0 : blurWerte[i];
                    retinex[i] = (float)(Math.Log(imgF[i]) - Math.Log(blurred));
                    if (retinex[i] < rMin) rMin = retinex[i];
                    if (retinex[i] > rMax) rMax = retinex[i];
                }

                var outWerte = new byte[n];
                if (rMax > rMin)
                {
                    float spanne = rMax - rMin;
                    for (int i = 0; i < n; i++)
                        outWerte[i] = (byte)Math.Round((retinex[i] - rMin) / spanne * 255.0);
                }
                else
                {
                    for (int i = 0; i < n; i++)
                        outWerte[i] = 128; // degenerierter Kanal
                }

                outKanaele[c] = MatVonBytes(outWerte, zeilen, spalten);
            }

            var ergebnis = new Mat();
            Cv2.Merge(outKanaele, ergebnis);
            return ergebnis;
        }
        finally
        {
            foreach (var k in kanaele) k.Dispose();
            foreach (var k in outKanaele) k?.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Verfahren 8: Dehaze — Dark Channel Prior (enhance_dehaze, He et al. CVPR 2009)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Entnebelung/Verstärkung via Dark Channel Prior — NightLift enhance_dehaze
    /// (ω=0.85, Fenster 15, t0=0.1). Entfernt den "Schleier" dunstiger Aufnahmen und
    /// hebt primär den KONTRAST (dunkle Pixel werden Richtung Atmosphärenlicht gezogen).
    /// </summary>
    public static Mat EnhanceDehaze(Mat bild, double omega = 0.85, int fensterGroesse = 15, double t0 = 0.1)
    {
        ArgumentNullException.ThrowIfNull(bild);

        int zeilen = bild.Rows, spalten = bild.Cols;
        int n = zeilen * spalten;

        var kanaele = Cv2.Split(bild);
        try
        {
            // Dunkelkanal: Minimum über die Kanäle [0,1], dann Erosion mit Rechteck-Fenster
            kanaele[0].GetArray(out byte[] bBytes);
            kanaele[1].GetArray(out byte[] gBytes);
            kanaele[2].GetArray(out byte[] rBytes);

            var dunkel = new float[n];
            for (int i = 0; i < n; i++)
            {
                var min = Math.Min(bBytes[i], Math.Min(gBytes[i], rBytes[i]));
                dunkel[i] = min / 255.0f;
            }

            using var dunkelMat = MatVonFloats(dunkel, zeilen, spalten);
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect,
                new OpenCvSharp.Size(fensterGroesse, fensterGroesse));
            using var dunkelErodiert = new Mat();
            Cv2.Erode(dunkelMat, dunkelErodiert, kernel);
            dunkelErodiert.GetArray(out float[] dunkelWerte);

            // Atmosphärenlicht: Maximum der Original-Pixel an den hellsten 0,1 %
            // Dunkelkanal-Pixeln (NightLift: np.argpartition top-0.1%).
            // O(n)-Histogramm-Auswahl statt Sortierung — bis Bin-Granularität
            // (4096 Bins) äquivalent, bei Bindung gleicher Werte leicht inklusiver.
            int anzahlHellster = Math.Max((int)(n * 0.001), 1);
            const int Bins = 4096;
            var histogramm = new int[Bins];
            for (int i = 0; i < n; i++)
            {
                int bin = (int)(dunkelWerte[i] * Bins);
                if (bin < 0) bin = 0;
                if (bin >= Bins) bin = Bins - 1;
                histogramm[bin]++;
            }

            int kumuliert = 0;
            int schwellenBin = Bins - 1;
            for (int bin = Bins - 1; bin >= 0; bin--)
            {
                kumuliert += histogramm[bin];
                if (kumuliert >= anzahlHellster)
                {
                    schwellenBin = bin;
                    break;
                }
            }
            var schwelle = schwellenBin / (float)Bins;

            double atmB = 0, atmG = 0, atmR = 0;
            for (int i = 0; i < n; i++)
            {
                if (dunkelWerte[i] >= schwelle)
                {
                    atmB = Math.Max(atmB, bBytes[i] / 255.0);
                    atmG = Math.Max(atmG, gBytes[i] / 255.0);
                    atmR = Math.Max(atmR, rBytes[i] / 255.0);
                }
            }

            // Transmissions: t = clip(1 − ω·dark, t0, 1), dann bilateral geglättet
            var transmission = new float[n];
            for (int i = 0; i < n; i++)
            {
                var t = 1.0 - omega * dunkelWerte[i];
                transmission[i] = (float)Math.Clamp(t, t0, 1.0);
            }

            using var transMat = MatVonFloats(transmission, zeilen, spalten);
            using var transGeblaettet = new Mat();
            Cv2.BilateralFilter(transMat, transGeblaettet, 9, 75, 75);
            transGeblaettet.GetArray(out float[] transWerte);

            // Wiederherstellung: result[c] = (img[c] − A[c]) / max(t, t0) + A[c]
            var outB = new byte[n];
            var outG = new byte[n];
            var outR = new byte[n];
            for (int i = 0; i < n; i++)
            {
                var t = transWerte[i] < t0 ? t0 : transWerte[i];
                var wertB = (bBytes[i] / 255.0 - atmB) / t + atmB;
                var wertG = (gBytes[i] / 255.0 - atmG) / t + atmG;
                var wertR = (rBytes[i] / 255.0 - atmR) / t + atmR;
                outB[i] = (byte)Math.Round(Math.Clamp(wertB * 255.0, 0, 255));
                outG[i] = (byte)Math.Round(Math.Clamp(wertG * 255.0, 0, 255));
                outR[i] = (byte)Math.Round(Math.Clamp(wertR * 255.0, 0, 255));
            }

            using var matB = MatVonBytes(outB, zeilen, spalten);
            using var matG = MatVonBytes(outG, zeilen, spalten);
            using var matR = MatVonBytes(outR, zeilen, spalten);

            var ergebnis = new Mat();
            Cv2.Merge(new[] { matB, matG, matR }, ergebnis);
            return ergebnis;
        }
        finally
        {
            foreach (var k in kanaele) k.Dispose();
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Kombi-Verfahren — enhance_comprehensive / enhance_strong
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Kombinierte Verstärkung — NightLift enhance_comprehensive:
    /// Gray-World-WB → CLAHE 1.5 → Gamma 0.7 → leichte Schärfung (1.5/−0.5, σ=2.0).
    /// </summary>
    public static Mat EnhanceKombiniert(Mat bild)
    {
        ArgumentNullException.ThrowIfNull(bild);

        var schritt1 = AutoWhiteBalance(bild);
        var schritt2 = EnhanceClahe(schritt1, clipLimit: 1.5, tileGroesse: 8);
        schritt1.Dispose();
        var schritt3 = EnhanceGamma(schritt2, gamma: 0.7);
        schritt2.Dispose();
        var schritt4 = UnsharpMaskAnwenden(schritt3, betrag: 1.5, sigma: 2.0);
        schritt3.Dispose();
        return schritt4;
    }

    /// <summary>
    /// Starke Verstärkung für extrem dunkle Szenen — NightLift enhance_strong:
    /// MSRCP (10/60/180, α=140, β=50) → CLAHE 2.5 → Gamma 0.6.
    /// </summary>
    public static Mat EnhanceStark(Mat bild)
    {
        ArgumentNullException.ThrowIfNull(bild);

        var schritt1 = EnhanceMsrcp(bild, skalen: new[] { 10.0, 60.0, 180.0 }, alpha: 140.0, beta: 50.0);
        var schritt2 = EnhanceClahe(schritt1, clipLimit: 2.5, tileGroesse: 8);
        schritt1.Dispose();
        var schritt3 = EnhanceGamma(schritt2, gamma: 0.6);
        schritt2.Dispose();
        return schritt3;
    }

    /// <summary>
    /// Unsharp Masking: result = img × betrag + blur × (1 − betrag) —
    /// NightLift: cv2.addWeighted(img, 1.4, blurred, −0.4, 0).
    /// </summary>
    private static Mat UnsharpMaskAnwenden(Mat bild, double betrag, double sigma)
    {
        using var blur = new Mat();
        Cv2.GaussianBlur(bild, blur, new OpenCvSharp.Size(0, 0), sigma);

        var ergebnis = new Mat();
        Cv2.AddWeighted(bild, betrag, blur, 1.0 - betrag, 0, ergebnis);
        return ergebnis;
    }

    // ═══════════════════════════════════════════════════════════
    // Hilfsfunktionen
    // ═══════════════════════════════════════════════════════════

    /// <summary>Erzeugt ein CV_32FC1-Mat aus float-Werten (kopiert, Caller disposet).</summary>
    private static Mat MatVonFloats(float[] werte, int zeilen, int spalten)
    {
        var mat = new Mat(zeilen, spalten, MatType.CV_32FC1);
        mat.SetArray(werte);
        return mat;
    }

    /// <summary>Erzeugt ein CV_8UC1-Mat aus byte-Werten (kopiert, Caller disposet).</summary>
    private static Mat MatVonBytes(byte[] werte, int zeilen, int spalten)
    {
        var mat = new Mat(zeilen, spalten, MatType.CV_8UC1);
        mat.SetArray(werte);
        return mat;
    }
}