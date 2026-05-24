import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import "."

Rectangle {
    id: histogram
    color: AppTheme.bgInput
    radius: AppTheme.radiusSm
    border.color: AppTheme.borderSubtle
    border.width: 1

    // ── Properties ─────────────────────────────────────────────────
    property var redData: []
    property var greenData: []
    property var blueData: []
    property var luminanceData: []
    property int channels: 255
    property bool hasData: false

    // ── Canvas ─────────────────────────────────────────────────────
    Canvas {
        id: histogramCanvas
        anchors {
            fill: parent
            margins: 4
        }

        property real maxValue: 1

        onPaint: {
            var ctx = getContext("2d")
            var w = width
            var h = height

            // Reset
            ctx.clearRect(0, 0, w, h)

            if (!histogram.hasData) {
                // Placeholder text
                ctx.fillStyle = AppTheme.textMuted
                ctx.font = AppTheme.fontSizeSm + "px " + AppTheme.fontFamily
                ctx.textAlign = "center"
                ctx.fillText(qsTr("No histogram data"), w / 2, h / 2)
                return
            }

            // Find max for scaling
            var allData = histogram.redData.concat(histogram.greenData, histogram.blueData)
            var max = 1
            for (var i = 0; i < allData.length; i++) {
                if (allData[i] > max) max = allData[i]
            }
            histogramCanvas.maxValue = max

            var barWidth = w / (histogram.channels + 1)
            if (barWidth < 1) barWidth = 1

            // Draw luminance background
            if (histogram.luminanceData.length > 0) {
                var lumGrad = ctx.createLinearGradient(0, 0, 0, h)
                lumGrad.addColorStop(1, AppTheme.alpha(AppTheme.textPrimary, 0.05))
                lumGrad.addColorStop(0, AppTheme.alpha(AppTheme.textPrimary, 0.15))
                ctx.fillStyle = lumGrad

                for (var j = 0; j < histogram.channels; j++) {
                    var lumVal = histogram.luminanceData[j] || 0
                    var lumH = (lumVal / max) * h
                    ctx.fillRect(j * barWidth, h - lumH, barWidth, lumH)
                }
            }

            // Draw RGB channels
            ctx.globalCompositeOperation = "lighter"

            // Red channel
            var redGrad = ctx.createLinearGradient(0, 0, 0, h)
            redGrad.addColorStop(1, "rgba(255,40,40,0.15)")
            redGrad.addColorStop(0, "rgba(255,40,40,0.6)")
            ctx.fillStyle = redGrad
            drawChannel(ctx, histogram.redData, barWidth, w, h, max)

            // Green channel
            var greenGrad = ctx.createLinearGradient(0, 0, 0, h)
            greenGrad.addColorStop(1, "rgba(40,255,40,0.15)")
            greenGrad.addColorStop(0, "rgba(40,255,40,0.6)")
            ctx.fillStyle = greenGrad
            drawChannel(ctx, histogram.greenData, barWidth, w, h, max)

            // Blue channel
            var blueGrad = ctx.createLinearGradient(0, 0, 0, h)
            blueGrad.addColorStop(1, "rgba(40,40,255,0.15)")
            blueGrad.addColorStop(0, "rgba(40,40,255,0.6)")
            ctx.fillStyle = blueGrad
            drawChannel(ctx, histogram.blueData, barWidth, w, h, max)

            // Overlay subtle grid lines
            ctx.globalCompositeOperation = "source-over"
            ctx.strokeStyle = AppTheme.alpha(AppTheme.borderSubtle, 0.5)
            ctx.lineWidth = 0.5
            for (var k = 1; k < 4; k++) {
                var yPos = (h / 4) * k
                ctx.beginPath()
                ctx.moveTo(0, yPos)
                ctx.lineTo(w, yPos)
                ctx.stroke()
            }
        }

        function drawChannel(ctx, data, barWidth, w, h, max) {
            if (data.length === 0) return
            for (var i = 0; i < histogram.channels; i++) {
                var val = data[i] || 0
                var bh = (val / max) * h
                if (bh < 0.5) continue  // skip negligible bars
                ctx.fillRect(i * barWidth, h - bh, barWidth + 0.5, bh)
            }
        }
    }

    // ── Channel Labels ─────────────────────────────────────────────
    RowLayout {
        anchors {
            left: parent.left
            bottom: parent.bottom
            leftMargin: 6
            bottomMargin: 2
        }
        spacing: AppTheme.spacingSm

        ChannelLabel { color: "#ff5050"; text: "R" }
        ChannelLabel { color: "#50ff50"; text: "G" }
        ChannelLabel { color: "#5050ff"; text: "B" }
    }

    component ChannelLabel: RowLayout {
        spacing: 2
        property color color: "white"
        property string text: ""

        Rectangle {
            width: 6; height: 6; radius: 3
            color: parent.color
        }
        Label {
            text: parent.text
            font.pixelSize: AppTheme.fontSizeXs
            font.family: AppTheme.fontMono
            color: AppTheme.textMuted
        }
    }
}
