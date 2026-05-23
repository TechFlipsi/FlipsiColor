# FlipsiColor — Deep Research: Video Color Grading & Consistency

## 1. Video Color Grading Workflow

### Professional Colorist Pipeline (3 stages)

**Primary Correction (Balance)**
1. White balance / color temperature
2. Exposure / lift / gamma / gain
3. Contrast curve
4. Saturation
5. Log → Rec.709 conversion (if shot in log)

**Secondary Correction (Isolation)**
1. Skin tone isolation and protection
2. Sky/exposure gradients
3. Color-specific adjustments (HSL qualifiers)
4. Power windows / masks
5. Vignettes

**Creative Grade (Look)**
1. LUT application (creative look)
2. Film emulation (Kodak, Fuji film stocks)
3. Teal-orange / complementary color grading
4. Desaturation / selective color
5. Film grain / texture

### FlipsiColor Application
Since FlipsiColor does NOT do editing/cutting, we apply the same pipeline per frame:
- Auto-detect log profile → convert to Rec.709 (Primary)
- Apply learned style profile (Creative Grade)
- Skin tone protection (Secondary)
- Ensure temporal consistency across frames

## 2. Frame-by-Frame Color Consistency

### Reference Frame Approach
1. **Select reference frame**: Best-exposed, representative frame from the video
2. **Compute reference statistics**: Histogram, mean/std per channel, color distribution
3. **Match each frame**: Adjust each frame to match reference

### Histogram Matching
- Compute histogram of reference frame (per channel)
- Build cumulative distribution function (CDF)
- For each target frame, compute CDF
- Map target CDF → reference CDF
- Fast: O(N) per channel, where N = pixels per frame
- Problem: Can shift colors unnaturally on scene transitions

### Temporal Smoothing
- After per-frame correction, apply temporal filter
- Exponential moving average: `corrected[t] = α * raw_corrected[t] + (1-α) * corrected[t-1]`
- α = smoothing factor (0.3-0.7 typical)
- Prevents flickering while allowing gradual changes
- Key: Don't smooth across scene boundaries

### Scene Detection Algorithms
1. **Histogram Difference**: Compare adjacent frame histograms. Threshold δ = |hist[t] - hist[t-1]|. If δ > T: scene change.
2. **Edge-based**: Compute edge map (Canny), compare edges between frames. Scene changes have different edges.
3. **Optical Flow**: Compute flow between frames. Large flow magnitude = scene change or fast motion.
4. **Content-based**: Use CNN features from a lightweight model (e.g., MobileNet feature at frame level). Compare cosine similarity.

**Recommended for FlipsiColor**: Start with histogram difference (fast, GPU-friendly), add edge-based as fallback.

### Temporal Smoothing Parameters
- **Smoothing window**: 5-15 frames typical
- **Strength**: 0.3-0.7 α value
- **Scene boundary reset**: Reset smoothing at detected scene changes
- **Skin tone lock**: Preserve skin hue angle within ±5° during smoothing

## 3. Video Codecs and Color

### Chroma Subsampling
| Subsampling | Description | Bits per pixel | Color Quality |
|---|---|---|---|
| 4:4:4 | Full color, no subsampling | 24 (8-bit) | Best — no color artifacts |
| 4:2:2 | Half horizontal chroma | 16 (8-bit) | Good — professional standard |
| 4:2:0 | Quarter chroma (half each axis) | 12 (8-bit) | Acceptable — most consumer video |
| 4:1:1 | Quarter horizontal chroma | 12 (8-bit) | Poor — DV format, avoid |

**FlipsiColor impact**: 4:2:0 (common in DJI, phone video) means we have half the color resolution. Upsampling chroma before processing is important for quality.

### Bit Depth
| Bit Depth | Colors | Banding | Use Case |
|---|---|---|---|
| 8-bit | 16.7M | Visible banding in gradients | Consumer video, social media |
| 10-bit | 1.07B | Minimal banding | Professional, DJI Air 3S |
| 12-bit | 68.7B | No visible banding | Cinema (RED, ARRI) |
| 16-bit linear | ~281T | No banding | Internal processing |

**FlipsiColor**: Always process in 16-bit float internally. Convert to output bit depth on export.

### Codec Impact on Color
- **H.264/AVC**: 8-bit most common, 10-bit (Hi10P) rare but supported. Lossy — some color detail lost.
- **H.265/HEVC**: 10-bit common. Better compression at same quality. DJI Air 3S uses H.265 10-bit.
- **AV1**: 10/12-bit. Open, royalty-free. Best compression quality.
- **ProRes**: 422 / 422 HQ / 4444. Nearly lossless. Very large files. Editor's codec.
- **DNxHR**: Avid equivalent of ProRes. Good quality.

**FlipsiColor codec support priority**: HEVC (10-bit) → H.264 → AV1 → ProRes → DNxHR

## 4. Skin Tone Protection

### Skin Color Ranges
In various color spaces, skin occupies specific regions:

**HSV**:
- Hue: 0-50° (fair to dark skin)
- Saturation: 20-70%
- Value: 40-95%

