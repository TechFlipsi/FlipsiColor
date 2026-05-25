// FlipsiColor — Haupt-QML
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

ApplicationWindow {
    id: fenster
    width: 1440
    height: 900
    minimumWidth: 960
    minimumHeight: 600
    title: "FlipsiColor"
    color: AppTheme.bgPrimary
    flags: Qt.Window | Qt.WindowMinMaxButtonsHint

    // 3-Panel Layout wie DaVinci Resolve
    RowLayout {
        anchors.fill: parent
        spacing: 0

        // Linke Seitenleiste (Werkzeuge)
        Sidebar {
            Layout.preferredWidth: 48
            Layout.fillHeight: true
            onModusGeaendert: ajustPanel.aktuellerModus = modus
            onEinstellungenAnfordern: einstellungenDialog.open()
        }

        // Mitte: Bild/Video-Anzeige
        CanvasArea {
            Layout.fillWidth: true
            Layout.fillHeight: true
        }

        // Rechts: Einstellungs-Panel
        AdjustPanel {
            id: ajustPanel
            Layout.preferredWidth: 320
            Layout.fillHeight: true
        }
    }

    // Statusleiste unten
    footer: Rectangle {
        height: 28
        color: AppTheme.bgSecondary
        Layout.fillWidth: true

        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 12
            anchors.rightMargin: 12
            spacing: 16

            Label {
                text: qsTr("Bereit")
                font.pixelSize: 11
                color: AppTheme.textSecondary
                Layout.fillWidth: true
            }

            Label {
                text: "GPU: —"
                font.pixelSize: 11
                font.family: "JetBrains Mono"
                color: AppTheme.textSecondary
            }

            Label {
                text: "v0.2.0"
                font.pixelSize: 11
                font.family: "JetBrains Mono"
                color: AppTheme.textMuted
            }
        }
    }

    // Export-Dialog
    ExportDialog {
        id: exportDialog
    }

    // Einstellungen-Dialog
    SettingsPanel {
        id: einstellungenDialog
    }

    // Erststart-Assistent
    FirstStartWizard {
        id: erststartAssistent
    }

    // Tastaturkürzel
    Shortcut {
        sequence: "Ctrl+O"
        onActivated: /* bildOeffnen() */ {}
    }
    Shortcut {
        sequence: "Ctrl+E"
        onActivated: exportDialog.open()
    }
    Shortcut {
        sequence: "Ctrl+,"
        onActivated: einstellungenDialog.open()
    }
    Shortcut {
        sequence: "F11"
        onActivated: fenster.visibility = fenster.visibility === Window.FullScreen ? Window.Windowed : Window.FullScreen
    }
}