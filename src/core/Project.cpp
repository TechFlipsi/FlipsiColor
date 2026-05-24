// FlipsiColor — Projekt-Verwaltung Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/core/Project.h>
#include <QFile>
#include <QDir>
#include <QJsonDocument>
#include <QJsonObject>
#include <QDebug>

namespace flipsicolor {

class Project::Impl {
public:
    QString name;
    QString dateipfad;
    QStringList bilder;
    QStringList videos;
    QVariantMap einstellungen;
    bool geaendert = false;
};

Project::Project(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
}

Project::~Project() = default;

bool Project::laden(const QString& pfad)
{
    QFile datei(pfad);
    if (!datei.open(QIODevice::ReadOnly)) return false;

    QJsonDocument doc = QJsonDocument::fromJson(datei.readAll());
    if (doc.isNull()) return false;

    QJsonObject obj = doc.object();
    m_impl->name = obj["name"].toString();
    m_impl->dateipfad = pfad;
    m_impl->geaendert = false;
    emit projektGeladen();
    return true;
}

bool Project::speichern()
{
    if (m_impl->dateipfad.isEmpty()) return false;

    QJsonObject obj;
    obj["name"] = m_impl->name;

    QJsonDocument doc(obj);
    QFile datei(m_impl->dateipfad);
    if (!datei.open(QIODevice::WriteOnly)) return false;

    datei.write(doc.toJson());
    m_impl->geaendert = false;
    return true;
}

void Project::setGeaendert(bool geaendert)
{
    m_impl->geaendert = geaendert;
    emit geaendertChanged();
}

bool Project::istGeaendert() const
{
    return m_impl->geaendert;
}

} // namespace flipsicolor
