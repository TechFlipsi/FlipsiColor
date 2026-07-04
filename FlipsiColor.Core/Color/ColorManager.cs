using System;
using System.Runtime.InteropServices;
using OpenCvSharp;

using FlipsiColor.Utils;

namespace FlipsiColor.Color;

/// <summary>
/// Farbmanagement — ProPhoto RGB Arbeitsfarbraum, Monitor-Profil-Erkennung (nur Windows).
/// Auf Linux/macOS wird sRGB als Fallback verwendet.
/// </summary>
public sealed class ColorManager : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ColorManager>();

    public const string Arbeitsfarbraum = "ProPhoto RGB";
    private bool _disposed;

    // sRGB→ProPhoto RGB Matrix (D50, aus Lindbloom, transponiert für Cv2.Transform mit RGB-Spaltenvektoren):
    private static readonly float[,] SrgbToProPhoto =
    {
        { 0.7976749f, 0.1351915f, 0.0313436f },
        { 0.2880402f, 0.7118742f, 0.0000856f },
        { 0.0000000f, 0.0000000f, 0.8252100f }
    };

    public void Initialisieren()
    {
        Log.Information("Farbmanagement initialisiert. Arbeitsfarbraum: {Farbraum}", Arbeitsfarbraum);
        var monitorProfil = MonitorProfilErkennen();
        Log.Information("Monitor-Profil: {Profil}", monitorProfil ?? "sRGB (Fallback)");
    }

    /// <summary>
    /// Erkennt das ICC-Profil des primären Monitors via Win32 GetICMProfileW.
    /// Nur auf Windows verfügbar. Auf Linux/macOS wird sRGB als Fallback verwendet.
    /// </summary>
    public string? MonitorProfilErkennen()
    {
        if (!OperatingSystem.IsWindows())
        {
            Log.Debug("Monitor-Profil-Erkennung nicht auf dieser Plattform — sRGB-Fallback");
            return null;
        }

        try
        {
            Log.Debug("Monitor-Profil-Erkennung aufgerufen");
            IntPtr hdc = IcmNative.GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero)
            {
                Log.Debug("GetDC fehlgeschlagen — sRGB-Fallback");
                return null;
            }

            try
            {
                var sb = new System.Text.StringBuilder(260);
                int laenge = sb.Capacity;
                bool ok = IcmNative.GetICMProfileW(hdc, ref laenge, sb);
                if (ok && laenge > 0)
                {
                    var profil = sb.ToString();
                    Log.Debug("ICC-Profil gefunden: {Profil}", profil);
                    return profil;
                }
                Log.Debug("Kein ICC-Profil für primären Monitor — sRGB-Fallback");
                return null;
            }
            finally
            {
                IcmNative.ReleaseDC(IntPtr.Zero, hdc);
            }
        }
        catch (Exception ex)
        {
            Log.Debug("Monitor-Profil-Erkennung nicht verfügbar — sRGB-Fallback: {Msg}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Konvertiert ein Bild in den ProPhoto RGB Arbeitsfarbraum.
    /// Pipeline: BGR → lineares sRGB (Gamma-Dekodierung) → ProPhoto RGB (Matrix).
    /// </summary>
    public Mat NachArbeitsfarbraumKonvertieren(Mat eingabe)
    {
        if (eingabe.Empty())
            return eingabe;

        if (eingabe.Channels() is not 3 and not 4)
        {
            var result = new Mat();
            eingabe.CopyTo(result);
            return result;
        }

        Mat bgr;
        Mat? alpha = null;
        if (eingabe.Channels() == 4)
        {
            var channels = Cv2.Split(eingabe);
            bgr = new Mat();
            Cv2.Merge([channels[0], channels[1], channels[2]], bgr);
            alpha = channels[3];
            channels[0].Dispose();
            channels[1].Dispose();
            channels[2].Dispose();
        }
        else
        {
            bgr = eingabe.Clone();
        }

        try
        {
            using Mat rgb = new();
            Cv2.CvtColor(bgr, rgb, ColorConversionCodes.BGR2RGB);

            using Mat rgb32 = new();
            rgb.ConvertTo(rgb32, MatType.CV_32FC3, 1.0 / 255.0);

            using Mat linear = new();
            Cv2.Pow(rgb32, 2.2, linear);

            using Mat m = new(3, 3, MatType.CV_32FC1);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    m.Set<float>(i, j, SrgbToProPhoto[i, j]);

            using Mat proPhoto = new();
            Cv2.Transform(linear, proPhoto, m);

            using Mat proPhoto8 = new();
            Cv2.Pow(proPhoto, 1.0 / 2.2, proPhoto8);
            proPhoto8.ConvertTo(proPhoto8, MatType.CV_8UC3, 255.0);

            Mat ergebnisBgr = new();
            Cv2.CvtColor(proPhoto8, ergebnisBgr, ColorConversionCodes.RGB2BGR);

            if (alpha != null)
            {
                var bgra = new Mat();
                Cv2.CvtColor(ergebnisBgr, bgra, ColorConversionCodes.BGR2BGRA);
                var ch = Cv2.Split(bgra);
                ch[3].Dispose();
                ch[3] = alpha;
                Cv2.Merge(ch, bgra);
                foreach (var c in ch) c.Dispose();
                ergebnisBgr.Dispose();
                return bgra;
            }

            return ergebnisBgr;
        }
        finally
        {
            bgr.Dispose();
            alpha?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

/// <summary>
/// Weißabgleich — statistische Methoden (kein Modell benötigt, &lt;1ms)
/// </summary>
public sealed class WhiteBalance
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<WhiteBalance>();

    public record WBErgebnis(double Temperatur, double Tint);

    public WBErgebnis GrayWorld(Mat bild)
    {
        var mean = bild.Mean();
        double avgR = mean[2], avgG = mean[1], avgB = mean[0];
        double scale = (avgR + avgG + avgB) / 3.0;
        if (scale < 1e-6) scale = 1e-6;
        double rRatio = avgR / scale;
        double bRatio = avgB / scale;
        var (temp, tint) = RbRatioZuTemperatur(rRatio, bRatio);
        Log.Debug("GrayWorld: R={R:F3} G={G:F3} B={B:F3} → Temp={Temp:F0}K Tint={Tint:F1}",
            avgR, avgG, avgB, temp, tint);
        return new WBErgebnis(temp, tint);
    }

    public WBErgebnis ShadesOfGray(Mat bild, int m = 10)
    {
        var mean = bild.Mean();
        double avgR = Math.Pow(mean[2], m);
        double avgG = Math.Pow(mean[1], m);
        double avgB = Math.Pow(mean[0], m);
        double norm = Math.Pow(avgR + avgG + avgB, 1.0 / m);
        if (norm < 1e-6) norm = 1e-6;
        double rRatio = avgR / norm;
        double bRatio = avgB / norm;
        var (temp, tint) = RbRatioZuTemperatur(rRatio, bRatio);
        return new WBErgebnis(temp, tint);
    }

    public WBErgebnis AutoWb(Mat bild)
    {
        var gw = GrayWorld(bild);
        var sog = ShadesOfGray(bild, 10);
        double temp = gw.Temperatur * 0.4 + sog.Temperatur * 0.6;
        double tint = gw.Tint * 0.4 + sog.Tint * 0.6;
        Log.Debug("AutoWB: GW={GwTemp:F0}K, SoG={SogTemp:F0}K → Final={FinalTemp:F0}K",
            gw.Temperatur, sog.Temperatur, temp);
        return new WBErgebnis(temp, tint);
    }

    private static (double Temp, double Tint) RbRatioZuTemperatur(double rRatio, double bRatio)
    {
        double temp = 5500.0;
        if (rRatio > 1.0) temp -= (rRatio - 1.0) * 3000;
        if (bRatio > 1.0) temp += (bRatio - 1.0) * 3000;
        temp = Math.Clamp(temp, 2000, 15000);
        double tint = (rRatio - bRatio) * 50;
        return (temp, tint);
    }
}

/// <summary>
/// Win32 ICM-API für ICC-Profil-Erkennung (P/Invoke, nur Windows).
/// Auf anderen Plattformen nicht verfügbar — Methoden werden nicht aufgerufen.
/// </summary>
internal static class IcmNative
{
    private const string Gdi32 = "gdi32.dll";
    private const string User32 = "user32.dll";

    [DllImport(User32, CallingConvention = CallingConvention.Winapi)]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport(User32, CallingConvention = CallingConvention.Winapi)]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport(Gdi32, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetICMProfileW(IntPtr hdc, ref int lpszFilename, System.Text.StringBuilder lpszFilenameBuffer);
}