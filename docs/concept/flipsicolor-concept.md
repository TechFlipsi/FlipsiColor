# FlipsiColor — Konzeptdokument v1.1

> **Professionelle KI-gestützte Bild- & Videofarbkorrektur — Lokal, Native, Null-Setup, Stil-Lernend**

---

## 1. Vision & Kernprinzipien

**FlipsiColor** ist eine native Desktop-Anwendung die **alles selbst macht** — keine Zusatzsoftware, kein Setup-Aufwand, keine Vorkenntnisse nötig.

### Die 4 Grundregeln

1. **Zero-Setup:** Installieren → Bild/Video rein → fertig bearbeitet raus. Keine Plugins, keine zusätzliche Software, keine Modelle die man extra herunterladen muss. Alles ist integriert.
2. **Zero-Post-Work:** Nach dem Auto-Enhance ist das Bild fertig. Kein "danach noch nachjustieren". Die KI macht es beim ersten Mal richtig.
3. **Konsistente Farben:** Videos bekommen **einen** Look — Frame 1 sieht genauso aus wie Frame 1000. Wiesen bleiben dieselbe Wiese, Hauttöne bleiben konsistent.
4. **Stil-Lernend:** Die KI merkt sich was du magst. Je mehr du bearbeitest, desto besser wird sie. Wie ein Assistent der deinen Geschmack lernt.

**Zielgruppe:** Fotografen, Drone-Piloten (DJI), Videografen, Content-Creator die perfekte Ergebnisse wollen ohne DaVinci Resolve zu lernen.

**Kernversprechen:** Rein → Profi-Ergebnis → Raus. Kein Zwischenschritt.

---

## 2. Architektur & Technologie-Stack

### 2.1 Programmiersprache: C++20

| Aspekt | Begründung |
|---|---|
| Performance | 4K-Video frame-by-frame, RAW-Bilder — C++ ist ungeschlagen |
| GPU-Zugriff | Direkter CUDA/Vulkan/OpenCL-Access für KI-Inferenz |
| Bibliotheken | OpenCV, LibRaw, FFmpeg, ONNX Runtime — alle native C++ APIs |
| Ecosystem | DaVinci Resolve, OBS, Darktable, RawTherapee — alle in C++ |
| Speicher | 4K/8K-Bilder, RAW, Video-Frames — manuelle Kontrolle wichtig |

### 2.2 UI-Framework: Qt6 + QML

| Aspekt | Begründung |
|---|---|
| Native Look | Rendert nativ auf Win/Mac/Linux — kein Electron |
| QML | Deklarative UI mit Animationen, Fluid Design |
| C++ Backend | Business-Logik in C++, UI in QML — saubere Trennung |
| Cross-Platform | Ein Codebase, 3 Plattformen |

### 2.3 Alles Inklusiv — Keine Zusatzsoftware nötig

| Komponente | Integration | Muss der User installieren? |
|---|---|---|
| OpenCV | Statisch gelinkt | ❌ Nein |
| LibRaw | Statisch gelinkt | ❌ Nein |
| FFmpeg | Statisch gelinkt (codec-spezifisch) | ❌ Nein |
| ONNX Runtime | Statisch gelinkt | ❌ Nein |
| CUDA/DirectML/Metal | Runtime-bundled | ❌ Nein |
| KI-Modelle | Beim ersten Start heruntergeladen (Hintergrund) | ❌ Nein |
| Log-Profil LUTs | Integriert | ❌ Nein |
| Qt6 | Statisch gelinkt | ❌ Nein |

Der Installer enthält **alles**. Nach der Installation ist FlipsiColor sofort einsatzbereit. KI-Modelle werden beim ersten Start im Hintergrund geladen (Progress-Bar mit "Optimiere KI für Ihre GPU...").

### 2.4 Plattform-Support

| Plattform | Compiler | GPU | Package |
|---|---|---|---|
| Windows 10/11 | MSVC 2022 | CUDA, DirectML | MSI/MSIX Installer |
| macOS 12+ | Clang (Xcode) | Metal/MPS | DMG |
| Linux | GCC 12+ | Vulkan, OpenCL | AppImage/Flatpak |

---

## 3. KI-Modelle — Alles integriert, lazy-loaded

### 3.1 ONNX Runtime (C++ API)

Alle KI-Modelle laufen lokal über ONNX Runtime:
- **GPU-Provider:** CUDA (NVIDIA) → DirectML (Windows) → Metal/MPS (Mac) → OpenCL/Vulkan (Linux)
- **CPU-Fallback:** Automatisch wenn keine GPU vorhanden
- **Quantisierung:** INT8 für schnellere Inferenz (3-4x schneller als FP32)
- **Download:** Modelle werden beim ersten Start heruntergeladen, danach lokal gecacht

### 3.2 Modell-Pipeline (Auto-Enhance)

```
Eingabe-Bild/Frame
       │
       ▼
  ┌─────────────┐
  │  Analyse    │ ← KI erkennt: Szene, Belichtung, Rauschen, Log-Profil
  └─────┬───────┘
        │
        ▼
  ┌─────────────┐     ┌──────────────┐
  │ Weißabgleich │◄────│ Stil-Profil  │ ← Persönliche Vorlieben
  └─────┬───────┘     │ (gelernt)     │
        │               └──────────────┘
        ▼
  ┌─────────────┐
  │ Belichtung   │ ← Histogramm + Szenen-Typ + Stil
  └─────┬───────┘
        │
        ▼
  ┌─────────────┐
  │ Rausch-     │ ← NAFNet (nur wo Rauschen erkannt)
  │ unterdr.    │
  └─────┬───────┘
        │
        ▼
  ┌─────────────┐
  │ Schärfung    │ ← Adaptiv: Kanten ja, Flächen nein
  └─────┬───────┘
        │
        ▼
  ┌─────────────┐
  │ Log → Rec709│ ← Nur wenn Log-Profil erkannt
  └─────┬───────┘
        │
        ▼
  ┌─────────────┐
  │ Stil-LUT    │ ← Gelernter Look (warm, kühl, vintage, etc.)
  └─────┬───────┘
        │
        ▼
   Ausgabe
```

### 3.3 Modelle nach Aufgabe

| Aufgabe | Modell | Zweck | Größe |
|---|---|---|---|
| Szenen-Analyse | Eigenes CNN (MobileNet-basiert) | Erkennt: Landschaft, Portrait, Nacht, Indoor, Drohne, etc. | ~5 MB |
| Weißabgleich | SCI (Spatial Color Illumination) | Automatischer Weißabgleich nach Beleuchtung | ~15 MB |
| Belichtungskorrektur | Eigenes U-Net | Histogramm-Analyse → optimale Parameter | ~10 MB |
| Rauschunterdrückung | NAFNet (width64) | SOTA Denoising, RAW-Rauschen | ~50 MB |
| Entrauschung Alt. | Restormer | Bewegungsunschärfe | ~95 MB |
| Upscaling | Real-ESRGAN x4 | 4x Upscaling mit Detail-Erhaltung | ~65 MB |
| Gesichts-Restaurierung | CodeFormer | Gesichter entpixeln | ~350 MB |
| Stil-Transfer | Eigenes 3DLUT-Net | Lernt persönlichen Look | ~20 MB |

**Gesamt beim ersten Start:** ~500 MB Download (Hintergrund, mit Progress-Bar).
**Minimal-Start:** Nur Szenen-Analyse + Weißabgleich + Belichtung = ~30 MB.

---

## 4. 🆕 Farb-Konsistenz im Video — "Wiese bleibt Wiese"

### 4.1 Das Problem

Ein Drohnenvideo: Frame 1 = schöne grüne Wiese. Frame 500 = plötzlich anderes Grün. Frame 1000 = dumpfes Gelbgrün. Das sieht amateurhaft aus und ist genau das was Sie nicht wollen.

### 4.2 Die Lösung: Reference-Frame-Pipeline

Die KI sucht sich automatisch den **besten Frame** im Video (Reference Frame / Hero Frame) und gleicht alle anderen Frames daran an.

```
Video rein → Scene Detection (Szenenwechsel erkennen)
                │
                ├── Szene 1: Frame 0-299
                │      └── Reference Frame = Frame mit bester Belichtung/ Histogramm
                │      └── Alle Frames → Histogramm-Matching an Reference
                │
                ├── Szene 2: Frame 300-599
                │      └── Reference Frame = bester Frame in Szene 2
                │      └── Alle Frames → Histogramm-Matching an Reference
                │
                └── Szene 3: Frame 600-899
                       └── Reference Frame = bester Frame
                       └── Alle Frames → Histogramm-Matching an Reference
```

### 4.3 Technische Umsetzung (C++)

```cpp
class VideoColorConsistencyEngine {
public:
    // 1. Szene erkennen — Schnitte finden
    std::vector<SceneBoundaries> detectScenes(const VideoFrames& frames);

    // 2. Besten Frame pro Szene finden
    FrameRef findReferenceFrame(const Scene& scene);

    // 3. Histogramm-Matching — Frame an Reference angleichen
    cv::Mat matchToReference(const cv::Mat& frame, const cv::Mat& reference);

    // 4. Temporal Smoothing — Parameter glätten über Zeit
    void smoothTransitions(std::vector<CorrectionParams>& params, int windowSize = 5);

    // 5. Skin-Tone-Schutz — Hauttöne erhalten bei Konsistenz-Korrektur
    cv::Mat protectSkinTones(const cv::Mat& corrected, const cv::Mat& original);
};
```

**Warum Histogramm-Matching und nicht "einfach dieselben Parameter"?**
Weil jedes Frame unterschiedliche Belichtung hat. Histogramm-Matching transformiert die Farbkurve SO dass die visuelle Wirkung konsistent wird — egal ob die Sonne hinter einer Wolke war oder nicht.

### 4.4 3 Ebenen der Konsistenz

| Ebene | Was | Wie |
|---|---|---|
| **Innerhalb eines Clips** | Alle Frames eines Videos sehen gleich aus | Reference-Frame + Histogramm-Matching |
| **Zwischen Clips** | Mehrere Drohnen-Videos sehen zusammenhängend aus | Globaler Stil-Anchor (aus Stil-Profil) |
| **Über Projekte hinweg** | Ihre Bilder/Videos haben einen erkennbaren Stil | Stil-Lernsystem (siehe Abschnitt 5) |

### 4.5 Was passiert bei Szenenwechsel?

Szenenwechsel (Schnitt, Indoor→Outdoor) werden **erkannt** und **respektiert**. Die Konsistenz gilt *innerhalb* einer Szene. Bei einem Szenenwechsel:
1. Neuer Reference Frame für die neue Szene
2. Sanfte Überblendung der Korrekturparameter über ~5 Frames
3. Hauttöne bleiben konsistent auch über Szenenwechsel hinweg

---

## 5. 🧠 Stil-Lernsystem — Zwei Modi, ein Ziel

