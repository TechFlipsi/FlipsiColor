// FlipsiColor — Stil-Lernen via AiLUT-Transform
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <QMap>

// OpenCV Vorwärtsdeklaration
namespace cv { class Mat; }

namespace flipsicolor {

struct TrainingsPaar {
    // 256×256 Vorschaubilder für effizientes Training
    // Original + benutzerverarbeitete Version
    QString originalPfad;
    QString bearbeitetPfad;
    QString szenenTyp;
};

class StyleLUT : public QObject
{
    Q_OBJECT
    Q_PROPERTY(int feedbackAnzahl READ feedbackAnzahl NOTIFY feedbackAnzahlChanged)
    Q_PROPERTY(int lernRunde READ lernRunde NOTIFY lernRundeChanged)

public:
    explicit StyleLUT(QObject* parent = nullptr);

    // Gelernten Stil auf Bild anwenden via AiLUT-Transform
    cv::Mat stilAnwenden(const cv::Mat& bild, const QString& szenenTyp, float staerke) const;

    // Feedback aufzeichnen
    void feedbackAufzeichnen(const TrainingsPaar& paar, bool positiv);
    void bearbeitungAufzeichnen(const TrainingsPaar& paar); // Smart-Learn-Modus

    // Lern-Status
    int feedbackAnzahl() const { return m_feedbackAnzahl; }
    int lernRunde() const { return m_feedbackAnzahl < 60 ? 1 : (m_feedbackAnzahl < 120 ? 2 : 0); }
    bool istSelbststaendig() const { return m_feedbackAnzahl >= 120; }
    bool brauchtVielfalt() const { return m_feedbackAnzahl >= 55 && m_feedbackAnzahl <= 65; }

    // Zurücksetzen
    void zuruecksetzen();

signals:
    void feedbackAnzahlChanged();
    void lernRundeChanged();
    void lernPhaseAbgeschlossen(int runde);

private:
    void lutNachtrainieren();    // AiLUT-Transform Neu-Training auslösen
    void wissenVerdichten();     // Auto-Verdichtung alle 100 Feedbacks

    int m_feedbackAnzahl = 0;
    QMap<QString, int> m_szenenTypZaehler; // Szene → Feedback-Anzahl
};

} // namespace flipsicolor