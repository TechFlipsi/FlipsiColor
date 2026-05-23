// FlipsiColor — Processing Pipeline
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <memory>

namespace flipsicolor {

enum class Intensity { Leicht, Mittel, Stark };
enum class OperationMode { Ask, SmartLearn, Turbo };

struct PipelineParams {
    float whiteBalanceTemp = 5500.0f;
    float whiteBalanceTint = 0.0f;
    float exposure = 0.0f;
    float contrast = 0.0f;
    float highlights = 0.0f;
    float shadows = 0.0f;
    float saturation = 0.0f;
    float vibrance = 0.0f;
    float sharpenAmount = 0.0f;
    float luminanceNR = 0.0f;
    float chrominanceNR = 0.0f;
    bool enableLensCorrection = true;
    bool enableFaceRestore = false;
    int upscaleFactor = 1;
};

class Pipeline : public QObject
{
    Q_OBJECT

public:
    explicit Pipeline(QObject* parent = nullptr);

    void setIntensity(Intensity level);
    void setMode(OperationMode mode);

    PipelineParams defaultParamsForScene(const QString& sceneType) const;
    float codeFormerFidelityWeight() const;

private:
    Intensity m_intensity = Intensity::Mittel;
    OperationMode m_mode = OperationMode::Ask;
};

} // namespace flipsicolor