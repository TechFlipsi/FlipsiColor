using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using FlipsiColor.UI;

namespace FlipsiColor;

/// <summary>
/// MainWindow Code-Behind
/// </summary>
public partial class MainWindow : Window
{
    private Brush? _originalDropBorderBrush;
    private Thickness _originalDropBorderThickness;

    // ===== UIPI Fix: ChangeWindowMessageFilterEx =====
    // Wenn die App elevated läuft (z.B. via Task Scheduler mit höchsten Privilegien),
    // blockiert Windows UIPI Drag&Drop von Explorer (medium integrity) zur App (high integrity).
    // Diese P/Invoke erlaubt WM_DROPFILES Nachrichten durch den UIPI-Filter.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint flag, IntPtr pChangeFilterStruct);

    private const uint MSGFLT_ALLOW = 1;
    private const uint WM_DROPFILES = 0x0233;
    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_COPYGLOBALDATA = 0x0049;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
    }

    /// <summary>
    /// UIPI Fix: Nach Erstellung des Window-Handles die Message-Filter lockern.
    /// </summary>
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        ChangeWindowMessageFilterEx(hwnd, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(hwnd, WM_COPYDATA, MSGFLT_ALLOW, IntPtr.Zero);
        ChangeWindowMessageFilterEx(hwnd, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var vm = DataContext as MainViewModel;
        if (vm != null)
        {
            var themeIndex = vm.AktuellesTheme switch
            {
                "Light" => 0,
                "Dark" => 1,
                _ => 2
            };
            ThemeComboBox.SelectedIndex = themeIndex;
        }
    }

    // ===== Modus-Umschalter =====

    private void OnBildModeChecked(object sender, RoutedEventArgs e)
    {
        if (VideoBackendPanel != null)
            VideoBackendPanel.Visibility = Visibility.Collapsed;
    }

    private void OnVideoModeChecked(object sender, RoutedEventArgs e)
    {
        if (VideoBackendPanel != null)
            VideoBackendPanel.Visibility = Visibility.Visible;
    }

    private void OnClipsModeChecked(object sender, RoutedEventArgs e)
    {
        if (VideoBackendPanel != null)
            VideoBackendPanel.Visibility = Visibility.Collapsed;
    }

    // ===== Theme-ComboBox =====

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (ThemeComboBox.SelectedIndex < 0) return;

        var theme = ThemeComboBox.SelectedIndex switch
        {
            0 => "Light",
            1 => "Dark",
            _ => "System"
        };

        vm.ThemeWechselnCommand.Execute(theme);
    }

    // ===== Galerie-Auswahl =====

    private void OnGalerieSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (GalerieListe.SelectedItem is not DateiEintrag eintrag) return;

        if (eintrag.Typ == "Bild")
            vm.LoadBild(eintrag.Pfad);
    }

    // ===== Drag & Drop — Preview Events (tunneling) + e.Handled = true =====
    // PreviewDragEnter/Over feuern top-down bevor child controls sie abfangen können.
    // e.Handled = true ist PFLICHT — ohne zeigt Windows das Sperrsymbol.

    private void OnWindowPreviewDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowPreviewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is MainViewModel vm)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 })
            {
                vm.DateienHinzufuegen(files);

                // Erste Bilddatei als Vorschau laden
                var firstImage = files
                    .Where(f => !System.IO.Directory.Exists(f) && MainViewModel.BildEndungen.Contains(System.IO.Path.GetExtension(f)))
                    .FirstOrDefault();
                if (firstImage != null)
                    vm.LoadBild(firstImage);
            }
        }
        e.Handled = true;
    }

    // ===== Alte Drop-Zonen Handler (für Galerie + DropZone Border) =====

    private void OnDragEnterAny(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragOverAny(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDragLeaveAny(object sender, DragEventArgs e)
    {
        e.Handled = true;
    }

    private void OnDropZoneDrop(object sender, DragEventArgs e)
    {
        RemoveDropHighlight();

        if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is MainViewModel vm)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 })
            {
                vm.DateienHinzufuegen(files);
                var firstImage = files
                    .Where(f => !System.IO.Directory.Exists(f) && MainViewModel.BildEndungen.Contains(System.IO.Path.GetExtension(f)))
                    .FirstOrDefault();
                if (firstImage != null)
                    vm.LoadBild(firstImage);
            }
        }
        e.Handled = true;
    }

    private void OnGalerieDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is MainViewModel vm)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 })
            {
                vm.DateienHinzufuegen(files);
                var firstImage = files
                    .Where(f => !System.IO.Directory.Exists(f) && MainViewModel.BildEndungen.Contains(System.IO.Path.GetExtension(f)))
                    .FirstOrDefault();
                if (firstImage != null)
                    vm.LoadBild(firstImage);
            }
        }
        e.Handled = true;
    }

    private void ApplyDropHighlight()
    {
        if (DropZone == null) return;
        _originalDropBorderBrush ??= DropZone.BorderBrush;
        _originalDropBorderThickness = DropZone.BorderThickness;
        DropZone.BorderBrush = (Brush)FindResource("AccentPrimaryBrush");
        DropZone.BorderThickness = new Thickness(3);
    }

    private void RemoveDropHighlight()
    {
        if (DropZone == null) return;
        if (_originalDropBorderBrush != null)
            DropZone.BorderBrush = _originalDropBorderBrush;
        DropZone.BorderThickness = _originalDropBorderThickness;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is MainViewModel vm)
            vm.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}