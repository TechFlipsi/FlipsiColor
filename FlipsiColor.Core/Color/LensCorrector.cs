using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

    // CropFactor-Offset innerhalb der lfCamera-Struktur (x64 ABI, C-Side).
    // Layout: lfMLstr Maker (8), lfMLstr Model (8), lfMLstr Variant (8),
    //         char* Mount (8), float CropFactor (4) → Offset 32.
    private const int CropFactorOffset = 32;

    /// <summary>
    /// Statischer Konstruktor — setzt den DllImportResolver, damit die
    /// lensfun-Bibliothek aus dem Installationsordner geladen wird.
    /// Windows: %LOCALAPPDATA%\FlipsiColor\lensfun\liblensfun.dll
    /// Linux:   liblensfun.so über System-Suchpfade.
    /// </summary>
    static LensCorrector()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(
                typeof(LensfunNative).Assembly,
                (libraryName, assembly, searchPath) => ResolveLensfunDll(libraryName, assembly, searchPath));
            Log.Debug("DllImportResolver für Lensfun registriert");
        }
        catch (Exception ex)
        {
            Log.Debug("DllImportResolver konnte nicht gesetzt werden: {Fehler}", ex.Message);
        }
    }

    /// <summary>
    /// Resolver für die lensfun-Bibliothek.
    /// Windows: lädt aus dem Lensfun-Installationsordner (%LOCALAPPDATA%\FlipsiColor\lensfun\).
    /// Linux:   Standard-Suchpfade (liblensfun.so über System).
    /// </summary>
    private static IntPtr ResolveLensfunDll(string libraryName, System.Reflection.Assembly assembly, System.Runtime.InteropServices.DllImportSearchPath? searchPath)
    {
        if (libraryName != "lensfun")
            return IntPtr.Zero;

        // Windows: DLL aus dem Installationsordner laden
        if (OperatingSystem.IsWindows())
        {
            string installPfad = LensfunInstallerPfad;
            if (File.Exists(installPfad))
            {
                // DLL mit absolutem Pfad laden — Windows findet Abhängigkeiten
                // (libgcc, libglib, etc.) automatisch im selben Verzeichnis.
                if (NativeLibrary.TryLoad(installPfad, assembly, DllImportSearchPath.ApplicationDirectory, out IntPtr handle))
                {
                    Log.Debug("Lensfun-DLL geladen aus: {Pfad}", installPfad);
                    return handle;
                }
            }
            Log.Debug("Lensfun-DLL nicht im Installationsordner gefunden: {Pfad}", installPfad);
        }

        // Fallback: Standard-Suchpfade (Linux: liblensfun.so im System)
        if (searchPath.HasValue && NativeLibrary.TryLoad(libraryName, assembly, searchPath.Value, out IntPtr fallback))
            return fallback;

        // Linux-Alternative: lib-Präfix
        if (!OperatingSystem.IsWindows())
        {
            if (NativeLibrary.TryLoad("liblensfun", assembly, DllImportSearchPath.System32, out IntPtr linuxHandle))
                return linuxHandle;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Absoluter Pfad zur liblensfun.dll im Lensfun-Installationsordner (Windows).
    /// </summary>
    private static string LensfunInstallerPfad =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlipsiColor", "lensfun", "liblensfun.dll");

    /// <summary>
    /// Absoluter Pfad zur lensfun-db-Datenbank im Installationsordner (Windows).
    /// </summary>
    private static string LensfunDatenbankPfad =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlipsiColor", "lensfun", "lensfun-db");

    /// <summary>
    /// Initialisiert Lensfun-Datenbank.
    /// Windows: lädt die Datenbank aus dem Installationsordner (lensfun-db/).
    /// Linux:   lädt die Datenbank aus den Systempfaden.
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

            // Auf Windows: Datenbank aus dem Installationsordner laden
            if (OperatingSystem.IsWindows())
            {
                string dbPfad = LensfunDatenbankPfad;
                if (Directory.Exists(dbPfad))
                {
                    int result = LensfunNative.lf_db_load_path(_lensfunDb, dbPfad);
                    Log.Information("Lensfun: Datenbank geladen aus Installationsordner ({Result})", result);
                }
                else
                {
                    // Fallback: Standard-Suchpfade
                    LensfunNative.lf_db_load(_lensfunDb);
                    Log.Information("Lensfun: Datenbank über Standardpfade geladen (Installationsordner nicht gefunden)");
                }
            }
            else
            {
                // Linux: Standard-Suchpfade
                LensfunNative.lf_db_load(_lensfunDb);
                Log.Information("Lensfun: Datenbank geladen (Linux Standardpfade)");
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Lensfun nicht verfügbar — Objektivkorrektur deaktiviert");
            return false;
        }
    }

    /// <summary>
    /// Gibt alle Kamera-Hersteller (Maker) aus der Lensfun-Datenbank zurück.
    /// Die Liste ist dedupliziert und alphabetisch sortiert.
    /// </summary>
    /// <returns>Liste der Kamera-Hersteller-Namen.</returns>
    public List<string> ListeKameras()
    {
        var hersteller = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_lensfunDb == IntPtr.Zero)
        {
            Log.Warning("Lensfun: Datenbank nicht initialisiert — ListeKameras leer");
            return new List<string>();
        }

        try
        {
            IntPtr camsArray = LensfunNative.lf_db_get_cams(_lensfunDb);
            if (camsArray == IntPtr.Zero)
                return new List<string>();

            int offset = 0;
            while (true)
            {
                IntPtr camPtr = Marshal.ReadIntPtr(camsArray, offset);
                if (camPtr == IntPtr.Zero)
                    break;

                // Maker ist das erste Feld (Offset 0) in lfCamera — ein lfMLstr (char*)
                IntPtr makerPtr = Marshal.ReadIntPtr(camPtr, 0);
                if (makerPtr != IntPtr.Zero)
                {
                    string? maker = Marshal.PtrToStringAnsi(makerPtr);
                    if (!string.IsNullOrWhiteSpace(maker))
                        hersteller.Add(maker);
                }

                offset += IntPtr.Size;
            }

            var result = new List<string>(hersteller);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            Log.Debug("Lensfun: {Anzahl} Kamera-Hersteller gefunden", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Lensfun: ListeKameras fehlgeschlagen");
            return new List<string>();
        }
    }

    /// <summary>
    /// Gibt alle Objektiv-Modelle aus der Lensfun-Datenbank zurück.
    /// Auf Windows werden die Objektive aus dem Installationsordner geladen.
    /// Die Liste ist dedupliziert und alphabetisch sortiert.
    /// </summary>
    /// <param name="kamera">Kamera-Hersteller (wird für zukünftige Mount-Filterung reserviert,
    /// aktuell werden alle Objektive zurückgegeben).</param>
    /// <returns>Liste der Objektiv-Bezeichnungen.</returns>
    public List<string> ListeObjektive(string kamera)
    {
        var objektive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_lensfunDb == IntPtr.Zero)
        {
            Log.Warning("Lensfun: Datenbank nicht initialisiert — ListeObjektive leer");
            return new List<string>();
        }

        try
        {
            IntPtr lensesArray = LensfunNative.lf_db_get_lenses(_lensfunDb);
            if (lensesArray == IntPtr.Zero)
                return new List<string>();

            int offset = 0;
            while (true)
            {
                IntPtr lensPtr = Marshal.ReadIntPtr(lensesArray, offset);
                if (lensPtr == IntPtr.Zero)
                    break;

                // Model ist das zweite Feld (Offset 8) in lfLens — ein lfMLstr (char*)
                IntPtr modelPtr = Marshal.ReadIntPtr(lensPtr, 8);
                if (modelPtr != IntPtr.Zero)
                {
                    string? model = Marshal.PtrToStringAnsi(modelPtr);
                    if (!string.IsNullOrWhiteSpace(model))
                        objektive.Add(model);
                }

                offset += IntPtr.Size;
            }

            var result = new List<string>(objektive);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            Log.Debug("Lensfun: {Anzahl} Objektive gefunden (für Kamera={Kamera})",
                result.Count, kamera ?? "alle");
            return result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Lensfun: ListeObjektive fehlgeschlagen");
            return new List<string>();
        }
    }

    /// <summary>
    /// Wendet Objektivkorrektur auf ein Bild an.
    /// Korrigiert Verzeichung (Distortion), chromatische Aberration (TCA)
    /// und Vignetting über die Lensfun-Bibliothek.
    /// </summary>
    /// <param name="bild">Eingabebild als OpenCvSharp Mat (BGR oder BGRA, 8-bit).</param>
    /// <param name="kamera">Kamera-Hersteller laut EXIF.</param>
    /// <param name="objektiv">Objektiv-Bezeichnung laut EXIF.</param>
    /// <param name="brennweite">Brennweite in mm.</param>
    /// <param name="blendenzahl">Blendenwert (f-Nummer).</param>
    /// <returns>Korrigiertes Bild; bei Fehlern das unveränderte Original.</returns>
    public Mat Korrigieren(Mat bild, string kamera, string objektiv, float brennweite, float blendenzahl)
    {
        if (_lensfunDb == IntPtr.Zero || bild.Empty())
            return bild;

        IntPtr modifier = IntPtr.Zero;

        try
        {
            // Kamera und Objektiv in Lensfun suchen
            IntPtr camHandle = LensfunNative.lf_db_find_cam(_lensfunDb, kamera);
            IntPtr lensHandle = LensfunNative.lf_db_find_lens(_lensfunDb, objektiv);

            if (camHandle == IntPtr.Zero)
            {
                Log.Warning("Lensfun: Kamera '{Kamera}' nicht gefunden", kamera);
                return bild;
            }

            if (lensHandle == IntPtr.Zero)
            {
                Log.Warning("Lensfun: Objektiv '{Objektiv}' nicht gefunden", objektiv);
                return bild;
            }

            int breite = bild.Width;
            int hoehe = bild.Height;
            int kanaele = bild.Channels();
            if (kanaele < 3)
            {
                Log.Warning("Lensfun: Bild muss mindestens 3 Kanäle haben (hat {Kanaele})", kanaele);
                return bild;
            }

            // Crop-Faktor der Kamera aus der lfCamera-Struktur auslesen
            float cropFaktor = LeseCropFaktor(camHandle);

            // Modifikations-Flags: TCA + Vignetting + Verzeichnung (alle gleichzeitig)
            const int flags = LensfunNative.LF_MODIFY_TCA
                              | LensfunNative.LF_MODIFY_VIGNETTING
                              | LensfunNative.LF_MODIFY_DISTORTION;

            // Modifier erstellen und initialisieren
            modifier = LensfunNative.lf_modifier_new(lensHandle, cropFaktor, breite, hoehe);
            if (modifier == IntPtr.Zero)
            {
                Log.Warning("Lensfun: Modifier konnte nicht erstellt werden");
                return bild;
            }

            int aktivFlags = LensfunNative.lf_modifier_initialize(
                modifier, lensHandle, LensfunNative.LF_PF_U8,
                brennweite, blendenzahl, 1.0f, 1.0f,
                LensfunNative.LF_RECTILINEAR, flags, 0);

            if (aktivFlags == 0)
            {
                Log.Warning("Lensfun: Modifier-Initialisierung lieferte keine Korrektur-Callbacks " +
                            "(Brennweite {Brennweite}mm außerhalb des Kalibrierbereichs?)", brennweite);
                return bild;
            }

            Log.Debug("Lensfun: Modifier initialisiert — {Brennweite}mm f/{Blende}, " +
                      "angeforderte Flags=0x{Flags:X}, aktive Flags=0x{Aktiv:X}",
                brennweite, blendenzahl, flags, aktivFlags);

            // --- Phase 1: Subpixel-Geometry-Distortion (Verzeichnung + TCA) ---
            // Für jeden Pixel liefert Lensfun die Quellkoordinaten je Kanal (R, G, B).
            // Wir remappen das Eingabebild entsprechend mit OpenCV-Interpolation.
            Mat verzerrt = SubpixelGeometryDistortionAnwenden(bild, modifier, breite, hoehe, kanaele);

            // --- Phase 2: Color Modification (Vignetting) ---
            // Lensfun modifiziert die Pixelwerte direkt im Speicher.
            ColorModificationAnwenden(verzerrt, modifier, breite, hoehe, kanaele);

            Log.Information("Objektivkorrektur angewendet: {Kamera} + {Objektiv} " +
                            "@ {Brennweite}mm f/{Blendenzahl}",
                kamera, objektiv, brennweite, blendenzahl);

            return verzerrt;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Objektivkorrektur fehlgeschlagen");
            return bild;
        }
        finally
        {
            if (modifier != IntPtr.Zero)
                LensfunNative.lf_modifier_destroy(modifier);
        }
    }

    /// <summary>
    /// Liest den CropFactor aus der lfCamera-Struktur.
    /// Fällt auf 1.0 zurück, wenn der Wert ungültig ist.
    /// </summary>
    private static float LeseCropFaktor(IntPtr camHandle)
    {
        try
        {
            float crop = Marshal.PtrToStructure<float>(camHandle + CropFactorOffset);
            return crop > 0f ? crop : 1f;
        }
        catch
        {
            // Wenn das Struktur-Layout nicht passt, sicherer Default
            return 1f;
        }
    }

    /// <summary>
    /// Wendet die Subpixel-Geometry-Distortion an (Verzeichnung + chromatische Aberration).
    /// Lensfun liefert für jeden Ausgabepixel die Quellkoordinaten je Kanal;
    /// wir nutzen OpenCV-Remap für die tatsächliche Interpolation.
    /// </summary>
    private static Mat SubpixelGeometryDistortionAnwenden(
        Mat bild, IntPtr modifier, int breite, int hoehe, int kanaele)
    {
        // Lensfun erwartet ein float-Array der Größe width*height*6 (xR,yR, xG,yG, xB,yB).
        int elemente = breite * hoehe * 6;
        IntPtr distBuffer = Marshal.AllocHGlobal(elemente * sizeof(float));
        if (distBuffer == IntPtr.Zero)
            throw new OutOfMemoryException("Lensfun: Distortion-Puffer konnte nicht allokiert werden");

        Mat mapXRot = new Mat(hoehe, breite, MatType.CV_32FC1);
        Mat mapYRot = new Mat(hoehe, breite, MatType.CV_32FC1);
        Mat mapXGruen = new Mat(hoehe, breite, MatType.CV_32FC1);
        Mat mapYGruen = new Mat(hoehe, breite, MatType.CV_32FC1);
        Mat mapXBlau = new Mat(hoehe, breite, MatType.CV_32FC1);
        Mat mapYBlau = new Mat(hoehe, breite, MatType.CV_32FC1);

        Mat ergebnis = new Mat();

        try
        {
            bool ok = LensfunNative.lf_modifier_apply_subpixel_geometry_distortion(
                modifier, 0f, 0f, breite, hoehe, distBuffer);
            if (!ok)
            {
                Log.Warning("Lensfun: apply_subpixel_geometry_distortion fehlgeschlagen — " +
                            "keine Verzeichnung/CA-Korrektur angewendet");
                bild.CopyTo(ergebnis);
                return ergebnis;
            }

            // Distortion-Puffer in .NET-Array kopieren
            float[] daten = new float[elemente];
            Marshal.Copy(distBuffer, daten, 0, elemente);

            // Lensfun liefert interleaved: xR,yR, xG,yG, xB,yB pro Pixel.
            // Für OpenCV Remap brauchen wir 6 separate MapX/MapY-Mats (float32).
            // Wir deinterleaven in separate Arrays und nutzen Mat.SetArray.
            int pixelCount = breite * hoehe;
            float[] mapXRotDaten = new float[pixelCount];
            float[] mapYRotDaten = new float[pixelCount];
            float[] mapXGruenDaten = new float[pixelCount];
            float[] mapYGruenDaten = new float[pixelCount];
            float[] mapXBlauDaten = new float[pixelCount];
            float[] mapYBlauDaten = new float[pixelCount];

            for (int i = 0; i < pixelCount; i++)
            {
                int idx = i * 6;
                mapXRotDaten[i] = daten[idx];
                mapYRotDaten[i] = daten[idx + 1];
                mapXGruenDaten[i] = daten[idx + 2];
                mapYGruenDaten[i] = daten[idx + 3];
                mapXBlauDaten[i] = daten[idx + 4];
                mapYBlauDaten[i] = daten[idx + 5];
            }

            mapXRot.SetArray(mapXRotDaten);
            mapYRot.SetArray(mapYRotDaten);
            mapXGruen.SetArray(mapXGruenDaten);
            mapYGruen.SetArray(mapYGruenDaten);
            mapXBlau.SetArray(mapXBlauDaten);
            mapYBlau.SetArray(mapYBlauDaten);

            // Eingabebild in Kanäle splitten
            Mat[] kanalRoh = Cv2.Split(bild);

            // Je Kanal remappen (Interpolation bicubic für gute Qualität)
            Mat[] kanalKorrigiert = new Mat[kanaele];
            for (int i = 0; i < kanaele; i++)
                kanalKorrigiert[i] = new Mat();

            // OpenCV BGR-Reihenfolge: Kanal 0 = Blau, 1 = Grün, 2 = Rot
            Cv2.Remap(kanalRoh[0], kanalKorrigiert[0], mapXBlau, mapYBlau,
                InterpolationFlags.Cubic, BorderTypes.Reflect);
            Cv2.Remap(kanalRoh[1], kanalKorrigiert[1], mapXGruen, mapYGruen,
                InterpolationFlags.Cubic, BorderTypes.Reflect);
            Cv2.Remap(kanalRoh[2], kanalKorrigiert[2], mapXRot, mapYRot,
                InterpolationFlags.Cubic, BorderTypes.Reflect);

            // Alpha-Kanal (falls vorhanden) unverändert übernehmen
            if (kanaele >= 4)
                kanalRoh[3].CopyTo(kanalKorrigiert[3]);

            // Kanäle wieder zusammenführen (Merge kopiert die Daten)
            Cv2.Merge(kanalKorrigiert, ergebnis);

            // Hilfs-Mats freigeben
            foreach (var k in kanalRoh)
                k.Dispose();
            foreach (var k in kanalKorrigiert)
                k.Dispose();

            return ergebnis;
        }
        finally
        {
            Marshal.FreeHGlobal(distBuffer);
            mapXRot.Dispose();
            mapYRot.Dispose();
            mapXGruen.Dispose();
            mapYGruen.Dispose();
            mapXBlau.Dispose();
            mapYBlau.Dispose();
        }
    }

    /// <summary>
    /// Wendet die Color-Modification (Vignetting-Korrektur) direkt auf die Pixeldaten an.
    /// Lensfun modifiziert die Werte in-place; wir übergeben den Roh-Speicher des Mat.
    /// </summary>
    private static void ColorModificationAnwenden(
        Mat bild, IntPtr modifier, int breite, int hoehe, int kanaele)
    {
        // comp_role kodiert die Pixel-Komponenten-Reihenfolge (LF_CR_*).
        // LF_CR_UNKNOWN=2, LF_CR_INTENSITY=3, LF_CR_RED=4, LF_CR_GREEN=5, LF_CR_BLUE=6
        // (Werte aus enum lfComponentRole im lensfun.h-Header)
        const int lfCrUnknown = 2;
        const int lfCrRed = 4;
        const int lfCrGreen = 5;
        const int lfCrBlue = 6;

        // OpenCV BGR-Reihenfolge: Kanal 0=Blau, 1=Grün, 2=Rot
        int compRole = lfCrBlue | (lfCrGreen << 4) | (lfCrRed << 8);
        if (kanaele == 4)
            compRole |= lfCrUnknown << 12;

        int rowStride = (int)bild.Step();

        // Lensfun arbeitet direkt auf dem Mat-Speicher (in-place)
        bool ok = LensfunNative.lf_modifier_apply_color_modification(
            modifier, bild.Data, 0f, 0f, breite, hoehe, compRole, rowStride);

        if (!ok)
            Log.Warning("Lensfun: apply_color_modification fehlgeschlagen — " +
                        "keine Vignetting-Korrektur angewendet");
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
/// P/Invoke Wrapper für lensfun.dll.
/// FIX #4: DllImport mit SearchPath-Beschränkung — lädt nur aus dem App-Verzeichnis,
/// nicht aus dem aktuellen Arbeitsverzeichnis oder PATH (verhindert DLL-Hijacking).
/// Auf Windows wird die DLL über den DllImportResolver (siehe LensCorrector static ctor)
/// aus dem Lensfun-Installationsordner geladen.
/// </summary>
internal static class LensfunNative
{
    private const string DllName = "lensfun";

    // FIX #4: Sichere DLL-Suchpfade als Konstante
    private const System.Runtime.InteropServices.DllImportSearchPath SichereSuchpfade =
        System.Runtime.InteropServices.DllImportSearchPath.ApplicationDirectory |
        System.Runtime.InteropServices.DllImportSearchPath.System32;

    // --- Modifikations-Flags (lfModifier) ---
    public const int LF_MODIFY_TCA = 0x00000001;
    public const int LF_MODIFY_VIGNETTING = 0x00000002;
    public const int LF_MODIFY_DISTORTION = 0x00000008;
    public const int LF_MODIFY_GEOMETRY = 0x00000010;
    public const int LF_MODIFY_SCALE = 0x00000020;
    public const int LF_MODIFY_ALL = -1;

    // --- Pixel-Formate (lfPixelFormat) ---
    public const int LF_PF_U8 = 0;
    public const int LF_PF_U16 = 1;
    public const int LF_PF_U32 = 2;
    public const int LF_PF_F32 = 3;
    public const int LF_PF_F64 = 4;

    // --- Objektivtypen (lfLensType) ---
    public const int LF_UNKNOWN = 0;
    public const int LF_RECTILINEAR = 1;
    public const int LF_FISHEYE = 2;
    public const int LF_PANORAMIC = 3;
    public const int LF_EQUIRECTANGULAR = 4;
    public const int LF_FISHEYE_ORTHOGRAPHIC = 5;
    public const int LF_FISHEYE_STEREOGRAPHIC = 6;
    public const int LF_FISHEYE_THOBY = 7;

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern IntPtr lf_db_new();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int lf_db_load(IntPtr db);

    /// <summary>
    /// Lädt die Lensfun-Datenbank aus einem bestimmten Verzeichnis.
    /// Auf Windows wird der Pfad zum lensfun-db/ Ordner im Installationsverzeichnis übergeben.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int lf_db_load_path(IntPtr db, string path);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern void lf_db_destroy(IntPtr db);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern IntPtr lf_db_find_cam(IntPtr db, string manufacturer);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern IntPtr lf_db_find_lens(IntPtr db, string lensName);

    /// <summary>
    /// Gibt alle Kameras in der Datenbank zurück.
    /// Liefert ein null-terminiertes Array von lfCamera* (lfCamera**).
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern IntPtr lf_db_get_cams(IntPtr db);

    /// <summary>
    /// Gibt alle Objektive in der Datenbank zurück.
    /// Liefert ein null-terminiertes Array von lfLens* (lfLens**).
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern IntPtr lf_db_get_lenses(IntPtr db);

    /// <summary>
    /// Erstellt einen neuen Lensfun-Modifier für das gegebene Objektiv.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern IntPtr lf_modifier_new(
        IntPtr lens, float crop, int width, int height);

    /// <summary>
    /// Initialisiert den Modifier mit den Korrektur-Parametern.
    /// Liefert eine Bitmaske der tatsächlich aktivierten Korrekturen (0 = keine).
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern int lf_modifier_initialize(
        IntPtr modifier, IntPtr lens, int format,
        float focal, float aperture, float distance, float scale,
        int targeom, int flags, int reverse);

    /// <summary>
    /// Berechnet für jeden Ausgabepixel die Quellkoordinaten je Subpixel-Kanal
    /// (R, G, B). Korrigiert Verzeichnung und chromatische Aberration gleichzeitig.
    /// res muss ein float-Array der Größe width*height*6 sein.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool lf_modifier_apply_subpixel_geometry_distortion(
        IntPtr modifier, float xu, float yu, int width, int height, IntPtr res);

    /// <summary>
    /// Modifiziert die Pixeldaten in-place (Vignetting-Korrektur).
    /// comp_role beschreibt die Pixel-Komponenten-Reihenfolge (LF_CR_*).
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool lf_modifier_apply_color_modification(
        IntPtr modifier, IntPtr pixels, float x, float y,
        int width, int height, int compRole, int rowStride);

    /// <summary>
    /// Gibt den Modifier und alle zugehörigen Ressourcen frei.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(SichereSuchpfade)]
    public static extern void lf_modifier_destroy(IntPtr modifier);
}