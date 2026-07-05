# FlipsiColor v0.4.2 — VapourSynth Integration als optionales Video-Backend

## Vision

FlipsiColor nutzt aktuell FFmpeg als einziges Video-Backend (Frame-für-Frame als PNG extrahieren → verarbeiten → neu kodieren). VapourSynth existiert als Code-Gerüst (VapourSynthProcessor.cs) ist aber nicht an die UI angebunden und nutzt keine KI-Modelle.

**Ziel:** VapourSynth als vollwertiges optionales Video-Backend mit automatischem Download + Installation. FFmpeg bleibt Standard. Der Nutzer kann in den Einstellungen mit einem Klick auf VapourSynth wechseln — die Software lädt alles Nötige selbstständig herunter und installiert es portabel. Beide Backends nutzen die KI-Modelle (NAFNet, RestormerLight, CodeFormer, AiLUTTransform, RealESRGAN, EfficientNet).

## Verifizierte Download-Links (alle HTTP 200 geprüft am 05.07.2026)

### Windows (Portable — kein Admin, keine Registry, kein Python)
| Komponente | URL | Größe |
|-----------|-----|-------|
| VapourSynth R76 Portable | `https://github.com/vapoursynth/vapoursynth/releases/download/R76/VapourSynth64-Portable-R76.zip` | 14.3 MB |
| ffms2 5.0 (Video Source) | `https://github.com/FFMS/ffms2/releases/download/5.0/ffms2-5.0-msvc.7z` | 8.2 MB |
| vs-mlrt ONNX Runtime CPU | `https://github.com/AmusementClub/vs-mlrt/releases/download/v15.16/VSORT-Windows-x64.v15.16.7z` | 56.4 MB |
| vs-mlrt Scripts | `https://github.com/AmusementClub/vs-mlrt/releases/download/v15.16/scripts.v15.16.7z` | <1 MB |

**Windows Gesamt: ~79 MB Download**

### Linux (via pip — Python 3.12+ erforderlich)
```bash
pip install vapoursynth vsrepo
# Dann Plugins installieren:
vsrepo install ffms2 havsfunc
# vs-mlrt ONNX Runtime:
pip install vapoursynth-mlrt-ort
```
**Linux benötigt Python 3.12+ auf dem System. Die Software prüft dies und gibt eine klare Fehlermeldung wenn Python fehlt.**

### Versions-Konstanten (im Code hardcoded)
```csharp
static readonly string VSVersion = "R76";
static readonly string FFMS2Version = "5.0";
static readonly string VSMLRTVersion = "v15.16";
```

## Phasen

### Phase 1: FFmpeg-Pipeline optimieren (pipe statt PNG)
**Ziel:** Die aktuelle VideoPipeline extrahiert jedes Frame als PNG auf die Festplatte (18.000+ Dateien bei 10 Min Video). Das ist langsam und I/O-lastig. Stattdessen soll FFmpeg Frames als raw pipe streamen.

**Dateien:**
- `FlipsiColor.Core/Video/VideoPipeline.cs` — PipelineAusfuehren umschreiben
- `FlipsiColor.Core/Video/FrameProcessor.cs` — bleibt unverändert (Verarbeitet Mat-Objekte)

**Implementierung:**
1. FFmpeg `ffmpeg -i video.mp4 -f rawvideo -pix_fmt bgr24 pipe:1` → stdout
2. In C# StreamReader liest raw frames in 4K-Buffer
3. Pro Frame: raw bytes → OpenCvSharp Mat (direkt, kein PNG)
4. FrameProcessor.Verarbeiten(mat, params) → korrigiertes Mat
5. Korrigiertes Mat → raw bytes → FFmpeg stdin `ffmpeg -f rawvideo -pix_fmt bgr24 -s WxH -r FPS -i pipe:0 -c:v libx264 -crf 18 output.mp4`
6. Audio: separater FFmpeg-Prozess extrahiert Audio → Re-Mux am Ende
7. Fortschritts-Callback: `frame_index / total_frames` → Prozent

**Vorteile:**
- Keine 18.000 PNG-Dateien auf der Festplatte
- 3-5x schneller (kein PNG encode/decode)
- Weniger Speicherplatz
- Gleiche KI-Modell-Nutzung

