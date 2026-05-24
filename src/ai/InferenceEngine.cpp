// FlipsiColor — KI-Inferenz-Engine Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/ai/InferenceEngine.h>
#include <onnxruntime_cxx_api.h>
#include <QDebug>
#include <QFile>

namespace flipsicolor {

class InferenceEngine::Impl {
public:
    Ort::Env umgebung{ORT_LOGGING_LEVEL_WARNING, "FlipsiColor"};
    Ort::SessionOptions optionen;
    std::map<std::string, std::unique_ptr<Ort::Session>> sessions;
    Ort::AllocatorWithDefaultOptions zuweiser;
};

InferenceEngine::InferenceEngine(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
    // Session-Optionen konfigurieren
    m_impl->optionen.SetIntraOpNumThreads(4);
    m_impl->optionen.SetGraphOptimizationLevel(GraphOptimizationLevel::ORT_ENABLE_ALL);

    // GPU Provider aktivieren (falls vorhanden)
    // TODO: CUDA / DirectML / Metal je nach Plattform
}

InferenceEngine::~InferenceEngine() = default;

bool InferenceEngine::modellLaden(const QString& modellId, const QString& dateiPfad)
{
    if (!QFile::exists(dateiPfad)) {
        qWarning() << "Modell-Datei nicht gefunden:" << dateiPfad;
        return false;
    }

    try {
        auto session = std::make_unique<Ort::Session>(
            m_impl->umgebung, dateiPfad.toStdWString(), m_impl->optionen);
        m_impl->sessions[modellId.toStdString()] = std::move(session);
        qDebug() << "Modell geladen:" << modellId;
        return true;
    } catch (const Ort::Exception& e) {
        qWarning() << "ONNX Modell-Fehler:" << e.what();
        return false;
    }
}

QVector<float> InferenceEngine::inferenz(const QString& modellId, const QVector<float>& eingabe, const QVector<int64_t>& form)
{
    auto it = m_impl->sessions.find(modellId.toStdString());
    if (it == m_impl->sessions.end()) {
        qWarning() << "Modell nicht geladen:" << modellId;
        return {};
    }

    auto& session = it->second;

    // Eingabe-Tensor erstellen
    std::vector<int64_t> inputShape(form.begin(), form.end());
    auto inputTensor = Ort::Value::CreateTensor<float>(
        m_impl->zuweiser, inputShape, const_cast<float*>(eingabe.data()), eingabe.size());

    const char* inputNamen[] = {"input"};
    const char* outputNamen[] = {"output"};

    try {
        auto ergebnis = session->Run(
            Ort::RunOptions{nullptr}, inputNamen, &inputTensor, 1, outputNamen, 1);

        // Ergebnis extrahieren
        float* daten = ergebnis[0].GetTensorMutableData<float>();
        size_t anzahl = ergebnis[0].GetTensorTypeAndShapeInfo().GetElementCount();

        return QVector<float>(daten, daten + anzahl);
    } catch (const Ort::Exception& e) {
        qWarning() << "Inferenz-Fehler:" << e.what();
        return {};
    }
}

} // namespace flipsicolor
