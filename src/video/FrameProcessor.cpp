// FlipsiColor — Frame-Verarbeitung Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <cmath>
#include <flipsicolor/video/FrameProcessor.h>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

namespace flipsicolor
{

    FrameProcessor::FrameProcessor(QObject* parent) : QObject(parent), m_emaAlpha(0.5) {}

    double FrameProcessor::histogrammAehnlichkeit(const cv::Mat& frame1, const cv::Mat& frame2) const
    {
        // Chi-Quadrat Histogramm-Vergleich
        cv::Mat      hist1, hist2;
        int          kanalen[]     = {0, 1, 2};
        int          histGroesse[] = {64, 64, 64};
        float        bereich[]     = {0, 256, 0, 256, 0, 256};
        const float* bereiche[]    = {bereich, bereich + 2, bereich + 4};

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

    cv::Mat FrameProcessor::histogrammAnpassen(const cv::Mat& frame, const cv::Mat& referenz) const
    {
        if ( frame.empty() || referenz.empty() )
            return frame.clone();

        // Histogramm-Anpassung (Histogram Matching) pro Kanal
        cv::Mat              ergebnis;
        std::vector<cv::Mat> frameKanaele, refKanaele, ergebnisKanaele;
        cv::split(frame, frameKanaele);
        cv::split(referenz, refKanaele);

        for ( int i = 0; i < 3 && i < static_cast<int>(frameKanaele.size()) && i < static_cast<int>(refKanaele.size());
              ++i )
        {
            cv::Mat angepasst;
            cv::equalizeHist(frameKanaele[i], angepasst);
            ergebnisKanaele.push_back(angepasst);
        }

        cv::merge(ergebnisKanaele, ergebnis);
        return ergebnis;
    }

}  // namespace flipsicolor