# FlipsiColor v0.4.0 — Master-Auftragsliste

> **Datum:** 04.07.2026
> **Autor:** Sir (Fabian Kirchweger) → entschlüsselt durch J.A.R.V.I.S.
> **Projekt:** TechFlipsi/FlipsiColor — KI-gestützte Bild- & Videofarbkorrektur
> **Repo:** `/root/FlipsiColor/` (GitHub: TechFlipsi/FlipsiColor)
> **Stack:** .NET 10 / C# / WPF (Windows) + Avalonia 12 (Linux) + ONNX Runtime + OpenCV
> **Zielversion:** v0.4.0

---

## PHASE 1: ARCHITEKTUR BEREINIGEN

### 1.1 Code-Dopplung eliminieren
**Problem:** `FlipsiColor/` (WPF) und `FlipsiColor.Core/` enthalten IDENTISCHE Kopien aller Business-Logik (AI/, Color/, Video/, Core/, Image/, Utils/). Das WPF-Projekt referenziert `FlipsiColor.Core` NICHT — es hat eigene Kopien. `FlipsiColor.Avalonia` referenziert Core korrekt via `<ProjectReference>`.

**Lösung:**
- WPF-Projekt (`FlipsiColor/FlipsiColor.csproj`) MUSS `FlipsiColor.Core` als `<ProjectReference>` aufnehmen
- Alle duplizierten Dateien in `FlipsiColor/` LÖSCHEN: `AI/`, `Color/`, `Core/`, `Image/`, `Video/`, `Utils/`, `GPUInfo.cs`, `Converters.cs`
- Nur UI-spezifische Dateien bleiben im WPF-Projekt: `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml`, `App.xaml.cs`, `Themes/`, `UI/MainViewModel.cs`, `UI/FolderPicker.cs`, `UI/ThemeManager.cs`, `Assets/`
- WPF-spezifische UI-Logik (FolderPicker, ThemeManager) bleibt im WPF-Projekt
- Core-Namespaces müssen stimmen: `FlipsiColor.Core` Namespace, WPF UI = `FlipsiColor`

### 1.2 Solution-Datei reparieren
**Problem:** `FlipsiColor.sln` enthält nur `FlipsiColor.Core` und `FlipsiColor.Avalonia` — das WPF-Projekt `FlipsiColor/FlipsiColor.csproj` FEHLT.

**Lösung:**
- WPF-Projekt zur `.sln` hinzufügen
- Alle drei Projekte in der Solution: Core (Shared), WPF (Windows UI), Avalonia (Cross-Platform UI)
- Build-Konfigurationen für alle drei prüfen (Debug|x64, Release|x64)

### 1.3 Versionsnummern synchronisieren
- WPF: v0.3.0 → v0.4.0
- Core: bereits v0.4.0
- Avalonia: bereits v0.4.0
- Installer: v0.2.1 → v0.4.0
- GitHub Actions: Versions-Extraktion auf neues csproj anpassen

---

## PHASE 2: DESIGN-ÜBERHOLUNG

### 2.1 Lila-Farben entfernen (BEIDE Themes)
**Problem:** `AccentPrimary=#7C3AED` und `AccentSecondary=#A78BFA` sind LILA/Violet — in beiden Themes. Sir will BLAU oder GRÜN, NIEMALS LILA.

**Lösung — Blau als neuer Accent:**
- `AccentPrimary`: `#7C3AED` → `#2563EB` (Blau)
- `AccentSecondary`: `#A78BFA` → `#3B82F6` (helleres Blau)
- Light Theme `AccentSecondary`: `#6D28D9` → `#1D4ED8` (dunkleres Blau)
- Beide Themes (`DarkTheme.xaml`, `LightTheme.xaml`) aktualisieren
- Avalonia Styles (falls vorhanden) synchronisieren