### 5.0 Zwei Lern-Modi in den Einstellungen

In den Einstellungen gibt es einen Schalter: **"KI lernt mit"**

| Modus | Was passiert | Für wen |
|---|---|---|
| **🔵 AN (Smart-Learn)** | KI lernt automatisch aus Ihren Bearbeitungen. Je mehr Sie bearbeiten, desto besser wird sie. | Für Nutzer die ihre Handschrift entwickeln wollen |
| **⚪ AUS (Ask-Mode)** | KI macht selbststaendig und fragt nach Feedback — per Swipe/Like/Dislike. Wie eine Dating-App fuer Bildlooks. | Fuer Nutzer die einfach nur klare Ergebnisse wollen |
| **🔴 TURBO** | Ordner rein → KI macht alles. Keine Fragen, keine Ruckfragen, keine Bestaetigung. KI entscheidet komplett selbststaendig mit ihrem Fotografie-Wissen. | Fuer Nutzer die 0 Interaction wollen — rein, fertig, raus |

**Der Ask-Mode (KI aus = AUS) ist der Standard bei Neuinstallation.** TURBO ist fuer Nutzer die einfach wollen dass die KI alles erledigt — kein Swipen, kein Bestaetigen, kein Nachfragen.

---

### 5.1 Ask-Mode — "Lovoo für Bildlooks" 👍👎

Wenn KI-Lernen **AUS** ist, startet der Ask-Mode. Die KI bearbeitet automatisch und zeigt Ihnen Ergebnisse — und Sie geben Feedback mit einem einfachen Swipe-System.

#### Wie es funktioniert

```
┌─────────────────────────────────────────────────────────────┐
│  FlipsiColor — Ask Mode                                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│    ┌───────────────────────────────────────┐                │
│    │                                       │                │
│    │                                       │                │
│    │       ← Original │ KI-Bearbeitet →    │                │
│    │                                       │                │
│    │         (Split-Slide Vorschau)       │                │
│    │                                       │                │
│    └───────────────────────────────────────┘                │
│                                                             │
│         👎                          👍                       │
│    ┌──────────┐              ┌──────────┐                   │
│    │  ✗ Nein  │              │  ✓ Ja!   │                   │
│    │ gefällt  │              │ gefällt  │                   │
│    │ mir nicht│              │ mir!     │                   │
│    └──────────┘              └──────────┘                   │
│                                                             │
│    ┌──────────────────────────────────────────────────┐     │
│    │ 💬 Optional: "Was gefällt dir nicht?"            │     │
│    │ ┌──────────────────────────────────────────────┐ │     │
│    │ │ z.B. "Zu warm", "Zu grell", "Zuweich" ...    │ │     │
│    │ └──────────────────────────────────────────────┘ │     │
│    │ Quick-Tags: [Zu warm] [Zu kalt] [Zu grell]      │     │
│    │              [Zu dunkel] [Zu hell] [Zuweich]      │     │
│    │              [Zu scharf] [Farben falsch]           │     │
│    └──────────────────────────────────────────────────┘     │
│                                                             │
│    Bilder übrig: 47                      [Überspringen »]   │
└─────────────────────────────────────────────────────────────┘
```

#### Der Flow

1. **KI bearbeitet ein Bild automatisch** → zeigt Vorher/Nachher
2. **Sie swipen oder klicken:**
   - **👍 JA** → KI merkt: "Dieser Look funktioniert!" → Stil-Profil wird bestätigt
   - **👎 NEIN** → KI merkt: "Dieser Look passt nicht" → generiert eine **andere** Variante
   - **Überspringen** → Kein Feedback, nächstes Bild
3. **Optional: Text-Feedback oder Quick-Tags** → "Zu warm", "Farben falsch", "Zu dunkel"
4. **KI lernt aus jedem Feedback** → nächste Bearbeitung wird besser

#### Wie die KI aus👎 lernt

Wenn Sie "Nein" klicken:

```
👎 "Gefällt mir nicht"
    │
    ├── KI generiert 2. Variante mit anderen Parametern
    │   ├── Wärmer statt kühler (oder umgekehrt)
    │   ├── Stärkerer Kontrast (oder weicher)
    │   ├── Andere Belichtung
    │   └── Kombination aus Obigem
    │
    ├── Sie swipen wieder → 👍 oder 👎
    │   ├── 👍 auf 2. Variante → KI lernt: "Erster Versuch war zu X, zweiter war besser"
    │   └── 👎 auf 2. Variante → KI merkt sich beide als Negativ-Beispiel
    │
    └── Optional: Sie schreiben "Bisschen wärmer bitte"
        → KI lernt: Richtung = wärmer, Stärke = "bisschen"
```

#### Quick-Tags (vorausgefüllte Feedback-Optionen)

| Kategorie | Tags |
|---|---|
| **Temperatur** | Zu warm · Zu kalt · Perfekt warm · Perfekt kühl |
| **Belichtung** | Zu hell · Zu dunkel · Perfekt belichtet |
| **Farben** | Zu grell · Zu blass · Farben falsch · Farben perfekt |
| **Kontrast** | Zu flach · Zu hart · Perfekt |
| **Schärfe** | Zu weich · Zu scharf · Perfekt |
| **Rauschen** | Zu glatt · Zu rauschig · Perfekt |
| **Stimmung** | Zu dramatisch · Zu langweilig · Cineastisch · Natürlich |

Nutzer kann auch **frei Text schreiben** — z.B. "Ich mag diesen warmen Sonnenuntergang-Look, aber weniger Sättigung". Die KI parst das und lernt die Richtung.

#### Batch-Ask-Mode (für plusieurs Bilder)

Für mehrere Bilder gleichzeitig:

```
┌─────────────────────────────────────────────────────────────┐
│  Batch-Ask: 12 Bilder werden bearbeitet...                  │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐  │
│  │ 📷 1 │ │ 📷 2 │ │ 📷 3 │ │ 📷 4 │ │ 📷 5 │ │ 📷 6 │  │
│  │  ✓   │ │  ✓   │ │  ✗   │ │  ✓   │ │  —   │ │  ✓   │  │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘  │
│                                                             │
│  ✓ = Gefällt mir    ✗ = Gefällt mir nicht    — = Übersprungen│
│                                                             │
│  [Alle ✓ markierten übernehmen]  [✗ Bild 3 neu bearbeiten]  │
│                                                             │
│  💬 Feedback für ✗ Bild 3:                                  │
│  ┌──────────────────────────────────────────────┐           │
│  │ "Zu warm, lieber natürlicher"               │           │
│  └──────────────────────────────────────────────┘           │
│  Quick: [Zu warm] [Zu kalt] [Natürlicher] [Zu grell]       │
└─────────────────────────────────────────────────────────────┘
```

Sie sehen 6-12 Vorschau-Bilder gleichzeitig, swipen durch und die KI lernt aus dem Gesamtbild Ihrer Präferenzen.

---

### 5.1b Turbo-Modus — "Ordner rein, fertig, raus"

Keine Fragen. Keine Ruckfragen. Keine Bestaetigungen. Die KI entscheidet **alles** selbststaendig — basierend auf ihrem eingebauten Fotografie-Wissen und dem gelernten Stil-Profil.

#### So funktioniert es

```
┌─────────────────────────────────────────────────────────────┐
│  FlipsiColor — Turbo-Modus                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  🔴 TURBO                                                   │
│                                                             │
│  📂 Quell-Ordner:  [/Drohne/2026-05-23_Wachau/            ▼] │
│     → 247 Bilder gefunden (RAW + JPEG)                     │
│                                                             │
│  💾 Export-Ordner: [/Export/2026-05-23_Wachau/             ▼] │
│     → Wird automatisch erstellt falls nicht vorhanden      │
│     ☑ Quell-Ordner-Struktur beibehalten                   │
│     ☐ In Original-Ordner speichern (nicht empfohlen)       │
│                                                             │
│  Intensitaet: [Mittel ▼] (Leicht / Mittel / Stark)        │
│  Export-Profil: [YouTube 4K ▼]                             │
│                                                             │
│  [🚀 LOS — Keine Fragen]                                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

Ein Klick auf "LOS" und die KI arbeitet komplett selbststaendig:

1. Ordner scannen → alle Bilder/videos erkennen
2. Fuer JEDS Bild: Szenen-Typ erkennen → Stil-Profil laden → Parameter berechnen → anwenden
3. Fuer JEDS Video: Log-Profil erkennen → Reference Frame waehlen → alle Frames korrigieren
4. Export in den gewaehlten Profil-Einstellungen
5. Fertig. Kein einziges Nachfragen.

#### Was die KI im Turbo-Modus entscheidet

| Entscheidung | Wie die KI entscheidet |
|---|---|
| Weißabgleich | Aus ihrem Fotografie-Wissen: Beleuchtung erkennen → passende Korrektur |
| Beltaeuchtung | Histogramm analysieren → optimale Beltaeuchtung berechnen |
| Kontrast | Szenen-Typ: Landschaft = mehr Kontrast, Portrait = weniger |
| Farben | Stil-Profil mit gelernten Vorlieben (falls vorhanden) |
| Rauschunterdrueckung | ISO-Wert aus EXIF → Rauschstaenge automatisch |
| Schaerfe | Szenen-Typ: Landschaft = schaerfer, Portrait = weicher |
| Log-Profil | EXIF + Dateiname + Histogramm → automatisch erkennen |
| Intensitaet | Vom Nutzer voreingestellt (Leicht/Mittel/Stark) |
| Neue Szenen-Typen | Fotografie-Wissen fallback — keine Fragen, beste Schaetzung |

#### Turbo vs. Ask-Mode vs. Smart-Learn

| | Turbo | Ask-Mode | Smart-Learn |
|---|---|---|---|
| Fragen an den Nutzer | **Keine** | 👍👎 Feedback | Selten (nur bei neuen Szenen) |
| Interaktion | Ordner waehlen, fertig | Jedes Bild bewerten | Normal bearbeiten |
| KI trifft Entscheidungen | Alleine | Nach Feedback | Mit gelernten Vorlieben |
| Neuer Szenen-Typ | Beste Schaetzung | Fragt nach 👍👎 | Fragt kurz nach |
| Geschwindigkeit | **Schnellster Modus** | Langsam (Interaktion) | Normal |
| Lerneffekt | Keiner (KI entscheidet) | Hoch (direktes Feedback) | Hoch (indirekt) |
| Fuer wen | "Mach einfach" | "Ich will mitreden" | "Lern meinen Stil" |

#### Wichtig: Turbo lernt NICHT

Im Turbo-Modus lernt die KI **nicht** dazu — weil es kein Feedback gibt. Die KI nutzt:
- Ihr eingebautes Fotografie-Wissen (was ist eine gute Belichtung, wie soll Landschaft aussehen, etc.)
- Das gelernte Stil-Profil (falls vorhanden, aus vorherigen Ask/Smart-Learn Sitzungen)
- Standard-Parameter fuer jeden Szenen-Typ

**Tipp:** Turbo ist perfekt fuer schnelle Bulk-Jobs. Ask- oder Smart-Learn fuer Bilder die Ihnen wirklich wichtig sind.

#### Turbo + Batch = Maximum Geschwindigkeit

```
Ordner waehlen → KI analysiert alle Bilder gleichzeitig
                 → Parameter berechnen (parallel)
                 → Export (parallel)
                 → Fertig. 247 Bilder in ~3 Minuten.
