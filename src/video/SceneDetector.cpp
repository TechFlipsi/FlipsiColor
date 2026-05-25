// FlipsiColor — Szenen-Erkennung Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include "flipsicolor/utils/Logger.h"
#include <flipsicolor/video/SceneDetector.h>
#include <QDebug>

namespace flipsicolor
{

    SceneDetector::SceneDetector(QObject* parent) : QObject(parent), m_schwellwert(0.3) {}

    void SceneDetector::frameAnalysieren(int frameNummer, double aehnlichkeit)
    {
        if ( aehnlichkeit > m_schwellwert )
        {
            m_szenenWechsel.append(frameNummer);
            emit szenenWechselErkannt(frameNummer);
            fcDebug("Szene") << "Szenenwechsel bei Frame" << frameNummer << "(Aehnlichkeit:" << aehnlichkeit << ")";
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

}  // namespace flipsicolor
