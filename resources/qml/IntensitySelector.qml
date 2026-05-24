import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Rectangle {
    id: intensitySelector
    radius: AppTheme.radiusLg
    color: AppTheme.bgInput
    border.color: AppTheme.borderSubtle
    border.width: 1

    property string currentIntensity: "medium"  // "low", "medium", "high"
    signal intensityChanged(string intensity)

    // ── Layout ─────────────────────────────────────────────────────
    RowLayout {
        anchors {
            fill: parent
            margins: 2
        }
        spacing: 2

        IntensityButton {
            id: lowBtn
            label: qsTr("Leicht")
            subtitle: qsTr("Light")
            intensity: "low"
            color: AppTheme.intensityLow
            icon: "\ud83c\udf3f"
        }

        IntensityButton {
            id: mediumBtn
            label: qsTr("Mittel")
            subtitle: qsTr("Medium")
            intensity: "medium"
            color: AppTheme.intensityMid
            icon: "\u26fd"
        }

        IntensityButton {
            id: highBtn
            label: qsTr("Stark")
            subtitle: qsTr("Strong")
            intensity: "high"
            color: AppTheme.intensityHigh
            icon: "\ud83d\udd25"
        }
    }

    // ── Intensity Button Component ─────────────────────────────────
    component IntensityButton: Rectangle {
        Layout.fillWidth: true
        Layout.fillHeight: true
        radius: AppTheme.radiusMd

        property string label: ""
        property string subtitle: ""
        property string intensity: ""
        property color color: AppTheme.accentPrimary
        property string icon: ""
        property bool isSelected: intensitySelector.currentIntensity === intensity

        // Background
        color: isSelected
               ? AppTheme.alpha(color, 0.18)
               : btnMouseArea.containsMouse
                 ? AppTheme.alpha(AppTheme.textPrimary, 0.06)
                 : "transparent"

        Behavior on color { ColorAnimation { duration: AppTheme.animFast } }

        // Active border
        Rectangle {
            anchors.fill: parent
            radius: parent.radius
            color: "transparent"
            border.color: isSelected ? parent.color : "transparent"
            border.width: 1.5
            opacity: isSelected ? 0.5 : 0
            Behavior on opacity { NumberAnimation { duration: AppTheme.animFast } }
        }

        // Intensity dot indicator
        RowLayout {
            anchors {
                horizontalCenter: parent.horizontalCenter
                top: parent.top
                topMargin: 6
            }
            spacing: 3

            Rectangle {
                Layout.preferredWidth: 5
                Layout.preferredHeight: 5
                radius: 2.5
                color: isSelected ? parent.parent.color : AppTheme.textMuted
                opacity: isSelected ? 1.0 : 0.4
            }
            Rectangle {
                Layout.preferredWidth: 5
                Layout.preferredHeight: 5
                radius: 2.5
                color: isSelected || intensity === "medium" || intensity === "high"
                       ? parent.parent.color : AppTheme.textMuted
                opacity: isSelected ? 1.0 : (intensity === "medium" || intensity === "high") ? 0.7 : 0.3
            }
            Rectangle {
                Layout.preferredWidth: 5
                Layout.preferredHeight: 5
                radius: 2.5
                color: isSelected || intensity === "high"
                       ? parent.parent.color : AppTheme.textMuted
                opacity: isSelected ? 1.0 : intensity === "high" ? 0.7 : 0.2
            }
        }

        // Label
        ColumnLayout {
            anchors {
                centerIn: parent
                verticalCenterOffset: 2
            }
            spacing: -1

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: parent.parent.label
                font.pixelSize: AppTheme.fontSizeSm
                font.family: AppTheme.fontFamily
                font.bold: isSelected
                color: isSelected ? parent.parent.color : AppTheme.textSecondary
                Behavior on color { ColorAnimation { duration: AppTheme.animFast } }
            }

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: parent.parent.subtitle
                font.pixelSize: AppTheme.fontSizeXs
                font.family: AppTheme.fontFamily
                color: AppTheme.textMuted
            }
        }

        // Mouse area
        MouseArea {
            id: btnMouseArea
            anchors.fill: parent
            hoverEnabled: true
            cursorShape: Qt.PointingHandCursor
            onClicked: {
                intensitySelector.currentIntensity = intensity
                intensitySelector.intensityChanged(intensity)
            }
        }
    }
}
