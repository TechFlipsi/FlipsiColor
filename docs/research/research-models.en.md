# FlipsiColor — Research: AI Model Selection & Benchmarks

> **Note:** German original at [research-models.md](research-models.md). This is the English reference summary.
> 🚧 Full translation in progress.

## Final Model Selection (Upgraded)

| Task | Model | Size | Speed | Priority | Download |
|------|-------|------|-------|----------|----------|
| **White Balance** | Statistical (Gray World + Shades of Gray) | 0MB (code) | <1ms | Core | Built-in |
| **Denoise** | NAFNet | 17MB | ~20ms | Core | On first use |
| **Deblur (+Denoise+Derain)** | Restormer-light (multi-task) | 24MB | ~50ms | Core | On first use |
| **Upscale (Best Quality)** | Real_HAT_GAN_SRx4 | 120MB | ~200ms | Optional | Lazy-load |
| **Upscale (Fast)** | Real-ESRGAN | 64MB | ~150ms | Optional | Lazy-load |
| **Face Restoration** | CodeFormer (with adjustable fidelity) | 350MB | ~60ms | Optional | Lazy-load |
| **Color Style** | AiLUT-Transform (Image-Adaptive-3DLUT) | 8MB | ~1ms | Core | Built-in |
| **Scene Classification** | EXIF+Histogram + EfficientNet-Lite0 | 4.6MB | ~5ms | Core | On first use |

### Key Upgrades from Initial Selection
1. **Upscale**: Real-ESRGAN → **Real_HAT_GAN_SRx4** (HAT CVPR 2023, better texture, same XPixelGroup)
2. **Face**: GFPGAN → **CodeFormer** (adjustable fidelity weight = perfect for intensity levels)
3. **Color Style**: 3DLUT-Net → **AiLUT-Transform** (SOTA April 2026, non-uniform sampling, self-distillation)

### CodeFormer Fidelity_weight → FlipsiColor Intensity Mapping

| Intensity | Fidelity Weight | Effect |
|-----------|----------------|--------|
| Leicht | 0.7 | Light touch, preserves more original |
| Mittel | 0.5 | Balanced (default) |
| Stark | 0.3 | Maximum restoration, generative priors dominate |

See: [research-models.md](research-models.md)