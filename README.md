# FlipsiColor

[![Build](https://img.shields.io/github/actions/workflow/status/TechFlipsi/FlipsiColor/ci.yml?branch=main&label=Build)](https://github.com/TechFlipsi/FlipsiColor/actions)
[![Version](https://img.shields.io/github/v/release/TechFlipsi/FlipsiColor?label=Version)](https://github.com/TechFlipsi/FlipsiColor/releases/latest)
[![License](https://img.shields.io/github/license/TechFlipsi/FlipsiColor?label=License)](https://github.com/TechFlipsi/FlipsiColor/blob/main/LICENSE)
[![Downloads](https://img.shields.io/github/downloads/TechFlipsi/FlipsiColor/total?label=Downloads)](https://github.com/TechFlipsi/FlipsiColor/releases)
[![Discord](https://img.shields.io/discord/1496261911677894867?label=Discord)](https://discord.gg/zHPhQ7EaqH)

KI-gestützte Bild- & Videofarbkorrektur. Cross-Platform (Linux + Windows) mit Avalonia UI und .NET 10.

## Features

- **Bild-Pipeline:** KI-Farbkorrektur mit NAFNet/Restormer, Farbkalibrierung, Verzerrungs-Raster
- **RAW-Unterstützung:** CR2, CR3, NEF, ARW, DNG (via LibRaw)
- **Objektivkorrektur:** Lensfun-Integration mit automatischer EXIF-Erkennung (Kamera + Objektiv), Verzeichnung-, Vignetting- und TCA-Korrektur
- **Video-Pipeline:** Frame-weise Farbkorrektur mit Szenenwechsel-Erkennung und Audio-Erhaltung
- **VapourSynth-Backend:** Optionaler Video-Backend mit Auto-Installation (alternativ zu FFmpeg)
- **Clips zusammenfügen:** Automatische Video-Clip-Gruppierung und Zusammenführung (alle Kameras, inkl. DJI Auto-Merge)
- **Hochskalieren:** RealESRGAN (2x/3x/4x)
- **Gesichtswiederherstellung:** CodeFormer
- **Farbstil-LUT:** .cube LUT-Dateien laden und anwenden
- **OpenColorIO (OCIO):** Industrie-Standard Farbmanagement als optionales Backend — LUT-Baking via `ociobakelut`, Default ACES-Config wird automatisch generiert, eigene `.ocio` Configs ladbar
- **Pro-Funktion KI-Toggles:** Jede KI-Funktion einzeln an/abschaltbar — KI-Denoising, KI-Schärfung, KI-Upscaling, KI-Gesichtswiederherstellung, KI-Farbstil und KI-Szenenklassifizierung können deaktiviert werden (klassische Filter als Fallback)
- **Dark/Light Design:** Mit Live-Switch, blaue Akzentfarbe (#0EA5E9)
- **Drag & Drop:** Mehrere Dateien gleichzeitig (Bilder UND Videos)
- **Lokalisierung:** 13 Sprachen (DE, EN, ES, FR, IT, NL, PL, PT, TR, RU, ZH, JA, KO) — JSON-basiert, Systemsprache wird automatisch erkannt, English-Fallback, Contributors können ohne Code-Änderung neue Sprachen hinzufügen

## Architektur

```
FlipsiColor.Core/          # Plattneutrale Business Logic (net10.0)
  AI/                       # ModelManager, ModelDownloader, InferenceEngine
  Color/                    # ColorManager, ColorCalibration, LensCorrector, StyleLUT, OCIO
  Image/                    # ImagePipeline, RawDecoder, ExifReader
  Video/                    # VideoPipeline, FrameProcessor, SceneDetector, ClipMerger
  Core/                     # Settings, Pipeline, PipelineParams, AutoUpdater
  Utils/                    # Logger, SecurityValidator

FlipsiColor.Avalonia/       # Avalonia UI (net10.0, cross-platform)
  ViewModels/               # MainViewModel (MVVM)
  Views/                    # MainWindow.axaml
  Styles/                   # DarkTheme.axaml, LightTheme.axaml, Colors.axaml
  Converters/               # MatToBitmapConverter, BoolToVisibilityConverter, LocConverter
```

## KI-Modelle

Die App lädt 7 ONNX-Modelle automatisch von GitHub Releases herunter:

| Modell | Verwendung |
|--------|-----------|
| NAFNet | Bild-Restauration / Rauschunterdrückung |
| RestormerLight | Bild-Enhancement / Schärfung |
| RealESRGAN | Hochskalierung (2x/3x/4x) |
| CodeFormer | Gesichtswiederherstellung |
| AiLUTTransform | KI-Farbtransformation |
| EfficientNet | Bildklassifikation / Szenenerkennung |
| RealHATGAN | HDR-Enhancement |

## Download

### Windows
```
FlipsiColor-X.Y.Z-setup.exe herunterladen und ausführen.
(Inno Setup Installer — installiert automatisch)
```

### Linux (Ubuntu/Debian)
```bash
# .deb-Paket installieren
sudo dpkg -i FlipsiColor-X.Y.Z-linux.deb
```

### Linux (universell)
```bash
# AppImage — keine Installation nötig
chmod +x FlipsiColor-X.Y.Z.AppImage
./FlipsiColor-X.Y.Z.AppImage
```

### Systemanforderungen
- **Windows:** 10 (19041+) oder neuer
- **Linux:** Ubuntu 20.04+ / Debian 11+
- **RAM:** 8 GB minimum
- **GPU:** Empfohlen (DirectML auf Windows, CPU auf Linux)
- **FFmpeg:** Linux: `apt install ffmpeg`
- **libraw:** Linux: `apt install libraw-dev`
- **Optional:** OpenColorIO Tools (`apt install opencolorio-tools` für LUT-Baking)

## Entwicklung

```bash
# Build
dotnet build FlipsiColor.sln

# Linux Publish (self-contained)
dotnet publish FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish/linux-x64

# Windows Publish (self-contained)
dotnet publish FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish/win-x64
```

## Community

💬 [Discord](https://discord.gg/zHPhQ7EaqH) – Fragen, Feedback, Hilfe, Bug-Reports

Wir suchen Contributors! Siehe [CONTRIBUTING.md](CONTRIBUTING.md) für Build-Anleitung und Code-Standards.

## Versionierung

Wir folgen Semantic Versioning (`vMAJOR.MINOR.PATCH`). Details siehe [VERSIONING.md](VERSIONING.md).

## Credits

- **Idee:** Fabian Kirchweger
- **Entwicklung:** J.A.R.V.I.S. (Hermes Agent)

### Verwendete KI-Modelle (Entwicklung)

| Modell | Rolle |
|--------|-------|
| GLM-5.2 | Hauptmodell |
| GLM-5.2 | Sub-Agenten |