**Test:** TestRunner Phase 6 hinzufügen — "VideoPipeline Pipe-Modus" mit kleinem Test-Video (5 Sekunden, 64x64).

### Phase 2: VapourSynthInstaller Klasse
**Ziel:** Automatischer Download + Installation von VapourSynth + Plugins in AppData/FlipsiColor/vapoursynth/.

**Neue Datei:** `FlipsiColor.Core/Video/VapourSynthInstaller.cs`

**Implementierung:**
```csharp
public sealed class VapourSynthInstaller
{
    // Installations-Pfad: AppData/Local/FlipsiColor/vapoursynth/
    // Windows: VapourSynth64-Portable-R76.zip entpacken
    //          ffms2-5.0-msvc.7z entpacken nach vapoursynth/plugins/
    //          VSORT-Windows-x64.v15.16.7z entpacken nach vapoursynth/plugins/
    //          scripts.v15.16.7z entpacken nach vapoursynth/plugins/
    // Linux: pip install vapoursynth vsrepo
    //        vsrepo install ffms2 havsfunc
    //        pip install vapoursynth-mlrt-ort

    public bool IstInstalliert { get; }  // Prüft ob vspipe + plugins vorhanden
    public event EventHandler<InstallProgress>? Fortschritt;
    public async Task<bool> InstallierenAsync(CancellationToken ct);
    public Task<bool> DeinstallierenAsync();
}
```

**Download-Logik:**
1. `HttpClient` mit Progress-Reporting
2. ZIP/7z Dateien herunterladen
3. 7z entpacken (7-Zip standalone oder System.IO.Compression für ZIP, für 7z: `7z` CLI oder `SevenZipExtractor` NuGet)
4. Plugins in richtigen Ordner entpacken
5. `IstInstalliert` prüft: `vspipe` existiert + `ffms2.dll` existiert + `vsort.dll` existiert
6. Fehlerbehandlung: Download schlägt fehl → klare Fehlermeldung, keine halbe Installation

**Sicherheit:**
- SHA256-Hash für jede Datei verifizieren nach Download
- HTTPS-only (SecurityValidator.ValidiereDownloadUrl)
- Download in temp-Verzeichnis, erst nach Verifikation verschieben

**Test:** TestRunner Phase 7 — "VapourSynthInstaller Pfad-Check" (ohne echten Download, nur IstInstalliert-Logik).

### Phase 3: VapourSynthProcessor mit KI-Modellen erweitern
**Ziel:** VapourSynthProcessor soll die ONNX-Modelle über vs-mlrt (vs-onnxruntime) nutzen können.

**Datei:** `FlipsiColor.Core/Video/VapourSynthProcessor.cs`

**Implementierung:**
1. `GeneriereFilterScript()` erweitern:
   - KI-Modelle über `core.ort.Model(clip, model_path, ...)` aufrufen
   - NAFNet: `core.ort.Model(clip, "NAFNet.onnx", tilesize=[512,512], overlap=[32,32])`
   - CodeFormer: `core.ort.Model(clip, "CodeFormer.onnx", ...)` — Input 512x512 fixed
   - AiLUTTransform: `core.ort.Model(clip, "AiLUTTransform.onnx", ...)`
   - RealESRGAN: `core.ort.Model(clip, "RealESRGAN.onnx", ...)` — 4x Upscaling
2. Modell-Pfade: zeigen auf AppData/FlipsiColor/Models/ (gleicher Ordner wie FFmpeg-Backend)
3. Provider: CPU als Standard, `"DML"` auf Windows wenn DirectML verfügbar
4. `PipelineAusfuehren()` — vspipe → ffmpeg pipe mit KI-Filtern
5. Audio: extrahieren und re-mux wie in VideoPipeline

**Wichtig:** VapourSynth-Script muss die Modelle aus dem lokalen Modelle-Ordner laden, nicht aus dem VapourSynth-Plugin-Ordner. Pfad-Mapping:
```
C:\Users\X\AppData\Local\FlipsiColor\Models\NAFNet.onnx
→ im Script: r'C:/Users/X/AppData/Local/FlipsiColor/Models/NAFNet.onnx'
```

