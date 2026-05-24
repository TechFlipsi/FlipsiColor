// FlipsiColor — App-Kern Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include "flipsicolor/core/Application.h"
#include "flipsicolor/ai/ModelManager.h"
#include "flipsicolor/utils/AutoUpdater.h"

#include <QSettings>
#include <QDebug>

#include <onnxruntime/core/session/onnxruntime_cxx_api.h>

namespace flipsicolor {

Application::Application(QObject* parent)
    : QObject(parent)
{
}

void Application::initialisieren()
{
    qDebug() << "FlipsiColor — App-Initialisierung gestartet";
    gpuErkennen();
    einstellungenLaden();
    kiInitialisieren();

    // ── Auto-Updater starten ────────────────────────────────────────────
    m_updater = std::make_unique<AutoUpdater>(this);
    connect(m_updater.get(), &AutoUpdater::updateVerfuegbarChanged,
            this, &Application::updateVerfuegbarChanged);
    connect(m_updater.get(), &AutoUpdater::neueVersionChanged,
            this, &Application::neueVersionChanged);
    // Prüfung startet automatisch nach 30s
    qDebug() << "FlipsiColor — Auto-Updater initialisiert (prüft automatisch)";

    qDebug() << "FlipsiColor — App-Initialisierung abgeschlossen";
}

bool Application::updateVerfuegbar() const
{
    return m_updater ? m_updater->updateVerfuegbar() : false;
}

QString Application::neueVersion() const
{
    return m_updater ? m_updater->neueVersion() : QString();
}

void Application::updatePruefen()
{
    if (m_updater) m_updater->pruefen();
}

void Application::updateStarten()
{
    if (m_updater) m_updater->updateStarten();
}

void Application::updateIgnorieren()
{
    if (m_updater) m_updater->ignorieren();
}

void Application::gpuErkennen()
{
    // ONNX Runtime Provider-Prüfung
    QStringList verfuegbareProvider;

    try {
        // Prüfe verfügbare Execution-Provider via ONNX Runtime API
        auto api = Ort::GetApi();
        auto providers = api.GetAvailableProviders();
        for (const auto& p : providers) {
            QString s = QString::fromStdString(p);
            verfuegbareProvider << s;
            qDebug() << "ONNX Runtime Provider gefunden:" << s;
        }
    } catch (const std::exception& e) {
        qWarning() << "Provider-Prüfung fehlgeschlagen:" << e.what();
    }

    // GPU-Verfügbarkeit anhand CUDA-Provider prüfen
    m_gpuVerfuegbar = verfuegbareProvider.contains("CUDAExecutionProvider", Qt::CaseInsensitive)
                   || verfuegbareProvider.contains("TensorrtExecutionProvider", Qt::CaseInsensitive)
                   || verfuegbareProvider.contains("DmlExecutionProvider", Qt::CaseInsensitive)
                   || verfuegbareProvider.contains("CoreMLExecutionProvider", Qt::CaseInsensitive);

    if (verfuegbareProvider.contains("CUDAExecutionProvider")) {
        m_gpuName = "NVIDIA CUDA GPU (ONNX Runtime)";
    } else if (verfuegbareProvider.contains("DmlExecutionProvider")) {
        m_gpuName = "DirectML GPU (Windows)";
    } else if (verfuegbareProvider.contains("CoreMLExecutionProvider")) {
        m_gpuName = "Apple Neural Engine / CoreML";
    } else if (verfuegbareProvider.contains("TensorrtExecutionProvider")) {
        m_gpuName = "NVIDIA TensorRT GPU";
    } else {
        m_gpuName = "CPU (keine GPU-Beschleunigung)";
    }

    qDebug() << "GPU verfügbar:" << m_gpuVerfuegbar << "—" << m_gpuName;
    emit gpuVerfuegbarChanged();
}

void Application::einstellungenLaden()
{
    QSettings s("TechFlipsi", "FlipsiColor");

    // Feedback-Anzahl aus gespeicherten Einstellungen laden
    m_feedbackAnzahl = s.value("lernen/feedbackAnzahl", 0).toInt();

    qDebug() << "Einstellungen geladen. Feedback-Anzahl:" << m_feedbackAnzahl;

    // Migrationslogik für zukünftige Versionen kann hier eingefügt werden
}

void Application::kiInitialisieren()
{
    // ModelManager-Initialisierung auslösen
    // Der ModelManager lädt das Manifest und stellt sicher, dass Core-Modelle verfügbar sind
    auto* manager = new ModelManager(this);
    manager->manifestLaden();

    qDebug() << "KI-Initialisierung abgeschlossen. Core-Modelle Gesamtgröße:"
             << manager->coreGroesseGesamt() << "Bytes";
}

} // namespace flipsicolor
