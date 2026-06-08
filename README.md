# FlipsiColor

**KI-gestützte Bild- & Videofarbkorrektur für Windows**

## Features

- 🎨 **KI-Farbkorrektur** — 7 ONNX-Modelle (NAFNet, RestormerLight, RealHATGAN, RealESRGAN, CodeFormer, AiLUTTransform, EfficientNet)
- 📷 **RAW-Unterstützung** — CR2, CR3, NEF, ARW, DNG, ORF, RW2 (LibRaw)
- 🎬 **Video-Pipeline** — Frame-Video-Verarbeitung mit FFMPEG
- 🔍 **Objektivkorrektur** — Lensfun-basierte Verzeichnungskorrektur
- 🎭 **Dark/Light Theme** — System-Erkennung + manueller Wechsel
- 🔄 **Auto-Updater** — GitHub Releases API, Downgrade-Schutz
- 🖥 **GPU-Beschleunigung** — DirectML (CUDA Fallback)
- 📊 **EXIF-Leser** — MetadataExtractor (reines .NET)

## Systemanforderungen

- Windows 10 (19041+) oder neuer
- 8 GB RAM
- GPU empfohlen (DirectML)

## Build

```bash
dotnet publish FlipsiColor/FlipsiColor.csproj -c Release -r win-x64 --self-contained true -o FlipsiColor/publish
```

## Tech Stack

- .NET 9 / C# / WPF
- OpenCvSharp4 (Image Processing)
- ONNX Runtime + DirectML (KI-Inferenz)
- Serilog (Logging)
- CommunityToolkit.Mvvm (MVVM)
- LibRaw.Native (RAW-Decoder)
- MetadataExtractor (EXIF)
- FFMPEG (Video)

## Credits

- **Idee:** Fabian Kirchweger
- **Entwicklung:** J.A.R.V.I.S. (Hermes Agent)

### Verwendete KI-Modelle

| Modell              | Rolle        |
|---------------------|--------------|
| **GLM-5.1**         | Hauptmodell  |
| **DeepSeek V4 Pro** | Sub-Agenten  |

## Lizenz

GPL-3.0-or-later © 2026 Fabian Kirchweger (TechFlipsi)