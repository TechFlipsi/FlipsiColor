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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // ViewModel disposen
        if (DataContext is MainViewModel vm)
            vm.Dispose();
    }
}