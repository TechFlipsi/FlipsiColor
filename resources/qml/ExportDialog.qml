import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Dialog {
    id: exportDialog
    title: qsTr("Export Image")
    modal: true
    standardButtons: Dialog.Ok | Dialog.Cancel
    anchors.centerIn: parent
    width: 480
    height: 520

    // ── Styling ────────────────────────────────────────────────────
    background: Rectangle {
        color: AppTheme.bgPanel
        radius: AppTheme.radiusLg
        border.color: AppTheme.borderDefault
    }

    header: Rectangle {
        height: 44
        color: AppTheme.bgSecondary
        radius: AppTheme.radiusLg

        Label {
            anchors {
                left: parent.left
                verticalCenter: parent.verticalCenter
                leftMargin: AppTheme.spacingXl
            }
            text: exportDialog.title
            font.pixelSize: AppTheme.fontSizeLg
            font.bold: true
            font.family: AppTheme.fontFamily
            color: AppTheme.textPrimary
        }
    }

    footer: Rectangle {
        height: 52
        color: AppTheme.bgSecondary

        RowLayout {
            anchors {
                right: parent.right
                verticalCenter: parent.verticalCenter
                rightMargin: AppTheme.spacingXl
            }
            spacing: AppTheme.spacingMd

            Button {
                text: qsTr("Cancel")
                flat: true
                font.family: AppTheme.fontFamily
                font.pixelSize: AppTheme.fontSizeBase
                onClicked: exportDialog.reject()

                background: Rectangle {
                    color: parent.hovered ? AppTheme.alpha(AppTheme.textPrimary, 0.08) : "transparent"
                    radius: AppTheme.radiusMd
                }
                contentItem: Label {
                    text: parent.text
                    color: AppTheme.textSecondary
                    font: parent.font
                    horizontalAlignment: Text.AlignHCenter
                    verticalAlignment: Text.AlignVCenter
                }
            }

            Button {
                text: qsTr("Export")
                font.family: AppTheme.fontFamily
                font.pixelSize: AppTheme.fontSizeBase
                onClicked: {
                    // Gather settings and emit to backend
                    exportDialog.accepted()
                }

                background: Rectangle {
                    color: AppTheme.accentPrimary
                    radius: AppTheme.radiusMd
                }
                contentItem: Label {
                    text: parent.text
                    color: AppTheme.textPrimary
                    font: parent.font
                    font.bold: true
                    horizontalAlignment: Text.AlignHCenter
                    verticalAlignment: Text.AlignVCenter
                }
            }
        }
    }

    // ── Content ────────────────────────────────────────────────────
    ScrollView {
        anchors.fill: parent
        clip: true

        ColumnLayout {
            width: parent.width - AppTheme.spacingXxl
            spacing: AppTheme.spacingLg
            anchors.horizontalCenter: parent.horizontalCenter

            Item { Layout.preferredHeight: AppTheme.spacingMd }

            // ── Format Selection ────────────────────────────────────
            SectionLabel { text: qsTr("Format") }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd

                FormatChip {
                    id: pngChip
                    text: "PNG"
                    description: qsTr("Lossless")
                    format: "png"
                    selected: true
                }
                FormatChip {
                    id: jpgChip
                    text: "JPEG"
                    description: qsTr("Smallest file")
                    format: "jpeg"
                }
                FormatChip {
                    id: tiffChip
                    text: "TIFF"
                    description: qsTr("Professional")
                    format: "tiff"
                }
                FormatChip {
                    id: webpChip
                    text: "WebP"
                    description: qsTr("Modern web")
                    format: "webp"
                }
            }

            // ── Quality Slider (JPEG/WebP) ─────────────────────────
            SectionLabel { text: qsTr("Quality") }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd

                Label {
                    text: qsTr("Quality:")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textSecondary
                }

                Slider {
                    id: qualitySlider
                    Layout.fillWidth: true
                    from: 10
                    to: 100
                    value: 90
                    stepSize: 5
                    live: true

                    background: Rectangle {
                        x: qualitySlider.leftPadding
                        y: qualitySlider.topPadding + qualitySlider.availableHeight / 2 - 1
                        implicitWidth: 100
                        implicitHeight: 3
                        width: qualitySlider.availableWidth
                        height: 3
                        radius: 1.5
                        color: AppTheme.borderDefault

                        Rectangle {
                            width: qualitySlider.visualPosition * parent.width
                            height: parent.height
                            radius: 1.5
                            color: AppTheme.accentPrimary
                        }
                    }

                    handle: Rectangle {
                        x: qualitySlider.leftPadding + qualitySlider.visualPosition * (qualitySlider.availableWidth - width)
                        y: qualitySlider.topPadding + qualitySlider.availableHeight / 2 - height / 2
                        implicitWidth: 14
                        implicitHeight: 14
                        radius: 7
                        color: AppTheme.accentPrimary
                        border.color: AppTheme.textPrimary
                        border.width: 1
                    }
                }

                Label {
                    Layout.preferredWidth: 36
                    text: Math.round(qualitySlider.value) + "%"
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontMono
                    color: AppTheme.textPrimary
                    horizontalAlignment: Text.AlignRight
                }
            }

            // ── Resolution ──────────────────────────────────────────
            SectionLabel { text: qsTr("Resolution") }

            ComboBox {
                id: resolutionCombo
                Layout.fillWidth: true
                model: [
                    qsTr("Original (no resize)"),
                    qsTr("4K (3840 × 2160)"),
                    qsTr("Full HD (1920 × 1080)"),
                    qsTr("HD (1280 × 720)"),
                    qsTr("Custom…")
                ]
                currentIndex: 0

                background: Rectangle {
                    color: AppTheme.bgInput
                    border.color: AppTheme.borderDefault
                    radius: AppTheme.radiusMd
                }

                contentItem: Label {
                    text: resolutionCombo.currentText
                    color: AppTheme.textPrimary
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    verticalAlignment: Text.AlignVCenter
                    leftPadding: AppTheme.spacingMd
                }

                indicator: Label {
                    x: parent.width - width - AppTheme.spacingMd
                    y: parent.height / 2 - height / 2
                    text: "\u25be"
                    color: AppTheme.textSecondary
                    font.pixelSize: AppTheme.fontSizeMd
                }

                delegate: ItemDelegate {
                    width: resolutionCombo.width
                    text: modelData
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    background: Rectangle {
                        color: highlighted ? AppTheme.alpha(AppTheme.accentPrimary, 0.15) : "transparent"
                    }
                    contentItem: Label {
                        text: parent.text
                        color: highlighted ? AppTheme.accentPrimary : AppTheme.textPrimary
                        font: parent.font
                        verticalAlignment: Text.AlignVCenter
                    }
                }

                popup: Popup {
                    y: resolutionCombo.height + 2
                    width: resolutionCombo.width
                    padding: 2
                    background: Rectangle {
                        color: AppTheme.bgCard
                        border.color: AppTheme.borderDefault
                        radius: AppTheme.radiusMd
                    }
                }
            }

            // ── Color Space ─────────────────────────────────────────
            SectionLabel { text: qsTr("Color Space") }

            ComboBox {
                id: colorSpaceCombo
                Layout.fillWidth: true
                model: ["sRGB", "Adobe RGB", "Display P3", "Rec. 709", "Rec. 2020"]
                currentIndex: 0

                background: Rectangle {
                    color: AppTheme.bgInput
                    border.color: AppTheme.borderDefault
                    radius: AppTheme.radiusMd
                }

                contentItem: Label {
                    text: colorSpaceCombo.currentText
                    color: AppTheme.textPrimary
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    verticalAlignment: Text.AlignVCenter
                    leftPadding: AppTheme.spacingMd
                }

                indicator: Label {
                    x: parent.width - width - AppTheme.spacingMd
                    y: parent.height / 2 - height / 2
                    text: "\u25be"
                    color: AppTheme.textSecondary
                    font.pixelSize: AppTheme.fontSizeMd
                }

                delegate: ItemDelegate {
                    width: colorSpaceCombo.width
                    text: modelData
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    background: Rectangle {
                        color: highlighted ? AppTheme.alpha(AppTheme.accentPrimary, 0.15) : "transparent"
                    }
                    contentItem: Label {
                        text: parent.text
                        color: highlighted ? AppTheme.accentPrimary : AppTheme.textPrimary
                        font: parent.font
                        verticalAlignment: Text.AlignVCenter
                    }
                }

                popup: Popup {
                    y: colorSpaceCombo.height + 2
                    width: colorSpaceCombo.width
                    padding: 2
                    background: Rectangle {
                        color: AppTheme.bgCard
                        border.color: AppTheme.borderDefault
                        radius: AppTheme.radiusMd
                    }
                }
            }

            // ── Output Information ──────────────────────────────────
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 40
                radius: AppTheme.radiusMd
                color: AppTheme.bgInput
                border.color: AppTheme.borderSubtle

                Label {
                    anchors.centerIn: parent
                    text: qsTr("Estimated size: ~2.4 MB")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textMuted
                }
            }

            Item { Layout.preferredHeight: AppTheme.spacingMd }
        }
    }

    // ── Sub-components ─────────────────────────────────────────────
    component SectionLabel: Label {
        Layout.fillWidth: true
        text: parent.text
        font.pixelSize: AppTheme.fontSizeSm
        font.bold: true
        font.family: AppTheme.fontFamily
        color: AppTheme.textMuted
    }

    component FormatChip: Rectangle {
        Layout.fillWidth: true
        Layout.preferredHeight: 56
        radius: AppTheme.radiusMd
        color: selected ? AppTheme.alpha(AppTheme.accentPrimary, 0.15) : AppTheme.bgInput
        border.color: selected ? AppTheme.accentPrimary : AppTheme.borderSubtle
        border.width: selected ? 1.5 : 1

        property string text: ""
        property string description: ""
        property string format: ""
        property bool selected: false
        signal selected()

        ColumnLayout {
            anchors.centerIn: parent
            spacing: 0

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: parent.parent.text
                font.pixelSize: AppTheme.fontSizeBase
                font.bold: true
                font.family: AppTheme.fontFamily
                color: parent.parent.selected ? AppTheme.accentPrimary : AppTheme.textPrimary
            }

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: parent.parent.description
                font.pixelSize: AppTheme.fontSizeXs
                font.family: AppTheme.fontFamily
                color: AppTheme.textMuted
            }
        }

        MouseArea {
            anchors.fill: parent
            cursorShape: Qt.PointingHandCursor
            onClicked: parent.selected = true
        }
    }
}