```

Kein Warten auf Nutzer-Input. Kein Pausieren. Die KI arbeitet durch wie eine Maschine.

-Modus — KI lernt im Hintergrund

Wenn KI-Lernen **AN** ist, lernt die KI stillschweigend aus Ihren Bearbeitungen:

```
┌──────────────────────────────────────────────────────┐
│                 SMART-LEARN-PROZESS                  │
├──────────────────────────────────────────────────────┤
│                                                      │
│  1. Auto-Enhance macht Vorschlag                    │
│     │                                                │
│     ▼                                                │
│  2. Sie JUSTIEREN (oder nicht)                       │
│     │                                                │
│     ├── Garnicht verändert → "Stimmt perfekt! ✅"  │
│     │   → Stil-Profil wird BESTÄTIGT                 │
│     │                                                │
│     ├── Leicht wärmer → "Bitte wärmer"              │
│     │   → Stil-Profil lernt: +5% Wärme              │
│     │                                                │
│     └── Viel kühler → "Ganz anderer Look"           │
│         → Stil-Profil lernt: Dieses Motiv = kühl    │
│                                                      │
│  3. Nächstes Bild → berücksichtigt gelernten Stil    │
│     → Auto-Enhance wird WARMER weil Sie das mögen   │
└──────────────────────────────────────────────────────┘
```

**Beide Modi bauen dasselbe Stil-Profil auf.** Der Ask-Mode ist nur expliziter — Sie geben aktiv Feedback statt passiv zu bearbeiten.

---

### 5.3 Stil-Profil-Struktur

```cpp
struct StyleProfile {
    std::string name;               // "Meine Drohne", "Portraits", etc.
    bool isActive = true;

    // Basis-Parameter (gelernt aus Feedback)
    float warmthBias = 0.0f;        // -1.0 (kühl) bis +1.0 (warm)
    float saturationBias = 0.0f;    // -1.0 (entsättigt) bis +1.0 (knallig)
    float contrastBias = 0.0f;      // -1.0 (flach) bis +1.0 (kontrastreich)
    float shadowsTint = 0.0f;       // Teal/Orange Shift in Schatten
    float highlightsTint = 0.0f;    // Teal/Orange Shift in Lichtern
    float sharpnessPreference = 0.5f; // 0.0 (weich) bis 1.0 (scharf)
    float noisePreference = 0.5f;   // 0.0 (rauschig-natürlich) bis 1.0 (sauber-glatt)
    float exposureBias = 0.0f;      // -1.0 (dunkler) bis +1.0 (heller)

    // Szenen-spezifische Vorlieben
    std::map<SceneType, SceneStyle> sceneOverrides;
    // SceneType: Landscape, Portrait, Night, Indoor, Drone, Street, etc.

    // LUT-gelernte Korrektur
    std::vector<LUTPoint> learnedLUT;  // 3D-LUT die den gelernten Stil kodiert

    // Konfidenz (wie viele Bewertungen hat die KI schon)
    int sampleCount = 0;             // Start: 0, wird besser ab ~30

    // Ask-Mode History
    std::vector<FeedbackEntry> feedbackHistory;
    // Jeder Like/Dislike + optionaler Text wird gespeichert
};

struct FeedbackEntry {
    std::string imagePath;           // Welches Bild
    bool liked = true;               // 👍 oder 👎
    std::string textFeedback;        // Optionales Text-Feedback
    std::vector<std::string> tags;   // Quick-Tags: "Zu warm", "Zu grell", etc.
    CorrectionParams aiSuggestion;   // Was die KI vorgeschlagen hat
    CorrectionParams userFinal;      // Was der Nutzer akzeptiert hat (oder 👎 = null)
    SceneType scene;                 // Erkannte Szene
    std::chrono::system_clock::time_point timestamp;
};
```

### 5.4 Wie lernt die KI aus Feedback?

| Event | Was die KI lernt | Algorithmus |
|---|---|---|
| **👍 Like** | Vorschlag war gut → Parameter bestätigen | Moving Average: `param = param * 0.9 + current * 0.1` |
| **👎 Dislike** | Vorschlag war falsch → gegenteilige Richtung probieren | `param = param - delta_to_suggestion * 0.3` |
| **👎 + "Zu warm"** | Konkretes Feedback → gezielte Anpassung | Parse Tag → `warmthBias -= 0.15` |
| **👎 + Neuer Versuch 👍** | Erster Vorschlag falsch, zweiter richtig | `param = param * 0.5 + second_try * 0.5` |
| **Freitext "weniger Sättigung"** | NLP-parsing → Parametrisierung | Einfache Keyword-Extraction: "weniger" → reduzieren, "Sättigung" → saturationBias |
| **10 👍 in Folge** | Sehr hohes Vertrauen → Profil kann exportiert werden | Konfidenz steigt exponentiell |

**Text-Feedback ist OPTIONAL.** Quick-Tags sind OPTIONAL. Ein einfacher 👍👎 reicht völlig. Aber wenn der Nutzer schreibt, lernt die KI **schneller und gezielter**.

### 5.4b Lernphase — 2 Runden, dann selbständig

Die KI fragt NICHT ewig. Aber ein Durchlauf reicht nicht — die ersten 60 Feedbacks könnten nur Drohnenbilder sein. Die KI muss **mindestens 2 Runden** durchlaufen um verschiedene Motiv-Typen zu lernen.

#### Warum 2 Runden?

| Problem mit nur 1 Runde | Lösung mit 2 Runden |
|---|---|
| Runde 1 = nur Drohnenbilder → KI lernt "grüne Landschaft, warm" | Runde 1: Drohnenbilder → KI lernt Landschafts-Stil |
| Runde 2 = Portraits → KI wendet Landschafts-Stil auf Gesichter an ❌ | Runde 2: Portraits/Kamera-Bilder → KI lernt dass verschiedene Motive verschiedene Stile brauchen |
| Ergebnis: Einseitiges Profil | Ergebnis: Vielseitiges Profil, verschiedene Szenen-Typen |

#### Die 3 Phasen

| Phase | Feedbacks | Runde | Verhalten | Fragen pro Bild |
|---|---|---|---|---|
| Runde 1 — Basis | 0-20 | 1 | Jedes Bild fragen | Immer |
| Runde 1 — Reduziert | 20-60 | 1 | Bekannte Motive auto, unsichere fragen | Selten (1 von 3-5) |
| Runde 2 — Vertiefung | 61-80 | 2 | Wieder haeufiger fragen, neue Motiv-Typen | Oft (neue Typen) |
| Runde 2 — Reduziert | 80-120 | 2 | Wenig fragen, verschiedene Szenen auto | Selten |
| Selbststaendig | 120+ | — | Alles automatisch | Nie |

Statusleiste:
- R1: "Runde 1 — KI lernt Grundlagen (7/60)"
- R1: "Runde 1 — KI wird sicherer (42/60)"
- R1 done: "Runde 1 abgeschlossen! Runde 2 starten?"
- R2: "Runde 2 — KI lernt neue Motive (75/120)"
- R2: "Runde 2 — KI wird vielseitig (105/120)"
- Done: "KI kennt Ihren Stil! Selbststaendig ab jetzt."

Nach 60 Feedbacks zeigt die App einen Hinweis: Runde 1 abgeschlossen, bitte jetzt andere Bildtypen testen (Portraits, Nacht, Indoor). Der Nutzer kann Runde 2 ueberspringen, aber dann kennt die KI nur die bisherigen Motiv-Typen.

Manuell ueberspringen: "Markiere als fertig" — mindestens eine vollstaendige Runde (60 Feedbacks) empfohlen.

5.4c Neues Motiv? — Automatisches Nachlernen

Die KI lernt nie "aus". Nach den 2 Runden arbeitet sie selbststaendig — **aber nur fuer Motiv-Typen die sie kennt.** Sobald sie ein Bild sieht das sie nicht einordnen kann, passiert automatisch:

```
┌─────────────────────────────────────────────────────────────────┐
│  🤔 Neues Motiv erkannt!                                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Die KI kennt Ihren Stil fuer:                                │
│  ✅ Landschaften (42 Bewertungen)                              │
│  ✅ Drohne (38 Bewertungen)                                    │
│  ✅ Portraits (22 Bewertungen)                                 │
│  ✅ Nacht (18 Bewertungen)                                     │
│                                                                 │
│  Aber dieses Bild ist neu:                                      │
│  ❓ Architektur — Noch keine Bewertungen                       │
│                                                                 │
│  Wie soll die KI Architektur-Bilder bearbeiten?               │
│                                                                 │
│  [👍 Gefaellt mir]  [👎 Nicht mein Stil]                      │
│                                                                 │
│  ▸ Mehr Varianten zeigen                                      │
│  ▸ Quick-Tag: [Zu kalt] [Zu warm] [Zu flach] [Zu hart]       │
│                                                                 │
│  Nach ~5-10 Architektur-Bildern:                               │
│  → KI kennt Ihren Architektur-Stil ✅                         │
│  → Ab sofort automatisch wie Sie es moegen                    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

#### Wie das Nachlernen funktioniert

1. **Neues Motiv erkannt** — KI kann Bild keinem bekannten Szenen-Typ zuordnen
2. **Mini-Lernzyklus startet automatisch** — 5-10 Bilder dieses Typs mit 👍👎 bewerten
3. **Neuer Szenen-Typ wird erstellt** — z.B. "Architektur" mit eigenen Stil-Parametern
4. **Selbststaendig ab 5+ Bewertungen** — KI hat genug Daten fuer diesen Typ
5. **Niemals wieder gefragt** — ab jetzt automatisch bearbeitet

#### Das bedeutet:

| Szenen-Typ | Bewertungen | Verhalten |
|---|---|---|
| Bereits gelernt (z.B. Landschaft) | 120+ | Automatisch, nie gefragt |
| Neu erkannt (z.B. Architektur) | 0 | Sofort Mini-Lernzyklus |
| Am Lernen (z.B. Architektur) | 1-5 | Haeufiger gefragt |
| Gelernt (z.B. Architektur) | 5+ | Automatisch |

**Die Wissensbasis waechst mit jedem neuen Motiv-Typ.** Jedesmal wenn ein neuer Szenen-Typ auftaucht, erweitert sich die Lern-Datenbank — aber NUR fuer diesen neuen Typ. Die bereits gelernten Typen bleiben unberuehrt.

