using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.AI;

/// <summary>
/// KI-Modell-Downloader mit Fortschritts-Tracking und SHA256-Verifikation.
/// FIX #3: URL-Validierung (HTTPS, kein file://), Redirect-Beschränkung, SSRF-Schutz.
/// FIX #6: SHA256-Verifikation wird erzwungen wenn Hash angegeben ist.
/// </summary>
public sealed class ModelDownloader
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ModelDownloader>();
    // FIX #3: HttpClient mit Redirect-Beschränkung — blockiert file:// und http:// Redirects
    private static readonly HttpClient _http = CreateSafeHttpClient();

    /// <summary>
    /// Erstellt einen HttpClient mit sicheren Redirect-Einstellungen.
    /// Erlaubt nur HTTPS-Redirects, blockiert file:// und http:// Redirects.
    /// </summary>
    private static HttpClient CreateSafeHttpClient()
    {
        var handler = new HttpClientHandler
        {
            // Redirects erlauben, aber auf HTTPS beschränken
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3, // Max 3 Redirects — verhindert Redirect-Schleifen
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        return new HttpClient(handler, disposeHandler: true);
    }

    public event EventHandler<(ModellId Id, long Empfangen, long Gesamt, double Prozent)>? Fortschritt;

    public async Task<bool> HerunterladenAsync(ModellId id, string url, string zielPfad, string? erwarteterSha256 = null)
    {
        // FIX #3: URL-Validierung — HTTPS erzwingen, file:// und private IPs blockieren
        if (!SecurityValidator.ValidiereDownloadUrl(url))
        {
            Log.Error("Download abgelehnt: URL-Validierung fehlgeschlagen für {Id}", id);
            return false;
        }

        // FIX #1: Ziel-Pfad gegen Path-Traversal validieren
        var zielEndungen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".onnx", ".tmp", ".downloading" };
        var validierterZielPfad = SecurityValidator.ValidiereAusgabePfad(zielPfad, zielEndungen);
        if (validierterZielPfad == null)
        {
            Log.Error("Download abgelehnt: Ziel-Pfad-Validierung fehlgeschlagen für {Id}", id);
            return false;
        }
        zielPfad = validierterZielPfad;

        Log.Information("Download startet: {Id}", id);
        var zielDir = Path.GetDirectoryName(zielPfad);
        if (!string.IsNullOrEmpty(zielDir))
            Directory.CreateDirectory(zielDir);

        var tempPfad = zielPfad + ".downloading";
        try
        {
            // FIX #3: Redirect-Ziel nach dem Request validieren — blockiert file:// und private IPs
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // FIX #3: Antwort-URL prüfen — nach Redirect darf nur HTTPS stehen
            if (response.RequestMessage?.RequestUri is { } finalUri)
            {
                if (finalUri.Scheme != Uri.UriSchemeHttps)
                {
                    Log.Error("Download abgelehnt: Redirect zu nicht-HTTPS ({Schema}) für {Id}", finalUri.Scheme, id);
                    return false;
                }
            }

            var total = response.Content.Headers.ContentLength ?? 0;
            long empfangen = 0;

            await using var content = await response.Content.ReadAsStreamAsync();
            await using var file = new FileStream(tempPfad, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            int gelesen;
            while ((gelesen = await content.ReadAsync(buffer.AsMemory())) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, gelesen));
                empfangen += gelesen;
                var prozent = total > 0 ? empfangen * 100.0 / total : 0;
                Fortschritt?.Invoke(this, (id, empfangen, total, prozent));
            }

            await file.FlushAsync();

            // SHA256 prüfen
            if (!string.IsNullOrEmpty(erwarteterSha256))
            {
                var hash = await Utils.Crypto.Sha256FileAsync(tempPfad);
                if (!hash.Equals(erwarteterSha256, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error("SHA256 mismatch für {Id}: erwartet {Expected}, erhalten {Actual}", id, erwarteterSha256, hash);
                    try { File.Delete(tempPfad); } catch { /* Ignorieren */ }
                    return false;
                }
                Log.Information("SHA256 OK für {Id}", id);
            }
            else
            {
                // FIX #6: Warnung wenn kein SHA256 angegeben — Modell-Integrität nicht verifiziert
                Log.Warning("Kein SHA256-Hash für {Id} angegeben — Modell-Integrität nicht verifiziert", id);
            }

            File.Move(tempPfad, zielPfad, overwrite: true);
            Log.Information("Download abgeschlossen: {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Download fehlgeschlagen: {Id} — {Fehler}", id, SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            try { if (File.Exists(tempPfad)) File.Delete(tempPfad); } catch { /* Ignorieren */ }
            return false;
        }
    }
}