**Test:** TestRunner Phase 8 — "VapourSynth Script-Generierung" (generiert Script, prüft dass ONNX-Modell-Pfade korrekt sind, ohne echten vspipe-Aufruf).

### Phase 4: VideoBackend-Auswahl in Pipeline
**Ziel:** VideoPipeline wählt automatisch zwischen FFmpeg und VapourSynth basierend auf Settings.

**Dateien:**
- `FlipsiColor.Core/Video/VideoPipeline.cs` — Konstruktor erweitert
- `FlipsiColor.Core/Core/PipelineParams.cs` — VideoBackend enum bleibt
- `FlipsiColor.Core/Core/Settings.cs` — VideoBackend bleibt

**Logik:**
```csharp
public void PipelineAusfuehren(PipelineParams param, ...)
{
    if (_settings.VideoBackend == VideoBackend.VapourSynth && _vsProcessor?.IstVerfuegbar == true)
    {
        // VapourSynth-Pipeline mit KI-Modellen
        _vsProcessor.VideoLaden(_videoPfad);
        _vsProcessor.PipelineAusfuehren(param, outputPfad, fortschrittCallback);
    }
    else
    {
        // FFmpeg-Pipeline (optimiert mit pipe)
        FFmpegPipelineAusfuehren(param, outputPfad, fortschrittCallback);
    }
}
```

**Fallback:** Wenn VapourSynth aktiviert aber nicht verfügbar → Warnung + automatisch FFmpeg.

### Phase 5: Einstellungen UI — Backend-Toggle
**Ziel:** In den Einstellungen einen Toggle "Video-Backend" mit Auto-Install.

**Avalonia UI (Linux):**
- `FlipsiColor.Avalonia/Views/MainWindow.axaml` — Settings-Tab erweitern
- Toggle: FFmpeg (Standard) | VapourSynth (Empfohlen für beste Qualität)
- Bei VapourSynth-Auswahl:
  - Wenn nicht installiert: "VapourSynth installieren (~79 MB)" Button
  - Progress-Bar während Download
  - Nach Installation: "VapourSynth aktiv — Bessere Video-Qualität"
- Bei Zurück auf FFmpeg: sofort, kein Neustart nötig

**WPF UI (Windows):**
- `FlipsiColor/Views/MainWindow.xaml` — entsprechender Settings-Bereich
- Gleiche Logik wie Avalonia

**Beide UIs:**
- Toggle speichert in `Settings.VideoBackend`
- `Settings.Speichern()` nach Änderung
- `VideoPipeline` liest Setting bei nächster Verarbeitung

### Phase 6: VapourSynth Fehlerbehandlung + Edge Cases
**Ziel:** Robuste Fehlerbehandlung für alle Szenarien.

**Szenarien:**
1. **VapourSynth nicht installiert + Nutzer klickt Toggle:**
   → Auto-Download startet, Progress-Bar, bei Erfolg aktivieren
   → Bei Fehler: klare Meldung, FFmpeg bleibt aktiv

2. **VapourSynth installiert aber vspipe nicht im PATH:**
   → Software nutzt absoluten Pfad: `AppData/FlipsiColor/vapoursynth/vspipe.exe`
   → Kein PATH-Messing, keine Registry

3. **Plugin fehlt (ffms2, vsort):**
   → `IstVerfuegbar` gibt false zurück
   → Software anbietet: "Plugin fehlt — nachinstallieren?"

4. **Linux ohne Python 3.12:**
   → Klare Fehlermeldung: "Python 3.12+ erforderlich. Bitte installieren Sie Python."
   → Link zu python.org/downloads

5. **Video-Format nicht von ffms2 unterstützt:**
   → Fallback auf LWLibAVSource im Script
   → Wenn beide fehlschlagen: FFmpeg-Backend

6. **KI-Modell nicht heruntergeladen + VapourSynth aktiv:**
   → ModelManager lädt Modell automatisch herunter (gleiche Logik wie FFmpeg-Backend)
   → VapourSynth-Script wartet auf Modell-Download

7. **Abbruch durch Nutzer während Installation:**
   → CancellationToken, teilweise Downloads löschen
   → Keine halbe Installation stehen lassen

