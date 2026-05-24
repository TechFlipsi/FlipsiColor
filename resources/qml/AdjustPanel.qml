import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Rectangle {
    id: adjustPanel
    color: AppTheme.bgPanel

    property bool histogramVisible: true
    property alias modeSelector: modeSelector

    // ── Functions ──────────────────────────────────────────────────
    function resetAll() {
        exposureSlider.value = 0
        contrastSlider.value = 0
        highlightsSlider.value = 0
        shadowsSlider.value = 0
        whitesSlider.value = 0
        blacksSlider.value = 0
        saturationSlider.value = 0
        vibranceSlider.value = 0
        temperatureSlider.value = 6500
        tintSlider.value = 0
        sharpnessSlider.value = 0
        noiseReductionSlider.value = 0
    }

    // ── Scrollable Content ─────────────────────────────────────────
    ScrollView {
        anchors.fill: parent
        clip: true
        ScrollBar.vertical.policy: ScrollBar.AlwaysOn
        ScrollBar.horizontal.policy: ScrollBar.AlwaysOff

        background: Rectangle { color: AppTheme.bgPanel }

        ColumnLayout {
            width: parent.width - AppTheme.spacingMd
            spacing: 0

            // ── Panel Header ─────────────────────────────────────────
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 48
                color: AppTheme.bgSecondary

                RowLayout {
                    anchors {
                        fill: parent
                        leftMargin: AppTheme.spacingLg
                        rightMargin: AppTheme.spacingLg
                    }

                    Label {
                        text: qsTr("Adjustments")
                        font.pixelSize: AppTheme.fontSizeLg
                        font.bold: true
                        font.family: AppTheme.fontFamily
                        color: AppTheme.textPrimary
                    }

                    Item { Layout.fillWidth: true }

                    IconButton {
                        iconText: "\u21ba"
                        tooltip: qsTr("Reset All")
                        onClicked: adjustPanel.resetAll()
                    }
                }
            }

            // ── Mode Selector in Panel ───────────────────────────────
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 56
                color: "transparent"
                Layout.margins: AppTheme.spacingMd

                ModeSelector {
                    id: modeSelector
                    anchors.centerIn: parent
                    compact: false
                }
            }

            Divider {}

            // ── Histogram ────────────────────────────────────────────
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: histogramVisible ? 100 : 0
                Layout.margins: AppTheme.spacingMd
                color: "transparent"
                clip: true
                visible: histogramVisible

                Histogram {
                    id: histogram
                    anchors.fill: parent
                }
            }

            Divider {}

            // ── Intensity Selector ───────────────────────────────────
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 48
                Layout.margins: AppTheme.spacingMd
                color: "transparent"

                IntensitySelector {
                    id: intensitySelector
                    anchors.fill: parent
                }
            }

            Divider {}

            // ── Section: Exposure ────────────────────────────────────
            SectionHeader { text: qsTr("Exposure") }

            AdjustSlider {
                id: exposureSlider
                label: qsTr("Exposure")
                minValue: -5
                maxValue: 5
                defaultValue: 0
                stepSize: 0.01
                suffix: " EV"
                decimals: 2
            }

            AdjustSlider {
                id: contrastSlider
                label: qsTr("Contrast")
                minValue: -100
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: "%"
                decimals: 0
            }

            AdjustSlider {
                id: highlightsSlider
                label: qsTr("Highlights")
                minValue: -100
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: "%"
                decimals: 0
            }

            AdjustSlider {
                id: shadowsSlider
                label: qsTr("Shadows")
                minValue: -100
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: "%"
                decimals: 0
            }

            AdjustSlider {
                id: whitesSlider
                label: qsTr("Whites")
                minValue: -100
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: "%"
                decimals: 0
            }

            AdjustSlider {
                id: blacksSlider
                label: qsTr("Blacks")
                minValue: -100
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: "%"
                decimals: 0
            }

            // ── Section: Color ───────────────────────────────────────
            Divider {}
            SectionHeader { text: qsTr("Color") }

            AdjustSlider {
                id: saturationSlider
                label: qsTr("Saturation")
                minValue: -100
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: "%"
                decimals: 0
            }

            AdjustSlider {
                id: vibranceSlider
                label: qsTr("Vibrance")
                minValue: -100
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: "%"
                decimals: 0
            }

            AdjustSlider {
                id: temperatureSlider
                label: qsTr("Temperature")
                minValue: 2000
                maxValue: 50000
                defaultValue: 6500
                stepSize: 50
                suffix: "K"
                decimals: 0
            }

            AdjustSlider {
                id: tintSlider
                label: qsTr("Tint")
                minValue: -100
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: ""
                decimals: 0
            }

            // ── Section: Detail ──────────────────────────────────────
            Divider {}
            SectionHeader { text: qsTr("Detail") }

            AdjustSlider {
                id: sharpnessSlider
                label: qsTr("Sharpness")
                minValue: 0
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: ""
                decimals: 0
            }

            AdjustSlider {
                id: noiseReductionSlider
                label: qsTr("Noise Reduction")
                minValue: 0
                maxValue: 100
                defaultValue: 0
                stepSize: 1
                suffix: ""
                decimals: 0
            }

            // ── Learning Card (Ask mode) ─────────────────────────────
            Divider {}

            LearningCard {
                id: learningCard
                Layout.fillWidth: true
                Layout.margins: AppTheme.spacingMd
                Layout.preferredHeight: 120
                visible: modeSelector.currentMode === "ask"
            }

            // Bottom spacer
            Item { Layout.preferredHeight: AppTheme.spacingXxl }
        }
    }

    // ── Sub-Components ─────────────────────────────────────────────
    component Divider: Rectangle {
        Layout.fillWidth: true
        Layout.preferredHeight: 1
        Layout.leftMargin: AppTheme.spacingMd
        Layout.rightMargin: AppTheme.spacingMd
        color: AppTheme.borderSubtle
    }

    component SectionHeader: Rectangle {
        Layout.fillWidth: true
        Layout.preferredHeight: 32
        color: "transparent"
        Layout.leftMargin: AppTheme.spacingLg
        Layout.rightMargin: AppTheme.spacingLg

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
            textFormat: Text.PlainText
        }
    }

    component AdjustSlider: Item {
        Layout.fillWidth: true
        Layout.preferredHeight: 48
        Layout.leftMargin: AppTheme.spacingLg
        Layout.rightMargin: AppTheme.spacingLg

        property string label: ""
        property real minValue: 0
        property real maxValue: 100
        property real defaultValue: 0
        property real stepSize: 1
        property string suffix: ""
        property int decimals: 0
        property real value: defaultValue

        RowLayout {
            anchors.fill: parent
            anchors.margins: 0
            spacing: AppTheme.spacingMd

            Label {
                Layout.preferredWidth: 90
                text: parent.label
                font.pixelSize: AppTheme.fontSizeSm
                font.family: AppTheme.fontFamily
                color: AppTheme.textSecondary
                elide: Text.ElideRight
            }

            Slider {
                id: slider
                Layout.fillWidth: true
                from: parent.minValue
                to: parent.maxValue
                value: parent.defaultValue
                stepSize: parent.stepSize
                live: true

                background: Rectangle {
                    x: slider.leftPadding
                    y: slider.topPadding + slider.availableHeight / 2 - 1
                    implicitWidth: 100
                    implicitHeight: 3
                    width: slider.availableWidth
                    height: 3
                    radius: 1.5
                    color: AppTheme.borderDefault

                    Rectangle {
                        width: slider.visualPosition * parent.width
                        height: parent.height
                        radius: 1.5
                        color: AppTheme.accentPrimary
                    }
                }

                handle: Rectangle {
                    x: slider.leftPadding + slider.visualPosition * (slider.availableWidth - width)
                    y: slider.topPadding + slider.availableHeight / 2 - height / 2
                    implicitWidth: 14
                    implicitHeight: 14
                    radius: 7
                    color: slider.pressed ? AppTheme.accentSecondary : AppTheme.accentPrimary
                    border.color: AppTheme.textPrimary
                    border.width: 1
                }

                onValueChanged: function(v) { parent.value = v }
            }

            Label {
                Layout.preferredWidth: 54
                text: Number(parent.value).toFixed(parent.decimals) + parent.suffix
                font.pixelSize: AppTheme.fontSizeSm
                font.family: AppTheme.fontMono
                color: AppTheme.textPrimary
                horizontalAlignment: Text.AlignRight
            }

            IconButton {
                visible: Math.abs(parent.value - parent.defaultValue) > 0.001
                iconText: "\u21ba"
                tooltip: qsTr("Reset to default")
                onClicked: {
                    parent.value = parent.defaultValue
                    slider.value = parent.defaultValue
                }
            }
        }
    }

    component IconButton: Rectangle {
        width: 22
        height: 22
        radius: AppTheme.radiusSm
        color: mouseArea.containsMouse ? AppTheme.alpha(AppTheme.textPrimary, 0.08) : "transparent"

        property string iconText: ""
        property string tooltip: ""
        signal clicked()

        Label {
            anchors.centerIn: parent
            text: parent.iconText
            font.pixelSize: AppTheme.fontSizeBase
            color: AppTheme.textSecondary
        }

        MouseArea {
            id: mouseArea
            anchors.fill: parent
            hoverEnabled: true
            cursorShape: Qt.PointingHandCursor
            onClicked: parent.clicked()

            ToolTip {
                visible: mouseArea.containsMouse
                delay: 400
                text: parent.tooltip
            }
        }
    }
}
