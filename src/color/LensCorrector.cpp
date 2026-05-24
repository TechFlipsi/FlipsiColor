// FlipsiColor — Objektivkorrektur-Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/color/LensCorrector.h>
#include <lensfun.h>
#include <QDebug>
#include <opencv2/core/core.hpp>
#include <opencv2/imgproc/imgproc.hpp>

namespace flipsicolor {

class LensCorrector::Impl {
public:
    lfDatabase* db = nullptr;
    lfCamera* kamera = nullptr;
    lfLens* objektiv = nullptr;
};

LensCorrector::LensCorrector(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
}

LensCorrector::~LensCorrector()
{
    if (m_impl->db)
        lf_db_destroy(m_impl->db);
}

bool LensCorrector::initialisieren()
{
    m_impl->db = lf_db_new();
    if (lf_db_load(m_impl->db) != LF_NO_ERROR) {
        qWarning() << "Lensfun Datenbank konnte nicht geladen werden";
        return false;
    }
    qDebug() << "Lensfun geladen:" << lf_db_get_cameras(m_impl->db) << "Kameras";
    return true;
}

bool LensCorrector::kameraSetzen(const QString& hersteller, const QString& modell)
{
    if (!m_impl->db) return false;

    const lfCamera** kameras = lf_db_find_cameras(m_impl->db,
        hersteller.toUtf8().constData(),
        modell.toUtf8().constData());

    if (kameras && kameras[0]) {
        m_impl->kamera = kameras[0];
        qDebug() << "Kamera gesetzt:" << kameras[0]->Maker << kameras[0]->Model;
        lf_free(kameras);
        return true;
    }
    lf_free(kameras);
    return false;
}

bool LensCorrector::objektivSetzen(const QString& hersteller, const QString& modell)
{
    if (!m_impl->db) return false;

    const lfLens** objektive = lf_db_find_lenses(m_impl->db, m_impl->kamera,
        hersteller.toUtf8().constData(),
        modell.toUtf8().constData());

    if (objektive && objektive[0]) {
        m_impl->objektiv = objektive[0];
        qDebug() << "Objektiv gesetzt:" << objektive[0]->Maker << objektive[0]->Model;
        lf_free(objektive);
        return true;
    }
    lf_free(objektive);
    return false;
}

cv::Mat LensCorrector::korrigieren(const cv::Mat& bild, float brennweite, float blende) const
{
    if (!m_impl->objektiv || bild.empty())
        return bild.clone();

    // Lensfun Korrektur-Modifikation erstellen
    lfModifier* mod = lf_modifier_create(
        m_impl->objektiv, LF_CF_DISTORTION | LF_CF_VIGNETTING | LF_CF_TCA,
        bild.cols, bild.rows, LF_PF_U16, brennweite, blende, 1.0f, 0.0f);

    if (!mod) return bild.clone();

    // Rückwärts-Abbildung generieren
    // TODO: Vollständige Implementierung mit cv::remap
    lf_modifier_destroy(mod);
    return bild.clone();
}

} // namespace flipsicolor
