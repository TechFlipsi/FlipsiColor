// FlipsiColor — App-Kern
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>

namespace flipsicolor {

class Application : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool gpuVerfuegbar READ gpuVerfuegbar NOTIFY gpuVerfuegbarChanged)
    Q_PROPERTY(QString gpuName READ gpuName NOTIFY gpuVerfuegbarChanged)
    Q_PROPERTY(int feedbackAnzahl READ feedbackAnzahl NOTIFY feedbackAnzahlChanged)

public:
    explicit Application(QObject* parent = nullptr);

    void initialisieren();

    bool gpuVerfuegbar() const { return m_gpuVerfuegbar; }
    QString gpuName() const { return m_gpuName; }
    int feedbackAnzahl() const { return m_feedbackAnzahl; }

signals:
    void gpuVerfuegbarChanged();
    void feedbackAnzahlChanged();

private:
    void gpuErkennen();
    void einstellungenLaden();
    void kiInitialisieren();

    bool m_gpuVerfuegbar = false;
    QString m_gpuName;
    int m_feedbackAnzahl = 0;
};

} // namespace flipsicolor