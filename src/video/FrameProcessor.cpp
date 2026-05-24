// FlipsiColor — Frame-Verarbeitung Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/video/FrameProcessor.h>
#include <opencv2/core/core.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include <cmath>

namespace flipsicolor {

FrameProcessor::FrameProcessor(QObject* parent)
    : QObject(parent)
    , m_emaAlpha(0.5)
{
}

double FrameProcessor::histogrammAehnlichkeit(const cv::Mat& frame1, const cv::Mat& frame2) const
{
    // Chi-Quadrat Histogramm-Vergleich
    cv::Mat hist1, hist2;
    int kanalen[] = {0, 1, 2};
    int histGroesse[] = {64, 64, 64};
    float bereich[] = {0, 256, 0, 256, 0, 256};
    const float* bereiche[] = {bereich, bereich+2, bereich+4};

    cv::calcHist(&frame1, 1, kanalen, cv::Mat(), hist1, 3, histGroesse, bereiche);
    cv::calcHist(&frame2, 1, kanalen, cv::Mat(), hist2, 3, histGroesse, bereiche);

    cv::normalize(hist1, hist1);
    cv::normalize(hist2, hist2);

    double aehnlichkeit = cv::compareHist(hist1, hist2, cv::HISTCMP_CHISQR);
    return aehnlichkeit;
}

bool FrameProcessor::szenenWechselErkennen(const cv::Mat& frame1, const cv::Mat& frame2, double schwellwert) const
{
    return histogrammAehnlichkeit(frame1, frame2) > schwellwert;
}

cv::Mat FrameProcessor::histogrammA...[truncated]