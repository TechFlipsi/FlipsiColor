using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using FlipsiColor.Utils;

namespace FlipsiColor.AI;

/// <summary>
/// KI-Modell-Downloader mit Fortschritts-Tracking und SHA256-Verifikation
/// </summary>
public sealed class ModelDownloader
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ModelDownloader>();
    private readonly HttpClient _http = new();

    public event EventHandler<(ModellId Id, long Empfangen, long Gesamt, double Prozent)>? Fortschritt;

    public async Task<bool> HerunterladenAsync(ModellId id, string url, string zielPfad, string? erwarteterSha256 = null)
    {
        Log.Information("Download startet: {Id} → {Ziel}", id, zielPfad);
        Directory.CreateDirectory(Path.GetDirectoryName(zielPfad)!);

        var tempPfad = zielPfad + ".downloading";
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

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
                    File.Delete(tempPfad);
                    return false;
                }
                Log.Information("SHA256 OK für {Id}", id);
            }

            File.Move(tempPfad, zielPfad, overwrite: true);
            Log.Information("Download abgeschlossen: {Id}", id);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Download fehlgeschlagen: {Id}", id);
            if (File.Exists(tempPfad)) File.Delete(tempPfad);
            return false;
        }
    }
}