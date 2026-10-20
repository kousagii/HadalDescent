# Minigame 4: environmental hazards

The existing Hazard Dodge entry point, three-lane controls, HUD and return-to-exploration flow launch the updated activity. Let Unity import the new files before entering Play Mode. No scene edits, manual prefab assignments or downloaded assets are required.

## Existing assets used

`Assets/Resources/HazardDodgeVisuals.asset` references the existing environment prefabs:

| Asset | Use |
| --- | --- |
| `Assets/Prefabs/Environment/Rocks/Icosphere.prefab` | Boulder and falling-rock variation |
| `Assets/Prefabs/Environment/Rocks/Icosphere_002.prefab` | Second rock variation |
| `Assets/Prefabs/Environment/Rocks/Cylinder_001.prefab` | Vent chimney |

The models are fitted to the lane width and grounded using their mesh geometry. Only runtime instances are adjusted; the original prefabs and materials are unchanged. Existing component prefab overrides still take precedence.

Currents use a large cyan geometric chevron with cyan streak particles flowing in the push direction. Vent plumes and sediment also use particle systems created at runtime. `Assets/Resources/Materials/Hazard_Particles.mat` explicitly references the installed URP particle shader so player builds retain that dependency. A small soft particle texture is generated once per run. Particle counts are capped at 36 per current, 32 per vent and 96 per sediment cloud, plus 64 ambient marine-snow particles and 80 nearby stirred-silt particles. Sizes, lifetimes, depth layers and low-quality noise vary their motion. Particle shadows and depth-based soft particles remain disabled.

## Behavior

| Hazard | Visual and effect |
| --- | --- |
| Rock outcrop | Existing rock model with an orange footprint; contact removes one hull point. |
| Falling rock | Existing rock drops toward the marked seabed footprint before reaching the submarine; contact removes one hull point. |
| Hot vent | Existing chimney emits a gray mineral plume; entering the marked area removes one hull point. |
| Current surge | A cyan chevron pushes one adjacent lane in the direction shown, once on contact. 85% of waves containing a current pair a center-lane chevron with a rock ahead in its destination lane. That rock arrives 0.9 seconds later, giving time to swipe away; the other outer lane remains clear. An immediately overlapping obstacle or track edge prevents a forced move; the chevron never reverses direction. Swipes always change lanes normally. No stabilization meter, counter-steering task or current instruction text. Currents do not directly remove hull points. |
| Sediment cloud | Large, dense brown wisps make the cloud visible before entry. Contact immediately reduces visibility, followed by roughly 2.8-3.2 seconds of stronger brown haze, localized blur and slight distortion, then gradual recovery. Movement and nearby disturbances stir more silt. Density is capped and the submarine has a protected clear area in the screen effect. HUD and controls stay sharp and active. No direct hull damage. |

Every wave reserves one lane without hazards; a data pod may occupy that lane. Solid impacts have a 0.75-second protection interval to avoid stacked damage. Collision checks use the submarine's current horizontal position while it changes lanes, and account for hazards crossing its position between frames.

Chevron contact selects the adjacent lane through the existing smooth lane movement. The submarine stays in that lane after the push, and manual steering remains available immediately. Brief hull banking and restrained camera sway give movement feedback. Input consumes at most one lane change per touch gesture, including quick release gestures; pausing clears unfinished gestures.

`HazardDodgeAtmosphere` registers a render pass only for its stage camera. `Assets/Resources/Materials/Hazard_Underwater.mat` references `Assets/Shaders/HazardUnderwater.shader`, keeping the shader in builds. The effect runs before overlay UI, using camera depth for atmospheric perspective, cool color grading and sediment visibility. Screen rays are removed. It protects the submarine's projected location, reduces blur on nearby silhouettes, and uses smooth condition values rather than toggling a global volume. The blur uses five color samples only while sediment is visible, with one scene-color copy. Render Graph and URP compatibility-mode implementations are included. No shared renderer asset, global fog, global volume or scene camera is edited. If the material/shader is unavailable, unsupported, or its render pass is skipped, the brown fallback tint remains active. A material existing by itself no longer disables this fallback: the camera pass must actually have rendered recently.

Sunlight uses outcrops, currents and sediment. Twilight adds falling rocks. Midnight, Abyss and Hadal randomly choose a rocky passage or vent field; only vent fields include hot vents. The opening banner identifies the route. Deeper-zone speed, hull allowance and base reward retain their existing settings. Runs still last 20 seconds, pods award 5 RDP each, and failure deducts 50 RDP.

Effects pause with the pause menu and are cleared on finish/cancel. Runtime models, particle texture and materials are released when the stage closes. The encounter warning says **SWIPE TO DODGE.** and retains **COLLECT RARE PEARLS FOR BONUS RDP**. The bottom status-message panel is removed entirely, including sediment/low-visibility messages; hazard feedback comes from the visuals and movement.

## Test immediately

1. Enter Play Mode in the zone you want to test. Complete or skip the main tutorial first, and close the shop, bestiary and other popups.
2. Search for `HazardZoneTrigger` in the Hierarchy and select that object, normally under the zone's managers.
3. Open the three-dot menu on its **Hazard Zone Trigger** component (or right-click the component header).
4. Choose **▶ Test Trigger Hazard Minigame Now**. This bypasses the startup grace and automatic interval; activity, tutorial, cinematic and zone-transition guards still apply.
5. The existing Minigame 4 instruction slideshow appears if configured. Finish or skip those instructions to enter the encounter. Use A/D, arrow keys, or swipe left/right to change lanes.

