// FlipsiColor — Verarbeitungs-Pipeline
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <memory>

namespace flipsicolor {

enum class Intensitaet { Leicht, Mittel, Stark };
enum class BetriebsModus { Ask, SmartLearn, Turbo };

struct PipelineParams {
    float weissabgleichTemp = 5500.0f;
    float weissabgleichTint = 0.0f;
    float belichtung = 0.0f;
    float kontrast = 0.0f;
    float lichter = 0.0f;
    float schatten = 0.0f;
    float saettigung = 0.0f;
    float vibranz = 0.0f;
    float schaerfeBetrag = 0.0f;
    float luminanzRauschen = 0.0f;
    float chrominanzRauschen = 0.0f;
    bool objektivkorrekturAktiv = true;
    bool gesichtswiederherstellungAktiv = false;
    int hochskalierenFaktor = 1;
};

class Pipeline : public QObject
{
    Q_OBJECT

public:
    explicit Pipeline(QObject* parent = nullptr);

    void setIntensitaet(Intensitaet stufe);
    void setModus(BetriebsModus modus);

    PipelineParams standardParamsFuerSzene(const QString& szenenTyp) const;
    float codeFormerFidelityWeight() const;

private:
    Intensitaet m_intensitaet = Intensitaet::Mittel;
    BetriebsModus m_modus = BetriebsModus::Ask;
};

} // namespace flipsicolor