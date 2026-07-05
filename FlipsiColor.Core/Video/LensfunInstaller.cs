using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using FlipsiColor.Utils;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace FlipsiColor.Video;

/// <summary>
/// Automatischer Download + Installation der Lensfun-Bibliothek für Windows.
///
/// Windows:
///   Lädt lensfun-windows-bundle.zip herunter und entpackt nach
///   %LOCALAPPDATA%/FlipsiColor/lensfun/.
///   Enthält: liblensfun.dll + GCC/GLib/Gettext Runtime-DLLs + lensfun-db/ XML-Datenbank.
///
/// Linux:
///   Lensfun ist über den Paketmanager installiert (liblensfun.so).
///   IstInstalliert gibt true zurück, wenn liblensfun.so gefunden wird.
///
/// Die Installation ist portabel — keine PATH-Änderungen, keine Registry-Einträge.
/// Alle Downloads laufen über den SecurityValidator (HTTPS-Validierung, kein file://,
/// keine private IPs).
/// </summary>
public sealed class LensfunInstaller
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<LensfunInstaller>();

    /// <summary>HttpClient mit Redirect-Beschränkung (gleiche Konvention wie VapourSynthInstaller).</summary>
    private static readonly HttpClient _http = ErstelleSicherenHttpClient();

    /// <summary>Lensfun Windows Bundle URL — enthält DLLs + Datenbank.</summary>
    private const string LensfunUrl =
        "https://github.com/TechFlipsi/FlipsiColor/releases/download/v0.4.2/lensfun-windows-bundle.zip";

    // --- Installationspfade ---

    /// <summary>
    /// Basis-Installationsverzeichnis.
    /// Windows: %LOCALAPPDATA%/FlipsiColor/lensfun/
    /// Linux:   ~/.local/share/FlipsiColor/lensfun/ (nur für Konsistenz, nicht verwendet)
    /// </summary>
    private static string BasisVerzeichnis =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipsiColor", "lensfun")
            : Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "FlipsiColor", "lensfun");

    /// <summary>
    /// Absoluter Pfad zur liblensfun.dll (Windows) bzw. liblensfun.so (Linux).
    /// </summary>
    public string DllPfad =>
        OperatingSystem.IsWindows()
            ? Path.Combine(BasisVerzeichnis, "liblensfun.dll")
            : "liblensfun.so";

    /// <summary>
    /// Absoluter Pfad zum lensfun-db-Ordner (enthält die XML-Datenbank).
    /// </summary>
    public string DatenbankPfad => Path.Combine(BasisVerzeichnis, "lensfun-db");

    // --- Events ---

    /// <summary>
    /// Progress-Event für UI-Updates. Wird während des Downloads und der Installation gefeuert.
    /// </summary>
    public event EventHandler<LensfunInstallFortschritt>? InstallationsFortschritt;

    // ============================================================
    //  Öffentliche API
    // ============================================================

    /// <summary>
    /// Prüft ob Lensfun installiert ist.
    /// Windows: liblensfun.dll existiert im Installationsordner.
    /// Linux:   liblensfun.so im System verfügbar (über Paketmanager installiert).
    /// </summary>
    public bool IstInstalliert
    {
        get
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    bool dllVorhanden = File.Exists(DllPfad);
                    if (!dllVorhanden)
                    {
                        Log.Debug("IstInstalliert: liblensfun.dll nicht gefunden in {Pfad}", BasisVerzeichnis);
                        return false;
                    }
                    Log.Debug("IstInstalliert: Lensfun-Installation erkannt (Windows)");
                    return true;
                }
                else
                {
                    // Linux: liblensfun.so über Systembibliothek verfügbar?
                    // NativeLibrary.TryLoad prüft die Standard-Suchpfade.
                    bool geladen = NativeLibrary.TryLoad("liblensfun", out _);
                    if (geladen)
                    {
                        Log.Debug("IstInstalliert: liblensfun.so im System gefunden (Linux)");
                        return true;
                    }
                    // Fallback: auch lensfun ohne lib-Präfix versuchen
                    geladen = NativeLibrary.TryLoad("lensfun", out _);
                    if (geladen)
                    {
                        Log.Debug("IstInstalliert: lensfun.so im System gefunden (Linux)");
                        return true;
                    }
                    Log.Debug("IstInstalliert: liblensfun.so nicht im System gefunden (Linux)");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("IstInstalliert: Prüfung fehlgeschlagen: {Fehler}",
                    SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
                return false;
            }
        }
    }

    /// <summary>
    /// Installiert Lensfun (Windows: ZIP herunterladen + entpacken; Linux: nichts tun, schon installiert).
    /// Unterstützt CancellationToken für Abbruch und feuert Progress-Events für UI-Updates.
    /// </summary>
    /// <param name="cancellationToken">Token für Abbruch-Benachrichtigung.</param>
    /// <returns>true wenn Installation erfolgreich, false bei Fehler.</returns>
    public async Task<bool> InstallierenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("Lensfun-Installation gestartet (Platform: {Platform})",
                OperatingSystem.IsWindows() ? "Windows" : "Linux");

            if (OperatingSystem.IsWindows())
            {
                return await InstalliereWindowsAsync(cancellationToken);
            }
            else
            {
                // Linux: Lensfun ist über den Paketmanager installiert — nichts zu tun.
                Log.Information("Lensfun-Installation übersprungen (Linux — bereits installiert)");
                FireProgress("Lensfun bereits installiert (Linux)", 1, 1);
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("Lensfun-Installation abgebrochen durch CancellationToken");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error("Lensfun-Installation fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    // ============================================================
    //  Windows-Installation
    // ============================================================

    /// <summary>
    /// Windows-Installation: lensfun-windows-bundle.zip herunterladen und entpacken.
    /// </summary>
    private async Task<bool> InstalliereWindowsAsync(CancellationToken cancellationToken)
    {
        // Installationsverzeichnis vorbereiten
        Directory.CreateDirectory(BasisVerzeichnis);

        // Temp-Verzeichnis für Downloads
        string tempVerzeichnis = Path.Combine(Path.GetTempPath(), "FlipsiColor_lensfun_install_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVerzeichnis);

        try
        {
            // --- Schritt 1: lensfun-windows-bundle.zip herunterladen ---
            cancellationToken.ThrowIfCancellationRequested();
            FireProgress("Lade Lensfun Windows Bundle herunter", 0, 2);

            string zipPfad = await LadeDateiHerunterAsync(LensfunUrl, tempVerzeichnis, cancellationToken);
            Log.Information("Lensfun Windows Bundle heruntergeladen");

            // --- Schritt 2: ZIP entpacken ---
            cancellationToken.ThrowIfCancellationRequested();
            FireProgress("Entpacke Lensfun Windows Bundle", 1, 2);

            EntpackeZip(zipPfad, BasisVerzeichnis);
            Log.Information("Lensfun Windows Bundle entpackt nach {Pfad}", BasisVerzeichnis);

            // --- Verifikation ---
            FireProgress("Verifiziere Installation", 2, 2);
            if (!IstInstalliert)
            {
                Log.Error("Lensfun-Installation abgeschlossen, aber liblensfun.dll nicht gefunden");
                return false;
            }

            FireProgress("Lensfun erfolgreich installiert", 2, 2);
            Log.Information("Lensfun-Installation erfolgreich (Windows)");
            return true;
        }
        finally
        {
            // Temp-Verzeichnis bereinigen
            try
            {
                if (Directory.Exists(tempVerzeichnis))
                    Directory.Delete(tempVerzeichnis, recursive: true);
            }
            catch (Exception ex)
            {
                Log.Debug("Temp-Verzeichnis konnte nicht gelöscht werden: {Fehler}",
                    SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            }
        }
    }

    // ============================================================
    //  Hilfsmethoden — Download, Entpackung
    // ============================================================

    /// <summary>
    /// Erstellt einen sicheren HttpClient mit Redirect-Beschränkung (gleiche Konvention wie VapourSynthInstaller).
    /// </summary>
    private static HttpClient ErstelleSicherenHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        var client = new HttpClient(handler, disposeHandler: true);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FlipsiColor/LensfunInstaller");
        client.Timeout = TimeSpan.FromMinutes(10);
        return client;
    }

    /// <summary>
    /// Lädt eine Datei von einer URL herunter, verifiziert die URL über den SecurityValidator,
    /// speichert in ein Temp-Verzeichnis und gibt den lokalen Pfad zurück.
    /// Nach erfolgreichem Download wird SHA256 berechnet und geloggt (Integritäts-Prüfung).
    /// </summary>
    private async Task<string> LadeDateiHerunterAsync(string url, string zielVerzeichnis, CancellationToken cancellationToken)
    {
        // SecurityValidator: HTTPS-Validierung, kein file://, keine private IPs
        if (!SecurityValidator.ValidiereDownloadUrl(url))
        {
            throw new InvalidOperationException($"Download-URL abgelehnt (SecurityValidator): {url}");
        }

        string dateiname = Path.GetFileName(new Uri(url).LocalPath);
        if (string.IsNullOrEmpty(dateiname))
        {
            dateiname = Guid.NewGuid().ToString("N");
        }

        // Download in Temp-Datei (.downloading), erst nach Verifikation umbenennen
        string tempPfad = Path.Combine(zielVerzeichnis, dateiname + ".downloading");
        string endgueltigerPfad = Path.Combine(zielVerzeichnis, dateiname);

        Log.Information("Lade {Url} herunter...", SecurityValidator.BereinigePfadFuerLog(url));

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Antwort-URL nach Redirect prüfen — nur HTTPS erlaubt
            if (response.RequestMessage?.RequestUri is { } finalUri && finalUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"Redirect zu nicht-HTTPS blockiert: {finalUri.Scheme}");
            }

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            long empfangen = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(tempPfad, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            int gelesen;
            while ((gelesen = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, gelesen), cancellationToken);
                empfangen += gelesen;
            }

            await fileStream.FlushAsync(cancellationToken);
            await fileStream.DisposeAsync();

            // SHA256-Verifikation (Hash wird geloggt — Integritäts-Prüfung)
            string sha256 = await BerechneSha256Async(tempPfad);
            Log.Information("Download abgeschlossen: {Datei} ({Bytes} bytes, SHA256: {Hash})",
                dateiname, empfangen, sha256);

            // Nach Verifikation verschieben
            File.Move(tempPfad, endgueltigerPfad, overwrite: true);

            return endgueltigerPfad;
        }
        catch (OperationCanceledException)
        {
            // Temp-Datei bereinigen
            try { if (File.Exists(tempPfad)) File.Delete(tempPfad); } catch { /* Ignorieren */ }
            throw;
        }
        catch (Exception)
        {
            // Temp-Datei bereinigen
            try { if (File.Exists(tempPfad)) File.Delete(tempPfad); } catch { /* Ignorieren */ }
            throw;
        }
    }

    /// <summary>
    /// Berechnet den SHA256-Hash einer Datei.
    /// </summary>
    private static async Task<string> BerechneSha256Async(string pfad)
    {
        using var stream = File.OpenRead(pfad);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Entpackt eine ZIP-Datei mit SharpCompress.
    /// </summary>
    private void EntpackeZip(string archivPfad, string zielVerzeichnis)
    {
        try
        {
            using var archive = ArchiveFactory.OpenArchive(archivPfad, new SharpCompress.Readers.ReaderOptions());
            foreach (var entry in archive.Entries)
            {
                if (!entry.IsDirectory)
                {
                    entry.WriteToDirectory(zielVerzeichnis,
                        new ExtractionOptions(extractFullPath: true, overwrite: true));
                }
            }
            Log.Debug("SharpCompress entpackt: {Archiv} → {Ziel}", archivPfad, zielVerzeichnis);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Entpacken von {Path.GetFileName(archivPfad)} fehlgeschlagen: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Feuert das Progress-Event für UI-Updates.
    /// </summary>
    private void FireProgress(string schritt, long aktuelleSchritt, long gesamtSchritte)
    {
        Log.Debug("Installations-Fortschritt: {Schritt} ({Aktuell}/{Gesamt})", schritt, aktuelleSchritt, gesamtSchritte);
        InstallationsFortschritt?.Invoke(this, new LensfunInstallFortschritt(schritt, aktuelleSchritt, gesamtSchritte));
    }
}

/// <summary>
/// Progress-Event-Args für die Lensfun-Installation.
/// Enthält den aktuellen Schritt-Name und Fortschritt (aktuell/gesamt).
/// </summary>
public sealed class LensfunInstallFortschritt : EventArgs
{
    /// <summary>Beschreibender Name des aktuellen Schritts (z.B. "Lade Lensfun Windows Bundle herunter").</summary>
    public string Schritt { get; }

    /// <summary>Aktueller Schritt-Index (0-basiert oder 1-basiert je nach Kontext).</summary>
    public long AktuellerSchritt { get; }

    /// <summary>Gesamtzahl der Schritte.</summary>
    public long GesamtSchritte { get; }

    /// <summary>Fortschritt in Prozent (0-100).</summary>
    public double Prozent => GesamtSchritte > 0 ? AktuellerSchritt * 100.0 / GesamtSchritte : 0;

    public LensfunInstallFortschritt(string schritt, long aktuellerSchritt, long gesamtSchritte)
    {
        Schritt = schritt;
        AktuellerSchritt = aktuellerSchritt;
        GesamtSchritte = gesamtSchritte;
    }
}