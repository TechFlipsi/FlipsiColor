// FlipsiColor — Präcompiler-Header (MSVC C++20 <ctime>-Fix)
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later
//
// MSVC C++20-Workaround: <ctime> MUSS vor allen Qt-/ONNX-Headern inkludiert
// werden, da MSVC sonst "symbol cannot be used in a using-declaration" wirft,
// wenn <ctime> nach Headern kommt, die Symbole in den std-Namespace bringen.

#ifndef FLIPSICOLOR_PCH_H
#define FLIPSICOLOR_PCH_H

// C-Compat-Header VOR C++-Headern — verhindert MSVC C2039/C2873
#include <ctime>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cmath>
#include <cstddef>
#include <cerrno>

#endif // FLIPSICOLOR_PCH_H