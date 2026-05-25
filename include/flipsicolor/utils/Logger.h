// FlipsiColor — Logger
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QFile>
#include <QObject>
#include <QString>

namespace flipsicolor
{

    class Logger : public QObject
    {
        Q_OBJECT

    public:
        enum class Stufe
        {
            Debug,
            Info,
            Warnung,
            Fehler
        };

        [[nodiscard]] static Logger* instanz();

        void loggen(Stufe stufe, const QString& modul, const QString& nachricht);
        void setStufe(Stufe stufe) { m_stufe = stufe; }

    private:
        Logger();
        Stufe m_stufe = Stufe::Info;
        QFile m_datei;
    };

}  // namespace flipsicolor
