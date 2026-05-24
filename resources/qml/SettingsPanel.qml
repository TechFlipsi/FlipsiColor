import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Dialog {
    id: settingsPanel
    title: qsTr("Settings")
    modal: true
    standardButtons: Dialog.Ok | Dialog.Apply | Dialog.Cancel
    anchors.centerIn: parent
    width: 620
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
            text: settingsPanel.title
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
                onClicked: settingsPanel.reject()

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
                text: qsTr("Apply")
                flat: true
                font.family: AppTheme.fontFamily
                font.pixelSize: AppTheme.fontSizeBase
                onClicked: settingsPanel.accepted()

                background: Rectangle {
                    color: parent.hovered ? AppTheme.alpha(AppTheme.accentPrimary, 0.12) : "transparent"
                    radius: AppTheme.radiusMd
                }
                contentItem: Label {
                    text: parent.text
                    color: AppTheme.accentPrimary
                    font: parent.font
                    horizontalAlignment: Text.AlignHCenter
                    verticalAlignment: Text.AlignVCenter
                }
            }

            Button {
                text: qsTr("OK")
                font.family: AppTheme.fontFamily
                font.pixelSize: AppTheme.fontSizeBase
                onClicked: settingsPanel.accepted()

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
            spacing: AppTheme.spacingMd
            anchors.horizontalCenter: parent.horizontalCenter

            Item { Layout.preferredHeight: AppTheme.spacingSm }

            // ── Section: Language ───────────────────────────────────
            SettingsSection { text: qsTr("Language / Sprache") }

            ComboBox {
                id: languageCombo
                Layout.fillWidth: true
                model: [
                    "English",
                    "Deutsch (German)",
                    "Français (French)",
                    "Español (Spanish)",
                    "Italiano (Italian)",
                    "\u65e5\u672c\u8a9e (Japanese)",
                    "\ud55c\uad6d\uc5b4 (Korean)",
                    "\u4e2d\u6587 (Chinese)"
                ]
                currentIndex: 0

                background: Rectangle {
                    color: AppTheme.bgInput
                    border.color: AppTheme.borderDefault
                    radius: AppTheme.radiusMd
                }

                contentItem: Label {
                    text: languageCombo.currentText
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
                    width: languageCombo.width
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
                    y: languageCombo.height + 2
                    width: languageCombo.width
                    padding: 2
                    background: Rectangle {
                        color: AppTheme.bgCard
                        border.color: AppTheme.borderDefault
                        radius: AppTheme.radiusMd
                    }
                }
            }

            // ── Section: Appearance ─────────────────────────────────
            SettingsSection { text: qsTr("Appearance") }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd

                Label {
                    text: qsTr("Theme:")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textSecondary
                    Layout.preferredWidth: 100
                }

                ComboBox {
                    id: themeCombo
                    Layout.fillWidth: true
                    model: [qsTr("Dark"), qsTr("Darker"), qsTr("High Contrast")]
                    currentIndex: 0

                    background: Rectangle {
                        color: AppTheme.bgInput
                        border.color: AppTheme.borderDefault
                        radius: AppTheme.radiusMd
                    }

                    contentItem: Label {
                        text: themeCombo.currentText
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
                        width: themeCombo.width
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
                        y: themeCombo.height + 2
                        width: themeCombo.width
                        padding: 2
                        background: Rectangle {
                            color: AppTheme.bgCard
                            border.color: AppTheme.borderDefault
                            radius: AppTheme.radiusMd
                        }
                    }
                }
            }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd

                Label {
                    text: qsTr("Font Size:")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textSecondary
                    Layout.preferredWidth: 100
                }

                ComboBox {
                    id: fontSizeCombo
                    Layout.fillWidth: true
                    model: [qsTr("Small"), qsTr("Normal"), qsTr("Large")]
                    currentIndex: 1

                    background: Rectangle {
                        color: AppTheme.bgInput
                        border.color: AppTheme.borderDefault
                        radius: AppTheme.radiusMd
                    }

                    contentItem: Label {
                        text: fontSizeCombo.currentText
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
                        width: fontSizeCombo.width
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
                        y: fontSizeCombo.height + 2
                        width: fontSizeCombo.width
                        padding: 2
                        background: Rectangle {
                            color: AppTheme.bgCard
                            border.color: AppTheme.borderDefault
                            radius: AppTheme.radiusMd
                        }
                    }
                }
            }

            // ── Section: Performance / GPU ──────────────────────────
            SettingsSection { text: qsTr("Performance") }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd

                Label {
                    text: qsTr("GPU Acceleration:")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textSecondary
                    Layout.preferredWidth: 130
                }

                Switch {
                    id: gpuSwitch
                    checked: true

                    indicator: Rectangle {
                        implicitWidth: 40
                        implicitHeight: 22
                        radius: 11
                        color: gpuSwitch.checked ? AppTheme.accentPrimary : AppTheme.borderDefault

                        Rectangle {
                            x: gpuSwitch.checked ? parent.width - width - 2 : 2
                            y: 2
                            width: 18
                            height: 18
                            radius: 9
                            color: AppTheme.textPrimary

                            Behavior on x { NumberAnimation { duration: AppTheme.animFast } }
                        }
                    }

                    contentItem: Label {
                        text: gpuSwitch.checked ? qsTr("Enabled") : qsTr("Disabled")
                        font.pixelSize: AppTheme.fontSizeSm
                        font.family: AppTheme.fontFamily
                        color: AppTheme.textSecondary
                        leftPadding: 50
                        verticalAlignment: Text.AlignVCenter
                    }
                }
            }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd
                visible: gpuSwitch.checked

                Label {
                    text: qsTr("GPU Device:")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textSecondary
                    Layout.preferredWidth: 130
                }

                ComboBox {
                    id: gpuDeviceCombo
                    Layout.fillWidth: true
                    model: [qsTr("Auto (Recommended)"), "NVIDIA GeForce RTX 4080", "NVIDIA GeForce RTX 3070"]
                    currentIndex: 0

                    background: Rectangle {
                        color: AppTheme.bgInput
                        border.color: AppTheme.borderDefault
                        radius: AppTheme.radiusMd
                    }

                    contentItem: Label {
                        text: gpuDeviceCombo.currentText
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
                        width: gpuDeviceCombo.width
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
                        y: gpuDeviceCombo.height + 2
                        width: gpuDeviceCombo.width
                        padding: 2
                        background: Rectangle {
                            color: AppTheme.bgCard
                            border.color: AppTheme.borderDefault
                            radius: AppTheme.radiusMd
                        }
                    }
                }
            }

            // ── Section: AI Settings ────────────────────────────────
            SettingsSection { text: qsTr("AI Settings") }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd

                Label {
                    text: qsTr("AI Model:")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textSecondary
                    Layout.preferredWidth: 130
                }

                ComboBox {
                    id: aiModelCombo
                    Layout.fillWidth: true
                    model: [
                        qsTr("FlipsiColor Default"),
                        qsTr("Fast (Quality)"),
                        qsTr("Balanced"),
                        qsTr("Best Quality (Slow)")
                    ]
                    currentIndex: 1

                    background: Rectangle {
                        color: AppTheme.bgInput
                        border.color: AppTheme.borderDefault
                        radius: AppTheme.radiusMd
                    }

                    contentItem: Label {
                        text: aiModelCombo.currentText
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
                        width: aiModelCombo.width
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
                        y: aiModelCombo.height + 2
                        width: aiModelCombo.width
                        padding: 2
                        background: Rectangle {
                            color: AppTheme.bgCard
                            border.color: AppTheme.borderDefault
                            radius: AppTheme.radiusMd
                        }
                    }
                }
            }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd

                Label {
                    text: qsTr("Auto-Apply AI:")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textSecondary
                    Layout.preferredWidth: 130
                }

                Switch {
                    id: autoApplySwitch
                    checked: false

                    indicator: Rectangle {
                        implicitWidth: 40
                        implicitHeight: 22
                        radius: 11
                        color: autoApplySwitch.checked ? AppTheme.accentPrimary : AppTheme.borderDefault

                        Rectangle {
                            x: autoApplySwitch.checked ? parent.width - width - 2 : 2
                            y: 2
                            width: 18
                            height: 18
                            radius: 9
                            color: AppTheme.textPrimary

                            Behavior on x { NumberAnimation { duration: AppTheme.animFast } }
                        }
                    }

                    contentItem: Label {
                        text: autoApplySwitch.checked ? qsTr("Enabled") : qsTr("Disabled")
                        font.pixelSize: AppTheme.fontSizeSm
                        font.family: AppTheme.fontFamily
                        color: AppTheme.textSecondary
                        leftPadding: 50
                        verticalAlignment: Text.AlignVCenter
                    }
                }
            }

            // ── Section: Cache ──────────────────────────────────────
            SettingsSection { text: qsTr("Cache & Storage") }

            RowLayout {
                Layout.fillWidth: true
                spacing: AppTheme.spacingMd

                Label {
                    text: qsTr("Cache Size:")
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    color: AppTheme.textSecondary
                    Layout.preferredWidth: 130
                }

                Label {
                    text: "≈ 342 MB"
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontMono
                    color: AppTheme.textPrimary
                }

                Item { Layout.fillWidth: true }

                Button {
                    text: qsTr("Clear Cache")
                    flat: true
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily

                    background: Rectangle {
                        color: parent.hovered ? AppTheme.alpha(AppTheme.error, 0.12) : "transparent"
                        radius: AppTheme.radiusMd
                    }
                    contentItem: Label {
                        text: parent.text
                        color: AppTheme.error
                        font: parent.font
                        horizontalAlignment: Text.AlignHCenter
                        verticalAlignment: Text.AlignVCenter
                    }
                }
            }

            // ── Section: About ──────────────────────────────────────
            SettingsSection { text: qsTr("About") }

            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 44
                radius: AppTheme.radiusMd
                color: AppTheme.bgInput
                border.color: AppTheme.borderSubtle

                RowLayout {
                    anchors {
                        fill: parent
                        leftMargin: AppTheme.spacingMd
                        rightMargin: AppTheme.spacingMd
                    }
                    spacing: AppTheme.spacingMd

                    Label {
                        text: "FlipsiColor"
                        font.pixelSize: AppTheme.fontSizeSm
                        font.bold: true
                        font.family: AppTheme.fontFamily
                        color: AppTheme.textPrimary
                    }

                    Label {
                        text: "v0.1.0"
                        font.pixelSize: AppTheme.fontSizeSm
                        font.family: AppTheme.fontMono
                        color: AppTheme.accentPrimary
                    }

                    Rectangle {
                        Layout.preferredWidth: 1
                        Layout.preferredHeight: 14
                        color: AppTheme.borderSubtle
                    }

                    Label {
                        text: "Qt " + "6.x" + " | " + "C++20"
                        font.pixelSize: AppTheme.fontSizeSm
                        font.family: AppTheme.fontMono
                        color: AppTheme.textMuted
                    }
                }
            }

            // ── Section: Updates ──────────────────────────────────
            SettingsSection { text: qsTr("Updates") }

            Rectangle {
                id: updateSection
                Layout.fillWidth: true
                Layout.preferredHeight: updateAvailable ? 120 : 80
                radius: AppTheme.radiusMd
                color: AppTheme.bgInput
                border.color: updateAvailable ? AppTheme.accentPrimary : AppTheme.borderSubtle

                property bool updateAvailable: false
                property string newVersion: ""

                ColumnLayout {
                    anchors {
                        fill: parent
                        leftMargin: AppTheme.spacingMd
                        rightMargin: AppTheme.spacingMd
                        topMargin: AppTheme.spacingSm
                    }
                    spacing: AppTheme.spacingSm

                    RowLayout {
                        Layout.fillWidth: true
                        spacing: AppTheme.spacingMd

                        Label {
                            text: qsTr("Current Version:")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontFamily
                            color: AppTheme.textSecondary
                        }

                        Label {
                            text: "v0.1.0"
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontMono
                            color: AppTheme.textPrimary
                        }

                        Item { Layout.fillWidth: true }

                        Button {
                            text: qsTr("Check for Updates")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontFamily
                            flat: true

                            background: Rectangle {
                                color: parent.hovered ? AppTheme.alpha(AppTheme.accentPrimary, 0.12) : "transparent"
                                radius: AppTheme.radiusMd
                            }
                            contentItem: Label {
                                text: parent.text
                                color: AppTheme.accentPrimary
                                font: parent.font
                                horizontalAlignment: Text.AlignHCenter
                                verticalAlignment: Text.AlignVCenter
                            }
                            onClicked: {
                                // TODO: Connect to Application.updatePruefen()
                                updateSection.updateAvailable = false
                                console.log("Update-Prüfung gestartet")
                            }
                        }
                    }

                    // Update-Hinweis (nur sichtbar wenn Update verfügbar)
                    RowLayout {
                        Layout.fillWidth: true
                        visible: updateSection.updateAvailable
                        spacing: AppTheme.spacingMd

                        Rectangle {
                            Layout.preferredWidth: 8
                            Layout.preferredHeight: 8
                            radius: 4
                            color: AppTheme.accentPrimary

                            SequentialAnimation on opacity {
                                loops: Animation.Infinite
                                NumberAnimation { from: 1.0; to: 0.3; duration: 1000 }
                                NumberAnimation { from: 0.3; to: 1.0; duration: 1000 }
                            }
                        }

                        Label {
                            text: qsTr("Update available: v%1").arg(updateSection.newVersion)
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontFamily
                            color: AppTheme.accentPrimary
                            font.bold: true
                        }

                        Item { Layout.fillWidth: true }

                        Button {
                            text: qsTr("Install Update")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontFamily

                            background: Rectangle {
                                color: AppTheme.accentPrimary
                                radius: AppTheme.radiusMd
                            }
                            contentItem: Label {
                                text: parent.text
                                color: "#ffffff"
                                font: parent.font
                                font.bold: true
                                horizontalAlignment: Text.AlignHCenter
                                verticalAlignment: Text.AlignVCenter
                            }
                            onClicked: {
                                // TODO: Connect to Application.updateStarten()
                                console.log("Update-Installation gestartet")
                            }
                        }

                        Button {
                            text: qsTr("Later")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontFamily
                            flat: true

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
                            onClicked: {
                                updateSection.updateAvailable = false
                                console.log("Update auf später verschoben")
                            }
                        }
                    }
                }
            }

            Item { Layout.preferredHeight: AppTheme.spacingMd }
        }
    }

    // ── Component ──────────────────────────────────────────────────
    component SettingsSection: Rectangle {
        Layout.fillWidth: true
        Layout.preferredHeight: 28
        color: "transparent"

        required property string text

        Label {
            anchors {
                left: parent.left
                bottom: parent.bottom
                bottomMargin: 2
            }
            text: parent.text
            font.pixelSize: AppTheme.fontSizeSm
            font.bold: true
            font.family: AppTheme.fontFamily
            color: AppTheme.textMuted
        }
    }

    onAccepted: {
        // Save all settings to backend
        console.log("Settings saved")
    }
}