#### Beispiele fuer neue Motiv-Typen

| Erstes Bild | KI erkennt | Mini-Lernzyklus |
|---|---|---|
| Hochzeitsfoto | "Portrait-Event, weiches Licht" | 5-10 Hochzeitsbilder → Stil gelernt |
| Macro-Blume | "Macro, Detail" | 5-10 Macro-Bilder → Stil gelernt |
| Sternenhimmel | "Astro, extrem dunkel" | 5-10 Astro-Bilder → Stil gelernt |
| Innenraum | "Indoor, kuenstliches Licht" | 5-10 Indoor-Bilder → Stil gelernt |
| Sportfoto | "Schnelle Bewegung" | 5-10 Sport-Bilder → Stil gelernt |

Jeder neue Typ bekommt seine eigenen Parameter: Wie warm sollen Architektur-Bilder sein? Wie stark die SCHAERFE bei Macro? Wie viel Schattendetail bei Astro? Die KI lernt es — genau wie bei den ersten 2 Runden, nur kuerzer (5-10 statt 60).

5.4d Wissensdatenbank — Kompakt halten, nie aufblaehen

Das Stil-Profil waechst mit der Zeit — aber es darf **niemals** gross und langsam werden. Jedes Feedback ist wertvoll, aber 1000 aehnliche Feedbacks sind nicht mehr Informationswert als 50 diverse Feedbacks. Die Datenbank wird kontinuierlich komprimiert ohne Daten zu verlieren.

#### Das Problem

| Nach | Rohdaten (ohne Kompression) | Problem |
|---|---|---|
| 100 Bildern | ~5 KB | Kein Problem |
| 1.000 Bildern | ~50 KB | Noch ok |
| 10.000 Bildern | ~500 KB | Langsam beim Laden |
| 100.000 Bildern | ~5 MB | Unnoetig gross, viele Dubletten |

#### Die Loesung: 3-stufige Kompression

**Stufe 1: Deduplizierung — Gleiche Bilder, ein Feedback**

100 Landschaftsbilder mit 👍 heissen nicht 100 Eintraege. Sie heissen:
```
"Landschaft": { "count": 100, "avgWarmth": 0.72, "avgSaturation": 0.45 }
```
Ein Eintrag statt 100. Die Rohdaten (100 einzelne Bewertungen) werden zu Durchschnittswerte komprimiert. **Kein Datenverlust** — der Durchschnitt enthaelt die gleiche Information.

**Stufe 2: Szenen-Konsolidierung — Aehnliche Szenen verschmelzen**

"Landschaft - Sommer" und "Landschaft - Fruehling" mit fast identischen Parametern? Werden zu einem Eintrag:
```
"Landschaft": { "variants": ["Sommer", "Fruehling"], "shared": { ... } }
```
Nur die Unterschiede werden gespeichert, nicht die Gemeinsamkeiten doppelt.

**Stufe 3: Periodisches Compacting — Automatisch im Hintergrund**

Jedes Mal wenn die App startet (oder alle 100 neuen Feedbacks):
1. Aehnliche Feedbacks zusammenfuehren (Dedup)
2. Szenen mit < 5% Parameter-Unterschied verschmelzen
3. Veraltete Eintrage entfernen (aelter als 1 Jahr, weniger als 3 Bewertungen)
4. JSON optimieren (kompakte Schluesselnamen, keine Redundanz)

#### Ergebnis: Immer klein

| Nach | Ohne Kompression | Mit Kompression | Ersparnis |
|---|---|---|---|
| 100 Bilder | ~5 KB | ~3 KB | 40% |
| 1.000 Bilder | ~50 KB | ~8 KB | 84% |
| 10.000 Bilder | ~500 KB | ~25 KB | 95% |
| 100.000 Bilder | ~5 MB | ~80 KB | 98% |

**Die Datenbank bleibt immer unter 200 MB** — genug Platz fuer alle Parameter und gelernten LUTs ohne jemals ein Problem zu verursachen. 200 MB passen auf jede Festplatte und merkt kein Nutzer.

#### Wie die kompakte Datenbank aussieht

Statt jedes einzelne Feedback zu speichern, werden nur **aggregierte Parameter** gehalten:

```json
{
  "version": 2,
  "profiles": {
    "Landschaft": {
      "count": 142,
      "params": {
        "warmth": 0.72, "saturation": 0.45, "contrast": 0.58,
        "sharpness": 0.40, "noise": 0.30, "exposure": 0.10,
        "highlights": -0.15, "shadows": 0.25
      },
      "lut": "base64-encoded-3dlut-compact",
      "lastUpdated": "2026-05-24"
    },
    "Drohne": {
      "count": 88,
      "params": { "warmth": 0.80, "saturation": 0.55, ... },
      "lut": "base64-...",
      "lastUpdated": "2026-05-23"
    },
    "Portrait": {
      "count": 45,
      "params": { "warmth": 0.35, "saturation": 0.20, ... },
      "lut": "base64-...",
      "lastUpdated": "2026-05-22"
    },
    "Architektur": {
      "count": 12,
      "params": { "warmth": 0.10, "saturation": 0.15, ... },
      "lut": null,
      "lastUpdated": "2026-05-24"
    }
  },
  "globalPreference": {
    "defaultWarmth": 0.50,
    "defaultSaturation": 0.35,
    "defaultContrast": 0.45
  }
}
```

Jeder Szenen-Typ hat: **count** (wie viele Bewertungen), **params** (aggregierte Durchschnittswerte), **lut** (gelernte Farbkurve, komprimiert), **lastUpdated** (wann zuletzt aktualisiert).

**Keine rohen Feedback-Historie. Keine riesigen Arrays. Nur kompakte Durchschnittswerte.**

#### Automatisches Backup

Beim Compacting wird automatisch ein Backup erstellt:
- `profile.flipsicolor-style` — aktuelle kompakte Version
- `profile.flipsicolor-style.bak` — vorherige Version (Falls etwas schiefgeht)

Backup wird beim naechsten Compacting ueberschrieben. Nie mehr als 2 Versionen auf der Festplatte.

5.4e Stil-Reset — "Von vorne anfangen"

In den Einstellungen gibt es einen **Reset-Button** für das Stil-Profil:

