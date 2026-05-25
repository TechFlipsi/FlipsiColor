// FlipsiColor — Logger Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/utils/Logger.h>
#include <QDateTime>
#include <QDebug>
#include <QDir>
#include <QFile>
#include <QStandardPaths>
#include <QTextStream>

namespace flipsicolor
{

    Logger* Logger::instanz()
    {
        static Logger logger;
        return &logger;
    }

    Logger::Logger() : m_stufe(Stufe::Info)
    {
        QString logPfad = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
        QDir().mkpath(logPfad);
        m_datei.setFileName(logPfad + "/flipsicolor.log");
    }

    void Logger::loggen(Stufe stufe, const QString& modul, const QString& nachricht)
    {
        if ( stufe < m_stufe )
            return;

        QString stufenName;
        switch ( stufe )
        {
        case Stufe::Debug:
            stufenName = "DEBUG";
            break;
        case Stufe::Info:
            stufenName = "INFO";
            break;
        case Stufe::Warnung:
            stufenName = "WARN";
            break;
        case Stufe::Fehler:
            stufenName = "ERROR";
            break;
        }

        QString zeile = QString("[%1] [%2] %3: %4")
                            .arg(QDateTime::currentDateTime().toString("yyyy-MM-dd hh:mm:ss"))
                            .arg(stufenName)
                            .arg(modul)
                            .arg(nachricht);

        // Konsole
        if ( stufe >= Stufe::Warnung )
            qWarning().noquote() << zeile;
        else
            qDebug().noquote() << zeile;

        // Datei
        if ( m_datei.open(QIODevice::Append | QIODevice::Text) )
        {
            QTextStream stream(&m_datei);
            stream << zeile << "\n";
            m_datei.close();
        }
    }

}  // namespace flipsicolor
