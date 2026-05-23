// FlipsiColor — Haupt-QML
// Copyright (C) 2026 Fabian Kirchweger (TechFlipsi)
// SPDX-License-Identifier: GPL-3.0-or-later

import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

ApplicationWindow {
    id: fenster
    width: 1280
    height: 800
    minimumWidth: 960
    minimumHeight: 600
    title: "FlipsiColor"
    color: "#1a1a2e"

    // Seitenleiste (nur Icons)
    RowLayout {
        anchors.fill: parent
        spacing: 0

        Rectangle {
            Layout.preferredWidth: 64
            Layout.fillHeight: true
            color: "#16213e"

            Column {
                anchors.horizontalCenter: parent.horizontalCenter
                anchors.top: parent.top
                anchors.topMargin: 16
                spacing: 16

                // Bild-Modus
                ToolButton {
                    icon.source: "qrc:/icons/image.svg"
                    icon.color: "#e94560"
                    ToolTip.text: qsTr("Bild")
                    ToolTip.visible: hovered
                }

                // Video-Modus
                ToolButton {
                    icon.source: "qrc:/icons/video.svg"
                    icon.color: "#a0a0b0"
                    ToolTip.text: qsTr("Video")
                    ToolTip.visible: hovered
                }

                // Einstellungen
                ToolButton {
                    icon.source: "qrc:/icons/settings.svg"
                    icon.color: "#a0a0b0"
                    ToolTip.text: qsTr("Einstellungen")
                    ToolTip.visible: hovered
                    onClicked: einstellungenPanel.open()
                }
            }
        }

        // Hauptinhaltsbereich
        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: "#1a1a2e"

            Label {
                anchors.centerIn: parent
                text: "FlipsiColor v0.1.0"
                font.pixelSize: 32
                font.bold: true
                color: "#e94560"
            }
        }
    }

    Drawer {
        id: einstellungenPanel
        width: 320
        height: parent.height
        edge: Qt.RightEdge

        ColumnLayout {
            anchors.fill: parent
            anchors.margins: 16
            spacing: 12

            Label {
                text: qsTr("Einstellungen")
                font.pixelSize: 20
                font.bold: true
                Layout.fillWidth: true
            }

            // Sprachauswahl
            Label {
                text: qsTr("Sprache")
                font.pixelSize: 14
            }
            ComboBox {
                id: sprachAuswahl
                Layout.fillWidth: true
                model: verfuegbareSprachen
                currentIndex: verfuegbareSprachen.indexOf(aktuelleSprache)
                onActivated: {
                    // Sprachwechsel erfordert Neustart
                    einstellungen.value("sprache", model[index]);
                    neustartDialog.open();
                }
            }

            Label {
                text: qsTr("Thema")
                font.pixelSize: 14
            }
            ComboBox {
                Layout.fillWidth: true
                model: [qsTr("Dunkel"), qsTr("Hell"), qsTr("System")]
            }
        }
    }

    Dialog {
        id: neustartDialog
        title: qsTr("Sprache geändert")
        modal: true
        anchors.centerIn: parent
        standardButtons: Dialog.Ok | Dialog.Cancel

        Label {
            text: qsTr("Die Sprache wird nach einem Neustart wirksam.")
        }

        onAccepted: Qt.quit()
    }
}