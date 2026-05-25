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

    [[nodiscard]] QString gpuName() const;
    [[nodiscard]] bool istVerfuegbar() const;
    [[nodiscard]] int vramMB() const;
    [[nodiscard]] Backend bestesBackend() const;
};

} // namespace flipsicolor
