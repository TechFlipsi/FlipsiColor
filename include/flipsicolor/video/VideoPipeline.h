// FlipsiColor — Video-Pipeline
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <memory>
#include "flipsicolor/core/Pipeline.h"

namespace flipsicolor {

class VideoPipeline : public QObject
{
    Q_OBJECT

public:
    explicit VideoPipeline(QObject* parent = nullptr);
    ~VideoPipeline();

    [[nodiscard]] bool videoLaden(const QString& pfad);
    void pipelineAusfuehren(const PipelineParams& params);

signals:
    void videoGeladen();
    void pipelineAbgeschlossen();

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace flipsicolor
