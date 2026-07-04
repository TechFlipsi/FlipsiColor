# FlipsiColor

KI-gestützte Bild- & Videofarbkorrektur. Cross-Platform (Linux + Windows) mit Avalonia UI und .NET 10.

## Features

- **Bild-Pipeline:** KI-Farbkorrektur mit NAFNet/Restormer, Farbkalibrierung, Verzerrungs-Raster
- **Video-Pipeline:** Frame-weise Farbkorrektur mit Szenenwechsel-Erkennung und Audio-Erhaltung
- **Clips zusammenfügen:** Automatische Video-Clip-Gruppierung und Zusammenführung (alle Kameras)
- **Hochskalieren:** RealESRGAN (2x/3x/4x)
- **Gesichtswiederherstellung:** CodeFormer
- **Farbstil-LUT:** .cube LUT-Dateien laden und anwenden
- **Dark/Light Design:** Mit Live-Switch, blaue Akzentfarbe (#0EA5E9)
- **Drag & Drop:** Mehrere Dateien gleichzeitig (Bilder UND Videos)
- **Lokalisierung:** Deutsch / Englisch umschaltbar

## Architektur

```
FlipsiColor.Core/          # Plattneutrale Business Logic (net10.0)
  AI/                       # ModelManager, ModelDownloader, InferenceEngine
  Color/                    # ColorManager, ColorCalibration, LensCorrector, StyleLUT
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
| NAFNet | Bild-Restauration |
| RestormerLight | Bild-Enhancement |
| RealESRGAN | Hochskalierung (2x/3x/4x) |
| CodeFormer | Gesichtswiederherstellung |
| AiLUTTransform | KI-Farbtransformation |
| EfficientNet | Bildklassifikation |
| RealHATGAN | HDR-Enhancement |

## Download

### Linux
```bash
# Installer von GitHub Releases herunterladen
# FlipsiColor-vX.Y.Z-Linux-x64-Installer.run
chmod +x FlipsiColor-*.run
./FlipsiColor-*.run
```

### Windows
```
FlipsiColor-vX.Y.Z-Windows-x64-Installer.exe ausführen.
```

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

## Credits

- **Idee:** Fabian Kirchweger
- **Entwicklung:** J.A.R.V.I.S. (Hermes Agent)

### Verwendete KI-Modelle

| Modell | Rolle |
|--------|-------|
| GLM-5.2 | Hauptmodell |
| GLM-5.2 | Sub-Agenten |