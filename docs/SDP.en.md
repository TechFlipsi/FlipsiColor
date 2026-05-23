# FlipsiColor — Software Development Plan

> **Note:** This is the English translation of [SDP.md](SDP.md). The German version is authoritative.

> 🚧 **Translation in progress** — The full English translation of the 1600+ line SDP is being prepared.
> The German original at [SDP.md](SDP.md) is the authoritative document.

## Key Architecture Overview

### Module Structure

| Module | Source | Header | Purpose |
|--------|--------|--------|---------|
| Core | `src/core/` | `include/flipsicolor/core/` | App, pipeline, settings, project |
| AI | `src/ai/` | `include/flipsicolor/ai/` | ONNX model management, inference |
| Color | `src/color/` | `include/flipsicolor/color/` | Color mgmt (LCMS2), lens correction, style LUT, white balance |
| Image | `src/image/` | `include/flipsicolor/image/` | Image pipeline, RAW decode, EXIF |
| Video | `src/video/` | `include/flipsicolor/video/` | Video pipeline, frame processing, scene detection |
| UI | `src/ui/` | `include/flipsicolor/ui/` | QML windows, editors, settings, learning UI |
| Utils | `src/utils/` | — | Logger, GPU info, filesystem |

### AI Models

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

### CodeFormer Fidelity Weight → Intensity Mapping

| Intensity | Fidelity Weight | Effect |
|-----------|----------------|--------|
| Leicht | 0.7 | Light touch, preserves more original |
| Mittel | 0.5 | Balanced (default) |
| Stark | 0.3 | Maximum restoration, generative priors dominate |

### Image Pipeline (15 Steps)

1. RAW Decode (LibRaw → ProPhoto RGB)
2. Scene Classification (EfficientNet-Lite0)
3. White Balance (statistical Gray World / Shades of Gray)
4. Lens Correction (Lensfun)
5. Exposure Compensation
6. Highlight Recovery
7. Shadow Recovery
8. Tone Curve
9. AI Denoise / Deblur (Restormer-light via ONNX)
10. Color Grading (AiLUT-Transform style + manual adjustments)
11. Skin Tone Protection
12. Sharpening (Unsharp Mask)
13. Output Color Space Convert (Working → Output, LCMS2)
14. Gamma Encode for Display
15. Optional: AI Upscale (Real_HAT_GAN best / Real-ESRGAN fast)
16. Optional: Face Restore (CodeFormer, adjustable fidelity weight)

### Development Phases

| Phase | Goal | Duration |
|-------|------|----------|
| 1 | Core pipeline + UI skeleton | 4 weeks |
| 2 | AI integration (denoise, deblur, style) | 3 weeks |
| 3 | Learning system (3 modes, 2-round) | 3 weeks |
| 4 | Video processing | 4 weeks |
| 5 | Polish + optimization | 3 weeks |
| 6 | Release (installer, docs) | 3 weeks |

---

See the full German document: [SDP.md](SDP.md)