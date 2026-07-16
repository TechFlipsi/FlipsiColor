using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

using FlipsiColor.Core;
using OpenCvSharp;

namespace FlipsiColor;

/// <summary>
/// BoolToVisibility Converter
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>
/// Inverse BoolToVisibility Converter
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v != Visibility.Visible;
}

/// <summary>
/// OpenCvSharp Mat → WPF BitmapSource Converter (via PNG-Encoding)
/// </summary>
public class MatToBitmapSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => ConvertMat(value as Mat);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;

    /// <summary>Converts an OpenCV Mat to WPF BitmapSource (PNG-encoded, UI-thread safe)</summary>
    public static BitmapSource? ConvertMat(Mat? mat)
    {
        if (mat == null || mat.Empty()) return null;
        Cv2.ImEncode(".png", mat, out var bytes);
        using var ms = new MemoryStream(bytes);
        var decoder = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }
}

/// <summary>
/// Lokalisierungs-Converter: {Binding ., Converter={StaticResource Loc}, ConverterParameter='App.Titel'}
/// Übersetzt einen Lokalisierungsschlüssel in den aktuellen UI-Text.
/// </summary>
public class LocConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string schluessel)
            return Lokalisierung.T(schluessel);
        return parameter?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>
/// VideoBackend-Enum → Bool Converter für RadioButton IsChecked Bindings.
/// ConverterParameter = "FFmpeg" oder "VapourSynth".
/// Convert: true wenn value == ConverterParameter.
/// ConvertBack: parsed den bool zurück in den VideoBackend-Enum.
/// </summary>
public class VideoBackendToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is VideoBackend backend && parameter is string param)
        {
            return param == "VapourSynth"
                ? backend == VideoBackend.VapourSynth
                : backend == VideoBackend.FFmpeg;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is string param)
        {
            return param == "VapourSynth" ? VideoBackend.VapourSynth : VideoBackend.FFmpeg;
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}