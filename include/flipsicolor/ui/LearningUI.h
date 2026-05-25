// FlipsiColor — LearningUI Header
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later
#pragma once

#include <QObject>

namespace flipsicolor
{

    class LearningUI : public QObject
    {
        Q_OBJECT
    public:
        explicit LearningUI(QObject* parent = nullptr) : QObject(parent) {}
    };

}  // namespace flipsicolor
