// FlipsiColor — Verarbeitungs-Pipeline Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

// FlipsiColor — MSVC <ctime>-Fix: Workaround für MSVC 14.44 Bug C2039/C2873.
// Inkludiert <time.h> und importiert Symbole manuell in std-Namespace,
// OHNE <ctime> zu inkludieren (das die Fehler auslöst).
// Siehe: include/flipsicolor/msvc_ctime_fix.h für Details.
#if defined(_MSC_VER)
#include "flipsicolor/msvc_ctime_fix.h"
#endif

#include "flipsicolor/core/Pipeline.h"
#include <QDebug>
#include <QHash>

namespace flipsicolor {

Pipeline::Pipeline(QObject* parent)
    : QObject(parent)
{
}

void Pipeline::setIntensitaet(Intensitaet stufe)
{
    m_intensitaet = stufe;
    qDebug() << "Intensität gesetzt auf:" << static_cast<int>(stufe) << "(Leicht=0, Mittel=1, Stark=2)";
}

void Pipeline::setModus(BetriebsModus modus)
{
    m_modus = modus;
    qDebug() << "Betriebsmodus gesetzt auf:" << static_cast<int>(modus) << "(Ask=0, SmartLearn=1, Turbo=2)";
}

PipelineParams Pipeline::standardParamsFuerSzene(const QString& szenenTyp) const
{
    PipelineParams params;

    // Szenen-basierte Voreinstellungen
    // Jeder Szenentyp hat optimierte Basis-Parameter, die der Benutzer weiter anpassen kann.
    // Die Werte werden durch die Intensität skaliert.

    // Mapping: Parameter-Wert → skaliert je Szenentyp
    // Leicht = Faktor ~0.33, Mittel = Faktor ~1.0, Stark = Faktor ~1.5

    auto skaliere = [this](float basis) -> float {
        switch (m_intensitaet) {
            case Intensitaet::Leicht: return basis * 0.33f;
            case Intensitaet::Mittel: return basis;
            case Intensitaet::Stark:  return basis * 1.5f;
        }
        return basis;
    };

    if (szenenTyp == "landschaft") {
        // Landschaft: mehr Sättigung, mehr Schärfe, Kontrast erhöhen
        params.kontrast   = skaliere(15.0f);
        params.saettigung = skaliere(10.0f);
        params.vibranz    = skaliere(8.0f);
        params.schaerfeBetrag = skaliere(0.5f);
        params.lichter    = skaliere(-5.0f);
        params.schatten   = skaliere(10.0f);
        params.objektivkorrekturAktiv = true;
    }
    else if (szenenTyp == "portrait") {
        // Porträt: zurückhaltend, Hauttöne schonen, leichte Schärfe, Gesichtswiederherstellung
        params.kontrast   = skaliere(8.0f);
        params.saettigung = skaliere(3.0f);
        params.vibranz    = skaliere(5.0f);
        params.schaerfeBetrag = skaliere(0.2f);
        params.luminanzRauschen = skaliere(3.0f);
        params.objektivkorrekturAktiv = true;
        params.gesichtswiederherstellungAktiv = true;
    }
    else if (szenenTyp == "nacht") {
        // Nacht: Belichtung hoch, starkes Entrauschen, Kontrast senken
        params.belichtung  = skaliere(1.0f);
        params.kontrast    = skaliere(-10.0f);
        params.schatten    = skaliere(20.0f);
        params.luminanzRauschen = skaliere(15.0f);
        params.chrominanzRauschen = skaliere(10.0f);
        params.schaerfeBetrag = skaliere(0.1f);
        params.objektivkorrekturAktiv = false; // Bei Nacht oft nicht hilfreich
    }
    else if (szenenTyp == "makro") {
        // Makro: Details betonen, moderate Schärfe, Kontrast
        params.kontrast   = skaliere(10.0f);
        params.saettigung = skaliere(8.0f);
        params.schaerfeBetrag = skaliere(0.8f);
        params.lichter    = skaliere(-10.0f);
        params.schatten   = skaliere(5.0f);
        params.objektivkorrekturAktiv = true;
    }
    else if (szenenTyp == "sport" || szenenTyp == "action") {
        // Sport/Action: schnelle Bearbeitung, klare Kontraste, mäßige Schärfe
        params.kontrast   = skaliere(12.0f);
        params.saettigung = skaliere(6.0f);
        params.schaerfeBetrag = skaliere(0.6f);
        params.objektivkorrekturAktiv = false;
    }
    else if (szenenTyp == "essen" || szenenTyp == "food") {
        // Essen: warme Farben, hohe Sättigung, moderate Schärfe
        params.weissabgleichTemp = skaliere(5800.0f);
        params.weissabgleichTint = skaliere(5.0f);
        params.kontrast   = skaliere(8.0f);
        params.saettigung = skaliere(15.0f);
        params.vibranz    = skaliere(10.0f);
        params.schaerfeBetrag = skaliere(0.4f);
    }
    else if (szenenTyp == "unterwasser") {
        // Unterwasser: starke Weißabgleich-Korrektur, hohe Sättigung, Kontrast
        params.weissabgleichTemp = skaliere(7500.0f);
        params.weissabgleichTint = skaliere(-10.0f);
        params.kontrast   = skaliere(15.0f);
        params.saettigung = skaliere(20.0f);
        params.vibranz    = skaliere(12.0f);
        params.schatten   = skaliere(15.0f);
    }
    else {
        // Standard/Generisch: ausgewogene Parameter
        params.kontrast   = skaliere(8.0f);
        params.saettigung = skaliere(5.0f);
        params.vibranz    = skaliere(5.0f);
        params.schaerfeBetrag = skaliere(0.4f);
        params.objektivkorrekturAktiv = true;
    }

    // Turbo-Modus: aggressivere Parameter anwenden
    if (m_modus == BetriebsModus::Turbo) {
        params.kontrast   *= 1.2f;
        params.saettigung *= 1.1f;
        params.schaerfeBetrag *= 1.3f;
        params.luminanzRauschen *= 1.2f;
        params.chrominanzRauschen *= 1.2f;
        // Turbo deaktiviert rechenintensive Optionen für Geschwindigkeit
        params.objektivkorrekturAktiv = false;
        params.gesichtswiederherstellungAktiv = false;
    }

    // Smart-Learn-Modus: zurückhaltendere Basis-Parameter (KI lernt vom Benutzer)
    if (m_modus == BetriebsModus::SmartLearn) {
        params.kontrast   *= 0.8f;
        params.saettigung *= 0.8f;
        params.vibranz    *= 0.8f;
        params.schaerfeBetrag *= 0.7f;
    }

    return params;
}

float Pipeline::codeFormerFidelityWeight() const
{
    // CodeFormer fidelity weight je nach Intensität:
    // - Leicht (Light): Gewicht 0.7 — natürlicher Look, weniger Wiederherstellung
    // - Mittel (Default): Gewicht 0.5 — ausgewogene Wiederherstellung
    // - Stark (Heavy): Gewicht 0.3 — maximale Wiederherstellung, kann etwas künstlich wirken
    //
    // Niedrigeres Gewicht = stärkere Wiederherstellung (mehr CodeFormer-Einfluss)
    // Höheres Gewicht = mehr Originaltreue (weniger CodeFormer-Einfluss)

    switch (m_intensitaet) {
        case Intensitaet::Leicht: return 0.7f;
        case Intensitaet::Mittel: return 0.5f;
        case Intensitaet::Stark:  return 0.3f;
    }

    return 0.5f;
}

} // namespace flipsicolor
