// FlipsiColor — MSVC <ctime>-Fix
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later
//
// MSVC C++20-Bug: <ctime> schlägt mit C2039/C2873 fehl, wenn vorher
// schon Symbole (durch Qt/OpenCV/ONNX) in den std-Namespace gebracht
// wurden. <ctime> versucht dann using-Deklarationen, die fehlschlagen,
// weil die Symbole bereits im std-Namespace existieren.
//
// Lösung: <time.h> (C-Header) inkludieren, der die Symbole korrekt im
// globalen Namespace definiert. Danach kann <ctime> die using-Deklarationen
// erfolgreich durchführen, da die Symbole im globalen Namespace bereits
// existieren und nicht nochmal importiert werden müssen.
//
// Dieser Header MUSS vor allen Qt/OpenCV/ONNX-Headern inkludiert werden.
// Auf MSVC wird er per /FI-Flag vor jede Übersetzungseinheit gezwungen.
//
// Auf nicht-MSVC-Systemen ist dieser Header ein no-op (leer).

#ifndef FLIPSICOLOR_PCH_H
#define FLIPSICOLOR_PCH_H

#ifdef _MSC_VER
// C-Header VOR C++-Headern — definiert clock_t/time_t/tm im globalen Namespace
#include <errno.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
// <ctime> muss NACH <time.h> kommen — jetzt funktionieren die using-Deklarationen
#include <cerrno>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <ctime>
#endif

#endif  // FLIPSICOLOR_PCH_H