### 2.2 Dark Mode Lesbarkeit fixen
**Problem:** Im Dark Mode sind Auswahlfelder (ComboBox, ListBox, Dropdowns) UNSICHTBAR — Text nicht lesbar, Hintergrund nicht kontrastiert. Light Theme funktioniert, Dark nicht.

**Lösung:**
- ComboBox/ListBox/Dropdown Styles im DarkTheme.xaml prüfen und reparieren
- Sicherstellen: Text-Farbe = `TextPrimary` (#E0E0E0), Hintergrund = `BgSecondary`/`BgTertiary`
- SelectedItem-Hintergrund = AccentPrimary (Blau), SelectedItem-Text = White
- Hover-Effekte sichtbar
- DropDown-Popup Hintergrund = BgSecondary, Border = BorderPrimary
- ALLE WPF-Controls testen: ComboBox, ListBox, CheckBox, RadioButton, TabControl, Slider, ProgressBar

### 2.3 Software optisch verbessern
- Modernere Button-Styles (Rundung, Hover-Animation, Disabled-State)
- Tab-Control ansprechender gestalten
- Fortschrittsbalken/Loading-Indicators
- Einheitliche Abstände/Padding
- Avalonia-Styles synchronisieren (FlipsiColor.Avalonia/Styles/)

---

## PHASE 3: LOKALISIERUNG (DEUTSCH/ENGLISCH)

### 3.1 Vollständige Übersetzung
**Problem:** UI hat englische Begriffe obwohl Software deutsch sein soll ("Color Calibration", "DJI Auto-Merge", etc.). Settings.cs hat `Sprache = "de"` aber keine echte i18n-Implementierung.

**Lösung:**
- Resource-Dateien erstellen: `Resources.de.resx` (Deutsch), `Resources.en.resx` (Englisch)
- ALLE UI-Texte in Resource-Dateien auslagern:
  - Tab-Beschriftungen
  - Button-Texte
  - Label-Texte
  - Tooltips
  - Fehlermeldungen
  - Statusmeldungen
  - Dialog-Texte
- Bei Sprache=de: KEIN englisches Wort im UI
- Bei Sprache=en: KEIN deutsches Wort im UI
- Avalonia: `Lokalisierung.cs` bereits vorhanden — synchronisieren
- MainWindow.xaml: Alle hartcodierten Texte durch `{x:Static}` oder Binding ersetzen

### 3.2 Einstellungsseite mit Spracheinstellung
**Neu:** Einstellungs-Tab oder -Dialog mit:
- **Sprache:** Deutsch / Englisch (mit Live-Umschaltung — UI aktualisiert sich sofort)
- **Theme:** Dark / Light / System
- **GPU-Auswahl** (falls mehrere GPUs vorhanden)
- **Model-Pfad** (wo ONNX-Modelle gespeichert werden — konfigurierbar)
- **Video-Backend:** FFmpeg / VapourSynth (siehe Phase 6)
- Einstellungen in Settings.cs persistieren (JSON-Datei)

---

## PHASE 4: DRAG & DROP + DATEIAUSWAHL FIXEN

### 4.1 Drag & Drop reparieren (KRITISCHER BUG)
**Problem:** Material reinziehen funktioniert NICHT — UI reagiert nicht, keine Liste, keine Anzeige dass etwas drin ist. Keine visuelle Rückmeldung.

**Lösung:**
- `AllowDrop="True"` auf dem Hauptbereich/Panel setzen
- `DragEnter`, `DragOver`, `Drop` Event-Handler implementieren
- Drag-Over: visuelles Feedback (Highlight, Drop-Zone-Animation)
- Drop: Dateien empfangen, validieren, zur Dateiliste hinzufügen
- Unterstützte Formate: Bilder (.jpg, .png, .tiff, .bmp, .raw, .cr2, .cr3, .nef, .arw, .dng, .orf, .rw2), Videos (.mp4, .mov, .avi, .mkv)
- Ordner-Drop: rekursiv alle unterstützten Dateien laden
- Dateiliste im UI anzeigen: Dateiname, Typ, Größe, Remove-Button pro Eintrag
- Liste leeren-Button
- Bei Avalonia: `DragDrop.AllowDrop` mit Avalonia-Drop-Event-System

### 4.2 Ordner auswählen reparieren
**Problem:** Ordner über FolderPicker auswählen → passiert nichts, keine Dateien laden, keine Reaktion im UI.

**Lösung:**
- FolderPicker öffnen, Ordner auswählen → alle unterstützten Dateien rekursiv laden
- Gleiche Dateiliste wie bei Drag & Drop
- Loading-Indicator während des Scannens
- Fehlermeldung wenn Ordner leer oder keine unterstützten Dateien

### 4.3 Dateiliste im UI
- ListView/ListBox mit allen geladenen Dateien
- Spalten: Dateiname, Typ (Bild/Video), Auflösung, Größe
- Kontextmenü: Entfernen, Alle entfernen
- Multi-Select für Batch-Operation
- Sortierbar (Name, Typ, Größe)

---

## PHASE 5: DJI-SPEZIFISCHE NAMEN ENTFERNEN

### 5.1 Umbenennungen
**Problem:** Software ist für ALLE Kameras, nicht nur DJI. DJI-spezifische Namen müssen weg.

| Aktuell | Neu |
|---------|-----|
| `DjiAutoMerge.cs` | `AutoMerge.cs` |
| Tab "DJI Merge" | Tab "Auto-Merge" (de) / "Auto-Merge" (en) |
| "DJI Auto-Merge" Beschreibungen | Generische Beschreibungen für alle Kameras |
| README "DJI" Referenzen | "für alle Kameras" |
| DJI Osmo/Pocket 4 Erwähnungen | "verschiedene Kamera-Modelle" |

### 5.2 Funktionalität bleibt gleich
- Auto-Merge Feature funktioniert weiterhin für DJI-Geräte
- Aber auch für andere Kameras (GoPro, Insta360, etc.)
- Keine DJI-spezifische Logik im Code — nur generische Clip-Erkennung + Zusammenführung

---

## PHASE 6: VAPOURSYNTH-INTEGRATION (GitHub Issue #2)

### 6.1 VapourSynth als optionales Video-Backend
- VapourSynth als alternative Video-Processing-Pipeline einbauen
- FFmpeg bleibt Standard-Backend
- Einstellung in Settings: "Video-Backend: FFmpeg / VapourSynth"
- VapourSynth MUSS optional sein — Software funktioniert auch ohne installiertes VapourSynth
- Bei Auswahl prüfen: Ist VapourSynth installiert? Wenn nicht → Hinweis + FFmpeg als Fallback
- VapourSynth-Integration für Frame-Level-Processing (Filter-Pipelines, Color Correction auf Frame-Ebene)

### 6.2 VapourSynth-Engine
- Neue Klasse `VapourSynthProcessor.cs` in `FlipsiColor.Core/Video/`
- VapourSynth Python-Scripts generieren (für Filter-Pipelines)
- VapourSynth-Output an FFmpeg zur Encoding-Pipeline weitergeben
- Cross-Platform: VapourSynth funktioniert auf Windows und Linux

---

## PHASE 7: VOLLSTÄNDIGER CODE-DURCHGANG

### 7.1 Bug-Hunting (alle .cs und .xaml Dateien)
Systematische Suche nach:

- **NullReferenceExceptions** — alle `.`-Zugriffe auf potentiell null Objekte prüfen
- **Async/Await Anti-Patterns** — `.Result`, `.Wait()`, `async void` (außer Event-Handler)
- **Memory Leaks** — Event-Handler nicht abgemeldet, `IDisposable` nicht disposed
- **Path Traversal** — `SecurityValidator.cs` prüfen, alle Dateipfade validieren
- **UI-Thread-Blockaden** — `Dispatcher.Invoke` für UI-Updates, keine CPU-bound Arbeit auf UI-Thread
- **Resource Leaks** — `FileStream`, `InferenceSession`, `Mat` (OpenCV) — alle mit `using` oder `Dispose()`
- **Exception Swallowing** — leere `catch`-Blöcke finden und beheben
- **Thread-Safety** — shared State ohne Locks, `ObservableCollection` aus Background-Thread
- **Race Conditions** — async Operations die UI gleichzeitig aktualisieren
- **Integer Overflow** — bei Bild-Dimensionen, Buffer-Größen
- **Encoding Issues** — Umlaute in Dateinamen, UTF-8 überall

### 7.2 Code-Qualität
- `TreatWarningsAsErrors=true` ist aktiv — MUSS 0 Fehler, 0 Warnungen bleiben
- Alle Methoden haben XML-Dokumentation (summary, params, returns)
- Konsistente Namenskonventionen (PascalCase für public, _camelCase für private)
- Keine Magic Numbers — Konstanten oder Config-Werte
- Keine TODO/HACK/FIXME Kommentare im finalen Code

### 7.3 Skills laden und anwenden
Der Sub-Agent MUSS folgende Skills laden und anwenden:
- `frontend-ui-engineering` — für UI/Design-Arbeit
- `code-review-and-quality` — für Code-Qualitäts-Check
- `software-security` — für Security-Audit
- `cross-platform-error-prevention` — für .NET/Linux/Windows Fallen
- `build-systems-cicd-2026` — für Build/CI/Installer
- `test-driven-development` — für Tests
- `code-simplification` — für Refactoring
- `debugging-and-error-recovery` — für Bug-Hunting
- `error-learning` — für bekannte Fehler-Pattern

---

## PHASE 8: LINUX-MIGRATION (AVALONIA)

### 8.1 Avalonia auf Funktionsstand bringen
**Status:** `FlipsiColor.Avalonia` existiert mit Grundgerüst (7 .cs-Dateien, Version 0.4.0). Muss auf gleichen Funktionsstand wie WPF-Version.

**Aufgaben:**
- MainViewModel synchronisieren — alle Features die WPF hat auch in Avalonia
- Views/MainWindow.axaml — komplettes UI nachbauen (Tabs, Panels, Controls)
- Drag & Drop in Avalonia (anderes API als WPF)
- FolderPicker in Avalonia (`StorageProvider.PickFolderAsync()`)
- ThemeManager für Avalonia (`ThemeManager.cs` bereits vorhanden — erweitern)
- Lokalisierung (`Lokalisierung.cs` bereits vorhanden — mit neuen Resources synchronisieren)
- Styles/ Ordner — Themes für Avalonia (Dark/Light, Blau statt Lila)

### 8.2 Platform-Unterschiede beachten
- **ONNX Runtime:** DirectML nur Windows → Linux braucht CPU oder OpenCL
  - `Microsoft.ML.OnnxRuntime` (CPU) als Fallback auf Linux
  - Zur Laufzeit prüfen: `OperatingSystem.IsWindows()` → DirectML, sonst CPU
- **LibRaw:** Native lib `libraw.so` auf Linux (apt install libraw-dev)
- **FFmpeg:** Auf Linux via `apt install ffmpeg`
- **VapourSynth:** Auf Linux via `pip install vapoursynth`
- **Dateipfade:** Linux `/home/user/` vs Windows `C:\Users\`
- **OpenCvSharp4:** `runtime.linux-x64` Package bereits im Core-csproj referenziert
- **GPU-Erkennung:** `System.Management` (WMI) funktioniert nur Windows → Linux Alternative (lspci, /proc)

### 8.3 Linux testen
- `dotnet build FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj -c Release` MUSS funktionieren
- `dotnet run --project FlipsiColor.Avalonia` starten (headless mit Xvfb falls nötig)
- KI-Modelle laden/testen auf Linux
- ONNX Runtime CPU-Mode verifizieren
- FFmpeg-Aufruf auf Linux testen
- Datei-Dialoge (FolderPicker) auf Linux testen
- VapourSynth-Verfügbarkeit auf Linux prüfen

---

## PHASE 9: INSTALLER + GITHUB RELEASES

### 9.1 Windows Installer aktualisieren
**Status:** `installer/installer.iss` (Inno Setup) existiert, Version 0.2.1 — veraltet.

**Lösung:**
- Version auf 0.4.0 aktualisieren
- Source-Pfad auf neues Build-Output anpassen (nach Architektur-Bereinigung)
- VC++ Redistributable Download beibehalten
- Installer-Sprachen: Deutsch + Englisch (bereits vorhanden)
- Desktop-Verknüpfung + Start-Menü Eintrag

### 9.2 Linux Installer (NEU)
- `.deb` Package für Ubuntu/Debian (dpkg-buildpackage oder dotnet-deb)
- `.AppImage` für universelle Linux-Distributionen (AppImageTool)
- Abhängigkeiten im Package: `ffmpeg`, `libraw-dev` (als Depends im .deb)
- Desktop-Integration (.desktop File, Icon)

### 9.3 GitHub Actions Workflow aktualisieren
**Status:** Aktueller Workflow baut nur WPF (win-x64). Muss erweitert werden.

**Neuer Workflow:**
- **Job 1: Build Core** — `dotnet build FlipsiColor.Core` (plattformunabhängig validieren)
- **Job 2: Build Windows** — `dotnet publish FlipsiColor/FlipsiColor.csproj -c Release -r win-x64 --self-contained true`
  - Inno Setup Installer bauen
  - Artifact: `FlipsiColor-0.4.0-setup.exe`
- **Job 3: Build Linux** — `dotnet publish FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj -c Release -r linux-x64 --self-contained true`
  - .deb Package bauen
  - .AppImage bauen
  - Artifacts: `FlipsiColor-0.4.0-linux.deb`, `FlipsiColor-0.4.0.AppImage`
- **Job 4: Release** — Bei Tag-Push: GitHub Release mit allen drei Installern
- ⚠️ LOKAL validieren VOR Push — `dotnet build` MUSS funktionieren bevor GitHub Actions getriggert wird
- ⚠️ Keine Trial-and-Error CI-Runs — GitHub Actions Regel!

---

## PHASE 10: KI-MODELLE TESTEN

### 10.1 ONNX-Modell-Ladevorgang prüfen
- Alle 7 ONNX-Modelle durchgehen:
  1. NAFNet (Bild-Restauration)
  2. RestormerLight (Bild-Restauration)
  3. RealHATGAN (Bild-Verbesserung)
  4. RealESRGAN (Hochskalierung)
  5. CodeFormer (Gesicht-Restauration)
  6. AiLUTTransform (Farb-LUT)
  7. EfficientNet (Klassifikation/KI-Vorschläge)
- Prüfen: Werden Modelle korrekt geladen?
- Prüfen: Werden Modelle heruntergeladen falls nicht vorhanden? (ModelDownloader.cs)
- Prüfen: Gibt es Fehlermeldungen die verschluckt werden? (leere catch-Blöcke)
- Prüfen: Model-Pfad konfigurierbar? (in Einstellungen)

### 10.2 GPU vs CPU Mode
- Windows: DirectML GPU-Mode testen
- Linux: CPU-Mode testen (DirectML nicht verfügbar)
- Fallback-Logik: GPU nicht verfügbar → CPU automatisch
- Fehlermeldung wenn keine GPU und CPU zu langsam

### 10.3 Modell-Download-Workflow
- ModelDownloader.cs: Download-Pfade prüfen
- Download-Fortschritt im UI anzeigen
- Download-Fehler behandeln (Netzwerk-Ausfall, korrupte Datei)
- Modelle bei Bedarf neu herunterladen können (Einstellungen: "Modelle zurücksetzen")

---

## ABSCHLUSS-KONTROLLE

### Vor "Fertig" MUSS alles geprüft werden:

- [ ] Build: `dotnet build FlipsiColor.sln` — 0 Fehler, 0 Warnungen
- [ ] Build WPF: `dotnet publish FlipsiColor/FlipsiColor.csproj -c Release -r win-x64 --self-contained true`
- [ ] Build Avalonia: `dotnet publish FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj -c Release -r linux-x64 --self-contained true`
- [ ] Build Avalonia Windows: `dotnet publish FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj -c Release -r win-x64 --self-contained true`
- [ ] KEINE Code-Dopplung: WPF referenziert Core, keine kopierten Dateien
- [ ] KEINE Lila-Farben: `grep -ri "7C3AED\|A78BFA\|6D28D9\|purple\|violet" Themes/` → leer
- [ ] Dark Mode: ComboBox/DropDown/Auswahlfelder lesbar
- [ ] Deutsch bei Sprache=de: KEIN englisches Wort im UI
- [ ] Englisch bei Sprache=en: KEIN deutsches Wort im UI
- [ ] Drag & Drop: Dateien reinziehen → Liste zeigt Dateien
- [ ] Ordner auswählen → Dateien laden
- [ ] KEINE DJI-Namen: `grep -ri "DJI\|Dji\|dji\|Osmo\|Pocket.4" --include="*.cs" --include="*.xaml"` → leer (außer README historisch)
- [ ] VapourSynth: Optional, Software läuft auch ohne
- [ ] Einstellungsseite: Sprache, Theme, GPU, Model-Pfad, Video-Backend
- [ ] Installer Windows: `.exe` baut
- [ ] Installer Linux: `.deb` + `.AppImage` bauen
- [ ] GitHub Actions: Workflow für beide Plattformen
- [ ] KI-Modelle: Ladevorgang, Download, GPU/CPU Mode getestet
- [ ] Linux: `dotnet build` und `dotnet run` funktioniert
- [ ] KEINE Platzhalter: Keine `// TODO`, keine `throw new NotImplementedException()`, keine leeren Methoden
- [ ] KEINE doppelten Code-Blöcke an verschiedenen Stellen
- [ ] ALLE Phasen 1-10 abgearbeitet — nichts übersprungen

### Selbst-Verifikation
- Jede geänderte Datei nochmal durchlesen
- Build nach JEDEM Phase-Abschluss prüfen (nicht nur am Ende)
- Wenn etwas nicht funktioniert → reparieren, nicht überspringen
- Wenn ein Feature nicht testbar ist (z.B. VapourSynth nicht installiert) → Code schreiben, Build prüfen, mit Kommentar dokumentieren was zu testen ist
- Nicht aufhören bis alles funktioniert oder ein echter Blocker vorliegt (dann Sir informieren)

---

## REGELN

- ⚠️ NULL FEHLER, NULL WARNUNGEN im Build
- ⚠️ Blau oder Grün als Accent — NIEMALS Lila (#7C3AED etc.)
- ⚠️ Bei Sprache=de: KEIN englisches Wort im UI — bei Sprache=en: KEIN deutsches Wort
- ⚠️ Software ist für ALLE Kameras — keine DJI-spezifischen Namen im Code
- ⚠️ Build lokal validieren VOR GitHub Push — keine Trial-and-Error CI-Runs
- ⚠️ Alle verfügbaren Skills laden und anwenden
- ⚠️ Nicht aufhören bis alles funktioniert oder echter Blocker
- ⚠️ Keine Platzhalter, keine TODOs, keine leeren Methoden im finalen Code
- ⚠️ Keine doppelten Code-Blöcke an verschiedenen Stellen — DRY-Prinzip
- ⚠️ Jede Phase abschließen und Build prüfen vor nächster Phase
- ⚠️ Selbständig suchen, finden, testen, verifizieren — nicht fragen, machen