```
┌─────────────────────────────────────────────────────────────┐
│  ⚙️ Einstellungen → KI-Stil                                │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Aktives Profil: [Meine Drohne ▼]                         │
│  Konfidenz: 🟢 Hoch (142 Feedbacks)                       │
│  Lernphase: ✅ Abgeschlossen                                │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  🔄 Stil zurücksetzen                               │   │
│  │                                                     │   │
│  │  Was wird zurückgesetzt?                            │   │
│  │  ☑ Gelernte Parameter (Wärme, Sättigung, etc.)     │   │
│  │  ☑ Gelernte Szenen-Überschreibungen                │   │
│  │  ☑ Gelernte LUT                                    │   │
│  │  ☐ Profil-Name behalten                            │   │
│  │                                                     │   │
│  │  ⚠️  Nach dem Reset startet die Lernphase von       │   │
│  │  vorne (0/30 Feedbacks). Die KI muss Ihren Stil    │   │
│  │  neu lernen.                                        │   │
│  │                                                     │   │
│  │  [Reset bestätigen]    [Abbrechen]                 │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  📤 Stil exportieren                                │   │
│  │  → .flipsicolor-style Datei speichern               │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  📥 Stil importieren                                │   │
│  │  → .flipsicolor-style Datei laden (z.B. von Kollegen)│   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

**Was der Reset macht:**
- Alle gelernten Parameter (Wärme, Sättigung, Kontrast, etc.) → auf 0.0 zurückgesetzt
- Alle Szenen-Überschreibungen → gelöscht
- Gelernte 3D-LUT → gelöscht
- Feedback-Historie → gelöscht
- Konfidenz → 0 (Lernphase startet von vorne)
- Profil-Name → kann behalten oder gelöscht werden (Checkbox)

**Export/Import:** Stil-Profile können als `.flipsicolor-style` Datei exportiert werden. Perfekt zum Teilen mit Kollegen oder Backup vor einem Reset.

### 5.5 Stil-Profile verwalten

Sie können mehrere Stil-Profile erstellen:
- **"Meine Drohne"** — warm, kontrastreich, Landschafts-optimiert
- **"Portraits"** — weich, hautfreundlich, niedriger Kontrast
- **"Nachtleuchten"** — kühl, stark, Details in Schatten
- **"Instagram-Vibes"** — knallig, gesättigt, vignettiert
- **+ Neues Profil erstellen** (startet Ask-Mode für dieses Profil)

Jedes Profil lernt unabhängig. Sie können Profile exportieren und mit Kollegen teilen (.flipsicolor-style Datei).

### 5.6 Stil-Transfer auf Video

Wenn das Stil-Profil gelernt hat (ab ~30 👍👎), wird es automatisch auf Videos angewendet:

1. **Stil-Profil laden** → gelernte Parameter und LUT
2. **Ersten Frame analysieren** → Szene erkennen
3. **Szenen-spezifischen Look anwenden** → aus dem Profil
4. **Alle weiteren Frames** → konsistent mit Stil-Parameter + Konsistenz-Engine

Ergebnis: Ihre Drohnen-Videos sehen aus wie **Sie** es machen würden. Nicht wie ein generischer Filter.

### 5.7 Ask-Mode im Video-Kontext

Bei Videos funktioniert der Ask-Mode so:

1. KI korrigiert das Video automatisch (Farbkonsistenz-Engine aktiv)
2. Zeigt Vorher/Nachher des **Representative Frame** (bestes Bild aus dem Video)
3. **👍** → Video wird mit diesen Einstellungen exportiert
4. **👎** → KI generiert eine neue Variante (z.B. wärmer, kühler, mehr Kontrast)
5. Optional: Quick-Tag oder Freitext

Das spart enorm Zeit — statt 1000 Frames einzeln zu bewerten, bewerten Sie **ein** repräsentatives Frame und die KI wendet es konsistent auf das ganze Video an

---

## 6. Bildbearbeitung — Professioneller Workflow

### 6.1 RAW-Unterstützung (alles integriert, LibRaw)

CR2/CR3 (Canon) · NEF/NRW (Nikon) · ARW/SRF (Sony) · DNG · RAF (Fuji) · ORF (Olympus) · RW2 (Panasonic) · HEIF/HEIC (Apple)

### 6.2 Auto-Enhance — Der Profi-Workflow, automatisiert

Ein Profi bearbeitet in dieser Reihenfolge. FlipsiColor macht genau das, automatisch:

1. **Weißabgleich** — KI erkennt Beleuchtung, korrigiert Farbstich
2. **Belichtung** — Schatten aufhellen, Highlights schützen, Histogramm optimieren
3. **Kontrast** — Tonwertkurve anpassen, Tiefe und Lichter
4. **Farbkorrektur** — Farbstich entfernen, Sättigung anpassen
5. **Rauschunterdrückung** — NAFNet, adaptiv (nur wo Rauschen ist)
6. **Schärfung** — Kanten betonen, Flächen glatt lassen
7. **Log → Rec.709** — Falls Log-Profil erkannt (Drohne, Kamera)
8. **Stil-LUT** — Gelernter persönlicher Look als Abschluss

### 6.3 Intensität: Leicht / Mittel / Stark

Nicht jeder will dass die KI alles übernimmt. Manchmal reicht ein sanfter Schubs, manchmal soll die KI komplett durchziehen. Dafür gibt es drei Intensitätsstufen:

#### ⚪ Leicht — "Nur ein Anstoß"

Die KI macht nur das Absolute Minimum. Alles andere bleibt wie vom Fotograf/von der Kamera aufgenommen.

| Was die KI macht | Was NICHT gemacht wird |
|---|---|
| ✅ Weißabgleich — nur leicht korrigieren (max ±200K) | ❌ Keine Belichtungsänderung |
| ✅ Leichte Belichtungsoptimierung — nur ±0.3 EV | ❌ Kein Kontrast-Eingriff |
| ✅ Log → Rec.709 — falls Log-Profil erkannt | ❌ Keine Rauschunterdrückung |
| ✅ Objektiv-Korrektur — Verzeichnung entfernen | ❌ Keine Schärfung (außer Objektiv-Korrektur) |
| — | ❌ Keine Farbkorrektur/Betonung |
| — | ❌ Keine Sättigungsänderung |
| — | ❌ Keine Vignettierung |

**Für wen:** Fotografen die ihre Kamera-Einstellungen mögen und nur minimale Korrekturen wollen. Das Bild bleibt wie es ist — nur sauberer.

#### 🟡 Mittel — "Professioneller Touch" (Default)

Die KI macht was ein Profi machen würde — sachlich, nicht übertrieben. Das Bild sieht "richtig" aus, nicht "bearbeitet".

| Was die KI macht | Was NICHT gemacht wird |
|---|---|
| ✅ Weißabgleich — vollständige Korrektur | ❌ Keine stilistische Färbung |
| ✅ Belichtung — optimale Histogramm-Verteilung | ❌ Keine extreme Kontrast-S-Curve |
| ✅ Kontrast — leicht angehoben, natürlich | ❌ Kein Clarity/Rauch-Effekt |
| ✅ Farbkorrektur — Sättigung leicht angepasst | ❌ Keine Haut-Glättung |
| ✅ Rauschunterdrückung — adaptiv, schonend | ❌ Keine Über-Schärfung |
| ✅ Schärfung — Kanten betont, Flächen glatt | ❌ Keine Vignettierung |
| ✅ Log → Rec.709 | ❌ Keine Teal-Orange-Spaltung |
| ✅ Objektiv-Korrektur | ❌ Kein HDR-Look |

**Für wen:** Die meisten Nutzer. Das Bild sieht aus als hätte ein Profi die Farben eingestellt — natürlich, sauber, nicht übertrieben.

#### 🔴 Stark — "KI übernimmt komplett"

Die KI macht alles. Von Weißabgleich bis zum fertigen Look. Wie ein Colorist der das Bild von Grund auf bearbeitet.

| Was die KI macht | Detail |
|---|---|
| ✅ Weißabgleich | Vollständig, inkl. Farbstich-Entfernung |
| ✅ Belichtung | Shadow-Recovery, Highlight-Schutz, Histogramm-Optimierung |
| ✅ Kontrast | S-Curve, Tiefe/Lichter-Trennung |
| ✅ Farbkorrektur | Volle Sättigungsanpassung, Farbtonverschiebung |
| ✅ Rauschunterdrückung | Stärkere NAFNet-Anwendung, auch bei moderatem Rauschen |
| ✅ Schärfung | Stärkere Kantenschärfung |
| ✅ Log → Rec.709 | Ja, automatiskt |
| ✅ Stil-LUT | Gelernter persönlicher Look wird angewendet |
| ✅ Szenen-Anpassung | Nacht → mehr Schattendetail, Landschaft → mehr Grün-Sättigung |
| ✅ Hautton-Schutz | Hauttöne werden bewahrt bei starker Farbkorrektur |

**Für wen:** Nutzer die einfach ein perfektes Ergebnis wollen ohne sich um Details zu kümmern. "Rein → Fertig → Raus."

#### Intensität im Auto-Enhance Button

```
┌─────────────────────────────────────────────┐
│  ⚡ Auto-Enhance                  [Mittel ▼] │
│                                              │
│  Dropdown:                                   │
│  ⚪ Leicht  — Nur sanfte Korrektur          │
│  🟡 Mittel  — Professioneller Touch         │
│  🔴 Stark   — KI übernimmt komplett         │
│                                              │
│  Letzte Wahl wird gespeichert              │
└─────────────────────────────────────────────┘
```

Die Intensität wirkt sich auch auf den **Ask-Mode** aus:
- **Leicht + 👎**: KI probiert Mittel und zeigt beide Varianten
- **Mittel + 👎**: KI probiert Stark und zeigt beide Varianten
- **Stark + 👎**: KI generiert alternative Looks (z.B. wärmer, kühler, mehr Kontrast)

### 6.4 Was die KI übernimmt vs. was ein Profi manuell macht

| Profin-Arbeit | FlipsiColor KI |
|---|---|
| Weißabgleich per Graukarte/Pipette | SCI erkennt Neutralgrau automatisch, berücksichtigt gelernten Stil |
| Belichtung per Histogramm + Auge | CNN analysiert Histogramm + Szenen-Typ + persönliche Vorliebe |
| Rauschmasken per Hand | NAFNet erkennt Rauschmuster adaptiv |
| Schärfung per USM-Regler | Kantenerkennung + adaptiv: Kanten scharf, Flächen glatt |
| Farbstil per Erfahrung | 3D-LUT die aus 50+ gelernten Bearbeitungen besteht |
| Shot-Matching per Auge | Histogramm-Matching an Reference-Frame |
| Hauttöne schützen | YCbCr-Hauttondetektion + Schutzmaske |

---

## 7. Videofarbkorrektur — Kein Schnitt, nur Farbe

### 7.1 Kernprinzip

FlipsiColor macht **Farbkorrektur**, nicht Videoschnitt. Keine Timeline, keine Clips, keine Transitions.

### 7.2 Video-Pipeline

```
Video (MP4/MOV/MKV)
    │
    ├── FFmpeg HW-Decode (NVDEC/QSV/VideoToolbox)
    │
    ├── Scene Detection (Szenenwechsel erkennen)
    │
    ├── Frame 1 → Reference Frame wählen
    │            → KI-Pipeline + Stil-Profil anwenden
    │            → Korrektur-Parameter extrahieren
    │
    ├── Frame 2..N → gleiche Parameter + Histogramm-Matching an Reference
    │                 (+ temporal smoothing für Konsistenz)
    │                 (+ Hautton-Schutz)
    │
    └── FFmpeg HW-Encode (NVENC/QSV/VideoToolbox)
        → Output: H.265/ProRes/DNxHR
```

### 7.3 Log-Profil-Erkennung (automatisch, kein Dropdown nötig)

| Methode | Wie | Genauigkeit |
|---|---|---|
| EXIF/MakerNotes | Kamera-Tag → Profil zuordnen | 95% bei bekannten Kameras |
| Dateiname-Pattern | `DJI_` → D-Log M, `C0` → C-Log | 80% |
| KI-Histogramm | "Sieht aus wie Log-Footage" → Profil vorschlagen | 90%+ |
| Automatische Korrektur | Einfach machen — KI erkennt und korrigiert | Best Effort |

**Wichtig: Der User muss NICHTS auswählen.** Wenn es Log-Footage ist, erkennt die KI das und korrigiert automatisch. Wenn der User override will → Dropdown im UI.

### 7.4 Unterstützte Log-Profile

| Log-Profil | Hersteller | Technische LUT (integriert) |
|---|---|---|
| D-Log | DJI (ältere Drohnen) | D-Log → Rec.709 ✅ |
| D-Log M | DJI (Mini 3/4 Pro, Action, Osmo, Pocket) | D-Log M → Rec.709 ✅ |
| C-Log / C-Log2 / C-Log3 | Canon | → Rec.709 ✅ |
| S-Log2 / S-Log3 | Sony | → Rec.709 ✅ |
| V-Log | Panasonic | → Rec.709 ✅ |
| F-Log | Fujifilm | → Rec.709 ✅ |
| N-Log | Nikon | → Rec.709 ✅ |
| Apple Log | iPhone 15 Pro+ | → Rec.709 ✅ |
| REDLogFilm | RED | → Rec.709 ✅ |
| Blackmagic Film | BMD | → Rec.709 ✅ |
| ARRI Log C | ARRI | → Rec.709 ✅ |

Alle LUTs sind **in der App integriert** — kein Download, keine extra Dateien.

---

## 8. Nicht-destruktive Bearbeitung — Original bleibt immer sicher

### 8.1 Kernprinzip

**Das Original wird NIEMALS verändert.** Jede Bearbeitung ist ein Parameter-Stack der auf das Original angewendet wird. Wie in Lightroom — Sie können jederzeit alles ändern, zurücknehmen, oder verschiedene Versionen vergleichen.

```
Original (DJI_0001.DNG) ← WIRD NIEMALS ANGETASTET
    |
    +-- Parameter-Stack:
    |   +-- Weißabgleich: +120K (automatisch erkannt)
    |   +-- Belichtung: +0.3 EV
    |   +-- Kontrast: +12
    |   +-- Rauschunterdrückung: Stärke 0.6
    |   +-- Schärfung: Stärke 0.4
    |   +-- Log → Rec.709: D-Log M (automatisch erkannt)
    |   +-- Stil-LUT: "Meine Drohne" (gelernt)
    |
    +-- Vorschau = Original + Parameter-Stack (in Echtzeit berechnet)
