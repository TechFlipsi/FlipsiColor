// FlipsiColor — Auto-Updater Header
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <memory>
#include <QDateTime>
#include <QObject>
#include <QString>
#include <QTimer>
#include <QVersionNumber>

namespace flipsicolor
{

    /// Prüft GitHub Releases auf neue Versionen und signalisiert verfügbare Updates.
    /// Startet automatisch beim App-Start (nach 30s Delay) und danach alle 24h.
    /// Download und Installation werden an den jeweiligen Plattform-Installer delegiert.
    /// Downgrade-Schutz: Niemals auf ältere oder gleiche Version downgraden.
    class AutoUpdater : public QObject
    {
        Q_OBJECT
        Q_PROPERTY(bool updateVerfuegbar READ updateVerfuegbar NOTIFY updateVerfuegbarChanged)
        Q_PROPERTY(QString neueVersion READ neueVersion NOTIFY neueVersionChanged)
        Q_PROPERTY(QString aenderungen READ aenderungen NOTIFY aenderungenChanged)
        Q_PROPERTY(QString downloadUrl READ downloadUrl NOTIFY downloadUrlChanged)
        Q_PROPERTY(qint64 downloadGroesse READ downloadGroesse NOTIFY downloadGroesseChanged)

    public:
        /// Update-Kanal für Beta-Tester
        enum class UpdateKanal
        {
            Stable = 0,  ///< Nur stabile Releases
            Beta   = 1   ///< Auch Pre-releases
        };
        Q_ENUM(UpdateKanal)

        explicit AutoUpdater(QObject* parent = nullptr);
        ~AutoUpdater();

        /// Manuelle Prüfung auf Updates (z.B. Button-Klick)
        Q_INVOKABLE void pruefen();

        /// Update herunterladen und installieren
        Q_INVOKABLE void updateStarten();

        /// Update-Prüfung überspringen (User hat "Später erinnern" geklickt)
        Q_INVOKABLE void spaeterErinnern();

        /// Update dauerhaft ignorieren bis zur nächsten verfügbaren Version
        Q_INVOKABLE void ignorieren();

        // Properties
        bool    updateVerfuegbar() const;
        QString neueVersion() const;
        QString aenderungen() const;
        QString downloadUrl() const;
        qint64  downloadGroesse() const;

        /// Setzt den Update-Kanal (Stable oder Beta)
        void        setKanal(UpdateKanal kanal);
        UpdateKanal kanal() const;

    signals:
        void updateVerfuegbarChanged(bool verfuegbar);
        void neueVersionChanged(const QString& version);
        void aenderungenChanged(const QString& text);
        void downloadUrlChanged(const QString& url);
        void downloadGroesseChanged(qint64 groesse);
        void pruefungFertig(bool updateGefunden, const QString& version);
        void downloadFortschritt(qint64 empfangen, qint64 gesamt, double prozent);
        void downloadFertig(const QString& dateiPfad);
        void fehler(const QString& meldung);

    private:
        class Impl;
        std::unique_ptr<Impl> m_impl;
    };

}  // namespace flipsicolor