This launches a normal encounter with the usual rewards or failure penalty. It can be launched again after returning to exploration. For an automatic startup test, the component's **Test Trigger On Start** option and **Initial Test Delay** can also be configured before entering Play Mode; keep that option off for normal gameplay.

While the run is active, select **Minigame4HUD** (or the active object with **Hazard Dodge Minigame**) and open that component's context menu. Under **Test Conditions**, use **Current - Push Left**, **Current - Push Right**, or inject light/dense sediment. Current commands test the movement; normal spawned chevrons show the track visual. Sediment commands test the screen visibility effect; normal spawned clouds show the approaching world particles. These Editor-only commands do not change saved tuning. Choose **HadalDescent > Hazard Dodge > Validate Assets and Shader** to validate Unity's imported references and compile the water shader in the current editor graphics environment.

## Automatic encounters

All five current zone scenes enable **Periodic Surge** and disable **Spatial Proximity** on their global HazardZoneTrigger. The automatic encounter therefore depends on exploration time rather than proximity to a rock or vent.

| Zone | Random exploration interval |
| --- | --- |
| Sunlight, Twilight | 120-180 seconds (2-3 minutes) |
| Midnight, Abyss, Hadal | 60-120 seconds (1-2 minutes) |

The countdown pauses during menus, popups, tutorials, cinematics, other minigames and zone-transition protection. Opening the bestiary for one minute does not consume one minute of the interval. The startup grace overlaps the first countdown rather than adding another 25 seconds. Tutorial Step 7 launches its scripted encounter separately.

When the interval expires, MinigameManager launches the configured instruction slideshow, then the encounter warning and three-lane run. The warning retains **COLLECT RARE PEARLS FOR BONUS RDP**, visible only with the encounter warning. A new random interval is selected when an encounter launches; it starts counting again when exploration resumes, including after a cancelled encounter. Tutorial marker cleanup also resets the interval.

Periodic managers now stay active when their spatial marker is retired. Previously, deactivating the whole GameObject stopped its coroutine after the first encounter (including tutorial cleanup). Spatial-only triggers still retire after use. The Inspector's **Trigger Cooldown** field applies to optional spatial triggering; automatic timing uses the zone intervals above.

## Verify in Unity and Android

1. Enter Play Mode and launch a normal Hazard Dodge encounter. The existing tutorial and its skip action still lead to the activity.
2. Check both rock variations, ground alignment, orange footprints and falling-rock timing. Pass through an obstacle while changing lanes to confirm contact follows the visible submarine.
3. Enter left/right chevrons: contact should push one lane in that direction exactly once. The submarine must stay in the new lane after the push, with every subsequent swipe changing lanes normally. Check track edges and nearby rocks: blocked pushes must hold position rather than reverse direction. No words, stability bar or current instruction should appear on the lanes or HUD.
4. Inject light and dense sediment. Compare holding course with repeated lane changes, and combine sediment with a current. Rapid movement should raise particle activity and haze. Let it settle and confirm smooth recovery. The submarine and near hazards should remain identifiable, with sharp HUD text. Test finishing, failing and cancelling during sediment; the result banner and exploration must not retain the effect.
5. Test deeper zones and repeat encounters to see both rocky passages and vent fields. Vent particles should originate at the chimney top. Sunlight/Twilight must not spawn hot vents.
6. Pause/resume with effects visible. Complete, fail, and cancel separate runs, then relaunch. Verify the exploration camera and HUD return and no old hazards remain.
7. Rebuild the Android player and repeat the checks on the target device. Confirm model and particle rendering, landscape layout, swipe input and tutorial skip. The three tutorial images now include the supplied alert, chevron and sediment screenshots with updated instructions, matching heading sizes and centered footers.

Tutorial pages are synchronized in `Assets/Arts/Tutorial/` and `Assets/Resources/Tutorial/`, retaining their existing filenames and import metadata. The runtime slideshow keeps its three-page mapping. Built-in image generation was used; original references, final images, the prompt set and saved-file hashes are under `output/imagegen/minigame4/`. Restart Play Mode after the images import, since the instruction overlay caches tutorial textures during a session.

Also check 30/60 FPS, the supported landscape aspect ratios, and both mobile/desktop quality profiles. The projected clear area should follow camera sway and the submarine across all lanes. Use the Frame Debugger to check **Hazard Water Copy** and **Hazard Water and Sediment** on the stage camera only; check draw calls, GPU time and memory on the Android device. Confirm that no screen rays remain.

## Automated checks

`python Tests/HazardDodge/run_checks.py` exercises the production wave rules, direction-preserving push safety, collision/effect dispatch, normal steering after a push, reward deduplication, finish cleanup and Resources reference IDs. It covers both directions, edges and blocked destinations, one push per chevron, settling into the new lane without springing back, 10/30/60/120 Hz movement, immediate sediment onset, dense/light exposure, movement/disturbance interactions, settling and zero-time pause behavior. It also steps the production trigger coroutine to verify both zone intervals, repeated encounters, blocked-time pauses, tutorial cleanup and the manual test guard. Unity side effects are substituted; this does not validate rendered pixels, native model bounds, particle appearance or HUD layout.

`python Tests/AndroidRendering/run_checks.py` covers the existing camera-restoration and tutorial-cancellation control flow. Native Play Mode and device rendering remain manual checks.

Runtime and Editor C# compilation and the portable interaction checks passed during this change. The attempted native Unity batch validation exited during license/WMI startup before running `HazardDodgeValidation`; shader GPU rendering, visual layout and Android performance therefore require the normal Editor/device checks above.