```

### 8.2 Was das bedeutet

| Aktion | Andere Software | FlipsiColor |
|---|---|---|
| Belichtung ändern | Pixeldaten verändert, irreversibel | Parameter-Wert geändert, Original sicher |
| Nachträglich anpassen | Nur möglich mit Undo-Stack | Immer möglich, Parameter sind editierbar |
| Verschiedene Versionen | Datei duplizieren | Mehrere Parameter-Sets pro Bild |
| Original wiederherstellen | Nur mit Backup | Ein Klick: "Auf Original zurücksetzen" |
| Batch-Bearbeitung | Änderungen unwiderruflich | Alle Parameter auf alle Bilder, jederzeit änderbar |

### 8.3 Projekt-Datei (.flipsicolor)

Die .flipsicolor-Datei speichert **nur Parameter** — die Original-Bilder bleiben dort wo sie sind. Projekte sind portabel und klein (wenige KB).

---

## 9. Objektiv-Korrektur — Für ALLE Kameras und Objektive

### 9.1 Lensfun: 500+ Kamera/Objektiv-Profile

FlipsiColor nutzt **Lensfun** (Open-Source) mit **500+ Profilen**. Nicht nur DJI — jede Kamera:

| Hersteller | Kameras | Objektive |
|---|---|---|
| **DJI** | Air 2S, Air 3, Air 3S, Mini 3/4 Pro, Mavic 3 | Alle DJI-Objektive |
| **Canon** | EOS R5/R6/R3, 5D Mark IV | EF, RF, TS-E |
| **Sony** | A7 IV/V, A1, FX3 | FE, E-Mount |
| **Nikon** | Z6 III, Z8, Z9 | Z-Mount, F-Mount |
| **Fujifilm** | X-T5, GFX 100S | XF, GF |
| **Panasonic** | S5 II, GH6 | L-Mount |
| **GoPro** | Hero 12/13 | Fixed |
| **iPhone** | 15/16 Pro | Ultrawide, Tele |
| **100+ weitere** | ... | ... |

Automatisch: EXIF auslesen → Profil finden → korrigieren. Kein Dropdown nötig.

Korrigiert: Radiale Verzeichnung, Vignettierung, Chromatische Aberration.

---

## 10. Farbraum-Management — Keine verwaschenen Farben mehr

FlipsiColor managed Farbräume **durchgehend** — von Import bis Export, mit LCMS2 und ICC-Profilen.

Unterstützt: sRGB, Adobe RGB, Display P3, Rec.2020, DCI-P3, ProPhoto RGB (intern).

Monitor-ICC-Profil wird automatisch erkannt → Farben werden korrekt angezeigt. Eingebettete ICC-Profile in Bildern werden automatisch verwendet.

---

## 11. Batch-Verarbeitung — "Ordner rein, fertig raus"

### 11.1 Export-Ordner — Wohin damit?

Jede Aktion braucht ein Ziel. Der Export-Ordner wird **immer** separat vom Quell-Ordner gewählt — niemals werden Original-Bilder überschrieben.

**Export-Ordner Optionen:**
- **Eigenen Ordner wählen** — z.B. `/Export/2026-05-23_Wachau/` (Default)
- **Quell-Ordner-Struktur beibehalten** — Unterordner wie im Quell-Ordner erstellt
- **In Original-Ordner speichern** — mit `_bearbeitet` Suffix (nicht empfohlen)

Der Export-Ordner wird automatisch erstellt falls er nicht vorhanden ist. Original-Dateien werden **niemals** verändert.

### 11.2 Smart Batch

Quell-Ordner wählen, Export-Ordner wählen, KI übernimmt den Rest. Im Ask-Mode: erst 12 Vorschau-Bilder → 👍👎 → dann Rest automatisch. Video-Batch genauso. Turbo-Modus: Ordner rein → KI ohne Fragen → Export fertig.

---

## 12. Export-Profile

Vordefiniert: Instagram Feed/Stories, YouTube 4K/1080p, Print (Adobe RGB/TIFF), Web (WebP), Drone-Export, Social Media Video, Archiv (verlustfrei).

Eigene Profile: Name, Auflösung, Farbraum, Format, Codec — speicherbar und teilbar.

---

## 13. Systemanforderungen & GPU-Pflicht

**GPU ist Pflicht** für KI-Funktionen und Video.

| | Minimum (Bilder) | Empfohlen | Optimal |
|---|---|---|---|
| **GPU** | Dedicated 4 GB VRAM | RTX 3060 / RX 6600 XT | RTX 4070+ / RX 7800 XT+ |
| **GPU-Alt** | Intel Arc A380 | Mac M1 | Mac M2 Pro+ / M3+ |
| **RAM** | 8 GB | 16 GB | 32 GB |
| **CPU** | 4-Kern | 8-Kern | 8+ Kern |

GPU-Support: NVIDIA (CUDA, DirectML), AMD (Vulkan, DirectML), Apple Silicon (Metal/MPS), Intel Arc (experimentell).

Intel UHD/Iris: **Nur manuelle Bildbearbeitung** — keine KI, kein Video. App warnt beim Start.

---

## 14. Auto-Update für KI-Modelle

Modelle verbessern sich → Hintergrund-Update bei App-Start (GitHub Releases API). Kleine Updates automatisch, große nachfragen. SHA-256-Hash-Verifikation. Lokal gecacht unter ~/.flipsicolor/models/.

---

## 15. UI-Konzept — Native Qt6/QML Dark Theme

### 15.1 Design-Philosophie

**"Dunkel, fokussiert, professionell"** — wie DaVinci Resolve, aber auf das Wesentliche reduziert.

- Dark-Theme (Fotografen-Umgebung)
- Akzentfarbe: Orange/Bernstein (#FF8C00)
- Maximale Bildfläche, minimale UI
- Vorher/Nachher-Vergleich: Split-Screen mit Drag-Divider

### 15.2 Hauptfenster — Seitenleiste mit Bild/Video-Switch

Die UI hat eine kompakte Seitenleiste links — nur Icons, kein Text. Bild- und Video-Modus sind sauber getrennt. Ein Klick auf das jeweilige Icon switcht den gesamten Inhalt.

```
+--+------------------------------------------------+------------------+
|  |  Open   Export   Auto   Style                  |                  |
|  +------------------------------------------------+                  |
|Pi|                                                | CORRECTIONS      |
|  |     +--------------------------+              |                  |
|Vi|     |                          |              |  O White Balance  |
|  |     |    Before <-> After      |              |  O Exposure       |
|Se|     |                          |              |  O Contrast        |
|  |     +--------------------------+              |  O Color           |
|Fo|                                                |  O Noise           |
|  |  FILE LIST (Images or Videos)                 |  O Sharpness       |
|  |    IMG_0001.CR3                               |  O Vignette        |
|  |    IMG_0002.CR3                               |  O LUT/Look        |
|  |    IMG_0003.CR3                               |                    |
|  |    IMG_0004.CR3                               |  [Light]          |
|  |                                                |  [Medium *]       |
|  |                                                |  [Strong]         |
+--+------------------------------------------------+------------------+
| Image Mode | KI learned (142) | GPU: RTX 4070 | 4K                  |
+--------------------------------------------------+------------------+
```

#### Seitenleiste — Nur Icons, kein Text

| Icon | Modus | Was passiert |
|---|---|---|
| Pi (Bild-Icon) | **Bild** | Datei-Liste zeigt Bilder, Korrekturen auf Einzelbild, Vorschau Vorher/Nachher. Ordner-Auswahl direkt im Reiter. |
| Vi (Video-Icon) | **Video** | Datei-Liste zeigt Videos, Korrekturen auf gesamtes Video, Timeline+Player. Ordner-Auswahl direkt im Reiter. |
| Se (Zahnrad-Icon) | **Einstellungen** | KI-Modi, Export, GPU, Stil-Reset, etc. |

**Kein extra Ordner-Icon in der Seitenleiste!** Der Quell-Ordner wird direkt im jeweiligen Reiter (Bild oder Video) ausgewaehlt — dort wo man arbeitet. Der Export-Ordner wird in den Einstellungen konfiguriert.

**Klick auf Bild-Icon → Bild-Modus:**
- Quell-Ordner-Auswahl direkt oben im Reiter
- Datei-Liste zeigt nur Bilder (JPEG, PNG, RAW, DNG)
- Vorschau: Einzelbild Vorher/Nachher
- Korrekturen auf dieses eine Bild
- Batch: alle Bilder im Ordner bearbeiten

**Klick auf Video-Icon → Video-Modus:**
- Quell-Ordner-Auswahl direkt oben im Reiter
- Datei-Liste zeigt nur Videos (MP4, MOV, AVI, MKV)
- Vorschau: Video-Player mit Timeline, Scrubbing
- Korrekturen auf gesamtes Video (frame-by-frame)
- Farb-Konsistenz-Engine aktiv
- Log-Profil-Erkennung

#### Video-Modus Detail

```
+--+------------------------------------------------+------------------+
|  |  Open   Export   Auto   Style                  |                  |
|  +------------------------------------------------+                  |
|Pi|                                                | CORRECTIONS      |
|  |     +--------------------------+              |                  |
|Vi|     |    VIDEO PREVIEW         |              |  O White Balance  |
|  |     |    > DJI_0001.MP4       |              |  O Exposure       |
|Se|     |                          |              |  O Contrast        |
|  |     +--------------------------+              |  O Log Profile     |
|Fo|                                                |  O Color           |
|  |  < > || =====o===============| 0:42/3:15     |  O Noise           |
|  |                                                |  O Consistency      |
|  |  FILE LIST (Videos)                            |                    |
|  |    DJI_0001.MP4    4K  | 3:15                 |                    |
|  |    DJI_0002.MP4    4K  | 2:48                 |                    |
|  |    DJI_0003.MP4    1080| 1:33                 |                    |
+--+------------------------------------------------+------------------+
| Video Mode | Consistency ON | GPU: RTX 4070 | 4K                     |
+--------------------------------------------------+------------------+
```

### 15.3 Auto-Enhance Button ⚡

Der wichtigste Button in der ganzen App. Ein Klick:

1. Bild analysieren (Szenen-Typ, Belichtung, Rauschen)
2. Stil-Profil laden (gelernte Vorlieben)
3. Konsistente Parameter berechnen
4. Anwenden
5. Fertig — kein Nachjustieren nötig

### 15.4 Stil-Profile (🎨)

Dropdown-Menü:
- **"Automatisch Lernen"** (Default) — KI lernt mit jedem Bild
- **"Meine Drohne"** — warm, kontrastreich, Landschaft
- **"Portraits"** — weich, hautfreundlich
- **"Kino-Look"** — Teal-Orange, filmisch
- **"Minimalistisch"** — clean, entsättigt
- **+ Neues Profil erstellen**

---

### 15.5 Einstellungen (⚙️) — Vollständige Übersicht

Die Einstellungen sind in klare Kategorien unterteilt. Jede Kategorie hat ihre eigene Seite in der Settings-Ansicht.

#### Allgemein

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| Sprache | Dropdown | Deutsch | Deutsch, Englisch (mehr geplant) |
| Thema | Dropdown | Dunkel | Dunkel, Hell, System |
| Akzentfarbe | Farbwähler | Orange/Bernstein (#FF8C00) | Anpassbar |
| Automatischer Update-Check | Checkbox | Ein | Bei Start auf neue Version prüfen |
| KI-Modelle Update | Dropdown | Automatisch | Automatisch, Nachfragen, Nie |
| KI-Modelle Speicherpfad | Pfad | Standard | Wo die ONNX-Modelle liegen |

#### KI & Lernen

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| KI-Modus | Dropdown | Ask-Mode | Ask-Mode (👍👎), Smart-Learn (lernend), Turbo (keine Fragen) |
| Intensität | Dropdown | Mittel | Leicht, Mittel, Stark |
| Stil-Profile | Liste | — | Verwalten: "Meine Drohne", "Portraits", etc. |
| Aktives Profil | Dropdown | Automatisch | Welches Stil-Profil standardmaeßig genutzt wird |
| Konfidenz-Anzeige | Checkbox | Ein | Statusleiste zeigt Lernfortschritt (z.B. "42/60") |
| Neuen Szenen-Typ fragen | Checkbox | Ein | Bei unbekanntem Motiv:fragen oder beste Schätzung |

#### Stil-Reset & Export

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| Stil zurücksetzen | Button | — | Alle gelernten Parameter loeschen, Lernphase startet neu |
| Stil exportieren | Button | — | Als .flipsicolor-style Datei speichern |
| Stil importieren | Button | — | .flipsicolor-style Datei laden |
| Reset-Optionen | Checkboxen | Alle an | Was zurueckgesetzt wird: Parameter, Szenen, LUT, Historie, Profil-Name behalten |

#### Bild

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| RAW-Verarbeitung | Dropdown | Automatisch | Automatisch, LibRaw-Einstellungen manuell |
| Objektiv-Korrektur | Checkbox | Ein | Automatisch aus EXIF erkennen und korrigieren |
| Farbraum-Verwaltung | Checkbox | Ein | ICC-Profile erkennen und konvertieren |
| Vorschau-Farbraum | Dropdown | Monitor-ICC | Monitor-ICC, sRGB, Adobe RGB, Display P3 |
| Nicht-destruktiv | Info | — | (Immer aktiv — Original wird nie veraendert) |

#### Video

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| Farb-Konsistenz | Checkbox | Ein | Frames an Reference-Frame anpassen |
| Konsistenz-Staerke | Slider | 70% | Wie stark die Anpassung ist (0%=aus, 100%=strikt) |
| Szenen-Erkennung | Dropdown | Automatisch | Szenenwechsel automatisch erkennen |
| Szenen-Erkennung Empfindlichkeit | Slider | Mittel | Niedrig, Mittel, Hoch (wie schnell Szenenwechsel erkannt werden) |
| Hautton-Schutz | Checkbox | Ein | Hautfarben vor Ueber-korrektur schuetzen |
| Video-Codec | Dropdown | H.265 | H.264, H.265, AV1, ProRes (Export) |
| Video-Aufloesung | Dropdown | Original | Original, 4K, 1080p, 720p |

#### Export

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| Export-Ordner-Modus | Dropdown | Eigenen Ordner waehlen | Eigener Ordner / Im Quell-Ordner (Unterordner `_bearbeitet`) |
| Standard-Export-Ordner | Pfad | ~/Export | Wo bearbeitete Dateien landen (nur bei "Eigener Ordner") |
| Ordner-Struktur beibehalten | Checkbox | Ein | Unterordner aus Quell-Ordner nachbilden |
| Unterordner-Name | Textfeld | _bearbeitet | Name des Unterordners im Quell-Ordner (nur bei "Im Quell-Ordner") |
| Dateiname-Schema | Dropdown | Original_bearbeitet | Original_bearbeitet, Original_edit, Zeitstempel |
| Bild-Format | Dropdown | JPEG | JPEG, PNG, WebP, TIFF |
| JPEG-Qualitaet | Slider | 95 | 70-100% |
| Bild-Aufloesung | Dropdown | Original | Original, Benutzerdefiniert |
| Export-Profile | Liste | — | Vordefiniert + eigene Profile verwalten |

**Export-Ordner-Modus erklaert:**
- **Eigener Ordner**: Export in einen separaten Ordner (z.B. `/Export/2026-05-23/`) — Original-Ordner bleibt sauber
- **Im Quell-Ordner**: Export in Unterordner im Quell-Ordner (z.B. `/Drohne/2026-05-23/_bearbeitet/`) — alles am selben Ort

#### GPU & Leistung

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| GPU-Backend | Dropdown | Auto | Auto (CUDA > Vulkan > Metal > DirectML), Manuell waehlen |
| VRAM-Limit | Slider | 80% | Wie viel VRAM die KI nutzen darf (30-100%) |
| Parallele Bild-Verarbeitung | Dropdown | Auto | Auto (nach CPU/GPU), 1, 2, 4 Threads |
| Vorschau-Qualitaet | Dropdown | Mittel | Niedrig (schnell), Mittel, Hoch (langsam) |
| Cache-Groesse | Slider | 5 GB | Maximaler Cache fuer bearbeitete Bilder |
| Cache loeschen | Button | — | Bearbeiteten Cache loeschen |

---



## 16. Vorher/Nachher — Live-Ansicht

**Immer Original links, bearbeitet rechts.** Kein Umschalten, kein Toggle. Man sieht sofort was die KI macht — in Echtzeit, bei jedem Bild.

### 16.1 Seitenverhaeltnis anpassbar

Der Trennstreifen in der Mitte ist ziehbar — man kann ihn nach links oder rechts schieben um mehr vom Original oder mehr vom bearbeiteten Bild zu sehen.

```
+---------------------------+-----------------------------+
|                           |                             |
|      ORIGINAL             |      BEARBEITET             |
|                           |                             |
|    (unberuehrt, roh)      |    (KI-Enhanced, live)      |
|                           |                             |
|                           |                             |
+===========================+<-- ziehbarer Trennstreifen --+
|                           |                             |
|  ISO: 800 | f/2.8        |  Weißabgleich: +200K        |
|  1/500s | 24mm           |  Belichtung: +0.3 EV        |
|  DJI Air 3S | D-Log M    |  Kontrast: +15              |
+---------------------------+-----------------------------+
```

Linke Seite zeigt immer das **unbearbeitete Original** — Pixel für Pixel, so wie die Kamera es aufgenommen hat. Rechte Seite zeigt das **aktuelle KI-Ergebnis live** — Parameter aendern sich, das Bild aktualisiert sich sofort.

### 16.2 EXIF-Daten Anzeige

Unter dem Bild die wichtigsten EXIF-Daten als kompakte Zeile:
- **Original-Seite**: ISO, Blaende, Verschlusszeit, Brennweite, Kamera, Objektiv, Log-Profil
- **Bearbeitet-Seite**: Welche Korrekturen die KI angewendet hat (Weißabgleich +200K, Belichtung +0.3EV, etc.)

### 16.3 Video-Vorher/Nachher

Im Video-Modus dasselbe Prinzip: Links Original-Frame, Rechts bearbeiteter Frame. Timeline-Steuerelement unten. Scrubben zeigt beide Seiten synchron.

---

## 17. Undo/Redo — Nicht-destruktiv heisst immer umkehrbar

Da FlipsiColor nicht-destruktiv arbeitet (Original wird nie veraendert), ist Undo/Redo auf Parameter-Ebene:

| Aktion | Was passiert |
|---|---|
| **Ctrl+Z** | Letzte Parameter-Aenderung zuruecknehmen |
| **Ctrl+Y** | Letzte Undo wiederherstellen |
| **Ctrl+Shift+Z** | Alle Parameter auf Original zuruecksetzen |

Undo-Historie pro Bild gespeichert. Neues Bild = neue Historie. Turbo-Modus hat keine Undo-Historie (Batch-Verarbeitung).

---

## 18. Metadaten & Privatsphaere — EXIF selbst entscheiden

### 18.1 EXIF-Verhalten beim Export

In den Einstellungen konfigurierbar:

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| EXIF behalten | Checkbox | Ein | Kamera-Daten, Datum, Aufnahme-Parameter behalten |
| GPS-Daten entfernen | Checkbox | **Ein** | Standort-Daten aus EXIF loeschen (Privatsphaere) |
| Kamera-Modell behalten | Checkbox | Ein | Kamera- und Objektiv-Name behalten |
| FlipsiColor-Pro-Tag | Checkbox | Aus | "Bearbeitet mit FlipsiColor" in EXIF einfuegen |
| Thumbnail erneuern | Checkbox | Ein | EXIF-Thumbnail auf bearbeitetes Bild aktualisieren |

**Default: EXIF behalten, GPS entfernen.** So bleiben Kamera-Daten erhalten aber der Standort ist geschuetzt — besonders wichtig fuer Drohnen-Fotos und Social Media.

---

## 19. Wasserzeichen — Professionell und unaufdraenglich

### 19.1 Wasserzeichen-Art

| Art | Beschreibung | Vorschau |
|---|---|---|
| **Text** | Name, URL, Logo-Text | © Fabian Kirchweger |
| **Bild/Logo** | PNG mit Transparenz | Eigenes Logo-File |
| **Schriftzug** | Wiederkennbarer Stil | Brush-Signatur, Monogramm |

### 19.2 Einstellungen

In den Einstellungen unter "Export → Wasserzeichen":

| Einstellung | Typ | Default | Beschreibung |
|---|---|---|---|
| Wasserzeichen aktiv | Checkbox | Aus | Standardmaessig aus, an wenn man es braucht |
| Art | Dropdown | Text | Text, Bild/Logo, Schriftzug |
| Text | Textfeld | © [Name] | Wasserzeichen-Text (Platzhalter: {name}, {year}, {copyright}) |
| Logo-Datei | Pfad | — | PNG-Datei mit Transparenz (nur bei Art=Bild) |
| Position | Dropdown | Unten rechts | 9 Positionen (oben/mtte/unten x links/mitte/rechts) |
| Groesse | Slider | 5% | Prozentsatz der Bildbreite (2-20%) |
| Opazitaet | Slider | 30% | Durchsichtigkeit (10-80%) |
| Abstand | Slider | 15px | Abstand vom Rand |
| Schriftart | Dropdown | Montserrat | Montserrat, Roboto, Open Sans, systemspezifisch |
| Schriftfarbe | Farbe | Weiss | Weiss, Schwarz, Benutzerdefiniert |
| Schatten | Checkbox | Ein | Leichter Schatten fuer Lesbarkeit auf jedem Hintergrund |

### 19.3 Export-Profil-Bindung

Wasserzeichen kann **pro Export-Profil** unterschiedlich sein:
- **Instagram**: © {name} unten rechts, 5%, 30% Opazitaet
- **YouTube**: Kein Wasserzeichen
- **Print**: Kein Wasserzeichen
- **Web**: Logo unten links, 8%, 40% Opazitaet
- **Archiv**: Kein Wasserzeichen

---

## 20. Drag & Drop — Einfach Bilder reinziehen

Ordner und Bilder koennen direkt in die App gezogen werden:

| Drag & Drop | Was passiert |
|---|---|
| Ordner reinziehen | Quell-Ordner gesetzt, alle Bilder/Videos geladen |
| Einzelne Bilder reinziehen | Zu aktueller Sitzung hinzufuegen |
| .flipsicolor-Datei reinziehen | Projekt oeffnen mit allen Parametern |

Unterstuetzt: Ordner, Einzel-Dateien, .flipsicolor-Projekte. Funktioniert auf dem Bildbereich, der Datei-Liste, und dem App-Icon (System-Dock/Taskbar).

---

## 21. Erster Start — Willkommen bei FlipsiColor

### 21.1 Start-Reihenfolge

Die App startet in dieser Reihenfolge — **kein Login, kein Account, kein Abo:**

1. **KI-Modelle laden** — SCI (Weißabgleich, 15MB), NAFNet (Denoise, 50MB) etc. werden heruntergeladen falls nicht vorhanden. Fortschrittsbalken: "Lade KI-Modelle... (3/8)"
2. **Willkommen-Screen** — Kurze Vorstellung: "FlipsiColor — KI-gestuetzte Farbkorrektur, lokal, privat."
3. **Tour** — 4 Schritte durch die App:
   - Schritt 1: "Waehle einen Ordner" → Ordner-Icon blinkt
   - Schritt 2: "KI bearbeitet automatisch" → Auto-Enhance blinkt
   - Schritt 3: "Bewerte mit 👍👎" → Ask-Mode erklaert
   - Schritt 4: "Exportiere" → Export-Button blinkt
4. **Fertig** — App ist bereit. Tour kann uebersprungen werden.

### 21.2 Nach dem ersten Start

Ab dem zweiten Start: Direkt zur App. Kein Willkommensscreen, keine Tour. KI-Modelle werden im Hintergrund gecheckt (nur bei Update heruntergeladen).

---

## 22. Tastaturkuerzel — Fuer Profis

| Kuerzel | Aktion | Kontext |
|---|---|---|
| **Ctrl+Z** | Undo | Letzte Parameter-Aenderung zurueck |
| **Ctrl+Y** | Redo | Undo wiederherstellen |
| **Ctrl+Shift+Z** | Alles zuruecksetzen | Alle Parameter auf Original |
| **Ctrl+E** | Export | Aktuelles Bild/Video exportieren |
| **Ctrl+O** | Ordner oeffnen | Quell-Ordner waehlen |
| **Leertaste** | Vorher/Nachher Toggle | Linke Seite kurz auf bearbeitet schalten |
| **+/-** | Zoom rein/raus | Bild vergroessern |
| **0** | 100% zoom | Originalgroesse |
| **F** | Vollbild | Vorschau im Vollbild-Modus |
| **Pfeil Links/Rechts** | Naechstes/Vorheriges Bild | Bild-Navigation |
| **1** | Intensitaet: Leicht | Schnellwechsel |
| **2** | Intensitaet: Mittel | Schnellwechsel |
| **3** | Intensitaet: Stark | Schnellwechsel |
| **Ctrl+1/2/3** | Turbo/Ask/Smart-Learn | KI-Modus wechseln |
| **Ctrl+B** | Batch starten | Alle Bilder im Ordner bearbeiten |
| **Ctrl+T** | Turbo starten | Turbo-Modus starten |

---

## 23. Kern-Komponenten (C++ Architektur)

```
┌─────────────────────────────────────────────────┐
│                   QML UI Layer                  │
├─────────────────────────────────────────────────┤
│              ApplicationController               │
├────────┬────────┬──────────┬──────────┬─────────┤
│ Image  │ Video  │   AI     │   LUT    │ Style  │
│ Engine │ Engine │ Pipeline │ Manager  │ Learner│
├────────┴────────┴──────────┴──────────┴─────────┤
│              Core Processing Library             │
│  OpenCV │ LibRaw │ FFmpeg │ ONNX Runtime │ LCMS2 │
├─────────────────────────────────────────────────┤
│              Platform Abstraction                │
│    CUDA/DML/Metal │ Qt6 │ vcpkg │ CMake        │
└─────────────────────────────────────────────────┘
```

### Neue Komponenten für v1.1

```cpp
// Farb-Konsistenz im Video
class VideoColorConsistencyEngine {
    std::vector<SceneBoundary> detectScenes(const VideoFrames& frames);
    FrameRef findReferenceFrame(const Scene& scene);
    cv::Mat matchToReference(const cv::Mat& frame, const cv::Mat& reference);
    void smoothTransitions(std::vector<CorrectionParams>& params, int window = 5);
    cv::Mat protectSkinTones(const cv::Mat& corrected, const cv::Mat& original);
};

