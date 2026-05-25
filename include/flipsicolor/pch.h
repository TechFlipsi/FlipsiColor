// FlipsiColor — Präcompiler-Header (MSVC C++20 <ctime>-Fix)
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later
//
// MSVC C++20-Workaround: <ctime> MUSS vor Qt/ONNX-Headern geladen werden.
// Auf MSVC schlägt <ctime> fehl, wenn vorher schon C++-Header geladen wurden,
// die Symbole in den std-Namespace bringen (Qt, OpenCV, ONNX Runtime).
// Wir inkludieren <time.h> (C-Header) direkt — das umgeht den Bug, da
// <time.h> die Symbole korrekt in den globalen Namespace bringt, und
// <ctime> danach nur noch using-Deklarationen versucht (die aber durch
// die bereits existierenden Symbole im globalen Namespace nun klappen).

#ifndef FLIPSICOLOR_PCH_H
#define FLIPSICOLOR_PCH_H

// C-Header VOR C++-Headern — umgeht MSVC <ctime>-Bug
// <time.h> definiert Symbole im globalen Namespace (wo sie hingehören)
// <ctime> versucht sie dann via using ins std:: zu verschieben — klappt jetzt
#include <time.h>
#include <errno.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#endif // FLIPSICOLOR_PCH_H