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

namespace FlipsiColor.Core;

/// <summary>
/// Auto-Updater — GitHub Releases API, Downgrade-Schutz, Beta/Stable Kanal
/// </summary>
public sealed class AutoUpdater : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<AutoUpdater>();
    private readonly HttpClient _http = new();
    private Timer? _pruefTimer;
    private bool _disposed;

    private const string GitHubApiUrl = "https://api.github.com/repos/TechFlipsi/FlipsiColor/releases";

    public bool UpdateVerfuegbar { get; private set; }
    public string NeueVersion { get; private set; } = "";
    public string Aenderungen { get; private set; } = "";
    public string DownloadUrl { get; private set; } = "";
    public long DownloadGroesse { get; private set; }
    public UpdateKanal Kanal { get; set; } = UpdateKanal.Stable;

    public event EventHandler<bool>? UpdateVerfuegbarChanged;
    public event EventHandler<string>? NeueVersionChanged;
    public event EventHandler<string>? FehlerAufgetreten;

    private readonly Version _aktuelleVersion;
    private string? _ignorierteVersion;

    public AutoUpdater()
    {
        var versionStr = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.2.0";
        _aktuelleVersion = new Version(versionStr);

        // Ignorierte Version laden
        // TODO: Aus Settings laden

        // Erste Prüfung in 30s, dann alle 24h
        _pruefTimer = new System.Threading.Timer(_ => Pruefen(), null, TimeSpan.FromSeconds(30), TimeSpan.FromHours(24));
        Log.Information("AutoUpdater initialisiert. Aktuell: v{Version}. Prüfung in 30s.", _aktuelleVersion);
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

            var response = await _http.SendAsync(request);
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

                UpdateVerfuegbar = true;
                NeueVersion = tagStr;
                Aenderungen = release.GetProperty("body").GetString() ?? "";
                DownloadUrl = releaseUrl ?? "";
                DownloadGroesse = groesse;

                Log.Information("Update gefunden: v{Neu} (aktuell: v{Aktuell})", tagStr, _aktuelleVersion);
                UpdateVerfuegbarChanged?.Invoke(this, true);
                NeueVersionChanged?.Invoke(this, tagStr);
                gefunden = true;
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
            Log.Error(ex, "Update-Prüfung fehlgeschlagen");
            FehlerAufgetreten?.Invoke(this, ex.Message);
        }
    }

    public void UpdateStarten()
    {
        if (string.IsNullOrEmpty(DownloadUrl))
        {
            FehlerAufgetreten?.Invoke(this, "Kein Download-Link verfügbar");
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

        _ = UpdateStartenAsync();
    }

    private async Task UpdateStartenAsync()
    {
        try
        {
            var tempPfad = Path.Combine(Path.GetTempPath(), $"FlipsiColor-Update-{NeueVersion}.exe");

            Log.Information("Lade Update herunter: {Url}", DownloadUrl);
            var data = await _http.GetByteArrayAsync(DownloadUrl);
            await File.WriteAllBytesAsync(tempPfad, data);

            // SHA256 loggen
            var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            Log.Information("Download SHA256: {Hash}", hash);

            // Installer starten und App beenden
            Process.Start(new ProcessStartInfo
            {
                FileName = tempPfad,
                Arguments = "/S",
                UseShellExecute = false
            });

            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Update-Download fehlgeschlagen");
            FehlerAufgetreten?.Invoke(this, ex.Message);
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
        // TODO: In Settings speichern
        UpdateVerfuegbar = false;
        UpdateVerfuegbarChanged?.Invoke(this, false);
        Log.Information("Version v{Version} wird ignoriert", NeueVersion);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pruefTimer?.Dispose();
        _http.Dispose();
    }
}

public enum UpdateKanal
{
    Stable,
    Beta
}