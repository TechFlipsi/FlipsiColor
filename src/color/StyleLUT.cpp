// FlipsiColor — Stil-Lernen Implementierung (AiLUT-Transform)
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

// FlipsiColor — MSVC <ctime>-Fix: Workaround für MSVC 14.44 Bug C2039/C2873.
// Inkludiert <time.h> und importiert Symbole manuell in std-Namespace,
// OHNE <ctime> zu inkludieren (das die Fehler auslöst).
// Siehe: include/flipsicolor/msvc_ctime_fix.h für Details.
#if defined(_MSC_VER)
#include "flipsicolor/msvc_ctime_fix.h"
#endif

#include "flipsicolor/color/StyleLUT.h"

#include <QDebug>
#include <QFile>
#include <QDir>
#include <QStandardPaths>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

namespace flipsicolor {

StyleLUT::StyleLUT(QObject* parent)
    : QObject(parent)
{
}

cv::Mat StyleLUT::stilAnwenden(const cv::Mat& bild, const QString& szenenTyp, float staerke) const
{
    // TODO: AiLUT-Transform ONNX Inferenz
    // 1) Bild durch CNN → Basis-LUT-Gewichte
    // 2) Mehrere Basis-LUTs mit Gewichten kombinieren
    // 3) Bild durch resultierende LUT transformieren
    // 4) Staerke-Blending mit Original
    Q_UNUSED(szenenTyp)
    Q_UNUSED(staerke)
    return bild.clone();
}

void StyleLUT::feedbackAufzeichnen(const TrainingsPaar& paar, bool positiv)
{
    Q_UNUSED(paar)
    m_feedbackAnzahl++;
    emit feedbackAnzahlChanged();

    // Lernrunde-Wechsel prüfen
    int alteRunde = (m_feedbackAnzahl - 1) < 60 ? 1 : ((m_feedbackAnzahl - 1) < 120 ? 2 : 0);
    int neueRunde = lernRunde();
    if (alteRunde != neueRunde) {
        emit lernRundeChanged();
        emit lernPhaseAbgeschlossen(alteRunde);
    }

    // Vielfalt-Check
    if (brauchtVielfalt()) {
        qDebug() << "Lernphase: Bitte verschiedene Szenen bewerten!";
    }
}

} // namespace flipsicolor
