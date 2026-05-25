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
// Dieser Header MUSS vor allen Qt/OpenCV/ONNX-Headern inkludiert werden.
// Auf MSVC wird er per /FI-Flag vor jede Übersetzungseinheit gezwungen.
// In .cpp-Dateien wird er automatisch per MSVC_FIX include geladen.

#ifndef FLIPSICOLOR_CTIME_FIX_H
#define FLIPSICOLOR_CTIME_FIX_H

#ifdef _MSC_VER
// C-Header inkludieren — definiert clock_t, time_t, tm, timespec etc.
// im globalen Namespace. Danach manuell in std importieren.
#include <time.h>

// Manuelle using-Deklarationen — ersetzt <ctime>'s fehlgeschlagene
// using-Deklarationen, die bei MSVC C2039/C2873 werfen.
namespace std {
    using ::clock_t;
    using ::time_t;
    using ::tm;
    using ::size_t;
    // C-Funktionen in std importieren
    using ::clock;
    using ::difftime;
    using ::mktime;
    using ::time;
    using ::asctime;
    using ::ctime;
    using ::gmtime;
    using ::localtime;
    using ::strftime;
#if _HAS_CXX17
    using ::timespec;
#endif
}

// VERHINDERE, dass <ctime> später inkludiert wird und die Fehler wirft.
// _CTIME_ definieren, sodass <ctime>'s Include-Guard den Header überspringt.
#ifndef _CTIME_
#define _CTIME_
#endif

#else
// GCC/Clang: kein Bug, einfach <ctime> normal inkludieren
#include <ctime>
#endif

#endif // FLIPSICOLOR_CTIME_FIX_H