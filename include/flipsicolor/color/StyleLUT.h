// FlipsiColor — Style Learning via AiLUT-Transform
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <QMap>

namespace flipsicolor {

struct TrainingPair {
    // 256×256 thumbnails for efficient training
    // original + user-edited version
    QString originalPath;
    QString editedPath;
    QString sceneType;
};

class StyleLUT : public QObject
{
    Q_OBJECT
    Q_PROPERTY(int feedbackCount READ feedbackCount NOTIFY feedbackCountChanged)
    Q_PROPERTY(int learningRound READ learningRound NOTIFY learningRoundChanged)

public:
    explicit StyleLUT(QObject* parent = nullptr);

    // Apply learned style to an image via AiLUT-Transform
    cv::Mat applyStyle(const cv::Mat& image, const QString& sceneType, float strength) const;

    // Record feedback
    void recordFeedback(const TrainingPair& pair, bool positive);
    void recordEdit(const TrainingPair& pair); // Smart-Learn mode

    // Learning state
    int feedbackCount() const { return m_feedbackCount; }
    int learningRound() const { return m_feedbackCount < 60 ? 1 : (m_feedbackCount < 120 ? 2 : 0); }
    bool isAutonomous() const { return m_feedbackCount >= 120; }
    bool needsVariety() const { return m_feedbackCount >= 55 && m_feedbackCount <= 65; }

    // Reset
    void reset();

signals:
    void feedbackCountChanged();
    void learningRoundChanged();
    void learningPhaseCompleted(int round);

private:
    void retrainLUT(); // Trigger AiLUT-Transform retraining
    void compactKnowledge(); // Auto-compact every 100 feedbacks

    int m_feedbackCount = 0;
    QMap<QString, int> m_sceneTypeCounts; // Scene → feedback count
};

} // namespace flipsicolor