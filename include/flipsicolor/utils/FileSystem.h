// FlipsiColor — Dateisystem-Hilfsfunktionen
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>

namespace flipsicolor {

class FileSystem : public QObject
{
    Q_OBJECT

public:
    explicit FileSystem(QObject* parent = nullptr);

    static QString modellVerzeichnis();
    static QString cacheVerzeichnis();
    static qint64 verzeichnisGroesse(const QString& pfad);
    static bool verzeichnisLeeren(const QString& pfad);
};

} // namespace flipsicolor
