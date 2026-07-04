using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Serilog;
using Serilog.Events;

namespace FlipsiColor.Utils;

/// <summary>
/// Strukturiertes Logging mit Serilog — Datei-Rotation + Console.
/// FIX #9: Reduzierte Log-Level in Production — keine Debug-Logs mit sensiblen Daten.
/// </summary>
public class Logger
{
    private const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    private const int MaxFiles = 5;

    /// <summary>
    /// Initialisiert Serilog mit Datei- und Console-Output.
    /// FIX #9: Log-Level auf Information (nicht Debug) — reduziert sensible Daten in Logs.
    /// </summary>
    public static void Init(string? logDir = null)
    {
        logDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlipsiColor", "Logs");
        Directory.CreateDirectory(logDir);

        // FIX #9: MinimumLevel.Information statt Debug — verhindert Logging sensibler Details
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logDir, "flipsicolor-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: MaxFileSize,
                retainedFileCountLimit: MaxFiles,
                // FIX #9: Keine Stack-Traces in File-Logs — nur Message
                outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}")
            .Enrich.FromLogContext()
            .CreateLogger();

        Log.ForContext<Logger>().Information("FlipsiColor Logging initialisiert. Log-Dir: {Dir}", logDir);
    }

    /// <summary>
    /// Schließt den Logger ordnungsgemäß
    /// </summary>
    public static void Close()
    {
        Log.CloseAndFlush();
    }
}

/// <summary>
/// Crypto-Utilities (SHA256)
/// </summary>
public static class Crypto
{
    public static async Task<string> Sha256FileAsync(string pfad)
    {
        using var stream = File.OpenRead(pfad);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Datei-System Utilities
/// </summary>
public static class FileSystem
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "FileSystem");

    /// <summary>
    /// Sichert dass ein Verzeichnis existiert
    /// </summary>
    public static void EnsureDirectory(string pfad)
    {
        if (!Directory.Exists(pfad))
        {
            Directory.CreateDirectory(pfad);
            Log.Information("Verzeichnis erstellt: {Pfad}", pfad);
        }
    }

    /// <summary>
    /// Sicheres Kopieren mit Überschreibung
    /// </summary>
    public static bool SafeCopy(string source, string dest)
    {
        try
        {
            File.Copy(source, dest, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Kopieren fehlgeschlagen: {Src} → {Dest}", source, dest);
            return false;
        }
    }
}