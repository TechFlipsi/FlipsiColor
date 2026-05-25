pragma Singleton

import QtQuick
import QtQuick.Controls.Material
import QtQuick.Window

QtObject {
    id: theme

    // ── Theme Mode ──────────────────────────────────────────────────
    // 0 = Light, 1 = Dark, 2 = System (follows OS preference)
    property int themeMode: 2
    property bool darkMode: true
    readonly property string activeTheme: darkMode ? "dark" : "light"

    /// @param mode — "light", "dark", "system" (string) OR 0, 1, 2 (int)
    function setThemeMode(mode) {
        if (typeof mode === "number") {
            themeMode = mode
            if (mode === 0) {
                darkMode = false          // light
            } else if (mode === 1) {
                darkMode = true           // dark
            } else {
                // mode === 2 → system
                themeMode = 2
                darkMode = (typeof Screen !== "undefined"
                            && Screen.colorScheme !== undefined)
                    ? (Screen.colorScheme === Qt.ColorScheme.Dark
                       || Screen.colorScheme === Qt.ColorScheme.Unknown)
                    : true
            }
        } else if (typeof mode === "string") {
            var m = mode.toLowerCase()
            if (m === "light") {
                themeMode = 0
                darkMode = false
            } else if (m === "dark") {
                themeMode = 1
                darkMode = true
            } else if (m === "system") {
                themeMode = 2
                darkMode = (typeof Screen !== "undefined"
                            && Screen.colorScheme !== undefined)
                    ? (Screen.colorScheme === Qt.ColorScheme.Dark
                       || Screen.colorScheme === Qt.ColorScheme.Unknown)
                    : true
            }
        }
    }

    // ── Color Palette ───────────────────────────────────────────────
    // Background layers
    readonly property color bgPrimary:       darkMode ? "#0d1117" : "#f5f5f5"
    readonly property color bgSecondary:     darkMode ? "#161b22" : "#ffffff"
    readonly property color bgTertiary:      darkMode ? "#1a1a2e" : "#e8e8e8"
    readonly property color bgCard:          darkMode ? "#1e2433" : "#ffffff"
    readonly property color bgPanel:         darkMode ? "#141820" : "#f0f0f0"
    readonly property color bgInput:         darkMode ? "#21262d" : "#e5e5e5"

    // Borders & dividers
    readonly property color borderSubtle:    darkMode ? "#2d2d44" : "#d0d0d0"
    readonly property color borderDefault:   darkMode ? "#30363d" : "#c0c0c0"
    readonly property color borderEmphasis:  darkMode ? "#484f64" : "#909090"

    // Accent colors
    readonly property color accentPrimary:   darkMode ? "#e94560" : "#d63251"   // coral
    readonly property color accentSecondary: darkMode ? "#ff6b81" : "#e85d75"
    readonly property color accentMuted:     darkMode ? "rgba(233,69,96,0.15)"  : "rgba(214,50,81,0.12)"
    readonly property color accentGlow:      darkMode ? "rgba(233,69,96,0.35)"  : "rgba(214,50,81,0.25)"

    // Text colors
    readonly property color textPrimary:     darkMode ? "#e6edf3" : "#1a1a1a"
    readonly property color textSecondary:   darkMode ? "#8b949e" : "#5c5c5c"
    readonly property color textMuted:       darkMode ? "#484f58" : "#9a9a9a"
    readonly property color textInverse:     darkMode ? "#0d1117" : "#ffffff"

    // Semantic colors (same in both themes)
    readonly property color success:         "#3fb950"
    readonly property color warning:         "#d29922"
    readonly property color error:           "#f85149"
    readonly property color info:            "#58a6ff"

    // Mode colors (same in both themes)
    readonly property color modeAsk:         "#58a6ff"   // blue
    readonly property color modeLearn:       "#bc8cff"   // purple
    readonly property color modeTurbo:       darkMode ? "#e94560" : "#d63251"   // coral

    // Intensity colors
    readonly property color intensityLow:    "#3fb950"
    readonly property color intensityMid:    "#d29922"
    readonly property color intensityHigh:   "#f85149"

    // Histogram colors
    readonly property color histRed:         "rgba(255,80,80,0.7)"
    readonly property color histGreen:       "rgba(80,255,80,0.7)"
    readonly property color histBlue:        "rgba(80,80,255,0.7)"
    readonly property color histWhite:       darkMode ? "rgba(220,220,220,0.6)" : "rgba(60,60,60,0.6)"

    // Overlay
    readonly property color overlay:         darkMode ? "rgba(0,0,0,0.55)" : "rgba(0,0,0,0.15)"
    readonly property color shadow:          darkMode ? "rgba(0,0,0,0.4)"  : "rgba(0,0,0,0.08)"

    // ── Typography ──────────────────────────────────────────────────
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

    // ── Spacing & Sizing (4px Base Grid) ───────────────────────────
    // 4-8-12-16-24-32 — fließende Skala, nicht zu aggressive Sprünge
    readonly property int spacingXs:         4
    readonly property int spacingSm:         8
    readonly property int spacingMd:         12
    readonly property int spacingLg:         16
    readonly property int spacingXl:         24
    readonly property int spacingXxl:        32

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

    // ── Timing ──────────────────────────────────────────────────────
    readonly property int animFast:          150
    readonly property int animBase:          250
    readonly property int animSlow:          400

    // ── Animation Easing ────────────────────────────────────────────
    readonly property real easingStandard:   Easing.OutCubic
    readonly property real easingEmphasized: Easing.OutQuint

    // ── Helper Functions ────────────────────────────────────────────
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

    // ── Theme Variant Helpers ───────────────────────────────────────
    function darkColor(hex) {
        // Returns the dark-theme variant of a color (darkens it)
        var r = parseInt(hex.substr(1,2), 16)
        var g = parseInt(hex.substr(3,2), 16)
        var b = parseInt(hex.substr(5,2), 16)
        // Blend toward #0d1117 (dark bgPrimary) by 35%
        var dr = Math.round(r * 0.65 + 13 * 0.35)
        var dg = Math.round(g * 0.65 + 17 * 0.35)
        var db = Math.round(b * 0.65 + 23 * 0.35)
        return "#" + ((1 << 24) + (dr << 16) + (dg << 8) + db).toString(16).slice(1)
    }

    function lightColor(hex) {
        // Returns the light-theme variant of a color (lightens it)
        var r = parseInt(hex.substr(1,2), 16)
        var g = parseInt(hex.substr(3,2), 16)
        var b = parseInt(hex.substr(5,2), 16)
        // Blend toward #f5f5f5 (light bgPrimary) by 35%
        var lr = Math.round(r * 0.65 + 245 * 0.35)
        var lg = Math.round(g * 0.65 + 245 * 0.35)
        var lb = Math.round(b * 0.65 + 245 * 0.35)
        return "#" + ((1 << 24) + (lr << 16) + (lg << 8) + lb).toString(16).slice(1)
    }
}
