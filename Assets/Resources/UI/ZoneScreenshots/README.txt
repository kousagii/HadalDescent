======================================================================
HADAL DESCENT - ZONE SELECTION SCREENSHOTS GUIDE
======================================================================

You can place screenshots or preview art for each ocean zone in this
directory. The Zone Selection UI will automatically detect and load
them at runtime.

----------------------------------------------------------------------
1. RECOMMENDED FILE NAMES
----------------------------------------------------------------------
Save your screenshot images (PNG or JPG) using ANY of the following naming
conventions:

• By Scene Name (Recommended):
    SunlightZone.png     (Zone 0 - Sunlight Zone)
    TwilightZone.png     (Zone 1 - Twilight Zone)
    MidnightZone.png     (Zone 2 - Midnight Zone)
    AbyssZone.png        (Zone 3 - Abyss Zone)
    HadalZone.png        (Zone 4 - Hadal Zone)

• Or By Index:
    Zone_0.png           (Zone 0 - Sunlight Zone)
    Zone_1.png           (Zone 1 - Twilight Zone)
    Zone_2.png           (Zone 2 - Midnight Zone)
    Zone_3.png           (Zone 3 - Abyss Zone)
    Zone_4.png           (Zone 4 - Hadal Zone)

----------------------------------------------------------------------
2. UNITY IMPORT SETTINGS (IMPORTANT!)
----------------------------------------------------------------------
When you add an image into Unity (e.g. Assets/Resources/UI/ZoneScreenshots/):
  1. Click on the image file in the Project window.
  2. In the Inspector, change "Texture Type" from "Default" to:
         "Sprite (2D and UI)"
  3. Click "Apply" at the bottom of the Inspector.

* Note: Unity UI Image components require the texture to be imported
  as a Sprite (2D and UI) in order to be rendered.

----------------------------------------------------------------------
3. RECOMMENDED RESOLUTION & ASPECT RATIO
----------------------------------------------------------------------
  • Aspect Ratio: ~16:9 or 3:2
  • Recommended Size: 680 x 440 px (or higher, e.g. 1280 x 720 / 1920 x 1080)
  • The preview frame automatically crops/masks to fit cleanly with rounded
    card edges.

----------------------------------------------------------------------
4. ALTERNATIVE: INSPECTOR DRAG-AND-DROP
----------------------------------------------------------------------
If you prefer not using Resources, you can also assign sprites directly:
  1. Select the ZoneSelectionUI GameObject / Prefab.
  2. Under the "Zone Screenshots (Optional)" foldout in the Inspector:
     - Element 0: Sunlight Zone
     - Element 1: Twilight Zone
     - Element 2: Midnight Zone
     - Element 3: Abyss Zone
     - Element 4: Hadal Zone
======================================================================
