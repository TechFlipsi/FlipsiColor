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

namespace FlipsiColor.AI;

/// <summary>
/// Verwaltet KI-Modelle: Download, SHA256-Verifikation, ONNX Session-Erstellung
/// </summary>
public sealed class ModelManager : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ModelManager>();

    private readonly Dictionary<ModellId, ModellInfo> _modelle = new();
    private readonly Dictionary<ModellId, InferenceSession> _sessions = new();
    private readonly HttpClient _http = new();
    private readonly string _modelDir;
    private bool _disposed;

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
            Sha256 = "", GroesseBytes = 17_825_792, Erforderlich = true
        };
        _modelle[ModellId.RestormerLight] = new()
        {
            Id = ModellId.RestormerLight, Name = "RestormerLight",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/restormer_light.onnx",
            Sha256 = "", GroesseBytes = 25_165_824, Erforderlich = true
        };
        _modelle[ModellId.RealHATGAN] = new()
        {
            Id = ModellId.RealHATGAN, Name = "RealHATGAN",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/realhatgan.onnx",
            Sha256 = "", GroesseBytes = 125_829_120, Erforderlich = false
        };
        _modelle[ModellId.RealESRGAN] = new()
        {
            Id = ModellId.RealESRGAN, Name = "RealESRGAN",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/realesrgan.onnx",
            Sha256 = "", GroesseBytes = 67_108_864, Erforderlich = false
        };
        _modelle[ModellId.CodeFormer] = new()
        {
            Id = ModellId.CodeFormer, Name = "CodeFormer",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/codeformer.onnx",
            Sha256 = "", GroesseBytes = 367_001_600, Erforderlich = false
        };
        _modelle[ModellId.AiLUTTransform] = new()
        {
            Id = ModellId.AiLUTTransform, Name = "AiLUTTransform",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/ailut_transform.onnx",
            Sha256 = "", GroesseBytes = 8_388_608, Erforderlich = true
        };
        _modelle[ModellId.EfficientNet] = new()
        {
            Id = ModellId.EfficientNet, Name = "EfficientNet",
            Url = "https://github.com/TechFlipsi/FlipsiColor-Models/releases/download/v0.1/efficientnet.onnx",
            Sha256 = "", GroesseBytes = 4_818_304, Erforderlich = true
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
    /// Stellt sicher dass ein Modell heruntergeladen und geladen ist
    /// </summary>
    public async Task<bool> ModellSicherstellenAsync(ModellId id)
    {
        if (!_modelle.TryGetValue(id, out var info))
        {
            Log.Error("Unbekanntes Modell: {Id}", id);
            return false;
        }

        if (info.Heruntergeladen && _sessions.ContainsKey(id))
            return true;

        // Herunterladen falls nötig
        if (!info.Heruntergeladen)
        {
            if (!await ModellHerunterladenAsync(id))
                return false;
        }

        // ONNX Session erstellen
        return ModellLaden(id);
    }

    /// <summary>
    /// Gibt die ONNX InferenceSession für ein Modell zurück
    /// </summary>
    public InferenceSession? Session(ModellId id)
    {
        return _sessions.GetValueOrDefault(id);
    }

    public long CoreGroesseGesamt() => _modelle.Values
        .Where(m => m.Erforderlich).Sum(m => m.GroesseBytes);
    public long OptionalGroesseGesamt() => _modelle.Values
        .Where(m => !m.Erforderlich).Sum(m => m.GroesseBytes);

    private string ModellPfad(ModellId id) =>
        Path.Combine(_modelDir, $"{_modelle[id].Name}.onnx");

    private async Task<bool> ModellHerunterladenAsync(ModellId id)
    {
        var info = _modelle[id];
        var zielPfad = ModellPfad(id);
        var tempPfad = zielPfad + ".downloading";

        Log.Information("Lade Modell {Name} herunter ({Groesse:N0} bytes)...", info.Name, info.GroesseBytes);

        try
        {
            using var response = await _http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

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
                    Log.Error("SHA256-Fehler für {Name}: erwartet {Expected}, erhalten {Actual}",
                        info.Name, info.Sha256, hash);
                    File.Delete(tempPfad);
                    DownloadFehler?.Invoke(this, new ModellFehlerEventArgs(id, "SHA256-Verifikation fehlgeschlagen"));
                    return false;
                }
                Log.Information("SHA256-Verifikation für {Name}: OK", info.Name);
            }

            File.Move(tempPfad, zielPfad, overwrite: true);
            info.Heruntergeladen = true;
            Log.Information("Modell {Name} erfolgreich heruntergeladen", info.Name);
            ModellBereit?.Invoke(this, id);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Download-Fehler für {Name}", info.Name);
            if (File.Exists(tempPfad)) File.Delete(tempPfad);
            DownloadFehler?.Invoke(this, new ModellFehlerEventArgs(id, ex.Message));
            return false;
        }
    }

    private bool ModellLaden(ModellId id)
    {
        var pfad = ModellPfad(id);
        if (!File.Exists(pfad))
        {
            Log.Error("Modell-Datei nicht gefunden: {Pfad}", pfad);
            return false;
        }

        try
        {
            var sessionOptions = new SessionOptions();
            // DirectML (GPU) als首选, Fallback auf CPU
            try
            {
                sessionOptions.AppendExecutionProvider_DML(0);
                Log.Information("Modell {Name}: DirectML GPU-Provider aktiviert", _modelle[id].Name);
            }
            catch
            {
                Log.Warning("Modell {Name}: DirectML nicht verfügbar, verwende CPU", _modelle[id].Name);
            }

            var session = new InferenceSession(pfad, sessionOptions);
            _sessions[id] = session;
            Log.Information("Modell {Name}: ONNX Session erstellt (Inputs: {Inputs})",
                _modelle[id].Name, string.Join(", ", session.InputNames));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Laden von Modell {Name}", _modelle[id].Name);
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
        foreach (var session in _sessions.Values)
            session.Dispose();
        _sessions.Clear();
        _http.Dispose();
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