// FlipsiColor — Logger Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/utils/Logger.h>

#include <QCoreApplication>
#include <QDateTime>
#include <QDebug>
#include <QDir>
#include <QMutexLocker>
#include <QStandardPaths>
#include <QTextStream>

namespace flipsicolor
{

    // ── Stufen-Namen ────────────────────────────────────────────────
    static const char* stufenName(LogLevel stufe)
    {
        switch ( stufe )
        {
        case LogLevel::Debug:
            return "DEBUG";
        case LogLevel::Info:
            return "INFO ";
        case LogLevel::Warning:
            return "WARN ";
        case LogLevel::Error:
            return "ERROR";
        case LogLevel::Fatal:
            return "FATAL";
        }
        return "?????";
    }

    // ── Singleton ───────────────────────────────────────────────────
    Logger& Logger::instanz()
    {
        static Logger logger;
        return logger;
    }

    Logger::Logger() : m_stufe(LogLevel::Info)
    {
        QString logPfad = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
        QDir().mkpath(logPfad);
        m_datei.setFileName(logPfad + "/flipsicolor.log");
    }

    Logger::~Logger() = default;

    // ── Konfiguration ───────────────────────────────────────────────
    void Logger::setStufe(LogLevel stufe)
    {
        m_stufe = stufe;
    }

    LogLevel Logger::stufe() const
    {
        return m_stufe;
    }

    void Logger::setMaxDateiGroesse(qreal mb)
    {
        m_maxGroesse = mb;
    }

    void Logger::setMaxRotationen(int anzahl)
    {
        m_maxRotationen = anzahl;
    }

    // ── Loggen ──────────────────────────────────────────────────────
    void Logger::loggen(LogLevel stufe, const QString& modul, const QString& nachricht)
    {
        if ( stufe < m_stufe )
            return;

        QString zeile = QString("[%1] [%2] %3: %4")
                            .arg(QDateTime::currentDateTime().toString("yyyy-MM-dd hh:mm:ss.zzz"))
                            .arg(stufenName(stufe))
                            .arg(modul, -12)  // Links, 12 Zeichen breit
                            .arg(nachricht);

        // Konsole (farbig wäre schön, aber QDebug reicht)
        switch ( stufe )
        {
        case LogLevel::Debug:
            qDebug().noquote() << zeile;
            break;
        case LogLevel::Info:
            qInfo().noquote() << zeile;
            break;
        case LogLevel::Warning:
            qWarning().noquote() << zeile;
            break;
        case LogLevel::Error:
            qCritical().noquote() << zeile;
            break;
        case LogLevel::Fatal:
            qCritical().noquote() << zeile;
            break;
        }

        schreiben(zeile);
        emit logSignal(stufe, modul, nachricht);
    }

    // ── Datei-Schreiben (thread-safe) ──────────────────────────────
    void Logger::schreiben(const QString& zeile)
    {
        QMutexLocker locker(&m_mutex);

        // Rotieren wenn Datei zu groß
        if ( m_datei.exists() && m_datei.size() > static_cast<qint64>(m_maxGroesse * 1024 * 1024) )
            rotieren();

        if ( m_datei.open(QIODevice::Append | QIODevice::Text) )
        {
            QTextStream stream(&m_datei);
            stream << zeile << "\n";
            m_datei.close();
        }
    }

    // ── Log-Rotation ────────────────────────────────────────────────
    void Logger::rotieren()
    {
        // Älteste Rotation löschen
        QString altName = QString("%1/flipsicolor.log.%2")
                              .arg(QStandardPaths::writableLocation(QStandardPaths::AppDataLocation))
                              .arg(m_maxRotationen);
        QFile::remove(altName);

        // Existierende Rotationen aufrücken
        for ( int i = m_maxRotationen - 1; i >= 1; --i )
        {
            QString von = QString("%1/flipsicolor.log.%2")
                              .arg(QStandardPaths::writableLocation(QStandardPaths::AppDataLocation))
                              .arg(i);
            QString nach = QString("%1/flipsicolor.log.%2")
                               .arg(QStandardPaths::writableLocation(QStandardPaths::AppDataLocation))
                               .arg(i + 1);
            QFile::rename(von, nach);
        }

        // Aktuelle Log → .1
        QString rotation1 = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation) + "/flipsicolor.log.1";
        QFile::rename(m_datei.fileName(), rotation1);
    }

    // ── Qt Message-Handler ──────────────────────────────────────────
    // Fängt alle qDebug/qWarning/qCritical/qFatal Aufrufe ab
    // die NICHT über unsere Makros laufen (z.B. Qt-intern, ONNX, etc.)
    void Logger::messageHandler(QtMsgType type, const QMessageLogContext& context, const QString& msg)
    {
        // Modul aus Dateipfad extrahieren (nur Dateiname)
        QString modul = "Qt-Intern";
        if ( context.file )
        {
            QString datei = QString::fromUtf8(context.file);
            int     idx   = datei.lastIndexOf('/');
            if ( idx >= 0 )
                datei = datei.mid(idx + 1);
            modul = datei;
        }

        LogLevel stufe;
        switch ( type )
        {
        case QtDebugMsg:
            stufe = LogLevel::Debug;
            break;
        case QtInfoMsg:
            stufe = LogLevel::Info;
            break;
        case QtWarningMsg:
            stufe = LogLevel::Warning;
            break;
        case QtCriticalMsg:
            stufe = LogLevel::Error;
            break;
        case QtFatalMsg:
            stufe = LogLevel::Fatal;
            break;
        }

        instanz().loggen(stufe, modul, msg);
    }

}  // namespace flipsicolor