// Stil-Lernsystem
class StyleLearner {
    void learnFromEdit(const Image& before, const Image& after, const SceneType& scene);
    StyleProfile getCurrentProfile() const;
    void saveProfile(const std::string& name);
    void loadProfile(const std::string& name);
    CorrectionParams applyStyle(const Image& image, const SceneType& scene) const;
    float getConfidence() const;  // 0.0 (keine Daten) bis 1.0 (sehr sicher)
};

// Log-Profil-Erkennung
class LogProfileDetector {
    LogProfile detectFromMetadata(const VideoMeta& meta);
    LogProfile detectFromHistogram(const cv::Mat& frame);
    LogProfile detectAuto(const VideoFile& video);  // kombiniert alles
};
```

---

## 24. Projektstruktur

```
flipsicolor/
├── CMakeLists.txt
├── vcpkg.json                    # Dependencies: OpenCV, LibRaw, FFmpeg, ONNX, LCMS2, Lensfun
├── src/
│   ├── main.cpp
│   ├── app/
│   │   ├── ApplicationController.h/.cpp
│   │   ├── SettingsManager.h/.cpp
│   │   └── ModelDownloader.h/.cpp     # Erst-Start KI-Modelle Download
│   ├── engine/
│   │   ├── ImageEngine.h/.cpp
│   │   ├── VideoEngine.h/.cpp
│   │   ├── AIPipeline.h/.cpp
│   │   ├── LUTManager.h/.cpp
│   │   ├── ExportEngine.h/.cpp
│   │   ├── RawDecoder.h/.cpp
│   │   ├── LogProfileDetector.h/.cpp
│   │   ├── ColorSpaceConverter.h/.cpp
│   │   ├── LensCorrection.h/.cpp          # Lensfun Integration
│   │   ├── VideoColorConsistencyEngine.h/.cpp  # NEU
│   │   ├── StyleLearner.h/.cpp                  # NEU
│   │   ├── BatchProcessor.h/.cpp               # Batch-Verarbeitung
│   │   └── ExportProfileManager.h/.cpp          # Export-Profile
│   ├── ai/
│   │   ├── ONNXRuntimeProvider.h/.cpp
│   │   ├── SceneAnalyzer.h/.cpp
│   │   ├── WhiteBalanceCorrector.h/.cpp
│   │   ├── NoiseReducer.h/.cpp
│   │   ├── ExposureCorrector.h/.cpp
│   │   ├── Sharpener.h/.cpp
│   │   └── FaceRestorer.h/.cpp
│   ├── qml/
│   │   ├── Main.qml
│   │   ├── ImagePreview.qml
│   │   ├── VideoTimeline.qml
│   │   ├── ToolPanel.qml
│   │   ├── StyleProfilePanel.qml       # NEU
│   │   ├── ColorWheel.qml
│   │   ├── Histogram.qml
│   │   └── components/
│   │       ├── PFSlider.qml
│   │       ├── PFButton.qml
│   │       ├── PFPanel.qml
│   │       └── BeforeAfter.qml
│   └── resources/
│       ├── icons/
│       ├── lensfun/                  # Lensfun DB (integriert)
│       ├── luts/                    # Log-Profil LUTs (integriert)
│       │   ├── dji_dlog.cube
│       │   ├── dji_dlogm.cube
│       │   ├── canon_clog3.cube
│       │   ├── sony_slog3.cube
│       │   └── ... (alle 11+ Profile)
│       ├── models/                  # ONNX-Modelle (lazy-download)
│       └── styles/
│           └── dark.qml
├── tests/
└── .github/workflows/              # CI/CD Win/Mac/Linux
```

---

## 25. Phasenplan

| Phase | Inhalt | Dauer |
|---|---|---|
| **1** | Basis-UI, JPEG/PNG laden/speichern, manuelle Slider (Belichtung, Kontrast, etc.), Export | 8-10 Wochen |
| **2** | RAW-Support (LibRaw), KI-Modelle (Weißabgleich, Rauschen, Belichtung), Auto-Enhance | 6-8 Wochen |
| **3** | LUTs & 14 Log-Profile, Log-Erkennung, Stil-Lernsystem (Basis) | 6-8 Wochen |
| **3.5** | **Farb-Konsistenz-Engine** (Reference-Frame, Histogramm-Matching, Temporal Smoothing) | 4-5 Wochen |
| **4** | Video-Pipeline (FFmpeg decode/encode), frame-by-frame, Hautton-Schutz | 6-8 Wochen |
| **5** | Upscaling (Real-ESRGAN), Face Restoration (CodeFormer), Batch-Verarbeitung | 5-6 Wochen |
| **6** | Polish, Lokalisierung (DE/EN), Installer-Paketierung | 3-4 Wochen |

**Gesamt: ~32-39 Wochen**

---

## 26. Was macht FlipsiColor anders?

| Feature | Lightroom | DaVinci Resolve | FlipsiColor |
|---|---|---|---|
| Zero-Setup | ❌ (Adobe Account nötig) | ❌ (Komplex) | ✅ Installieren → fertig |
| KI-Auto-Enhance | ✅ (basic) | ❌ (manuell) | ✅ (Profi-Workflow) |
| Stil lernt mit | ❌ | ❌ | ✅ Persönliches Profil |
| Farb-Konsistenz Video | ❌ (nur Bilder) | ✅ (aber manuell) | ✅ (automatisch) |
| Log-Profil-Erkennung | ❌ | ✅ (manuell) | ✅ (automatisch) |
| Drohnen-Fokus | ❌ | ❌ | ✅ D-Log M etc. integriert |
| Lokal, kein Abo | ❌ (Cloud) | ✅ (aber $300+) | ✅ (einmalig) |
| RAW-Support | ✅ | ✅ | ✅ |
| Videos | ❌ | ✅ | ✅ (nur Farbe) |
| Objektiv-Korrektur | ✅ (manuell) | ❌ | ✅ (Lensfun, 500+ Profile, auto) |
| Nicht-destruktiv | ✅ | ✅ | ✅ (Parameter-Stack) |
| Farbraum-Management | ❌ | ✅ | ✅ (LCMS2, ICC, P3) |
| Batch-Verarbeitung | ✅ (basic) | ❌ | ✅ (Smart Batch + Ask + Turbo) |
| Export-Ordner wählbar | ✅ | ✅ | ✅ (immer separat vom Quell-Ordner) |
| GPU-beschleunigt | ❌ | ✅ | ✅ (CUDA/Vulkan/Metal) |
| GPU-Pflicht | ❌ | ✅ (opt.) | ✅ (für KI+Video) |
| Auto-Update KI | ❌ | ❌ | ✅ (Hintergrund) |

---

*FlipsiColor — Konzept v1.2 — Fabian Kirchweger 2026*