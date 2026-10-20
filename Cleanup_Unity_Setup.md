# Minigame 3: varied collection and drag sorting

The existing `MinigameManager.TriggerDebrisCleanupMinigame` entry point and exploration callbacks still launch the two-phase activity. No scene or existing prefab edits are required for the new sorting UI.

## Test immediately

1. Open `Assets/Scenes/MainMenu.unity` and enter Play Mode. Let Unity finish importing and compiling first.
2. Select **HadalDescent > Cleanup > Quick Test > Sorting - All Debris** to go straight to a tray containing all ten visual variants.
3. The three large bins occupy the left 70% of the sorting work area; the collected-debris tray occupies the right 30%. Drag an item sideways out of the tray into a bin. Browse the two-column tray with the mouse wheel, scrollbar, or a vertical swipe over an item. An incorrect drop returns the item and explains the category; dropping outside a bin returns it without affecting accuracy or streak. Correct drops show a floating +15 RDP at the bin, and the remaining tray items close the gap.
4. Try **Collection - Sunlight** or **Collection - Hadal** for the full round. Move with the joystick, A/D, or arrow keys; use Drop Claw, Space, or the down arrow to retrieve debris.
5. Preview results never grant RDP, write progress, or remove a world debris cluster. Return before launching another preview. These menus require a menu scene so exploration controls cannot interfere.

## Existing art connections

`Assets/Resources/Minigames/CleanupModelLibrary.asset` references the existing SodaCan, MetalPipes, MetalTube, MetalDrum models and the PlasticBin, MetalBin, HazardBin prefabs. Resources keeps the references available in a player build. Any prefab already assigned directly to an EnvironmentalCleanupMinigame component takes precedence over these defaults.

Assign the remaining models to the library when ready:

| Library field | Model |
| --- | --- |
| Water Bottle | Water bottle |
| Plastic Bag | Plastic bag |
| Six Pack Rings | Six-pack rings |
| Aa Battery | AA battery |
| Electronic Board | Electronic board |
| Submarine Battery Cell | Submarine battery cell |

These six library slots are empty because those models were not found in the supplied Minigame 3 folder. Existing component overrides continue to work. Unassigned items retain the existing procedural placeholders in both extraction and sorting. Sorting thumbnails are rendered from the actual retrieved instance; they do not need separate sprites or photographs. Bin positions remain Plastics, Metals, Hazards from left to right.

Use **HadalDescent > Cleanup > Validate Connected Assets** to check that Unity resolves all four metal models and three bin prefabs and that each contains renderers.

## Round behavior and tuning

- Each cluster's plastic/metal/hazard counts form the baseline manifest. Zones add zero to four items, capped at eleven total. The default cluster has 5/6/7/8/9 items across zones.
- Selections and positions vary; both pipe models belong to Metals. Consecutive rounds avoid repeating the same available variation.
- Mixed Cleanup asks for 70% of the debris. Current Sweep asks for the plastics and increases their drift. Heavy Recovery asks for metals. Priority Recovery asks for hazardous debris. Focused rounds add zone extras from their focus category. These are optional recovery objectives worth 15 RDP.
- Plastics drift sideways by up to 0.45 world units and bob up to 0.35 units above their resting height. Current Sweep increases these limits to 0.8 sideways and 0.6 upward. Drift speed increases with depth; horizontal positions stay within claw reach. All metals, including the drum, reel at 50% of normal speed and take twice as long to return. The initial claw drop keeps its normal timing. Hazardous grabs within 0.22 world units of the model center earn a 3 RDP careful-recovery bonus.
- Collection starts with 30 seconds in every zone, regardless of debris count. An in-flight drop/reel finishes before Phase 2, including the last item. Zone difficulty increases through debris counts and plastic movement speed.
- A clownfish bump during reel-up releases the held debris. Both the claw and carried model can trigger the contact. The item falls back to the seabed and can be collected again after landing; the empty claw continues upward. Released items earn no retrieval credit or careful-recovery bonus. The existing three-second fish-disturbance penalty and bump sound remain, while the bottom retrieval/eco-disturbance notifications are removed.
- Sorting starts with 30 seconds in every zone, regardless of retrieved count. All items are available immediately and can be sorted in any order. The old tap-bin and 1/2/3 controls are replaced by mouse/touch dragging.
- Each correct placement earns 15 RDP. Every three consecutive correct placements adds 5 RDP; a wrong bin resets the streak but preserves already earned bonuses. A fully correct first attempt earns a 10 RDP Clean Sweep bonus. Accuracy measures items sorted correctly on their first attempt, so correcting an item does not erase a mistake.
- A successful round requires at least one retrieval and every retrieved item sorted before time runs out. As in the existing cleanup flow, retrieving every spawned item is optional. Successful rounds retain the base 30 RDP and 10 RDP per retrieved item, plus sorting and bonus rewards.
- An empty round or sorting timeout leaves the cluster available for retry and grants no RDP. Rewards and cluster cleanup happen once when returning from successful results. Cancelling a round does not grant rewards.

