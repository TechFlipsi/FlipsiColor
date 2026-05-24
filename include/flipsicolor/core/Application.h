// FlipsiColor — App-Kern
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <QObject>
#include <QString>
#include <memory>

namespace flipsicolor {

class AutoUpdater;

class Application : public QObject
{
    Q_OBJECT
    Q_PROPERTY(bool gpuVerfuegbar READ gpuVerfuegbar NOTIFY gpuVerfuegbarChanged)
    Q_PROPERTY(QString gpuName READ gpuName NOTIFY gpuVerfuegbarChanged)
    Q_PROPERTY(int feedbackAnzahl READ feedbackAnzahl NOTIFY feedbackAnzahlChanged)
    Q_PROPERTY(bool updateVerfuegbar READ updateVerfuegbar NOTIFY updateVerfuegbarChanged)
    Q_PROPERTY(QString neueVersion READ neueVersion NOTIFY neueVersionChanged)

public:
    explicit Application(QObject* parent = nullptr);
    ~Application(); // Destruktor im .cpp (unique_ptr<AutoUpdater> braucht vollständigen Typ)

    void initialisieren();

    bool gpuVerfuegbar() const { return m_gpuVerfuegbar; }
    QString gpuName() const { return m_gpuName; }
    int feedbackAnzahl() const { return m_feedbackAnzahl; }
    bool updateVerfuegbar() const;
    QString neueVersion() const;

    Q_INVOKABLE void updatePruefen();
    Q_INVOKABLE void updateStarten();
    Q_INVOKABLE void updateIgnorieren();

signals:
    void gpuVerfuegbarChanged();
    void feedbackAnzahlChanged();
    void updateVerfuegbarChanged(bool verfuegbar);
    void neueVersionChanged(const QString& version);

private:
    void gpuErkennen();
    void einstellungenLaden();
    void kiInitialisieren();

    bool m_gpuVerfuegbar = false;
    QString m_gpuName;
    int m_feedbackAnzahl = 0;
    std::unique_ptr<AutoUpdater> m_updater;
};

} // namespace flipsicolor