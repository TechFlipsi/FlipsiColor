// FlipsiColor — Objektivkorrektur
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <memory>

namespace cv { class Mat; }

namespace flipsicolor {

class LensCorrector : public QObject
{
    Q_OBJECT

public:
    explicit LensCorrector(QObject* parent = nullptr);
    ~LensCorrector();

    bool initialisieren();
    bool kameraSetzen(const QString& hersteller, const QString& modell);
    bool objektivSetzen(const QString& hersteller, const QString& modell);
    cv::Mat korrigieren(const cv::Mat& bild, float brennweite, float blende) const;

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace flipsicolor
