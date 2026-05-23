// FlipsiColor — Application Core
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>

namespace flipsicolor {

class Application : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool gpuAvailable READ gpuAvailable NOTIFY gpuAvailableChanged)
    Q_PROPERTY(QString gpuName READ gpuName NOTIFY gpuAvailableChanged)
    Q_PROPERTY(int feedbackCount READ feedbackCount NOTIFY feedbackCountChanged)

public:
    explicit Application(QObject* parent = nullptr);

    void initialize();

    bool gpuAvailable() const { return m_gpuAvailable; }
    QString gpuName() const { return m_gpuName; }
    int feedbackCount() const { return m_feedbackCount; }

signals:
    void gpuAvailableChanged();
    void feedbackCountChanged();

private:
    void detectGPU();
    void loadSettings();
    void initializeAI();

    bool m_gpuAvailable = false;
    QString m_gpuName;
    int m_feedbackCount = 0;
};

} // namespace flipsicolor