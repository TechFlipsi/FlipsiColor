using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using FlipsiColor.ViewModels;

namespace FlipsiColor.Views;

/// <summary>
/// MainWindow Code-Behind — Avalonia UI.
/// Drag &amp; Drop: mehrere Dateien gleichzeitig (Bilder UND Videos).
/// OpenFileDialog → StorageProvider (Avalonia).
/// Pitfall: DragOver/Drop Events müssen via DragDrop.AddDragOverHandler hinzugefügt werden.
/// Pitfall: DragEventArgs.DataTransfer (IDataTransfer) statt .Data.
/// Pitfall: Dispatcher.UIThread.Post statt Dispatcher.Invoke.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Drag & Drop Events — Avalonia 12: via DragDrop.AddDragOverHandler/AddDropHandler
        var dropBorder = this.FindControl<Border>("DropBorder");
        if (dropBorder != null)
        {
            DragDrop.SetAllowDrop(dropBorder, true);
            DragDrop.AddDragOverHandler(dropBorder, OnDragOver);
            DragDrop.AddDropHandler(dropBorder, OnDrop);
            DragDrop.AddDragLeaveHandler(dropBorder, OnDragLeave);
        }

        // Callbacks für Dialoge setzen (StorageProvider)
        SetViewModelCallbacks();

        Opened += OnOpened;
    }

    private void SetViewModelCallbacks()
    {
        if (DataContext is MainViewModel vm)
        {
            vm.DateiOeffnenCallback = DateiOeffnenAsync;
            vm.OrdnerOeffnenCallback = OrdnerOeffnenAsync;
            vm.MeldungAnzeigenCallback = MeldungAnzeigen;
            vm.FehlerAnzeigenCallback = FehlerAnzeigen;
            vm.SpracheGeaendert += OnSpracheGeaendert;
        }
    }

    /// <summary>
    /// Controls können im Constructor null sein → AttachedToVisualTree oder Opened Event verwenden.
    /// Pitfall: Controls können im Constructor null sein.
    /// </summary>
    private void OnOpened(object? sender, EventArgs e)
    {
        SetViewModelCallbacks();
    }

    /// <summary>Drag-Over: visuelles Feedback (Border-Highlight).</summary>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        // Border-Highlight via Classes
        if (sender is Border border)
            border.Classes.Add("dragOver");
    }

    /// <summary>Drag-Leave: Border-Highlight entfernen.</summary>
    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (sender is Border border)
            border.Classes.Remove("dragOver");
    }

    /// <summary>Drop: mehrere Dateien empfangen (Bilder UND Videos).</summary>
    private void OnDrop(object? sender, DragEventArgs e)
    {
        // Border-Highlight entfernen
        if (sender is Border border)
            border.Classes.Remove("dragOver");

        if (DataContext is not MainViewModel vm) return;

        // Avalonia 12: e.DataTransfer (IDataTransfer) statt e.Data
        var dataTransfer = e.DataTransfer;
        if (dataTransfer != null)
        {
            // IDataTransfer.Items enthält IDataTransferItem-Objekte.
            // Für Dateien: item.TryGetRaw(DataFormat.File) → IStorageItem
            var dateien = new System.Collections.Generic.List<string>();
            foreach (var item in dataTransfer.Items)
            {
                var raw = item.TryGetRaw(DataFormat.File);
                if (raw is Avalonia.Platform.Storage.IStorageItem storageItem)
                {
                    dateien.Add(storageItem.Path.LocalPath);
                }
            }
            if (dateien.Count > 0)
            {
                vm.DateienHinzufuegen(dateien.ToArray());
            }
        }

        e.DragEffects = DragDropEffects.Copy;
    }

    /// <summary>
    /// Datei-Öffnen-Dialog via StorageProvider (Avalonia).
    /// Pitfall: OpenFileDialog existiert in Avalonia nicht → StorageProvider.
    /// </summary>
    private async Task<string?> DateiOeffnenAsync(string titel, string filter, string? defaultExt)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return null;

        var fileTypes = FilterZuFileTypes(filter);
        var options = new FilePickerOpenOptions
        {
            Title = titel,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        };

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Ordner-Öffnen-Dialog via StorageProvider.
    /// </summary>
    private async Task<string?> OrdnerOeffnenAsync(string titel)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = titel,
            AllowMultiple = false
        };

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Konvertiert einen Filter-String (*.jpg;*.png) in FilePickerFileType-Liste.
    /// </summary>
    private static System.Collections.Generic.List<FilePickerFileType> FilterZuFileTypes(string filter)
    {
        var patterns = filter.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(f => f.Trim().TrimStart('*'))
            .Where(f => !string.IsNullOrEmpty(f))
            .ToArray();

        return new System.Collections.Generic.List<FilePickerFileType>
        {
            new(Lokalisierung.T("DateiListe.Titel"))
            {
                Patterns = patterns.Length > 0 ? patterns : new[] { "*.*" }
            }
        };
    }

    /// <summary>Einfache Meldung anzeigen (ContentDialog-Ersatz).</summary>
    private void MeldungAnzeigen(string meldung)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // MessageBox.Show() existiert in Avalonia NICHT → Logging
            Serilog.Log.Information("Meldung: {Meldung}", meldung);
        });
    }

    /// <summary>Fehlermeldung anzeigen.</summary>
    private void FehlerAnzeigen(string fehler)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Serilog.Log.Error("Fehler: {Fehler}", fehler);
        });
    }

    /// <summary>
    /// Bei Sprachwechsel: UI-Texte aktualisieren.
    /// </summary>
    private void OnSpracheGeaendert(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MainViewModel vm)
            {
                // Bei Sprachwechsel alle Bindings aktualisieren
                vm.StatusText = vm.StatusText;
            }
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is MainViewModel vm)
            vm.Dispose();
    }
}