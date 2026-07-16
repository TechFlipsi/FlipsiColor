# FlipsiColor Translations

This directory contains reference templates for FlipsiColor's JSON-based localization system.

## Available Languages

| Code | Language | Status |
|------|----------|--------|
| `de` | Deutsch | ✅ Complete (original) |
| `en` | English | ✅ Complete (original) |
| `it` | Italiano | ✅ Complete (by MarcoRavich, PR #11) |
| `es` | Español | ⚠️ Placeholder (English copy — needs translation) |
| `fr` | Français | ⚠️ Placeholder (English copy — needs translation) |
| `pt` | Português | ⚠️ Placeholder (English copy — needs translation) |
| `nl` | Nederlands | ⚠️ Placeholder (English copy — needs translation) |
| `pl` | Polski | ⚠️ Placeholder (English copy — needs translation) |
| `tr` | Türkçe | ⚠️ Placeholder (English copy — needs translation) |
| `ru` | Русский | ⚠️ Placeholder (English copy — needs translation) |
| `zh` | 中文 | ⚠️ Placeholder (English copy — needs translation) |
| `ja` | 日本語 | ⚠️ Placeholder (English copy — needs translation) |
| `ko` | 한국어 | ⚠️ Placeholder (English copy — needs translation) |

## How to Add a New Language

### Option A: Translate an existing placeholder

1. Pick a placeholder file (e.g. `es.json` for Spanish)
2. Translate all **values** (right side of the `:`) to your language
3. **Do NOT change the keys** (left side of the `:`)
4. Submit a Pull Request

### Option B: Add a completely new language

1. Copy `template.json` to `xx.json` (where `xx` is your language code, e.g. `sv.json` for Swedish)
2. Translate all values to your language
3. Add a `<ComboBoxItem Content="XX"/>` in:
   - `FlipsiColor/MainWindow.xaml` (WPF)
   - `FlipsiColor.Avalonia/Views/MainWindow.axaml` (Avalonia)
4. Add your language code to the `sprachen` array in `SpracheAendern()` in:
   - `FlipsiColor/UI/MainViewModel.cs` (WPF)
   - `FlipsiColor.Avalonia/ViewModels/MainViewModel.cs` (Avalonia)
5. Add your language code to the `sprachenListe` array in the constructor of both MainViewModel files
6. Submit a Pull Request

## File Locations in the Codebase

The actual translation files live in two places (WPF and Avalonia have separate UIs):

```
FlipsiColor/Assets/i18n/              ← WPF version
FlipsiColor.Avalonia/Assets/i18n/     ← Avalonia version
```

Both directories contain the same set of languages. If you translate a language, please update **both** files.

## Translation Rules

- **Keys** (left side) are always in English naming convention (e.g. `"Status.Geladen"`) — do NOT translate them
- **Values** (right side) are the actual UI text — translate these
- Keep placeholders like `{0}` intact (e.g. `"{0} file(s) loaded"`)
- Keep technical terms unchanged: FlipsiColor, CodeFormer, RealESRGAN, FFmpeg, VapourSynth, Lensfun, OCIO, LUT, GPU, AI/KI
- The app automatically falls back to English if a key is missing in your translation
- The app automatically detects the system language on first launch

## Example

### `en.json` (English — source):
```json
{
  "App.Titel": "FlipsiColor v0.6.0",
  "Toolbar.Bild": "Image",
  "Status.Geladen": "Loaded"
}
```

### `it.json` (Italian — by MarcoRavich):
```json
{
  "App.Titel": "FlipsiColor v0.6.0",
  "Toolbar.Bild": "Immagine",
  "Status.Geladen": "Caricato"
}
```

### `de.json` (German — original):
```json
{
  "App.Titel": "FlipsiColor v0.6.0",
  "Toolbar.Bild": "Bild",
  "Status.Geladen": "Geladen"
}
```

## Questions?

Open an [Issue](https://github.com/TechFlipsi/FlipsiColor/issues) or join our [Discord](https://discord.gg/zHPhQ7EaqH).