### Phase 7: Version auf 0.4.2 bumpen
**Ziel:** Alle Versionsnummern aktualisieren.

**Dateien (alle `0.4.1` → `0.4.2`):**
- `FlipsiColor.Core/FlipsiColor.Core.csproj` — Version, AssemblyVersion, FileVersion
- `FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj` — dito
- `FlipsiColor/FlipsiColor.csproj` — dito
- `FlipsiColor.Core/Core/Settings.cs` — falls Version dort steht
- `FlipsiColor.Avalonia/Lokalisierung.cs` — App.Titel DE + EN
- `FlipsiColor.Avalonia/ViewModels/MainViewModel.cs` — Title
- `FlipsiColor/UI/MainViewModel.cs` — Title
- `FlipsiColor.Avalonia/App.axaml.cs` — Log-Text
- `FlipsiColor/App.xaml.cs` — Log-Text
- `FlipsiColor.TestRunner/Program.cs` — TestRunner-Titel
- `installer/installer.iss` — AppVersion
- `installer/windows/installer.nsi` — APPVERSION
- `installer/linux/control` — Version
- `installer/build-script.sh` — APP_VERSION + Kommentar
- `FlipsiColor/app.manifest` — assemblyIdentity version
- `FlipsiColor.Avalonia/app.manifest` — dito

### Phase 8: Build, Test, Release
**Ziel:** Alles kompiliert, Tests bestehen, Release auf GitHub.

1. **Linux Build:**
   ```bash
   dotnet build FlipsiColor.Avalonia/FlipsiColor.Avalonia.csproj -c Release -r linux-x64 --self-contained true
   ```
   → 0 Fehler, 0 Warnungen

2. **Windows Build:**
   ```bash
   dotnet build FlipsiColor/FlipsiColor.csproj -c Release -r win-x64 --self-contained true
   ```
   → 0 Fehler, 0 Warnungen

3. **TestRunner:**
   ```bash
   unset CI && dotnet run --project FlipsiColor.TestRunner -- -c Release
   ```
   → Alle Tests bestanden (inklusive neuer VapourSynth-Tests)

4. **Installer bauen:**
   - Linux: `dpkg-deb --build` → `FlipsiColor-0.4.2-linux.deb`
   - Windows: `makensis` → `FlipsiColor-v0.4.2-Windows-x64-Installer.exe`

5. **Commit + Push:**
   ```bash
   git add -A
   git commit -m "feat: VapourSynth als optionales Video-Backend mit Auto-Install — v0.4.2"
   git push origin main
   ```

6. **CI abwarten:** Alle Workflows müssen `success` sein.

7. **GitHub Release:**
   ```bash
   gh release create v0.4.2 --title "v0.4.2 — VapourSynth Integration" \
     FlipsiColor-0.4.2-linux.deb \
     FlipsiColor-v0.4.2-Windows-x64-Installer.exe
   ```

8. **Verifikation:**
   - `gh run list` → alle `success`
   - `gh release view v0.4.2` → beide Assets `uploaded`
   - `curl -sL -o /dev/null -w "%{http_code}"` für alle Download-Links → 200
   - Download + ONNX Session Test mit TestRunner → 10/10+ bestanden

## Wichtige Regeln

1. **FFmpeg bleibt Standard** — VapourSynth ist optional. Niemand wird gezwungen.
2. **Kein PATH-Messing** — VapourSynth läuft portabel aus AppData-Ordner
3. **Keine Registry-Einträge** — alles im FlipsiColor-Ordner
4. **Linux braucht Python** — das ist die einzige Voraussetzung, klare Fehlermeldung
5. **Download-Links sind verifizierte HTTP 200** — alle oben geprüft
6. **KI-Modelle funktionieren mit beiden Backends** — gleicher Modelle-Ordner
7. **0 Fehler, 0 Warnungen** — TreatWarningsAsErrors bleibt aktiv
8. **Linux-First** — zuerst auf Linux bauen+testen, dann Windows
9. **Cross-Platform** — beide UIs (Avalonia + WPF) bekommen den Toggle
10. **Test-Driven** — TestRunner muss neue Phasen abdecken