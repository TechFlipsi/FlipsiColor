# FlipsiColor — Software Development Plan (SDP) v1.0

> **Professionelle KI-gestützte Bild- & Videofarbkorrektur — C++20 / Qt6 / ONNX Runtime**

---

## Inhaltsverzeichnis

1. [Project Overview](#1-project-overview)
2. [System Architecture](#2-system-architecture)
3. [Module Breakdown](#3-module-breakdown)
4. [Data Models](#4-data-models)
5. [Processing Pipeline](#5-processing-pipeline)
6. [AI Model Integration](#6-ai-model-integration)
7. [Style Learning System](#7-style-learning-system)
8. [Video System](#8-video-system)
9. [Cross-Platform Strategy](#9-cross-platform-strategy)
10. [Phases & Milestones](#10-phases--milestones)
11. [Risk Assessment](#11-risk-assessment)
12. [Testing Strategy](#12-testing-strategy)

---

## 1. Project Overview

### Vision
FlipsiColor ist eine native Desktop-Anwendung (C++20/Qt6) die professionelle Foto- und Videofarbkorrektur mit KI automatisiert. Kein Setup, kein Cloud-Service, kein Abo. Installieren → Bild/Video rein → perfekt farbkorrigiert raus.

### Unique Selling Points
1. **Zero-Setup**: Alles gebündelt — LibRaw, OpenCV, Lensfun, LCMS2, FFmpeg, ONNX Runtime. KI-Modelle lazy-loaded.
2. **Stil-Lernend**: 3 Modi (Ask/Swipe, Smart-Learn, Turbo). 2-Runden Lernphase. Die KI lernt den persönlichen Stil.
3. **3 Intensitätsstufen**: Leicht (touch), Mittel (pro, Default), Stark (KI komplett)
4. **Kamera-Universal**: Lensfun 500+ Objektiv-Profile. Nicht nur DJI — alle Kameras.
5. **Bild + Video**: Gleiche Pipeline, gleiche Stil-Profile. Video bekommt Frame-übergreifend konsistente Farben.
6. **Nicht-destruktiv**: Original NIE verändert. Alles als Parameter-Stack gespeichert.
7. **Lokal**: Keine Cloud, kein Account. Deine Bilder bleiben auf deinem Rechner.
8. **Cross-Platform**: Windows (MSVC), macOS (Clang), Linux (GCC 12+)

### Target Users
- Fotografen (JPEG + RAW)
- Drone-Piloten (DJI Air 3S, Mavic, Mini)
- Videografen (Log-Profile, 10-bit)
- Content-Creator (Instagram, YouTube)
- Hobby-Fotografen die Lightroom zu komplex finden

### Tech Stack
| Komponente | Technologie | Zweck |
|---|---|---|
| Sprache | C++20 | Performance, Speicherkontrolle |
| UI | Qt6 + QML | Native Cross-Platform UI |
| AI Runtime | ONNX Runtime | CUDA/DirectML/Metal Backends |
| Bildverarbeitung | OpenCV 4.10+ | Filter, Histogramme, Transforms |
| RAW Decode | LibRaw 0.22+ | Kamera-RAW, EXIF, WB |
| Objektiv-Korrektur | Lensfun 0.3.4+ | 500+ Profile, Verzeichnung/TCA/Vignettierung |
| Video | FFmpeg 6.x/7.x | HW-Decode/Encode |
| Farbmanagement | LCMS2 2.17+ | ICC-Profile, Farbraum-Konvertierung |
| Build | CMake 3.21+ | Cross-Platform Build |

---

## 2. System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      QML UI Layer                           │
│  ┌──────┐  ┌──────────┐  ┌────────┐  ┌─────────────────┐  │
│  │ Bild │  │  Video   │  │Settings│  │ Before/After    │  │
│  │ View │  │  Player  │  │ Panel  │  │ Split View      │  │
│  └──┬───┘  └────┬─────┘  └───┬────┘  └────────┬────────┘  │
│     │           │             │                  │          │
├─────┼───────────┼─────────────┼──────────────────┼──────────┤
│     │     C++ Backend Bridge (Q_PROPERTY/Q_INVOKABLE)    │  │
├─────┼───────────┼─────────────┼──────────────────┼──────────┤
│                     C++ Core Engine                         │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Pipeline Engine (Core)                    │  │
│  │  ┌────────┐ ┌──────────┐ ┌────────┐ ┌──────────────┐ │  │
│  │  │Import  │→│Color Mgmt│→│Lens    │→│AI Processing │ │  │
│  │  │Module │ │(LCMS2)   │ │Corr.   │ │(ONNX Runtime)│ │  │
│  │  └────────┘ └──────────┘ └────────┘ └──────────────┘ │  │
│  │  ┌──────────┐ ┌───────────┐ ┌──────────┐ ┌─────────┐  │  │
│  │  │Style    │→│Video Proc │→│Export    │→│Settings  │  │  │
│  │  │Learning │ │Module     │ │Module    │ │Store     │  │  │
│  │  └──────────┘ └───────────┘ └──────────┘ └─────────┘  │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌───────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ Model Manager │  │ File System  │  │ GPU Detector     │  │
│  │ (ONNX models) │  │ Watcher      │  │ (CUDA/DML/Metal) │  │
│  └───────────────┘  └──────────────┘  └──────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### Module Dependencies

```
Import → Pipeline → Export
  │         │
  │    ┌────┼────┬──────────┐
  │    │    │    │           │
  │  Color  Lens  AI         Style
  │  Mgmt   Corr  Processing Learning
  │    │    │    │           │
  │  LCMS2  Lensfun ONNX RT  AiLUT-Transform
  │         │                │
  │    Video ←────────────────┘
  │    Processing
  │    │
  │  FFmpeg + Scene Detection
```

### Thread Model

```
Main Thread: UI (QML), user interaction, display
Worker Thread 1: Image processing pipeline (heavy CPU/GPU)
Worker Thread 2: Video frame extraction + decode (FFmpeg)
Worker Thread 3: AI model inference (ONNX Runtime)
Worker Thread 4: Thumbnail generation + file scanning
IO Thread: File I/O, model downloads, export writes
```

All worker threads communicate via Qt signals/slots (queued connections). No shared state — only message passing.

---

## 3. Module Breakdown

### 3.1 Import Module (src/import/)

**Purpose**: Load images and video files, extract metadata, create internal representation.

**Class: `ImportManager`**
```cpp
class ImportManager : public QObject {
    Q_OBJECT
public:
    enum class FileType { JPEG, PNG, TIFF, RAW, DNG, MP4, MOV, AVI, MKV, WEBM };
    
    struct ImportResult {
        QString filePath;
        FileType type;
        QSize dimensions;
        int bitDepth;          // 8, 10, 12, 14, 16
        QString colorSpace;   // "sRGB", "AdobeRGB", "ProPhoto", "DisplayP3", "Rec2020", "Unknown"
        bool isLogProfile;
        QString logProfile;    // "D-Log M", "S-Log3", "C-Log3", etc.
        QString cameraMake;
        QString cameraModel;
        QString lensModel;
        double focalLength;
        double aperture;
        double shutterSpeed;
        int iso;
        QDateTime captureTime;
        bool hasGPS;
        double gpsLat, gpsLon, gpsAlt;
    };
    
    Q_INVOKABLE ImportResult analyzeFile(const QString& path);
    Q_INVOKABLE QList<ImportResult> analyzeDirectory(const QString& path);
    
private:
    LibRaw m_rawProcessor;
    ImportResult analyzeImageFile(const QString& path);
    ImportResult analyzeVideoFile(const QString& path);
    QString detectLogProfile(const ImportResult& info, const cv::Mat& sample);
};
```

**Supported Formats**:
- Images: JPEG, PNG, TIFF, BMP, WebP, HEIF/HEIC, DNG, CR2/CR3, NEF, ARW, RAF, RW2, ORF, PEF, SRW, KDC, DCR, MOS
- Video: MP4 (H.264/HEVC/AV1), MOV (ProRes), AVI, MKV, WebM
- Max image: 200MP (limited by RAM)
- Max video: 8K, 120fps

**Log Profile Detection**:
1. Check EXIF maker notes for log profile tag
2. Analyze thumbnail histogram (flat = log)
3. Match against known camera+log combinations

### 3.2 Pipeline Engine (src/pipeline/)

**Purpose**: Orchestrate all processing steps. Non-destructive parameter stack.

**Class: `PipelineEngine`**
```cpp
class PipelineEngine : public QObject {
    Q_OBJECT
public:
    struct PipelineParams {
        // WB
        double wbTemperature;     // Kelvin, 2000-15000
        double wbTint;            // -100 to +100
        bool wbAuto;              // Use auto WB
        
        // Exposure
        double exposure;          // EV, -5.0 to +5.0
        double highlights;        // -100 to +100
        double shadows;           // -100 to +100
        double whites;            // -100 to +100
        double blacks;            // -100 to +100
        
        // Contrast
        double contrast;          // -100 to +100
        double clarity;           // -100 to +100
        
        // Color
        double saturation;        // -100 to +100
        double vibrance;          // -100 to +100
        double hueShift;          // -180 to +180 degrees
        
        // Denoise
        double luminanceNR;       // 0-100
        double chromaNR;          // 0-100
        
        // Sharpen
        double sharpenAmount;     // 0-100
        double sharpenRadius;     // 0.5-3.0
        double sharpenThreshold;  // 0-50
        
        // Lens
        bool enableDistortion;    // auto from Lensfun
        bool enableTCA;
        bool enableVignetting;
        
        // AI
        bool enableAIDenoise;
        bool enableAIDeblur;
        bool enableAIUpscale;
        int upscaleFactor;        // 2 or 4
        bool enableFaceRestore;
        
        // Style
        QString styleProfileId;
        double styleStrength;     // 0-100%
        
        // Output
        QString outputColorSpace; // "sRGB", "AdobeRGB", "DisplayP3", "ProPhoto"
        int outputBitDepth;       // 8 or 16
        int outputQuality;        // JPEG: 1-100
    };
    
    // Process single image
    Q_INVOKABLE ProcessResult processImage(const QString& filePath, 
                                             const PipelineParams& params,
                                             const QString& styleProfileId = "");
    
    // Process with preview (lower quality, for UI)
    Q_INVOKABLE QImage processPreview(const QString& filePath,
                                        const PipelineParams& params,
                                        const QSize& targetSize);
    
    // Get parameter stack for undo/redo
    Q_INVOKABLE QVector<PipelineParams> getParamStack(const QString& filePath) const;
    
signals:
    void progressChanged(int percent);
    void stageChanged(const QString& stageName);
    void processingComplete(const ProcessResult& result);
};
```

**Processing Order (CRITICAL — never change without migration)**:
1. RAW Decode (LibRaw) → 16-bit linear RGB
2. Black Level Subtraction
3. White Balance Application
4. Color Space Convert (Camera → Working Space, LCMS2)
5. Lens Correction (Distortion → TCA → Vignetting, Lensfun)
6. Exposure Compensation
7. Highlight Recovery
8. Tone Curve (Shadow/Highlight/Contrast)
9. AI Denoise / Deblur (Restormer-light via ONNX)
10. Color Grading (AiLUT-Transform style + manual adjustments)
11. Skin Tone Protection
12. Sharpening (Unsharp Mask)
13. Output Color Space Convert (Working → Output, LCMS2)
14. Gamma Encode for Display
15. Optional: AI Upscale (Real_HAT_GAN best quality / Real-ESRGAN fast)
16. Optional: Face Restore (CodeFormer, adjustable fidelity weight)
    - Leicht: fidelity=0.7 (preserve original)
    - Mittel: fidelity=0.5 (balanced)
    - Stark: fidelity=0.3 (max restoration)

### 3.3 Color Management Module (src/color/)

**Class: `ColorManager`**
```cpp
class ColorManager : public QObject {
    Q_OBJECT
public:
    // Initialize with detected monitor profile
    void initialize();
    
    // Get/set working color space
    QString workingSpace() const;        // Default: "ProPhoto RGB"
    void setWorkingSpace(const QString& space);
    
    // Get monitor ICC profile path
    QString monitorProfile() const;
    
    // Create transform (cached)
    cmsHTRANSFORM getTransform(const QString& from, const QString& to,
                                int intent = INTENT_PERCEPTUAL,
                                bool blackPointComp = true);
    
    // Convert image between color spaces
    cv::Mat convertColorSpace(const cv::Mat& image,
                               const QString& from, const QString& to);
    
    // Get preview for display (working → monitor)
    QImage toDisplay(const cv::Mat& linearFloatImage);
    
    // Soft proof (working → output simulation → monitor)
    QImage softProof(const cv::Mat& image, const QString& outputSpace);
    
    // Built-in profiles
    static cmsHPROFILE createSRGBProfile();
    static cmsHPROFILE createAdobeRGBProfile();
    static cmsHPROFILE createProPhotoProfile();
    static cmsHPROFILE createDisplayP3Profile();
    static cmsHPROFILE createRec2020Profile();
    static cmsHPROFILE createACEScgProfile();
    
private:
    std::unordered_map<TransformKey, cmsHTRANSFORM> m_transformCache;
    cmsHPROFILE m_workingProfile;
    cmsHPROFILE m_monitorProfile;
    QMutex m_cacheMutex;
};
```

**Working Space**: ProPhoto RGB (linear gamma) as default — widest practical gamut, prevents clipping.

**Display Pipeline**: Working Space → LCMS2 Transform → Monitor ICC → 8-bit BGRA → QML Image

**Transform Caching**: LRU cache of last 20 transforms. Invalidation on profile change.

### 3.4 Lens Correction Module (src/lens/)

**Class: `LensCorrection`**
```cpp
class LensCorrection : public QObject {
    Q_OBJECT
public:
    struct CorrectionParams {
        bool enableDistortion = true;
        bool enableTCA = true;
        bool enableVignetting = true;
    };
    
    struct DetectionResult {
        bool found;
        QString cameraMake;
        QString cameraModel;
        QString lensModel;
        double cropFactor;
        const lfCamera* cameraPtr;
        const lfLens* lensPtr;
    };
    
    void initialize(const QString& lensfunDbPath);
    
    // Auto-detect camera/lens from EXIF
    DetectionResult detectFromExif(const QString& make, const QString& model,
                                    const QString& lensName);
    
    // Apply all corrections to image
    cv::Mat correct(const cv::Mat& image, const DetectionResult& detection,
                     double focalLength, double aperture, double subjectDist,
                     const CorrectionParams& params);
    
    // Get list of matching cameras (for manual selection)
    QStringList findCameras(const QString& make, const QString& model);
    QStringList findLenses(const QString& cameraModel);
    
private:
    lfDatabase* m_db;
};
```

**Correction Order**: Distortion geometry → TCA (subpixel shift) → Vignetting (per-pixel multiplier)

**Database Path**:
- Windows: `%APPDATA%/flipsicolor/lensfun/`
- macOS: `~/Library/Application Support/flipsicolor/lensfun/`
- Linux: `~/.local/share/flipsicolor/lensfun/`

**Bundled**: Ship Lensfun database with app. User can add custom profiles.

### 3.5 AI Processing Module (src/ai/)

**Class: `AIProcessor`**
```cpp
class AIProcessor : public QObject {
    Q_OBJECT
public:
    enum class ModelType {
        Denoise = 0,
        Deblur,
        Upscale,
        FaceRestore,
        StyleLUT,
        SceneClassify
    };
    
    // Initialize with GPU detection
    void initialize();
    
    // Get available GPU backend
    QString gpuBackend() const;  // "CUDA", "DirectML", "CoreML/Metal", "CPU"
    
    // Check model availability
    bool isModelAvailable(ModelType type) const;
    qint64 modelSize(ModelType type) const;
    
    // Download model (lazy-load)
    Q_INVOKABLE void downloadModel(ModelType type);
    
    // Process image with specific model
    cv::Mat denoise(const cv::Mat& image, float strength);    // NAFNet / Restormer-light
    cv::Mat deblur(const cv::Mat& image);                      // Restormer-light
    cv::Mat upscale(const cv::Mat& image, int factor, bool quality = true); // HAT or ESRGAN
    cv::Mat restoreFaces(const cv::Mat& image, float fidelityWeight = 0.5f); // CodeFormer
    
    // Apply learned style LUT
    cv::Mat applyStyleLUT(const cv::Mat& image, const StyleLUT& lut);
    
    // Scene classification
    SceneType classifyScene(const cv::Mat& image, const ImportResult& exif);
    
    // Get model download progress
    Q_INVOKABLE qreal downloadProgress(ModelType type) const;
    
signals:
    void modelDownloadProgress(ModelType type, qreal progress);
    void modelDownloadComplete(ModelType type);
    void modelDownloadFailed(ModelType type, const QString& error);
    void processingComplete();
    
private:
    Ort::Env m_env;
    std::unordered_map<ModelType, std::unique_ptr<Ort::Session>> m_sessions;
    std::unordered_map<ModelType, QString> m_modelPaths;
    QString m_gpuBackend;
    OrtCUDAProviderOptions m_cudaOpts;   // If CUDA available
};
```

**GPU Fallback Chain**:
1. Try CUDA (NVIDIA) → if available and CC ≥ 5.3
2. Try DirectML (Windows) → if any DX12 GPU
3. Try CoreML (macOS) → if Apple Silicon or AMD
4. Fall back to CPU (with warning: slower)

**Model Storage**: `~/.local/share/flipsicolor/models/` (Linux), equivalent on other platforms.

**Model Manifest** (`models-manifest.json`):
```json
{
  "version": 1,
  "models": {
    "nafnet": {
      "url": "https://models.flipsicolor.com/nafnet-v1.0.onnx",
      "size": 17825792,
      "sha256": "abc123...",
      "required": true,
      "description": "NAFNet Denoise (17MB)"
    },
    "restormer-light": {
      "url": "https://models.flipsicolor.com/restormer-light-v1.0.onnx",
      "size": 25165824,
      "sha256": "def456...",
      "required": true,
      "description": "Restormer Light Multi-Task (24MB)"
    },
    "real-hat-gan": {
      "url": "https://models.flipsicolor.com/real-hat-gan-srx4-v1.0.onnx",
      "size": 125829120,
      "sha256": "hat123...",
      "required": false,
      "description": "Real_HAT_GAN_SRx4 Best Quality Upscale (120MB)"
    },
    "real-esrgan": {
      "url": "https://models.flipsicolor.com/real-esrgan-x4-v1.0.onnx",
      "size": 67108864,
      "sha256": "ghi789...",
      "required": false,
      "description": "Real-ESRGAN Fast Upscale (64MB)"
    },
    "codeformer": {
      "url": "https://models.flipsicolor.com/codeformer-v1.0.onnx",
      "size": 367001600,
      "sha256": "cfo456...",
      "required": false,
      "description": "CodeFormer Face Restoration with Fidelity Weight (350MB)"
    },
    "ailut-transform": {
      "url": "https://models.flipsicolor.com/ailut-transform-v1.0.onnx",
      "size": 8388608,
      "sha256": "mno345...",
      "required": true,
      "description": "AiLUT-Transform Image-Adaptive Style Learning (8MB)"
    },
    "efficientnet-lite0": {
      "url": "https://models.flipsicolor.com/efficientnet-lite0-places365-v1.0.onnx",
      "size": 4833280,
      "sha256": "pqr678...",
      "required": false,
      "description": "EfficientNet-Lite0 Scene Classifier (4.6MB)"
    }
  }
}
```

**Tiling for Large Images**: For images >2048px, process in overlapping 512×512 tiles (64px overlap). Blend with cosine window to avoid seams.

### 3.6 Style Learning Module (src/style/)

**Class: `StyleLearningEngine`**
```cpp
class StyleLearningEngine : public QObject {
    Q_OBJECT
public:
    enum class Mode { Ask, SmartLearn, Turbo };
    enum class Intensity { Leicht, Mittel, Stark };
    enum class Feedback { Positive, Negative, Skip };
    
    // Current state
    Mode mode() const;
    Intensity intensity() const;
    int feedbackCount() const;
    int learningRound() const;           // 1 (0-60), 2 (61-120), 0 (autonomous 120+)
    bool isAutonomous() const;            // feedbackCount >= 120
    
    // Process feedback (Ask mode)
    Q_INVOKABLE void submitFeedback(const QString& filePath, Feedback feedback,
                                      const QStringList& tags = {});
    
    // Record edit (Smart-Learn mode)
    Q_INVOKABLE void recordEdit(const QString& filePath, const PipelineParams& original,
                                 const PipelineParams& edited);
    
    // Get style suggestion for image
    Q_INVOKABLE PipelineParams suggestParams(const QString& filePath,
                                              const SceneType& scene);
    
    // Get style LUT for video/pipeline
    StyleLUT currentStyleLUT() const;
    
    // Style profiles
    QStringList styleProfiles() const;
    Q_INVOKABLE void createProfile(const QString& name);
    Q_INVOKABLE void deleteProfile(const QString& id);
    Q_INVOKABLE void exportProfile(const QString& id, const QString& path);
    Q_INVOKABLE void importProfile(const QString& path);
    
    // Reset
    Q_INVOKABLE void resetStyle();
    
    // Knowledge base management
    qint64 knowledgeBaseSize() const;     // bytes
    Q_INVOKABLE void compactKnowledgeBase();
    
signals:
    void feedbackCountChanged(int count);
    void roundChanged(int round);
    void learningProgress(double percent); // 0-1
    void styleProfileChanged();
    
private:
    Mode m_mode = Mode::Ask;
    Intensity m_intensity = Intensity::Mittel;
    int m_feedbackCount = 0;
    
    // Style knowledge base
    struct KnowledgeEntry {
        SceneType sceneType;
        double weight;                 // feedback count (consolidated)
        PipelineParams params;        // accumulated average params
        StyleLUT lut;                 // learned 3DLUT
        QDateTime lastUpdated;
    };
    std::vector<KnowledgeEntry> m_knowledge;
    
    // AiLUT-Transform training data
    struct TrainingPair {
        cv::Mat originalThumb;     // 256×256
        cv::Mat editedThumb;       // 256×256
        SceneType scene;
        Feedback feedback;
    };
    std::deque<TrainingPair> m_trainingQueue;
};
```

**Knowledge Base Schema** (SQLite: `~/.local/share/flipsicolor/style.db`):
```sql
CREATE TABLE feedback_history (
    id INTEGER PRIMARY KEY,
    file_hash TEXT,            -- SHA256 of file (NOT the path)
    scene_type TEXT,           -- "landscape", "portrait", "drone", etc.
    feedback INTEGER,          -- 1=positive, 0=negative
    params TEXT,               -- JSON: PipelineParams
    created_at TIMESTAMP
);

CREATE TABLE style_profiles (
    id TEXT PRIMARY KEY,       -- UUID
    name TEXT,
    params TEXT,               -- JSON: base PipelineParams
    lut BLOB,                  -- AiLUT-Transform weights
    scene_overrides TEXT,      -- JSON: { "landscape": {...}, "portrait": {...} }
    feedback_count INTEGER,
    created_at TIMESTAMP,
    updated_at TIMESTAMP
);

CREATE TABLE knowledge (
    scene_type TEXT PRIMARY KEY,
    weight REAL,               -- consolidated feedback count
    avg_params TEXT,            -- JSON: averaged PipelineParams
    lut BLOB,                  -- learned AiLUT-Transform weights per scene
    last_updated TIMESTAMP
);

CREATE TABLE meta (
    key TEXT PRIMARY KEY,
    value TEXT                 -- total_feedback_count, learning_round, etc.
);
```

**3-Stage Compaction** (triggered every 100 feedbacks or on app start):
1. **Deduplication**: Same scene + similar params → average and merge weights
2. **Consolidation**: Similar scene types (landscape-summer + landscape-spring) → merge with variants
3. **Compaction**: weight < 3 AND age > 365 days → delete

**Target**: DB stays under 200MB (realistic: ~50MB for heavy users)

### 3.7 Video Processing Module (src/video/)

**Class: `VideoProcessor`**
```cpp
class VideoProcessor : public QObject {
    Q_OBJECT
public:
    struct VideoInfo {
        QString filePath;
        int width, height;
        int fpsNum, fpsDen;
        int totalFrames;
        double duration;            // seconds
        QString codec;              // "h264", "hevc", "av1", "prores"
        int bitDepth;              // 8, 10, 12
        QString chromaSubsampling; // "4:2:0", "4:2:2", "4:4:4"
        bool isLog;
        QString logProfile;
        bool hasAudio;
        QString audioCodec;
    };
    
    // Analyze video
    Q_INVOKABLE VideoInfo analyze(const QString& path);
    
    // Process video with style profile
    Q_INVOKABLE void processVideo(const QString& inputPath,
                                    const QString& outputPath,
                                    const PipelineParams& params,
                                    const QString& styleProfileId = "");
    
    // Extract single frame for preview
    Q_INVOKABLE QImage extractFrame(const QString& path, int frameNumber);
    
    // Detect scene changes
    Q_INVOKABLE QList<int> detectScenes(const QString& path,
                                          double threshold = 0.4);
    
signals:
    void frameProgress(int current, int total);
    void sceneDetected(int frameNumber);
    void processingComplete(const QString& outputPath);
    
private:
    // Scene detection
    bool isSceneChange(const cv::Mat& frame_t, const cv::Mat& frame_t1,
                        double threshold);
    
    // Temporal smoothing
    cv::Mat temporalSmooth(const cv::Mat& current, const cv::Mat& previous,
                            double alpha = 0.5);
    
    // Skin tone protection mask
    cv::Mat createSkinMask(const cv::Mat& hsvFrame);
    
    // HW-accelerated decode pipeline
    AVCodecContext* createHwDecoder(AVCodecID codecId);
    AVCodecContext* createHwEncoder(AVCodecID codecId, int width, int height,
                                     int fps, int64_t bitRate);
};
```

**Video Processing Pipeline**:
```
1. Open video (FFmpeg, HW-accelerated decode)
2. Analyze first 30 frames → detect log profile, exposure baseline
3. For each frame:
   a. Decode frame (HW → CPU transfer → OpenCV Mat)
   b. Convert to working space (if log: apply inverse log curve)
   c. Apply corrections (same pipeline as images)
   d. Temporal smooth with previous frame(s)
   e. Skin tone protection
   f. Convert to output color space
   g. Encode frame (HW-accelerated)
4. Copy audio stream (no re-encode)
5. Write output file
```

**Scene Detection** (on every 5th frame for performance):
- Histogram chi-square distance > threshold → scene change
- At scene boundary: reset reference frame, reset temporal smoothing
- Threshold: 0.3-0.5 (tune per video based on first 30 frames' variance)

**Temporal Smoothing**:
- EMA: `corrected[t] = α * raw_corrected[t] + (1-α) * corrected[t-1]`
- α = 0.5 default (adjustable in Settings)
- Scene boundary reset: α = 1.0 at scene change frames
- Only smooth chroma (hue/saturation), preserve luminance changes

**Chroma Upsampling**: Before processing 4:2:0 video, upsample chroma to 4:4:4 using Lanczos interpolation. This prevents color artifacts during processing.

### 3.8 Export Module (src/export/)

**Class: `ExportManager`**
```cpp
class ExportManager : public QObject {
    Q_OBJECT
public:
    struct ExportParams {
        QString inputPath;
        QString outputDirectory;
        QString fileNameTemplate;    // "{original}", "{original}_edited", etc.
        QString format;              // "JPEG", "PNG", "TIFF", "DNG", "MP4", "MOV"
        int quality;                 // JPEG: 1-100, PNG: 0 (lossless)
        QString colorSpace;          // "sRGB", "AdobeRGB", "DisplayP3", "ProPhoto"
        int bitDepth;                // 8 or 16
        bool preserveEXIF;
        bool removeGPS;
        bool addWatermark;
        QString watermarkText;
        QString watermarkImagePath;
        int watermarkPosition;       // 0-8 grid positions
        qreal watermarkOpacity;      // 0-1
        int maxWidth, maxHeight;     // Resize if needed (0 = no resize)
    };
    
    // Export profiles (presets)
    struct ExportProfile {
        QString id;
        QString name;
        QString icon;
        ExportParams params;
    };
    
    // Built-in profiles
    QList<ExportProfile> builtInProfiles() const;
    // Instagram (sRGB, JPEG 85%, 1080px max), YouTube (sRGB, MP4 H.264),
    // Print (AdobeRGB, TIFF 16-bit, no resize), Web (sRGB, WebP 80%),
    // Drone (sRGB, JPEG 95%, keep size), Archiv (ProPhoto, TIFF 16-bit)
    
    Q_INVOKABLE void exportImage(const QString& input, const ExportParams& params);
    Q_INVOKABLE void exportVideo(const QString& input, const ExportParams& params);
    Q_INVOKABLE void exportBatch(const QStringList& inputs, const ExportParams& params);
    
    // Export folder options
    enum class FolderMode { SeparateFolder, Subfolder, InPlace };
    
signals:
    void exportProgress(int current, int total);
    void exportComplete(const QString& outputPath);
    
private:
    cv::Mat applyWatermark(const cv::Mat& image, const ExportParams& params);
    bool writeEXIF(const QString& path, const ImportResult& metadata, bool removeGPS);
};
```

**Export Folder Structure**:
- SeparateFolder: `{outputDir}/image_edited.jpg`
- Subfolder: `{sourceDir}/_bearbeitet/image.jpg` (subfolder name configurable)
- InPlace: `{sourceDir}/image_edited.jpg` (never overwrites original)

### 3.9 UI Module (src/ui/)

**QML Structure**:
```
Main.qml
├── Sidebar.qml              (3 icons: Image, Video, Settings)
├── ImageMode/
│   ├── ImageListView.qml    (thumbnail grid, folder picker)
│   ├── ImageEditor.qml      (single image, before/after)
│   ├── AutoEnhanceBar.qml   (intensity selector, mode selector)
│   └── HistogramPanel.qml   (RGB histogram, waveform)
├── VideoMode/
│   ├── VideoListView.qml    (thumbnail list, folder picker)
│   ├── VideoPlayer.qml      (timeline, scrub, preview)
│   └── VideoExport.qml      (format, codec, quality)
├── Settings/
│   ├── GeneralSettings.qml  (language, theme, auto-update)
│   ├── AILearning.qml      (mode, intensity, learning progress)
│   ├── StyleReset.qml       (reset, export/import profiles)
│   ├── ImageSettings.qml    (default NR, sharpening, lens corr)
│   ├── VideoSettings.qml    (temporal smoothing, scene detect)
│   ├── ExportSettings.qml   (folder mode, subfolder name, profiles)
│   └── GPUSettings.qml      (backend info, VRAM usage)
├── Shared/
│   ├── BeforeAfter.qml      (draggable split, left=original, right=edited)
│   ├── SwipeFeedback.qml    (👍👎 for Ask mode)
│   ├── StyleProfiles.qml    (profile list, rename, delete)
│   └── FirstStartWizard.qml (4-step tour)
└── Components/
    ├── Slider.qml            (custom styled)
    ├── ComboBox.qml          (custom styled)
    └── Toast.qml             (notification popup)
```

**C++ Backend Classes exposed to QML**:
```cpp
// Registered in main.cpp:
qmlRegisterSingletonType<AppController>("com.flipsi.color", 1, 0, "App");
qmlRegisterType<FileListModel>("com.flipsi.color", 1, 0, "FileList");
qmlRegisterType<PipelineEngine>("com.flipsi.color", 1, 0, "Pipeline");
qmlRegisterType<StyleLearningEngine>("com.flipsi.color", 1, 0, "Style");
qmlRegisterType<AIProcessor>("com.flipsi.color", 1, 0, "AI");
```

**Key QML Interactions**:
- Image selection → `Pipeline.processPreview(path, params, size)` → `Image { source: "image://preview/..." }`
- Auto-Enhance button → `AI.suggestParams(path, scene)` → `Pipeline.processPreview()`
- 👍 button → `Style.submitFeedback(path, Positive)` → learning update
- Intensity dropdown → `App.intensity = Mittel` → re-process with adjusted params
- Mode toggle → `Style.mode = SmartLearn` → background learning

---

## 4. Data Models

### .flipsicolor Project File (JSON)

```json
{
  "version": 1,
  "created": "2026-05-24T10:30:00Z",
  "modified": "2026-05-24T10:35:00Z",
  "source": {
    "filePath": "/home/user/photos/DJI_001.DNG",
    "fileHash": "sha256:abc123...",
    "fileSize": 52428800,
    "lastModified": "2026-05-23T14:22:00Z"
  },
  "import": {
    "cameraMake": "DJI",
    "cameraModel": "Air 3S",
    "lensModel": "DJI Air 3S Camera",
    "focalLength": 24.0,
    "aperture": 1.8,
    "shutterSpeed": 0.004,
    "iso": 100,
    "width": 5472,
    "height": 3648,
    "bitDepth": 14,
    "isLog": true,
    "logProfile": "D-Log M",
    "colorSpace": "Unknown",
    "hasGPS": true
  },
  "pipeline": {
    "steps": [
      { "type": "whiteBalance", "params": { "temperature": 5500, "tint": 5 } },
      { "type": "exposure", "params": { "ev": 0.3, "highlights": -20, "shadows": 15 } },
      { "type": "lensCorrection", "params": { "distortion": true, "tca": true, "vignetting": true } },
      { "type": "denoise", "params": { "strength": 30, "model": "restormer-light" } },
      { "type": "colorGrade", "params": { "styleProfile": "default-drone", "strength": 75 } },
      { "type": "sharpen", "params": { "amount": 40, "radius": 1.0, "threshold": 5 } }
    ]
  },
  "export": {
    "colorSpace": "sRGB",
    "bitDepth": 8,
    "format": "JPEG",
    "quality": 92
  },
  "styleLearning": {
    "feedbackGiven": "positive",
    "sceneType": "drone",
    "round": 1,
    "feedbackIndex": 42
  }
}
```

### .flipsicolor-style Style Profile (JSON)

```json
{
  "version": 1,
  "name": "Meine Drohne",
  "created": "2026-05-20T08:00:00Z",
  "baseParams": {
    "temperature": 5500, "tint": 3,
    "exposure": 0.2, "highlights": -15, "shadows": 10,
    "contrast": 10, "saturation": 8, "vibrance": 12,
    "sharpenAmount": 35, "luminanceNR": 25, "chromaNR": 30
  },
  "sceneOverrides": {
    "landscape": { "saturation": 15, "vibrance": 20 },
    "portrait": { "saturation": -5, "sharpenAmount": 20 },
    "night": { "shadows": 30, "luminanceNR": 50 }
  },
  "styleLUT": "<base64-encoded AiLUT-Transform weights>",
  "feedbackCount": 87,
  "totalFeedbackCount": 87
}
```

### Knowledge Base (SQLite)

See section 3.6 for full schema.

### Model Manifest

See section 3.5 for full JSON schema.

---

## 5. Processing Pipeline

### Image Pipeline (Detailed)

```
INPUT FILE
   │
   ├─ JPEG/PNG/TIFF → OpenCV imread → 8-bit sRGB
   ├─ RAW/DNG → LibRaw → 16-bit linear RGB + EXIF
   └─ Video frame → FFmpeg decode → NV12/P010 → OpenCV BGR
   
   │
   ▼
[1] Black Level Subtraction
    sensor_black = imgdata.color.black
    pixel = max(0, pixel - sensor_black)
    
   │
   ▼
[2] White Balance
    Apply WB multipliers: R*mul_r, G*mul_g, B*mul_b
    Source: camera WB / auto WB / custom
    
   │
   ▼
[3] Color Space: Camera → Working (LCMS2)
    Camera matrix → ProPhoto RGB (linear)
    Float32, unbounded mode
    
   │
   ▼
[4] Lens Correction (Lensfun)
    IF EXIF has make/model/lens:
      Distortion correction (geometry remap)
      TCA correction (subpixel shift)
      Vignetting correction (multiply)
    IF no Lensfun match:
      Skip (warn user)
    
   │
   ▼
[5] Exposure
    Linear multiply: pixel * 2^ev
    
   │
   ▼
[6] Highlight Recovery
    IF pixel > 0.95 in working space:
      Blend with neighboring unclipped pixels
    Mode configurable (clip/unclip/reconstruct)
    
   │
   ▼
[7] Tone Curve (Shadow/Highlight/Contrast)
    Apply parametric tone curve
    Shadows lift, highlights compress
    Contrast: S-curve around mid-gray
    
   │
   ▼
[8] AI Denoise / Deblur (ONNX Runtime)
    IF Intensity == Leicht: skip AI denoise
    IF Intensity == Mittel: light denoise (strength 30)
    IF Intensity == Stark: full denoise + deblur (strength 70)
    Model: NAFNet or Restormer-light
    
   │
   ▼
[9] Color Grading
    Manual adjustments: saturation, vibrance, hue
    Style profile: AiLUT-Transform learned transform
    Blend: manual * (1 - styleStrength) + style * styleStrength
    
   │
   ▼
[10] Skin Tone Protection
    Detect skin mask (HSV threshold)
    Preserve skin hue angle within ±5°
    Reduce saturation/contrast on skin
    
   │
   ▼
[11] Sharpening
    Unsharp Mask: amount, radius, threshold
    IF Intensity == Leicht: amount = 15
    IF Intensity == Mittel: amount = 35
    IF Intensity == Stark: amount = 50
    
   │
   ▼
[12] Working Space → Output (LCMS2)
    ProPhoto RGB → sRGB / AdobeRGB / DisplayP3 / Rec2020
    
   │
   ▼
[13] Gamma Encode for Display
    Apply output TRC (sRGB: piecewise ~2.2)
    Quantize to output bit depth (8 or 16)
    
   │
   ▼
[14] Optional: AI Upscale (Real_HAT_GAN best / Real-ESRGAN fast)
    IF upscaleFactor > 1:
      Process in 512×512 tiles, 64px overlap
      Cosine-window blend at edges
    
   │
   ▼
[15] Optional: Face Restoration (GFPGAN)
    IF enableFaceRestore:
      Detect faces (OpenCV DNN or ultra-lightweight)
      Crop face regions + 48px padding
      Run CodeFormer per face (with fidelity weight)
      Blend back (feathered mask)
    
   │
   ▼
OUTPUT: cv::Mat (8/16-bit, output color space)
```

### Video Pipeline (Detailed)

```
VIDEO FILE
   │
   ▼
[0] FFmpeg Open + Analyze
    Detect: codec, bit depth, chroma subsampling
    HW-accelerated decoder selection
    First 30 frames: analyze for log profile, exposure baseline
    
   │
   ▼
[1] Scene Detection (on every 5th frame)
    Histogram chi-square distance
    Mark scene boundaries
    Build scene list
    
   │
   ▼
[2] For each scene:
    Select reference frame (best-exposed frame in scene)
    Process reference frame through FULL image pipeline
    Store result as scene reference
    
   │
   ▼
[3] For each frame in scene:
    Decode frame (HW-accelerated)
    Process through image pipeline (steps 1-13)
    Temporal smooth with reference:
      - Luminance: keep current (preserve motion)
      - Chroma: EMA with reference (hue/sat stability)
      α = 0.5 (adjustable)
    At scene boundary: α = 1.0 (reset)
    
   │
   ▼
[4] Skin Tone Protection (per frame)
    Generate skin mask (HSV threshold)
    On skin pixels: reduce style strength by 60-80%
    
   │
   ▼
[5] Encode (HW-accelerated)
    NVENC / AMF / VideoToolbox / VAAPI
    Match input codec + quality
    If input was 10-bit HEVC: output 10-bit HEVC
    
   │
   ▼
[6] Audio Passthrough
    Copy audio stream without re-encode
    Sync timestamps with video
    
   │
   ▼
OUTPUT: video file in export directory
```

---

## 6. AI Model Integration

### Model Download Manager

```cpp
class ModelDownloadManager : public QObject {
    Q_OBJECT
public:
    struct ModelInfo {
        QString id;              // "nafnet", "restormer-light", etc.
        QString url;             // Download URL
        qint64 size;             // File size in bytes
        QString sha256;          // Integrity check
        bool required;           // Core model or optional
        QString description;
    };
    
    void initialize(const QString& manifestUrl);
    
    // Check which models need download
    QList<ModelInfo> pendingModels() const;
    
    // Download specific model
    Q_INVOKABLE void download(const QString& modelId);
    Q_INVOKABLE void downloadAllRequired();
    
    // Verify model integrity
    bool verifyModel(const QString& modelId);
    
signals:
    void downloadProgress(const QString& modelId, qint64 received, qint64 total);
    void downloadComplete(const QString& modelId);
    void downloadFailed(const QString& modelId, const QString& error);
    void allRequiredDownloaded();
    
private:
    QString m_modelsDir;        // ~/.local/share/flipsicolor/models/
    QNetworkAccessManager m_network;
    std::unordered_map<QString, ModelInfo> m_manifest;
};
```

### ONNX Session Management

- **One `Ort::Env`** shared across all sessions (process-level)
- **Session creation**: Lazy — session created on first use of that model
- **Session options**: Based on detected GPU backend
- **Memory**: Enable `MemPattern` + `CpuMemArena` for all sessions
- **Thread safety**: Each session is thread-safe for `Run()` (one `Run` at a time per session)

### Inference Optimizations

1. **FP16 models**: Ship FP16 ONNX models where possible (~50% size reduction, ~30% speed increase on Ampere+)
2. **IO Binding**: Pre-allocate input/output tensors, zero-copy inference
3. **Tiling**: Images >2048px processed in overlapping 512×512 tiles
4. **Batch processing**: For video, batch 4 frames at once when GPU memory allows
5. **Model warmup**: Run one dummy inference on app start to warm up GPU

---

## 7. Style Learning System

### Ask Mode Flow (Default)

```
1. User opens image
2. FlipsiColor analyzes EXIF + scene type
3. Pipeline generates "Mittel" default correction
4. UI shows: [👍] Original  |  [👎] Enhanced
   (Original left, Enhanced right, draggable divider)
5. User presses 👍:
   - StyleLearningEngine.submitFeedback(path, Positive)
   - feedbackCount++
   - If feedbackCount crosses 20-30 threshold:
     KI starts auto-applying for known scenes (reduces questions)
6. User presses 👎:
   - Generate alternative variant (Mittel → adjust contrast/warmth)
   - Show new variant: [👍] Var1 [👎→Var2] Original
   - Max 3 variants, then suggest manual editing
7. Optional: Quick-Tags (#too_warm, #too_dark, #good_contrast)
8. Optional: Free text feedback
```

### Smart-Learn Mode Flow

```
1. User opens image
2. Auto-enhance applied immediately (like Ask mode but no 👍👎)
3. User makes manual adjustments (exposure, contrast, etc.)
4. StyleLearningEngine.recordEdit() records:
   - Original auto-params
   - User's modified params
   - Scene type
5. Difference (user - auto) is treated as implicit feedback
6. Learning engine updates knowledge base in background
7. Next image of same scene type → adjusted auto-params
```

### Turbo Mode Flow

```
1. User selects folder (no single image — batch only)
2. No 👍👎, no preview, no confirmations
3. KI processes all images/videos with:
   - Built-in photography knowledge (exposure, WB, etc.)
   - Learned style profile (if exists, otherwise Mittel defaults)
4. Does NOT learn from results (no feedback = no data)
5. Export immediately to output folder
6. Progress bar: "Frame 142 / 5400"
```

### 2-Round Learning Phases

```
R1 (0-60): Full Lernphase
├── KI asks for 👍👎 on EVERY image
├── Progress bar: 🔴 42/60
├── At 60: "Runde 1 abgeschlossen!"
└── Show what KI knows vs what's missing

R2 (61-120): Vertiefung
├── KI only asks on uncertain/new scenes
├── Known scenes: auto-apply, skip question
├── Progress bar: 🟡 85/120
├── Show: "Bitte Portraits und Nachtbilder testen!"
└── At 120: "KI ist jetzt selbstständig!"

Autonomous (120+): No more questions
├── KI applies learned style automatically
├── User can still 👍👎 → KI adjusts
├── New scene types: mini 5-10 feedback cycle
└── Progress bar: 🟢 (always solid green)
```

### Auto-Retrain on New Scenes

```
1. KI encounters image with unknown scene type
2. Classify: EfficientNet-Lite0 → "architecture" (new!)
3. Show banner: "Neuer Bildtyp: Architektur. 5 Feedbacks zum Lernen?"
4. User confirms → 5-image mini learning cycle
5. After 5 feedbacks: architecture style = average of those 5
6. Known scenes (landscape, drone) UNTOUCHED
```

### Style Reset

```
Settings → "Stil zurücksetzen"
├── Confirmation dialog: "AI-Stil wirklich zurücksetzen? Alle gelernten Einstellungen gehen verloren."
├── On confirm:
│   ├── Clear knowledge base (all tables)
│   ├── Reset feedback count to 0
│   ├── Reset learning round to R1
│   ├── Clear AiLUT-Transform weights
│   ├── Clear scene overrides
│   └── Keep style profiles (user-created, not auto)
└── After reset: Lernphase restarts from 0/60
```

---

## 8. Video System

### Supported Codecs

| Decode | Encode | Platform | HW API |
|---|---|---|---|
| H.264 | H.264 | All | NVDEC/CUVID/DXVA2/D3D11VA/VAAPI/VideoToolbox |
| HEVC (H.265) | HEVC | All | Same as H.264 |
| AV1 | AV1 (if GPU supports) | Win/Linux | NVDEC (AV1)/VAAPI |
| ProRes 422/4444 | ProRes 422 | macOS | VideoToolbox |
| VP9 | — | All | NVDEC/VAAPI |

### Frame Extraction to OpenCV

```
FFmpeg HW Decode → AVFrame (GPU)
   ↓ av_hwframe_transfer_data()
AVFrame (CPU, NV12/P010)
   ↓ sws_scale() with SWS_BILINEAR
cv::Mat (BGR24, 8UC3) or (BGR24, 32FC3) for 10-bit
```

**10-bit handling**: P010 (10-bit NV12 equivalent) → sws_scale to BGR48 (16-bit) → process in 16-bit → encode back.

### Scene Detection Algorithm

```cpp
bool VideoProcessor::isSceneChange(const cv::Mat& frame_t, const cv::Mat& frame_t1, 
                                     double threshold) {
    // Downsample for speed
    cv::Mat small_t, small_t1;
    cv::resize(frame_t, small_t, cv::Size(160, 90));
    cv::resize(frame_t1, small_t1, cv::Size(160, 90));
    
    // Convert to HSV
    cv::Mat hsv_t, hsv_t1;
    cv::cvtColor(small_t, hsv_t, cv::COLOR_BGR2HSV);
    cv::cvtColor(small_t1, hsv_t1, cv::COLOR_BGR2HSV);
    
    // Per-channel histogram
    int histSize = 64;
    float range[] = {0, 256};
    const float* histRange = {range};
    
    double totalDiff = 0;
    for (int c = 0; c < 3; c++) {
        cv::Mat hist_t, hist_t1;
        int channels[] = {c};
        cv::calcHist(&hsv_t, 1, channels, cv::Mat(), hist_t, 1, &histSize, &histRange);
        cv::calcHist(&hsv_t1, 1, channels, hist_t1, 1, &histSize, &histRange);
        cv::normalize(hist_t, hist_t);
        cv::normalize(hist_t1, hist_t1);
        totalDiff += cv::compareHist(hist_t, hist_t1, cv::HISTCMP_CHISQR);
    }
    
    return (totalDiff / 3.0) > threshold;
}
```

### Temporal Consistency

```cpp
cv::Mat VideoProcessor::temporalSmooth(const cv::Mat& current, 
                                         const cv::Mat& previous, 
                                         double alpha) {
    cv::Mat currentHSV, prevHSV;
    cv::cvtColor(current, currentHSV, cv::COLOR_BGR2HSV);
    cv::cvtColor(previous, prevHSV, cv::COLOR_BGR2HSV);
    
    // Split channels
    std::vector<cv::Mat> curChannels, prevChannels;
    cv::split(currentHSV, curChannels);
    cv::split(prevHSV, prevChannels);
    
    // Only smooth Hue and Saturation (not Value/luminance)
    cv::Mat smoothH, smoothS;
    cv::addWeighted(curChannels[0], alpha, prevChannels[0], 1-alpha, 0, smoothH);
    cv::addWeighted(curChannels[1], alpha, prevChannels[1], 1-alpha, 0, smoothS);
    
    // Keep current luminance
    std::vector<cv::Mat> resultChannels = {smoothH, smoothS, curChannels[2]};
    cv::Mat resultHSV, result;
    cv::merge(resultChannels, resultHSV);
    cv::cvtColor(resultHSV, result, cv::COLOR_HSV2BGR);
    
    return result;
}
```

---

## 9. Cross-Platform Strategy

### Build System (CMake)

```cmake
cmake_minimum_required(VERSION 3.21)
project(FlipsiColor VERSION 1.0.0 LANGUAGES CXX C)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Platform detection
if(WIN32)
    set(FLIPSICOLOR_PLATFORM "windows")
    add_compile_definitions(PLATFORM_WINDOWS)
elseif(APPLE)
    set(FLIPSICOLOR_PLATFORM "macos")
    add_compile_definitions(PLATFORM_MACOS)
else()
    set(FLIPSICOLOR_PLATFORM "linux")
    add_compile_definitions(PLATFORM_LINUX)
endif()

# Dependencies via CMake find_package or vcpkg
find_package(Qt6 6.5 REQUIRED COMPONENTS Core Quick Qml QuickControls2 Widgets Concurrent)
find_package(OpenCV 4.10 REQUIRED COMPONENTS core imgproc cudaarithm cudaimgproc)
find_package(lcms2 2.17 REQUIRED)
find_package(LibRaw 0.22 REQUIRED)
find_package(LensFun 0.3.4 REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(FFMPEG REQUIRED IMPORTED_TARGET libavcodec libavformat libavutil libswscale)

# ONNX Runtime (pre-built binary)
include(FetchOnnxRuntime.cmake)

# FlipsiColor executable
qt_add_executable(flipsicolor
    src/main.cpp
    src/import/ImportManager.cpp
    src/pipeline/PipelineEngine.cpp
    src/color/ColorManager.cpp
    src/lens/LensCorrection.cpp
    src/ai/AIProcessor.cpp
    src/ai/ModelDownloadManager.cpp
    src/style/StyleLearningEngine.cpp
    src/style/KnowledgeBase.cpp
    src/video/VideoProcessor.cpp
    src/export/ExportManager.cpp
    src/ui/AppController.cpp
    src/ui/FileListModel.cpp
    src/ui/PreviewImageProvider.cpp
    src/ui/GPUDetector.cpp
)

target_link_libraries(flipsicolor PRIVATE
    Qt6::Core Qt6::Quick Qt6::Qml Qt6::QuickControls2 Qt6::Concurrent
    ${OpenCV_LIBS}
    lcms2::lcms2
    LibRaw::libraw
    LensFun::LensFun
    PkgConfig::FFMPEG
    onnxruntime
)

# Platform-specific sources
if(WIN32)
    target_sources(flipsicolor PRIVATE src/platform/PlatformWindows.cpp)
    target_link_libraries(flipsicolor PRIVATE d3d12 dxgi)  # DirectML
elseif(APPLE)
    target_sources(flipsicolor PRIVATE src/platform/PlatformMacOS.cpp)
else()
    target_sources(flipsicolor PRIVATE src/platform/PlatformLinux.cpp)
endif()

# Install
install(TARGETS flipsicolor
    RUNTIME DESTINATION bin
    BUNDLE DESTINATION .
)
```

### Platform-Specific Code

| Feature | Windows | macOS | Linux |
|---|---|---|---|
| GPU Backend | DirectML (any GPU) / CUDA (NVIDIA) | Metal/CoreML | CUDA (NVIDIA) / VAAPI (AMD/Intel) |
| HW Decode | D3D11VA / CUVID | VideoToolbox | VAAPI / CUVID |
| HW Encode | NVENC / AMF | VideoToolbox | NVENC / VAAPI |
| Monitor ICC | ICM2 API | ColorSync | colord |
| App Data | %APPDATA%/flipsicolor/ | ~/Library/Application Support/flipsicolor/ | ~/.local/share/flipsicolor/ |
| Deployment | .msix or .exe installer | .dmg / .app bundle | .AppImage / Flatpak |

### Deployment

**Windows**: MSIX package or InnoSetup installer. Bundles: Qt6 DLLs, ONNX Runtime DLL, OpenCV DLLs, Lensfun DB, FFmpeg DLLs. ~150MB installer.

**macOS**: .app bundle in .dmg. All frameworks embedded. Signed + notarized. ~180MB.

**Linux**: AppImage (single file, no install). Bundles everything except glibc and GPU drivers. ~170MB.

---

## 10. Phases & Milestones

### Phase 1: Foundation (4 weeks)
**Goal**: App starts, loads image, displays it, basic WB/exposure/contrast

**Deliverables**:
- [ ] CMake build system (3 platforms)
- [ ] Qt6/QML skeleton (sidebar, image view, settings stub)
- [ ] Import module (JPEG, PNG, basic RAW)
- [ ] Color management (LCMS2: sRGB, ProPhoto, monitor profile detection)
- [ ] Pipeline: WB + Exposure + Contrast only
- [ ] Before/After split view
- [ ] GPU detection (CUDA/DirectML/Metal)
- [ ] Unit tests for all modules

**Test**: Open a JPEG → adjust exposure → see result. App runs on all 3 platforms.

### Phase 2: AI Core (3 weeks)
**Goal**: AI denoise, deblur working. Model download. Basic auto-enhance.

**Deliverables**:
- [ ] ONNX Runtime integration (session management, EP fallback)
- [ ] Model download manager (manifest, lazy-load, progress)
- [ ] NAFNet denoise integration
- [ ] Restormer-light multi-task integration
- [ ] Auto-enhance button (Mittel intensity)
- [ ] 3 intensity levels (Leicht/Mittel/Stark)
- [ ] Processing with tiling for large images
- [ ] Progress UI (per-stage progress bar)

**Test**: Open noisy JPEG → click Auto-Enhance → see denoised result. Models download on first use.

### Phase 3: Lens + Color (3 weeks)
**Goal**: Lens correction, full color pipeline, RAW support, log profiles

**Deliverables**:
- [ ] Lensfun integration (auto-detect, distortion/TCA/vignetting)
- [ ] Full RAW support (LibRaw: CR2/CR3, NEF, ARW, RAF, RW2, ORF, DNG)
- [ ] Log profile detection + inverse curves (D-Log M, S-Log3, C-Log3, etc.)
- [ ] Full color pipeline (saturation, vibrance, hue, split toning)
- [ ] Histogram panel (RGB + luminance)
- [ ] Sharpening (USM: amount/radius/threshold)
- [ ] Highlight recovery modes
- [ ] Skin tone protection

**Test**: Open DJI Air 3S RAW in D-Log M → auto-enhance → correct colors + lens correction.

### Phase 4: Style Learning (3 weeks)
**Goal**: Full learning system with 3 modes, 2-round learning, knowledge base

**Deliverables**:
- [ ] AiLUT-Transform integration (style transfer)
- [ ] Ask mode (👍👎 UI, feedback recording)
- [ ] Smart-Learn mode (edit recording, background learning)
- [ ] Turbo mode (folder → export, no interaction)
- [ ] 2-round learning (R1: 0-60, R2: 61-120, 120+ autonomous)
- [ ] Auto-retrain on new scene types
- [ ] Knowledge base (SQLite + 3-stage compaction)
- [ ] Style profiles (create, rename, delete, export/import)
- [ ] Style reset
- [ ] EfficientNet-Lite0 scene classification

**Test**: Process 60 images in Ask mode → round 1 complete notification. Process 60+ more → round 2. After 120 → autonomous mode works.

### Phase 5: Video (4 weeks)
**Goal**: Full video processing with frame consistency, scene detection

**Deliverables**:
- [ ] FFmpeg integration (HW decode/encode)
- [ ] Video player UI (timeline, scrub, frame preview)
- [ ] Frame-by-frame pipeline (same as images)
- [ ] Scene detection (histogram difference)
- [ ] Temporal smoothing (EMA on chroma)
- [ ] Skin tone protection per frame
- [ ] Video export (H.264/HEVC, match input quality)
- [ ] Audio passthrough
- [ ] Progress UI (frame counter, ETA)

**Test**: Open DJI Air 3S video (D-Log M, 10-bit HEVC) → autoenhance → export. Colors consistent across all frames.

### Phase 6: Polish + Ship (3 weeks)
**Goal**: Export profiles, watermarks, undo/redo, first-start wizard, packaging

**Deliverables**:
- [ ] Export profiles (Instagram, YouTube, Print, Web, Drone, Archiv)
- [ ] Watermark support (text + image)
- [ ] Undo/Redo (Ctrl+Z/Y, parameter stack)
- [ ] Batch processing (folder → export)
- [ ] Drag & Drop (files, folders, .flipsicolor)
- [ ] EXIF handling (preserve/remove GPS)
- [ ] First-start wizard (4-step tour)
- [ ] Keyboard shortcuts (professional shortcuts)
- [ ] Auto-update for KI models (check manifest on startup)
- [ ] Cross-platform installer packaging
- [ ] Documentation (help, shortcuts reference)
- [ ] Performance optimization (profiling, benchmarking)
- [ ] Optional: Real_HAT_GAN upscale best quality + Real-ESRGAN fast (lazy-load)
- [ ] Optional: CodeFormer face restoration (lazy-load, adjustable fidelity weight)

**Test**: Full workflow: import folder → auto-enhance with style → batch export → verify output.

---

## 11. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| ONNX model compatibility issues (different GPU backends) | Medium | High | Test on all 3 backends early; keep CPU fallback; use ONNX opset 14+ |
| Video processing too slow for real-time preview | High | Medium | Process at lower resolution for preview; background processing; progress bar |
| Lensfun DB missing user's camera/lens | Medium | Low | Allow manual distortion adjustment; community profile submission |
| Memory issues with large RAW files (100MP+) | Medium | High | Tiled processing; memory limit; warn before loading huge files |
| AiLUT-Transform training instability | Medium | Medium | Use proven hyperparameters; limit training data diversity; reset option |
| Apple Metal EP not available in ONNX Runtime | Medium | High | Use CoreML EP instead; fall back to CPU; test on real Apple Silicon |
| FFmpeg build complexity for static linking | High | Low | Use shared libraries (.bundle/.dll); vcpkg for dependency management |
| Qt6 licensing (LGPLv3 compliance) | Low | Medium | Dynamic linking; proper attribution; Qt commercial license option |
| Model download server unavailable | Medium | Medium | Bundle core models (~51MB) in installer; CDN with fallback; retry logic |
| Windows DirectML driver issues | Medium | Medium | Detect DirectX 12 support; fall back to CUDA or CPU |

---

## 12. Testing Strategy

### Unit Tests (per module)
- **Import**: Test all supported formats, EXIF extraction accuracy, log profile detection
- **Color**: Test color space conversions against known values (Delta E < 1.0)
- **Lens**: Test known camera/lens corrections against reference images
- **AI**: Test model inference against pre-computed outputs (PSNR > 35dB)
- **Style**: Test feedback recording, knowledge base compaction, style reset
- **Video**: Test scene detection, temporal smoothing, codec roundtrip
- **Export**: Test all formats, EXIF preservation, watermark placement

### Integration Tests
- End-to-end: Import → Process → Export (every supported format)
- Style learning: 60 images → round 1 complete → verify feedback count
- Video: 30s clip → process → verify frame consistency (SSIM > 0.98 between adjacent frames)
- GPU fallback: Test CUDA → DML → CPU fallback chain
- Cross-platform: Same test images produce identical results on Win/Mac/Linux

### Performance Benchmarks
- Image pipeline: <100ms for 24MP JPEG on RTX 3060 (core pipeline)
- Video pipeline: >10 FPS for 1080p30 on RTX 3060
- First-start to first-image: <30 seconds (including model download)
- Memory: <4GB RAM for single 24MP image processing
- Memory: <8GB RAM for 1080p video processing (streaming, not all-in-memory)

### GPU Test Matrix
| GPU | Backend | Expected Image Speed | Expected Video Speed |
|---|---|---|---|
| RTX 3060 12GB | CUDA | ~80ms | ~15 FPS |
| RTX 4070 | CUDA | ~40ms | ~30 FPS |
| RX 6600 XT | DirectML | ~120ms | ~10 FPS |
| RX 7800 XT | DirectML | ~70ms | ~18 FPS |
| Apple M1 | CoreML/Metal | ~100ms | ~12 FPS |
| Apple M3 Pro | CoreML/Metal | ~50ms | ~25 FPS |
| Intel UHD 630 | CPU only | ~2000ms | ~0.5 FPS |

---

*End of FlipsiColor SDP v1.0 — Generated 2026-05-24*