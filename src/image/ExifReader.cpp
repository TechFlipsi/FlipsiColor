// FlipsiColor — EXIF-Leser Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/image/ExifReader.h>
#include <QFile>
#include <QDebug>
#include <libexif/exif-data.h>

namespace flipsicolor {

class ExifReader::Impl {
public:
    QVariantMap daten;
};

ExifReader::ExifReader(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
}

ExifReader::~ExifReader() = default;

static ExifEntry* exifEintragSuchen(ExifData* exifData, ExifIfd ifd, ExifTag tag)
{
    if (!exifData || !exifData->ifd[ifd])
        return nullptr;
    return exif_content_get_entry(exifData->ifd[ifd], tag);
}

QVariantMap ExifReader::lesen(const QString& pfad)
{
    m_impl->daten.clear();

    if (!QFile::exists(pfad)) return m_impl->daten;

    ExifData* exifData = exif_data_new_from_file(pfad.toUtf8().constData());
    if (!exifData) {
        qDebug() << "Keine EXIF-Daten in:" << pfad;
        return m_impl->daten;
    }

    ExifByteOrder ordnung = exif_data_get_byte_order(exifData);

    // Kamera-Hersteller
    ExifEntry* eintrag = exifEintragSuchen(exifData, EXIF_IFD_0, EXIF_TAG_MAKE);
    if (eintrag) {
        char puffer[1024];
        exif_entry_get_value(eintrag, puffer, sizeof(puffer));
        m_impl->daten["kameraHersteller"] = QString(puffer);
    }

    // Kamera-Modell
    eintrag = exifEintragSuchen(exifData, EXIF_IFD_0, EXIF_TAG_MODEL);
    if (eintrag) {
        char puffer[1024];
        exif_entry_get_value(eintrag, puffer, sizeof(puffer));
        m_impl->daten["kameraModell"] = QString(puffer);
    }

    // Brennweite
    eintrag = exifEintragSuchen(exifData, EXIF_IFD_EXIF, EXIF_TAG_FOCAL_LENGTH);
    if (eintrag) {
        ExifRational rat = exif_get_rational(eintrag->data, ordnung);
        double brennweite = (double)rat.numerator / (double)rat.denominator;
        m_impl->daten["brennweite"] = brennweite;
    }

    // ISO
    eintrag = exifEintragSuchen(exifData, EXIF_IFD_EXIF, EXIF_TAG_ISO_SPEED_RATINGS);
    if (eintrag) {
        char puffer[1024];
        exif_entry_get_value(eintrag, puffer, sizeof(puffer));
        m_impl->daten["iso"] = QString(puffer).toInt();
    }

    // Belichtungszeit
    eintrag = exifEintragSuchen(exifData, EXIF_IFD_EXIF, EXIF_TAG_EXPOSURE_TIME);
    if (eintrag) {
        ExifRational rat = exif_get_rational(eintrag->data, ordnung);
        double zeit = (double)rat.numerator / (double)rat.denominator;
        m_impl->daten["belichtungszeit"] = zeit;
    }

    // Blende
    eintrag = exifEintragSuchen(exifData, EXIF_IFD_EXIF, EXIF_TAG_FNUMBER);
    if (eintrag) {
        ExifRational rat = exif_get_rational(eintrag->data, ordnung);
        double blende = (double)rat.numerator / (double)rat.denominator;
        m_impl->daten["blende"] = blende;
    }

    exif_data_free(exifData);
    return m_impl->daten;
}

} // namespace flipsicolor