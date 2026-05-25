// FlipsiColor — Bild-Pipeline
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <memory>
#include "flipsicolor/core/Pipeline.h"

namespace cv { class Mat; }

namespace flipsicolor {

class ImagePipeline : public QObject
{
    Q_OBJECT

public:
    explicit ImagePipeline(QObject* parent = nullptr);
    ~ImagePipeline();

    [[nodiscard]] bool bildLaden(const QString& pfad);
    void pipelineAusfuehren(const PipelineParams& params);
    cv::Mat ergebnis() const;

signals:
    void bildGeladen();
    void pipelineAbgeschlossen();

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace flipsicolor
