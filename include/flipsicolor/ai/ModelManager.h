// FlipsiColor — KI-Modellverwaltung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <QMap>
#include <memory>

struct OrtSession;

namespace flipsicolor {

enum class ModellId {
    NAFNet,           // Entrauschen (17MB, Core)
    RestormerLight,   // Entschärfen/Multi-Task (24MB, Core)
    RealHATGAN,       // Hochskalieren beste Qualität (120MB, Lazy)
    RealESRGAN,       // Hochskalieren schnell (64MB, Lazy)
    CodeFormer,       // Gesichtswiederherstellung (350MB, Lazy)
    AiLUTTransform,   // Farbstil-Lernen (8MB, Core)
    EfficientNet,     // Szenen-Klassifizierung (4,6MB, Core)
};

struct ModellInfo {
    QString id;
    QString url;
    QString sha256;
    qint64 groesseBytes;
    bool erforderlich;
    bool heruntergeladen = false;
};

class ModelManager : public QObject
{
    Q_OBJECT

public:
    explicit ModelManager(QObject* parent = nullptr);

    void manifestLaden();
    bool modellSicherstellen(ModellId id);
    void* session(ModellId id); // Gibt OrtSession* zurück (void* um ONNX-Header-Abhängigkeit zu vermeiden)

    qint64 coreGroesseGesamt() const;
    qint64 optionalGroesseGesamt() const;

signals:
    void modellDownloadFortschritt(ModellId id, qint64 empfangen, qint64 gesamt);
    void modellBereit(ModellId id);
    void downloadFehler(ModellId id, const QString& fehler);

private:
    void modellHerunterladen(ModellId id);
    void modellLaden(ModellId id);

    QMap<ModellId, ModellInfo> m_modelle;
    QMap<ModellId, void*> m_sessions;
};

} // namespace flipsicolor