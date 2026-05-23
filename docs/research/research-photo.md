# FlipsiColor — Deep Research: Photo & Video Color Correction

## 1. Color Science Fundamentals

### Color Spaces
- **sRGB**: Standard web. Gamma ~2.2 (actually piecewise). Most monitors. Gamut covers ~35% of CIE 1931.
- **Adobe RGB (1998)**: Wider gamut (~50% CIE 1931). Important for print. Gamma 2.2.
- **Display P3**: Apple's wide-gamut. ~45% CIE 1931. D65 white point like sRGB.
- **DCI-P3**: Cinema standard. D63 white point (slightly warmer).
- **Rec.2020**: Ultra-wide gamut (~75% CIE 1931). 10/12-bit. Future standard for HDR.
- **ProPhoto RGB**: Widest common gamut (~90% CIE 1931). Linear gamma internally. **Important for working space** — prevents clipping.
- **ACEScg**: Academy Color Encoding System. Linear, AP1 primaries. Industry standard for VFX.

**Best practice**: Use ProPhoto RGB or ACEScg as internal working space. Convert to sRGB/Display P3 for display, Adobe RGB for print.

### ICC Profiles
- Monitor ICC: Auto-detected via OS (Windows: ICM2 API, macOS: ColorSync, Linux: colord)
- Camera ICC: Embedded in RAW by some cameras, or derived from color matrix
- Working ICC: ProPhoto RGB or ACEScg (application-managed)
- LCMS2 handles all conversions with proper rendering intents

### Rendering Intents
- **Perceptual**: Best for photos, compresses gamut to fit
- **Relative Colorimetric** (with BPC): Most used, maps white point
- **Absolute Colorimetric**: Proofing only, preserves exact colors
- **Saturation**: For business graphics, not photos

### Monitor Calibration
- Use OS ICC profile (auto-detected)
- If no profile: assume sRGB
- HDR: Transfer function PQ (ST 2084) or HLG instead of gamma

## 2. RAW Processing Pipeline

### Step-by-Step (Order Matters!)
1. **RAW Decode** — LibRaw extracts raw Bayer/CFA data + metadata
2. **Black Level Subtraction** — Remove sensor black level
3. **White Balance** — Apply WB multipliers (camera or custom)
4. **Demosaicing** — Convert CFA to RGB (AHD default in LibRaw)
5. **Color Space Conversion** — Camera RGB → Working Space (ProPhoto/ACEScg)
6. **Exposure Compensation** — Linear multiply (EV adjustment)
7. **Highlight Recovery** — Before tone mapping (blend/reconstruct clipped highlights)
8. **Tone Mapping / Curve** — Apply base tone curve or film curve
9. **Lens Correction** — Lensfun distortion/TCA/vignetting (on linear data!)
10. **Noise Reduction** — After demosaic, before sharpening
11. **Color Correction** — Saturation, hue shifts, color grading
12. **Sharpening** — Last step before output
13. **Color Space Convert** — Working → Output (sRGB, Adobe RGB, Display P3)
14. **Gamma Encode** — Apply output gamma for display

**Critical**: Steps 1-6 should work on linear data. Steps 7-12 on gamma-encoded data. Step 13 converts to output space.

### Demosaicing
- AHD (Adaptive Homogeneity-Directed): Default, best quality
- VNG (Variable Number of Gradients): Faster, lower quality
- PPG: Good speed/quality balance
- Bilinear: Fastest, lowest quality
- AMZE (Aliasing Minimized Zipped Edges): Some cameras support, LibRaw doesn't have native AMZE

### White Balance
- Camera WB: `imgdata.color.cam_mul[0-3]`
- Auto WB: `imgdata.params.use_auto_wb = 1`
- Custom WB: Set multipliers manually
- Temperature/Tint: Convert between Kelvin + tint and RGB multipliers

### Highlight Recovery
- Mode 0: Clip (fast, loses data)
- Mode 1: Unclip (blend with white)
- Mode 2-9: Various reconstruction algorithms
- Best: recover highlight detail before clipping

## 3. Professional Photo Editing Workflow

### Order of Operations (Critical!)
1. **Lens Correction** (distortion, CA, vignetting) — on linear data
2. **White Balance** — fundamental, affects everything after
3. **Exposure** — set overall brightness
4. **Tone/Contrast** — shadows, highlights, midtones
5. **Color Grading** — hue shifts, split toning, saturation
6. **Noise Reduction** — after exposure/color, before sharpening
7. **Sharpening** — last correction step
8. **Output Sharpening** — resize-specific (for print/web)

### Why This Order?
- WB affects color everywhere — must be first
- Exposure sets the dynamic range — must be before contrast
- Contrast can amplify noise — fix noise after setting contrast
- Sharpening amplifies everything including noise — must be last

### Professional Best Practices
- Always work in 16-bit or floating point (never 8-bit until final export)
- Soft proof before printing (LCMS2 gamut check)
- Use histogram to monitor clipping (shadows, highlights)
- Skin tone protection: HSL skin tone line at ~18-22° hue in vectorscope
- Never clip channels — keep data in working space gamut

## 4. Lens Correction

