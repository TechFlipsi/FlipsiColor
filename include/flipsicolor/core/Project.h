// FlipsiColor — Projekt-Verwaltung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <QStringList>
#include <QVariantMap>
#include <memory>

namespace flipsicolor {

class Project : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool geaendert READ istGeaendert NOTIFY geaendertChanged)

public:
    explicit Project(QObject* parent = nullptr);
    ~Project();

    bool laden(const QString& pfad);
    bool speichern();
    void setGeaendert(bool geaendert);
    bool istGeaendert() const;

signals:
    void projektGeladen();
    void geaendertChanged();

private:
    struct Impl;
    std::unique_ptr<Impl> m_impl;
};

} // namespace flipsicolor
