// FlipsiColor — SettingsPanel Stub (wird implementiert)
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

// FlipsiColor — MSVC <ctime>-Fix: <time.h> VOR allen anderen Headern inkludieren,
// da MSVC 14.44 sonst C2039/C2873 in <ctime> wirft, wenn Drittanbieter-Header
// (Qt, OpenCV, ONNX Runtime) vor <time.h>/<ctime> geladen werden.
#if defined(_MSC_VER)
#include <time.h>
#endif

#include "flipsicolor/ui/SettingsPanel.h"

namespace flipsicolor {

// TODO: Implementierung

} // namespace flipsicolor
