# Android preview corruption, tutorial cancellation, and flicker investigation

## Implemented changes

- Sorting model thumbnails now use a normalized, unit-sized model in a modest-coordinate preview stage. A synchronous URP `SingleCameraRequest` runs after the main frame into a cleared, single-sample RGBA target. Pixels are copied into a readable Texture2D before the temporary stage and render target are released. The UI retains the completed thumbnail, and captures refresh after app resume. Failed requests stay hidden and emit a GPU/API diagnostic instead of exposing uninitialized GPU data.
- Research quiz models also use normalized models at modest coordinates, single-sample render targets, and the shared URP render-request utility after the main frame. Independent idle rotation and 15 FPS rendering are retained. Preview skinning updates offscreen. Render targets are recreated if lost after app background/resume.
- The shared preview renderer restores the previous fog, active render texture, and camera target even when rendering fails. The bestiary snapshot path also uses this utility; its live camera is disabled whenever its inspection image is hidden or its model is absent, and retains its offscreen target when active.
- Gameplay cameras exclude the ModelPreview layer. Minigame screen cameras are disabled before exploration cameras are restored, rather than waiting for deferred object destruction. Offscreen preview cameras are excluded from minigame screen-camera handoffs.
- Android graphics API preference changed from Vulkan/OpenGL ES 3 to OpenGL ES 3/Vulkan. Desktop settings and the URP renderer assets are unchanged. This is a compatibility mitigation for the reported device flicker, not proof that this device has a Vulkan driver fault.
- Development builds emit GPU, driver, graphics API, quality, and pipeline information. They warn when multiple base cameras simultaneously target the gameplay screen.

## Confirmed control-flow defects

1. Hiding the minigame slideshow did not clear its open flag or pending launch callback. The HUD visibility guard could therefore keep the exploration HUD hidden after a tutorial abort. `HideInstant`/`CancelSlideshow` now clear logical state and input blocking; ordinary Continue/slide-Skip captures and invokes its callback exactly once.
2. Minigame cancellation attempted to restore the HUD before all components were cancelled. Cancellation now retains UI ownership throughout teardown, invalidates pending activity callbacks, and restores controls/time/HUD after teardown. Reentrant cancellation is guarded.
3. Hazard Dodge could hide its own host object before reporting normal completion. Its `OnDisable` abort could stop the completion coroutine. Completion is now marked before hiding the root, while abort clears warning/result banners, vignettes, callbacks, and transition routines.
4. The submarine camera could reactivate a deliberately disabled touch zone and process direct touch input while a minigame was active. It now respects minigames, hidden exploration HUD, pause, and cinematics; HUD/input activation remains owned by UIManager.
5. Hazard/cleanup screen cameras could remain enabled until the end of the frame after camera restoration. Both are now disabled immediately before exploration cameras are restored.

## What remains unconfirmed on the device

The screenshots establish corrupted preview pixels and disappearing world geometry, but do not identify the exact GPU fault. The old preview implementation combined very large coordinates, multisampled targets, and manual Camera.Render calls. Those are removed from sorting and research previews. The Android configuration preferred Vulkan, which has documented device-dependent RenderGraph/flickering issues. OpenGL ES 3 is now preferred as a scoped Android compatibility mitigation.

The distance culler already has a hysteresis gap and uses the player position; it was not changed. The terrain mesh recalculates bounds and uses the expected index format. Existing environment placement edits and art assets were not changed by this repair.

Relevant Unity documentation and related reports:

- URP single-camera requests: https://docs.unity.cn/Packages/com.unity.render-pipelines.universal%4017.0/api/UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest.html
- Mobile precision guidance: https://docs.unity.cn/6000.0/Documentation/Manual/SL-DataTypesAndPrecision.html
- Related Vulkan/Mali RenderGraph flicker report: https://issuetracker-mig.prd.it.unity3d.com/issues/android-vulkan-animations-glitch-out-when-using-a-specific-canvas-layout-and-rendergraph-on-mali-gpu-devices
- Related Vulkan fullscreen artifacts report: https://issuetracker.unity.com/issues/10111/horizontal-graphical-artifacts-are-visible-in-player-when-using-urp-rendergraph-with-fullscreen-blit-and-using-vulkan-graphics-api

These related reports are not a reproduction of this project's device failure.

## Verification

Run:

```
python Tests/AndroidRendering/run_checks.py
python Tests/Cleanup/run_checks.py
python Tests/ResearchVerification/run_checks.py
```

The first runner compiles actual production cancellation methods and the shared render utility against stand-ins for Unity side effects. It exercises slideshow close/cancel, reentrant callbacks, cancelled activity tickets, teardown/HUD ordering, camera handoff order, and render-state restoration after unsupported requests or exceptions. It does not simulate GPU pixels or the actual scene hierarchy.

Completed verification: runtime and Editor C# compilation succeeded against the
project's Unity assemblies. The rendering/cancellation suite passed 27 assertions,
cleanup passed 15,526 assertions, and research verification passed 4,600 assertions.
Scoped Git whitespace checks passed. These results do not verify Android GPU output.

Before accepting the Android repair:

1. Rebuild the APK so the C# and graphics API changes are included. Preserve the normal save data.
2. Use a Development Build on the affected device. Check the `[RenderingDiagnostics]` startup line reports OpenGLES3 and record the GPU/driver.
3. In Minigame 3, inspect all three bins and all debris types. Complete multiple rounds, scroll and drag, then background/resume the app. No colored blocks, stale textures, or missing previews should appear.
4. In research verification, inspect a regular question and every camera-answer model. Models should rotate independently, photographs remain unchanged, and closing/reopening the quiz or bestiary should not corrupt a preview.
5. Skip the whole tutorial during the slideshow, hazard alert, active hazard game, and results. Check sonar, labels, joystick, reticle, scan/interact controls, time scale, and the exploration camera. No pending minigame should start afterward.
6. Separately use the instruction slideshow's Skip button: it should still start Hazard Dodge normally. Complete and fail a normal run and confirm callbacks and HUD restoration each occur once.
7. Swim and rotate around the same reef before and after minigames. Check the seabed remains visible. If flicker persists, retain the new diagnostics and capture Android Logcat to separate driver/RenderGraph errors from camera or culling state.

ADB was available, but no Android device was attached during this repair. Device rendering and APK behavior therefore still require the checks above.
