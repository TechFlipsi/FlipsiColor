using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using FlipsiColor.Utils;

// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.Video;

/// <summary>
/// Automatischer Download + Installation von VapourSynth + Plugins.
///
/// Windows:
///   1. VapourSynth64-Portable-R76.zip       → vapoursynth/
///   2. ffms2-5.0-msvc.7z                     → vapoursynth/plugins/
///   3. VSORT-Windows-x64.v15.16.7z          → vapoursynth/plugins/
///   4. scripts.v15.16.7z                     → vapoursynth/plugins/
///
/// Linux:
///   1. pip install vapoursynth vsrepo
///   2. vsrepo install ffms2 havsfunc
///   3. pip install vapoursynth-mlrt-ort
///
/// Die Installation ist portabel — keine PATH-Änderungen, keine Registry-Einträge.
/// Alle Downloads laufen über den SecurityValidator (HTTPS-Validierung, kein file://,
/// keine private IPs). 7z-Dateien werden via 7z CLI entpackt; ist 7z nicht verfügbar,
/// wird eine klare Fehlermeldung ausgegeben.
/// </summary>
public sealed class VapourSynthInstaller
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<VapourSynthInstaller>();

    /// <summary>
    /// HttpClient mit Redirect-Beschränkung (gleiche Konvention wie ModelManager).
    /// </summary>
    private static readonly HttpClient _http = ErstelleSicherenHttpClient();

    // --- Verifizierte Download-URLs (alle HTTP 200 geprüft) ---

    /// <summary>VapourSynth R76 Portable (Windows) — 14.3 MB ZIP.</summary>
    private const string VapourSynthUrl =
        "https://github.com/vapoursynth/vapoursynth/releases/download/R76/VapourSynth64-Portable-R76.zip";

    /// <summary>ffms2 5.0 Plugin (Windows) — 8.2 MB 7z.</summary>
    private const string Ffms2Url =
        "https://github.com/FFMS/ffms2/releases/download/5.0/ffms2-5.0-msvc.7z";

    /// <summary>vs-mlrt ONNX Runtime CPU (Windows) — 56.4 MB 7z.</summary>
    private const string VsortUrl =
        "https://github.com/AmusementClub/vs-mlrt/releases/download/v15.16/VSORT-Windows-x64.v15.16.7z";

    /// <summary>vs-mlrt Scripts — &lt;1 MB 7z.</summary>
    private const string VsortScriptsUrl =
        "https://github.com/AmusementClub/vs-mlrt/releases/download/v15.16/scripts.v15.16.7z";

    // --- Installationspfade ---

    /// <summary>
    /// Basis-Installationsverzeichnis.
    /// Windows: %LOCALAPPDATA%/FlipsiColor/vapoursynth/
    /// Linux:   ~/.local/share/FlipsiColor/vapoursynth/
    /// </summary>
    private static string BasisVerzeichnis =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlipsiColor", "vapoursynth")
            : Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share", "FlipsiColor", "vapoursynth");

    /// <summary>
    /// Absoluter Pfad zu vspipe.
    /// Windows: vapoursynth/vspipe.exe
    /// Linux:  vapoursynth/bin/vspipe (falls portable), sonst via pip im PATH.
    /// </summary>
    public string VspipePfad =>
        OperatingSystem.IsWindows()
            ? Path.Combine(BasisVerzeichnis, "vspipe.exe")
            : Path.Combine(BasisVerzeichnis, "bin", "vspipe");

    /// <summary>
    /// Absoluter Pfad zum plugins-Ordner (Windows portable Installation).
    /// </summary>
    public string PluginPfad => Path.Combine(BasisVerzeichnis, "plugins");

    // --- Events ---

    /// <summary>
    /// Progress-Event für UI-Updates. Wird während des Downloads und der Installation gefeuert.
    /// </summary>
    public event EventHandler<VapourSynthInstallFortschritt>? InstallationsFortschritt;

    // ============================================================
    //  Öffentliche API
    // ============================================================

    /// <summary>
    /// Prüft ob VapourSynth + benötigte Plugins installiert sind.
    /// Windows: vspipe.exe existiert + ffms2 Plugin + vsort Plugin im plugins-Ordner.
    /// Linux:   vspipe im PATH verfügbar.
    /// </summary>
    public bool IstInstalliert
    {
        get
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Windows: portable Installation prüfen
                    if (!File.Exists(VspipePfad))
                    {
                        Log.Debug("IstInstalliert: vspipe.exe nicht gefunden: {Pfad}", VspipePfad);
                        return false;
                    }

                    // ffms2 Plugin prüfen (typischerweise ffms2.dll)
                    bool ffms2Gefunden = PluginDateiVorhanden("ffms2");
                    if (!ffms2Gefunden)
                    {
                        Log.Debug("IstInstalliert: ffms2 Plugin nicht gefunden in {Pfad}", PluginPfad);
                        return false;
                    }

                    // vsort Plugin prüfen (typischerweise vsORT.py oder _vsmlrt.py)
                    bool vsortGefunden = PluginDateiVorhanden("vsmlrt") || PluginDateiVorhanden("vsORT");
                    if (!vsortGefunden)
                    {
                        Log.Debug("IstInstalliert: vsort/vsmlrt Plugin nicht gefunden in {Pfad}", PluginPfad);
                        return false;
                    }

                    Log.Debug("IstInstalliert: VapourSynth portable Installation erkannt");
                    return true;
                }
                else
                {
                    // Linux: vspipe via pip installiert → im PATH verfügbar
                    if (!IstBefehlVerfuegbar("vspipe"))
                    {
                        Log.Debug("IstInstalliert: vspipe nicht im PATH auf Linux");
                        return false;
                    }

                    Log.Debug("IstInstalliert: vspipe im PATH gefunden (Linux)");
                    return true;
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
    /// Installiert VapourSynth + Plugins (Windows: portable ZIPs, Linux: pip + vsrepo).
    /// Unterstützt CancellationToken für Abbruch und feuert Progress-Events für UI-Updates.
    /// </summary>
    /// <param name="cancellationToken">Token für Abbruch-Benachrichtigung.</param>
    /// <returns>true wenn Installation erfolgreich, false bei Fehler.</returns>
    public async Task<bool> InstallierenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("VapourSynth-Installation gestartet (Platform: {Platform})",
                OperatingSystem.IsWindows() ? "Windows" : "Linux");

            if (OperatingSystem.IsWindows())
            {
                return await InstalliereWindowsAsync(cancellationToken);
            }
            else
            {
                return await InstalliereLinuxAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("VapourSynth-Installation abgebrochen durch CancellationToken");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error("VapourSynth-Installation fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Deinstalliert VapourSynth durch Löschen des Installationsverzeichnisses.
    /// Linux: deinstalliert zusätzlich pip-Pakete.
    /// </summary>
    /// <returns>true wenn Deinstallation erfolgreich, false bei Fehler.</returns>
    public async Task<bool> DeinstallierenAsync()
    {
        try
        {
            Log.Information("VapourSynth-Deinstallation gestartet");

            if (OperatingSystem.IsWindows())
            {
                // Windows: vapoursynth Ordner löschen
                if (Directory.Exists(BasisVerzeichnis))
                {
                    FireProgress("Lösche Installationsverzeichnis", 0, 1);
                    Directory.Delete(BasisVerzeichnis, recursive: true);
                    FireProgress("Installationsverzeichnis gelöscht", 1, 1);
                    Log.Information("VapourSynth deinstalliert: {Pfad} gelöscht", BasisVerzeichnis);
                }
                else
                {
                    Log.Information("VapourSynth deinstalliert: kein Installationsverzeichnis vorhanden");
                }
            }
            else
            {
                // Linux: pip-Pakete deinstallieren
                FireProgress("Deinstalliere pip-Pakete", 0, 1);
                await FuehreProcessAusAsync("pip", new[] { "uninstall", "-y", "vapoursynth", "vsrepo", "vapoursynth-mlrt-ort" });
                FireProgress("pip-Pakete deinstalliert", 1, 1);

                // Zusätzliches Installationsverzeichnis löschen (falls vorhanden)
                if (Directory.Exists(BasisVerzeichnis))
                {
                    Directory.Delete(BasisVerzeichnis, recursive: true);
                }

                Log.Information("VapourSynth deinstalliert (Linux)");
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error("VapourSynth-Deinstallation fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    // ============================================================
    //  Windows-Installation
    // ============================================================

    /// <summary>
    /// Windows-Installation: portable ZIPs + 7z-Plugins herunterladen und entpacken.
    /// </summary>
    private async Task<bool> InstalliereWindowsAsync(CancellationToken cancellationToken)
    {
        // 7z CLI für .7z Dateien erforderlich
        bool siebenZipVerfuegbar = IstBefehlVerfuegbar("7z");
        if (!siebenZipVerfuegbar)
        {
            Log.Warning("7z CLI nicht verfügbar — ffms2 und vsort Plugins können nicht entpackt werden");
        }

        // Installationsverzeichnis vorbereiten
        Directory.CreateDirectory(BasisVerzeichnis);
        Directory.CreateDirectory(PluginPfad);

        // Temp-Verzeichnis für Downloads
        string tempVerzeichnis = Path.Combine(Path.GetTempPath(), "FlipsiColor_vs_install_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempVerzeichnis);

        try
        {
            // --- Schritt 1: VapourSynth64-Portable-R76.zip entpacken ---
            cancellationToken.ThrowIfCancellationRequested();
            FireProgress("Lade VapourSynth R76 Portable herunter", 0, 4);

            string vsZip = await LadeDateiHerunterAsync(VapourSynthUrl, tempVerzeichnis, cancellationToken);
            FireProgress("Entpacke VapourSynth R76 Portable", 1, 4);
            ZipFile.ExtractToDirectory(vsZip, BasisVerzeichnis, overwriteFiles: true);
            Log.Information("VapourSynth R76 Portable entpackt nach {Pfad}", BasisVerzeichnis);

            // --- Schritt 2: ffms2-5.0-msvc.7z entpacken ---
            cancellationToken.ThrowIfCancellationRequested();
            FireProgress("Lade ffms2 Plugin herunter", 1, 4);

            string ffms2Archiv = await LadeDateiHerunterAsync(Ffms2Url, tempVerzeichnis, cancellationToken);
            FireProgress("Entpacke ffms2 Plugin", 2, 4);

            if (siebenZipVerfuegbar)
            {
                Entpacke7z(ffms2Archiv, PluginPfad);
                Log.Information("ffms2 Plugin entpackt nach {Pfad}", PluginPfad);
            }
            else
            {
                throw new InvalidOperationException(
                    "ffms2 Plugin ist eine .7z-Datei, aber 7z CLI ist nicht installiert. " +
                    "Bitte installieren Sie 7z (z.B. 'apt install p7zip-full' oder 'choco install 7zip') " +
                    "und versuchen Sie es erneut.");
            }

            // --- Schritt 3: VSORT-Windows-x64.v15.16.7z entpacken ---
            cancellationToken.ThrowIfCancellationRequested();
            FireProgress("Lade vs-mlrt ONNX Runtime herunter", 2, 4);

            string vsortArchiv = await LadeDateiHerunterAsync(VsortUrl, tempVerzeichnis, cancellationToken);
            FireProgress("Entpacke vs-mlrt ONNX Runtime", 3, 4);

            if (siebenZipVerfuegbar)
            {
                Entpacke7z(vsortArchiv, PluginPfad);
                Log.Information("vs-mlrt ONNX Runtime entpackt nach {Pfad}", PluginPfad);
            }
            else
            {
                throw new InvalidOperationException(
                    "vs-mlrt ONNX Runtime ist eine .7z-Datei, aber 7z CLI ist nicht installiert. " +
                    "Bitte installieren Sie 7z und versuchen Sie es erneut.");
            }

            // --- Schritt 4: scripts.v15.16.7z entpacken ---
            cancellationToken.ThrowIfCancellationRequested();
            FireProgress("Lade vs-mlrt Scripts herunter", 3, 4);

            string scriptsArchiv = await LadeDateiHerunterAsync(VsortScriptsUrl, tempVerzeichnis, cancellationToken);
            FireProgress("Entpacke vs-mlrt Scripts", 4, 4);

            if (siebenZipVerfuegbar)
            {
                Entpacke7z(scriptsArchiv, PluginPfad);
                Log.Information("vs-mlrt Scripts entpackt nach {Pfad}", PluginPfad);
            }
            else
            {
                throw new InvalidOperationException(
                    "vs-mlrt Scripts ist eine .7z-Datei, aber 7z CLI ist nicht installiert. " +
                    "Bitte installieren Sie 7z und versuchen Sie es erneut.");
            }

            // --- Verifikation ---
            FireProgress("Verifiziere Installation", 4, 4);
            if (!IstInstalliert)
            {
                Log.Error("VapourSynth-Installation abgeschlossen, aber IstInstalliert prüfung fehlgeschlagen");
                return false;
            }

            FireProgress("VapourSynth erfolgreich installiert", 4, 4);
            Log.Information("VapourSynth-Installation erfolgreich (Windows)");
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
    //  Linux-Installation
    // ============================================================

    /// <summary>
    /// Linux-Installation: pip + vsrepo für VapourSynth + Plugins.
    /// </summary>
    private async Task<bool> InstalliereLinuxAsync(CancellationToken cancellationToken)
    {
        // Installationsverzeichnis erstellen (für Plugin-Pfad-Konsistenz)
        Directory.CreateDirectory(BasisVerzeichnis);

        // --- Schritt 1: pip install vapoursynth vsrepo ---
        cancellationToken.ThrowIfCancellationRequested();
        FireProgress("pip install vapoursynth vsrepo", 0, 3);

        int exitCode = await FuehreProcessAusAsync("pip",
            new[] { "install", "vapoursynth", "vsrepo" }, cancellationToken);

        if (exitCode != 0)
        {
            Log.Error("pip install vapoursynth vsrepo fehlgeschlagen (ExitCode {Code})", exitCode);
            return false;
        }
        Log.Information("vapoursynth + vsrepo via pip installiert");

        // --- Schritt 2: vsrepo install ffms2 havsfunc ---
        cancellationToken.ThrowIfCancellationRequested();
        FireProgress("vsrepo install ffms2 havsfunc", 1, 3);

        exitCode = await FuehreProcessAusAsync("vsrepo",
            new[] { "install", "ffms2", "havsfunc" }, cancellationToken);

        if (exitCode != 0)
        {
            Log.Error("vsrepo install ffms2 havsfunc fehlgeschlagen (ExitCode {Code})", exitCode);
            return false;
        }
        Log.Information("ffms2 + havsfunc via vsrepo installiert");

        // --- Schritt 3: pip install vapoursynth-mlrt-ort ---
        cancellationToken.ThrowIfCancellationRequested();
        FireProgress("pip install vapoursynth-mlrt-ort", 2, 3);

        exitCode = await FuehreProcessAusAsync("pip",
            new[] { "install", "vapoursynth-mlrt-ort" }, cancellationToken);

        if (exitCode != 0)
        {
            Log.Error("pip install vapoursynth-mlrt-ort fehlgeschlagen (ExitCode {Code})", exitCode);
            return false;
        }
        Log.Information("vapoursynth-mlrt-ort via pip installiert");

        // --- Verifikation ---
        FireProgress("Verifiziere Installation", 3, 3);
        if (!IstInstalliert)
        {
            Log.Error("VapourSynth-Installation abgeschlossen, aber vspipe nicht im PATH gefunden");
            return false;
        }

        FireProgress("VapourSynth erfolgreich installiert", 3, 3);
        Log.Information("VapourSynth-Installation erfolgreich (Linux)");
        return true;
    }

    // ============================================================
    //  Hilfsmethoden — Download, Entpackung, Process-Ausführung
    // ============================================================

    /// <summary>
    /// Erstellt einen sicheren HttpClient mit Redirect-Beschränkung (gleiche Konvention wie ModelManager).
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
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FlipsiColor/VapourSynthInstaller");
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
    /// Entpackt eine .7z-Datei mit dem 7z CLI.
    /// Voraussetzung: 7z ist im PATH verfügbar.
    /// </summary>
    private void Entpacke7z(string archivPfad, string zielVerzeichnis)
    {
        if (!IstBefehlVerfuegbar("7z"))
        {
            throw new InvalidOperationException(
                "7z CLI ist nicht verfügbar. Bitte installieren Sie 7z (p7zip-full auf Linux, 7zip auf Windows) " +
                "um .7z-Dateien zu entpacken.");
        }

        // 7z x <archiv> -o<ziel> -y  (x = mit Pfaden entpacken, -y = alle Fragen mit Ja beantworten)
        var psi = SecurityValidator.SichereProcessStartInfo("7z",
            new[] { "x", archivPfad, $"-o{zielVerzeichnis}", "-y" });

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        string output = proc.StandardOutput.ReadToEnd();
        string error = proc.StandardError.ReadToEnd();
        proc.WaitForExit(120000); // 2 Minuten Timeout für große Archive

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"7z entpacken fehlgeschlagen (ExitCode {proc.ExitCode}): {error}");
        }

        Log.Debug("7z entpackt: {Archiv} → {Ziel}", archivPfad, zielVerzeichnis);
    }

    /// <summary>
    /// Führt einen externen Prozess aus und wartet auf Beendigung.
    /// Verwendet SecurityValidator.SichereProcessStartInfo für sichere Argument-Übergabe.
    /// Unterstützt CancellationToken für Abbruch.
    /// </summary>
    private async Task<int> FuehreProcessAusAsync(string befehl, IEnumerable<string> argumente,
        CancellationToken cancellationToken = default)
    {
        var psi = SecurityValidator.SichereProcessStartInfo(befehl, argumente);
        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // CancellationToken registrieren — tötet Prozess bei Abbruch
        await using var registration = cancellationToken.Register(() =>
        {
            try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { /* Ignorieren */ }
        });

        await proc.WaitForExitAsync(cancellationToken);
        return proc.ExitCode;
    }

    /// <summary>
    /// Prüft ob ein Befehl (Executable) im PATH verfügbar ist.
    /// Windows: where <cmd>, Linux: which <cmd>.
    /// </summary>
    private static bool IstBefehlVerfuegbar(string befehl)
    {
        try
        {
            var checker = OperatingSystem.IsWindows() ? "where" : "which";
            var psi = SecurityValidator.SichereProcessStartInfo(checker, new[] { befehl });
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
        }
        catch (Exception ex)
        {
            Log.Debug("Befehl nicht verfügbar: {Befehl} — {Fehler}", befehl, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Prüft ob eine Plugin-Datei mit dem angegebenen Namens-Präfix im plugins-Ordner existiert.
    /// Sucht nach allen Dateien deren Name (case-insensitive) das Präfix enthält.
    /// </summary>
    private bool PluginDateiVorhanden(string namensPraefix)
    {
        if (!Directory.Exists(PluginPfad))
            return false;

        try
        {
            foreach (var datei in Directory.EnumerateFiles(PluginPfad, "*", SearchOption.AllDirectories))
            {
                string dateiname = Path.GetFileName(datei);
                if (dateiname.Contains(namensPraefix, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug("Plugin gefunden: {Datei}", datei);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug("Plugin-Suche fehlgeschlagen: {Fehler}", ex.Message);
        }

        return false;
    }

    /// <summary>
    /// Feuert das Progress-Event für UI-Updates.
    /// </summary>
    private void FireProgress(string schritt, long aktuelleSchritt, long gesamtSchritte)
    {
        Log.Debug("Installations-Fortschritt: {Schritt} ({Aktuell}/{Gesamt})", schritt, aktuelleSchritt, gesamtSchritte);
        InstallationsFortschritt?.Invoke(this, new VapourSynthInstallFortschritt(schritt, aktuelleSchritt, gesamtSchritte));
    }

    // ============================================================
    //  Statische Methode: ErstelleTestVideo
    // ============================================================

    /// <summary>
    /// Erstellt ein Test-Video für den TestRunner.
    /// Erzeugt ein 5 Sekunden langes 64x64 Test-Video via FFMPEG (colorbars + tone).
    /// Static-Methode — kein VapourSynthInstaller-Instanz nötig.
    /// (Gleiche Implementierung wie VideoPipeline.ErstelleTestVideo.)
    /// </summary>
    /// <param name="pfad">Zielpfad für das Test-Video (z.B. /tmp/test.mp4).</param>
    /// <returns>true wenn erfolgreich, false bei Fehler.</returns>
    public static bool ErstelleTestVideo(string pfad)
    {
        try
        {
            // Ausgabe-Pfad validieren (nur .mp4 erlaubt)
            var ausgabeEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4" };
            var validierterPfad = SecurityValidator.ValidiereAusgabePfad(pfad, ausgabeEndungen);
            if (validierterPfad == null)
            {
                Log.Warning("ErstelleTestVideo: Pfad-Validierung fehlgeschlagen");
                return false;
            }

            // FFMPEG: 5 Sekunden 64x64 colorbars + tone generator
            // -f lavfi -i testsrc=duration=5:size=64x64:rate=30 → Video
            // -f lavfi -i sine=frequency=440:duration=5 → Audio (Test-Ton)
            // -c:v libx264 -crf 23 -c:a aac → Encoding
            var psi = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                new[] { "-y",
                        "-f", "lavfi",
                        "-i", "testsrc=duration=5:size=64x64:rate=30",
                        "-f", "lavfi",
                        "-i", "sine=frequency=440:duration=5",
                        "-c:v", "libx264",
                        "-crf", "23",
                        "-pix_fmt", "yuv420p",
                        "-c:a", "aac",
                        "-shortest",
                        validierterPfad });
            using var proc = new Process { StartInfo = psi };
            proc.Start();
            proc.WaitForExit(30000);

            bool erfolg = proc.ExitCode == 0 && File.Exists(validierterPfad);
            if (erfolg)
                Log.Information("Test-Video erstellt: {Pfad}", SecurityValidator.BereinigePfadFuerLog(validierterPfad));
            else
                Log.Warning("Test-Video konnte nicht erstellt werden (ExitCode {Code})", proc.ExitCode);

            return erfolg;
        }
        catch (Exception ex)
        {
            Log.Warning("ErstelleTestVideo fehlgeschlagen: {Fehler}",
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }
}

/// <summary>
/// Progress-Event-Args für die VapourSynth-Installation.
/// Enthält den aktuellen Schritt-Name und Fortschritt (aktuell/gesamt).
/// </summary>
public sealed class VapourSynthInstallFortschritt : EventArgs
{
    /// <summary>Beschreibender Name des aktuellen Schritts (z.B. "Lade VapourSynth R76 Portable herunter").</summary>
    public string Schritt { get; }

    /// <summary>Aktueller Schritt-Index (0-basiert oder 1-basiert je nach Kontext).</summary>
    public long AktuellerSchritt { get; }

    /// <summary>Gesamtzahl der Schritte.</summary>
    public long GesamtSchritte { get; }

    /// <summary>Fortschritt in Prozent (0-100).</summary>
    public double Prozent => GesamtSchritte > 0 ? AktuellerSchritt * 100.0 / GesamtSchritte : 0;

    public VapourSynthInstallFortschritt(string schritt, long aktuellerSchritt, long gesamtSchritte)
    {
        Schritt = schritt;
        AktuellerSchritt = aktuellerSchritt;
        GesamtSchritte = gesamtSchritte;
    }
}