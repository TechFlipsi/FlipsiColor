// FlipsiColor — Test-Hauptprogramm
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)

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