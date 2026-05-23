// FlipsiColor — KI-gestützte Bild- & Videofarbkorrektur
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQuickStyle>
#include <QIcon>

#include "flipsicolor/core/Application.h"

int main(int argc, char* argv[])
{
    QGuiApplication app(argc, argv);
    app.setOrganizationName("TechFlipsi");
    app.setOrganizationDomain("techflipsi.com");
    app.setApplicationName("FlipsiColor");
    app.setApplicationVersion("0.1.0");

    QQuickStyle::setStyle("Fusion");

    flipsicolor::Application flipsiApp;
    flipsiApp.initialisieren();

    QQmlApplicationEngine engine;
    const QUrl url(u"qrc:/qml/Main.qml"_qs);

    QObject::connect(&engine, &QQmlApplicationEngine::objectCreated,
                     &app, [url](QObject* obj, const QUrl& objUrl) {
        if (!obj && url == objUrl)
            QCoreApplication::exit(-1);
    }, Qt::QueuedConnection);

    engine.load(url);

    return app.exec();
}