<div align="center">

# 🎨 FlipsiColor

**KI-gestützte Bild- & Videofarbkorrektur Desktop-App**

*Professionelles Color Grading — lokal, kein Setup, stil-lernend*

[![C++20](https://img.shields.io/badge/C%2B%2B-20-blue.svg)](https://en.cppreference.com/w/cpp/20)
[![Qt6](https://img.shields.io/badge/Qt-6-green.svg)](https://www.qt.io)
[![ONNX Runtime](https://img.shields.io/badge/ONNX-Runtime-purple.svg)](https://onnxruntime.ai)
[![Lizenz: GPL v3](https://img.shields.io/badge/Lizenz-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

[🇬🇧 English](README.en.md)

</div>

---

## ✨ Features

- **🎯 Kein Setup** — Installieren, starten, nutzen. Keine Zusatz-Software, keine Plugin-Installationen, keine manuellen Modell-Downloads.
- **🔒 Lokal zuerst** — Alles läuft lokal. Keine Cloud, kein Account, kein Abo. Deine Fotos bleiben auf deinem Rechner.
- **🛡️ Nicht-destruktiv** — Originaldateien werden NIE verändert. Alle Bearbeitungen als Parameter-Stacks gespeichert.
- **🤖 KI-Verbessert** — State-of-the-Art Modelle: NAFNet (Entrauschen), Restormer (Entschärfen), Real_HAT_GAN (Hochskalieren), CodeFormer (Gesichter), AiLUT-Transform (Stil).
- **📚 Stil-Lernen** — Die KI lernt DEINEN Bearbeitungsstil mit der Zeit (Ask / Smart-Learn / Turbo Modus).
- **📷 Kamera-Universell** — Objektivkorrektur für 500+ Kameras/Objektive via Lensfun. DJI, Canon, Sony, Nikon und mehr.
- **🎬 Video-Unterstützung** — Komplette Color-Grading-Pipeline mit Frame-konsistenter Verarbeitung.
- **🖥️ Plattformübergreifend** — Windows, macOS, Linux. Eine Codebasis.

## 🤖 KI-Modelle

| Aufgabe | Modell | Größe | Qualität | Geschwindigkeit |
|---------|--------|-------|----------|-----------------|
| Weißabgleich | Gray World + Shades of Gray | 0 MB (Code) | Gut | <1 ms |
| Entrauschen | NAFNet (SIDD SOTA) | 17 MB | Hervorragend | ~20 ms |
| Entschärfen / Multi-Task | Restormer-light | 24 MB | Sehr gut | ~50 ms |
| Hochskalieren (Beste) | Real_HAT_GAN_SRx4 | 120 MB | Hervorragend | ~200 ms |
| Hochskalieren (Schnell) | Real-ESRGAN | 64 MB | Gut | ~150 ms |
| Gesichtswiederherstellung | CodeFormer (einstellbare Fidelity) | 350 MB | Hervorragend | ~60 ms |
| Farbstil | AiLUT-Transform (Bild-Adaptiv) | 8 MB | Sehr gut | ~1 ms |
| Szenen-Klassifizierung | EfficientNet-Lite0 | 4,6 MB | Gut | ~5 ms |

**Core-Download: ~54 MB** · Optional (Lazy-Load): bis zu ~534 MB

## 🎮 Drei Modi

| Modus | Interaktion | Am besten für |
|-------|-------------|---------------|
| **Ask** 👍👎 | Swipe-Feedback bei jedem Bild | Stil lernen |
| **Smart-Learn** 🧠 | KI lernt stillschweigend aus deinen Bearbeitungen | Stil aufbauen ohne Aufwand |
| **Turbo** ⚡ | Keine Interaktion, Ordner rein → Export raus | Massenverarbeitung |

## 🎚️ Drei Intensitätsstufen

| Stufe | Verhalten |
|-------|-----------|
| **Leicht** | Minimale Korrekturen — Weißabgleich, Objektivkorrektur nur |
| **Mittel** | Professioneller Touch — volle Pipeline, ausgewogen (Standard) |
| **Stark** | KI übernimmt — Stil-LUT, szenen-adaptiv, maximale Verbesserung |

### CodeFormer Fidelity-Weight → FlipsiColor Intensität

| Intensität | Fidelity Weight | Wirkung |
|------------|----------------|---------|
| Leicht | 0,7 | Leichte Berührung, mehr Original erhalten |
| Mittel | 0,5 | Ausgewogen (Standard) |
| Stark | 0,3 | Maximale Wiederherstellung, generative Priors dominieren |

## 🛠️ Tech-Stack

| Komponente | Technologie |
|------------|-------------|
| Sprache | C++20 |
| UI-Framework | Qt6 + QML |
| KI-Runtime | ONNX Runtime (CUDA / DirectML / Metal) |
| Bildverarbeitung | OpenCV |
| RAW-Dekodierung | LibRaw |
| Objektivkorrektur | Lensfun |
| Video-Codecs | FFmpeg (HW-beschleunigt) |
| Farbmanagement | LCMS2 (ICC-Profile, ProPhoto RGB Arbeitsfarbraum) |
| Build-System | CMake |

## 💻 Mindest-Hardware

| Komponente | Nur Bilder | Bilder + Video |
|------------|------------|----------------|
| GPU | Dedicated 4 GB VRAM | RTX 3060 / RX 6600 XT |
| CPU | 4-Kern x64 | 8+ Kerne |
| RAM | 8 GB | 16+ GB |
| Speicher | 2 GB | 10+ GB |

> ⚠️ Intel UHD/Iris: KI-Funktionen deaktiviert (Warnung beim Start). Intel Arc: experimentell.

## 📁 Projektstruktur

```
FlipsiColor/
├── src/                    # Quellcode
│   ├── core/               # App-Kern, Pipeline
│   ├── ai/                  # ONNX-Modellverwaltung, Inferenz
│   ├── color/               # Farbmanagement (LCMS2, LUT)
│   ├── image/               # Bildverarbeitungs-Pipeline
│   ├── video/               # Videverarbeitungs-Pipeline
│   ├── ui/                  # QML-UI-Komponenten
│   └── utils/               # Hilfsprogramme, Protokollierung
├── include/flipsicolor/    # Öffentliche Header
├── resources/              # QML, Icons, Modell-Manifeste
├── docs/                   # Konzept, SDP, Research
├── tests/                  # Unit- + Integrationstests
├── cmake/                  # CMake-Module
└── .github/workflows/      # CI/CD
```

## 📖 Dokumentation

- [Konzept-Dokument v1.2](docs/concept/flipsicolor-concept.md) — Vollständige Feature-Spezifikation
- [Software-Entwicklungsplan](docs/SDP.md) — Architektur, Module, Phasen
- [Research](docs/research/) — KI-Modell-Analyse, Foto-/Video-Pipeline-Research

## 🚀 Roadmap

| Phase | Ziel | Dauer |
|-------|------|-------|
| 1 | Core-Pipeline + UI-Skelett | 4 Wochen |
| 2 | KI-Integration (Entrauschen, Entschärfen, Stil) | 3 Wochen |
| 3 | Lern-System (3 Modi, 2 Runden) | 3 Wochen |
| 4 | Video-Verarbeitung | 4 Wochen |
| 5 | Feinschliff + Optimierung | 3 Wochen |
| 6 | Release (Installer, Doku) | 3 Wochen |

## 📄 Lizenz

Dieses Projekt steht unter der **GNU General Public License v3.0** — siehe [LICENSE](LICENSE) für Details.

Qt6 wird unter **LGPLv3** (dynamische Verknüpfung) verwendet. ONNX Runtime ist MIT-lizenziert.

---

<div align="center">

*Teil der Flipsi-Familie: [FlipsiInk](https://github.com/TechFlipsi/FlipsiInk) · [FlipsiSort](https://github.com/TechFlipsi/FlipsiSort) · **FlipsiColor***

</div>