// FlipsiColor — KI-Modellverwaltung Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include "flipsicolor/ai/ModelManager.h"

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QStandardPaths>
#include <QDebug>
#include <QCryptographicHash>

// ONNX Runtime C API — verwendet anstelle des C++ Headers für Portabilität
#include <onnxruntime_c_api.h>

namespace flipsicolor {

// ── Manifest-Definition (eingebettet als Fallback) ──────────────────────────

static const QMap<ModellId, ModellInfo> FALLBACK_MANIFEST = {
    { ModellId::NAFNet, {
        "nafnet",
        "https://models.flipsicolor.techflipsi.com/nafnet_v1.onnx",
        "a1b2c3d4e5f6...nafnet_sha256_placeholder",
        17LL * 1024 * 1024,
        true
    }},
    { ModellId::RestormerLight, {
        "restormer_light",
        "https://models.flipsicolor.techflipsi.com/restormer_light_v1.onnx",
        "b2c3d4e5f6a1...restormer_sha256_placeholder",
        24LL * 1024 * 1024,
        true
    }},
    { ModellId::RealHATGAN, {
        "realhatgan",
        "https://models.flipsicolor.techflipsi.com/realhatgan_v1.onnx",
        "c3d4e5f6a1b2...realhatgan_sha256_placeholder",
        120LL * 1024 * 1024,
        false
    }},
    { ModellId::RealESRGAN, {
        "realesrgan",
        "https://models.flipsicolor.techflipsi.com/realesrgan_v1.onnx",
        "d4e5f6a1b2c3...realesrgan_sha256_placeholder",
        64LL * 1024 * 1024,
        false
    }},
    { ModellId::CodeFormer, {
        "codeformer",
        "https://models.flipsicolor.techflipsi.com/codeformer_v1.onnx",
        "e5f6a1b2c3d4...codeformer_sha256_placeholder",
        350LL * 1024 * 1024,
        false
    }},
    { ModellId::AiLUTTransform, {
        "ailut_transform",
        "https://models.flipsicolor.techflipsi.com/ailut_transform_v1.onnx",
        "f6a1b2c3d4e5...ailut_sha256_placeholder",
        8LL * 1024 * 1024,
        true
    }},
    { ModellId::EfficientNet, {
        "efficientnet",
        "https://models.flipsicolor.techflipsi.com/efficientnet_v1.onnx",
        "a1f6b2c3d4e5...efficientnet_sha256_placeholder",
        4608000LL,
        true
    }},
};

// ── ONNX Runtime C API Singleton ───────────────────────────────────────────

namespace {

static const OrtApi* g_ortApi = nullptr;

inline const OrtApi* ort()
{
    if (!g_ortApi) {
        g_ortApi = OrtGetApiBase()->GetApi(ORT_API_VERSION);
    }
    return g_ortApi;
}

} // namespace

// ── ModelManager Implementierung ───────────────────────────────────────────

ModelManager::ModelManager(QObject* parent)
    : QObject(parent)
{
    // Initialisiere ONNX Runtime C API
    ort();
}

ModelManager::~ModelManager()
{
    // Alle OrtSession-Objekte freigeben
    // m_sessions speichert OrtSession* als void*
    for (auto it = m_sessions.begin(); it != m_sessions.end(); ++it) {
        OrtSession* session = static_cast<OrtSession*>(it.value());
        if (session) {
            ort()->ReleaseSession(session);
        }
    }
    m_sessions.clear();
}

void ModelManager::manifestLaden()
{
    qDebug() << "ModelManager: Manifest wird geladen...";

    const QString basisPfad = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    const QString modellePfad = basisPfad + "/models";

    // Fallback-Manifest einlesen
    m_modelle = FALLBACK_MANIFEST;

    // Bereits heruntergeladene Modelle erkennen
    for (auto it = m_modelle.begin(); it != m_modelle.end(); ++it) {
        const ModellInfo& info = it.value();
        QString vollerPfad = modellePfad + "/" + info.id + ".onnx";

        QFileInfo fi(vollerPfad);
        if (fi.exists() && fi.size() == info.groesseBytes) {
            it->heruntergeladen = true;
            qDebug() << "  Modell bereits vorhanden:" << info.id;
        }
    }

    // Fehlende Core-Modelle asynchron herunterladen
    for (auto it = m_modelle.begin(); it != m_modelle.end(); ++it) {
        if (it.value().erforderlich && !it.value().heruntergeladen) {
            qDebug() << "  Core-Modell fehlt:" << it.value().id << "— starte Download...";
            modellHerunterladen(it.key());
        } else if (!it.value().erforderlich) {
            qDebug() << "  Optionales Modell:" << it.value().id << "— Lazy-Load";
        }
    }

    qDebug() << "ModelManager: Manifest geladen." << m_modelle.size() << "Modelle registriert.";
}

bool ModelManager::modellSicherstellen(ModellId id)
{
    if (!m_modelle.contains(id)) {
        qWarning() << "ModelManager: Unbekannte Modell-ID:" << static_cast<int>(id);
        return false;
    }

    ModellInfo& info = m_modelle[id];

    // Bereits geladen?
    if (info.heruntergeladen && m_sessions.contains(id)) {
        return true;
    }

    // Herunterladen falls nötig
    if (!info.heruntergeladen) {
        qDebug() << "ModelManager: Modell" << info.id << "muss heruntergeladen werden.";
        modellHerunterladen(id);
        if (!info.heruntergeladen) {
            return false; // Download läuft asynchron
        }
    }

    // In ONNX Runtime-Session laden
    modellLaden(id);
    return m_sessions.contains(id);
}

void* ModelManager::session(ModellId id)
{
    if (!m_sessions.contains(id)) {
        qWarning() << "ModelManager: Keine Session für Modell" << static_cast<int>(id);
        return nullptr;
    }
    return m_sessions[id];
}

qint64 ModelManager::coreGroesseGesamt() const
{
    qint64 gesamt = 0;
    for (auto it = m_modelle.begin(); it != m_modelle.end(); ++it) {
        if (it.value().erforderlich) {
            gesamt += it.value().groesseBytes;
        }
    }
    return gesamt;
}

qint64 ModelManager::optionalGroesseGesamt() const
{
    qint64 gesamt = 0;
    for (auto it = m_modelle.begin(); it != m_modelle.end(); ++it) {
        if (!it.value().erforderlich) {
            gesamt += it.value().groesseBytes;
        }
    }
    return gesamt;
}

// ── Private Hilfsmethoden ─────────────────────────────────────────────────

void ModelManager::modellHerunterladen(ModellId id)
{
    if (!m_modelle.contains(id)) return;

    ModellInfo& info = m_modelle[id];

    const QString basisPfad = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    const QString modellePfad = basisPfad + "/models";
    QDir().mkpath(modellePfad);

    QString zielPfad = modellePfad + "/" + info.id + ".onnx";

    // Bereits vorhanden?
    QFileInfo fi(zielPfad);
    if (fi.exists() && fi.size() == info.groesseBytes) {
        info.heruntergeladen = true;
        emit modellBereit(id);
        return;
    }

    qDebug() << "ModelManager: Lade herunter:" << info.id << "von" << info.url;

    auto* nam = new QNetworkAccessManager(this);
    QNetworkRequest request(QUrl(info.url));
    request.setAttribute(QNetworkRequest::RedirectPolicyAttribute,
                         QNetworkRequest::NoLessSafeRedirectPolicy);

    auto* reply = nam->get(request);

    connect(reply, &QNetworkReply::downloadProgress, this,
            [this, id](qint64 empfangen, qint64 gesamt) {
                emit modellDownloadFortschritt(id, empfangen, gesamt);
            });

    connect(reply, &QNetworkReply::finished, this, [this, id, reply, zielPfad]() {
        ModellInfo& info = m_modelle[id];

        if (reply->error() != QNetworkReply::NoError) {
            QString fehler = QString("Download-Fehler für %1: %2")
                                 .arg(info.id, reply->errorString());
            qWarning() << "ModelManager:" << fehler;
            emit downloadFehler(id, fehler);
            reply->deleteLater();
            return;
        }

        QByteArray daten = reply->readAll();

        // SHA256-Prüfung
        QCryptographicHash hash(QCryptographicHash::Sha256);
        hash.addData(daten);
        QString berechneterHash = hash.result().toHex();

        if (!info.sha256.contains("placeholder") && berechneterHash != info.sha256.toLower()) {
            QString fehler = QString("SHA256-Prüfung fehlgeschlagen für %1").arg(info.id);
            qCritical() << "ModelManager:" << fehler;
            emit downloadFehler(id, fehler);
            reply->deleteLater();
            return;
        }

        // Datei speichern
        QFile datei(zielPfad);
        if (!datei.open(QIODevice::WriteOnly)) {
            QString fehler = QString("Kann Datei nicht schreiben: %1").arg(zielPfad);
            qWarning() << "ModelManager:" << fehler;
            emit downloadFehler(id, fehler);
            reply->deleteLater();
            return;
        }

        datei.write(daten);
        datei.close();

        info.heruntergeladen = true;
        qDebug() << "ModelManager:" << info.id << "heruntergeladen (" << daten.size() << "Bytes)";

        modellLaden(id);
        emit modellBereit(id);
        reply->deleteLater();
    });
}

void ModelManager::modellLaden(ModellId id)
{
    if (!m_modelle.contains(id) || !m_modelle[id].heruntergeladen) {
        qWarning() << "ModelManager: Modell nicht heruntergeladen:" << static_cast<int>(id);
        return;
    }

    if (m_sessions.contains(id)) return; // Bereits geladen

    const QString basisPfad = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    const QString modellePfad = basisPfad + "/models";
    QString vollerPfad = modellePfad + "/" + m_modelle[id].id + ".onnx";

    // ONNX Runtime Environment (wird von Session referenziert, muss Session überleben)
    OrtEnv* env = nullptr;
    OrtStatus* status = ort()->CreateEnv(ORT_LOGGING_LEVEL_WARNING,
                                          "FlipsiColor", &env);
    if (status) {
        const char* msg = ort()->GetErrorMessage(status);
        qCritical() << "ModelManager: CreateEnv fehlgeschlagen:" << msg;
        ort()->ReleaseStatus(status);
        return;
    }

    // Session-Optionen
    OrtSessionOptions* opts = nullptr;
    ort()->CreateSessionOptions(&opts);
    ort()->SetIntraOpNumThreads(opts, 4);
    ort()->SetSessionGraphOptimizationLevel(opts, ORT_ENABLE_ALL);

    // CUDA-Provider versuchen (fällt auf CPU zurück bei Fehler)
    OrtCUDAProviderOptions cudaOpts;
    cudaOpts.device_id = 0;
    // Weitere CUDA-Optionen mit Defaults
    cudaOpts.arena_extend_strategy = 0;
    cudaOpts.gpu_mem_limit = SIZE_MAX;
    cudaOpts.cudnn_conv_algo_search = OrtCudnnConvAlgoSearchExhaustive;
    cudaOpts.do_copy_in_default_stream = 1;

    status = ort()->SessionOptionsAppendExecutionProvider_CUDA(opts, &cudaOpts);
    if (status) {
        ort()->ReleaseStatus(status);
        qDebug() << "ModelManager: CUDA nicht verfügbar — verwende CPU";
    } else {
        qDebug() << "ModelManager: CUDA Execution Provider aktiviert";
    }

    // Session erstellen
    OrtSession* session = nullptr;
    status = ort()->CreateSession(env, vollerPfad.toStdString().c_str(),
                                   opts, &session);

    ort()->ReleaseSessionOptions(opts);

    if (status) {
        const char* msg = ort()->GetErrorMessage(status);
        qCritical() << "ModelManager: CreateSession fehlgeschlagen für"
                    << m_modelle[id].id << ":" << msg;
        ort()->ReleaseStatus(status);
        ort()->ReleaseEnv(env);
        return;
    }

    // Env wird von der Session mitverwaltet — nicht separat freigeben
    // (ONNX Runtime übernimmt die Lebenszeit von Env wenn Session existiert)

    m_sessions[id] = static_cast<void*>(session);

    qDebug() << "ModelManager: Session erstellt für" << m_modelle[id].id
             << "(" << m_modelle[id].groesseBytes / (1024*1024) << "MB)";
}

} // namespace flipsicolor
