// FlipsiColor — Einstellungen Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/core/Settings.h>
#include <QSettings>
#include <QDebug>

namespace flipsicolor {

Settings::Settings(QObject* parent)
    : QObject(parent)
{
}

QString Settings::sprache() const
{
    QSettings s;
    return s.value("sprache", "de").toString();
}

void Settings::setSprache(const QString& sprache)
{
    QSettings s;
    s.setValue("sprache", sprache);
    emit spracheGeaendert(sprache);
}

QString Settings::thema() const
{
    QSettings s;
    return s.value("thema", "dunkel").toString();
}

void Settings::setThema(const QString& thema)
{
    QSettings s;
    s.setValue("thema", thema);
    emit themaGeaendert(thema);
}

bool Settings::kiAutoAnwenden() const
{
    QSettings s;
    return s.value("ki/autoAnwenden", true).toBool();
}

void Settings::setKiAutoAnwenden(bool aktiv)
{
    QSettings s;
    s.setValue("ki/autoAnwenden", aktiv);
    emit kiAutoAnwendenGeaendert(aktiv);
}

int Settings::schriftgroesse() const
{
    QSettings s;
    return s.value("ui/schriftgroesse", 14).toInt();
}

} // namespace flipsicolor
