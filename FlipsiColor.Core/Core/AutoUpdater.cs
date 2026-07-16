using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Security.Cryptography;
using System.Threading.Tasks;

using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.Core;

/// <summary>
/// Auto-Updater — GitHub Releases API, Downgrade-Schutz, Beta/Stable Kanal.
/// FIX #3: URL-Validierung (HTTPS), Redirect-Beschränkung, sicherer Download.
/// FIX #10: Thread-Sicherheit und Schutz gegen parallele Updates.
/// </summary>
public sealed class AutoUpdater : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<AutoUpdater>();
    // FIX #3: HttpClient mit Redirect-Beschränkung
    private static readonly HttpClient _http = CreateSafeHttpClient();
    private readonly object _updateLock = new(); // FIX #10: Schutz gegen parallele Updates
    private System.Threading.Timer? _pruefTimer;
    private bool _disposed;
    private bool _updateLaeuft; // FIX #10: Flag gegen doppelte Update-Ausführung

    private const string GitHubApiUrl = "https://api.github.com/repos/TechFlipsi/FlipsiColor/releases";

    public bool UpdateVerfuegbar { get; private set; }
    public string NeueVersion { get; private set; } = "";
    public string Aenderungen { get; private set; } = "";
    public string DownloadUrl { get; private set; } = "";
    public long DownloadGroesse { get; private set; }
    public UpdateKanal Kanal { get; set; } = UpdateKanal.Stable;

    public bool AutoUpdate { get; set; } = false; // Wenn true: Update wird automatisch installiert ohne User-Klick

    public event EventHandler<bool>? UpdateVerfuegbarChanged;
    public event EventHandler<string>? NeueVersionChanged;
    public event EventHandler<string>? FehlerAufgetreten;
    public event EventHandler<(long empfangen, long gesamt)>? DownloadFortschritt;

    private readonly Version _aktuelleVersion;
    private string? _ignorierteVersion;

    public AutoUpdater()
    {
        // FIX v0.7.0: Assembly.GetEntryAssembly() gibt bei SingleFile-Publish null/0.0.0.0 zurück
        var versionStr = "0.7.0";
        try
        {
            var asmVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            if (asmVersion != null && asmVersion.Major > 0)
                versionStr = asmVersion.ToString(3);
        }
        catch { }
        _aktuelleVersion = new Version(versionStr);

        // Ignorierte Version aus Settings laden
        try
        {
            var settings = Settings.Laden();
            if (!string.IsNullOrEmpty(settings.IgnorierteUpdateVersion))
                _ignorierteVersion = settings.IgnorierteUpdateVersion;
        }
        catch (Exception ex)
        {
            Log.Warning("Ignorierte Version konnte nicht aus Settings geladen werden: {Fehler}", ex.Message);
        }

        // Erste Prüfung in 30s, dann alle 24h
        _pruefTimer = new System.Threading.Timer(_ => Pruefen(), null, TimeSpan.FromSeconds(30), TimeSpan.FromHours(24));
        Log.Information("AutoUpdater initialisiert. Aktuell: v{Version}. Prüfung in 30s.", _aktuelleVersion);
    }

    /// <summary>
    /// Erstellt einen sicheren HttpClient mit Redirect-Beschränkung.
    /// FIX #3: Blockiert Redirects zu file:// oder http://.
    /// </summary>
    private static HttpClient CreateSafeHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        return new HttpClient(handler, disposeHandler: true);
    }

    public void Pruefen()
    {
        _ = PruefenAsync();
    }

    public async Task PruefenAsync()
    {
        try
        {
            var url = Kanal == UpdateKanal.Beta
                ? $"{GitHubApiUrl}?per_page=10"
                : GitHubApiUrl;

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd($"FlipsiColor/{_aktuelleVersion}");

            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var releases = doc.RootElement.EnumerateArray();

            bool gefunden = false;
            foreach (var release in releases)
            {
                bool istPreRelease = release.GetProperty("prerelease").GetBoolean();
                if (Kanal == UpdateKanal.Stable && istPreRelease)
                    continue;

                var tagStr = release.GetProperty("tag_name").GetString() ?? "";
                if (tagStr.StartsWith('v')) tagStr = tagStr[1..];

                if (!Version.TryParse(tagStr, out var releaseVersion))
                    continue;

                // Downgrade-Schutz
                if (releaseVersion < _aktuelleVersion)
                {
                    Log.Warning("DOWNGRADE BLOCKIERT: v{Neu} < v{Aktuell}", releaseVersion, _aktuelleVersion);
                    continue;
                }

                if (releaseVersion <= _aktuelleVersion)
                    continue;

                // Ignorierte Version überspringen
                if (tagStr == _ignorierteVersion)
                {
                    Log.Information("Version v{Version} wird ignoriert (User-Entscheidung)", tagStr);
                    continue;
                }

                // Passenden Asset finden
                string? releaseUrl = null;
                long groesse = 0;
                var assets = release.GetProperty("assets").EnumerateArray();
                foreach (var asset in assets)
                {
                    var name = asset.GetProperty("name").GetString()?.ToLowerInvariant() ?? "";
                    if (name.Contains("setup") || name.Contains("installer") || name.EndsWith(".exe"))
                    {
                        releaseUrl = asset.GetProperty("browser_download_url").GetString();
                        groesse = asset.GetProperty("size").GetInt64();
                        break;
                    }
                }

                // FIX #3: Download-URL validieren — nur HTTPS erlaubt
                if (!string.IsNullOrEmpty(releaseUrl) && !SecurityValidator.ValidiereDownloadUrl(releaseUrl))
                {
                    Log.Warning("Update-Download-URL ungültig (nicht HTTPS oder private IP): {Url}", releaseUrl);
                    releaseUrl = null;
                }

                UpdateVerfuegbar = true;
                NeueVersion = tagStr;
                Aenderungen = release.GetProperty("body").GetString() ?? "";
                DownloadUrl = releaseUrl ?? "";
                DownloadGroesse = groesse;

                Log.Information("Update gefunden: v{Neu} (aktuell: v{Aktuell})", tagStr, _aktuelleVersion);
                UpdateVerfuegbarChanged?.Invoke(this, true);
                NeueVersionChanged?.Invoke(this, tagStr);
                gefunden = true;

                // Auto-Update: Wenn aktiviert, automatisch herunterladen und installieren
                if (AutoUpdate && !string.IsNullOrEmpty(DownloadUrl))
                {
                    Log.Information("Auto-Update aktiviert — Starte automatischen Download...");
                    _ = UpdateStartenAsync();
                }
                break;
            }

            if (!gefunden)
            {
                UpdateVerfuegbar = false;
                Log.Information("Kein Update verfügbar");
                UpdateVerfuegbarChanged?.Invoke(this, false);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Update-Prüfung fehlgeschlagen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            FehlerAufgetreten?.Invoke(this, SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }
    }

    public void UpdateStarten()
    {
        if (string.IsNullOrEmpty(DownloadUrl))
        {
            FehlerAufgetreten?.Invoke(this, "Kein Download-Link verfügbar");
            return;
        }

        // FIX #3: Download-URL erneut validieren vor dem Start
        if (!SecurityValidator.ValidiereDownloadUrl(DownloadUrl))
        {
            FehlerAufgetreten?.Invoke(this, "Download-URL ungültig (nicht HTTPS)");
            return;
        }

        // Downgrade-Schutz
        if (!Version.TryParse(NeueVersion, out var ziel) || ziel <= _aktuelleVersion)
        {
            var msg = $"DOWNGRADE BLOCKIERT: v{NeueVersion} ≤ v{_aktuelleVersion}";
            Log.Warning(msg);
            FehlerAufgetreten?.Invoke(this, msg);
            return;
        }

        // FIX #10: Schutz gegen parallele Update-Ausführung
        lock (_updateLock)
        {
            if (_updateLaeuft)
            {
                Log.Warning("Update läuft bereits — UpdateStarten ignoriert");
                return;
            }
            _updateLaeuft = true;
        }

        _ = UpdateStartenAsync();
    }

    private async Task UpdateStartenAsync()
    {
        try
        {
            // FIX: Eindeutigen Temp-Dateinamen verwenden — verhindert Kollisionen
            var tempPfad = Path.Combine(Path.GetTempPath(), $"FlipsiColor-Update-{NeueVersion}-{Guid.NewGuid():N}.exe");

            Log.Information("Lade Update herunter: v{Version}", NeueVersion);

            // FIX #3: Download mit Redirect-Validierung
            using var response = await _http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // FIX #3: Antwort-URL nach Redirect prüfen — nur HTTPS
            if (response.RequestMessage?.RequestUri is { } finalUri && finalUri.Scheme != Uri.UriSchemeHttps)
            {
                Log.Error("Update-Download abgelehnt: Redirect zu nicht-HTTPS ({Schema})", finalUri.Scheme);
                FehlerAufgetreten?.Invoke(this, "Redirect zu nicht-HTTPS blockiert");
                return;
            }

            // FIX: Datei als Stream herunterladen (nicht GetByteArrayAsync — verhindert OOM bei großen Updates)
            await using var content = await response.Content.ReadAsStreamAsync();
            await using var file = new FileStream(tempPfad, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            int gelesen;
            long empfangen = 0;
            long gesamt = response.Content.Headers.ContentLength ?? DownloadGroesse;
            while ((gelesen = await content.ReadAsync(buffer.AsMemory())) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, gelesen));
                empfangen += gelesen;
                DownloadFortschritt?.Invoke(this, (empfangen, gesamt));
            }
            await file.FlushAsync();

            // SHA256 loggen (Information-Disclosure-Schutz: nur Hash, nicht Pfad)
            var hash = await Utils.Crypto.Sha256FileAsync(tempPfad);
            Log.Information("Download SHA256: {Hash}", hash);

            // FIX #10: SHA256-Verifikation würde hier erfolgen — derzeit nur Logging
            // In einer Produktionsumgebung sollte der erwartete Hash aus der Release-API kommen

            // FIX v0.7.0: UseShellExecute=true + Verb=runas für UAC-Prompt (PrivilegesRequired=admin seit v0.5.5)
            Process.Start(new ProcessStartInfo
            {
                FileName = tempPfad,
                Arguments = "/SILENT /NOCANCEL /NORESTART",
                UseShellExecute = true,
                Verb = "runas"
            });

            // Anwendung beenden — plattformneutral via Environment.Exit
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Log.Error("Update-Download fehlgeschlagen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            FehlerAufgetreten?.Invoke(this, SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
        }
        finally
        {
            // FIX #10: Update-Flag zurücksetzen
            lock (_updateLock)
            {
                _updateLaeuft = false;
            }
        }
    }

    public void SpaeterErinnern()
    {
        _pruefTimer?.Change(TimeSpan.FromHours(4), TimeSpan.FromHours(24));
        UpdateVerfuegbar = false;
        UpdateVerfuegbarChanged?.Invoke(this, false);
        Log.Information("Erinnerung in 4 Stunden");
    }

    public void Ignorieren()
    {
        _ignorierteVersion = NeueVersion;
        // Ignorierte Version in Settings speichern
        try
        {
            var settings = Settings.Laden();
            settings.IgnorierteUpdateVersion = NeueVersion;
            settings.Speichern();
        }
        catch (Exception ex)
        {
            Log.Warning("Ignorierte Version konnte nicht gespeichert werden: {Fehler}", ex.Message);
        }
        UpdateVerfuegbar = false;
        UpdateVerfuegbarChanged?.Invoke(this, false);
        Log.Information("Version v{Version} wird ignoriert", NeueVersion);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pruefTimer?.Dispose();
        // _http ist static und wird NICHT pro-Instanz disposed
        // (siehe ModelManager-Kommentar — mehrere Instanzen teilen sich denselben HttpClient)
    }
}

public enum UpdateKanal
{
    Stable,
    Beta
}