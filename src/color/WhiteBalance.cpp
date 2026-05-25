// FlipsiColor — Weißabgleich-Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

// FlipsiColor — MSVC <ctime>-Fix: Workaround für MSVC 14.44 Bug C2039/C2873.
// Inkludiert <time.h> und importiert Symbole manuell in std-Namespace,
// OHNE <ctime> zu inkludieren (das die Fehler auslöst).
// Siehe: include/flipsicolor/msvc_ctime_fix.h für Details.
#if defined(_MSC_VER)
#include "flipsicolor/msvc_ctime_fix.h"
#endif

#include "flipsicolor/color/ColorManager.h"
#include <opencv2/core/core.hpp>
#include <opencv2/imgproc/imgproc.hpp>
#include <cmath>
#include <algorithm>

namespace flipsicolor {

WhiteBalance::WhiteBalance(QObject* parent)
    : QObject(parent)
{
}

WhiteBalance::WBErgebnis WhiteBalance::grayWorld(const cv::Mat& bild) const
{
    if (bild.empty())
        return {5500.0, 0.0};

    cv::Mat konvertiert;
    if (bild.channels() == 4)
        cv::cvtColor(bild, konvertiert, cv::COLOR_BGRA2BGR);
    else if (bild.channels() == 1)
        cv::cvtColor(bild, konvertiert, cv::COLOR_GRAY2BGR);
    else
        konvertiert = bild;

    cv::Scalar mittel = cv::mean(konvertiert);

    double avgB = mittel[0];
    double avgG = mittel[1];
    double avgR = mittel[2];

    double avgGray = (avgR + avgG + avgB) / 3.0;
    if (avgGray < 1e-6)
        return {5500.0, 0.0};

    double faktorR = avgGray / std::max(avgR, 1e-6);
    double faktorG = avgGray / std::max(avgG, 1e-6);
    double faktorB = avgGray / std::max(avgB, 1e-6);

    // Faktoren → Temperatur/Tint konvertieren (vereinfacht)
    double temperatur = 5500.0 + (faktorB - faktorR) * 3000.0;
    double tint = (faktorG - 1.0) * 50.0;

    temperatur = std::clamp(temperatur, 2000.0, 12000.0);
    tint = std::clamp(tint, -100.0, 100.0);

    return {temperatur, tint};
}

WhiteBalance::WBErgebnis WhiteBalance::shadesOfGray(const cv::Mat& bild, int m) const
{
    if (bild.empty())
        return {5500.0, 0.0};

    cv::Mat konvertiert;
    if (bild.channels() == 4)
        cv::cvtColor(bild, konvertiert, cv::COLOR_BGRA2BGR);
    else if (bild.channels() == 1)
        cv::cvtColor(bild, konvertiert, cv::COLOR_GRAY2BGR);
    else
        konvertiert = bild;

    // Minkowski-Norm: (∑ p^m)^(1/m) pro Kanal
    double summeR = 0, summeG = 0, summeB = 0;
    int n = konvertiert.rows * konvertiert.cols;

    for (int y = 0; y < konvertiert.rows; ++y) {
        for (int x = 0; x < konvertiert.cols; ++x) {
            cv::Vec3b pixel = konvertiert.at<cv::Vec3b>(y, x);
            double r = pixel[2] / 255.0;
            double g = pixel[1] / 255.0;
            double b = pixel[0] / 255.0;
            summeR += std::pow(r, m);
            summeG += std::pow(g, m);
            summeB += std::pow(b, m);
        }
    }

    double normR = std::pow(summeR / n, 1.0 / m);
    double normG = std::pow(summeG / n, 1.0 / m);
    double normB = std::pow(summeB / n, 1.0 / m);

    double avgGray = (normR + normG + normB) / 3.0;
    if (avgGray < 1e-6)
        return {5500.0, 0.0};

    double faktorR = avgGray / std::max(normR, 1e-6);
    double faktorB = avgGray / std::max(normB, 1e-6);
    double faktorG = avgGray / std::max(normG, 1e-6);

    double temperatur = 5500.0 + (faktorB - faktorR) * 3000.0;
    double tint = (faktorG - 1.0) * 50.0;

    temperatur = std::clamp(temperatur, 2000.0, 12000.0);
    tint = std::clamp(tint, -100.0, 100.0);

    return {temperatur, tint};
}

WhiteBalance::WBErgebnis WhiteBalance::autoWB(const cv::Mat& bild) const
{
    auto gw = grayWorld(bild);
    auto sog = shadesOfGray(bild, 6);

    // Kombinieren: Durchschnitt beider Methoden
    double temperatur = (gw.temperatur + sog.temperatur) / 2.0;
    double tint = (gw.tint + sog.tint) / 2.0;

    return {temperatur, tint};
}

} // namespace flipsicolor
