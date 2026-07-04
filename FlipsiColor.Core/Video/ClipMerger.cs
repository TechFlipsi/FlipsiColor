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
/// ClipMerger — Fügt Video-Clips automatisch zusammen (für alle Kameras).
/// Gruppiert Video-Dateien nach Datum/Zeit aus dem Dateinamen oder EXIF-Metadaten.
/// </summary>
public sealed class ClipMerger : IDisposable
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "ClipMerger");

    /// <summary>
    /// Allgemeine Video-Datei-Endungen
    /// </summary>
    private static readonly HashSet<string> VideoEndungen = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".m4v", ".wmv", ".flv", ".mxf", ".mts", ".m2ts"
    };

    /// <summary>
    /// Allgemeine Datums/Zeit-Patterns für Dateinamen (für alle Kameras):
    /// YYYYMMDD_HHMMSS, YYYYMMDDHHMMSS, YYYY-MM-DD_HH-MM-SS, etc.
    /// </summary>
    private static readonly Regex[] DatumsPatterns =
    [
        // YYYYMMDD_HHMMSS  (z.B. CAM_20250101_120000_001_D.MP4, GOPR0101_120000.MP4)
        new(@"(\d{4})(\d{2})(\d{2})[_\s](\d{2})(\d{2})(\d{2})", RegexOptions.IgnoreCase),
        // YYYYMMDDHHMMSS   (z.B. CAM_20250101120000_001_D.MP4)
        new(@"(\d{4})(\d{2})(\d{2})(\d{2})(\d{2})(\d{2})", RegexOptions.IgnoreCase),
        // YYYY-MM-DD_HH-MM-SS oder YYYY-MM-DD-HH-MM-SS
        new(@"(\d{4})[-](\d{2})[-](\d{2})[_-](\d{2})[-](\d{2})[-](\d{2})", RegexOptions.IgnoreCase),
        // YYYY_MM_DD_HH_MM_SS
        new(@"(\d{4})[_](\d{2})[_](\d{2})[_](\d{2})[_](\d{2})[_](\d{2})", RegexOptions.IgnoreCase),
    ];

    /// <summary>
    /// Eine Gruppe zusammengehöriger Video-Clips
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
    /// Scannt einen Ordner und gruppiert Video-Clips automatisch (nach Datum/Zeit).
    /// FIX #1: Verzeichnis-Pfad wird gegen Path-Traversal validiert.
    /// </summary>
    public List<ClipGruppe> ClipsGruppieren(string ordner)
    {
        var validierterOrdner = SecurityValidator.ValidiereVerzeichnisPfad(ordner);
        if (validierterOrdner == null || !Directory.Exists(validierterOrdner))
        {
            Log.Warning("Ordner existiert nicht oder wurde abgelehnt");
            return [];
        }
        ordner = validierterOrdner;

        // Alle Video-Dateien im Ordner finden (alle Kameras)
        var videoDateien = Directory.GetFiles(ordner, "*", SearchOption.TopDirectoryOnly)
            .Where(f => VideoEndungen.Contains(Path.GetExtension(f)))
            .OrderBy(f => f)
            .ToList();

        if (videoDateien.Count == 0)
        {
            Log.Information("Keine Video-Dateien im Ordner gefunden");
            return [];
        }

        Log.Information("{Anzahl} Video-Dateien gefunden", videoDateien.Count);

        var gruppen = new Dictionary<string, List<string>>();

        foreach (var datei in videoDateien)
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
                // Fallback: Datei ohne erkennbares Datum als eigene Gruppe
                var nameOhneExt = Path.GetFileNameWithoutExtension(name);
                gruppen[$"{Path.GetFileName(ordner)}_{nameOhneExt}"] = [datei];
                Log.Debug("Datei {Name} ohne Datum-Pattern — eigene Gruppe", name);
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

    /// <summary>
    /// Findet einen Gruppierungsschlüssel basierend auf Datum/Zeit im Dateinamen.
    /// Allgemein: extrahiert Datum aus dem Dateinamen.
    /// </summary>
    private string? GruppierungsSchluesselFinden(string dateiName, string ordner)
    {
        var nameOhneExt = Path.GetFileNameWithoutExtension(dateiName);

        // 1. Versuch: Datum/Zeit aus Dateinamen extrahieren
        foreach (var pattern in DatumsPatterns)
        {
            var match = pattern.Match(nameOhneExt);
            if (match.Success)
            {
                // Gruppierung nach Datum (ohne Zeit) — alle Clips desselben Tages gehören zusammen
                var jahr = match.Groups[1].Value;
                var monat = match.Groups[2].Value;
                var tag = match.Groups[3].Value;
                return $"{Path.GetFileName(ordner)}_{jahr}{monat}{tag}";
            }
        }

        // 2. Versuch: Sequenz-Nummer am Ende entfernen (z.B. CAM_0001.MP4 → CAM)
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

        foreach (var pattern in DatumsPatterns)
        {
            var match = pattern.Match(name);
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
        }

        // Fallback: Datei-Erstellungszeit
        try { return File.GetCreationTime(dateiPfad); }
        catch { return null; }
    }

    /// <summary>
    /// Fügt eine Clip-Gruppe zu einer einzelnen MP4 zusammen (FFmpeg concat — kein Re-Encoding).
    /// FIX #1: Ausgabe-Ordner wird validiert. FIX #2: Command-Injection durch sichere ArgumentList verhindert.
    /// </summary>
    public async Task<string?> ClipsZusammenfuegenAsync(
        ClipGruppe gruppe,
        string ausgabeOrdner,
        IProgress<double>? fortschritt = null)
    {
        if (gruppe.Dateien.Count < 2) return null;

        var validierterOrdner = SecurityValidator.ValidiereVerzeichnisPfad(ausgabeOrdner);
        if (validierterOrdner == null)
        {
            Log.Warning("ClipsZusammenfuegen: Ausgabe-Ordner abgelehnt");
            return null;
        }
        ausgabeOrdner = validierterOrdner;
        Directory.CreateDirectory(ausgabeOrdner);

        var sichererGruppenName = gruppe.GruppenName.Replace("..", "").Replace("/", "").Replace("\\", "");
        var ausgabeDatei = Path.Combine(ausgabeOrdner, $"{sichererGruppenName}_merged.mp4");
        if (File.Exists(ausgabeDatei))
        {
            Log.Information("Bereits vorhanden: {Datei}", SecurityValidator.BereinigePfadFuerLog(ausgabeDatei));
            return ausgabeDatei;
        }

        var concatDatei = Path.Combine(ausgabeOrdner, $"concat_{sichererGruppenName}.txt");

        try
        {
            var lines = gruppe.Dateien.Select(f => $"file '{f.Replace("'", "'\\''")}'");
            await File.WriteAllLinesAsync(concatDatei, lines);

            var start = DateTime.UtcNow;
            using var process = new Process
            {
                StartInfo = SecurityValidator.SichereProcessStartInfo("ffmpeg",
                    new[] { "-y", "-f", "concat", "-safe", "0", "-i", concatDatei, "-c", "copy", ausgabeDatei })
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
            Log.Error("Fehler beim Zusammenfügen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return null;
        }
        finally
        {
            try { if (File.Exists(concatDatei)) File.Delete(concatDatei); } catch { }
        }
    }

    /// <summary>
    /// Fügt Clips zusammen UND wendet Farbkorrektur via VideoPipeline an.
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

        var validierterOrdner = SecurityValidator.ValidiereVerzeichnisPfad(ausgabeOrdner);
        if (validierterOrdner == null)
        {
            Log.Warning("ClipsZusammenfuegenMitFarbkorrektur: Ausgabe-Ordner abgelehnt");
            return null;
        }
        ausgabeOrdner = validierterOrdner;
        Directory.CreateDirectory(ausgabeOrdner);

        var sichererGruppenName = gruppe.GruppenName.Replace("..", "").Replace("/", "").Replace("\\", "");
        var ausgabeDatei = Path.Combine(ausgabeOrdner, $"{sichererGruppenName}_colorcorrected.mp4");
        if (File.Exists(ausgabeDatei))
        {
            Log.Information("Bereits vorhanden: {Datei}", SecurityValidator.BereinigePfadFuerLog(ausgabeDatei));
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
                Log.Information("Farbkorrigiert: {Datei}", SecurityValidator.BereinigePfadFuerLog(farbkorrigiert));
            }

            return farbkorrigiert;
        }
        catch (Exception ex)
        {
            Log.Error("Farbkorrektur fehlgeschlagen: {Fehler}", SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            return merged;
        }
    }

    public void Dispose() { }
}