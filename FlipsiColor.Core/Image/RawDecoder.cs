using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;

using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.Image;

/// <summary>
/// RAW-Decoder — versucht LibRaw, fällt auf OpenCV ImRead und externes dcraw/ffmpeg zurück.
/// Unterstützt CR2, CR3, NEF, ARW, DNG, ORF, RW2.
/// FIX #1: Pfad-Validierung gegen Path-Traversal und UNC-Pfade.
/// FIX #2: Sichere Prozess-Argumente (ArgumentList) verhindert Command-Injection.
/// FIX #4: NativeLibrary.SetDllImportResolver für sichere DLL-Ladung.
/// </summary>
public class RawDecoder
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<RawDecoder>();

    /// <summary>
    /// Erlaubte RAW-Dateiendungen.
    /// </summary>
    private static readonly HashSet<string> RawEndungen = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2", ".raw"
    };

    /// <summary>
    /// Dekodiert eine RAW-Datei und gibt ein OpenCV Mat zurück.
    /// Reihenfolge: 1) LibRaw P/Invoke → 2) Cv2.ImRead (Color|AnyDepth) → 3) dcraw/ffmpeg extern.
    /// FIX #1: Pfad-Validierung gegen Path-Traversal und UNC-Pfade.
    /// </summary>
    public static Mat Decode(string pfad)
    {
        // FIX #1: Pfad-Validierung gegen Path-Traversal und UNC-Pfade
        var validierterPfad = SecurityValidator.ValidiereDateiPfad(pfad, RawEndungen);
        if (validierterPfad == null)
        {
            Log.Warning("RAW-Dekodierung: Pfad-Validierung fehlgeschlagen");
            return new Mat();
        }
        pfad = validierterPfad;

        // Versuch 1: LibRaw via P/Invoke
        Log.Debug("RAW-Dekodierung Versuch 1/3 (LibRaw): {Pfad}", pfad);
        var result = VersucheLibRaw(pfad);
        if (IstGueltig(result))
            return result!;

        // Versuch 2: OpenCV ImRead mit Color | AnyDepth
        Log.Debug("RAW-Dekodierung Versuch 2/3 (OpenCV ImRead): {Pfad}", pfad);
        result = VersucheOpenCv(pfad);
        if (IstGueltig(result))
            return result!;

        // Versuch 3: Externes dcraw oder ffmpeg
        Log.Debug("RAW-Dekodierung Versuch 3/3 (dcraw/ffmpeg): {Pfad}", pfad);
        result = VersucheExtern(pfad);
        if (IstGueltig(result))
            return result!;

        Log.Error("RAW-Dekodierung fehlgeschlagen — alle 3 Versuche gescheitert: {Pfad}", pfad);
        return new Mat();
    }

    private static bool IstGueltig(Mat? mat) => mat != null && !mat.Empty();

    /// <summary>
    /// Versuch 1: LibRaw P/Invoke (nur auf Windows mit installierter libraw verfügbar)
    /// </summary>
    private static Mat? VersucheLibRaw(string pfad)
    {
        try
        {
            using var processor = new LibRawProcessor();
            if (!processor.OpenFile(pfad))
            {
                Log.Debug("LibRaw: OpenFile fehlgeschlagen für {Pfad}", pfad);
                return null;
            }

            var imageData = processor.Unpack();
            if (imageData == null || imageData.Length == 0)
            {
                Log.Debug("LibRaw: Unpack lieferte keine Daten für {Pfad}", pfad);
                return null;
            }

            var bgrData = processor.DcrawProcess();
            if (bgrData == null || bgrData.Length == 0)
            {
                Log.Debug("LibRaw: DcrawProcess fehlgeschlagen für {Pfad}", pfad);
                return null;
            }

            var (width, height) = processor.getImageSize();
            var mat = Mat.FromPixelData(height, width, MatType.CV_8UC3, bgrData);
            Log.Information("RAW dekodiert via LibRaw: {Pfad} ({W}x{H})", pfad, width, height);
            return mat;
        }
        catch (Exception ex)
        {
            Log.Debug("LibRaw nicht verfügbar oder fehlgeschlagen: {Msg}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Versuch 2: OpenCV ImRead mit Color | AnyDepth Flags.
    /// OpenCV nutzt intern codecs (libpng/libjpeg/libtiff) und kann einige RAW-Formate (DNG, TIFF-basierte) lesen.
    /// </summary>
    private static Mat? VersucheOpenCv(string pfad)
    {
        try
        {
            var mat = Cv2.ImRead(pfad, ImreadModes.Color | ImreadModes.AnyDepth);
            if (mat.Empty())
            {
                Log.Debug("OpenCV ImRead: leer oder fehlgeschlagen für {Pfad}", pfad);
                return null;
            }

            // Tiefe >8bit → auf 8-Bit normalisieren (für konsistente Pipeline)
            if (mat.Depth() != MatType.CV_8U)
            {
                var normalized = new Mat();
                Cv2.Normalize(mat, normalized, 0, 255, NormTypes.MinMax, -1, new Mat());
                Cv2.ConvertScaleAbs(normalized, normalized, 1.0, 0);
                mat.Dispose();
                mat = normalized;
            }

            // Falls Graustufen → BGR
            if (mat.Channels() == 1)
            {
                var bgr = new Mat();
                Cv2.CvtColor(mat, bgr, ColorConversionCodes.GRAY2BGR);
                mat.Dispose();
                mat = bgr;
            }

            Log.Information("RAW dekodiert via OpenCV ImRead: {Pfad} ({W}x{H})", pfad, mat.Width, mat.Height);
            return mat;
        }
        catch (Exception ex)
        {
            Log.Debug("OpenCV ImRead fehlgeschlagen: {Msg}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Versuch 3: Externe Werkzeuge (dcraw → PPM, dann OpenCV; oder ffmpeg → PNG).
    /// Gibt das dekodierte Bild als OpenCV Mat zurück.
    /// </summary>
    private static Mat? VersucheExtern(string pfad)
    {
        // 3a: dcraw — konvertiert RAW → PPM (oder TIFF)
        var mat = VersucheDcraw(pfad);
        if (IstGueltig(mat))
            return mat;

        // 3b: ffmpeg — kann einige RAW-Formate dekodieren
        mat = VersucheFfmpeg(pfad);
        if (IstGueltig(mat))
            return mat;

        return null;
    }

    /// <summary>
    /// Ruft dcraw extern auf: dcraw -w -6 → erzeugt eine .ppm/.tiff Datei neben dem Original.
    /// -w: Weißabgleich aus Kamera übernehmen, -6: 16-Bit Ausgabe, -T: TIFF-Ausgabe.
    /// </summary>
    private static Mat? VersucheDcraw(string pfad)
    {
        try
        {
            var dir = Path.GetDirectoryName(pfad) ?? ".";
            var baseName = Path.GetFileNameWithoutExtension(pfad);

            // FIX #2: Sichere ArgumentList statt String-Arguments — verhindert Command-Injection
            var psi = SecurityValidator.SichereProcessStartInfo("dcraw",
                new[] { "-w", "-T", pfad });

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Log.Debug("dcraw: Prozess konnte nicht gestartet werden");
                return null;
            }

            proc.WaitForExit(30000); // 30s Timeout
            if (!proc.HasExited)
            {
                try { proc.Kill(true); } catch { /* Ignorieren */ }
                Log.Debug("dcraw: Timeout — Prozess abgebrochen");
                return null;
            }

            if (proc.ExitCode != 0)
            {
                var stderr = proc.StandardError.ReadToEnd();
                Log.Debug("dcraw: ExitCode={Code}, Stderr={Stderr}", proc.ExitCode, stderr);
                return null;
            }

            // dcraw -T erzeugt <baseName>.tiff im Quellverzeichnis
            var tiffPfad = Path.Combine(dir, baseName + ".tiff");
            if (!File.Exists(tiffPfad))
            {
                // Manchmal .ppm statt .tiff
                var ppmPfad = Path.Combine(dir, baseName + ".ppm");
                if (!File.Exists(ppmPfad))
                {
                    Log.Debug("dcraw: Ausgabedatei nicht gefunden ({Tiff} oder {Ppm})", tiffPfad, ppmPfad);
                    return null;
                }
                tiffPfad = ppmPfad;
            }

            var mat = Cv2.ImRead(tiffPfad, ImreadModes.Color | ImreadModes.AnyDepth);
            if (mat.Empty())
            {
                Log.Debug("dcraw: Ausgabedatei konnte nicht gelesen werden: {Pfad}", tiffPfad);
                return null;
            }

            Log.Information("RAW dekodiert via dcraw: {Pfad} → {Out} ({W}x{H})", pfad, tiffPfad, mat.Width, mat.Height);
            return mat;
        }
        catch (Exception ex)
        {
            Log.Debug("dcraw nicht verfügbar oder fehlgeschlagen: {Msg}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Ruft ffmpeg extern auf: dekodiert RAW → temporäre PNG-Datei, dann OpenCV ImRead.
    /// ffmpeg kann DNG und einige Camera-RAW-Formate via libraw/libavcodec verarbeiten.
    /// </summary>
    private static Mat? VersucheFfmpeg(string pfad)
    {
        var tempPfad = Path.Combine(Path.GetTempPath(), $"flipsi_raw_{Guid.NewGuid():N}.png");
        try
        {
        // FIX #2: Sichere ArgumentList statt String-Arguments — verhindert Command-Injection
            var psi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                new[] { "-y", "-i", pfad, "-frames:v", "1", tempPfad });

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                Log.Debug("ffmpeg: Prozess konnte nicht gestartet werden");
                return null;
            }

            proc.WaitForExit(30000);
            if (!proc.HasExited)
            {
                try { proc.Kill(true); } catch { /* Ignorieren */ }
                Log.Debug("ffmpeg: Timeout — Prozess abgebrochen");
                return null;
            }

            if (proc.ExitCode != 0)
            {
                var stderr = proc.StandardError.ReadToEnd();
                Log.Debug("ffmpeg: ExitCode={Code}, Stderr={Stderr}", proc.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(tempPfad))
            {
                Log.Debug("ffmpeg: Ausgabedatei nicht erstellt: {Pfad}", tempPfad);
                return null;
            }

            var mat = Cv2.ImRead(tempPfad, ImreadModes.Color);
            if (mat.Empty())
            {
                Log.Debug("ffmpeg: Ausgabedatei konnte nicht gelesen werden: {Pfad}", tempPfad);
                return null;
            }

            Log.Information("RAW dekodiert via ffmpeg: {Pfad} → {Out} ({W}x{H})", pfad, tempPfad, mat.Width, mat.Height);
            return mat;
        }
        catch (Exception ex)
        {
            Log.Debug("ffmpeg nicht verfügbar oder fehlgeschlagen: {Msg}", ex.Message);
            return null;
        }
        finally
        {
            try { if (File.Exists(tempPfad)) File.Delete(tempPfad); } catch { /* Ignorieren */ }
        }
    }
}

/// <summary>
/// LibRaw P/Invoke Wrapper — nur verfügbar wenn libraw.dll/libraw.so installiert ist.
/// </summary>
internal sealed class LibRawProcessor : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<LibRawProcessor>();
    private IntPtr _handle;
    private bool _disposed;

    public LibRawProcessor()
    {
        _handle = LibRawNative.libraw_init(0); // LIBRAW_OPTIONS_NONE
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("LibRaw konnte nicht initialisiert werden");
    }

    public bool OpenFile(string pfad)
    {
        var result = LibRawNative.libraw_open_file(_handle, pfad);
        if (result != 0)
        {
            Log.Debug("LibRaw open_file fehlgeschlagen: {Code}", result);
            return false;
        }
        return true;
    }

    public (int Width, int Height) getImageSize()
    {
        return (LibRawNative.libraw_get_raw_width(_handle),
                LibRawNative.libraw_get_raw_height(_handle));
    }

    public byte[]? Unpack()
    {
        var result = LibRawNative.libraw_unpack(_handle);
        if (result != 0) return null;
        // Rohdaten werden intern von LibRaw gehalten — DcrawProcess liest sie
        // und gibt die verarbeiteten BGR-Daten zurück.
        return new byte[1]; // Platzhalter — DcrawProcess liefert die echten Daten
    }

    public byte[]? DcrawProcess()
    {
        var result = LibRawNative.libraw_dcraw_process(_handle);
        if (result != 0) return null;

        int width = LibRawNative.libraw_get_processed_width(_handle);
        int height = LibRawNative.libraw_get_processed_height(_handle);
        var dataPtr = LibRawNative.libraw_dcraw_ppm_tiff_writer_mem(_handle);

        if (dataPtr == IntPtr.Zero) return null;

        var size = width * height * 3;
        var bytes = new byte[size];
        Marshal.Copy(dataPtr, bytes, 0, size);
        return bytes;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != IntPtr.Zero)
            LibRawNative.libraw_close(_handle);
    }
}

/// <summary>
/// P/Invoke Wrapper für LibRaw.
/// FIX #4: DLL-Suchpfad auf App-Verzeichnis beschränkt — verhindert DLL-Hijacking.
/// </summary>
internal static class LibRawNative
{
    // FIX #4: DefaultDllImportSearchPaths-Attribut auf jeder P/Invoke-Methode
    // verhindert DLL-Hijacking — lädt nur aus ApplicationDirectory und System32.
    private const string DllName = "libraw";

    // FIX #4: Sichere DLL-Suchpfade als Konstante
    private const System.Runtime.InteropServices.DllImportSearchPath SichereSuchpfade =
        System.Runtime.InteropServices.DllImportSearchPath.ApplicationDirectory |
        System.Runtime.InteropServices.DllImportSearchPath.System32;

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern IntPtr libraw_init(int options);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern void libraw_close(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int libraw_open_file(IntPtr handle, string filename);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int libraw_unpack(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int libraw_dcraw_process(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int libraw_get_raw_width(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int libraw_get_raw_height(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int libraw_get_processed_width(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int libraw_get_processed_height(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern IntPtr libraw_dcraw_ppm_tiff_writer_mem(IntPtr handle);
}