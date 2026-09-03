using UnityEngine;

/// <summary>
/// Centralized Design System & Color Palette for Hadal Descent.
/// 
/// Theme: Atmospheric Deep-Sea Abyssal Sci-Fi / Submarine Instrumentation.
/// 
/// Usage in Scripts:
///   myImage.color = UITheme.SonarCyan;
///   myText.color  = UITheme.TextPrimary;
/// 
/// Usage in Unity Inspector:
///   Copy the HEX values into the color picker hex input.
/// </summary>
public static class UITheme
{
    // =======================================================================
    // 1. ABYSSAL BASE SURFACES (Dark Oceanic Layers)
    // =======================================================================

    /// <summary>#02060D - The deepest trench black/navy. Full-screen backdrop or overlay tint.</summary>
    public static readonly Color DeepBackdrop      = new Color(0.01f, 0.02f, 0.05f, 0.96f); // #02060D
    public const string HexDeepBackdrop            = "#02060D";

    /// <summary>#06121E - Submarine bulkhead navy. Top navigation bars and modal window frames.</summary>
    public static readonly Color HeaderSurface      = new Color(0.02f, 0.07f, 0.12f, 0.98f); // #06121E
    public const string HexHeaderSurface           = "#06121E";

    /// <summary>#0A1C2E - Deep ocean slate. Zone cards, inventory slots, and panel backgrounds.</summary>
    public static readonly Color CardSurface        = new Color(0.04f, 0.11f, 0.18f, 0.98f); // #0A1C2E
    public const string HexCardSurface             = "#0A1C2E";

    /// <summary>#0E273F - Illuminated card / modal box elevation for highlighted cards.</summary>
    public static readonly Color CardSurfaceElevated = new Color(0.05f, 0.15f, 0.25f, 1.00f); // #0E273F
    public const string HexCardSurfaceElevated     = "#0E273F";

    /// <summary>#173452 - Subtle sci-fi border / separator line between elements.</summary>
    public static readonly Color BorderStroke       = new Color(0.09f, 0.20f, 0.32f, 0.75f); // #173452
    public const string HexBorderStroke            = "#173452";

    // =======================================================================
    // 2. BIOLUMINESCENT ACCENTS (Submarine Sonar & Instrumentation)
    // =======================================================================

    /// <summary>#00F2DC - Primary Sonar Cyan. Active scan reticles, glowing borders, active indicators.</summary>
    public static readonly Color SonarCyan          = new Color(0.00f, 0.95f, 0.86f, 1.00f); // #00F2DC
    public const string HexSonarCyan               = "#00F2DC";

    /// <summary>#108B7A - Deep Teal. Primary call-to-action buttons (e.g. DIVE, START, ENTER ZONE).</summary>
    public static readonly Color ButtonPrimary      = new Color(0.06f, 0.55f, 0.48f, 1.00f); // #108B7A
    public const string HexButtonPrimary           = "#108B7A";

    /// <summary>#18BAA4 - Hover state for primary teal buttons.</summary>
    public static readonly Color ButtonPrimaryHover = new Color(0.09f, 0.73f, 0.64f, 1.00f); // #18BAA4
    public const string HexButtonPrimaryHover      = "#18BAA4";

    /// <summary>#26E8A0 - Bioluminescent Seafoam. 100% species completed, unlock checkmarks, positive buffs.</summary>
    public static readonly Color SuccessSeafoam    = new Color(0.15f, 0.91f, 0.63f, 1.00f); // #26E8A0
    public const string HexSuccessSeafoam          = "#26E8A0";

    // =======================================================================
    // 3. STRUCTURAL SLATE & SECONDARY CONTROLS
    // =======================================================================

    /// <summary>#182A3D - Submarine steel slate. Secondary buttons (Back, Cancel, Settings).</summary>
    public static readonly Color ButtonSecondary    = new Color(0.09f, 0.16f, 0.24f, 1.00f); // #182A3D
    public const string HexButtonSecondary         = "#182A3D";

    /// <summary>#253F5C - Hover state for secondary slate buttons.</summary>
    public static readonly Color ButtonSecondaryHover = new Color(0.15f, 0.25f, 0.36f, 1.00f); // #253F5C
    public const string HexButtonSecondaryHover    = "#253F5C";

    /// <summary>#121C26 - Disabled button fill.</summary>
    public static readonly Color DisabledFill       = new Color(0.07f, 0.11f, 0.15f, 0.65f); // #121C26
    public const string HexDisabledFill            = "#121C26";

    /// <summary>#4A6078 - Disabled button or inactive text label.</summary>
    public static readonly Color DisabledText       = new Color(0.29f, 0.38f, 0.47f, 0.70f); // #4A6078
    public const string HexDisabledText            = "#4A6078";

    // =======================================================================
    // 4. ALARMS, PRESSURE LIMITS & WARNINGS
    // =======================================================================

    /// <summary>#FF4A4A - Pressure Threshold Exceeded, critical alerts, lock icons, missing requirements.</summary>
    public static readonly Color DangerAlarm        = new Color(1.00f, 0.29f, 0.29f, 1.00f); // #FF4A4A
    public const string HexDangerAlarm             = "#FF4A4A";

    /// <summary>#8B1A1A - Dark crimson background tint for locked zones or danger warning boxes.</summary>
    public static readonly Color DangerDarkTint     = new Color(0.55f, 0.10f, 0.10f, 0.85f); // #8B1A1A
    public const string HexDangerDarkTint          = "#8B1A1A";

    // =======================================================================
    // 5. RESEARCH CURRENCY & PROGRESS (RDP & CLAMSHELLS)
    // =======================================================================

    /// <summary>#FFC83B - Bioluminescent Gold / Amber. Research Data Points (RDP), currency icons, star ratings.</summary>
    public static readonly Color CurrencyRDP        = new Color(1.00f, 0.78f, 0.23f, 1.00f); // #FFC83B
    public const string HexCurrencyRDP             = "#FFC83B";

    /// <summary>#FF9526 - Warm Orange. Rare findings, warning cautions, engine/speed stat badges.</summary>
    public static readonly Color AccentOrange       = new Color(1.00f, 0.58f, 0.15f, 1.00f); // #FF9526
    public const string HexAccentOrange            = "#FF9526";

    // =======================================================================
    // 6. TYPOGRAPHY & TEXT LABELS
    // =======================================================================

    /// <summary>#FFFFFF - Pure high-contrast white for titles, zone names, and active button text.</summary>
    public static readonly Color TextPrimary        = Color.white;
    public const string HexTextPrimary             = "#FFFFFF";

    /// <summary>#D0F9F5 - Luminous ice cyan for sensor readouts, depth gauges, and active values.</summary>
    public static readonly Color TextReadout        = new Color(0.82f, 0.98f, 0.96f, 1.00f); // #D0F9F5
    public const string HexTextReadout             = "#D0F9F5";

    /// <summary>#BACBDC - Soft oceanic slate for body text, descriptions, and lore entries.</summary>
    public static readonly Color TextSecondary      = new Color(0.73f, 0.80f, 0.86f, 1.00f); // #BACBDC
    public const string HexTextSecondary           = "#BACBDC";

    /// <summary>#68809A - Muted ocean slate for metadata, requirements, and subtle hints.</summary>
    public static readonly Color TextMuted          = new Color(0.41f, 0.50f, 0.60f, 1.00f); // #68809A
    public const string HexTextMuted               = "#68809A";
}
