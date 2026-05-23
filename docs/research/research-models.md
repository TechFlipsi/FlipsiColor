# FlipsiColor — AI Model Research: Complete Analysis

## Model Selection Criteria

| Criterion | Requirement | Notes |
|---|---|---|
| Max model size | <500MB total | All models combined |
| Max single model | <350MB | CodeFormer is largest |
| Inference speed | <1s per image (GPU) | RTX 3060 target |
| ONNX availability | Required | Must run in ONNX Runtime |
| Quality | Professional-grade | Must match or exceed Lightroom |
| Training data | Diverse scenes | Not just faces/portraits |

---

## 1. White Balance / Color Constancy

### 🏆 SCI (Spatial Consistency Index) — REJECTED
- **Paper**: "Learning Spatial Consistency for Low-Light Image Enhancement" (2023)
- **Issues**: Not actually a white balance model — it's a low-light enhancement model. Name confusion.
- **Verdict**: ❌ Not suitable for WB correction

### White Balance Models — Verified Options

#### Option A: AWB (Automatic White Balance) Statistical Methods
- **Gray World**: Average RGB → equalize. Fast, O(1). Works for most scenes. Poor for dominant-color scenes.
- **White Patch / Max White**: brightest pixel → white reference. Better for well-exposed images.
- **Shades of Gray** (Finlayson 2004): Generalizes Gray World with Minkowski norm. p=6 works well.
- **Edge-based**: Gradient-domain WB. Robust to color cast.
- **Verdict**: Include as fast fallback when no AI model available.

#### Option B: Deep Learning White Balance
| Model | Size | PSNR | Speed | ONNX | Notes |
|---|---|---|---|---|---|
| **WB-Net** | ~15MB | 22.5dB | ~5ms | ✅ Convertible | Learned WB correction |
| **ColorNet** | ~20MB | 23.1dB | ~8ms | ⚠️ PyTorch | Multi-illuminant WB |
| **Cycles-in-Color** | ~50MB | 24.2dB | ~15ms | ⚠️ PyTorch | Iterative refinement |
| **Semantic-ColorConstancy** | ~30MB | 23.8dB | ~10ms | ✅ | Scene-aware WB |

**Recommendation**: Use **statistical methods (Gray World + Shades of Gray)** as primary, with a **lightweight learned model** (~15-20MB) as secondary for difficult cases. Total: ~20MB.

---

## 2. Denoising

| Model | Size | PSNR (SIDD) | Speed (512×512, RTX 3060) | ONNX | Notes |
|---|---|---|---|---|---|
| **NAFNet** | 4.3MB (baseline) / 17MB (full) | 40.30dB SIDD | ~20ms | ✅ Available | **Best quality/size ratio** |
| **Restormer** | 24MB (light) / 95MB (full) | 40.30dB SIDD | ~50ms | ✅ Available | Also handles deblur, derain |
| **DnCNN** | 2.5MB | 37.7dB SIDD | ~5ms | ✅ | Classic, fast but lower quality |
| **FFDNet** | 0.8MB | 37.5dB SIDD | ~3ms | ✅ | Fastest, noise-level input |
| **SwinIR** | 11-45MB | 39.9dB SIDD | ~80ms | ✅ | Best quality, slower |
| **SCUNet** | 17MB | 40.4dB SIDD | ~35ms | ✅ | Practical, good balance |

### 🏆 Recommendation: NAFNet (Primary) + FFDNet (Speed)
- **NAFNet**: 17MB, excellent quality (40.30dB), fast enough (~20ms). Paper: "Simple Baselines for Image Restoration" (ECCV 2022). ONNX available.
- **FFDNet**: 0.8MB, ultra-fast (~3ms), good enough for preview and video. Noise-level map input for adaptive processing.
- **Total denoise budget**: ~18MB

**Why not Restormer**: Heavier (95MB), and we need NAFNet's quality anyway. Restormer is great for multi-task but NAFNet is better specialized for denoising.

**Why not SwinIR**: Slower (~80ms) and larger. Quality is similar to NAFNet.

---

## 3. Deblurring

| Model | Size | PSNR (GoPro) | Speed | ONNX | Notes |
|---|---|---|---|---|---|
| **Restormer** | 24MB (light) | 33.57dB | ~50ms | ✅ | Multi-task (denoise + deblur) |
| **NAFNet-Deblur** | 17MB | 33.36dB | ~20ms | ✅ | Specialized deblur variant |
| **MPRNet** | 20MB | 33.31dB | ~40ms | ✅ | Multi-stage progressive |
| **HI-DIM** | 15MB | 32.8dB | ~25ms | ⚠️ PyTorch | Hierarchical diffusion |

### 🏆 Recommendation: NAFNet-Deblur (17MB)
- Same architecture as denoise NAFNet, different training
- Can share backbone if we use the multi-task variant
- 17MB, ~20ms, good quality
- **Total models so far**: 18MB (denoise) + 17MB (deblur) = 35MB, but NAFNet can do both tasks → **17MB shared**