### Distortion Types
- **Barrel**: Straight lines bow outward (common in wide-angle)
- **Pincushion**: Straight lines bow inward (common in telephoto)
- **Mustache (Wavy)**: Barrel near center, pincushion at edges
- **Chromatic Aberration (CA)**:
  - Transverse (TCA): Red/blue shift radially from center — Lensfun corrects this
  - Longitudinal (LoCA): Different focal planes per color — NOT corrected by Lensfun
- **Vignetting**: Darkening toward edges

### Lensfun Correction Models
- **Distortion**: ptlens (a + b·r² + c·r⁴), poly3, poly5, acm
- **TCA**: linear (scale per channel), poly3
- **Vignetting**: pa (polynomial in focal, aperture, distance)

### Best Practices
- Apply distortion/TCA/vignetting on **linear data** (before gamma/gamma-encoded operations)
- Use EXIF data (make, model, focal length, aperture) to auto-select correction profile
- If no Lensfun profile: estimate barrel/pincushion from image analysis (straight-line detection)
- Process order: distortion → TCA → vignetting

## 5. Log Profiles

### Transfer Functions
All log profiles convert scene-referred linear light to a compressed range for creative grading.

| Log Profile | Camera | Black Level | Mid Gray | White Clip | Notes |
|---|---|---|---|---|---|
| D-Log | DJI (Phantom 4/Mavic) | 0.0 | ~0.38 | 1.0 | 8-bit only |
| D-Log M | DJI (Mini 3/Air 3S) | 0.0 | ~0.35 | 1.0 | 10-bit, wider dynamic range |
| C-Log | Canon | 0.0 | ~0.34 | 1.0 | Wide DR |
| C-Log 2 | Canon | ~-0.07 | ~0.26 | 1.0 | Even wider DR, cinema |
| C-Log 3 | Canon | ~-0.01 | ~0.34 | 1.0 | Balance of C-Log and C-Log 2 |
| S-Log2 | Sony | ~0.04 | ~0.32 | 1.09 | Very wide DR |
| S-Log3 | Sony | ~0.01 | ~0.34 | 1.0 | Improved shadows |
| V-Log | Panasonic | ~0.01 | ~0.43 | 1.0 | Similar to C-Log |
| F-Log | Fujifilm | 0.0 | ~0.46 | 1.0 | Slightly different curve |
| N-Log | Nikon | 0.0 | ~0.33 | 1.0 | Similar to C-Log |
| Apple Log | Apple (iPhone 15 Pro) | 0.0 | ~0.35 | 1.0 | 10-bit ProRes |
| REDLogFilm | RED | 0.0 | ~0.33 | 1.0 | Designed for grading |
| Blackmagic Film | BMD | 0.0 | ~0.35 | 1.0 | Similar to ARRI |
| ARRI Log C | ARRI | 0.0 | ~0.35 | 1.0 | Industry standard cinema |

### Log Detection Strategy
1. Check EXIF maker notes for log profile tag
2. Check file naming conventions (DJI: DJI_*.MP4 with D-Log M metadata)
3. Analyze histogram: log profiles have compressed shadows/highlights, flat appearance
4. Apply inverse transfer curve to convert to linear/scenes-linear

## 6. Noise Reduction

### Noise Types
- **Luminance noise**: Random brightness variation. More visible at high ISO.
- **Chroma noise**: Random color variation in shadows. More visible in underexposed areas.
- **Banding**: Horizontal/vertical stripes from sensor readout. Common in Canon.
- **Hot pixels**: Single bright pixels. Fixed position, temperature-dependent.
- **Pattern noise**: Repeating pattern from sensor electronics.

### Processing Techniques
- **Spatial domain**: Blur-based (Gaussian, bilateral, non-local means)
- **Frequency domain**: Transform domain (DCT, wavelet) — separate signal from noise
- **Deep learning**: CNN/SwinIR/Restormer — learn noise patterns from data

### Best Practices
- Apply NR on linear data after WB and before gamma encoding
- Luminance NR: preserve detail, reduce noise
- Chroma NR: can be stronger (human eye less sensitive to color noise)
- Edge-aware: don't smooth edges (detail preservation)
- ISO-dependent: adjust NR strength based on ISO value from EXIF

## 7. Sharpening Techniques

### Methods
- **Unsharp Mask (USM)**: Classic. Amount + Radius + Threshold. Pros: simple, controllable. Cons: halos at high amounts.
- **Deconvolution**: Richardson-Lucy, Van Cittert. Pros: recovers actual detail. Cons: amplifies noise, needs PSF estimate.
- **Edge-aware**: Bilateral filter sharpening. Preserves edges while sharpening.
- **AI-based**: Dedicated sharpening networks (Restormer, NAFNet fine-tuned for deblur)

### When to Sharpen
- After noise reduction (never before — sharpening amplifies noise)
- After lens correction
- Before output (display-specific)
- Capture sharpening vs creative sharpening vs output sharpening (3-stage approach)

### Parameters
- **Amount**: How much sharpening (0.5-3.0 for USM)
- **Radius**: Size of edges to enhance (0.5-2.0 pixels typical)
- **Threshold**: Minimum contrast change to sharpen (0-20 typical, prevents noise sharpening)