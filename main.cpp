// FlipsiColor — KI-gestützte Bild- & Videofarbkorrektur
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQuickStyle>
#include <QIcon>
#include <QTranslator>
#include <QLibraryInfo>
#include <QDir>
#include <QDebug>

#include "flipsicolor/core/Application.h"

// Verfügbare Sprachen (ISO-Code → Anzeigename)
static const QStringList VERFUEGBARE_SPRACHEN = {
    "de",     // Deutsch (Standard)
    "en",     // English
    "es",     // Español
    "fr",     // Français
    "it",     // Italiano
    "pt_BR",  // Português (Brasil)
    "ja",     // 日本語
    "zh_CN",  // 简体中文
    "ko"      // 한국어
};

static const QMap<QString, QString> SPRACHNAMEN = {
    {"de",    "Deutsch"},
    {"en",    "English"},
    {"es",    "Español"},
    {"fr",    "Français"},
    {"it",    "Italiano"},
    {"pt_BR", "Português (Brasil)"},
    {"ja",    "日本語"},
    {"zh_CN", "简体中文"},
    {"ko",    "한국어"}
};

int main(int argc, char* argv[])
{
    QGuiApplication app(argc, argv);
    app.setOrganizationName("TechFlipsi");
    app.setOrganizationDomain("techflipsi.com");
    app.setApplicationName("FlipsiColor");
    app.setApplicationVersion("0.1.0");

    QQuickStyle::setStyle("Fusion");

    // ── Übersetzung laden ────────────────────────────────────────────────────
    QTranslator qtTranslator;
    QTranslator appTranslator;

    // 1) Qt-Standardübersetzungen laden
    const QString qtLocale = QLocale().name();
    if (qtTranslator.load("qt_" + qtLocale,
                          QLibraryInfo::path(QLibraryInfo::TranslationsPath))) {
        app.installTranslator(&qtTranslator);
    }

    // 2) App-spezifische Übersetzung laden
    //    Priorität: Einstellungen > Systemsprache > Deutsch (Default)
    QSettings einstellungen;
    QString sprache = einstellungen.value("sprache", QString()).toString();

    if (sprache.isEmpty()) {
        // System-Sprache versuchen, Fallback auf Deutsch
        sprache = QLocale().name().left(2);
        if (!VERFUEGBARE_SPRACHEN.contains(sprache)) {
            sprache = "de"; // Deutsch als Default
        }
    }

    // .qm Datei suchen (neben der Binary oder im Installationspfad)
    QStringList suchPfade = {
        QCoreApplication::applicationDirPath() + "/../share/flipsicolor/i18n",
        QCoreApplication::applicationDirPath() + "/i18n",
        ":/i18n" // Fallback: eingebettete Ressource
    };

    bool uebersetzungGeladen = false;
    for (const QString& pfad : suchPfade) {
        if (appTranslator.load("flipsicolor_" + sprache, pfad)) {
            app.installTranslator(&appTranslator);
            uebersetzungGeladen = true;
            qDebug() << "Übersetzung geladen:" << sprache << "aus" << pfad;
            break;
        }
    }

    if (!uebersetzungGeladen) {
        qDebug() << "Keine Übersetzung für" << sprache << "gefunden, Deutsch wird verwendet.";
    }

    // ── App initialisieren ─────────────────────────────────────────────────
    flipsicolor::Application flipsiApp;
    flipsiApp.initialisieren();

    QQmlApplicationEngine engine;

    // Sprachinformationen für QML verfügbar machen
    engine.rootContext()->setContextProperty("verfuegbareSprachen", VERFUEGBARE_SPRACHEN);
    engine.rootContext()->setContextProperty("sprachNamen", SPRACHNAMEN);
    engine.rootContext()->setContextProperty("aktuelleSprache", sprache);

    const QUrl url(u"qrc:/qml/Main.qml"_qs);

    QObject::connect(&engine, &QQmlApplicationEngine::objectCreated,
                     &app, [url](QObject* obj, const QUrl& objUrl) {
        if (!obj && url == objUrl)
            QCoreApplication::exit(-1);
    }, Qt::QueuedConnection);

    engine.load(url);

    return app.exec();
}