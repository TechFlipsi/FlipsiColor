// FlipsiColor — Farbmanagement
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#pragma once

#include <memory>
#include <QObject>
#include <QString>

// OpenCV Vorwärtsdeklaration (vermeidet Header-Abhängigkeit)
namespace cv
{
    class Mat;
}

namespace flipsicolor
{

    class ColorManager : public QObject
    {
        Q_OBJECT

    public:
        explicit ColorManager(QObject* parent = nullptr);
        ~ColorManager();

        // Arbeitsfarbraum = ProPhoto RGB (verhindert Clipping bei Bearbeitung)
        static constexpr const char* ARBEITSFARBRAUM = "ProPhoto RGB";

        void    initialisieren();
        QString monitorProfilErkennen() const;

    private:
        struct Impl;
        std::unique_ptr<Impl> m_impl;
    };

    class WhiteBalance : public QObject
    {
        Q_OBJECT

    public:
        explicit WhiteBalance(QObject* parent = nullptr);

        // Statistische Methoden (kein Modell, <1ms)
        struct WBErgebnis
        {
            double temperatur;
            double tint;
        };

        WBErgebnis grayWorld(const cv::Mat& bild) const;
        WBErgebnis shadesOfGray(const cv::Mat& bild, int m = 10) const;
        WBErgebnis autoWB(const cv::Mat& bild) const;  // Kombiniert beide
    };

}  // namespace flipsicolor