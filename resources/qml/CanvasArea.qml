import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Rectangle {
    id: canvasArea
    color: "#0a0e14"
    clip: true

    // ── Properties ─────────────────────────────────────────────────
    property real imageScale: 1.0
    property real minScale: 0.1
    property real maxScale: 10.0
    property real panX: 0
    property real panY: 0
    property bool beforeAfterVisible: false
    property real sliderPosition: 0.5
    property string imageSource: ""
    property bool hasImage: false
    property string statusText: qsTr("Drop an image or video here, or use File → Open")

    // ── Functions ──────────────────────────────────────────────────
    function toggleBeforeAfter() { beforeAfterVisible = !beforeAfterVisible }
    function fitToWindow() { imageScale = 1.0; panX = 0; panY = 0 }
    function zoomIn() { imageScale = Math.min(maxScale, imageScale * 1.25) }
    function zoomOut() { imageScale = Math.max(minScale, imageScale / 1.25) }

    // ── Empty State ────────────────────────────────────────────────
    Item {
        anchors.centerIn: parent
        visible: !canvasArea.hasImage

        ColumnLayout {
            anchors.centerIn: parent
            spacing: AppTheme.spacingXl

            Rectangle {
                Layout.preferredWidth: 80
                Layout.preferredHeight: 80
                Layout.alignment: Qt.AlignHCenter
                radius: AppTheme.radiusXl
                color: AppTheme.bgCard
                border.color: AppTheme.borderDefault
                border.width: 1

                Label {
                    anchors.centerIn: parent
                    text: "\ud83d\udcf7"
                    font.pixelSize: 32
                }
            }

            Label {
                text: canvasArea.statusText
                color: AppTheme.textSecondary
                font.pixelSize: AppTheme.fontSizeMd
                font.family: AppTheme.fontFamily
                horizontalAlignment: Text.AlignHCenter
                wrapMode: Text.WordWrap
                Layout.maximumWidth: 320
                Layout.alignment: Qt.AlignHCenter
            }

            Button {
                Layout.alignment: Qt.AlignHCenter
                text: qsTr("Open Image/Video…")
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
                    horizontalAlignment: Text.AlignHCenter
                    verticalAlignment: Text.AlignVCenter
                }

                onClicked: { /* open file dialog via C++ backend */ }
            }
        }
    }

    // ── Image Display Container ────────────────────────────────────
    Item {
        id: imageContainer
        anchors.fill: parent
        visible: canvasArea.hasImage

        // Transform for zoom/pan
        Item {
            id: transformGroup
            anchors.centerIn: parent
            width: imageDisplay.implicitWidth * canvasArea.imageScale
            height: imageDisplay.implicitHeight * canvasArea.imageScale
            x: canvasArea.panX
            y: canvasArea.panY

            // Original image (behind)
            Image {
                id: originalImage
                source: canvasArea.imageSource
                width: parent.width
                height: parent.height
                fillMode: Image.PreserveAspectFit
                visible: canvasArea.beforeAfterVisible
                opacity: 0.8
            }

            // Corrected image (in front, clipped by slider)
            Item {
                id: correctedImageContainer
                anchors.fill: parent
                clip: true

                // Before-after clip
                Rectangle {
                    id: afterClip
                    color: "transparent"
                    anchors {
                        left: parent.left
                        top: parent.top
                        bottom: parent.bottom
                    }
                    width: canvasArea.beforeAfterVisible
                           ? parent.width * canvasArea.sliderPosition
                           : parent.width
                    clip: true

                    Image {
                        id: imageDisplay
                        source: canvasArea.imageSource
                        width: parent.parent.width
                        height: parent.parent.height
                        fillMode: Image.PreserveAspectFit
                        // C++ backend would apply color correction as an effect here
                    }
                }
            }

            // Before/After label overlay
            Rectangle {
                visible: canvasArea.beforeAfterVisible
                anchors {
                    left: parent.left
                    top: parent.top
                    leftMargin: 8
                    topMargin: 8
                }
                radius: AppTheme.radiusSm
                color: AppTheme.overlay
                width: beforeLabel.width + 16
                height: beforeLabel.height + 8

                Label {
                    id: beforeLabel
                    anchors.centerIn: parent
                    text: qsTr("Original")
                    color: AppTheme.textPrimary
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    font.bold: true
                }
            }

            Rectangle {
                visible: canvasArea.beforeAfterVisible
                anchors {
                    right: parent.right
                    top: parent.top
                    rightMargin: 8
                    topMargin: 8
                }
                radius: AppTheme.radiusSm
                color: AppTheme.alpha(AppTheme.accentPrimary, 0.8)
                width: afterLabel.width + 16
                height: afterLabel.height + 8

                Label {
                    id: afterLabel
                    anchors.centerIn: parent
                    text: qsTr("Corrected")
                    color: AppTheme.textPrimary
                    font.pixelSize: AppTheme.fontSizeSm
                    font.family: AppTheme.fontFamily
                    font.bold: true
                }
            }
        }
    }

    // ── Before/After Split Slider ──────────────────────────────────
    Item {
        id: splitSlider
        anchors.fill: parent
        visible: canvasArea.beforeAfterVisible && canvasArea.hasImage

        // Slider handle
        Rectangle {
            id: sliderHandle
            x: parent.width * canvasArea.sliderPosition - width / 2
            y: (parent.height - height) / 2
            width: 4
            height: Math.min(parent.height, 200)
            radius: 2
            color: AppTheme.accentPrimary

            Rectangle {
                anchors.centerIn: parent
                width: 32
                height: 32
                radius: 16
                color: AppTheme.accentPrimary
                border.color: AppTheme.textPrimary
                border.width: 2

                Label {
                    anchors.centerIn: parent
                    text: "\u2194"
                    font.pixelSize: 14
                    color: AppTheme.textPrimary
                }
            }

            // Drag handle
            MouseArea {
                anchors.fill: parent
                anchors.margins: -20
                cursorShape: Qt.SplitHCursor
                drag.target: sliderHandle
                drag.axis: Drag.XAxis
                drag.minimumX: 0
                drag.maximumX: parent.parent.width

                onPositionChanged: {
                    if (drag.active) {
                        canvasArea.sliderPosition = Math.max(0, Math.min(1,
                            (sliderHandle.x + sliderHandle.width / 2) / parent.width))
                    }
                }
            }
        }
    }

    // ── Pinch Zoom / Mouse Wheel ───────────────────────────────────
    PinchArea {
        anchors.fill: parent
        enabled: canvasArea.hasImage

        onPinchUpdated: function(pinch) {
            canvasArea.imageScale *= pinch.scale
            canvasArea.imageScale = Math.max(canvasArea.minScale,
                                   Math.min(canvasArea.maxScale, canvasArea.imageScale))
        }

        MouseArea {
            anchors.fill: parent
            acceptedButtons: Qt.LeftButton | Qt.MiddleButton

            onWheel: function(wheel) {
                if (wheel.modifiers & Qt.ControlModifier || true) {
                    var factor = wheel.angleDelta.y > 0 ? 1.1 : 0.9
                    canvasArea.imageScale *= factor
                    canvasArea.imageScale = Math.max(canvasArea.minScale,
                                           Math.min(canvasArea.maxScale, canvasArea.imageScale))
                }
            }

            // Pan with middle mouse button
            property real lastX: 0
            property real lastY: 0

            onPressed: function(mouse) {
                if (mouse.button === Qt.MiddleButton) {
                    lastX = mouse.x
                    lastY = mouse.y
                    cursorShape = Qt.ClosedHandCursor
                }
            }

            onPositionChanged: function(mouse) {
                if (pressed && mouse.button === Qt.MiddleButton) {
                    canvasArea.panX += mouse.x - lastX
                    canvasArea.panY += mouse.y - lastY
                    lastX = mouse.x
                    lastY = mouse.y
                }
            }

            onReleased: function(mouse) {
                if (mouse.button === Qt.MiddleButton) {
                    cursorShape = Qt.ArrowCursor
                }
            }
        }
    }

    // ── Checkerboard background for transparent images ─────────────
    Rectangle {
        anchors.fill: parent
        visible: false  // enabled when image has alpha
        color: "transparent"
    }

    // ── Border ─────────────────────────────────────────────────────
    Rectangle {
        anchors.fill: parent
        color: "transparent"
        border.color: AppTheme.borderSubtle
        border.width: 1
    }
}
