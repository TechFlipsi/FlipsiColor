import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Rectangle {
    id: learningCard
    radius: AppTheme.radiusLg
    color: AppTheme.bgCard
    border.color: AppTheme.alpha(AppTheme.modeAsk, 0.25)
    border.width: 1

    // ── Header ─────────────────────────────────────────────────────
    ColumnLayout {
        anchors {
            fill: parent
            margins: AppTheme.spacingMd
        }
        spacing: AppTheme.spacingSm

        // Title
        RowLayout {
            Layout.fillWidth: true
            spacing: AppTheme.spacingSm

            Label {
                text: "\ud83e\udde0"
                font.pixelSize: AppTheme.fontSizeMd
                color: AppTheme.modeLearn
            }

            Label {
                text: qsTr("AI Learning")
                font.pixelSize: AppTheme.fontSizeBase
                font.bold: true
                font.family: AppTheme.fontFamily
                color: AppTheme.textPrimary
            }

            Item { Layout.fillWidth: true }

            Label {
                text: qsTr("Feedback")
                font.pixelSize: AppTheme.fontSizeXs
                font.family: AppTheme.fontFamily
                color: AppTheme.textMuted
            }
        }

        // Description
        Label {
            Layout.fillWidth: true
            text: qsTr("Rate the AI's correction to help it learn your style. The more feedback you give, the better the results become.")
            font.pixelSize: AppTheme.fontSizeSm
            font.family: AppTheme.fontFamily
            color: AppTheme.textSecondary
            wrapMode: Text.WordWrap
            lineHeight: 1.4
        }

        // Spacer
        Item { Layout.preferredHeight: AppTheme.spacingSm }

        // Feedback buttons
        RowLayout {
            Layout.fillWidth: true
            spacing: AppTheme.spacingMd

            FeedbackButton {
                id: goodBtn
                text: "\ud83d\udc4d " + qsTr("Good")
                color: AppTheme.success
                tooltipText: qsTr("This correction was good — learn from it")
                onClicked: submitFeedback("positive")
            }

            FeedbackButton {
                id: okBtn
                text: "\ud83d\udc4c " + qsTr("Okay")
                color: AppTheme.warning
                tooltipText: qsTr("Decent but needs refinement")
                onClicked: submitFeedback("neutral")
            }

            FeedbackButton {
                id: badBtn
                text: "\ud83d\udc4e " + qsTr("Bad")
                color: AppTheme.error
                tooltipText: qsTr("This correction was off — teach me better")
                onClicked: submitFeedback("negative")
            }
        }
    }

    // ── Feedback Animation ─────────────────────────────────────────
    Label {
        id: thankYouLabel
        anchors.centerIn: parent
        text: qsTr("Thank you! \u2764\ufe0f")
        font.pixelSize: AppTheme.fontSizeLg
        font.bold: true
        font.family: AppTheme.fontFamily
        color: AppTheme.success
        opacity: 0
        scale: 0.5

        Behavior on opacity { NumberAnimation { duration: AppTheme.animSlow } }
        Behavior on scale { NumberAnimation { duration: AppTheme.animSlow } }
    }

    // ── Functions ──────────────────────────────────────────────────
    function submitFeedback(rating) {
        // Send feedback to the C++ backend
        // backend.submitFeedback(rating)

        // Show thank-you animation
        thankYouLabel.opacity = 1
        thankYouLabel.scale = 1
        resetTimer.restart()
    }

    Timer {
        id: resetTimer
        interval: 2000
        onTriggered: {
            thankYouLabel.opacity = 0
            thankYouLabel.scale = 0.5
        }
    }

    // ── Feedback Button Component ──────────────────────────────────
    component FeedbackButton: Rectangle {
        Layout.fillWidth: true
        Layout.preferredHeight: 30
        radius: AppTheme.radiusMd

        property string text: ""
        property color color: AppTheme.accentPrimary
        property string tooltipText: ""
        signal clicked()

        color: isHovered
               ? AppTheme.alpha(color, 0.2)
               : AppTheme.alpha(color, 0.08)

        property bool isHovered: false
        Behavior on color { ColorAnimation { duration: AppTheme.animFast } }

        border.color: isHovered ? color : "transparent"
        border.width: 1

        Label {
            anchors.centerIn: parent
            text: parent.text
            font.pixelSize: AppTheme.fontSizeSm
            font.family: AppTheme.fontFamily
            color: isHovered ? parent.parent.color : AppTheme.textSecondary
        }

        MouseArea {
            anchors.fill: parent
            hoverEnabled: true
            cursorShape: Qt.PointingHandCursor
            onEntered: parent.isHovered = true
            onExited: parent.isHovered = false
            onClicked: parent.clicked()

            ToolTip {
                visible: parent.containsMouse
                delay: 400
                text: parent.parent.tooltipText
            }
        }
    }
}
