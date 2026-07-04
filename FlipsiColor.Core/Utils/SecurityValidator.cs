using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

using Serilog;

namespace FlipsiColor.Utils;

/// <summary>
/// Zentrale Sicherheits-Validierung für Dateipfade, URLs und Prozess-Argumente.
/// Verhindert Path-Traversal, Command-Injection, unsichere Downloads und DLL-Hijacking.
/// </summary>
public static class SecurityValidator
{
    // FIX: Serilog.ForContext kann nicht mit static class verwendet werden — String-Literal
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "SecurityValidator");

    /// <summary>
    /// Erlaubte Datei-Endungen für Bild- und Video-Importe.
    /// </summary>
    private static readonly HashSet<string> ErlaubteBildEndungen = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".webp",
        ".cr2", ".cr3", ".nef", ".arw", ".dng", ".orf", ".rw2", ".raw",
        ".mp4", ".mov", ".avi", ".mkv", ".m4v", ".wmv", ".flv"
    };

    /// <summary>
    /// Maximale Pfadlänge (Windows-Maximum mit etwas Sicherheitsmarge).
    /// </summary>
    private const int MaxPfadLaenge = 240;

    /// <summary>
    /// Validiert einen Dateipfad gegen Path-Traversal, UNC-Pfade und nicht erlaubte Endungen.
    /// Gibt den vollqualifizierten, bereinigten Pfad zurück, oder null bei Ablehnung.
    /// </summary>
    /// <param name="pfad">Der zu prüfende Dateipfad.</param>
    /// <param name="erlaubteEndungen">Optionale Liste erlaubter Endungen; null = alle Bild/Video-Endungen.</param>
    /// <returns>Vollqualifizierter Pfad oder null.</returns>
    public static string? ValidiereDateiPfad(string? pfad, HashSet<string>? erlaubteEndungen = null)
    {
        if (string.IsNullOrWhiteSpace(pfad))
        {
            Log.Warning("Pfad-Validierung: leerer Pfad abgelehnt");
            return null;
        }

        // Längenbegrenzung — verhindert Buffer-Overflow-Versuche
        if (pfad.Length > MaxPfadLaenge)
        {
            Log.Warning("Pfad-Validierung: Pfad zu lang ({Len} Zeichen) abgelehnt", pfad.Length);
            return null;
        }

        // UNC-Pfade (\\server\share) blockieren — nur lokale Dateien erlauben
        if (pfad.StartsWith(@"\\", StringComparison.Ordinal) ||
            pfad.StartsWith("//", StringComparison.Ordinal))
        {
            Log.Warning("Pfad-Validierung: UNC-Pfad abgelehnt (nur lokale Pfade erlaubt): {Pfad}", pfad);
            return null;
        }

        // Path-Traversal-Muster erkennen — sowohl literal als auch nach Kanonisierung
        // Prüfe auf ../ und ..\ in beiden Varianten
        string normalisiert = pfad.Replace('/', '\\');
        if (normalisiert.Contains("..\\", StringComparison.Ordinal) ||
            normalisiert.Contains("..\u005C", StringComparison.Ordinal))
        {
            Log.Warning("Pfad-Validierung: Path-Traversal-Muster ('..') erkannt und abgelehnt: {Pfad}", pfad);
            return null;
        }

        // Volles Pfad kanonisieren — schließt encoded/obfuscated Traversal ein
        string fullPfad;
        try
        {
            fullPfad = Path.GetFullPath(pfad);
        }
        catch (Exception ex)
        {
            Log.Warning("Pfad-Validierung: GetFullPath fehlgeschlagen für '{Pfad}': {Fehler}", pfad, ex.Message);
            return null;
        }

        // Erneut auf UNC prüfen nach Kanonisierung (z.B. "relative/../../\\server\share")
        if (fullPfad.StartsWith(@"\\", StringComparison.Ordinal))
        {
            Log.Warning("Pfad-Validierung: kanonisierter UNC-Pfad abgelehnt: {Pfad}", fullPfad);
            return null;
        }

        // Path-Traversal nach Kanonisierung: Verhindere Zugriff außerhalb des erlaubten Root
        // Wir akzeptieren alle lokalen Pfade, aber nicht solche, die nach Kanonisierung
        // Directory-Traversal enthalten (Root-Escape via symlink etc. wird von GetFullPath bereinigt).
        // Sicherheitscheck: ensure root drive is a letter drive, not UNC
        if (fullPfad.Length >= 2 && fullPfad[1] == ':' && !char.IsLetter(fullPfad[0]))
        {
            Log.Warning("Pfad-Validierung: ungültiges Laufwerk im Pfad: {Pfad}", fullPfad);
            return null;
        }

        // Datei-Endung prüfen
        var endungen = erlaubteEndungen ?? ErlaubteBildEndungen;
        var ext = Path.GetExtension(fullPfad);
        if (string.IsNullOrEmpty(ext) || !endungen.Contains(ext))
        {
            Log.Warning("Pfad-Validierung: Endung '{Ext}' nicht erlaubt für Pfad: {Pfad}", ext, fullPfad);
            return null;
        }

        // Datei muss existieren (nur für Lese-Operationen relevant)
        if (!File.Exists(fullPfad))
        {
            Log.Warning("Pfad-Validierung: Datei existiert nicht: {Pfad}", fullPfad);
            return null;
        }

        return fullPfad;
    }

    /// <summary>
    /// Validiert einen Ausgabe-Dateipfad (Datei muss nicht existieren, Endung wird geprüft).
    /// Verhindert Path-Traversal und UNC-Pfade.
    /// </summary>
    public static string? ValidiereAusgabePfad(string? pfad, HashSet<string>? erlaubteEndungen = null)
    {
        if (string.IsNullOrWhiteSpace(pfad))
        {
            Log.Warning("Ausgabe-Pfad-Validierung: leerer Pfad abgelehnt");
            return null;
        }

        if (pfad.Length > MaxPfadLaenge)
        {
            Log.Warning("Ausgabe-Pfad-Validierung: Pfad zu lang ({Len} Zeichen) abgelehnt", pfad.Length);
            return null;
        }

        if (pfad.StartsWith(@"\\", StringComparison.Ordinal) ||
            pfad.StartsWith("//", StringComparison.Ordinal))
        {
            Log.Warning("Ausgabe-Pfad-Validierung: UNC-Pfad abgelehnt: {Pfad}", pfad);
            return null;
        }

        string normalisiert = pfad.Replace('/', '\\');
        if (normalisiert.Contains("..\\", StringComparison.Ordinal))
        {
            Log.Warning("Ausgabe-Pfad-Validierung: Path-Traversal abgelehnt: {Pfad}", pfad);
            return null;
        }

        string fullPfad;
        try
        {
            fullPfad = Path.GetFullPath(pfad);
        }
        catch (Exception ex)
        {
            Log.Warning("Ausgabe-Pfad-Validierung: GetFullPath fehlgeschlagen: {Fehler}", ex.Message);
            return null;
        }

        if (fullPfad.StartsWith(@"\\", StringComparison.Ordinal))
        {
            Log.Warning("Ausgabe-Pfad-Validierung: kanonisierter UNC-Pfad abgelehnt: {Pfad}", fullPfad);
            return null;
        }

        var endungen = erlaubteEndungen ?? ErlaubteBildEndungen;
        var ext = Path.GetExtension(fullPfad);
        if (string.IsNullOrEmpty(ext) || !endungen.Contains(ext))
        {
            Log.Warning("Ausgabe-Pfad-Validierung: Endung '{Ext}' nicht erlaubt: {Pfad}", ext, fullPfad);
            return null;
        }

        return fullPfad;
    }

    /// <summary>
    /// Validiert einen Verzeichnis-Pfad gegen Path-Traversal und UNC.
    /// </summary>
    public static string? ValidiereVerzeichnisPfad(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad))
        {
            Log.Warning("Verzeichnis-Validierung: leerer Pfad abgelehnt");
            return null;
        }

        if (pfad.Length > MaxPfadLaenge)
        {
            Log.Warning("Verzeichnis-Validierung: Pfad zu lang abgelehnt");
            return null;
        }

        if (pfad.StartsWith(@"\\", StringComparison.Ordinal) ||
            pfad.StartsWith("//", StringComparison.Ordinal))
        {
            Log.Warning("Verzeichnis-Validierung: UNC-Pfad abgelehnt: {Pfad}", pfad);
            return null;
        }

        string normalisiert = pfad.Replace('/', '\\');
        if (normalisiert.Contains("..\\", StringComparison.Ordinal))
        {
            Log.Warning("Verzeichnis-Validierung: Path-Traversal abgelehnt: {Pfad}", pfad);
            return null;
        }

        string fullPfad;
        try
        {
            fullPfad = Path.GetFullPath(pfad);
        }
        catch (Exception ex)
        {
            Log.Warning("Verzeichnis-Validierung: GetFullPath fehlgeschlagen: {Fehler}", ex.Message);
            return null;
        }

        if (fullPfad.StartsWith(@"\\", StringComparison.Ordinal))
        {
            Log.Warning("Verzeichnis-Validierung: kanonisierter UNC-Pfad abgelehnt: {Pfad}", fullPfad);
            return null;
        }

        return fullPfad;
    }

    /// <summary>
    /// Escaped einen Dateipfad für sichere Verwendung in FFMPEG/ffprobe Kommandozeilen-Argumenten.
    /// Verhindert Command-Injection durch Dateinamen mit Sonderzeichen (Semikolon, Pipe, Backtick, etc.).
    ///
    /// WICHTIG: Diese Methode escapet für Shell-Argumente. Die bevorzugte Methode ist jedoch
    /// ArgumentList zu verwenden (siehe SichereProcessStartInfo) — dann ist kein Escaping nötig.
    /// </summary>
    public static string EscapeFfmpegArgument(string arg)
    {
        // Alle Dateipfad-Argumente werden in doppelte Anführungszeichen gesetzt.
        // Innerhalb der Anführungszeichen escapen wir: " → \" und \ → \\ (Windows-Shell-Konvention).
        // Neuelinien und Null-Bytes werden komplett entfernt (verhindert Befehls-Injection).
        var bereinigt = arg
            .Replace("\0", "")
            .Replace("\r", "")
            .Replace("\n", "")
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");

        return $"\"{bereinigt}\"";
    }

    /// <summary>
    /// Erstellt eine ProcessStartInfo mit ArgumentList (kein String-Arguments) — die sicherste Methode,
    /// um Command-Injection zu verhindern. ArgumentList übergibt Argumente direkt an den Prozess,
    /// ohne Shell-Interpretation.
    /// </summary>
    public static ProcessStartInfo SichereProcessStartInfo(string fileName, IEnumerable<string> argumente)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        foreach (var arg in argumente)
        {
            // Null-Bytes und Zeilenumbrüche entfernen — rest wird von .NET sicher übergeben
            var bereinigt = arg
                .Replace("\0", "")
                .Replace("\r", "")
                .Replace("\n", "");
            psi.ArgumentList.Add(bereinigt);
        }

        return psi;
    }

    /// <summary>
    /// Validiert eine Download-URL: muss HTTPS sein, kein file:// oder http://,
    /// keine Loopback/Private-IP-Adressen (verhindert SSRF und Redirect-Angriffe).
    /// </summary>
    public static bool ValidiereDownloadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Log.Warning("URL-Validierung: leere URL abgelehnt");
            return false;
        }

        // URL parsen
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            Log.Warning("URL-Validierung: ungültige URL abgelehnt: {Url}", url);
            return false;
        }

        // HTTPS erzwingen
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            Log.Warning("URL-Validierung: nur HTTPS erlaubt, Schema '{Schema}' abgelehnt: {Url}",
                uri.Scheme, url);
            return false;
        }

        // file:// und andere Schemata werden bereits oben durch die HTTPS-Prüfung blockiert.
        // (Dead Code entfernt — der if-Block war unreachable, da uri.Scheme != HTTPS schon return false ergibt.)

        // Loopback / Private-IP-Adressen blockieren (SSRF-Schutz)
        var host = uri.Host;
        if (string.IsNullOrEmpty(host))
        {
            Log.Warning("URL-Validierung: kein Host in URL: {Url}", url);
            return false;
        }

        // localhost und IP-Literal blockieren
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("URL-Validierung: localhost abgelehnt: {Url}", url);
            return false;
        }

        // IP-Adresse prüfen (verhindert SSRF zu internen Netzwerken)
        if (IPAddress.TryParse(host, out var ipAddr))
        {
            if (IPAddress.IsLoopback(ipAddr) ||
                ipAddr.ToString().StartsWith("10.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("192.168.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.16.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.17.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.18.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.19.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.20.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.21.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.22.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.23.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.24.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.25.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.26.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.27.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.28.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.29.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.30.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("172.31.", StringComparison.Ordinal) ||
                ipAddr.ToString().StartsWith("169.254.", StringComparison.Ordinal) ||
                ipAddr.ToString() == "::1")
            {
                Log.Warning("URL-Validierung: private/loopback IP '{Ip}' abgelehnt: {Url}", ipAddr, url);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Begrenzt einen Pipeline-Parameter-Wert auf einen sicheren Bereich.
    /// Verhindert Overflow/Underflow bei extremen float-Werten.
    /// </summary>
    public static float BegrenzeParameter(float wert, float min, float max)
    {
        // NaN und Infinity abfangen
        if (float.IsNaN(wert) || float.IsInfinity(wert))
            return 0f;

        return Math.Clamp(wert, min, max);
    }

    /// <summary>
    /// Begrenzt einen Integer-Parameter auf einen sicheren Bereich.
    /// </summary>
    public static int BegrenzeIntParameter(int wert, int min, int max)
    {
        return Math.Clamp(wert, min, max);
    }

    /// <summary>
    /// Bereinigt einen Dateipfad für sichere Logging-Ausgabe — entfernt Username aus Pfaden.
    /// Aus "C:\Users\MaxMustermann\Bilder\foto.jpg" wird "C:\Users\*\Bilder\foto.jpg".
    /// </summary>
    public static string BereinigePfadFuerLog(string pfad)
    {
        if (string.IsNullOrEmpty(pfad))
            return pfad;

        // Windows User-Pfade: C:\Users\<name>\...
        // Ersetze den User-Namen durch *
        var match = Regex.Match(pfad, @"([A-Za-z]:\\Users\\)([^\\]+)", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        if (match.Success)
        {
            return pfad.Substring(0, match.Groups[1].Length) + "*" + pfad.Substring(match.Groups[1].Length + match.Groups[2].Length);
        }

        // Unix home-Pfade: /home/<name>/... oder /Users/<name>/...
        match = Regex.Match(pfad, @"(/home/|/Users/)([^/]+)", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        if (match.Success)
        {
            return pfad.Substring(0, match.Groups[1].Length) + "*" + pfad.Substring(match.Groups[1].Length + match.Groups[2].Length);
        }

        return pfad;
    }

    /// <summary>
    /// Bereinigt eine Exception-Message für Logging — entfernt potenziell sensible Pfad-Anteile.
    /// Stack-Traces werden nur im Debug-Modus geloggt.
    /// </summary>
    public static string BereinigeExceptionFuerLog(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        // Pfade in Exception-Messages bereinigen
        var match = Regex.Match(message, @"([A-Za-z]:\\Users\\)([^\\]+)", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        if (match.Success)
        {
            message = message.Replace(match.Groups[2].Value, "*");
        }

        match = Regex.Match(message, @"(/home/|/Users/)([^/]+)", RegexOptions.None, TimeSpan.FromMilliseconds(100));
        if (match.Success)
        {
            message = message.Replace(match.Groups[2].Value, "*");
        }

        return message;
    }
}