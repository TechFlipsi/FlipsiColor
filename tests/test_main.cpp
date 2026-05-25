// FlipsiColor — Test-Hauptprogramm
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)

// FlipsiColor — MSVC <ctime>-Fix: Workaround für MSVC 14.44 Bug C2039/C2873.
// Inkludiert <time.h> und importiert Symbole manuell in std-Namespace,
// OHNE <ctime> zu inkludieren (das die Fehler auslöst).
// Siehe: include/flipsicolor/msvc_ctime_fix.h für Details.
#if defined(_MSC_VER)
#include "flipsicolor/msvc_ctime_fix.h"
#endif

#include <QtTest/QtTest>
#include "flipsicolor/core/Pipeline.h"
#include "flipsicolor/color/ColorManager.h"

class TestPipeline : public QObject
{
    Q_OBJECT

private slots:
    void testIntensitaetStandard()
    {
        flipsicolor::Pipeline pipeline;
        QCOMPARE(pipeline.codeFormerFidelityWeight(), 0.5f);
    }
};

class TestColorManager : public QObject
{
    Q_OBJECT

private slots:
    void testArbeitsfarbraum()
    {
        QCOMPARE(flipsicolor::ColorManager::ARBEITSFARBRAUM, "ProPhoto RGB");
    }
};

QTEST_MAIN(TestPipeline)
#include "test_main.moc"