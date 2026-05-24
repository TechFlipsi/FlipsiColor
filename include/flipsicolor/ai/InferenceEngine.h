// FlipsiColor — KI-Inferenz-Engine
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <QVector>
#include <map>
#include <memory>

namespace flipsicolor {

class InferenceEngine : public QObject
{
    Q_OBJECT

public:
    explicit InferenceEngine(QObject* parent = nullptr);
    ~InferenceEngine();

    bool modellLaden(const QString& modellId, const QString& dateiPfad);
    QVector<float> inferenz(const QString& modellId, const QVector<float>& eingabe, const QVector<int64_t>& form);

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace flipsicolor
