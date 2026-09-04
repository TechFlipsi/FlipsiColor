# FlipsiColor

**KI-gestützte Bild- & Videofarbkorrektur für Windows & Linux**

> ⚠️ **Status: v0.8.0 — Low-Light-Enhancement (Issue #20)**
> 
> Die Bild- und Video-Verarbeitung ist implementiert und kompilationsgetestet. Die App lädt 7 ONNX-Modelle automatisch von GitHub Releases herunter. Bitte melden Sie Probleme via GitHub Issues.

## Features

### 🎨 Bild-Korrektur
- **KI-Farbkorrektur** — 7 ONNX-Modelle (NAFNet, RestormerLight, RealHATGAN, RealESRGAN, CodeFormer, AiLUTTransform, EfficientNet)
- **RAW-Unterstützung** — CR2, CR3, NEF, ARW, DNG, ORF, RW2 (LibRaw)
- **Belichtung, Kontrast, Sättigung, Vibranz** — manuelle Regler + KI-Vorschläge
- **Lichter & Schatten** — selektive Korrektur
- **Schärfe & Rauschunterdrückung** — Luminanz + Chrominanz
- **Weißabgleich** — auto + manuelle Farbtemperatur
- **Low-Light-Enhancement (v0.8.0 NEU)** — 11 Verfahren (CLAHE, Gamma, Auto-Levels, MSRCP/SSR, Dehaze, Weißabgleich, Helligkeit, Kombiniert, Stark), Auto-Modus erkennt 4 Dunkelheits-Stufen und wählt adaptiv, auch für Video (Frame-weise)

### 🔲 Objektivkorrektur
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

## v0.7.0 — Feature-Sprint abgeschlossen

Features hinzugefügt im Feature-Sprint (v0.5.0–v0.7.0):

### Pro-Funktion KI-Toggles
- Jede KI-Funktion einzeln an/abschaltbar — KI-Denoising, KI-Schärfung, KI-Upscaling, KI-Gesichtswiederherstellung, KI-Farbstil und KI-Szenenklassifizierung können deaktiviert werden (klassische Filter als Fallback)

### OpenColorIO (OCIO)
- Industrie-Standard Farbmanagement als optionales Backend — LUT-Baking via `ociobakelut`, Default ACES-Config wird automatisch generiert, eigene `.ocio` Configs ladbar

### Clips zusammenfügen
- Automatische Video-Clip-Gruppierung und Zusammenführung (alle Kameras, inkl. DJI Auto-Merge)

### Lokalisierung
- 13 Sprachen (DE, EN, ES, FR, IT, NL, PL, PT, TR, RU, ZH, JA, KO) — JSON-basiert, Systemsprache wird automatisch erkannt, English-Fallback

### Cross-Platform
- Avalonia UI für Linux + Windows (zusätzlich zu WPF)

## Credits

- **Idee:** Fabian Kirchweger
- **Entwicklung:** J.A.R.V.I.S. (Hermes Agent)

### Verwendete KI-Modelle

| Modell              | Rolle        |
|---------------------|--------------|
| **GLM-5.2**         | Hauptmodell  |
| **DeepSeek V4 Pro** | Sub-Agenten  |

## Lizenz

GPL-3.0-or-later © 2026 Fabian Kirchweger (TechFlipsi)