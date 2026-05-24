// FlipsiColor — AppWindow Header
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later
#pragma once

#include <QObject>

namespace flipsicolor {

class AppWindow : public QObject {
    Q_OBJECT
public:
    explicit AppWindow(QObject* parent = nullptr) : QObject(parent) {}
};

} // namespace flipsicolor
