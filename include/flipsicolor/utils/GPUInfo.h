// FlipsiColor — GPU-Informationen
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>

namespace flipsicolor {

class GPUInfo : public QObject
{
    Q_OBJECT

public:
    enum class Backend { CUDA, DirectML, Metal, CPU };
    Q_ENUM(Backend)

    explicit GPUInfo(QObject* parent = nullptr);

    QString gpuName() const;
    bool istVerfuegbar() const;
    int vramMB() const;
    Backend bestesBackend() const;
};

} // namespace flipsicolor
