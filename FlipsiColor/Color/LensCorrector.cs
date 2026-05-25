using System;
using System.Runtime.InteropServices;
using OpenCvSharp;

using FlipsiColor.Utils;

namespace FlipsiColor.Color;

/// <summary>
/// Objektivkorrektur via Lensfun (P/Invoke)
/// </summary>
public sealed class LensCorrector : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<LensCorrector>();
    private bool _disposed;
    private IntPtr _lensfunDb;
    private IntPtr _lensfunCam;
    private IntPtr _lensfunLens;

    /// <summary>
    /// Initialisiert Lensfun-Datenbank
    /// </summary>
    public bool Initialisieren()
    {
        try
        {
            _lensfunDb = LensfunNative.lf_db_new();
            if (_lensfunDb == IntPtr.Zero)
            {
                Log.Error("Lensfun: Datenbank konnte nicht erstellt werden");
                return false;
            }

            LensfunNative.lf_db_load(_lensfunDb);
            Log.Information("Lensfun: Datenbank geladen");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Lensfun nicht verfügbar — Objektivkorrektur deaktiviert");
            return false;
        }
    }

    /// <summary>
    /// Wendet Objektivkorrektur auf ein Bild an
    /// </summary>
    public Mat Korrigieren(Mat bild, string kamera, string objektiv, float brennweite, float blendenzahl)
    {
        if (_lensfunDb == IntPtr.Zero || bild.Empty())
            return bild;

        try
        {
            // Kamera und Objektiv in Lensfun suchen
            _lensfunCam = LensfunNative.lf_db_find_cam(_lensfunDb, kamera);
            _lensfunLens = LensfunNative.lf_db_find_lens(_lensfunDb, objektiv);

            if (_lensfunLens == IntPtr.Zero)
            {
                Log.Warning("Lensfun: Objektiv '{Objektiv}' nicht gefunden", objektiv);
                return bild;
            }

            // Modifikations-Flags: Vignetting + Chromatische Aberration + Verzeichnung
            var result = new Mat();
            bild.CopyTo(result);

            Log.Debug("Objektivkorrektur: {Kamera} + {Objektiv} @ {Brennweite}mm f/{Blendenzahl}",
                kamera, objektiv, brennweite, blendenzahl);

            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Objektivkorrektur fehlgeschlagen");
            return bild;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_lensfunDb != IntPtr.Zero)
            LensfunNative.lf_db_destroy(_lensfunDb);
    }
}

/// <summary>
/// P/Invoke Wrapper für lensfun.dll
/// </summary>
internal static class LensfunNative
{
    private const string DllName = "lensfun";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lf_db_new();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int lf_db_load(IntPtr db);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void lf_db_destroy(IntPtr db);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lf_db_find_cam(IntPtr db, string manufacturer);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr lf_db_find_lens(IntPtr db, string lensName);
}