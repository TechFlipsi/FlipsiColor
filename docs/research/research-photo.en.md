# FlipsiColor — Research: Photo Processing Pipeline

> **Note:** German original at [research-photo.md](research-photo.md). This is the English reference summary.
> 🚧 Full translation in progress.

## Key Findings

- RAW pipeline via LibRaw → 14-step processing chain
- Working color space: ProPhoto RGB (prevents clipping during editing)
- 14 log profiles with transfer curves for professional color grading
- Lensfun integration for 500+ camera/lens distortion profiles
- Output pipeline: Working → Display/Output color space via LCMS2

See: [research-photo.md](research-photo.md)