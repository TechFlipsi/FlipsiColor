using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using OpenCvSharp;

namespace FlipsiColor;

/// <summary>
/// OpenCvSharp Mat → Avalonia Bitmap Converter (via PNG-Encoding).
/// BitmapImage → Avalonia.Media.Imaging.Bitmap (Pitfall: BitmapImage existiert in Avalonia nicht).
/// </summary>
public class MatToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ConvertMat(value as Mat);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    /// <summary>Converts an OpenCV Mat to Avalonia Bitmap (PNG-encoded, UI-thread safe)</summary>
    public static Bitmap? ConvertMat(Mat? mat)
    {
        if (mat == null || mat.Empty()) return null;
        Cv2.ImEncode(".png", mat, out var bytes);
        using var ms = new MemoryStream(bytes);
        return new Bitmap(ms);
    }
}

/// <summary>
/// Bool → Visibility Converter. Avalonia verwendet IsVisible (bool), nicht Visibility enum.
/// Pitfall: Visibility.Collapsed → IsVisible="False" (bool, nicht enum).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b;
}

/// <summary>
/// Inverse Bool → Visibility Converter.
/// </summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>
/// Lokalisierungs-Converter: {Binding ., Converter={StaticResource LocConverter}, ConverterParameter='App.Titel'}
/// </summary>
public class LocConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string schluessel)
            return Lokalisierung.T(schluessel);
        return parameter?.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}