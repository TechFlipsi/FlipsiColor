using System;
using System.Collections.Generic;

namespace FlipsiColor;

/// <summary>
/// Lokalisierung — Dictionary-basierte Übersetzung für DE/EN.
/// Bei Sprachwechsel werden alle UI-Texte sofort aktualisiert.
/// </summary>
public static class Lokalisierung
{
    /// <summary>
    /// Aktuelle Sprache ("de" oder "en").
    /// </summary>
    public static string Sprache { get; private set; } = "de";

    /// <summary>
    /// Event das bei Sprachwechsel ausgelöst wird.
    /// </summary>
    public static event EventHandler? SpracheGeaendert;

    private static readonly Dictionary<string, string> De = new()
    {
        // App
        ["App.Titel"] = "FlipsiColor v0.4.1",
        ["App.Bereit"] = "Bereit",

        // Sidebar
        ["Sidebar.Ansicht"] = "Ansicht",
        ["Sidebar.GPU"] = "GPU",
        ["Sidebar.UpdateVerfuegbar"] = "Update verfügbar!",
        ["Sidebar.Installieren"] = "Installieren",
        ["Sidebar.Ignorieren"] = "Ignorieren",
        ["Sidebar.Design"] = "Design",
        ["Sidebar.Hell"] = "Hell",
        ["Sidebar.Dunkel"] = "Dunkel",
        ["Sidebar.System"] = "System",

        // Bild-Tab
        ["Bild.Tab"] = "🎨 Bild",
        ["Bild.Modus"] = "Modus",
        ["Bild.Modus.Fragen"] = "Fragen",
        ["Bild.Modus.Lernen"] = "Lernen",
        ["Bild.Modus.Auto"] = "Auto",
        ["Bild.Intensitaet"] = "Intensität",
        ["Bild.Intensitaet.Leicht"] = "Leicht",
        ["Bild.Intensitaet.Mittel"] = "Mittel",
        ["Bild.Intensitaet.Stark"] = "Stark",
        ["Bild.Oeffnen"] = "📁 Bild öffnen",
        ["Bild.PipelineStarten"] = "▶ Pipeline starten",
        ["Bild.Zuruecksetzen"] = "↺ Zurücksetzen",
        ["Bild.Hochskalieren"] = "Hochskalieren",
        ["Bild.Hochskalieren.Aus"] = "Aus (1x)",
        ["Bild.Gesichtswiederherstellung"] = "👤 Gesichtswiederherstellung (CodeFormer)",
        ["Bild.FarbstilLUT"] = "Farbstil-LUT",
        ["Bild.Laden"] = "📂 Laden",
        ["Bild.Entfernen"] = "✕ Entfernen",
        ["Bild.VerzerrungsRaster"] = "🔲 Verzerrungs-Raster",
        ["Bild.Kalibrieren"] = "Kalibrieren",
        ["Bild.Farbkalibrierung"] = "🎨 Farbkalibrierung",
        ["Bild.Platzhalter"] = "Bild öffnen oder hierher ziehen",

        // Video-Tab
        ["Video.Tab"] = "🎬 Video",
        ["Video.Pipeline"] = "Video-Pipeline",
        ["Video.Beschreibung"] = "Video-Farbkorrektur mit Szenenwechsel-Erkennung und Audio-Erhaltung.",
        ["Video.Oeffnen"] = "🎬 Video öffnen",
        ["Video.PipelineStarten"] = "▶ Video-Pipeline starten",

        // Clips-Tab
        ["Clips.Tab"] = "🎬 Clips zusammenfügen",
        ["Clips.Titel"] = "Clips zusammenfügen",
        ["Clips.Beschreibung"] = "Fügt Video-Clips automatisch zusammen (alle Kameras).",
        ["Clips.OrdnerOeffnen"] = "📂 Ordner auswählen",
        ["Clips.Farbkorrektur"] = "Farbkorrektur nach Zusammenfügen",
        ["Clips.Farbkorrektur.Info"] = "An: Clips werden zusammengefügt UND farbkorrigiert. Aus: Nur Zusammenfügen.",
        ["Clips.GruppenErkannt"] = "Erkannte Clip-Gruppen:",
        ["Clips.AusgewaehlteZusammenfuegen"] = "🔗 Ausgewählte zusammenfügen",
        ["Clips.AlleZusammenfuegen"] = "🔗 Alle zusammenfügen",
        ["Clips.Clips"] = " Clips · ",

        // Einstellungen-Tab
        ["Einstellungen.Tab"] = "⚙ Einstellungen",
        ["Einstellungen.Sprache"] = "Sprache",
        ["Einstellungen.Sprache.Deutsch"] = "Deutsch",
        ["Einstellungen.Sprache.Englisch"] = "Englisch",
        ["Einstellungen.Design"] = "Design",
        ["Einstellungen.AutoUpdate"] = "Auto-Update prüfen",
        ["Einstellungen.ModellVerzeichnis"] = "Modell-Verzeichnis",
        ["Einstellungen.ModellNeuHerunterladen"] = "KI-Modelle neu herunterladen",
        ["Einstellungen.Speichern"] = "Speichern",

        // Adjustments
        ["Korrektur.Titel"] = "Korrektur",
        ["Korrektur.Belichtung"] = "Belichtung",
        ["Korrektur.Kontrast"] = "Kontrast",
        ["Korrektur.Saettigung"] = "Sättigung",
        ["Korrektur.Vibranz"] = "Vibranz",
        ["Korrektur.Lichter"] = "Lichter",
        ["Korrektur.Schatten"] = "Schatten",
        ["Korrektur.Schaerfe"] = "Schärfe",
        ["Korrektur.RauschenLuma"] = "Rauschen (Luma)",
        ["Korrektur.RauschenChroma"] = "Rauschen (Chroma)",
        ["Korrektur.Objektivkorrektur"] = "Objektivkorrektur",

        // Datei-Liste
        ["DateiListe.Titel"] = "Dateien",
        ["DateiListe.Entfernen"] = "Entfernen",
        ["DateiListe.AlleLeeren"] = "Alle leeren",

        // Status-Meldungen
        ["Status.Geladen"] = "Geladen",
        ["Status.PipelineLaeuft"] = "Pipeline läuft...",
        ["Status.PipelineAbgeschlossen"] = "Pipeline abgeschlossen",
        ["Status.PipelineFehler"] = "Pipeline-Fehler",
        ["Status.PipelineFehlgeschlagen"] = "Pipeline fehlgeschlagen",
        ["Status.ParameterZurueckgesetzt"] = "Parameter zurückgesetzt",
        ["Status.BildKonnteNichtGeladenWerden"] = "Bild konnte nicht geladen werden.",
        ["Status.VideoKonnteNichtGeladenWerden"] = "Video konnte nicht geladen werden.",
        ["Status.Fehler"] = "Fehler",
        ["Status.VideoGeladen"] = "Video geladen",
        ["Status.VideoPipelineLaeuft"] = "Video-Pipeline läuft...",
        ["Status.VideoVerarbeitung"] = "Video-Verarbeitung",
        ["Status.VideoPipelineAbgeschlossen"] = "Video-Pipeline abgeschlossen",
        ["Status.VideoPipelineFehler"] = "Video-Pipeline-Fehler",
        ["Status.VideoPipelineFehlgeschlagen"] = "Video-Pipeline fehlgeschlagen",
        ["Status.DistortionGridErfolgreich"] = "Verzerrungs-Raster-Kalibrierung erfolgreich",
        ["Status.DistortionGridFehlgeschlagen"] = "Verzerrungs-Raster-Kalibrierung fehlgeschlagen",
        ["Status.FarbkalibrierungErfolgreich"] = "Farbkalibrierung erfolgreich",
        ["Status.FarbkalibrierungFehlgeschlagen"] = "Farbkalibrierung fehlgeschlagen",
        ["Status.FarbstilLutGeladen"] = "Farbstil-LUT geladen",
        ["Status.FarbstilLutEntfernt"] = "Farbstil-LUT entfernt",
        ["Status.ClipGruppenErkannt"] = "Clip-Gruppen erkannt",
        ["Status.ClipsZusammenfuegen"] = "Füge Clips zusammen...",
        ["Status.Fertig"] = "Fertig",
        ["Status.ZusammenfuegenFehlgeschlagen"] = "Zusammenfügen fehlgeschlagen",
        ["Status.ZusammenfuegenMitFarbkorrekturFehlgeschlagen"] = "Zusammenfügen mit Farbkorrektur fehlgeschlagen",
        ["Status.AlleGruppenVerarbeitet"] = "Alle Gruppen verarbeitet",
        ["Status.MergeFehler"] = "Zusammenfügen-Fehler",
        ["Status.FehlerBeimBulkMerge"] = "Fehler bei Bulk-Merge",
        ["Status.UpdatePruefungFehlgeschlagen"] = "Update-Prüfung fehlgeschlagen",
        ["Status.UpdateFehlgeschlagen"] = "Update fehlgeschlagen",
        ["Status.ThemeFehler"] = "Design-Fehler",
        ["Status.ModellNeuHerunterladen"] = "KI-Modelle werden neu herunterladen...",

        // Dialog-Titel
        ["Dialog.BildOeffnen"] = "Bild öffnen",
        ["Dialog.VideoOeffnen"] = "Video öffnen",
        ["Dialog.LutOeffnen"] = "Farbstil-LUT (.cube) öffnen",
        ["Dialog.SchachbrettOeffnen"] = "Schachbrett-Referenzbild für Verzerrungs-Raster-Kalibrierung öffnen",
        ["Dialog.ColorCheckerOeffnen"] = "ColorChecker- oder Graukarten-Referenzbild für Farbkalibrierung öffnen",
        ["Dialog.ClipOrdner"] = "Ordner mit Video-Clips auswählen",
        ["Dialog.Bilddateien"] = "Bilddateien",
        ["Dialog.Videodateien"] = "Videodateien",
        ["Dialog.LUTDateien"] = "LUT-Dateien",
        ["Dialog.AlleDateien"] = "Alle Dateien",

        // Filter
        ["Filter.Bilder"] = "*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.cr2;*.cr3;*.nef;*.arw;*.dng;*.orf;*.rw2",
        ["Filter.Videos"] = "*.mp4;*.mov;*.avi;*.mkv;*.mxf",
        ["Filter.LUT"] = "*.cube",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        // App
        ["App.Titel"] = "FlipsiColor v0.4.1",
        ["App.Bereit"] = "Ready",

        // Sidebar
        ["Sidebar.Ansicht"] = "View",
        ["Sidebar.GPU"] = "GPU",
        ["Sidebar.UpdateVerfuegbar"] = "Update available!",
        ["Sidebar.Installieren"] = "Install",
        ["Sidebar.Ignorieren"] = "Ignore",
        ["Sidebar.Design"] = "Theme",
        ["Sidebar.Hell"] = "Light",
        ["Sidebar.Dunkel"] = "Dark",
        ["Sidebar.System"] = "System",

        // Bild-Tab
        ["Bild.Tab"] = "🎨 Image",
        ["Bild.Modus"] = "Mode",
        ["Bild.Modus.Fragen"] = "Ask",
        ["Bild.Modus.Lernen"] = "Learn",
        ["Bild.Modus.Auto"] = "Auto",
        ["Bild.Intensitaet"] = "Intensity",
        ["Bild.Intensitaet.Leicht"] = "Light",
        ["Bild.Intensitaet.Mittel"] = "Medium",
        ["Bild.Intensitaet.Stark"] = "Strong",
        ["Bild.Oeffnen"] = "📁 Open Image",
        ["Bild.PipelineStarten"] = "▶ Start Pipeline",
        ["Bild.Zuruecksetzen"] = "↺ Reset",
        ["Bild.Hochskalieren"] = "Upscale",
        ["Bild.Hochskalieren.Aus"] = "Off (1x)",
        ["Bild.Gesichtswiederherstellung"] = "👤 Face Restoration (CodeFormer)",
        ["Bild.FarbstilLUT"] = "Style LUT",
        ["Bild.Laden"] = "📂 Load",
        ["Bild.Entfernen"] = "✕ Remove",
        ["Bild.VerzerrungsRaster"] = "🔲 Distortion Grid",
        ["Bild.Kalibrieren"] = "Calibrate",
        ["Bild.Farbkalibrierung"] = "🎨 Color Calibration",
        ["Bild.Platzhalter"] = "Open image or drag here",

        // Video-Tab
        ["Video.Tab"] = "🎬 Video",
        ["Video.Pipeline"] = "Video Pipeline",
        ["Video.Beschreibung"] = "Video color correction with scene detection and audio preservation.",
        ["Video.Oeffnen"] = "🎬 Open Video",
        ["Video.PipelineStarten"] = "▶ Start Video Pipeline",

        // Clips-Tab
        ["Clips.Tab"] = "🎬 Merge Clips",
        ["Clips.Titel"] = "Merge Clips",
        ["Clips.Beschreibung"] = "Automatically merges video clips (all cameras).",
        ["Clips.OrdnerOeffnen"] = "📂 Select Folder",
        ["Clips.Farbkorrektur"] = "Color correction after merging",
        ["Clips.Farbkorrektur.Info"] = "On: Clips are merged AND color-corrected. Off: Merge only.",
        ["Clips.GruppenErkannt"] = "Detected clip groups:",
        ["Clips.AusgewaehlteZusammenfuegen"] = "🔗 Merge Selected",
        ["Clips.AlleZusammenfuegen"] = "🔗 Merge All",
        ["Clips.Clips"] = " clips · ",

        // Einstellungen-Tab
        ["Einstellungen.Tab"] = "⚙ Settings",
        ["Einstellungen.Sprache"] = "Language",
        ["Einstellungen.Sprache.Deutsch"] = "German",
        ["Einstellungen.Sprache.Englisch"] = "English",
        ["Einstellungen.Design"] = "Theme",
        ["Einstellungen.AutoUpdate"] = "Check for updates",
        ["Einstellungen.ModellVerzeichnis"] = "Model directory",
        ["Einstellungen.ModellNeuHerunterladen"] = "Re-download AI models",
        ["Einstellungen.Speichern"] = "Save",

        // Adjustments
        ["Korrektur.Titel"] = "Adjustments",
        ["Korrektur.Belichtung"] = "Exposure",
        ["Korrektur.Kontrast"] = "Contrast",
        ["Korrektur.Saettigung"] = "Saturation",
        ["Korrektur.Vibranz"] = "Vibrance",
        ["Korrektur.Lichter"] = "Highlights",
        ["Korrektur.Schatten"] = "Shadows",
        ["Korrektur.Schaerfe"] = "Sharpness",
        ["Korrektur.RauschenLuma"] = "Noise (Luma)",
        ["Korrektur.RauschenChroma"] = "Noise (Chroma)",
        ["Korrektur.Objektivkorrektur"] = "Lens Correction",

        // Datei-Liste
        ["DateiListe.Titel"] = "Files",
        ["DateiListe.Entfernen"] = "Remove",
        ["DateiListe.AlleLeeren"] = "Clear all",

        // Status-Meldungen
        ["Status.Geladen"] = "Loaded",
        ["Status.PipelineLaeuft"] = "Pipeline running...",
        ["Status.PipelineAbgeschlossen"] = "Pipeline complete",
        ["Status.PipelineFehler"] = "Pipeline error",
        ["Status.PipelineFehlgeschlagen"] = "Pipeline failed",
        ["Status.ParameterZurueckgesetzt"] = "Parameters reset",
        ["Status.BildKonnteNichtGeladenWerden"] = "Image could not be loaded.",
        ["Status.VideoKonnteNichtGeladenWerden"] = "Video could not be loaded.",
        ["Status.Fehler"] = "Error",
        ["Status.VideoGeladen"] = "Video loaded",
        ["Status.VideoPipelineLaeuft"] = "Video pipeline running...",
        ["Status.VideoVerarbeitung"] = "Video processing",
        ["Status.VideoPipelineAbgeschlossen"] = "Video pipeline complete",
        ["Status.VideoPipelineFehler"] = "Video pipeline error",
        ["Status.VideoPipelineFehlgeschlagen"] = "Video pipeline failed",
        ["Status.DistortionGridErfolgreich"] = "Distortion grid calibration successful",
        ["Status.DistortionGridFehlgeschlagen"] = "Distortion grid calibration failed",
        ["Status.FarbkalibrierungErfolgreich"] = "Color calibration successful",
        ["Status.FarbkalibrierungFehlgeschlagen"] = "Color calibration failed",
        ["Status.FarbstilLutGeladen"] = "Style LUT loaded",
        ["Status.FarbstilLutEntfernt"] = "Style LUT removed",
        ["Status.ClipGruppenErkannt"] = "Clip groups detected",
        ["Status.ClipsZusammenfuegen"] = "Merging clips...",
        ["Status.Fertig"] = "Done",
        ["Status.ZusammenfuegenFehlgeschlagen"] = "Merge failed",
        ["Status.ZusammenfuegenMitFarbkorrekturFehlgeschlagen"] = "Merge with color correction failed",
        ["Status.AlleGruppenVerarbeitet"] = "All groups processed",
        ["Status.MergeFehler"] = "Merge error",
        ["Status.FehlerBeimBulkMerge"] = "Bulk merge error",
        ["Status.UpdatePruefungFehlgeschlagen"] = "Update check failed",
        ["Status.UpdateFehlgeschlagen"] = "Update failed",
        ["Status.ThemeFehler"] = "Theme error",
        ["Status.ModellNeuHerunterladen"] = "Re-downloading AI models...",

        // Dialog-Titel
        ["Dialog.BildOeffnen"] = "Open Image",
        ["Dialog.VideoOeffnen"] = "Open Video",
        ["Dialog.LutOeffnen"] = "Open Style LUT (.cube)",
        ["Dialog.SchachbrettOeffnen"] = "Open chessboard reference image for distortion grid calibration",
        ["Dialog.ColorCheckerOeffnen"] = "Open ColorChecker or gray card reference image for color calibration",
        ["Dialog.ClipOrdner"] = "Select folder with video clips",
        ["Dialog.Bilddateien"] = "Image files",
        ["Dialog.Videodateien"] = "Video files",
        ["Dialog.LUTDateien"] = "LUT files",
        ["Dialog.AlleDateien"] = "All files",

        // Filter
        ["Filter.Bilder"] = "*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp;*.cr2;*.cr3;*.nef;*.arw;*.dng;*.orf;*.rw2",
        ["Filter.Videos"] = "*.mp4;*.mov;*.avi;*.mkv;*.mxf",
        ["Filter.LUT"] = "*.cube",
    };

    /// <summary>
    /// Setzt die Sprache und löst das SpracheGeaendert-Event aus.
    /// </summary>
    public static void SpracheSetzen(string sprache)
    {
        if (sprache != "de" && sprache != "en") sprache = "de";
        if (Sprache == sprache) return;
        Sprache = sprache;
        SpracheGeaendert?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Gibt den übersetzten Text für den gegebenen Schlüssel zurück.
    /// Fällt auf den Schlüssel selbst zurück, wenn keine Übersetzung gefunden wird.
    /// </summary>
    public static string T(string schluessel)
    {
        var dict = Sprache == "en" ? En : De;
        return dict.TryGetValue(schluessel, out var wert) ? wert : schluessel;
    }
}