// FlipsiColor — AI Model Manager
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <QMap>
#include <memory>

struct OrtSession;

namespace flipsicolor {

enum class ModelId {
    NAFNet,           // Denoise (17MB, core)
    RestormerLight,   // Deblur/Multi-task (24MB, core)
    RealHATGAN,       // Upscale best quality (120MB, lazy)
    RealESRGAN,       // Upscale fast (64MB, lazy)
    CodeFormer,       // Face restoration (350MB, lazy)
    AiLUTTransform,   // Color style learning (8MB, core)
    EfficientNet,     // Scene classification (4.6MB, core)
};

struct ModelInfo {
    QString id;
    QString url;
    QString sha256;
    qint64 sizeBytes;
    bool required;
    bool downloaded = false;
};

class ModelManager : public QObject
{
    Q_OBJECT

public:
    explicit ModelManager(QObject* parent = nullptr);

    void loadManifest();
    bool ensureModel(ModelId id);
    void* session(ModelId id); // Returns OrtSession* (void* to avoid ONNX header dep)

    qint64 totalCoreSize() const;
    qint64 totalOptionalSize() const;

signals:
    void modelDownloadProgress(ModelId id, qint64 received, qint64 total);
    void modelReady(ModelId id);
    void downloadError(ModelId id, const QString& error);

private:
    void downloadModel(ModelId id);
    void loadModel(ModelId id);

    QMap<ModelId, ModelInfo> m_models;
    QMap<ModelId, void*> m_sessions;
};

} // namespace flipsicolor