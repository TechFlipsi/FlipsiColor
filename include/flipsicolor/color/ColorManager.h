// FlipsiColor — Color Management
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>

namespace flipsicolor {

class ColorManager : public QObject
{
    Q_OBJECT

public:
    explicit ColorManager(QObject* parent = nullptr);

    // Working color space = ProPhoto RGB (prevents clipping during editing)
    static constexpr const char* WORKING_COLOR_SPACE = "ProPhoto RGB";

    void initialize();
    QString detectMonitorProfile() const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

class WhiteBalance : public QObject
{
    Q_OBJECT

public:
    explicit WhiteBalance(QObject* parent = nullptr);

    // Statistical methods (zero model, <1ms)
    struct WBResult {
        double temperature;
        double tint;
    };

    WBResult grayWorld(const cv::Mat& image) const;
    WBResult shadesOfGray(const cv::Mat& image, int m = 10) const;
    WBResult autoWB(const cv::Mat& image) const; // Combines both
};

} // namespace flipsicolor