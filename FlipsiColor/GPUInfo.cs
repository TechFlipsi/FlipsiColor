using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

using FlipsiColor.Utils;

namespace FlipsiColor;

/// <summary>
/// GPU-Erkennung — ONNX Runtime Provider-Check (CUDA, DirectML, TensorRT)
/// </summary>
public class GPUInfo
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<GPUInfo>();

    public static bool GpuVerfuegbar { get; private set; }
    public static string GpuName { get; private set; } = "Unbekannt";
    public static string Provider { get; private set; } = "CPU";

    /// <summary>
    /// Erkennt verfügbare GPU und ONNX Runtime Provider
    /// </summary>
    public static void Erkennen()
    {
        GpuVerfuegbar = false;
        GpuName = "Unbekannt";
        Provider = "CPU";

        // 1. DirectML prüfen (am einfachsten auf Windows)
        try
        {
            var sessionOpts = new Microsoft.ML.OnnxRuntime.SessionOptions();
            sessionOpts.AppendExecutionProvider_DML(0);
            sessionOpts.Dispose();
            GpuVerfuegbar = true;
            Provider = "DirectML";
            Log.Information("DirectML Provider verfügbar");
        }
        catch
        {
            Log.Debug("DirectML nicht verfügbar");
        }

        // 2. CUDA prüfen
        if (!GpuVerfuegbar)
        {
            try
            {
                var sessionOpts = new Microsoft.ML.OnnxRuntime.SessionOptions();
                sessionOpts.AppendExecutionProvider_CUDA(0);
                sessionOpts.Dispose();
                GpuVerfuegbar = true;
                Provider = "CUDA";
                Log.Information("CUDA Provider verfügbar");
            }
            catch
            {
                Log.Debug("CUDA nicht verfügbar");
            }
        }

        // 3. GPU-Name via WMI ermitteln
        if (GpuVerfuegbar)
        {
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    GpuName = obj["Name"]?.ToString() ?? "Unbekannt";
                    break;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "GPU-Name konnte nicht ermittelt werden");
            }
        }

        Log.Information("GPU: {Verfuegbar} | Name: {Name} | Provider: {Provider}",
            GpuVerfuegbar, GpuName, Provider);
    }
}