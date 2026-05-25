using System;
using System.IO;
using OpenCvSharp;

using FlipsiColor.Utils;

namespace FlipsiColor.Image;

/// <summary>
/// RAW-Decoder via LibRaw.Native — unterstützt CR2, CR3, NEF, ARW, DNG, ORF, RW2
/// </summary>
public static class RawDecoder
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<RawDecoder>();

    /// <summary>
    /// Dekodiert eine RAW-Datei und gibt ein OpenCV Mat zurück
    /// </summary>
    public static Mat Decode(string pfad)
    {
        if (!File.Exists(pfad))
        {
            Log.Error("RAW-Datei nicht gefunden: {Pfad}", pfad);
            return new Mat();
        }

        try
        {
            // LibRaw.Native: Process via P/Invoke
            var processor = new LibRawProcessor();
            if (!processor.OpenFile(pfad))
            {
                Log.Error("LibRaw konnte Datei nicht öffnen: {Pfad}", pfad);
                return new Mat();
            }

            var (width, height) = processor.getImageSize();
            var imageData = processor.Unpack();

            if (imageData == null || imageData.Length == 0)
            {
                Log.Error("LibRaw: Keine Bilddaten für {Pfad}", pfad);
                return new Mat();
            }

            // Konvertiere zu BGR (OpenCV Format)
            var bgrData = processor.DcrawProcess();
            if (bgrData == null)
            {
                Log.Error("LibRaw: dcraw_process fehlgeschlagen für {Pfad}", pfad);
                return new Mat();
            }

            var mat = Mat.FromPixelData(height, width, MatType.CV_8UC3, bgrData);
            Log.Information("RAW dekodiert: {Pfad} ({W}x{H})", pfad, width, height);
            return mat;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RAW-Dekodierung fehlgeschlagen: {Pfad}", pfad);
            // Fallback: OpenCV probieren
            try
            {
                return Cv2.ImRead(pfad, ImReadModes.Color);
            }
            catch
            {
                return new Mat();
            }
        }
    }
}

/// <summary>
/// LibRaw P/Invoke Wrapper
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
            Log.Warning("LibRaw open_file fehlgeschlagen: {Code}", result);
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
        return Array.Empty<byte>(); // TODO: Rohdaten lesen
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
        System.Runtime.InteropServices.Marshal.Copy(dataPtr, bytes, 0, size);
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

internal static class LibRawNative
{
    private const string DllName = "libraw";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr libraw_init(int options);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void libraw_close(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_open_file(IntPtr handle, string filename);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_unpack(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_dcraw_process(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_get_raw_width(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_get_raw_height(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_get_processed_width(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int libraw_get_processed_height(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr libraw_dcraw_ppm_tiff_writer_mem(IntPtr handle);
}