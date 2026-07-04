using System;

using FlipsiColor.Utils;

namespace FlipsiColor;

/// <summary>
/// GPU-Erkennung — ONNX Runtime Provider-Check (DirectML auf Windows, CUDA Fallback).
/// Auf Linux/macOS wird CPU-Only gemeldet (DirectML ist Windows-only).
/// </summary>
public static class GPUInfo
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "GPUInfo");

    public static bool GpuVerfuegbar { get; private set; }
    public static string GpuName { get; private set; } = "Unbekannt";
    public static string Provider { get; private set; } = "CPU";

    /// <summary>
    /// Erkennt verfügbare GPU und ONNX Runtime Provider.
    /// DirectML wird nur auf Windows geprüft (Windows-only).
    /// </summary>
    public static void Erkennen()
    {
        GpuVerfuegbar = false;
        GpuName = "Unbekannt";
        Provider = "CPU";

        // 1. DirectML prüfen (nur auf Windows verfügbar)
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var sessionOpts = new Microsoft.ML.OnnxRuntime.SessionOptions();
                sessionOpts.AppendExecutionProvider_DML(0);
                sessionOpts.Dispose();
                GpuVerfuegbar = true;
                Provider = "DirectML";
                GpuName = "DirectML GPU (Windows)";
                Log.Information("DirectML Provider verfügbar (Windows)");
            }
            catch
            {
                Log.Debug("DirectML nicht verfügbar");
            }
        }

        // 2. CUDA prüfen (cross-platform, falls CUDA installiert)
        if (!GpuVerfuegbar)
        {
            try
            {
                var sessionOpts = new Microsoft.ML.OnnxRuntime.SessionOptions();
                sessionOpts.AppendExecutionProvider_CUDA(0);
                sessionOpts.Dispose();
                GpuVerfuegbar = true;
                Provider = "CUDA";
                GpuName = "CUDA GPU";
                Log.Information("CUDA Provider verfügbar");
            }
            catch
            {
                Log.Debug("CUDA nicht verfügbar — CPU-Only");
            }
        }

        // 3. GPU-Name via WMI ermitteln (nur Windows)
        if (GpuVerfuegbar && OperatingSystem.IsWindows())
        {
            try
            {
                GpuName = Win32GpuInfo.GetGpuNameWindows() ?? GpuName;
            }
            catch (Exception ex)
            {
                Log.Warning("GPU-Name konnte nicht ermittelt werden: {Msg}", ex.Message);
            }
        }

        Log.Information("GPU: {Verfuegbar} | Name: {Name} | Provider: {Provider}",
            GpuVerfuegbar, GpuName, Provider);
    }
}

/// <summary>
/// Windows-spezifische GPU-Erkennung via WMI (System.Management).
/// Nur auf Windows verfügbar — sonst wird "Unbekannt" zurückgegeben.
/// </summary>
internal static class Win32GpuInfo
{
    public static string? GetGpuNameWindows()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            // System.Management ist Windows-only. Späte Bindung via Type vermeidet
            // Compile-Fehler auf Linux und Runtime-Fehler wenn Assembly fehlt.
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "System.Management");
            if (asm == null) return null;

            var searcherType = asm.GetType("System.Management.ManagementObjectSearcher")
                ?? throw new InvalidOperationException("ManagementObjectSearcher nicht gefunden");
            var searcher = Activator.CreateInstance(searcherType, "SELECT Name FROM Win32_VideoController")
                ?? throw new InvalidOperationException("ManagementObjectSearcher konnte nicht erstellt werden");

            var getMethod = searcherType.GetMethod("Get") ?? throw new InvalidOperationException("Get-Methode nicht gefunden");
            var collection = getMethod.Invoke(searcher, null);
            if (collection == null) return null;

            foreach (var item in (System.Collections.IEnumerable)collection)
            {
                var itemType = item.GetType();
                var indexerProps = itemType.GetProperties();
                // ManagementObject indexer: prop["Name"]
                var nameParams = new object?[] { "Name" };
                foreach (var prop in indexerProps)
                {
                    var idxParams = prop.GetIndexParameters();
                    if (idxParams.Length == 1 && idxParams[0].ParameterType == typeof(string))
                    {
                        var name = prop.GetValue(item, nameParams)?.ToString();
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
        }
        catch
        {
            // Fallback
        }
        return null;
    }
}