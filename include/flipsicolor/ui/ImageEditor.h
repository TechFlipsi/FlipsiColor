// FlipsiColor — ImageEditor Header
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later
#pragma once

#include <QObject>

namespace flipsicolor
{

    class ImageEditor : public QObject
    {
        Q_OBJECT
    public:
        explicit ImageEditor(QObject* parent = nullptr) : QObject(parent) {}
    };

}  // namespace flipsicolor
