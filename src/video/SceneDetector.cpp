// FlipsiColor — Szenen-Erkennung Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

// FlipsiColor — MSVC <ctime>-Fix: <time.h> VOR allen anderen Headern inkludieren,
// da MSVC 14.44 sonst C2039/C2873 in <ctime> wirft, wenn Drittanbieter-Header
// (Qt, OpenCV, ONNX Runtime) vor <time.h>/<ctime> geladen werden.
#if defined(_MSC_VER)
#include <time.h>
#endif

#include <flipsicolor/video/SceneDetector.h>
#include <QDebug>

namespace flipsicolor {

SceneDetector::SceneDetector(QObject* parent)
    : QObject(parent)
    , m_schwellwert(0.3)
{
}

void SceneDetector::frameAnalysieren(int frameNummer, double aehnlichkeit)
{
    if (aehnlichkeit > m_schwellwert) {
        m_szenenWechsel.append(frameNummer);
        emit szenenWechselErkannt(frameNummer);
        qDebug() << "Szenenwechsel bei Frame" << frameNummer << "(Aehnlichkeit:" << aehnlichkeit << ")";
    }
}

QList<int> SceneDetector::szenenWechsel() const
{
    return m_szenenWechsel;
}

void SceneDetector::zuruecksetzen()
{
    m_szenenWechsel.clear();
}

} // namespace flipsicolor
