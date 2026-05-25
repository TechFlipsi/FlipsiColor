// FlipsiColor — RAW-Dekodierer  
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <QVariantMap>
#include <memory>

namespace flipsicolor {

class RawDecoder : public QObject
{
    Q_OBJECT

public:
    explicit RawDecoder(QObject* parent = nullptr);
    ~RawDecoder();

    [[nodiscard]] bool laden(const QString& pfad);
    void schliessen();
    QVariantMap metadaten() const;

signals:
    void geladen(const QString& pfad);

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace flipsicolor
