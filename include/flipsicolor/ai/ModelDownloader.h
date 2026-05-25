// FlipsiColor — Modell-Downloader
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <memory>
#include <QObject>
#include <QString>

namespace flipsicolor
{

    class ModelDownloader : public QObject
    {
        Q_OBJECT

    public:
        explicit ModelDownloader(QObject* parent = nullptr);
        ~ModelDownloader();

        void herunterladen(const QString& modellId, const QString& url, const QString& erwarteterSha256 = QString());

    signals:
        void fortschritt(const QString& modellId, qint64 empfangen, qint64 gesamt);
        void herunterladenFertig(const QString& modellId, const QString& dateiPfad);
        void fehler(const QString& modellId, const QString& fehlermeldung);

    private:
        struct Impl;
        std::unique_ptr<Impl> m_impl;
    };

}  // namespace flipsicolor
