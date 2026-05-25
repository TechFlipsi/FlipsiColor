// FlipsiColor — EXIF-Leser
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <QVariantMap>
#include <memory>

namespace flipsicolor {

class ExifReader : public QObject
{
    Q_OBJECT

public:
    explicit ExifReader(QObject* parent = nullptr);
    ~ExifReader();

    [[nodiscard]] QVariantMap lesen(const QString& pfad);

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace flipsicolor
