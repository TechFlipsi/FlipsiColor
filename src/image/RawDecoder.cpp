// FlipsiColor — RAW-Dekodierer Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

// FlipsiColor — MSVC <ctime>-Fix: <time.h> VOR allen anderen Headern inkludieren,
// da MSVC 14.44 sonst C2039/C2873 in <ctime> wirft, wenn Drittanbieter-Header
// (Qt, OpenCV, ONNX Runtime) vor <time.h>/<ctime> geladen werden.
#if defined(_MSC_VER)
#include <time.h>
#endif

#include <flipsicolor/image/RawDecoder.h>
#include <libraw/libraw.h>
#include <QDebug>
#include <opencv2/core.hpp>

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
    if (ergebnis != LIBRAW_SUCCESS) {
        qWarning() << "LibRaw Fehler beim Öffnen:" << libraw_strerror(ergebnis);
        libraw_close(m_impl->rawData);
        m_impl->rawData = nullptr;
        return false;
    }

    ergebnis = libraw_unpack(m_impl->rawData);
    if (ergebnis != LIBRAW_SUCCESS) {
        qWarning() << "LibRaw Fehler beim Entpacken:" << libraw_strerror(ergebnis);
        libraw_close(m_impl->rawData);
        m_impl->rawData = nullptr;
        return false;
    }

    qDebug() << "RAW-Datei geladen:" << pfad;
    emit geladen(pfad);
    return true;
}

void RawDecoder::schliessen()
{
    if (m_impl->rawData) {
        libraw_close(m_impl->rawData);
        m_impl->rawData = nullptr;
    }
}

} // namespace flipsicolor