// FlipsiColor — Auto-Updater Implementierung
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

#include <flipsicolor/utils/AutoUpdater.h>
#include <flipsicolor/utils/Logger.h>
#include <QNetworkAccessManager>
#include <QNetworkRequest>
#include <QNetworkReply>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>
#include <QStandardPaths>
#include <QSettings>
#include <QDir>
#include <QProcess>
#include <QDesktopServices>
#include <QUrl>
#include <QFileInfo>

namespace flipsicolor {

class AutoUpdater::Impl {
public:
    QNetworkAccessManager networkManager;
    QTimer pruefTimer;

    bool updateVerfuegbar = false;
    QString neueVersionStr;
    QString aenderungenStr;
    QString downloadUrlStr;
    qint64 downloadGroesseVal = 0;
    UpdateKanal kanal = Stable;

    // GitHub API URL
    static constexpr const char* GITHUB_API_URL =
        "https://api.github.com/repos/TechFlipsi/FlipsiColor/releases";

    // Aktuelle.app-Version (wird im Konstruktor gesetzt)
    QVersionNumber aktuelleVersion;

    // Ignorierte Version (User hat "Überspringen" geklickt)
    QString ignorierteVersion;

    // Dateipfad für heruntergeladenes Update
    QString downloadPfad;

    void pruefungStarten();
    QStringList assetNamenFuerPlattform() const;
    QString platformIdentifier() const;
};

AutoUpdater::AutoUpdater(QObject* parent)
    : QObject(parent)
    , m_impl(std::make_unique<Impl>())
{
    m_impl->aktuelleVersion = QVersionNumber::fromString(
        QCoreApplication::applicationVersion());

    // Ignorierte Version laden
    QSettings einstellungen;
    m_impl->ignorierteVersion = einstellungen.value("update/ignorierteVersion").toString();

    // Automatische Prüfung: 30s nach Start, dann alle 24h
    m_impl->pruefTimer.setSingleShot(true);
    connect(&m_impl->pruefTimer, &QTimer::timeout, this, &AutoUpdater::pruefen);

    // Erste Prüfung nach 30 Sekunden (App muss erst laden)
    m_impl->pruefTimer.start(30000);
    Logger::info("AutoUpdater", "Initialisiert. Prüfung in 30s, dann alle 24h.");
}

AutoUpdater::~AutoUpdater() = default;

void AutoUpdater::pruefen()
{
    m_impl->pruefungStarten();
}

void AutoUpdater::Impl::pruefungStarten()
{
    QString url = GITHUB_API_URL;
    if (kanal == Beta) {
        url += "?per_page=10"; // Pre-releases einschließen
    }

    QNetworkRequest anfrage(QUrl(url));
    anfrage.setHeader(QNetworkRequest::UserAgentHeader,
        QString("FlipsiColor/%1").arg(aktuelleVersion.toString()));
    anfrage.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");

    QNetworkReply* antwort = networkManager.get(anfrage);

    QObject::connect(antwort, &QNetworkReply::finished, this,
        [this, antwort]() {
            antwort->deleteLater();

            if (antwort->error() != QNetworkReply::NoError) {
                Logger::warnung("AutoUpdater",
                    QString("Prüfung fehlgeschlagen: %1").arg(antwort->errorString()));
                // Nächste Prüfung in 1h
                pruefTimer.start(3600000);
                return;
            }

            QByteArray daten = antwort->readAll();
            QJsonDocument doc = QJsonDocument::fromJson(daten);

            if (!doc.isArray()) {
                Logger::warnung("AutoUpdater", "Unerwartetes JSON-Format von GitHub API");
                pruefTimer.start(3600000);
                return;
            }

            QJsonArray releases = doc.array();
            bool gefunden = false;

            for (const QJsonValue& wert : releases) {
                QJsonObject release = wert.toObject();

                // Beta-Kanal: auch Pre-releases
                bool istPreRelease = release["prerelease"].toBool();
                if (kanal == Stable && istPreRelease) {
                    continue;
                }

                QString tag = release["tag_name"].toString();
                // 'v' entfernen falls vorhanden
                if (tag.startsWith('v')) tag.remove(0, 1);

                QVersionNumber releaseVersion = QVersionNumber::fromString(tag);
                if (releaseVersion.isNull()) continue;

                // ═══════════════════════════════════════════════════════════════
                // DOWNGRADE-SCHUTZ: Niemals auf ältere Version downgraden
                // ═══════════════════════════════════════════════════════════════
                if (releaseVersion < aktuelleVersion) {
                    Logger::warnung("AutoUpdater",
                        QString("DOWNGRADE BLOCKIERT: v%1 < v%2 (aktuell). "
                                "Update auf ältere Version verweigert.")
                            .arg(tag, aktuelleVersion.toString()));
                    continue; // Ältere Version = komplett ignorieren
                }

                // Gleiche Version = kein Update nötig
                if (releaseVersion == aktuelleVersion) continue;

                // Ignorierte Version überspringen (User hat "Überspringen" geklickt)
                if (tag == ignorierteVersion) {
                    Logger::info("AutoUpdater",
                        QString("Version v%1 wird ignoriert (User-Entscheidung).").arg(tag));
                    continue;
                }

                // Neueste Version gefunden!
                QString releaseUrl;
                qint64 groesse = 0;

                // Passenden Asset für aktuelle Plattform finden
                QJsonArray assets = release["assets"].toArray();
                QStringList platformAssets = assetNamenFuerPlattform();

                for (const QJsonValue& assetWert : assets) {
                    QJsonObject asset = assetWert.toObject();
                    QString name = asset["name"].toString().toLower();

                    for (const QString& plattformAsset : platformAssets) {
                        if (name.contains(plattformAsset.toLower())) {
                            releaseUrl = asset["browser_download_url"].toString();
                            groesse = asset["size"].toInt();
                            break;
                        }
                    }
                    if (!releaseUrl.isEmpty()) break;
                }

                // Update-Info setzen
                updateVerfuegbar = true;
                neueVersionStr = tag;
                aenderungenStr = release["body"].toString();
                downloadUrlStr = releaseUrl;
                downloadGroesseVal = groesse;

                Logger::info("AutoUpdater",
                    QString("Update gefunden: v%1 (aktuell: v%2)")
                        .arg(tag, aktuelleVersion.toString()));

                gefunden = true;
                break; // Nur die neueste Version zeigen
            }

            if (!gefunden) {
                updateVerfuegbar = false;
                Logger::info("AutoUpdater", "Kein Update verfügbar.");
            }

            // Nächste Prüfung in 24h
            pruefTimer.start(86400000);
        });
}

void AutoUpdater::updateStarten()
{
    if (m_impl->downloadUrlStr.isEmpty()) {
        emit fehler("Kein Download-Link verfügbar.");
        return;
    }

    // ── DOWNGRADE-SCHUTZ: Nochmalige Prüfung vor dem Download ──────────
    QVersionNumber zielVersion = QVersionNumber::fromString(m_impl->neueVersionStr);
    if (zielVersion.isNull()) {
        emit fehler("Ungültige Zielversion.");
        return;
    }
    if (zielVersion <= m_impl->aktuelleVersion) {
        emit fehler(QString("DOWNGRADE BLOCKIERT: v%1 ≤ v%2 (aktuell). "
                             "Update auf ältere oder gleiche Version verweigert.")
                    .arg(m_impl->neueVersionStr, m_impl->aktuelleVersion.toString()));
        Logger::warnung("AutoUpdater",
            QString("DOWNGRADE-VERSUCH BLOCKIERT: Ziel v%1 <= aktuell v%2")
                .arg(m_impl->neueVersionStr, m_impl->aktuelleVersion.toString()));
        return;
    }

    // Plattform-spezifische Installation
#if defined(Q_OS_WIN)
    // Windows: Download .exe Installer, dann ausführen
    QString url = m_impl->downloadUrlStr;
    if (url.isEmpty()) {
        // Fallback: GitHub Release-Seite im Browser öffnen
        QDesktopServices::openUrl(QUrl(
            "https://github.com/TechFlipsi/FlipsiColor/releases/latest"));
        return;
    }
    // Installer herunterladen
    m_impl->downloadPfad = QStandardPaths::writableLocation(QStandardPaths::TempLocation)
        + "/FlipsiColor-Update-" + m_impl->neueVersionStr + ".exe";

    QNetworkRequest anfrage(QUrl(url));
    anfrage.setHeader(QNetworkRequest::UserAgentHeader, "FlipsiColor-Updater");
    QNetworkReply* antwort = m_impl->networkManager.get(anfrage);

    connect(antwort, &QNetworkReply::downloadProgress, this,
        [this](qint64 empfangen, qint64 gesamt) {
            double prozent = (gesamt > 0) ? (empfangen * 100.0 / gesamt) : 0.0;
            emit downloadFortschritt(empfangen, gesamt, prozent);
        });

    connect(antwort, &QNetworkReply::finished, this,
        [this, antwort]() {
            antwort->deleteLater();
            if (antwort->error() != QNetworkReply::NoError) {
                emit fehler(QString("Download fehlgeschlagen: %1").arg(antwort->errorString()));
                return;
            }
            QFile datei(m_impl->downloadPfad);
            if (datei.open(QIODevice::WriteOnly)) {
                datei.write(antwort->readAll());
                datei.close();
                emit downloadFertig(m_impl->downloadPfad);

                // Installer starten und App beenden
                QProcess::startDetached(m_impl->downloadPfad, {"/S"});
                QCoreApplication::quit();
            } else {
                emit fehler("Konnte Installer nicht speichern.");
            }
        });

#elif defined(Q_OS_MACOS)
    // macOS: Download .dmg, öffnen, User zieht in Applications
    QDesktopServices::openUrl(QUrl(
        "https://github.com/TechFlipsi/FlipsiColor/releases/latest"));

#elif defined(Q_OS_LINUX)
    // Linux: apt/pacaur/yay oder AppImage-Download
    // AppImage: Download + chmod +x
    QDesktopServices::openUrl(QUrl(
        "https://github.com/TechFlipsi/FlipsiColor/releases/latest"));
#endif
}

void AutoUpdater::spaeterErinnern()
{
    // Erneut in 4 Stunden prüfen
    m_impl->pruefTimer.start(14400000); // 4h
    Logger::info("AutoUpdater", "Erinnerung in 4 Stunden.");
}

void AutoUpdater::ignorieren()
{
    QSettings einstellungen;
    einstellungen.setValue("update/ignorierteVersion", m_impl->neueVersionStr);
    m_impl->ignorierteVersion = m_impl->neueVersionStr;
    m_impl->updateVerfuegbar = false;
    Logger::info("AutoUpdater",
        QString("Version v%1 wird ignoriert.").arg(m_impl->neueVersionStr));
}

bool AutoUpdater::updateVerfuegbar() const { return m_impl->updateVerfuegbar; }
QString AutoUpdater::neueVersion() const { return m_impl->neueVersionStr; }
QString AutoUpdater::aenderungen() const { return m_impl->aenderungenStr; }
QString AutoUpdater::downloadUrl() const { return m_impl->downloadUrlStr; }
qint64 AutoUpdater::downloadGroesse() const { return m_impl->downloadGroesseVal; }

void AutoUpdater::setKanal(UpdateKanal k) { m_impl->kanal = k; }
AutoUpdater::UpdateKanal AutoUpdater::kanal() const { return m_impl->kanal; }

QStringList AutoUpdater::Impl::assetNamenFuerPlattform() const
{
#if defined(Q_OS_WIN)
    return {"Setup", "Installer", ".exe"};
#elif defined(Q_OS_MACOS)
    return {".dmg", "macOS"};
#elif defined(Q_OS_LINUX)
    return {".AppImage", ".deb", ".rpm", "Linux"};
#else
    return {};
#endif
}

} // namespace flipsicolor