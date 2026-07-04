# Contributing to FlipsiColor

Vielen Dank für dein Interesse, an FlipsiColor mitzuwirken! 🎨

FlipsiColor ist ein Open-Source-Projekt für **KI-gestützte Bild- & Videofarbkorrektur** und wird unter der **GPL-3.0-Lizenz** entwickelt. Jeder ist herzlich eingeladen, beizutragen.

## 🚀 Schnellstart

1. **Repo forken** — Klicke auf "Fork" oben rechts auf GitHub
2. **Lokal klonen** — `git clone https://github.com/<dein-username>/FlipsiColor.git`
3. **Branch erstellen** — `git checkout -b feature/dein-feature-name`
4. **Build testen** — Siehe unten (Build-Anleitung)
5. **Änderungen committen** — Klare Commit-Messages (`feat: ...`, `fix: ...`, `docs: ...`)
6. **Push & PR** — `git push origin feature/dein-feature-name` → Pull Request aufstellen

## 📋 Was wir suchen

| Bereich | Beschreibung |
|---------|--------------|
| 🐛 **Bug Reports** | Gefundene Bugs als Issue melden (mit reproduzierbaren Schritten) |
| 🎨 **UI/Design** | Dark/Light Theme Verbesserungen, neue UI-Elemente |
| 🧠 **KI-Modelle** | Neue ONNX-Modelle integrieren, bestehende verbessern |
| 🎬 **Video-Processing** | VapourSynth-Integration, FFmpeg-Pipeline, Encoding |
| 📸 **RAW-Verarbeitung** | LibRaw Integration, neue Kamera-Formate |
| 🐧 **Linux** | Avalonia-UI, Cross-Platform-Kompatibilität |
| 🌍 **Übersetzung** | UI-Lokalisierung (Deutsch/Englisch, weitere Sprachen willkommen) |
| 📦 **Packaging** | Installer (Windows MSI/Inno Setup, Linux .deb/.AppImage) |
| 📝 **Dokumentation** | README, Code-Kommentare, Anleitungen |

## 🏗️ Build

### Voraussetzungen
- **.NET 10 SDK** ([Download](https://dotnet.microsoft.com/download))
- **Git**
- Für Windows-Build: Windows 10/11 mit Visual Studio 2022 oder Build Tools
- Für Linux-Build: Linux mit .NET 10 SDK

### Build (Windows — WPF)
```bash
dotnet build FlipsiColor.sln -c Release
dotnet publish FlipsiColor/FlipsiColor.csproj -c Release -r win-x64 --self-contained true -o publish/win
```

### Build (Linux — Avalonia)
```bash
dotnet build FlipsiColor.sln -c Release
dotnet publish FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj -c Release -r linux-x64 --self-contained true -o publish/linux
```

### Build-Regeln
- ⚠️ **`TreatWarningsAsErrors=true`** — Der Build MUSS mit **0 Fehlern und 0 Warnungen** durchlaufen
- ⚠️ **Keine `// TODO`**, `throw new NotImplementedException()` oder Platzhalter im finalen Code
- ⚠️ **Keine Magic Numbers** — Konstanten verwenden
- ⚠️ **UTF-8 überall** — Umlaute (äöüß) korrekt in Code, Kommentaren und UI

## 📐 Code-Standards

### Sprache
- **Code-Kommentare & Dokumentation:** Deutsch (mit korrekten Umlauten)
- **UI-Texte:** Über Resource-Dateien (`Resources.de.resx`, `Resources.en.resx`) — NICHT hartcodiert
- **Commit-Messages:** Englisch (`feat: add VapourSynth backend`, `fix: dark mode combo box`)

### Naming
- **Public:** PascalCase (`public void ProcessImage()`)
- **Private:** _camelCase (`private readonly string _modelPath;`)
- **Konstanten:** PascalCase (`public const int MaxBatchSize = 100;`)
- **Namespaces:** `FlipsiColor.Core` (Business-Logik), `FlipsiColor` (UI)

### Architektur
- **`FlipsiColor.Core`** — Plattformunabhängige Business-Logik (KI, Bild, Video, Color)
- **`FlipsiColor`** — WPF UI (Windows-spezifisch, referenziert Core)
- **`FlipsiColor.Avalonia`** — Avalonia UI (Cross-Platform, referenziert Core)
- ⚠️ **KEINE Code-Dopplung** — Business-Logik gehört NUR in `FlipsiColor.Core`, nicht in UI-Projekte kopieren
- ⚠️ **DRY-Prinzip** — Don't Repeat Yourself

### Design
- **Accent-Farbe:** Blau (`#2563EB` primär, `#3B82F6` sekundär) — NIEMALS Lila/Violet
- **Dark Mode:** ALLE Controls müssen lesbar sein (Kontrast prüfen!)
- **Themes:** DarkTheme.xaml + LightTheme.xaml müssen synchron sein

## 🎬 VapourSynth-Integration

Wir integrieren aktuell VapourSynth als optionales Video-Backend. Wenn du Erfahrung mit VapourSynth hast, schau dir [Issue #2](https://github.com/TechFlipsi/FlipsiColor/issues/2) an!

- VapourSynth MUSS optional sein — Software läuft auch ohne
- FFmpeg bleibt als Standard-Backend erhalten
- Cross-Platform (Windows + Linux)

## 🐛 Bug Reports

Bitte erstelle ein Issue mit:
1. **Was hast du gemacht?** (Schritt für Schritt)
2. **Was ist passiert?** (Fehlermeldung, Screenshot, Log-Datei)
3. **Dein System** (OS, GPU, .NET-Version)
4. **Version** (Welche FlipsiColor-Version?)

## 🔍 Pull Request Review

Dein PR wird akzeptiert wenn:
- ✅ Build läuft (0 Fehler, 0 Warnungen)
- ✅ Code folgt den Standards oben
- ✅ Keine Dopplung mit bestehendem Code
- ✅ UI-Texte sind lokalisiert (nicht hartcodiert)
- ✅ Keine Platzhalter oder TODOs
- ✅ Commit-Messages sind aussagekräftig

## 💬 Kontakt

- **Issues:** [GitHub Issues](https://github.com/TechFlipsi/FlipsiColor/issues)
- **Doom9 Forum:** [VapourSynth Section](https://forum.doom9.org/forumdisplay.php?f=82)

## 📄 Lizenz

Durch das Einreichen eines Beitrags stimmst du zu, dass deine Änderungen unter der gleichen **GPL-3.0-Lizenz** wie das Projekt veröffentlicht werden.

---

*Dieses Projekt wird in Freizeit entwickelt. Geduld und konstruktives Feedback werden geschätzt.* 🙏