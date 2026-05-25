// FlipsiColor — Video-Pipeline Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

// FlipsiColor — MSVC <ctime>-Fix: <time.h> VOR allen anderen Headern inkludieren,
// da MSVC 14.44 sonst C2039/C2873 in <ctime> wirft, wenn Drittanbieter-Header
// (Qt, OpenCV, ONNX Runtime) vor <time.h>/<ctime> geladen werden.
#if defined(_MSC_VER)
#include <time.h>
#endif

#include <flipsicolor/video/VideoPipeline.h>
#include "flipsicolor/core/Pipeline.h"
#include <QDebug>

namespace flipsicolor {

class VideoPipeline::Impl {
public:
    QString dateiPfad;
    int aktuellesFrame = 0;
    int gesamtFrames = 0;
    double fps = 0.0;
    bool istGeladen = false;
};

VideoPipeline::VideoPipeline(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
}

VideoPipeline::~VideoPipeline() = default;

bool VideoPipeline::videoLaden(const QString& pfad)
{
    m_impl->dateiPfad = pfad;
    // TODO: FFmpeg Demuxer + Decoder Initialisierung
    m_impl->istGeladen = true;
    emit videoGeladen();
    return true;
}

void VideoPipeline::pipelineAusfuehren(const PipelineParams& params)
{
    // TODO: Vollständige Video-Pipeline
    // 1) Frame-Dekodierung (FFmpeg)
    // 2) Szenen-Erkennung
    // 3) Bild-Pipeline pro Frame
    // 4) Frame-Konsistenz (Referenz-Frame + Histogram-Matching + EMA)
    // 5) Hautton-Schutz über Frame-Grenzen
    // 6) Encoding (FFmpeg HW-Beschleunigt)
    Q_UNUSED(params)
    emit pipelineAbgeschlossen();
}

} // namespace flipsicolor