The sorting UI uses the existing ocean colors and Aloha font, with neutral item cards so background colors do not disclose their category. Existing custom sorting buttons are hidden during this phase; the header and configured results controls remain available. Thumbnails render once, and their textures are released when the round UI is destroyed.

## Play Mode checks

- Try a correct drop, a wrong drop, a drop outside all bins, and correcting a previous mistake.
- Check a three-item streak, a streak reset, and a perfect run. Verify correcting an item does not count as first-try accuracy.
- Pause while holding an item: it returns to the tray and the clock stops. Resume and continue. Test touch input on the target device as well as mouse input.
- Retrieve the final remaining object and confirm it appears in the sorting tray.
- Reel into a clownfish with both a small item and the drum. Check that the item falls, the collected count and recovery objective stay unchanged, and the item can be grabbed again after landing. Check cargo-only contact as well as a claw hit. Pause during a fall and verify it stops until resumed. No bottom retrieval or disturbance banner should appear.
- Retrieve nothing; then separately let sorting expire. Neither result should remove the cluster or grant points.
- Complete a real gameplay round and return. Confirm one reward grant, one cleanup notification, and restored exploration controls/camera. Relaunch a different cluster to check repeated-round cleanup.
- Check the tray at the supported screen aspect ratios. Confirm all ten preview items can be reached by scrolling, held items remain visible outside the tray mask, and vertical swipes scroll without counting as sorting attempts. The three bins stay fixed and visible while the tray scrolls.

Tutorial pages 1 and 2 use the saved original gameplay captures as references, retaining the blue background, step badges, and white lettering style. Page 1 explains joystick positioning and plastic drift without the added plastic illustrations. Page 2 explains the drop button, half-speed metal reeling, and dropping the item after a clownfish bump during reel-up. The obsolete retrieval notification is absent.

Tutorial page 3 (`Assets/Resources/Tutorial/Minigame 3-3.png`) shows the current sorting layout with the collected tray on the right, bins on the left, and a leftward drag arrow. Its instruction reads: "Drag collected items into the bins on the left to sort and earn RDP!" It retains the existing blue tutorial background, step badge, white lettering style, and wrong-bin reminder.

## Automated verification

For the Android model-preview and tutorial-skip repair, see
[Android rendering fixes](Android_Rendering_Fixes.md). Rebuild the APK and run
the device checks there; portable tests cannot verify rendered GPU pixels.

Run `python Tests/Cleanup/run_checks.py` to exercise the production manifest and sorting-score rules. It checks zone counts, cluster distributions, all ten variants, randomization, corrections, accuracy history, duplicate-drop protection, streaks, and clean sweeps. Rendering, pointer events, imported model appearance, and scene callbacks still require the Play Mode checks above.
