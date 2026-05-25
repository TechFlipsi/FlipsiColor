// FlipsiColor — Logger
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QFile>
#include <QMutex>
#include <QObject>
#include <QString>

namespace flipsicolor
{

    // ── Log-Stufen ──────────────────────────────────────────────────
    enum class LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    };

    // ── Logger (Singleton, Thread-safe) ────────────────────────────
    class Logger : public QObject
    {
        Q_OBJECT

    public:
        [[nodiscard]] static Logger& instanz();

        void                   setStufe(LogLevel stufe);
        [[nodiscard]] LogLevel stufe() const;

        void loggen(LogLevel stufe, const QString& modul, const QString& nachricht);

        /// Qt Message-Handler — fängt qDebug/qWarning/qCritical/qFatal ab
        static void messageHandler(QtMsgType type, const QMessageLogContext& context, const QString& msg);

        /// Maximale Log-Dateigröße in MB (default: 5 MB)
        void setMaxDateiGroesse(qreal mb);
        /// Maximale Anzahl rotierter Log-Dateien (default: 3)
        void setMaxRotationen(int anzahl);

    signals:
        void logSignal(LogLevel stufe, const QString& modul, const QString& nachricht);

    private:
        Logger();
        ~Logger();

        Logger(const Logger&)            = delete;
        Logger& operator=(const Logger&) = delete;

        void schreiben(const QString& zeile);
        void rotieren();

        LogLevel m_stufe = LogLevel::Info;
        QFile    m_datei;
        QMutex   m_mutex;
        qreal    m_maxGroesse    = 5.0;  // MB
        int      m_maxRotationen = 3;
    };

}  // namespace flipsicolor

// ── Convenience-Makros ─────────────────────────────────────────────
// Verwendung: fcDebug("Modul") << "Nachricht";
//             fcInfo("Modul") << "Nachricht";
//             fcWarn("Modul") << "Nachricht";
//             fcErr("Modul") << "Nachricht";
//             fcFatal("Modul") << "Nachricht";
//
// Diese Makros erstellen temporäre LogStream-Objekte, die beim
// Zerstören automatisch die Nachricht an Logger::instanz() senden.

#include <QDebug>

namespace flipsicolor
{

    class LogStream
    {
    public:
        LogStream(LogLevel stufe, const QString& modul) : m_stufe(stufe), m_modul(modul) {}

        ~LogStream() { Logger::instanz().loggen(m_stufe, m_modul, m_buffer); }

        LogStream& operator<<(const QString& s)
        {
            m_buffer += s;
            return *this;
        }
        LogStream& operator<<(const char* s)
        {
            m_buffer += QString::fromUtf8(s);
            return *this;
        }
        LogStream& operator<<(int v)
        {
            m_buffer += QString::number(v);
            return *this;
        }
        LogStream& operator<<(qint64 v)
        {
            m_buffer += QString::number(v);
            return *this;
        }
        LogStream& operator<<(quint64 v)
        {
            m_buffer += QString::number(v);
            return *this;
        }
        LogStream& operator<<(double v)
        {
            m_buffer += QString::number(v, 'f', 2);
            return *this;
        }
        LogStream& operator<<(bool v)
        {
            m_buffer += v ? "true" : "false";
            return *this;
        }

    private:
        LogLevel m_stufe;
        QString  m_modul;
        QString  m_buffer;
    };

}  // namespace flipsicolor

#define fcDebug(modul) ::flipsicolor::LogStream(::flipsicolor::LogLevel::Debug, modul)
#define fcInfo(modul) ::flipsicolor::LogStream(::flipsicolor::LogLevel::Info, modul)
#define fcWarn(modul) ::flipsicolor::LogStream(::flipsicolor::LogLevel::Warning, modul)
#define fcErr(modul) ::flipsicolor::LogStream(::flipsicolor::LogLevel::Error, modul)
#define fcFatal(modul) ::flipsicolor::LogStream(::flipsicolor::LogLevel::Fatal, modul)

// Für QML: Direkter String-Log
#define fcLog(stufe, modul, nachricht) ::flipsicolor::Logger::instanz().loggen(stufe, modul, nachricht)