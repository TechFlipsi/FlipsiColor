// FlipsiColor — Objektivkorrektur-Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include "flipsicolor/utils/Logger.h"
#include <flipsicolor/color/LensCorrector.h>
#include <lensfun.h>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>

namespace flipsicolor
{

    class LensCorrector::Impl
    {
    public:
        lfDatabase*     db       = nullptr;
        const lfCamera* kamera   = nullptr;
        const lfLens*   objektiv = nullptr;
    };

    LensCorrector::LensCorrector(QObject* parent) : QObject(parent), m_impl(std::make_unique<Impl>()) {}

    LensCorrector::~LensCorrector()
    {
        if ( m_impl->db )
            lf_db_destroy(m_impl->db);
    }

    bool LensCorrector::initialisieren()
    {
        m_impl->db = lf_db_new();
        if ( lf_db_load(m_impl->db) != LF_NO_ERROR )
        {
            fcWarn("Objektiv") << "Lensfun Datenbank konnte nicht geladen werden";
            return false;
        }
        fcInfo("Objektiv") << "Lensfun geladen:" << lf_db_get_cameras(m_impl->db) << "Kameras";
        return true;
    }

    bool LensCorrector::kameraSetzen(const QString& hersteller, const QString& modell)
    {
        if ( !m_impl->db )
            return false;

        const lfCamera** kameras =
            lf_db_find_cameras(m_impl->db, hersteller.toUtf8().constData(), modell.toUtf8().constData());

        if ( kameras && kameras[0] )
        {
            m_impl->kamera = kameras[0];
            fcDebug("Objektiv") << "Kamera gesetzt:" << kameras[0]->Maker << kameras[0]->Model;
            lf_free(kameras);
            return true;
        }
        lf_free(kameras);
        return false;
    }

    bool LensCorrector::objektivSetzen(const QString& hersteller, const QString& modell)
    {
        if ( !m_impl->db )
            return false;

        // lf_db_find_lenses braucht ein lfLens-Objekt als Such-Vorlage
        // Achtung: lfMLstr = char* (nicht const), QByteArray muss am Leben bleiben
        QByteArray herstellerBytes = hersteller.toUtf8();
        QByteArray modellBytes     = modell.toUtf8();
        lfLens     suchObjektiv{};
        suchObjektiv.Maker = herstellerBytes.data();
        suchObjektiv.Model = modellBytes.data();

        const lfLens** objektive = lf_db_find_lenses(m_impl->db, &suchObjektiv, 0);

        if ( objektive && objektive[0] )
        {
            m_impl->objektiv = objektive[0];
            fcDebug("Objektiv") << "Objektiv gesetzt:" << objektive[0]->Maker << objektive[0]->Model;
            lf_free(objektive);
            return true;
        }
        lf_free(objektive);
        return false;
    }

    cv::Mat LensCorrector::korrigieren(const cv::Mat& bild, float brennweite, float blende) const
    {
        if ( !m_impl->objektiv || bild.empty() )
            return bild.clone();

        // Lensfun Korrektur-Modifikation erstellen (C++ API)
        // Konstruktor: (lens, crop, width, height)
        float      crop = m_impl->kamera ? m_impl->kamera->CropFactor : 1.0f;
        lfModifier mod(m_impl->objektiv, crop, bild.cols, bild.rows);

        // Korrektur-Typen aktivieren via Initialize
        // Signatur: (lens, pixelFormat, focal, aperture, distance, scale, targeom, flags, reverse)
        mod.Initialize(m_impl->objektiv,
                       LF_PF_U16,
                       brennweite,
                       blende,
                       1.0f,
                       0.0f,
                       LF_RECTILINEAR,
                       LF_MODIFY_DISTORTION | LF_MODIFY_VIGNETTING | LF_MODIFY_TCA,
                       false);

        // Rückwärts-Abbildung generieren
        // TODO: Vollständige Implementierung mit cv::remap
        return bild.clone();
    }

}  // namespace flipsicolor