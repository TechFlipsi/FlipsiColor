import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import "."

Dialog {
    id: firstStartWizard
    title: qsTr("Welcome to FlipsiColor")
    modal: true
    anchors.centerIn: parent
    width: 560
    height: 480

    // Prevent closing without completing
    property int currentStep: 0
    property int totalSteps: 3

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
            text: firstStartWizard.title
            font.pixelSize: AppTheme.fontSizeLg
            font.bold: true
            font.family: AppTheme.fontFamily
            color: AppTheme.textPrimary
        }
    }

    footer: Rectangle {
        height: 60
        color: AppTheme.bgSecondary

        RowLayout {
            anchors {
                fill: parent
                leftMargin: AppTheme.spacingXl
                rightMargin: AppTheme.spacingXl
            }
            spacing: AppTheme.spacingMd

            // Progress dots
            Row {
                Layout.alignment: Qt.AlignVCenter
                spacing: AppTheme.spacingSm

                Repeater {
                    model: firstStartWizard.totalSteps
                    Rectangle {
                        width: 8
                        height: 8
                        radius: 4
                        color: index <= firstStartWizard.currentStep
                               ? AppTheme.accentPrimary
                               : AppTheme.borderDefault
                        opacity: index <= firstStartWizard.currentStep ? 1.0 : 0.4
                    }
                }
            }

            Item { Layout.fillWidth: true }

            Button {
                text: firstStartWizard.currentStep === 0
                      ? qsTr("Previous")
                      : firstStartWizard.currentStep === firstStartWizard.totalSteps - 1
                        ? ""
                        : qsTr("Previous")
                flat: true
                visible: firstStartWizard.currentStep > 0
                font.family: AppTheme.fontFamily
                font.pixelSize: AppTheme.fontSizeBase

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
                    if (firstStartWizard.currentStep > 0)
                        firstStartWizard.currentStep--
                }
            }

            Button {
                text: firstStartWizard.currentStep === firstStartWizard.totalSteps - 1
                      ? qsTr("Get Started! \ud83c\udf89")
                      : qsTr("Next \u2192")
                font.family: AppTheme.fontFamily
                font.pixelSize: AppTheme.fontSizeBase

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

                onClicked: {
                    if (firstStartWizard.currentStep < firstStartWizard.totalSteps - 1) {
                        firstStartWizard.currentStep++
                    } else {
                        // Final step: save settings and close
                        finishWizard()
                    }
                }
            }
        }
    }

    // ── Content Stack ──────────────────────────────────────────────
    StackLayout {
        anchors.fill: parent
        currentIndex: firstStartWizard.currentStep

        // ── Step 1: Welcome ────────────────────────────────────────
        ColumnLayout {
            anchors.centerIn: parent
            width: parent.width - AppTheme.spacingXxl * 2
            spacing: AppTheme.spacingXl

            // Logo / icon
            Rectangle {
                Layout.preferredWidth: 72
                Layout.preferredHeight: 72
                Layout.alignment: Qt.AlignHCenter
                radius: AppTheme.radiusXl
                color: AppTheme.alpha(AppTheme.accentPrimary, 0.12)
                border.color: AppTheme.alpha(AppTheme.accentPrimary, 0.3)
                border.width: 1

                Label {
                    anchors.centerIn: parent
                    text: "\ud83c\udfa8"
                    font.pixelSize: 36
                }
            }

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: qsTr("Welcome to FlipsiColor!")
                font.pixelSize: AppTheme.fontSizeXxl
                font.bold: true
                font.family: AppTheme.fontFamily
                color: AppTheme.textPrimary
            }

            Label {
                Layout.alignment: Qt.AlignHCenter
                Layout.maximumWidth: 400
                text: qsTr("Professional AI-powered color correction for photos and videos. FlipsiColor combines cutting-edge AI with professional-grade tools to transform your workflow.")
                font.pixelSize: AppTheme.fontSizeMd
                font.family: AppTheme.fontFamily
                color: AppTheme.textSecondary
                wrapMode: Text.WordWrap
                horizontalAlignment: Text.AlignHCenter
                lineHeight: 1.5
            }

            // Feature highlights
            RowLayout {
                Layout.alignment: Qt.AlignHCenter
                spacing: AppTheme.spacingXxl

                FeatureChip {
                    icon: "\u2728"
                    title: qsTr("AI-Assisted")
                    subtitle: qsTr("Smart color suggestions")
                }
                FeatureChip {
                    icon: "\ud83c\udfa5"
                    title: qsTr("Video Support")
                    subtitle: qsTr("Up to 8K resolution")
                }
                FeatureChip {
                    icon: "\u2699\ufe0f"
                    title: qsTr("Professional")
                    subtitle: qsTr("Full manual control")
                }
            }
        }

        // ── Step 2: Language ───────────────────────────────────────
        ColumnLayout {
            anchors.centerIn: parent
            width: parent.width - AppTheme.spacingXxl * 2
            spacing: AppTheme.spacingXl

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: qsTr("Choose Your Language")
                font.pixelSize: AppTheme.fontSizeXxl
                font.bold: true
                font.family: AppTheme.fontFamily
                color: AppTheme.textPrimary
            }

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: qsTr("FlipsiColor supports multiple languages. You can change this later in Settings.")
                font.pixelSize: AppTheme.fontSizeMd
                font.family: AppTheme.fontFamily
                color: AppTheme.textSecondary
                wrapMode: Text.WordWrap
                horizontalAlignment: Text.AlignHCenter
            }

            // Language list
            ColumnLayout {
                Layout.alignment: Qt.AlignHCenter
                Layout.preferredWidth: 300
                spacing: AppTheme.spacingSm

                Repeater {
                    model: [
                        { flag: "\ud83c\uddec\ud83c\udde7", name: "English", code: "en" },
                        { flag: "\ud83c\udde9\ud83c\uddea", name: "Deutsch", code: "de" },
                        { flag: "\ud83c\uddeb\ud83c\uddf7", name: "Français", code: "fr" },
                        { flag: "\ud83c\uddea\ud83c\uddf8", name: "Español", code: "es" },
                        { flag: "\ud83c\uddee\ud83c\uddf9", name: "Italiano", code: "it" },
                        { flag: "\ud83c\uddef\ud83c\uddf5", name: "\u65e5\u672c\u8a9e", code: "ja" },
                    ]

                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 38
                        radius: AppTheme.radiusMd
                        color: selectedLanguage === modelData.code
                               ? AppTheme.alpha(AppTheme.accentPrimary, 0.15)
                               : langMouse.containsMouse
                                 ? AppTheme.alpha(AppTheme.textPrimary, 0.06)
                                 : "transparent"

                        border.color: selectedLanguage === modelData.code
                                      ? AppTheme.accentPrimary
                                      : "transparent"
                        border.width: 1

                        RowLayout {
                            anchors {
                                fill: parent
                                leftMargin: AppTheme.spacingMd
                                rightMargin: AppTheme.spacingMd
                            }
                            spacing: AppTheme.spacingMd

                            Label {
                                text: modelData.flag
                                font.pixelSize: AppTheme.fontSizeLg
                            }

                            Label {
                                text: modelData.name
                                font.pixelSize: AppTheme.fontSizeBase
                                font.family: AppTheme.fontFamily
                                color: AppTheme.textPrimary
                            }

                            Item { Layout.fillWidth: true }

                            Rectangle {
                                visible: selectedLanguage === modelData.code
                                Layout.preferredWidth: 18
                                Layout.preferredHeight: 18
                                radius: 9
                                color: AppTheme.accentPrimary

                                Label {
                                    anchors.centerIn: parent
                                    text: "\u2713"
                                    font.pixelSize: 11
                                    color: AppTheme.textPrimary
                                }
                            }
                        }

                        MouseArea {
                            id: langMouse
                            anchors.fill: parent
                            hoverEnabled: true
                            cursorShape: Qt.PointingHandCursor
                            onClicked: selectedLanguage = modelData.code
                        }
                    }
                }
            }
        }

        // ── Step 3: GPU Check ──────────────────────────────────────
        ColumnLayout {
            anchors.centerIn: parent
            width: parent.width - AppTheme.spacingXxl * 2
            spacing: AppTheme.spacingXl

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: qsTr("Hardware Check")
                font.pixelSize: AppTheme.fontSizeXxl
                font.bold: true
                font.family: AppTheme.fontFamily
                color: AppTheme.textPrimary
            }

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: qsTr("Checking your system for optimal performance…")
                font.pixelSize: AppTheme.fontSizeMd
                font.family: AppTheme.fontFamily
                color: AppTheme.textSecondary
            }

            // GPU detection card
            Rectangle {
                Layout.alignment: Qt.AlignHCenter
                Layout.preferredWidth: 400
                Layout.preferredHeight: 160
                radius: AppTheme.radiusLg
                color: AppTheme.bgCard
                border.color: AppTheme.borderDefault
                border.width: 1

                ColumnLayout {
                    anchors {
                        fill: parent
                        margins: AppTheme.spacingXl
                    }
                    spacing: AppTheme.spacingMd

                    // GPU Info Row
                    RowLayout {
                        Layout.fillWidth: true
                        spacing: AppTheme.spacingMd

                        Rectangle {
                            Layout.preferredWidth: 40
                            Layout.preferredHeight: 40
                            radius: AppTheme.radiusMd
                            color: gpuDetected
                                   ? AppTheme.alpha(AppTheme.success, 0.15)
                                   : AppTheme.alpha(AppTheme.warning, 0.15)

                            Label {
                                anchors.centerIn: parent
                                text: gpuDetected ? "\u2705" : "\u26a0\ufe0f"
                                font.pixelSize: 18
                            }
                        }

                        ColumnLayout {
                            spacing: 0

                            Label {
                                text: gpuDetected
                                      ? qsTr("GPU Detected")
                                      : qsTr("No GPU Found")
                                font.pixelSize: AppTheme.fontSizeMd
                                font.bold: true
                                font.family: AppTheme.fontFamily
                                color: gpuDetected ? AppTheme.success : AppTheme.warning
                            }

                            Label {
                                text: gpuDetected
                                      ? gpuDeviceName
                                      : qsTr("Falling back to CPU rendering. Some features may be slower.")
                                font.pixelSize: AppTheme.fontSizeSm
                                font.family: AppTheme.fontFamily
                                color: AppTheme.textSecondary
                                wrapMode: Text.WordWrap
                                Layout.fillWidth: true
                            }
                        }
                    }

                    // Separator
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 1
                        color: AppTheme.borderSubtle
                    }

                    // GPU Details
                    GridLayout {
                        Layout.fillWidth: true
                        columns: 2
                        rowSpacing: AppTheme.spacingSm
                        columnSpacing: AppTheme.spacingXl

                        Label {
                            text: qsTr("Device:")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontFamily
                            color: AppTheme.textMuted
                        }
                        Label {
                            text: gpuDetected ? gpuDeviceName : qsTr("N/A")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontMono
                            color: AppTheme.textPrimary
                        }

                        Label {
                            text: qsTr("VRAM:")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontFamily
                            color: AppTheme.textMuted
                        }
                        Label {
                            text: gpuDetected ? gpuVRAM : qsTr("N/A")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontMono
                            color: AppTheme.textPrimary
                        }

                        Label {
                            text: qsTr("API:")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontFamily
                            color: AppTheme.textMuted
                        }
                        Label {
                            text: gpuDetected ? "Vulkan / CUDA" : qsTr("CPU Only")
                            font.pixelSize: AppTheme.fontSizeSm
                            font.family: AppTheme.fontMono
                            color: AppTheme.textPrimary
                        }
                    }
                }
            }

            // Retry button
            Button {
                Layout.alignment: Qt.AlignHCenter
                text: qsTr("Re-check Hardware")
                flat: true
                font.pixelSize: AppTheme.fontSizeSm
                font.family: AppTheme.fontFamily

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

                onClicked: detectGPU()
            }
        }
    }

    // ── Sub-components ─────────────────────────────────────────────
    component FeatureChip: ColumnLayout {
        spacing: AppTheme.spacingSm

        property string icon: ""
        property string title: ""
        property string subtitle: ""

        Rectangle {
            Layout.preferredWidth: 44
            Layout.preferredHeight: 44
            Layout.alignment: Qt.AlignHCenter
            radius: AppTheme.radiusMd
            color: AppTheme.alpha(AppTheme.accentPrimary, 0.1)

            Label {
                anchors.centerIn: parent
                text: parent.parent.icon
                font.pixelSize: 20
            }
        }

        Label {
            Layout.alignment: Qt.AlignHCenter
            text: parent.parent.title
            font.pixelSize: AppTheme.fontSizeSm
            font.bold: true
            font.family: AppTheme.fontFamily
            color: AppTheme.textPrimary
        }

        Label {
            Layout.alignment: Qt.AlignHCenter
            text: parent.parent.subtitle
            font.pixelSize: AppTheme.fontSizeXs
            font.family: AppTheme.fontFamily
            color: AppTheme.textMuted
        }
    }

    // ── State ──────────────────────────────────────────────────────
    property string selectedLanguage: "en"
    property bool gpuDetected: false
    property string gpuDeviceName: ""
    property string gpuVRAM: ""

    // ── Functions ──────────────────────────────────────────────────
    function detectGPU() {
        // In production, this calls the C++ backend to query GPU info
        // For now, simulate detection with reasonable defaults
        gpuDetected = true
        gpuDeviceName = "NVIDIA GeForce RTX 4080"
        gpuVRAM = "16 GB GDDR6X"
    }

    function finishWizard() {
        // Save selected language
        console.log("Language selected:", selectedLanguage)
        console.log("GPU detected:", gpuDetected, gpuDeviceName)

        // Mark first-start as complete in backend settings
        firstStartWizard.accept()
    }

    // Auto-detect GPU on step 3
    onCurrentStepChanged: {
        if (currentStep === 2 && !gpuDetected) {
            detectGPU()
        }
    }

    Component.onCompleted: {
        currentStep = 0
    }
}
