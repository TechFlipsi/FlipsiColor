using System;
using System.Collections.Generic;
using System.IO;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Icc;
using MetadataExtractor.Formats.Jpeg;

using FlipsiColor.Utils;

namespace FlipsiColor.Image;

/// <summary>
/// EXIF-Leser via MetadataExtractor (reines .NET — besser als libexif)
/// </summary>
public class ExifReader
{
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext<ExifReader>();

    /// <summary>
    /// Liest alle EXIF-Daten aus einem Bild
    /// </summary>
    public static Dictionary<string, string> Lesen(string pfad)
    {
        var result = new Dictionary<string, string>();

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(pfad);

            foreach (var dir in directories)
            {
                foreach (var tag in dir.Tags)
                {
                    var key = $"{dir.Name}/{tag.Name}";
                    result[key] = tag.Description ?? tag.RawValue?.ToString() ?? "";
                }
            }

            Log.Debug("{Pfad}: {Anzahl} EXIF-Tags gelesen", pfad, result.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "EXIF-Lesung fehlgeschlagen: {Pfad}", pfad);
        }

        return result;
    }

    /// <summary>
    /// Liest spezifische EXIF-Felder für FlipsiColor
    /// </summary>
    public static ExifDaten LesenKompakt(string pfad)
    {
        var daten = new ExifDaten();

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(pfad);

            // Kamera & Objektiv
            foreach (var dir in directories)
            {
                foreach (var tag in dir.Tags)
                {
                    switch (tag.Name)
                    {
                        case "Model":
                            daten.Kamera = tag.Description ?? "";
                            break;
                        case "Lens Model":
                        case "LensModel":
                            daten.Objektiv = tag.Description ?? "";
                            break;
                        case "Focal Length":
                        case "FocalLength":
                            daten.Brennweite = tag.Description ?? "";
                            break;
                        case "F-Number":
                        case "FNumber":
                            daten.Blende = tag.Description ?? "";
                            break;
                        case "Exposure Time":
                        case "ExposureTime":
                            daten.Verschluesszeit = tag.Description ?? "";
                            break;
                        case "ISO":
                        case "ISO Speed Ratings":
                            daten.Iso = tag.Description ?? "";
                            break;
                        case "White Balance Mode":
                            daten.Weissabgleich = tag.Description ?? "";
                            break;
                        case "Flash":
                            daten.Blitz = tag.Description ?? "";
                            break;
                        case "Date/Time":
                        case "Date/Time Original":
                            daten.Aufnahmedatum = tag.Description ?? "";
                            break;
                        case "Image Width":
                            daten.Breite = tag.Description ?? "";
                            break;
                        case "Image Height":
                            daten.Hoehe = tag.Description ?? "";
                            break;
                    }
                }
            }

            Log.Debug("EXIF kompakt: {Kamera} {Objektiv} {Brennweite}", daten.Kamera, daten.Objektiv, daten.Brennweite);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "EXIF-Lesung fehlgeschlagen: {Pfad}", pfad);
        }

        return daten;
    }
}

/// <summary>
/// Kompakte EXIF-Daten für FlipsiColor
/// </summary>
public sealed class ExifDaten
{
    public string Kamera { get; set; } = "";
    public string Objektiv { get; set; } = "";
    public string Brennweite { get; set; } = "";
    public string Blende { get; set; } = "";
    public string Verschluesszeit { get; set; } = "";
    public string Iso { get; set; } = "";
    public string Weissabgleich { get; set; } = "";
    public string Blitz { get; set; } = "";
    public string Aufnahmedatum { get; set; } = "";
    public string Breite { get; set; } = "";
    public string Hoehe { get; set; } = "";
}