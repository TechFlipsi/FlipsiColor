// FlipsiColor — Dateisystem-Hilfsfunktionen Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/utils/FileSystem.h>

#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QStandardPaths>

namespace flipsicolor
{

    FileSystem::FileSystem(QObject* parent) : QObject(parent) {}

    QString FileSystem::modellVerzeichnis()
    {
        const QString pfad =
            QStandardPaths::writableLocation(QStandardPaths::AppDataLocation) + QStringLiteral("/models/");
        QDir().mkpath(pfad);
        return pfad;
    }

    QString FileSystem::cacheVerzeichnis()
    {
        const QString pfad = QStandardPaths::writableLocation(QStandardPaths::CacheLocation);
        QDir().mkpath(pfad);
        return pfad;
    }

    qint64 FileSystem::verzeichnisGroesse(const QString& pfad)
    {
        qint64 groesse = 0;
        QDir   verzeichnis(pfad);

        if ( !verzeichnis.exists() )
            return 0;

        const QFileInfoList eintraege =
            verzeichnis.entryInfoList(QDir::Files | QDir::Dirs | QDir::NoDotAndDotDot | QDir::Hidden | QDir::System);

        for ( const QFileInfo& eintrag : eintraege )
        {
            if ( eintrag.isDir() )
                groesse += verzeichnisGroesse(eintrag.absoluteFilePath());
            else
                groesse += eintrag.size();
        }

        return groesse;
    }

    bool FileSystem::verzeichnisLeeren(const QString& pfad)
    {
        QDir verzeichnis(pfad);

        if ( !verzeichnis.exists() )
            return false;

        const QFileInfoList eintraege =
            verzeichnis.entryInfoList(QDir::Files | QDir::Dirs | QDir::NoDotAndDotDot | QDir::Hidden | QDir::System);

        for ( const QFileInfo& eintrag : eintraege )
        {
            if ( eintrag.isDir() )
            {
                if ( !verzeichnisLeeren(eintrag.absoluteFilePath()) )
                    return false;
                if ( !verzeichnis.rmdir(eintrag.absoluteFilePath()) )
                    return false;
            }
            else
            {
                if ( !QFile::remove(eintrag.absoluteFilePath()) )
                    return false;
            }
        }

        return true;
    }

}  // namespace flipsicolor