**YCbCr (Rec.709)**:
- Cb: 77-127
- Cr: 133-173

**CIE Lab**:
- a*: 5-25 (warm tint)
- b*: 10-30 (yellow-blue axis)

### Detection Method
1. Convert frame to HSV or YCbCr
2. Threshold for skin region (hue range + saturation range)
3. Create binary mask (dilated for soft edges)
4. Use mask to blend: protected areas get lighter color correction

### Protection Algorithm
1. Before applying full color grade, compute skin mask
2. Apply grade to full frame
3. Blend: `result = skin_mask * original_skin + (1 - skin_mask) * graded_frame`
4. Strength: 60-80% protection typical (not 100% — allow some grading on skin)

## 5. 3D LUTs in Practice

### How 3D LUTs Work
A 3D LUT maps RGB input → RGB output:
- Input: (R, G, B) each in range [0, 1]
- Output: (R', G', B')
- LUT size: NxNxN grid (common: 17³, 33³, 65³)
- For values between grid points: interpolation (trilinear or tetrahedral)

### LUT Sizes
| Size | Entries | File Size | Quality | Speed |
|---|---|---|---|---|
| 17³ | 4,913 | ~15KB | Good for preview | Very fast |
| 33³ | 35,937 | ~100KB | Standard | Fast |
| 65³ | 274,625 | ~800KB | Professional | Moderate |

**Recommendation**: Use 33³ for export, 17³ for real-time preview.

### Generating LUTs from Image Pairs
1. Start with input image (before) and target image (after grading)
2. Both resized to small resolution (e.g., 256x256)
3. Input-output pairs form training data for 3DLUT-Net
4. Network learns the color transform as a compact 3D LUT
5. Result is a lightweight model that can be applied per-frame

### Interpolation Methods
- **Trilinear**: Fast, smooth. Slight quality loss.
- **Tetrahedral**: More accurate. Standard for professional use.
- **Pyramid**: Used by some GPU implementations.

## 6. Video Processing Pipeline

### Hardware-Accelerated Decoding

| API | Platform | GPU | Decoder |
|---|---|---|---|
| NVDEC | Win/Linux | NVIDIA | h264_cuvid, hevc_cuvid |
| DXVA2 | Win | Any DX11 | h264_dxva2, hevc_dxva2 |
| D3D11VA | Win | Any DX12 | h264_d3d11va, hevc_d3d11va |
| VAAPI | Linux | Intel/AMD | h264_vaapi, hevc_vaapi |
| VideoToolbox | macOS | Apple | h264_videotoolbox, hevc_videotoolbox |

### Frame Processing Pipeline

```
Video File
  ↓ FFmpeg HW-decode
Frame (NV12/P010, GPU memory)
  ↓ GPU→CPU transfer (if not processing on GPU)
OpenCV Mat (BGR24, CPU)
  ↓ Color space convert (if needed)
Working Space (linear float32)
  ↓ Apply corrections
Corrected Frame (linear float32)
  ↓ Convert to output color space
Output Space (8/10/12-bit)
  ↓ CPU→GPU transfer (for HW encode)
Frame (NV12, GPU memory)
  ↓ FFmpeg HW-encode
Output Video
```

### FFmpeg Best Practices
- Pre-allocate AVFrame and AVPacket outside loops
- Use `av_hwframe_transfer_data()` for GPU↔CPU
- Use `sws_scale()` for format conversion (NV12→BGR24)
- Memory pool: Reuse same cv::Mat across frames
- Audio passthrough: Copy audio stream without re-encoding

## 7. Scene Detection

### Algorithm: Histogram Difference (Primary)

```python
def scene_change(frame_t, frame_t1, threshold=0.4):
    # Compute per-channel histogram
    hist_t = cv2.calcHist([frame_t], [0,1,2], None, [256,256,256], [0,256]*3)
    hist_t1 = cv2.calcHist([frame_t1], [0,1,2], None, [256,256,256], [0,256]*3)
    
    # Normalize
    hist_t = hist_t / hist_t.sum()
    hist_t1 = hist_t1 / hist_t1.sum()
    
    # Chi-square distance
    diff = cv2.compareHist(hist_t, hist_t1, cv2.HISTCMP_CHISQR)
    
    return diff > threshold
```

### Algorithm: Edge-based (Fallback)

```python
def scene_change_edge(frame_t, frame_t1, threshold=0.3):
    edges_t = cv2.Canny(cv2.cvtColor(frame_t, cv2.COLOR_BGR2GRAY), 50, 150)
    edges_t1 = cv2.Canny(cv2.cvtColor(frame_t1, cv2.COLOR_BGR2GRAY), 50, 150)
    
    # Structural similarity of edges
    similarity = cv2.matchTemplate(edges_t, edges_t1, cv2.TM_CCOEFF_NORMED)
    
    return similarity < threshold
```

### Parameters for FlipsiColor
- Histogram threshold: 0.3-0.5 (tune per video)
- Process every 5th frame for detection (performance)
- At scene boundary: reset reference frame, reset temporal smoothing
- Log all detected scene changes for user review