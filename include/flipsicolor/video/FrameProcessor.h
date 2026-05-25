// FlipsiColor — Frame-Verarbeitung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <memory>
#include <QObject>

namespace cv
{
    class Mat;
}

namespace flipsicolor
{

    class FrameProcessor : public QObject
    {
        Q_OBJECT

    public:
        explicit FrameProcessor(QObject* parent = nullptr);

        double  histogrammAehnlichkeit(const cv::Mat& frame1, const cv::Mat& frame2) const;
        bool    szenenWechselErkennen(const cv::Mat& frame1, const cv::Mat& frame2, double schwellwert = 0.3) const;
        cv::Mat histogrammAnpassen(const cv::Mat& frame, const cv::Mat& referenz) const;
        cv::Mat emaGlatten(const cv::Mat& frame, const cv::Mat& vorhersage, float alpha) const;

    private:
        float m_emaAlpha = 0.5f;
    };

}  // namespace flipsicolor
