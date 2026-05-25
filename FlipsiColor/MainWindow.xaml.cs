using System.Windows;
using FlipsiColor.UI;

namespace FlipsiColor;

/// <summary>
/// MainWindow Code-Behind
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>DragEnter: Accept image file drops</summary>
    private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files is { Length: 1 } && IsImageFile(files[0]))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    /// <summary>Drop: Load image file into ViewModel</summary>
    private void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) && DataContext is MainViewModel vm)
        {
            var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files is { Length: > 0 } && IsImageFile(files[0]))
            {
                vm.LoadBild(files[0]);
            }
        }
    }

    private static bool IsImageFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" or ".bmp"
            or ".cr2" or ".cr3" or ".nef" or ".arw" or ".dng" or ".orf" or ".rw2";
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // ViewModel disposen
        if (DataContext is MainViewModel vm)
            vm.Dispose();
    }
}