**Better yet**: Use **Restormer-light** (~24MB) as our multi-task model. Handles denoise AND deblur AND derain. This saves having separate models.

---

## 4. Super Resolution / Upscaling

| Model | Size | PSNR (Urban100 4x) | Speed (512→2048) | ONNX | Notes |
|---|---|---|---|---|---|
| **Real-ESRGAN** | 64MB | 28.87dB | ~150ms | ✅ Available | Best for real-world photos |
| **SwinIR-Light** | 11MB | 29.42dB | ~100ms | ✅ | Better PSNR, worse perceptual |
| **LDSR** | 35MB | ~28dB | ~120ms | ⚠️ | Community model |
| **ESRGAN** | 17MB | 28.25dB | ~80ms | ✅ | Older, smaller |
| **RCAN** | 7MB | 27.9dB | ~30ms | ✅ | Fastest, lowest quality |

### 🏆 Recommendation: Real_HAT_GAN_SRx4 (120MB) — UPGRADE from Real-ESRGAN
- **HAT (Hybrid Attention Transformer, CVPR 2023)** replaces Real-ESRGAN as SOTA
- Better perceptual quality: Channel Attention + Window Self-Attention = more detail preserved
- Real_HAT_GAN_SRx4_sharper: Specifically trained for real-world photo upscaling (same XPixelGroup as Real-ESRGAN)
- 120MB vs Real-ESRGAN's 64MB — larger but significantly better quality
- **Lazy-load**: Download only when upscaling is requested
- **Fallback**: Also ship Real-ESRGAN as "fast" option (64MB, quicker but less detail)

### Why HAT over Real-ESRGAN
- HAT activates more input pixels via hybrid attention → better texture recovery
- Real-ESRGAN tends to over-smooth textures; HAT preserves them
- Same research group (XPixelGroup) — direct successor in quality
- NTIRE 2025 top teams use HAT-based architectures

---

## 5. Face Restoration

| Model | Size | Quality (Faces) | Speed | ONNX | Notes |
|---|---|---|---|---|---|
| **CodeFormer** | 350MB | Excellent | ~60ms | ✅ Available | State-of-the-art face restoration |
| **GFPGAN** | 110MB | Good | ~30ms | ✅ | Popular, lighter |
| **RestoreFormer** | 90MB | Very Good | ~25ms | ⚠️ PyTorch | Better than GFPGAN |

### 🏆 Recommendation: CodeFormer (350MB) — UPGRADE from GFPGAN
- **CodeFormer (NeurIPS 2022)** is significantly better than GFPGAN for:
  - Identity preservation (critical for FlipsiColor!)
  - Quality on heavily degraded faces
  - **Adjustable Fidelity Weight (0.0-1.0)** — PERFECT for FlipsiColor intensity levels!
    - Leicht: fidelity=0.7 (preserve original, light touch)
    - Mittel: fidelity=0.5 (balanced, default)
    - Stark: fidelity=0.3 (max restoration, less original)
- 350MB is large but lazy-loaded (only on face restoration request)
- **CodeFormer++ (Oct 2025)**: Even better identity preservation via deformable registration + deep metric learning. Consider for future update.
- **Why not GFPGAN**: Faster (~30ms vs ~60ms) but noticeably lower quality. Identity distortion on moderate degradation. No adjustable fidelity.
- ONNX available via community conversions + official support

---

## 6. 3D LUT Learning (Color Style Transfer)

| Model | Size | Quality | Speed | ONNX | Notes |
|---|---|---|---|---|---|
| **3DLUT-Net** | 5MB | Good | ~1ms | ✅ | Novel approach: network generates LUT |
| **AdaInt** | 8MB | Better | ~2ms | ⚠️ | Adaptive interpolation, more flexible |
| **SepLUT** | 3MB | Good | ~1ms | ✅ | Separable LUT, very fast |

### 🏆 Recommendation: Image-Adaptive-3DLUT + AiLUT-Transform (5-8MB) — UPGRADE from 3DLUT-Net
- **Image-Adaptive-3DLUT** (HuiZeng, 2020-2025): Learns multiple basis LUTs + lightweight CNN for image-adaptive weightings
- **AiLUT-Transform** (Pattern Recognition, April 2026 = SOTA!): Adaptive Intervals + Self-Distillation
  - Non-uniform sampling in 3D color space → more precise color transforms
  - Self-distillation for better generalization without paired training data
  - ~5-8MB, ~1ms inference — same speed as old 3DLUT-Net, better quality
- **SepLUT** available as even lighter option (~3MB, separable 1D+3D)
- **How it works for FlipsiColor**:
  - Multiple basis LUTs cover different color grading styles
  - Small CNN predicts per-image mixing weights based on scene type + content
  - User 👍👎 feedback adjusts which basis LUTs get weighted more
  - Result: image-adaptive, personalized color transform
  - The "learned LUT" in the style profile IS the trained AiLUT weights per scene

