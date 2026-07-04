using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FlipsiColor.UI;

namespace FlipsiColor;

/// <summary>
/// MainWindow Code-Behind
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Originaler Border-Brush für Drag-Over Reset.
    /// </summary>
    private Brush? _originalDropBorderBrush;
    /// <summary>
    /// Originaler Border-Thickness für Drag-Over Reset.
    /// </summary>
    private Thickness _originalDropBorderThickness;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>DragEnter: Accept file drops (images and videos)</summary>
    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Any(MainViewModel.IstUnterstuetzteDatei))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>DragOver: Provide visual feedback during drag-over</summary>
    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Any(MainViewModel.IstUnterstuetzteDatei))
            {
                e.Effects = DragDropEffects.Copy;
                ApplyDropHighlight(sender);
            }
            else
            {
                e.Effects = DragDropEffects.None;
                RemoveDropHighlight(sender);
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
            RemoveDropHighlight(sender);
        }
        e.Handled = true;
    }

    /// <summary>DragLeave: Remove visual highlight</summary>
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        RemoveDropHighlight(sender);
        e.Handled = true;
    }

    /// <summary>Drop: Receive files and add them to the ViewModel file list</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        RemoveDropHighlight(sender);

        if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is MainViewModel vm)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 })
            {
                vm.DateienHinzufuegen(files);

                // If a single image was dropped, also load it as preview
                var images = files
                    .Where(f => !System.IO.Directory.Exists(f) && MainViewModel.BildEndungen.Contains(System.IO.Path.GetExtension(f)))
                    .ToList();
                if (images.Count == 1)
                {
                    vm.LoadBild(images[0]);
                }
            }
        }
    }

    /// <summary>
    /// Hebt den Drop-Border visuell hervor (Drag-Over Feedback).
    /// </summary>
    private void ApplyDropHighlight(object sender)
    {
        if (sender is not Border border) return;
        _originalDropBorderBrush ??= border.BorderBrush;
        _originalDropBorderThickness = border.BorderThickness;
        border.BorderBrush = (Brush)FindResource("AccentPrimaryBrush");
        border.BorderThickness = new Thickness(3);
    }

    /// <summary>
    /// Setzt den Drop-Border auf den Originalzustand zurück.
    /// </summary>
    private void RemoveDropHighlight(object sender)
    {
        if (sender is not Border border) return;
        if (_originalDropBorderBrush != null)
            border.BorderBrush = _originalDropBorderBrush;
        border.BorderThickness = _originalDropBorderThickness;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // ViewModel disposen
        if (DataContext is MainViewModel vm)
            vm.Dispose();
    }
}