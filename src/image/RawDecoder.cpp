// FlipsiColor — RAW-Dekodierer Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/image/RawDecoder.h>
#include <libraw/libraw.h>
#include <QDebug>
#include <opencv2/core/core.hpp>

namespace flipsicolor {

class RawDecoder::Impl {
public:
    libraw_data_t* rawData = nullptr;
};

RawDecoder::RawDecoder(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
}

RawDecoder::~RawDecoder()
{
    schliessen();
}

bool RawDecoder::laden(const QString& pfad)
{
    schliessen();

    m_impl->rawData = libraw_init(0);
    if (!m_impl->rawData) {
        qWarning() << "LibRaw konnte nicht initialisiert werden";
        return false;
    }

    int ergebnis = libraw_open_file(m_impl->rawData, pfad.toUtf8().constData());
    if (erg...[truncated]