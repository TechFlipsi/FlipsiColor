// FlipsiColor — MSVC <ctime>-Workaround
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later
//
// MSVC 14.44 Bug: <ctime> wirft C2039/C2873, wenn Drittanbieter-Header
// (Qt, OpenCV, ONNX Runtime) Symbole in den std-Namespace bringen, bevor
// <ctime>'s using-Deklarationen ausgeführt werden können.
//
// Lösung: <time.h> (C-Header) inkludieren und die benötigten Symbole
// manuell in den std-Namespace importieren, OHNE <ctime> zu inkludieren.
// Auf GCC/Clang inkludiert dieser Header einfach <ctime> (kein Bug dort).
//
// Dieser Header wird per /FI-Flag VOR allen anderen Headern gezwungen
// und wirkt auf ALLE Übersetzungseinheiten inkl. AUTOMOC-generierte Dateien.

#ifndef FLIPSICOLOR_CTIME_FIX_H
#define FLIPSICOLOR_CTIME_FIX_H

#ifdef _MSC_VER

// ── C-Header inkludieren ──────────────────────────────────────────────────
// <time.h> definiert clock_t, time_t, tm, timespec etc. im globalen Namespace.
#include <time.h>

// ── Manuelle using-Deklarationen ───────────────────────────────────────────
// <ctime> würde diese via using-Deklaration importieren, schlägt aber mit
// C2039/C2873 fehl. Wir machen es manuell.
namespace std {
    // Typen
    using ::clock_t;
    using ::time_t;
    using ::tm;
#if _HAS_CXX17
    using ::timespec;
#endif

    // Funktionen
    using ::clock;
    using ::difftime;
    using ::mktime;
    using ::time;
    using ::asctime;
    using ::ctime;
    using ::gmtime;
    using ::localtime;
    using ::strftime;
}

// ── <ctime> Include-Guard setzen ────────────────────────────────────────────
// _CTIME_ ist der interne Include-Guard von MSVC's <ctime>.
// Durch Definieren wird verhindert, dass <ctime> jemals inkludiert wird
// (und die fehlerhaften using-Deklarationen auslöst).
#ifndef _CTIME_
#define _CTIME_
#endif

#else
// GCC/Clang: kein Bug, einfach <ctime> normal inkludieren
#include <ctime>
#endif

#endif // FLIPSICOLOR_CTIME_FIX_H