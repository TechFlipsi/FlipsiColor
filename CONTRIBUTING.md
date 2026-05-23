# Mitwirken an FlipsiColor

Vielen Dank für dein Interesse an einer Mitwirkung! Hier sind die Richtlinien.

[🇬🇧 English](CONTRIBUTING.en.md)

## Einrichtung

1. Forke das Repository
2. Klone deinen Fork: `git clone https://github.com/YOUR_USERNAME/FlipsiColor.git`
3. Erstelle einen Feature-Branch: `git checkout -b feature/mein-feature`
4. Bauen: `cmake -B build && cmake --build build`

## Code-Stil

- **C++20** — Verwende moderne C++-Features (Concepts, Ranges, Structured Bindings wo sinnvoll)
- **Namensgebung**: `PascalCase` für Klassen/Structs, `camelCase` für Methoden/Funktionen, `snake_case` für Variablen, `UPPER_CASE` für Konstanten
- **Header**: Verwende `#pragma once` als Include-Guard
- **Smart Pointer**: Bevorzuge `std::unique_ptr` / `std::shared_ptr` gegenüber rohem `new`/`delete`
- **Qt-Konventionen**: Signals/Slots mit der neuen Syntax (`&Class::method`)

## Commit-Nachrichten

Format: `type(scope): Beschreibung`

Typen: `feat`, `fix`, `docs`, `refactor`, `test`, `ci`, `chore`

Beispiele:
- `feat(ai): CodeFormer Gesichtswiederherstellung mit Fidelity-Weight hinzufügen`
- `fix(color): Clipping bei ProPhoto-RGB-zu-sRGB-Konvertierung korrigieren`
- `docs(sdp): Modellauswahl nach Benchmark-Überprüfung aktualisieren`

## Pull Requests

1. Stelle sicher, dass der Build erfolgreich ist: `cmake --build build --config Release`
2. Führe die Tests aus: `ctest --test-dir build`
3. Halte PRs fokussiert — ein Feature/Fix pro PR
4. Beschreibe, was geändert wurde und warum

## Issues

- Verwende GitHub Issues für Bugs und Feature-Requests
- Gib an: Betriebssystem, GPU, Treiberversion, Schritte zur Reproduktion
- Prüfe bestehende Issues, bevor du ein neues eröffnest