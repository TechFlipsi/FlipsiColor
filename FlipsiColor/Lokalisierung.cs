using System;
using System.Collections.Generic;

namespace FlipsiColor;

/// <summary>
/// Lokalisierung — Dictionary-basierte Übersetzung für DE/EN.
/// Bei Sprachwechsel werden alle UI-Texte durch Fenster-Neuaufbau aktualisiert.
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
        ["App.Titel"] = "FlipsiColor v0.5.3",
        ["App.Bereit"] = "Bereit",

        // Top-Toolbar
        ["Toolbar.Bild"] = "Bild",
        ["Toolbar.Video"] = "Video",
        ["Toolbar.Clips"] = "Clips",
        ["Toolbar.DateiOeffnen"] = "Datei öffnen",
        ["Toolbar.Export"] = "Export",

        // Theme
        ["Theme.Light"] = "Light",
        ["Theme.Dark"] = "Dark",
        ["Theme.System"] = "System",

        // Sprache
        ["Einstellungen.Sprache"] = "Sprache",
        ["Einstellungen.Sprache.Deutsch"] = "Deutsch",
        ["Einstellungen.Sprache.Englisch"] = "English",

        // Galerie
        ["Galerie.Titel"] = "Galerie",
        ["Galerie.DateiZaehler"] = "{0} Datei(en) geladen",
        ["Galerie.Hinweis"] = "Dateien hierher ziehen oder 'Datei öffnen' klicken",

        // Drop-Zone
        ["DropZone.Platzhalter"] = "Bild öffnen oder Dateien hierher ziehen",
        ["DropZone.Hinweis"] = "Einzelne Bilder oder mehrere Dateien gleichzeitig möglich",
        ["DropZone.FormateTitel"] = "Unterstützte Formate:",
        ["DropZone.FormateListe"] = "JPG, PNG, TIFF, BMP, RAW (CR2, CR3, NEF, ARW, DNG, ORF, RW2)",
        ["DropZone.PipelineLaueft"] = "Pipeline läuft...",

        // Korrektur-Panel
        ["Korrektur.Titel"] = "Korrektur",
        ["Korrektur.Beschreibung"] = "Manuelle Anpassungen — KI wird zusätzlich angewendet",
        ["Korrektur.TonExpander"] = "Ton — Helligkeit & Kontrast",
        ["Korrektur.FarbeExpander"] = "Farbe — Sättigung & Vibranz",
        ["Korrektur.DetailExpander"] = "Detail — Schärfe & Rauschen",
        ["Korrektur.Belichtung"] = "Belichtung",
        ["Korrektur.Kontrast"] = "Kontrast",
        ["Korrektur.Saettigung"] = "Sättigung",
        ["Korrektur.Vibranz"] = "Vibranz",
        ["Korrektur.Lichter"] = "Lichter",
        ["Korrektur.Schatten"] = "Schatten",
        ["Korrektur.Schaerfe"] = "Schärfe",
        ["Korrektur.RauschenLuma"] = "Rauschen L",
        ["Korrektur.RauschenChroma"] = "Rauschen C",

        // Objektivkorrektur
        ["Objektiv.Titel"] = "Objektivkorrektur",
        ["Objektiv.Beschreibung"] = "Entfernt Verzerrungen basierend auf Objektivprofil",
        ["Objektiv.Expander"] = "Kamera & Objektiv manuell wählen",
        ["Objektiv.KameraHersteller"] = "Kamera-Hersteller",
        ["Objektiv.Objektiv"] = "Objektiv",
        ["Objektiv.LeerAuto"] = "Leer = automatisch aus EXIF-Daten",

        // KI-Einstellungen
        ["KI.Titel"] = "KI-Einstellungen",
        ["KI.Beschreibung"] = "Automatische Optimierung durch KI-Modelle",
        ["KI.Modus"] = "Modus",
        ["KI.ModusBeschreibung"] = "Ask: interaktiv | SmartLearn: lernt | Turbo: schnell",
        ["KI.Intensitaet"] = "Intensität der KI-Korrektur",
        ["KI.Hochskalieren"] = "Hochskalieren",
        ["KI.HochskalierenBeschreibung"] = "Vergrößert das Bild mit KI — höhere Auflösung",
        ["KI.Gesichtswiederherstellung"] = "Gesichtswiederherstellung (CodeFormer)",
        ["KI.GesichtBeschreibung"] = "Repariert Gesichter in verpixelten Bildern",
        ["KI.StyleLUT"] = "Style-LUT (.cube)",
        ["KI.Laden"] = "Laden",
        ["KI.Entfernen"] = "Entfernen",

        // Pipeline
        ["Pipeline.Starten"] = "Pipeline starten",
        ["Pipeline.Beschreibung"] = "Wendet KI + manuelle Korrektur auf das Bild an",
        ["Pipeline.Zuruecksetzen"] = "Zurücksetzen",

        // Upscaling
        ["Upscale.Aus"] = "Aus (1x)",
        ["Upscale.2x"] = "2x (RealESRGAN)",
        ["Upscale.3x"] = "3x",
        ["Upscale.4x"] = "4x",

        // Intensität
        ["Intensitaet.Leicht"] = "Leicht",
        ["Intensitaet.Mittel"] = "Mittel",
        ["Intensitaet.Stark"] = "Stark",

        // Video-Backend
        ["Video.Backend.Titel"] = "Video-Backend",
        ["Video.Backend.Beschreibung"] = "Wählt die Verarbeitungsmethode für Videos",
        ["Video.Backend.FFmpeg"] = "FFmpeg (Standard)",
        ["Video.Backend.VapourSynth"] = "VapourSynth (bessere Qualität)",
        ["Video.Backend.ErneutInstallieren"] = "Erneut installieren",

        // Status-Bar
        ["Status.UpdateVerfuegbar"] = "Update verfügbar:",
        ["Status.Installieren"] = "Installieren",

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
        ["Status.ModellNeuHerunterladen"] = "KI-Modelle werden neu heruntergeladen...",
        ["Status.VapourSynthAktiv"] = "VapourSynth aktiv",

        // Dialog-Titel
        ["Dialog.BildOeffnen"] = "Bild öffnen",
        ["Dialog.VideoOeffnen"] = "Video öffnen",
        ["Dialog.LutOeffnen"] = "Style-LUT (.cube) öffnen",
        ["Dialog.SchachbrettOeffnen"] = "Schachbrett-Referenzbild für Verzerrungs-Raster-Kalibrierung öffnen",
        ["Dialog.ColorCheckerOeffnen"] = "ColorChecker- oder Graukarten-Referenzbild für Farbkalibrierung öffnen",
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
        ["App.Titel"] = "FlipsiColor v0.5.3",
        ["App.Bereit"] = "Ready",

        // Top-Toolbar
        ["Toolbar.Bild"] = "Image",
        ["Toolbar.Video"] = "Video",
        ["Toolbar.Clips"] = "Clips",
        ["Toolbar.DateiOeffnen"] = "Open File",
        ["Toolbar.Export"] = "Export",

        // Theme
        ["Theme.Light"] = "Light",
        ["Theme.Dark"] = "Dark",
        ["Theme.System"] = "System",

        // Language
        ["Einstellungen.Sprache"] = "Language",
        ["Einstellungen.Sprache.Deutsch"] = "German",
        ["Einstellungen.Sprache.Englisch"] = "English",

        // Gallery
        ["Galerie.Titel"] = "Gallery",
        ["Galerie.DateiZaehler"] = "{0} file(s) loaded",
        ["Galerie.Hinweis"] = "Drag files here or click 'Open File'",

        // Drop zone
        ["DropZone.Platzhalter"] = "Open image or drag files here",
        ["DropZone.Hinweis"] = "Single images or multiple files at once",
        ["DropZone.FormateTitel"] = "Supported formats:",
        ["DropZone.FormateListe"] = "JPG, PNG, TIFF, BMP, RAW (CR2, CR3, NEF, ARW, DNG, ORF, RW2)",
        ["DropZone.PipelineLaueft"] = "Pipeline running...",

        // Correction panel
        ["Korrektur.Titel"] = "Adjustments",
        ["Korrektur.Beschreibung"] = "Manual adjustments — AI applied additionally",
        ["Korrektur.TonExpander"] = "Tone — Brightness & Contrast",
        ["Korrektur.FarbeExpander"] = "Color — Saturation & Vibrance",
        ["Korrektur.DetailExpander"] = "Detail — Sharpness & Noise",
        ["Korrektur.Belichtung"] = "Exposure",
        ["Korrektur.Kontrast"] = "Contrast",
        ["Korrektur.Saettigung"] = "Saturation",
        ["Korrektur.Vibranz"] = "Vibrance",
        ["Korrektur.Lichter"] = "Highlights",
        ["Korrektur.Schatten"] = "Shadows",
        ["Korrektur.Schaerfe"] = "Sharpness",
        ["Korrektur.RauschenLuma"] = "Noise L",
        ["Korrektur.RauschenChroma"] = "Noise C",

        // Lens correction
        ["Objektiv.Titel"] = "Lens Correction",
        ["Objektiv.Beschreibung"] = "Removes distortion based on lens profile",
        ["Objektiv.Expander"] = "Select camera & lens manually",
        ["Objektiv.KameraHersteller"] = "Camera manufacturer",
        ["Objektiv.Objektiv"] = "Lens",
        ["Objektiv.LeerAuto"] = "Empty = automatic from EXIF data",

        // AI settings
        ["KI.Titel"] = "AI Settings",
        ["KI.Beschreibung"] = "Automatic optimization with AI models",
        ["KI.Modus"] = "Mode",
        ["KI.ModusBeschreibung"] = "Ask: interactive | SmartLearn: learns | Turbo: fast",
        ["KI.Intensitaet"] = "AI correction intensity",
        ["KI.Hochskalieren"] = "Upscale",
        ["KI.HochskalierenBeschreibung"] = "Enlarges image with AI — higher resolution",
        ["KI.Gesichtswiederherstellung"] = "Face Restoration (CodeFormer)",
        ["KI.GesichtBeschreibung"] = "Repairs faces in pixelated images",
        ["KI.StyleLUT"] = "Style LUT (.cube)",
        ["KI.Laden"] = "Load",
        ["KI.Entfernen"] = "Remove",

        // Pipeline
        ["Pipeline.Starten"] = "Start Pipeline",
        ["Pipeline.Beschreibung"] = "Applies AI + manual correction to image",
        ["Pipeline.Zuruecksetzen"] = "Reset",

        // Upscaling
        ["Upscale.Aus"] = "Off (1x)",
        ["Upscale.2x"] = "2x (RealESRGAN)",
        ["Upscale.3x"] = "3x",
        ["Upscale.4x"] = "4x",

        // Intensity
        ["Intensitaet.Leicht"] = "Light",
        ["Intensitaet.Mittel"] = "Medium",
        ["Intensitaet.Stark"] = "Strong",

        // Video backend
        ["Video.Backend.Titel"] = "Video Backend",
        ["Video.Backend.Beschreibung"] = "Selects the processing method for videos",
        ["Video.Backend.FFmpeg"] = "FFmpeg (Standard)",
        ["Video.Backend.VapourSynth"] = "VapourSynth (better quality)",
        ["Video.Backend.ErneutInstallieren"] = "Reinstall",

        // Status bar
        ["Status.UpdateVerfuegbar"] = "Update available:",
        ["Status.Installieren"] = "Install",

        // Status messages
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
        ["Status.VapourSynthAktiv"] = "VapourSynth active",

        // Dialog titles
        ["Dialog.BildOeffnen"] = "Open Image",
        ["Dialog.VideoOeffnen"] = "Open Video",
        ["Dialog.LutOeffnen"] = "Open Style LUT (.cube)",
        ["Dialog.SchachbrettOeffnen"] = "Open chessboard reference image for distortion grid calibration",
        ["Dialog.ColorCheckerOeffnen"] = "Open ColorChecker or gray card reference image for color calibration",
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