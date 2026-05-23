# 🌍 FlipsiColor — Mehrsprachigkeit (i18n)

Deutsch ist die **Standardsprache** und Quellsprache aller UI-Texte.

## Verfügbare Sprachen

| Code | Sprache | Status |
|------|---------|--------|
| `de` | Deutsch 🇩🇪🇦🇹 | ✅ Standard (Quellsprache) |
| `en` | English 🇬🇧🇺🇸 | ✅ Übersetzt |
| `es` | Español 🇪🇸 | ✅ Übersetzt |
| `fr` | Français 🇫🇷 | ✅ Übersetzt |
| `it` | Italiano 🇮🇹 | ✅ Übersetzt |
| `pt_BR` | Português (Brasil) 🇧🇷 | ✅ Übersetzt |
| `ja` | 日本語 🇯🇵 | ✅ Übersetzt |
| `zh_CN` | 简体中文 🇨🇳 | ✅ Übersetzt |
| `ko` | 한국어 🇰🇷 | ✅ Übersetzt |

## Systemarchitektur

```
i18n/
├── flipsicolor_de.ts     ← Quellsprache (Deutsch)
├── flipsicolor_en.ts     ← Englisch
├── flipsicolor_es.ts     ← Spanisch
├── flipsicolor_fr.ts     ← Französisch
├── flipsicolor_it.ts     ← Italienisch
├── flipsicolor_pt_BR.ts  ← Brasilianisches Portugiesisch
├── flipsicolor_ja.ts     ← Japanisch
├── flipsicolor_zh_CN.ts  ← Vereinfachtes Chinesisch
└── flipsicolor_ko.ts     ← Koreanisch
```

### Build-Prozess

1. `Qt6::lrelease` kompiliert `.ts` → `.qm` (binäres Qt-Übersetzungsformat)
2. `.qm` Dateien werden ins Installationsverzeichnis `share/flipsicolor/i18n/` installiert
3. Zur Laufzeit lädt `main.cpp` die passende `.qm` Datei

### Sprachauswahl-Priorität

1. **Benutzereinstellung** (`QSettings("sprache")`) — höchste Priorität
2. **Systemsprache** (`QLocale().name()`) — automatisch erkannt
3. **Deutsch** (`de`) — Fallback wenn nichts passt

### Neue Sprache hinzufügen

1. Neue Datei `i18n/flipsicolor_XX.ts` erstellen (XX = ISO-Code)
2. `FLIPSICOLOR_LANGUAGES` in `CMakeLists.txt` ergänzen
3. `VERFUEGBARE_SPRACHEN` und `SPRACHNAMEN` in `main.cpp` ergänzen
4. Übersetzungen eintragen
5. Build → `.qm` wird automatisch generiert

### Qt Linguist verwenden

```bash
# Neue Strings aus QML/C++ extrahieren
lupdate src resources -ts i18n/flipsicolor_de.ts

# Mit Qt Linguist bearbeiten (GUI)
linguist i18n/flipsicolor_de.ts

# Kompilieren für Tests
lrelease i18n/flipsicolor_de.ts
```

## Konventionen

- **Quellsprache**: Deutsch — alle `qsTr()` und `tr()` Aufrufe verwenden deutsche Texte
- **`.en.md` Pattern**: Repository-Dokumente auf Deutsch (Hauptversion), Englisch als `.en.md` Parallelversion
- **Code-Bezeichner**: Deutsch (Methoden, Variablen, Enums) — `gpuVerfuegbar()`, `Intensitaet::Leicht`, etc.
- **CI/CD**: Englisch (Branch-Namen, Workflow-Syntax)
- **Commit-Nachrichten**: Englisch (`feat:`, `fix:`, etc.) — Branch-Konvention

## Qt-Kontexte

Die `.ts` Dateien verwenden folgende Qt-Kontexte:

| Kontext | Beschreibung |
|---------|-------------|
| `Application` | App-Name und Beschreibung |
| `Main` | Hauptnavigation (Bild, Video, Einstellungen) |
| `Settings` | Einstellungs-Panel (Sprache, Thema, KI, Export) |
| `ImageEditor` | Bild-Editor (Intensität, Modus, Regler) |
| `LearningUI` | Lernfortschritt und Feedback (👍👎) |
| `Export` | Export-Dialog (Format, Qualität, Farbraum) |
| `VideoEditor` | Video-Editor (Wiedergabe, Szenen-Erkennung) |
| `ModelManager` | KI-Modell-Downloads und Status |
| `GPU` | GPU-Erkennung und Warnungen |
| `FirstStart` | Willkommens-Assistent |