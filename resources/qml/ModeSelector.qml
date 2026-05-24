import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Rectangle {
    id: modeSelector
    radius: AppTheme.radiusLg
    color: compact ? "transparent" : AppTheme.bgInput
    border.color: compact ? "transparent" : AppTheme.borderSubtle
    border.width: compact ? 0 : 1

    property bool compact: false
    property string currentMode: "ask"  // "ask", "learn", "turbo"

    signal modeChanged(string mode)

    // ── Layout ─────────────────────────────────────────────────────
    RowLayout {
        anchors {
            fill: parent
            margins: 3
        }
        spacing: 2

        ModeButton {
            id: askBtn
            text: "\u2728 " + qsTr("Ask")
            mode: "ask"
            iconColor: AppTheme.modeAsk
            tooltipText: qsTr("AI suggests corrections, you approve or refine")
        }

        ModeButton {
            id: learnBtn
            text: "\ud83e\udde0 " + qsTr("Smart-Learn")
            mode: "learn"
            iconColor: AppTheme.modeLearn
            tooltipText: qsTr("AI learns your preferences over time")
        }

        ModeButton {
            id: turboBtn
            text: "\u26a1 " + qsTr("Turbo")
            mode: "turbo"
            iconColor: AppTheme.modeTurbo
            tooltipText: qsTr("Instant one-click automatic correction")
        }
    }

    // ── Mode Button Component ──────────────────────────────────────
    component ModeButton: Rectangle {
        Layout.fillWidth: true
        Layout.fillHeight: true
        Layout.minimumWidth: compact ? 36 : 80
        radius: AppTheme.radiusMd

        property string text: ""
        property string mode: ""
        property color iconColor: AppTheme.accentPrimary
        property string tooltipText: ""
        property bool isSelected: modeSelector.currentMode === mode

        color: isSelected
               ? AppTheme.alpha(iconColor, 0.18)
               : btnMouse.containsMouse
                 ? AppTheme.alpha(AppTheme.textPrimary, 0.06)
                 : "transparent"

        Behavior on color { ColorAnimation { duration: AppTheme.animFast } }

        // Selected border glow
        Rectangle {
            anchors.fill: parent
            radius: parent.radius
            color: "transparent"
            border.color: isSelected ? iconColor : "transparent"
            border.width: 1.5
            opacity: isSelected ? 0.6 : 0
            Behavior on opacity { NumberAnimation { duration: AppTheme.animFast } }
        }

        Label {
            anchors.centerIn: parent
            text: compact
                  ? (modeSelector.currentMode === "ask" ? "\u2728"
                     : modeSelector.currentMode === "learn" ? "\ud83e\udde0"
                     : "\u26a1")
                  : parent.text
            font.pixelSize: compact ? AppTheme.fontSizeMd : AppTheme.fontSizeSm
            font.family: AppTheme.fontFamily
            font.bold: isSelected
            color: isSelected ? iconColor : AppTheme.textSecondary
            elide: Text.ElideRight

            Behavior on color { ColorAnimation { duration: AppTheme.animFast } }
        }

        MouseArea {
            id: btnMouse
            anchors.fill: parent
            hoverEnabled: true
            cursorShape: Qt.PointingHandCursor
            onClicked: {
                modeSelector.currentMode = mode
                modeSelector.modeChanged(mode)
            }

            ToolTip {
                visible: btnMouse.containsMouse
                delay: 500
                text: parent.tooltipText
                contentItem: Label {
                    text: parent.tooltipText
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