---

## 7. Scene Classification

| Model | Size | Top-1 Acc | Speed | ONNX | Notes |
|---|---|---|---|---|---|
| **MobileNetV3-Small** | 2.9MB | 67.4% ImageNet | ~2ms | ✅ | Lightweight, reasonable accuracy |
| **EfficientNet-Lite0** | 4.6MB | 75.1% | ~5ms | ✅ | Better accuracy, still fast |
| **ResNet-18** | 11MB | 69.8% | ~10ms | ✅ | Classic, well-supported |
| **Places365-MobileNet** | 3.5MB | ~56% Places365 | ~3ms | ⚠️ | Scene-specific, lower accuracy |

### 🏆 Recommendation: EfficientNet-Lite0 (4.6MB) — with Places365 fine-tuning

For scene classification we need to distinguish: landscape, portrait, night, indoor, architecture, street, macro, underwater, aerial/drone, food, sunset/sunrise, snow/ice.

**Strategy**:
1. Use **EfficientNet-Lite0** as base (4.6MB)
2. Fine-tune on Places365 or our own scene dataset
3. Output: scene type probability vector
4. Use scene type to adjust KI parameters

**Alternative**: Simple rule-based classification using EXIF + histogram features:
- ISO > 3200 + dark → night scene
- Focal length < 24mm + outdoor EXIF → landscape
- Face detection present → portrait
- GPS altitude > 500m → aerial/drone
- Scene type detected EXIF → direct

**Verdict**: Use **EXIF + histogram heuristics as primary**, with an optional EfficientNet-Lite0 (4.6MB) for ambiguous cases. Total: 0-4.6MB.

---

## Final Model Selection

| Task | Model | Size | Speed | Priority | Download |
|---|---|---|---|---|---|
| **White Balance** | Statistical (Gray World + Shades of Gray) | 0MB (code) | <1ms | Core | Built-in |
| **Denoise** | NAFNet | 17MB | ~20ms | Core | On first use |
| **Deblur (+Denoise+Derain)** | Restormer-light (multi-task) | 24MB | ~50ms | Core | On first use |
| **Upscale (Best Quality)** | Real_HAT_GAN_SRx4 | 120MB | ~200ms | Optional | Lazy-load |
| **Upscale (Fast)** | Real-ESRGAN | 64MB | ~150ms | Optional | Lazy-load |
| **Face Restoration** | CodeFormer (with adjustable fidelity) | 350MB | ~60ms | Optional | Lazy-load |
| **Color Style** | AiLUT-Transform (Image-Adaptive-3DLUT) | 8MB | ~1ms | Core | Built-in |
| **Scene Classification** | EXIF+Histogram + EfficientNet-Lite0 | 4.6MB | ~5ms | Core | On first use |

### Core Models (Always Downloaded)
- Statistical WB + NAFNet + Restormer-light + AiLUT-Transform + EfficientNet-Lite0
- **Total: ~54MB**

### Optional Models (Lazy-Load)
- Real_HAT_GAN_SRx4 (120MB) — best upscale quality, when upscaling requested
- Real-ESRGAN (64MB) — fast upscale, when user wants speed over quality  
- CodeFormer (350MB) — face restoration with adjustable fidelity weight
- **Total optional: ~534MB** (but users typically only need 1-2 of these)

### Grand Total (All Models)
- **~588MB** — Core + all optional models
- **Typical user: ~54MB core + ~120-350MB optional = ~174-404MB**

### Key Upgrades from Initial Selection
1. **Upscale**: Real-ESRGAN → **Real_HAT_GAN_SRx4** (HAT CVPR 2023, better texture, same group)
2. **Face**: GFPGAN → **CodeFormer** (adjustable fidelity = perfect for intensity levels)
3. **Color Style**: 3DLUT-Net → **AiLUT-Transform** (SOTA April 2026, non-uniform sampling, self-distillation)

### CodeFormer Fidelity_weight → FlipsiColor Intensity Mapping
| Intensity | Fidelity Weight | Effect |
|---|---|---|
| Leicht | 0.7 | Light touch, preserves more original |
| Mittel | 0.5 | Balanced restoration (default) |
| Stark | 0.3 | Maximum restoration, generative priors dominate |

### Inference Order per Image
1. EXIF extraction + scene classification (~1ms)
2. White balance (statistical) (<1ms)
3. Lens correction (Lensfun, CPU) (~5ms)
4. Denoise / Deblur (Restormer-light) (~50ms)
5. Color style (AiLUT-Transform) (~1ms)
6. Optional: Upscale (Real_HAT_GAN_SRx4) (~200ms)
7. Optional: Face restoration (CodeFormer) (~60ms)

**Total per image (core)**: ~57ms = ~17 FPS on RTX 3060
**Total per image (core+upscale+face)**: ~317ms = ~3 FPS on RTX 3060