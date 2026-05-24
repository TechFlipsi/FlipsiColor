// FlipsiColor — Bild-Pipeline Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/image/ImagePipeline.h>
#include "flipsicolor/core/Pipeline.h"
#include "flipsicolor/color/ColorManager.h"
#include "flipsicolor/color/StyleLUT.h"
#include <opencv2/core/core.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include <opencv2/imgcodecs.hpp>
#include <opencv2/photo/photo.hpp>
#include <QDebug>

namespace flipsicolor {

class ImagePipeline::Impl {
public:
    PipelineParams params;
    cv::Mat originalBild;
    cv::Mat ergebnisBild;
    bool istGeladen = false;
};

ImagePipeline::ImagePipeline(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
}

ImagePipeline::~ImagePipeline() = default;

bool ImagePipeline::bildLaden(const QString& pfad)
{
    // LibRaw oder OpenCV zum Laden
    cv::Mat bild = cv::imread(pfad.toStdString(), cv::IMREAD_UNCHANGED);
    if (bild.empty()) {
        qWarning() << "Bild konnte nicht geladen werden:" << pfad;
        return false;
    }

    m_impl->originalBild = bild.clone();
    m_impl->istGeladen = true;
    emit bildGeladen();
    return true;
}

void ImagePipeline::pipelineAusfuehren(const PipelineParams& params)
{
    if (!m_impl->istGeladen) return;

    m_impl->params = params;
    cv::Mat arbeit = m_impl->originalBild.clone();

    // 1) Arbeitsfarbraum: ProPhoto RGB
    // TODO: LCMS2 Konvertierung

    // 2) Szenenerkennung
    // TODO: EfficientNet-Lite0

    // 3) Weißabgleich
    // TODO: GrayWorld / ShadesOfGray

    // 4) Objektivkorrektur
    if (params.objektivkorrekturAktiv) {
        // TODO: Lensfun
    }

    // 5-8) Belichtung, Lichter, Schatten, Tonkurve
    if (std::abs(params.belichtung) > 0.001) {
        arbeit.convertTo(arbeit, -1, std::pow(2.0, params.belichtung));
    }
    if (std::abs(params.kontrast) > 0.001) {
        double alpha = 1.0 + params.kontrast;
        arbeit.convertTo(arbeit, -1, alpha, -(alpha - 1.0) * 128);
    }

    // 9) KI-Entrauschen/Entschärfen (Restormer-light)
    if (params.luminanzRauschen > 0 || params.chrominanzRauschen > 0) {
        // TODO: NAFNet ONNX Inferenz
    }

    // 10) Farbkorrektur (AiLUT-Transform Stil + manuell)
    if (std::abs(params.saettigung) > 0.001 || std::abs(params.vibranz) > 0.001) {
        cv::Mat hsv;
        cv::cvtColor(arbeit, hsv, cv::COLOR_BGR2HSV);
        // Sättigung anpassen
        // Vibrance: saturationsbasierte Anpassung
        // TODO: Vollständig
    }

    // 11) Hautton-Schutz
    // TODO: HSV-basierter Hautton-Schutz

    // 12) Schärfen (Unsharp Mask)
    if (params.schaerfeBetrag > 0) {
        cv::Mat unschaarf;
        cv::GaussianBlur(arbeit, unschaarf, cv::Size(0, 0), params.schaerfeBetrag);
        cv::addWeighted(arbeit, 1.5, unschaarf, -0.5, 0, arbeit);
    }

    // 13-14) Ausgabefarbraum + Gamma
    // TODO: LCMS2 Konvertierung + sRGB Gamma

    // 15) Optionales Hochskalieren
    if (params.hochskalierenFaktor > 1) {
        // TODO: Real_HAT_GAN / Real-ESRGAN
    }

    // 16) Optionale Gesichtswiederherstellung
    if (params.gesichtswiederherstellungAktiv) {
        // TODO: CodeFormer mit fidelity weight
    }

    m_impl->ergebnisBild = arbeit;
    emit pipelineAbgeschlossen();
}

cv::Mat ImagePipeline::ergebnis() const
{
    return m_impl->ergebnisBild;
}

} // namespace flipsicolor
