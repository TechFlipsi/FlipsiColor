// FlipsiColor — Farbmanagement-Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

// FlipsiColor — MSVC <ctime>-Fix: Workaround für MSVC 14.44 Bug C2039/C2873.
// Inkludiert <time.h> und importiert Symbole manuell in std-Namespace,
// OHNE <ctime> zu inkludieren (das die Fehler auslöst).
// Siehe: include/flipsicolor/msvc_ctime_fix.h für Details.
#if defined(_MSC_VER)
#include "flipsicolor/msvc_ctime_fix.h"
#endif

#include "flipsicolor/color/ColorManager.h"

#include <QDebug>
#include <QFileInfo>
#include <QDir>
#include <lcms2.h>

namespace flipsicolor {

struct ColorManager::Impl {
    cmsHPROFILE sRGBProfil = nullptr;
    cmsHPROFILE proPhotoProfil = nullptr;
    cmsHPROFILE adobeRGBProfil = nullptr;
    cmsHPROFILE monitorProfil = nullptr;
    cmsHTRANSFORM arbeitsbereichZuSRGB = nullptr;
    cmsHTRANSFORM arbeitsbereichZuMonitor = nullptr;
};

ColorManager::ColorManager(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
}

ColorManager::~ColorManager()
{
    if (m_impl->arbeitsbereichZuSRGB)
        cmsDeleteTransform(m_impl->arbeitsbereichZuSRGB);
    if (m_impl->arbeitsbereichZuMonitor)
        cmsDeleteTransform(m_impl->arbeitsbereichZuMonitor);
    if (m_impl->sRGBProfil)
        cmsCloseProfile(m_impl->sRGBProfil);
    if (m_impl->proPhotoProfil)
        cmsCloseProfile(m_impl->proPhotoProfil);
    if (m_impl->adobeRGBProfil)
        cmsCloseProfile(m_impl->adobeRGBProfil);
    if (m_impl->monitorProfil)
        cmsCloseProfile(m_impl->monitorProfil);
}

void ColorManager::initialisieren()
{
    // Standard-Profile erstellen
    m_impl->sRGBProfil = cmsCreate_sRGBProfile();
    m_impl->proPhotoProfil = cmsCreateProfilePlaceholder(nullptr);
    // ProPhoto RGB (ROMM RGB) Primaries
    cmsCIExyY whitePoint = {0.3457, 0.3585, 1.0}; // D50
    cmsCIExyYTRIPLE primaries = {
        {0.7347, 0.2653, 1.0},  // Rot
        {0.1596, 0.6801, 1.0},  // Grün
        {0.0366, 0.0001, 1.0}   // Blau
    };

    if (m_impl->proPhotoProfil) {
        cmsSetProfileVersion(m_impl->proPhotoProfil, 4.3);
        cmsSetColorSpace(m_impl->proPhotoProfil, cmsSigRgbData);
        cmsSetDeviceClass(m_impl->proPhotoProfil, cmsSigDisplayClass);
    }

    // Monitor-Profil erkennen
    QString monitorPfad = monitorProfilErkennen();
    if (!monitorPfad.isEmpty()) {
        m_impl->monitorProfil = cmsOpenProfileFromFile(monitorPfad.toUtf8().constData(), "r");
    }
    if (!m_impl->monitorProfil) {
        m_impl->monitorProfil = m_impl->sRGBProfil;
    }

    // Transform erstellen: Arbeitsfarbraum → sRGB (für Display)
    m_impl->arbeitsbereichZuSRGB = cmsCreateTransform(
        m_impl->proPhotoProfil, TYPE_RGB_16,
        m_impl->sRGBProfil, TYPE_RGB_16,
        INTENT_RELATIVE_COLORIMETRIC, 0
    );

    qDebug() << "Farbmanagement initialisiert: Arbeitsfarbraum =" << ARBEITSFARBRAUM;
}

QString ColorManager::monitorProfilErkennen() const
{
    // Versuche ICC-Profil vom Betriebssystem zu finden
#ifdef Q_OS_WIN
    // Windows: %SystemRoot%\System32\spool\drivers\color\
    return QString();
#elif defined(Q_OS_MACOS)
    // macOS: /Library/ColorSync/Profiles/
    return QString();
#else
    // Linux: ~/.local/share/icc/ oder /usr/share/color/icc/
    QFileInfo linuxProfile(QDir::homePath() + "/.local/share/icc/sRGB.icc");
    if (linuxProfile.exists())
        return linuxProfile.absoluteFilePath();
    return QString();
#endif
}

} // namespace flipsicolor
