# FlipsiColor — Research: Video Processing Pipeline

> **Note:** German original at [research-video.md](research-video.md). This is the English reference summary.
> 🚧 Full translation in progress.

## Key Findings

- Color Grading 3-Stage Pipeline: Technical → Creative → Refinement
- Frame consistency via Reference frame + Histogram matching + EMA temporal smoothing (α=0.5)
- Scene detection via chi-squared threshold (0.3-0.5)
- Skin tone protection HSV range: 0-50°
- Codec support matrix for HW-accelerated encode/decode

See: [research-video.md](research-video.md)