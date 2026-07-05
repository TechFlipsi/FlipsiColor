using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

using FlipsiColor.AI;
using FlipsiColor.Core;

namespace FlipsiColor;

/// <summary>
/// Lade-Fenster das beim ersten Start angezeigt wird,
/// während die KI-Modelle heruntergeladen werden.
/// </summary>
public partial class LoadingWindow : Window, INotifyPropertyChanged
{
    private readonly ModelManager _modelManager;
    private string _statusText = "Initialisierung...";
    private double _fortschritt;

    public ObservableCollection<ModellLadeStatus> Modelle { get; } = new();
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }
    public double Fortschritt { get => _fortschritt; set { _fortschritt = value; OnPropertyChanged(); } }

    public LoadingWindow(ModelManager modelManager)
    {
        InitializeComponent();
        _modelManager = modelManager;
        DataContext = this;

        // Modell-Liste initialisieren
        foreach (var info in modelManager.GetAllModellInfos())
        {
            Modelle.Add(new ModellLadeStatus
            {
                Name = info.Name,
                Status = info.Erforderlich ? "Erforderlich" : "Optional",
                Erforderlich = info.Erforderlich
            });
        }
    }

    public async Task LadeModelleAsync()
    {
        // 1. Prüfen ob Modelle bereits lokal vorhanden sind
        if (_modelManager.AlleErforderlichenModelleVorhanden())
        {
            StatusText = "Alle Modelle bereits vorhanden — kein Download nötig";
            Fortschritt = 100;
            await Task.Delay(500); // Kurz anzeigen
            return;
        }

        // 2. Modell-Version prüfen
        StatusText = "Prüfe Modell-Version...";
        await _modelManager.ModellVersionPruefenAsync();

        // 3. Alle Modelle herunterladen (erforderlich + optional)
        StatusText = "Lade alle Modelle herunter...";
        _modelManager.DownloadFortschritt += OnDownloadFortschritt;
        _modelManager.DownloadFehler += OnDownloadFehler;

        await _modelManager.AlleModelleSicherstellenAsync();

        _modelManager.DownloadFortschritt -= OnDownloadFortschritt;
        _modelManager.DownloadFehler -= OnDownloadFehler;

        StatusText = "Modelle bereit — App wird gestartet";
        Fortschritt = 100;
        await Task.Delay(500);
    }

    private void OnDownloadFortschritt(object? sender, ModellDownloadFortschritt e)
    {
        Dispatcher.Invoke(() =>
        {
            // Status des aktuellen Modells aktualisieren
            foreach (var m in Modelle)
            {
                if (m.Name == e.Id.ToString())
                {
                    m.Status = $"{e.Prozent:F0}%";
                    break;
                }
            }
            Fortschritt = e.Prozent;
            StatusText = $"Lade {e.Id}... {e.Prozent:F0}%";
        });
    }

    private void OnDownloadFehler(object? sender, ModellFehlerEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            foreach (var m in Modelle)
            {
                if (m.Name == e.Id.ToString())
                {
                    m.Status = "Fehler!";
                    break;
                }
            }
            StatusText = $"Fehler beim Laden von {e.Id}: {e.Fehler}";
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ModellLadeStatus : INotifyPropertyChanged
{
    private string _status = "";
    public string Name { get; set; } = "";
    public string Status { get => _status; set { _status = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); } }
    public bool Erforderlich { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
}