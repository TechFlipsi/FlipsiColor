# FlipsiColor

**KI-gestützte Bild- & Videofarbkorrektur für Windows & Linux**

> ⚠️ **Status: v0.4.3 — Bild- und Video-Verarbeitung noch nicht getestet!**
> 
> Die aktuellen Funktionen (Farbkorrektur, Objektivkorrektur, VapourSynth, KI-Modelle) sind implementiert und kompilieren fehlerfrei, aber die **tatsächliche Bild- und Video-Verarbeitung wurde noch nicht mit echten Dateien getestet**. Fehler bei der Verarbeitung sind wahrscheinlich. Bitte melden Sie Probleme via GitHub Issues.

## Features

### 🎨 Bild-Korrektur
- **KI-Farbkorrektur** — 7 ONNX-Modelle (NAFNet, RestormerLight, RealHATGAN, RealESRGAN, CodeFormer, AiLUTTransform, EfficientNet)
- **RAW-Unterstützung** — CR2, CR3, NEF, ARW, DNG, ORF, RW2 (LibRaw)
- **Belichtung, Kontrast, Sättigung, Vibranz** — manuelle Regler + KI-Vorschläge
- **Lichter & Schatten** — selektive Korrektur
- **Schärfe & Rauschunterdrückung** — Luminanz + Chrominanz
- **Weißabgleich** — auto + manuelle Farbtemperatur

### 🔲 Objektivkorrektur (v0.3.0 NEU)
- **Lensfun-Integration** — Verzeichnung, Vignetting, chromatische Aberration via Lensfun-Datenbank
- **Distortion Grid** — Kalibrierung mit Schachbrett-Referenzmuster (OpenCV calibrateCamera + undistort)
- **Color Calibration** — Macbeth ColorChecker 24-Feld-Kalibrierung + Graukarten-Weißabgleich

### 🎬 Video & DJI
- **Video-Pipeline** — Frame-Video-Verarbeitung mit FFMPEG
- **DJI Auto-Merge** — automatisches Zusammenfügen von DJI Clips (Osmo 360, Pocket 4) + optionale Farbkorrektur
- **Szenenerkennung** — KI-basierte automatische Parameter-Vorschläge

### 🖥 System
- **Dark/Light Theme** — System-Erkennung + manueller Wechsel
- **Auto-Updater** — GitHub Releases API, Downgrade-Schutz
- **GPU-Beschleunigung** — DirectML (CUDA Fallback)
- **EXIF-Leser** — MetadataExtractor (reines .NET)

## Systemanforderungen

- Windows 10 (19041+) oder neuer
- 8 GB RAM
- GPU empfohlen (DirectML)

## Build

```bash
dotnet publish FlipsiColor/FlipsiColor.csproj -c Release -r win-x64 --self-contained true -o FlipsiColor/publish
```

## Tech Stack

- .NET 10 / C# / WPF
- OpenCvSharp4 (Image Processing, CalibrateCamera, Undistort)
- ONNX Runtime + DirectML (KI-Inferenz)
- Lensfun (Objektivkorrektur via P/Invoke)
- Serilog (Logging)
- CommunityToolkit.Mvvm (MVVM)
- LibRaw.Native (RAW-Decoder)
- MetadataExtractor (EXIF)
- FFMPEG (Video)

## Pipeline-Architektur

Die Bild-Pipeline verarbeitet in 10 Schritten:

1. **Weißabgleich** — Auto-WB oder manuelle Farbtemperatur
2. **Belichtung** — Helligkeitsanpassung
3. **Kontrast** — Alpha/Beta-Korrektur
4. **Lichter** — selektive Aufhellung heller Bereiche
5. **Schatten** — selektive Aufhellung dunkler Bereiche
6. **Sättigung & Vibranz** — HSV-basierte Farbkorrektur
7. **Schärfe** — Unsharp Masking (GaussianBlur + AddWeighted)
8. **Rauschunterdrückung** — Luminanz (Gaussian) + Chrominanz
9. **Objektivkorrektur** — Lensfun (Verzeichnung, TCA, Vignetting)
10. **Distortion Grid** — OpenCV calibrateCamera + undistort (optional)
11. **Color Calibration** — Macbeth ColorChecker / Graukarte (optional)

## v0.3.0 — Advanced Color & Lens Correction

Neue Features inspiriert durch [Marco Ravich's Feature Request](https://github.com/Video-Capture-Guide/VCG-Deinterlacer/issues/13):

### Distortion Grid Korrektur
- Schachbrett-Referenzmuster fotografieren → Kalibrierung
- OpenCV `FindChessboardCorners` + `CalibrateCamera` + `Undistort`
- Kalibrierung speicherbar als JSON

### Color Calibration
- **ColorChecker-Modus:** Erkennt 24-Feld Macbeth ColorChecker, berechnet 3×3 Farb-Transfer-Matrix via Least-Squares (SVD)
- **Graukarten-Modus:** Erkennt neutrale Graufläche, berechnet Weißabgleich-Matrix
- **Auto-Modus:** Versucht ColorChecker, fällt auf Graukarte zurück
- Kalibrierung speicherbar als JSON

### Lensfun Objektivkorrektur (fertig implementiert)
- P/Invoke der Lensfun C-API (`lf_modifier_create`, `apply_subpixel_geometry_distortion`, `apply_color_modification`)
- Verzeichnungskorrektur via `Cv2.Remap` pro Kanal
- Vignetting-Korrektur in-place
- Chromatische Aberration (TCA)

## Credits

- **Idee:** Fabian Kirchweger
- **Entwicklung:** J.A.R.V.I.S. (Hermes Agent)

### Verwendete KI-Modelle

| Modell              | Rolle        |
|---------------------|--------------|
| **GLM-5.2**         | Hauptmodell  |
| **GLM-5.2**         | Sub-Agenten  |

## Lizenz

GPL-3.0-or-later © 2026 Fabian Kirchweger (TechFlipsi)