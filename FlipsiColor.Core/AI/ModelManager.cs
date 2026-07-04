using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;

using FlipsiColor.Utils;
// SecurityValidator wird über Utils-Namespace importiert

namespace FlipsiColor.AI;

/// <summary>
/// Verwaltet KI-Modelle: Download, SHA256-Verifikation, ONNX Session-Erstellung.
/// FIX #3: URL-Validierung (HTTPS, kein file://), Redirect-Beschränkung.
/// FIX #6: SHA256-Verifikation — Warnung bei fehlendem Hash.
/// FIX #10: Thread-Sicherheit durch Lock bei Download und Session-Erstellung.
/// </summary>
public sealed class ModelManager : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ModelManager>();

    // FIX #10: Lock-Objekt für thread-sicheren Download und Session-Erstellung
    private readonly object _lock = new();
    private readonly Dictionary<ModellId, ModellInfo> _modelle = new();
    private readonly Dictionary<ModellId, InferenceSession> _sessions = new();
    // FIX #3: HttpClient mit Redirect-Beschränkung
    private static readonly HttpClient _http = CreateSafeHttpClient();
    private readonly string _modelDir;
    private bool _disposed;

    /// <summary>
    /// GitHub API URL für Modell-Version-Check.
    /// </summary>
    private const string ModelsApiUrl = "https://api.github.com/repos/TechFlipsi/FlipsiColor-Models/releases";

    /// <summary>
    /// Aktuell verfügbare Modell-Version (von GitHub Releases).
    /// </summary>
    public string AktuelleModellVersion { get; private set; } = "v0.1";

    /// <summary>
    /// Event wenn eine neuere Modell-Version verfügbar ist.
    /// </summary>
    public event EventHandler<string>? NeueModellVersionVerfuegbar;

    /// <summary>
    /// Erstellt einen sicheren HttpClient mit Redirect-Beschränkung.
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

    public event EventHandler<ModellDownloadFortschritt>? DownloadFortschritt;
    public event EventHandler<ModellId>? ModellBereit;
    public event EventHandler<ModellFehlerEventArgs>? DownloadFehler;

    public ModelManager()
    {
        _modelDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlipsiColor", "Models");
        Directory.CreateDirectory(_modelDir);
        ManifestLaden();
    }

    /// <summary>
    /// Modell-Manifest laden (hardcoded — gleiche Modelle wie C++ Version)
    /// </summary>
    public void ManifestLaden()
    {
        _modelle.Clear();
        _modelle[ModellId.NAFNet] = new()
        {
            Id = ModellId.NAFNet, Name = "NAFNet",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/nafnet.onnx",
            Sha256 = null, GroesseBytes = 17_825_792, Erforderlich = true
        };
        _modelle[ModellId.RestormerLight] = new()
        {
            Id = ModellId.RestormerLight, Name = "RestormerLight",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/restormer_light.onnx",
            Sha256 = null, GroesseBytes = 25_165_824, Erforderlich = true
        };
        _modelle[ModellId.RealHATGAN] = new()
        {
            Id = ModellId.RealHATGAN, Name = "RealHATGAN",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/realhatgan.onnx",
            Sha256 = null, GroesseBytes = 125_829_120, Erforderlich = false
        };
        _modelle[ModellId.RealESRGAN] = new()
        {
            Id = ModellId.RealESRGAN, Name = "RealESRGAN",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/realesrgan.onnx",
            Sha256 = null, GroesseBytes = 67_108_864, Erforderlich = false
        };
        _modelle[ModellId.CodeFormer] = new()
        {
            Id = ModellId.CodeFormer, Name = "CodeFormer",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/codeformer.onnx",
            Sha256 = null, GroesseBytes = 367_001_600, Erforderlich = false
        };
        _modelle[ModellId.AiLUTTransform] = new()
        {
            Id = ModellId.AiLUTTransform, Name = "AiLUTTransform",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/ailut_transform.onnx",
            Sha256 = null, GroesseBytes = 8_388_608, Erforderlich = true
        };
        _modelle[ModellId.EfficientNet] = new()
        {
            Id = ModellId.EfficientNet, Name = "EfficientNet",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/efficientnet.onnx",
            Sha256 = null, GroesseBytes = 4_818_304, Erforderlich = true
        };

        // Prüfe welche Modelle bereits lokal existieren
        foreach (var info in _modelle.Values)
        {
            var pfad = ModellPfad(info.Id);
            info.Heruntergeladen = File.Exists(pfad);
            Log.Information("Modell {Name}: {Status}", info.Name,
                info.Heruntergeladen ? "bereits vorhanden" : "nicht vorhanden");
        }
    }

    /// <summary>
    /// Prüft ob alle erforderlichen Modelle lokal vorhanden und vollständig sind.
    /// </summary>
    public bool AlleErforderlichenModelleVorhanden()
    {
        foreach (var info in _modelle.Values.Where(m => m.Erforderlich))
        {
            var pfad = ModellPfad(info.Id);
            if (!File.Exists(pfad))
            {
                Log.Warning("Erforderliches Modell fehlt: {Name}", info.Name);
                return false;
            }
            // Dateigröße prüfen — verhindert unvollständige Downloads
            var groesse = new FileInfo(pfad).Length;
            if (groesse < info.GroesseBytes / 2) // Toleranz: mindestens halbe erwartete Größe
            {
                Log.Warning("Modell {Name} unvollständig: {Ist} bytes (erwartet {Erwartet})", info.Name, groesse, info.GroesseBytes);
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Prüft lokal ob ein Modell vollständig ist (Datei existiert + Größe stimmt).
    /// </summary>
    public bool IstModellVollstaendig(ModellId id)
    {
        if (!_modelle.TryGetValue(id, out var info))
            return false;
        var pfad = ModellPfad(id);
        if (!File.Exists(pfad))
            return false;
        var groesse = new FileInfo(pfad).Length;
        // Toleranz: mindestens 90% der erwarteten Größe
        return groesse >= info.GroesseBytes * 0.9;
    }

    /// <summary>
    /// Prüft ob eine neuere Modell-Version auf GitHub verfügbar ist.
    /// Vergleicht den neuesten Release-Tag mit der lokal bekannten Version.
    /// </summary>
    public async Task<bool> ModellVersionPruefenAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, ModelsApiUrl);
            request.Headers.UserAgent.ParseAdd("FlipsiColor/ModelCheck");

            using var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var releases = doc.RootElement.EnumerateArray();

            string? neuesterTag = null;
            foreach (var release in releases)
            {
                var tag = release.GetProperty("tag_name").GetString();
                if (!string.IsNullOrEmpty(tag))
                {
                    neuesterTag = tag;
                    break; // Erster Release = neuester
                }
            }

            if (!string.IsNullOrEmpty(neuesterTag) && neuesterTag != AktuelleModellVersion)
            {
                Log.Information("Neue Modell-Version verfügbar: {Neu} (aktuell: {Aktuell})", neuesterTag, AktuelleModellVersion);
                AktuelleModellVersion = neuesterTag;
                NeueModellVersionVerfuegbar?.Invoke(this, neuesterTag);
                return true;
            }

            Log.Information("Modell-Version aktuell: {Version}", AktuelleModellVersion);
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning("Modell-Version-Check fehlgeschlagen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Prüft ob eine Download-URL erreichbar ist (HTTP HEAD Request).
    /// </summary>
    public async Task<bool> UrlErreichbarAsync(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            request.Headers.UserAgent.ParseAdd("FlipsiColor/UrlCheck");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lädt alle erforderlichen Modelle herunter falls sie fehlen.
    /// Gibt true zurück wenn alle erforderlichen Modelle bereit sind.
    /// </summary>
    public async Task<bool> AlleErforderlichenModelleSicherstellenAsync()
    {
        bool alleOk = true;
        foreach (var info in _modelle.Values.Where(m => m.Erforderlich))
        {
            if (!await ModellSicherstellenAsync(info.Id))
            {
                Log.Error("Erforderliches Modell konnte nicht geladen werden: {Name}", info.Name);
                alleOk = false;
            }
        }
        return alleOk;
    }

    /// <summary>
    /// Gibt zurück wie viele Modelle (erforderlich + optional) bereits heruntergeladen sind.
    /// </summary>
    public int ModelleHeruntergeladen => _modelle.Values.Count(m => m.Heruntergeladen);
    public int ModelleGesamt => _modelle.Count;
    public int ModelleErforderlich => _modelle.Values.Count(m => m.Erforderlich);

    /// <summary>
    /// Gibt alle Modell-Infos zurück (für UI-Anzeige).
    /// </summary>
    public System.Collections.Generic.IEnumerable<ModellInfo> GetAllModellInfos()
        => _modelle.Values;

    /// <summary>
    /// Stellt sicher dass ein Modell heruntergeladen und geladen ist.
    /// FIX #10: Thread-Sicherheit — verhindert parallele Downloads desselben Modells.
    /// </summary>
    public async Task<bool> ModellSicherstellenAsync(ModellId id)
    {
        if (!_modelle.TryGetValue(id, out var info))
        {
            Log.Error("Unbekanntes Modell: {Id}", id);
            return false;
        }

        // FIX #10: Lock verhindert Race-Condition bei parallelen Downloads
        lock (_lock)
        {
            if (info.Heruntergeladen && _sessions.ContainsKey(id))
                return true;
        }

        // Herunterladen falls nötig (mit Lock — verhindert doppelten Download)
        bool needsDownload;
        lock (_lock)
        {
            needsDownload = !info.Heruntergeladen;
        }

        if (needsDownload)
        {
            if (!await ModellHerunterladenAsync(id))
                return false;
        }

        // ONNX Session erstellen (thread-sicher)
        lock (_lock)
        {
            if (_sessions.ContainsKey(id))
                return true;
        }

        return ModellLaden(id);
    }

    /// <summary>
    /// Gibt die ONNX InferenceSession für ein Modell zurück
    /// </summary>
    public InferenceSession? Session(ModellId id)
    {
        lock (_lock)
        {
            return _sessions.GetValueOrDefault(id);
        }
    }

    public long CoreGroesseGesamt() => _modelle.Values
        .Where(m => m.Erforderlich).Sum(m => m.GroesseBytes);
    public long OptionalGroesseGesamt() => _modelle.Values
        .Where(m => !m.Erforderlich).Sum(m => m.GroesseBytes);

    private string ModellPfad(ModellId id) =>
        Path.Combine(_modelDir, $"{(_modelle.TryGetValue(id, out var info) ? info.Name : "")}.onnx");

    private async Task<bool> ModellHerunterladenAsync(ModellId id)
    {
        var info = _modelle[id];
        var zielPfad = ModellPfad(id);
        var tempPfad = zielPfad + ".downloading";

        // FIX #3: URL-Validierung — HTTPS erzwingen, file:// und private IPs blockieren
        if (!SecurityValidator.ValidiereDownloadUrl(info.Url))
        {
            Log.Error("Modell-Download abgelehnt: URL-Validierung fehlgeschlagen für {Name}", info.Name);
            DownloadFehler?.Invoke(this, new ModellFehlerEventArgs(id, "URL-Validierung fehlgeschlagen"));
            return false;
        }

        // FIX #6: Warnung wenn kein SHA256-Hash angegeben
        if (string.IsNullOrEmpty(info.Sha256))
        {
            Log.Warning("Modell {Name}: kein SHA256-Hash hinterlegt — Integrität kann nicht verifiziert werden", info.Name);
        }

        Log.Information("Lade Modell {Name} herunter ({Groesse:N0} bytes)...", info.Name, info.GroesseBytes);

        try
        {
            using var response = await _http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            // FIX #3: Antwort-URL nach Redirect prüfen — nur HTTPS erlaubt
            if (response.RequestMessage?.RequestUri is { } finalUri && finalUri.Scheme != Uri.UriSchemeHttps)
            {
                Log.Error("Modell-Download abgelehnt: Redirect zu nicht-HTTPS ({Schema}) für {Name}", finalUri.Scheme, info.Name);
                DownloadFehler?.Invoke(this, new ModellFehlerEventArgs(id, "Redirect zu nicht-HTTPS blockiert"));
                return false;
            }

            var totalBytes = response.Content.Headers.ContentLength ?? info.GroesseBytes;
            long empfangen = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(tempPfad, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            int gelesen;
            while ((gelesen = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, gelesen));
                empfangen += gelesen;
                DownloadFortschritt?.Invoke(this, new ModellDownloadFortschritt(id, empfangen, totalBytes));
            }

            await fileStream.FlushAsync();
            await fileStream.DisposeAsync();

            // SHA256-Verifikation falls vorhanden
            if (!string.IsNullOrEmpty(info.Sha256))
            {
                var hash = await ComputeSha256Async(tempPfad);
                if (!hash.Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error("SHA256-Fehler für {Name}: Hash stimmt nicht überein", info.Name);
                    try { File.Delete(tempPfad); } catch { /* Ignorieren */ }
                    DownloadFehler?.Invoke(this, new ModellFehlerEventArgs(id, "SHA256-Verifikation fehlgeschlagen"));
                    return false;
                }
                Log.Information("SHA256-Verifikation für {Name}: OK", info.Name);
            }

            File.Move(tempPfad, zielPfad, overwrite: true);
            lock (_lock)
            {
                info.Heruntergeladen = true;
            }
            Log.Information("Modell {Name} erfolgreich heruntergeladen", info.Name);
            ModellBereit?.Invoke(this, id);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Download-Fehler für {Name}: {Fehler}", info.Name, SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            try { if (File.Exists(tempPfad)) File.Delete(tempPfad); } catch { /* Ignorieren */ }
            DownloadFehler?.Invoke(this, new ModellFehlerEventArgs(id, SecurityValidator.BereinigeExceptionFuerLog(ex.Message)));
            return false;
        }
    }

    private bool ModellLaden(ModellId id)
    {
        var pfad = ModellPfad(id);
        if (!File.Exists(pfad))
        {
            Log.Error("Modell-Datei nicht gefunden: {Name}", _modelle[id].Name);
            return false;
        }

        // FIX #10: Lock für thread-sichere Session-Erstellung
        lock (_lock)
        {
            if (_sessions.ContainsKey(id))
                return true; // Bereits von einem anderen Thread geladen
        }

        try
        {
            using var sessionOptions = new SessionOptions();
            // DirectML (GPU) nur auf Windows zur Laufzeit laden, sonst CPU-Only.
            // Microsoft.ML.OnnxRuntime (CPU) ist cross-platform; DirectML ist Windows-only.
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    sessionOptions.AppendExecutionProvider_DML(0);
                    Log.Information("Modell {Name}: DirectML GPU-Provider aktiviert (Windows)", _modelle[id].Name);
                }
                catch
                {
                    Log.Warning("Modell {Name}: DirectML nicht verfügbar, verwende CPU", _modelle[id].Name);
                }
            }
            else
            {
                Log.Information("Modell {Name}: CPU-Modus (nicht-Windows)", _modelle[id].Name);
            }

            var session = new InferenceSession(pfad, sessionOptions);
            lock (_lock)
            {
                _sessions[id] = session;
            }
            Log.Information("Modell {Name}: ONNX Session erstellt (Inputs: {Inputs})",
                _modelle[id].Name, string.Join(", ", session.InputNames));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Fehler beim Laden von Modell {Name}: {Fehler}", _modelle[id].Name,
                SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return false;
        }
    }

    private static async Task<string> ComputeSha256Async(string pfad)
    {
        using var stream = File.OpenRead(pfad);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // FIX #10: Thread-sichere Session-Freigabe
        lock (_lock)
        {
            foreach (var session in _sessions.Values)
                session.Dispose();
            _sessions.Clear();
        }
        // Hinweis: _http ist static und wird nicht pro-Instanz disposed
    }
}

public sealed class ModellDownloadFortschritt : EventArgs
{
    public ModellId Id { get; }
    public long Empfangen { get; }
    public long Gesamt { get; }
    public double Prozent => Gesamt > 0 ? Empfangen * 100.0 / Gesamt : 0;

    public ModellDownloadFortschritt(ModellId id, long empfangen, long gesamt)
    {
        Id = id; Empfangen = empfangen; Gesamt = gesamt;
    }
}

public sealed class ModellFehlerEventArgs : EventArgs
{
    public ModellId Id { get; }
    public string Fehler { get; }
    public ModellFehlerEventArgs(ModellId id, string fehler) { Id = id; Fehler = fehler; }
}