import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Rectangle {
    id: sidebar
    color: AppTheme.bgSecondary
    width: AppTheme.sidebarWidth

    // ── Tool Container ─────────────────────────────────────────────
    ColumnLayout {
        anchors {
            fill: parent
            topMargin: AppTheme.spacingMd
            bottomMargin: AppTheme.spacingMd
        }
        spacing: AppTheme.spacingSm

        // Mode icons (vertical icon bar)
        SidebarButton {
            iconText: "\u2728"          // ✨ Ask
            tooltip: qsTr("Ask Mode — AI-assisted color correction with feedback")
            activeColor: AppTheme.modeAsk
            isActive: modeSelector.currentMode === "ask"
            onClicked: modeSelector.currentMode = "ask"
        }

        SidebarButton {
            iconText: "\ud83e\udde0"    // 🧠 Smart-Learn
            tooltip: qsTr("Smart-Learn — AI learns your preferences")
            activeColor: AppTheme.modeLearn
            isActive: modeSelector.currentMode === "learn"
            onClicked: modeSelector.currentMode = "learn"
        }

        SidebarButton {
            iconText: "\u26a1"          // ⚡ Turbo
            tooltip: qsTr("Turbo Mode — instant automatic correction")
            activeColor: AppTheme.modeTurbo
            isActive: modeSelector.currentMode === "turbo"
            onClicked: modeSelector.currentMode = "turbo"
        }

        // Separator
        Rectangle {
            Layout.preferredWidth: 28
            Layout.preferredHeight: 1
            Layout.alignment: Qt.AlignHCenter
            color: AppTheme.borderSubtle
        }

        // Tool icons
        SidebarButton {
            iconText: "\u2194"          // ↔ Before/After
            tooltip: qsTr("Toggle Before/After (Space)")
            activeColor: AppTheme.accentPrimary
            isActive: canvasArea.beforeAfterVisible
            onClicked: canvasArea.toggleBeforeAfter()
        }

        SidebarButton {
            iconText: "\ud83d\udd0d"    // 🔍 Zoom
            tooltip: qsTr("Fit to Window (Ctrl+0)")
            activeColor: AppTheme.textPrimary
            isActive: false
            onClicked: canvasArea.fitToWindow()
        }

        SidebarButton {
            iconText: "\ud83d\udccf"    // 📏 Histogram
            tooltip: qsTr("Toggle Histogram")
            activeColor: AppTheme.accentPrimary
            isActive: adjustPanel.histogramVisible
            onClicked: adjustPanel.histogramVisible = !adjustPanel.histogramVisible
        }

        // Spacer
        Item { Layout.fillHeight: true }

        // Bottom: settings
        SidebarButton {
            iconText: "\u2699"          // ⚙ Settings
            tooltip: qsTr("Settings (Ctrl+,)")
            activeColor: AppTheme.textSecondary
            isActive: false
            onClicked: settingsPanel.open()
        }
    }

    // ── Separator line on right edge ───────────────────────────────
    Rectangle {
        anchors {
            right: parent.right
            top: parent.top
            bottom: parent.bottom
        }
        width: 1
        color: "transparent"  // border already handled in Main.qml
    }

    // ── Component for Sidebar Icons ────────────────────────────────
    component SidebarButton: Item {
        id: btn
        width: sidebar.width - AppTheme.spacingSm
        height: 40

        property string iconText: ""
        property string tooltip: ""
        property color activeColor: AppTheme.accentPrimary
        property bool isActive: false
        signal clicked()

        Rectangle {
            anchors {
                fill: parent
                margins: 3
                leftMargin: 3
                rightMargin: 0
            }
            radius: AppTheme.radiusMd
            color: {
                if (btn.isActive)
                    return AppTheme.alpha(btn.activeColor, 0.15)
                if (mouseArea.containsMouse)
                    return AppTheme.alpha(AppTheme.textPrimary, 0.08)
                return "transparent"
            }
        }

        // Active indicator bar
        Rectangle {
            anchors {
                left: parent.left
                top: parent.top
                bottom: parent.bottom
                topMargin: 8
                bottomMargin: 8
            }
            width: 3
            radius: 2
            color: btn.isActive ? btn.activeColor : "transparent"
            visible: btn.isActive
            Behavior on color { ColorAnimation { duration: AppTheme.animFast } }
        }

        Label {
            anchors.centerIn: parent
            text: btn.iconText
            font.pixelSize: AppTheme.fontSizeLg
            color: btn.isActive ? btn.activeColor : AppTheme.textSecondary
            opacity: btn.isActive ? 1.0 : 0.7
        }

        MouseArea {
            id: mouseArea
            anchors.fill: parent
            hoverEnabled: true
            cursorShape: Qt.PointingHandCursor
            onClicked: btn.clicked()

            ToolTip {
                visible: mouseArea.containsMouse
                delay: 400
                text: btn.tooltip
                contentItem: Label {
                    text: btn.tooltip
                    color: AppTheme.textPrimary
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                }
                background: Rectangle {
                    color: AppTheme.bgCard
                    border.color: AppTheme.borderDefault
                    radius: AppTheme.radiusSm
                }
            }
        }
    }
}
