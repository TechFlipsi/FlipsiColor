<div align="center">

# 🎨 FlipsiColor

**AI-powered Image & Video Color Correction Desktop App**

*Professional color grading — local, zero setup, style-learning*

[![C++20](https://img.shields.io/badge/C%2B%2B-20-blue.svg)](https://en.cppreference.com/w/cpp/20)
[![Qt6](https://img.shields.io/badge/Qt-6-green.svg)](https://www.qt.io)
[![ONNX Runtime](https://img.shields.io/badge/ONNX-Runtime-purple.svg)](https://onnxruntime.ai)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

[🇦🇹 Deutsch](README.md)

</div>

---

## ✨ Features

- **🎯 Zero Setup** — Install, start, use. No extra software, no plugin installs, no manual model downloads.
- **🔒 Local First** — Everything runs locally. No cloud, no account, no subscription. Your photos stay on your machine.
- **🛡️ Non-Destructive** — Original files are NEVER modified. All edits stored as parameter stacks.
- **🤖 AI-Enhanced** — State-of-the-art models: NAFNet (denoise), Restormer (deblur), Real_HAT_GAN (upscale), CodeFormer (face), AiLUT-Transform (style).
- **📚 Style Learning** — The AI learns YOUR editing style over time (Ask / Smart-Learn / Turbo modes).
- **📷 Camera-Universal** — Lens correction for 500+ cameras/lenses via Lensfun. DJI, Canon, Sony, Nikon, and more.
- **🎬 Video Support** — Full color grading pipeline with frame-consistent processing.
- **🖥️ Cross-Platform** — Windows, macOS, Linux. One codebase.

## 🤖 AI Models

| Task | Model | Size | Quality | Speed |
|------|-------|------|---------|-------|
| White Balance | Gray World + Shades of Gray | 0 MB (code) | Good | <1 ms |
| Denoise | NAFNet (SIDD SOTA) | 17 MB | Excellent | ~20 ms |
| Deblur / Multi-Task | Restormer-light | 24 MB | Very Good | ~50 ms |
| Upscale (Best) | Real_HAT_GAN_SRx4 | 120 MB | Excellent | ~200 ms |
| Upscale (Fast) | Real-ESRGAN | 64 MB | Good | ~150 ms |
| Face Restoration | CodeFormer (adjustable fidelity) | 350 MB | Excellent | ~60 ms |
| Color Style | AiLUT-Transform (Image-Adaptive) | 8 MB | Very Good | ~1 ms |
| Scene Classification | EfficientNet-Lite0 | 4.6 MB | Good | ~5 ms |

**Core download: ~54 MB** · Optional (lazy-load): up to ~534 MB

## 🎮 Three Modes

| Mode | Interaction | Best For |
|------|-------------|----------|
| **Ask** 👍👎 | Swipe-style feedback on every image | Learning your style |
| **Smart-Learn** 🧠 | AI learns silently from your edits | Building style without effort |
| **Turbo** ⚡ | Zero interaction, folder in → export out | Bulk processing |

## 🎚️ Three Intensity Levels

| Level | Behavior |
|-------|----------|
| **Leicht** | Minimal corrections — white balance, lens correction only |
| **Mittel** | Professional touch — full pipeline, balanced (default) |
| **Stark** | AI takes over — style LUT, scene-adaptive, maximum enhancement |

## 🛠️ Tech Stack

| Component | Technology |
|-----------|-----------|
| Language | C++20 |
| UI Framework | Qt6 + QML |
| AI Runtime | ONNX Runtime (CUDA / DirectML / Metal) |
| Image Processing | OpenCV |
| RAW Decoding | LibRaw |
| Lens Correction | Lensfun |
| Video Codecs | FFmpeg (HW-accelerated) |
| Color Management | LCMS2 (ICC profiles, ProPhoto RGB working space) |
| Build System | CMake |

## 💻 Minimum Hardware

| Component | Images Only | Images + Video |
|-----------|-------------|----------------|
| GPU | Dedicated 4 GB VRAM | RTX 3060 / RX 6600 XT |
| CPU | 4-core x64 | 8+ core |
| RAM | 8 GB | 16+ GB |
| Storage | 2 GB | 10+ GB |

> ⚠️ Intel UHD/Iris: AI features disabled (warning on startup). Intel Arc: experimental.

## 📁 Project Structure

```
FlipsiColor/
├── src/                    # Source code
│   ├── core/               # Application core, pipeline
│   ├── ai/                 # ONNX model management, inference
│   ├── color/              # Color management (LCMS2, LUT)
│   ├── image/              # Image processing pipeline
│   ├── video/              # Video processing pipeline
│   ├── ui/                 # QML UI components
│   └── utils/              # Utilities, logging
├── include/flipsicolor/    # Public headers
├── resources/              # QML, icons, model manifests
├── docs/                   # Concept, SDP, research
├── tests/                  # Unit + integration tests
├── cmake/                  # CMake modules
└── .github/workflows/      # CI/CD
```

## 📖 Documentation

- [Concept Document v1.2](docs/concept/flipsicolor-concept.md) — Full feature specification
- [Software Development Plan](docs/SDP.md) — Architecture, modules, phases
- [Research](docs/research/) — AI model analysis, photo/video pipeline research

## 🚀 Roadmap

| Phase | Goal | Duration |
|-------|------|----------|
| 1 | Core pipeline + UI skeleton | 4 weeks |
| 2 | AI integration (denoise, deblur, style) | 3 weeks |
| 3 | Learning system (3 modes, 2-round) | 3 weeks |
| 4 | Video processing | 4 weeks |
| 5 | Polish + optimization | 3 weeks |
| 6 | Release (installer, docs) | 3 weeks |

## 📄 License

This project is licensed under the **GNU General Public License v3.0** — see [LICENSE](LICENSE) for details.

Qt6 is used under **LGPLv3** (dynamic linking). ONNX Runtime is MIT licensed.

---

<div align="center">

*Part of the Flipsi family: [FlipsiInk](https://github.com/TechFlipsi/FlipsiInk) · [FlipsiSort](https://github.com/TechFlipsi/FlipsiSort) · **FlipsiColor***

</div>