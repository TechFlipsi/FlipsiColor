pragma Singleton

import QtQuick
import QtQuick.Controls.Material

QtObject {
    id: theme

    // ── Color Palette ──────────────────────────────────────────────
    // Background layers
    readonly property color bgPrimary:       "#0d1117"
    readonly property color bgSecondary:     "#161b22"
    readonly property color bgTertiary:      "#1a1a2e"
    readonly property color bgCard:          "#1e2433"
    readonly property color bgPanel:         "#141820"
    readonly property color bgInput:         "#21262d"

    // Borders & dividers
    readonly property color borderSubtle:    "#2d2d44"
    readonly property color borderDefault:   "#30363d"
    readonly property color borderEmphasis:  "#484f64"

    // Accent colors
    readonly property color accentPrimary:   "#e94560"   // coral
    readonly property color accentSecondary: "#ff6b81"
    readonly property color accentMuted:     "rgba(233,69,96,0.15)"
    readonly property color accentGlow:      "rgba(233,69,96,0.35)"

    // Text colors
    readonly property color textPrimary:     "#e6edf3"
    readonly property color textSecondary:   "#8b949e"
    readonly property color textMuted:       "#484f58"
    readonly property color textInverse:     "#0d1117"

    // Semantic colors
    readonly property color success:         "#3fb950"
    readonly property color warning:         "#d29922"
    readonly property color error:           "#f85149"
    readonly property color info:            "#58a6ff"

    // Mode colors
    readonly property color modeAsk:         "#58a6ff"   // blue
    readonly property color modeLearn:       "#bc8cff"   // purple
    readonly property color modeTurbo:       "#e94560"   // coral

    // Intensity colors
    readonly property color intensityLow:    "#3fb950"
    readonly property color intensityMid:    "#d29922"
    readonly property color intensityHigh:   "#f85149"

    // Histogram colors
    readonly property color histRed:         "rgba(255,80,80,0.7)"
    readonly property color histGreen:       "rgba(80,255,80,0.7)"
    readonly property color histBlue:        "rgba(80,80,255,0.7)"
    readonly property color histWhite:       "rgba(220,220,220,0.6)"

    // Overlay
    readonly property color overlay:         "rgba(0,0,0,0.55)"
    readonly property color shadow:          "rgba(0,0,0,0.4)"

    // ── Typography ─────────────────────────────────────────────────
    readonly property string fontFamily:     "Inter, Segoe UI, Roboto, sans-serif"
    readonly property string fontMono:       "JetBrains Mono, Fira Code, Consolas, monospace"

    readonly property int fontSizeXs:        10
    readonly property int fontSizeSm:        11
    readonly property int fontSizeBase:      13
    readonly property int fontSizeMd:        14
    readonly property int fontSizeLg:        16
    readonly property int fontSizeXl:        20
    readonly property int fontSizeXxl:       26
    readonly property int fontSizeTitle:     32

    readonly property real fontWeightNormal:  Font.Normal
    readonly property real fontWeightMedium:  Font.Medium
    readonly property real fontWeightBold:    Font.Bold

    // ── Spacing & Sizing ───────────────────────────────────────────
    readonly property int spacingXs:         2
    readonly property int spacingSm:         4
    readonly property int spacingMd:         8
    readonly property int spacingLg:         12
    readonly property int spacingXl:         16
    readonly property int spacingXxl:        24

    readonly property int radiusSm:          4
    readonly property int radiusMd:          6
    readonly property int radiusLg:          8
    readonly property int radiusXl:          12
    readonly property int radiusFull:        999

    // Panel widths
    readonly property int sidebarWidth:      48
    readonly property int adjustPanelWidth:  320
    readonly property int adjustPanelMin:    280
    readonly property int adjustPanelMax:    420

    // Icon sizes
    readonly property int iconSm:            14
    readonly property int iconMd:            18
    readonly property int iconLg:            24

    // ── Timing ─────────────────────────────────────────────────────
    readonly property int animFast:          150
    readonly property int animBase:          250
    readonly property int animSlow:          400

    // ── Helper Functions ───────────────────────────────────────────
    function alpha(color, opacity) {
        // Returns color with adjusted alpha, assuming hex #rrggbb input
        var r = parseInt(color.substr(1,2), 16)
        var g = parseInt(color.substr(3,2), 16)
        var b = parseInt(color.substr(5,2), 16)
        return Qt.rgba(r/255, g/255, b/255, opacity)
    }

    function lerpColor(a, b, t) {
        var ar = parseInt(a.substr(1,2), 16)
        var ag = parseInt(a.substr(3,2), 16)
        var ab = parseInt(a.substr(5,2), 16)
        var br = parseInt(b.substr(1,2), 16)
        var bg = parseInt(b.substr(3,2), 16)
        var bb = parseInt(b.substr(5,2), 16)
        var r = Math.round(ar + (br - ar) * t)
        var g = Math.round(ag + (bg - ag) * t)
        var bl = Math.round(ab + (bb - ab) * t)
        return "#" + ((1 << 24) + (r << 16) + (g << 8) + bl).toString(16).slice(1)
    }
}
