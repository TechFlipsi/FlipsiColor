// FlipsiColor — Szenen-Erkennung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QList>
#include <QObject>

namespace flipsicolor
{

    class SceneDetector : public QObject
    {
        Q_OBJECT

    public:
        explicit SceneDetector(QObject* parent = nullptr);

        void       frameAnalysieren(int frameNummer, double aehnlichkeit);
        QList<int> szenenWechsel() const;
        void       zuruecksetzen();

        void   setSchwellwert(double wert) { m_schwellwert = wert; }
        double schwellwert() const { return m_schwellwert; }

    signals:
        void szenenWechselErkannt(int frameNummer);

    private:
        double     m_schwellwert = 0.3;
        QList<int> m_szenenWechsel;
    };

}  // namespace flipsicolor
