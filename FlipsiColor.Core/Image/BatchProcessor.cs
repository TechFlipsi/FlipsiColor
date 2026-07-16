using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FlipsiColor.Utils;

namespace FlipsiColor.Image;

/// <summary>
/// Status eines Batch-Jobs (Issue #15).
/// </summary>
public enum BatchJobStatus
{
    /// <summary>Wartet in der Queue.</summary>
    Wartet,
    /// <summary>Wird gerade verarbeitet.</summary>
    Aktiv,
    /// <summary>Erfolgreich abgeschlossen.</summary>
    Abgeschlossen,
    /// <summary>Fehler bei der Verarbeitung.</summary>
    Fehlgeschlagen,
    /// <summary>Vom User pausiert.</summary>
    Pausiert
}

/// <summary>
/// Ein einzelner Batch-Job — eine Datei, die verarbeitet werden soll (Issue #15).
/// </summary>
public sealed class BatchJob
{
    /// <summary>Quell-Dateipfad (Bild oder Video).</summary>
    public string Quelle { get; set; } = "";

    /// <summary>Ziel-Dateipfad (Output).</summary>
    public string Ziel { get; set; } = "";

    /// <summary>Dateiname ohne Pfad.</summary>
    public string Dateiname => Path.GetFileName(Quelle);

    /// <summary>Aktueller Status.</summary>
    public BatchJobStatus Status { get; set; } = BatchJobStatus.Wartet;

    /// <summary>Fortschritt 0.0 bis 1.0.</summary>
    public double Fortschritt { get; set; }

    /// <summary>Fehlermeldung bei Fehlschlag.</summary>
    public string? FehlerMeldung { get; set; }

    /// <summary>True wenn es ein Bild ist, false wenn Video.</summary>
    public bool IstBild { get; set; }

    /// <summary>True wenn es ein Video ist.</summary>
    public bool IstVideo => !IstBild;
}

/// <summary>
/// BatchProcessor — verarbeitet mehrere Dateien automatisch mit Queue (Issue #15).
/// Async-Verarbeitung mit CancellationToken, Fortschritts-Callback.
/// Unterstützt Pause/Resume und Fehler-Handling (fehlgeschlagene Dateien sammeln).
/// </summary>
public sealed class BatchProcessor
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<BatchProcessor>();

    private readonly List<BatchJob> _jobs = [];
    private readonly CancellationTokenSource _cts = new();
    private bool _paused;

    /// <summary>Alle Jobs in der Queue.</summary>
    public IReadOnlyList<BatchJob> Jobs => _jobs.AsReadOnly();

    /// <summary>Anzahl abgeschlossener Jobs.</summary>
    public int Abgeschlossen => _jobs.FindAll(j => j.Status == BatchJobStatus.Abgeschlossen).Count;

    /// <summary>Anzahl fehlgeschlagener Jobs.</summary>
    public int Fehlgeschlagen => _jobs.FindAll(j => j.Status == BatchJobStatus.Fehlgeschlagen).Count;

    /// <summary>Gesamtanzahl Jobs.</summary>
    public int Gesamt => _jobs.Count;

    /// <summary>True wenn die Verarbeitung pausiert ist.</summary>
    public bool IstPausiert => _paused;

    /// <summary>
    /// Fügt Dateien zur Queue hinzu.
    /// </summary>
    /// <param name="dateien">Array von Dateipfaden.</param>
    /// <param name="zielOrdner">Ziel-Ordner für die Ausgabedateien.</param>
    public void DateienHinzufuegen(string[] dateien, string zielOrdner)
    {
        foreach (var datei in dateien)
        {
            if (!File.Exists(datei)) continue;

            var ext = Path.GetExtension(datei).ToLowerInvariant();
            var istBild = ext is ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" or ".bmp"
                or ".cr2" or ".cr3" or ".nef" or ".arw" or ".dng" or ".orf" or ".rw2";
            var istVideo = ext is ".mp4" or ".mov" or ".avi" or ".mkv";
            if (!istBild && !istVideo) continue;

            var zielDatei = Path.Combine(zielOrdner,
                Path.GetFileNameWithoutExtension(datei) + "_flipsicolor" + (istBild ? ".jpg" : ".mp4"));

            _jobs.Add(new BatchJob
            {
                Quelle = datei,
                Ziel = zielDatei,
                IstBild = istBild
            });
        }
    }

    /// <summary>
    /// Leert die komplette Queue.
    /// </summary>
    public void QueueLeeren()
    {
        _jobs.Clear();
    }

    /// <summary>
    /// Entfernt einen einzelnen Job aus der Queue.
    /// </summary>
    public void JobEntfernen(BatchJob job)
    {
        _jobs.Remove(job);
    }

    /// <summary>
    /// Startet die Batch-Verarbeitung asynchron.
    /// </summary>
    /// <param name="pipelineAusfuehren">Callback für die Verarbeitung eines einzelnen Jobs.
    /// Empfängt den Job und einen Progress-Callback (0.0-1.0).</param>
    /// <param name="gesamtFortschritt">Callback für den Gesamtfortschritt (0.0-1.0).</param>
    public async Task StartenAsync(Func<BatchJob, Action<double>, Task> pipelineAusfuehren,
        Action<double>? gesamtFortschritt = null)
    {
        var abgeschlossenZaehler = 0;
        var gesamt = _jobs.Count;
        if (gesamt == 0) return;

        foreach (var job in _jobs)
        {
            if (_cts.Token.IsCancellationRequested) break;

            // Warten während Pause
            while (_paused && !_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(100, _cts.Token);
            }

            if (_cts.Token.IsCancellationRequested) break;

            job.Status = BatchJobStatus.Aktiv;
            job.Fortschritt = 0;

            try
            {
                await pipelineAusfuehren(job, progress =>
                {
                    job.Fortschritt = progress;
                });

                job.Status = BatchJobStatus.Abgeschlossen;
                job.Fortschritt = 1.0;
            }
            catch (OperationCanceledException)
            {
                job.Status = BatchJobStatus.Pausiert;
                break;
            }
            catch (Exception ex)
            {
                job.Status = BatchJobStatus.Fehlgeschlagen;
                job.FehlerMeldung = SecurityValidator.BereinigeExceptionFuerLog(ex.Message);
                Log.Warning("Batch-Job fehlgeschlagen: {Datei} — {Fehler}",
                    job.Dateiname, SecurityValidator.BereinigeExceptionFuerLog(ex.Message));
            }

            abgeschlossenZaehler++;
            gesamtFortschritt?.Invoke((double)abgeschlossenZaehler / gesamt);
        }
    }

    /// <summary>
    /// Pausiert die Verarbeitung.
    /// </summary>
    public void Pausieren()
    {
        _paused = true;
        foreach (var job in _jobs)
        {
            if (job.Status == BatchJobStatus.Aktiv)
                job.Status = BatchJobStatus.Pausiert;
        }
    }

    /// <summary>
    /// Setzt die Verarbeitung nach Pause fort.
    /// </summary>
    public void Fortsetzen()
    {
        _paused = false;
        foreach (var job in _jobs)
        {
            if (job.Status == BatchJobStatus.Pausiert)
                job.Status = BatchJobStatus.Wartet;
        }
    }

    /// <summary>
    /// Bricht die Verarbeitung ab.
    /// </summary>
    public void Abbrechen()
    {
        _cts.Cancel();
    }
}