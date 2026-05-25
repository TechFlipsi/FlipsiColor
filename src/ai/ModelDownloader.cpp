// FlipsiColor — Modell-Downloader Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/ai/ModelDownloader.h>
#include <QNetworkAccessManager>
#include <QNetworkRequest>
#include <QNetworkReply>
#include <QFile>
#include <QDir>
#include <QStandardPaths>
#include <QDebug>

namespace flipsicolor {

class ModelDownloader::Impl {
public:
    QNetworkAccessManager networkManager;
    QString modellVerzeichnis;
};

ModelDownloader::ModelDownloader(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
    m_impl->modellVerzeichnis = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation) + "/models/";
    QDir().mkpath(m_impl->modellVerzeichnis);
}

ModelDownloader::~ModelDownloader() = default;

void ModelDownloader::herunterladen(const QString& modellId, const QString& url, const QString& erwarteterSha256)
{
    QString zielPfad = m_impl->modellVerzeichnis + modellId + ".onnx";

    // Bereits vorhanden?
    if (QFile::exists(zielPfad)) {
        // TODO: SHA256 prüfen
        emit herunterladenFertig(modellId, zielPfad);
        return;
    }

    QNetworkRequest anfrage{QUrl(url)};
    anfrage.setHeader(QNetworkRequest::UserAgentHeader, "FlipsiColor/v0.1.0");

    QNetworkReply* antwort = m_impl->networkManager.get(anfrage);

    connect(antwort, &QNetworkReply::downloadProgress, this,
        [this, modellId](qint64 empfangen, qint64 gesamt) {
            emit fortschritt(modellId, empfangen, gesamt);
        });

    connect(antwort, &QNetworkReply::finished, this,
        [this, antwort, modellId, zielPfad, erwarteterSha256]() {
            antwort->deleteLater();

            if (antwort->error() != QNetworkReply::NoError) {
                emit fehler(modellId, antwort->errorString());
                return;
            }

            QFile datei(zielPfad);
            if (!datei.open(QIODevice::WriteOnly)) {
                emit fehler(modellId, "Datei konnte nicht erstellt werden");
                return;
            }
            datei.write(antwort->readAll());
            datei.close();

            // TODO: SHA256 prüfen gegen erwarteterSha256

            qDebug() << "Modell heruntergeladen:" << modellId;
            emit herunterladenFertig(modellId, zielPfad);
        });
}

} // namespace flipsicolor
