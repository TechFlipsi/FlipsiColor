using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using OpenCvSharp;

using FlipsiColor.Core;
using FlipsiColor.Utils;

namespace FlipsiColor.Video;

/// <summary>
/// DJI Auto-Merge — Fügt DJI Video-Clips automatisch zusammen.
/// DJI Kameras (Osmo 360, Osmo Pocket 4) erstellen alle ~30 Min eine neue Datei.
/// </summary>
public sealed class DjiAutoMerge : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "DjiAutoMerge");

    /// <summary>
    /// DJI Dateinamen-Patterns:
    /// Osmo 360:     DJI_20250101_120000_001_D.MP4, ..._002_D.MP4
    /// Osmo Pocket 4: DJI_20250101120000_001_D.MP4, ..._002_D.MP4
    /// Alternative:  DJI_0001.MP4, DJI_0002.MP4 (ältere Modelle)
    /// </summary>
    private static readonly Regex[] DjiPatterns =
    [
        new(@"^DJI_(\d{8}_\d{6})_(\d{3})_D\.MP4$", RegexOptions.IgnoreCase),
        new(@"^DJI_(\d{14})_(\d{3})_D\.MP4$", RegexOptions.IgnoreCase),
        new(@"^DJI_(\d{4})\.MP4$", RegexOptions.IgnoreCase),
        new(@"^DJI_(\d{3,4})_D\.MP4$", RegexOptions.IgnoreCase),
    ];

    /// <summary>
    /// Eine Gruppe zusammengehöriger DJI-Clips
    /// </summary>
    public sealed class ClipGruppe
    {
        public string GruppenName { get; init; } = "";
        public List<string> Dateien { get; init; } = [];
        public DateTime? Aufnahmezeit { get; init; }
        public int ClipAnzahl => Dateien.Count;
        public long GesamtGroesseBytes => Dateien.Sum(f => new FileInfo(f).Length);
        public string GesamtGroesse => FormatGroesse(GesamtGroesseBytes);

        private static string FormatGroesse(long bytes) => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    /// <summary>
    /// Scannt einen Ordner und gruppiert DJI-Clips automatisch
    /// </summary>
    public List<ClipGruppe> ClipsGruppieren(string ordner)
    {
        if (!Directory.Exists(ordner))
        {
            Log.Warning("Ordner existiert nicht: {Ordner}", ordner);
            return [];
        }

        var mp4Files = Directory.GetFiles(ordner, "*.MP4", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(ordner, "*.mp4", SearchOption.TopDirectoryOnly))
            .OrderBy(f => f)
            .ToList();

        if (mp4Files.Count == 0)
        {
            Log.Information("Keine MP4-Dateien in {Ordner}", ordner);
            return [];
        }

        Log.Information("{Anzahl} MP4-Dateien gefunden in {Ordner}", mp4Files.Count, ordner);

        var gruppen = new Dictionary<string, List<string>>();

        foreach (var datei in mp4Files)
        {
            var name = Path.GetFileName(datei);
            var gruppenKey = GruppierungsSchluesselFinden(name, ordner);

            if (gruppenKey != null)
            {
                if (!gruppen.TryGetValue(gruppenKey, out var liste))
                {
                    liste = [];
                    gruppen[gruppenKey] = liste;
                }
                liste.Add(datei);
            }
            else
            {
                Log.Debug("Datei {Name} passt zu keinem DJI-Pattern", name);
            }
        }

        var result = new List<ClipGruppe>();
        foreach (var (key, dateien) in gruppen)
        {
            var sortiert = dateien.OrderBy(f => f).ToList();
            var aufnahmezeit = AufnahmezeitExtrahieren(sortiert.First());

            result.Add(new ClipGruppe
            {
                GruppenName = key,
                Dateien = sortiert,
                Aufnahmezeit = aufnahmezeit
            });
        }

        return result.OrderBy(g => g.Aufnahmezeit ?? DateTime.MaxValue).ToList();
    }

    private string? GruppierungsSchluesselFinden(string dateiName, string ordner)
    {
        foreach (var pattern in DjiPatterns)
        {
            var match = pattern.Match(dateiName);
            if (match.Success)
            {
                var basis = match.Groups[1].Value;
                return $"{Path.GetFileName(ordner)}_DJI_{basis}";
            }
        }

        var nameOhneExt = Path.GetFileNameWithoutExtension(dateiName);
        var ohneNummer = Regex.Replace(nameOhneExt, @"[\d_]+$", "");
        if (!string.IsNullOrEmpty(ohneNummer) && ohneNummer != nameOhneExt)
        {
            return $"{Path.GetFileName(ordner)}_{ohneNummer}";
        }

        return null;
    }

    private DateTime? AufnahmezeitExtrahieren(string dateiPfad)
    {
        var name = Path.GetFileNameWithoutExtension(dateiPfad);
        var match = Regex.Match(name, @"(\d{4})(\d{2})(\d{2})_?(\d{2})(\d{2})(\d{2})");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out var jahr) &&
                int.TryParse(match.Groups[2].Value, out var monat) &&
                int.TryParse(match.Groups[3].Value, out var tag) &&
                int.TryParse(match.Groups[4].Value, out var stunde) &&
                int.TryParse(match.Groups[5].Value, out var minute) &&
                int.TryParse(match.Groups[6].Value, out var sekunde))
            {
                try { return new DateTime(jahr, monat, tag, stunde, minute, sekunde); }
                catch { /* invalid date */ }
            }
        }

        try { return File.GetCreationTime(dateiPfad); }
        catch { return null; }
    }

    /// <summary>
    /// Fügt eine Clip-Gruppe zu einer einzelnen MP4 zusammen (FFmpeg concat — kein Re-Encoding)
    /// </summary>
    public async Task<string?> ClipsZusammenfuegenAsync(
        ClipGruppe gruppe,
        string ausgabeOrdner,
        IProgress<double>? fortschritt = null)
    {
        if (gruppe.Dateien.Count < 2) return null;

        Directory.CreateDirectory(ausgabeOrdner);

        var ausgabeDatei = Path.Combine(ausgabeOrdner, $"{gruppe.GruppenName}_merged.mp4");
        if (File.Exists(ausgabeDatei))
        {
            Log.Information("Bereits vorhanden: {Datei}", ausgabeDatei);
            return ausgabeDatei;
        }

        var concatDatei = Path.Combine(ausgabeOrdner, $"concat_{gruppe.GruppenName}.txt");

        try
        {
            var lines = gruppe.Dateien.Select(f => $"file '{f.Replace("'", "'\\''")}'");
            await File.WriteAllLinesAsync(concatDatei, lines);

            var start = DateTime.UtcNow;
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -f concat -safe 0 -i \"{concatDatei}\" -c copy \"{ausgabeDatei}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();

            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) != null)
            {
                if (line.Contains("time="))
                {
                    fortschritt?.Report(0.5);
                }
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                Log.Error("FFmpeg Fehler (Exit {Code})", process.ExitCode);
                return null;
            }

            var dauer = (DateTime.UtcNow - start).TotalSeconds;
            var groesse = new FileInfo(ausgabeDatei).Length;
            Log.Information(
                "Clips zusammengefügt: {Anzahl} → {Ausgabe} ({Groesse:F1} MB) in {Dauer:F1}s",
                gruppe.Dateien.Count, Path.GetFileName(ausgabeDatei),
                groesse / (1024.0 * 1024), dauer);

            fortschritt?.Report(1.0);
            return ausgabeDatei;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Zusammenfügen");
            return null;
        }
        finally
        {
            try { if (File.Exists(concatDatei)) File.Delete(concatDatei); } catch { }
        }
    }

    /// <summary>
    /// Fügt Clips zusammen UND wendet Farbkorrektur via VideoPipeline an
    /// </summary>
    public async Task<string?> ClipsZusammenfuegenMitFarbkorrekturAsync(
        ClipGruppe gruppe,
        string ausgabeOrdner,
        AI.ModelManager modelManager,
        Color.ColorManager colorManager,
        PipelineParams param,
        IProgress<double>? fortschritt = null)
    {
        if (gruppe.Dateien.Count < 2) return null;

        Directory.CreateDirectory(ausgabeOrdner);

        var ausgabeDatei = Path.Combine(ausgabeOrdner, $"{gruppe.GruppenName}_colorcorrected.mp4");
        if (File.Exists(ausgabeDatei))
        {
            Log.Information("Bereits vorhanden: {Datei}", ausgabeDatei);
            return ausgabeDatei;
        }

        // 1. Zusammenfügen
        var merged = await ClipsZusammenfuegenAsync(gruppe, ausgabeOrdner, fortschritt);
        if (merged == null) return null;

        // 2. Farbkorrektur
        fortschritt?.Report(0.3);
        Log.Information("Farbkorrektur wird angewendet...");

        try
        {
            var farbkorrigiert = await Task.Run(() =>
            {
                using var vp = new VideoPipeline(modelManager, colorManager);
                if (!vp.VideoLaden(merged)) return merged;

                vp.PipelineAusfuehren(param, (aktuell, gesamt) =>
                {
                    fortschritt?.Report(0.3 + 0.7 * ((double)aktuell / gesamt));
                });

                return ausgabeDatei;
            });

            if (farbkorrigiert != null && File.Exists(farbkorrigiert))
            {
                try { File.Delete(merged); } catch { }
                Log.Information("Farbkorrigiert: {Datei}", farbkorrigiert);
            }

            return farbkorrigiert;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Farbkorrektur fehlgeschlagen");
            return merged;
        }
    }

    public void Dispose() { }
}