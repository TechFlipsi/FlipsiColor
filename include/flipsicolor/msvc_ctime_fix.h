// FlipsiColor — MSVC <ctime>-Workaround
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later
//
// MSVC 14.44 C++20 Bug: Sowohl <time.h> als auch <ctime> definieren
// clock_t, time_t, tm, asctime, ctime etc. NUR im std-Namespace,
// NICHT im globalen Namespace. using-Deklarationen wie
// "using ::clock_t" oder <ctime>'s interne using-Deklarationen
// schlagen mit C2039/C2873 fehl weil die Symbole nicht im globalen
// Namespace existieren.
//
// Lösung: _CTIME_ definieren (MSVC's Include-Guard für <ctime>),
// sodass <ctime> NIEMALS geladen wird. Die Symbole sind bereits
// über <time.h> im std-Namespace verfügbar — std::clock_t, std::time_t
// etc. funktionieren problemlos, nur der globale Namespace ist leer.
//
// Auf GCC/Clang: einfach <ctime> normal inkludieren (kein Bug dort).
//
// Dieser Header wird per /FI-Flag VOR allen anderen Headern gezwungen
// und wirkt auf ALLE Übersetzungseinheiten inkl. AUTOMOC-generierte Dateien.

#ifndef FLIPSICOLOR_CTIME_FIX_H
#define FLIPSICOLOR_CTIME_FIX_H

#ifdef _MSC_VER

// <time.h> inkludieren — definiert clock_t, time_t, tm, timespec etc.
// ABER: Bei MSVC C++20 landen diese NUR im std-Namespace, nicht global!
// D.h. ::clock_t gibt es NICHT, aber std::clock_t funktioniert.
#include <time.h>

// _CTIME_ ist der Include-Guard von MSVC's <ctime>.
// Durch Definieren wird verhindert, dass <ctime> jemals geladen wird,
// da dessen using-Deklarationen (die aus dem globalen Namespace importieren
// wollen) fehlschlagen würden. Die Symbole sind bereits via <time.h>
// im std-Namespace verfügbar.
#ifndef _CTIME_
#define _CTIME_
#endif

#else
// GCC/Clang: kein Bug, einfach <ctime> normal inkludieren
#include <ctime>
#endif

#endif  // FLIPSICOLOR_CTIME_FIX_H