// FlipsiColor — Test-Hauptprogramm
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)

// FlipsiColor — MSVC <ctime>-Fix: <time.h> VOR allen anderen Headern inkludieren,
// da MSVC 14.44 sonst C2039/C2873 in <ctime> wirft, wenn Drittanbieter-Header
// (Qt, OpenCV, ONNX Runtime) vor <time.h>/<ctime> geladen werden.
#if defined(_MSC_VER)
#include <time.h>
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