// FlipsiColor — Einstellungen
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>

namespace flipsicolor
{

    class Settings : public QObject
    {
        Q_OBJECT

    public:
        explicit Settings(QObject* parent = nullptr);

        QString sprache() const;
        void    setSprache(const QString& sprache);

        QString thema() const;
        void    setThema(const QString& thema);

        bool kiAutoAnwenden() const;
        void setKiAutoAnwenden(bool aktiv);

        int schriftgroesse() const;

    signals:
        void spracheGeaendert(const QString& sprache);
        void themaGeaendert(const QString& thema);
        void kiAutoAnwendenGeaendert(bool aktiv);
    };

}  // namespace flipsicolor
