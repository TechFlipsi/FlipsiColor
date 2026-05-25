using System;
using OpenCvSharp;

using FlipsiColor.Utils;

namespace FlipsiColor.Color;

/// <summary>
/// Farbmanagement — ProPhoto RGB Arbeitsfarbraum, Monitor-Profil-Erkennung
/// </summary>
public sealed class ColorManager : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ColorManager>();

    public const string Arbeitsfarbraum = "ProPhoto RGB";
    private bool _disposed;

    public void Initialisieren()
    {
        Log.Information("Farbmanagement initialisiert. Arbeitsfarbraum: {Farbraum}", Arbeitsfarbraum);
        var monitorProfil = MonitorProfilErkennen();
        Log.Information("Monitor-Profil: {Profil}", monitorProfil ?? "nicht gefunden");
    }

    /// <summary>
    /// Erkennt das ICC-Profil des Monitors (Windows API via System.Drawing)
    /// </summary>
    public string? MonitorProfilErkennen()
    {
        try
        {
            // Windows: ICC-Profil via System.Drawing (WPF Dispatcher nötig)
            // TODO: Win32 API GetICMProfile für primären Monitor
            Log.Debug("Monitor-Profil-Erkennung aufgerufen");
            return null; // Wird mit Win32 Interop implementiert
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Monitor-Profil konnte nicht erkannt werden");
            return null;
        }
    }

    /// <summary>
    /// Konvertiert ein Bild in den ProPhoto RGB Arbeitsfarbraum
    /// </summary>
    public Mat NachArbeitsfarbraumKonvertieren(Mat eingabe)
    {
        if (eingabe.Empty())
            return eingabe;

        // ProPhoto RGB = linearer RGB Farbraum mit gamut > sRGB
        // OpenCV: Konvertierung über sRGB-Zwischenschritt
        var result = new Mat();
        if (eingabe.Channels() == 3)
        {
            // Annahme: Eingabe ist BGR (OpenCV Standard)
            // TODO: LCMS2 basierte Konvertierung via P/Invoke für korrekte ICC-Transform
            eingabe.CopyTo(result);
        }
        else
        {
            eingabe.CopyTo(result);
        }
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

/// <summary>
/// Weißabgleich — statistische Methoden (kein Modell benötigt, <1ms)
/// </summary>
public sealed class WhiteBalance
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<WhiteBalance>();

    /// <summary>Ergebnis des Weißabgleichs</summary>
    public record WBErgebnis(double Temperatur, double Tint);

    /// <summary>
    /// Gray-World-Algorithmus: Durchschnittsfarbe → neutral
    /// </summary>
    public WBErgebnis GrayWorld(Mat bild)
    {
        var mean = bild.Mean();
        double avgR = mean[2], avgG = mean[1], avgB = mean[0]; // BGR Reihenfolge

        double scale = (avgR + avgG + avgB) / 3.0;
        if (scale < 1e-6) scale = 1e-6;

        double rRatio = avgR / scale;
        double bRatio = avgB / scale;

        var (temp, tint) = RbRatioZuTemperatur(rRatio, bRatio);
        Log.Debug("GrayWorld: R={R:F3} G={G:F3} B={B:F3} → Temp={Temp:F0}K Tint={Tint:F1}",
            avgR, avgG, avgB, temp, tint);
        return new WBErgebnis(temp, tint);
    }

    /// <summary>
    /// Shades-of-Gray-Algorithmus: Minkowski-Norm p
    /// </summary>
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

    /// <summary>
    /// AutoWB: Kombiniert GrayWorld + ShadesOfGray für robustes Ergebnis
    /// </summary>
    public WBErgebnis AutoWb(Mat bild)
    {
        var gw = GrayWorld(bild);
        var sog = ShadesOfGray(bild, 10);

        // Gewichtetes Mittel (ShadesOfGray zu 60%, GrayWorld zu 40%)
        double temp = gw.Temperatur * 0.4 + sog.Temperatur * 0.6;
        double tint = gw.Tint * 0.4 + sog.Tint * 0.6;

        Log.Debug("AutoWB: GW={GwTemp:F0}K, SoG={SogTemp:F0}K → Final={FinalTemp:F0}K",
            gw.Temperatur, sog.Temperatur, temp);
        return new WBErgebnis(temp, tint);
    }

    /// <summary>
    /// Konvertiert R/B Ratio in Farbtemperatur + Tint
    /// Approximation basierend auf Planckscher Kurve
    /// </summary>
    private static (double Temp, double Tint) RbRatioZuTemperatur(double rRatio, double bRatio)
    {
        // Einfache Heuristik: wärmer = mehr Rot, kühler = mehr Blau
        double temp = 5500.0; // Neutralpunkt
        if (rRatio > 1.0) temp -= (rRatio - 1.0) * 3000;
        if (bRatio > 1.0) temp += (bRatio - 1.0) * 3000;
        temp = Math.Clamp(temp, 2000, 15000);

        double tint = (rRatio - bRatio) * 50;
        return (temp, tint);
    }
}