# Twilight and Midnight movement and grounding repair

## Changes

- Locomotion now computes propulsion and boundary correction on the same proposed
  position, then calls `Rigidbody.MovePosition` once per physics tick. Previously,
  jetters could submit a swim step followed by a correction based on their old
  position, discarding horizontal progress near the seabed or zone limits.
- Depth projection uses one consistent interval. If a hill conflicts with the
  preferred depth band, terrain clearance temporarily takes priority, while the
  absolute submerged ceiling stays at -3 m. The old upper/lower `if/else` could
  alternate between incompatible limits on successive ticks.
- ContextSteering leaves position correction to CreatureLocomotion when present.
  Locomotion refreshes its AI reference during initialization, after the spawner
  has added its peer components, so boundary turns and fleeing can reach the AI.
- Procedural pulsation cannot resize the Rigidbody root. Child-model pulsation
  and existing skeletal animations remain available.
- Ground contact and body clearance use actual mesh geometry rather than cached
  renderer bounds. Animated meshes are baked once during initialization/contact
  measurement. Measurement no longer temporarily teleports the live creature to
  the world origin. Initial animation poses are evaluated before placement and
  Animator root motion is disabled on spawned creatures.
- Ring Triangular Batfish and Albatross Coffinfish now use BenthicFollower movement,
  with 10/12 m wander radii and 0.55/0.45 m/s base speeds respectively. They follow
  the terrain, make slow turns, and keep the existing 2–4.5 second destination
  rests. Their feet/underside use the grounded crawler contact profile. Steering
  now accepts zero speed so a destination rest can actually stop movement.

The initial movement repair changed only those two species data assets and their
generator definitions. The coral follow-up below adds an anchor/component to the
Deep-water Coral prefab while preserving its existing Box Collider and overrides.
Existing GLBs, animation controllers, and other species assets were preserved.
The coffinfish's supplied model has no skeletal animation; it moves
through the existing locomotion system without adding a fin-walking animation.

The movement overwrite and contradictory bound handling are confirmed code
defects. The screenshots' exact grounding gap still needs an imported-model
Play Mode check. The repair removes the cached/animation-envelope bounds path
that could place the shell or coral above the visible seabed.

Unity documents that `BakeMesh` returns geometry relative to the renderer's
transform and can compute it offscreen:
[SkinnedMeshRenderer.BakeMesh](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SkinnedMeshRenderer.BakeMesh.html).
This repair uses that CPU operation only for initialization/contact measurement,
not every physics frame.

## Verification

`python Tests/CreatureMovement/run_checks.py` compiles and runs the production
FixedUpdate, jet propulsion, and bounds methods with stand-ins for Unity side
effects. It checks deferred movement requests, forward progress over many pulse
cycles, tangent movement at perimeter limits, conflicting terrain/depth limits,
stable depth projection, and both requested species profiles. The other movement
archetypes and native physics/skinning are not simulated by that runner.

Completed checks: runtime and Editor C# compilation succeeded, all 5,606 movement
assertions and both species profile checks passed, and the 4,600 research
verification assertions passed against the updated species data. Scoped Git
whitespace checks also passed.

In Unity, outside Play Mode, select
**HadalDescent > Species > Validate Movement and Grounding**. This read-only
validator creates temporary preview-scene instances of the assigned Twilight and
Midnight models, checks measurements at different positions/headings, and checks
stationary contact against an isolated flat fallback seabed. It saves no assets.
It can also run with `-executeMethod SpeciesMovementValidation.RunBatch`.

The headless Unity attempt exited during startup with code 1 before executing
the validator (see `Temp/SpeciesMovementValidation.log`). Native imported-model
and Play Mode checks therefore remain pending; C# compilation and the portable
suite can still run independently.

## Play Mode / Android checks

1. Reload TwilightZone so SpeciesSpawner creates fresh instances. Inspect the
   Philippine Deep-water Clam, Deep-water Coral, and sponge at several positions
   and specimen sizes. Their base should contact the terrain, including after
   animation has played for 30 seconds and after leaving/re-entering culling range.
2. Observe Arm Squid and the other jetters for at least 30 seconds, including near
   the seabed and a depth/perimeter edge. They should thrust, coast, and turn while
   continuing to travel rather than shaking in one position.
3. Repeat with Diamondback Squid and other mobile species in MidnightZone. Check
   the stationary placeholder clam too; its mesh should remain grounded.
4. Observe both newly mobile fish: slow seabed travel, terrain-following turns,
   short rests, then another destination. Scan/discovery and sonar must still work.
5. Pause/resume and enter/exit a minigame. Creature movement should pause with
   ocean simulation and resume without jumps. Rebuild the APK for Android testing.

No Inspector wiring or new art is required. Existing live instances need a scene
reload; runtime changes do not rewrite stored prefab or GLB assets.

## Deep-water Coral contact anchor follow-up

The coral GLB contains multiple sections at different heights. In its source rest
pose, the primary trunk (`Cylinder`) extends to about -0.270217 local Y, while
another section extends lower. Grounding the combined minimum can therefore leave
the primary attachment point raised. The ocean-floor height sampler also uses
bilinear interpolation, while the visible mesh/collider is triangulated; an exact
collider hit avoids that difference.

The Deep-water Coral prefab now contains a `GroundContact` child at approximately
`(-0.006163, -0.270217, -0.000075)` and a `CreatureGroundContact` component pointing
to it. This point comes from the lower rim of the primary trunk in the source GLB,
converted to the importer's Unity coordinates. Its final imported/animated visual
alignment still needs the Play Mode check below.

For stationary species with this component, SpeciesSpawner bypasses its combined
bounds placement and height safeguard. It samples beneath the attachment point
using `Collider.Raycast` on the generated ocean floor's MeshCollider, then places
the point 0.05 world metres below the hit. Own colliders, props, and other species
cannot intercept that query. Horizontal spawn position and rotation are preserved;
repeated placement does not accumulate offsets. When a floor mesh is unavailable,
placement uses the terrain height sample or the existing preview fallback floor.
Species without a valid enabled anchor retain their previous placement path.

The existing Box Collider size, center, and non-trigger configuration are retained.
No other species prefab, source model, or animation asset was changed by this follow-up.
The anchor controls the main trunk; it does not independently align every section
of the colony. If an authored section still visibly hovers, its source geometry or
individual attachment point needs separate adjustment.

Run `python Tests/CreatureMovement/run_grounding_checks.py` for the production
component's placement tests and prefab-reference checks. These check multiple
scales/headings, world-space embed depth, physical-hit preference over sampled
height, sloped support, invalid/missing anchors, fallback, and repeated placement.
Native collider intersection and imported mesh appearance remain Unity checks.

The follow-up passed runtime/Editor compilation, 152 anchored-placement checks,
prefab wiring/collider checks, and the existing 5,606 movement assertions. The
profile check now accepts Inspector speed tuning while still requiring both fish
to remain mobile seabed followers; the current fish speed settings were retained.

Reload TwilightZone and inspect newly spawned coral. In Prefab Mode the
GroundContact child can be moved to refine the trunk's attachment point. The
component's **Embed Depth** defaults to 0.05 m; adjust within 0.03–0.08 m if needed.
Check random specimen sizes and gentle slopes, then rebuild the Android APK.
The Editor **Validate Movement and Grounding** command also verifies that the
authored point, rather than the colony's lowest branch, meets the fallback seabed.

## Species facing and swimming-ray clearance follow-up

The corrections are stored in each species' existing data asset:

| Species | Authored nose direction after glTFast import | modelYawOffset |
| --- | --- | --- |
| Albatross Coffinfish | +X | -90 |
| Threelight Hatchetfish | -X | 90 |
| Shorttail Lanternshark | -X | 90 |
| Sulu Sea Ribbonfish | +X | -90 |
| Deep-water Stingray | +Z | 0 (retained) |

These directions were checked from projections of the supplied models, including
glTFast's X-axis handedness conversion and skin bind matrices. Existing prefab
rotations and source GLBs were not rewritten. Generator definitions carry the
same corrections. The current Inspector speed tuning was preserved.

Cruiser propulsion previously used raw `transform.forward`, while rotation and
steering had a separate yaw correction. Setting the asset offset alone would
therefore still allow lateral travel. `CreatureFacing` now supplies a consistent
travel heading for propulsion, turns, patrol/flee targets, probes, and banking.
Body/sensor dimensions are measured in that same heading frame so a model's
longitudinal X dimension does not become its sensed width. Offsets of zero retain
the usual forward direction.

Deep-water Stingray's downward wing stroke reaches approximately -1.3113 model-local
Y in the source animation, compared with roughly -0.3706 in its source rest pose.
Its data asset now contains five `benthicSupportPoints` covering the belly, left and
right wings, nose and tail. These use a conservative -1.32 Y envelope and the
actual wing positions, rather than the midpoint of the long-tail bounding box.
Source animation inspection included keyed poses and intervening samples, honoring
STEP and quaternion interpolation; this is source analysis, not a Unity playback test.

The points are scaled/rotated and cached once during initialization. The existing
`benthicSurfaceOffset = 0.4` is now used as swimming-ray clearance. At each movement
step the solver checks the generated seabed collider under all support points for
both the current and next body rotation. Upward penetration correction is immediate;
descending height still follows the smoothed target. No mesh skin baking is added
to the physics loop. Spawn placement applies the same authored underside allowance
before the first visible physics step. Other benthic crawlers retain their contact
embed profile, and rays without authored support points use a measured footprint.

Validation: the expanded portable runner passed 7,122 movement, facing and clearance
assertions, with checks of the four offset assets and stingray profile. The research
verification suite passed 4,600 assertions. These tests substitute Unity side effects;
actual collider intersection, imported animation playback, and device rendering still
need Play Mode/Android verification.

After Unity recompiles, reload TwilightZone to create fresh instances. Check each
fish swims nose-first along straight paths and through turns. Check the stingray at
spawn, during the full wing stroke, over slopes/crests, near wing-sized ground bumps,
and at different specimen scales. Pause/resume and enter/exit a minigame. Rebuild the
APK for device testing. No new art or manual Inspector wiring is required.


## Benthic footprint placement and crawler contact follow-up

The old spawn search checked a candidate center before specimen scaling and
horizontal mesh recentering. Its last fallback could still return an intersecting
location. Crawlers were also deliberately lowered into sediment at spawn and
while moving, and unlike rays lacked a hard underside clearance constraint.
These code paths match the buried lobster and crown-star/sponge overlap reports.
The Volsellate Crown Star remains stationary; its prop overlap originated during
placement rather than movement.

Benthic instances now undergo final clearance validation after scale, rotation,
visual recentering and ground contact are resolved. The query uses an oriented
box covering the full measured visual bounds (including long arms/antennae),
with a 0.04 m margin. It checks solid tagged environment props, environment fact
objects and stationary species across all layers, independently of the physics
collision matrix. Own colliders, bare seabed and passable vegetation triggers
are excluded. Generated environment props already receive colliders and tags;
no prop prefabs were rewritten. A saturated overlap buffer is treated as unsafe.
Every search fallback reaches this same final check. An intersecting candidate
is retried up to 12 times using the same scaled instance and cached measurement;
if no safe location is found, that instance is discarded. The existing placed /
target warning reports a reduced population. Only successful instances register
positions and anemone hosts.

Ground contact for crawlers, stars and clams now uses up to 16 lowest actual
geometry samples distributed across each specimen's footprint. Readable meshes
and baked skins supply vertices; unreadable static meshes use conservative mesh
bounds. Samples are measured at initialization, never baked every physics tick.
The new SpeciesData **Ground Contact Clearance** defaults to 0.01 m (scaled per
specimen); the old negative sediment offsets no longer govern these species.
**Benthic Surface Offset** retains the separate swimming-ray clearance purpose.
Existing authored support points, including the stingray's animated wing envelope,
override automatic samples. Coral's authored GroundContact anchor and the existing
upright colony/anemone contact paths remain available.

Height queries use the generated ocean floor MeshCollider directly, avoiding
props and bilinear-vs-triangle height differences. Moving benthics apply immediate
upward underside protection at current and proposed rotations, with smooth descent.
Final protection runs after perimeter correction. Full-body prop checks examine
the proposed pose and midpoint; unsafe translation is cancelled and the existing
AI is told to turn away. A turn in place is allowed only when its resulting
footprint is clear. Ground walkers steer around low solid props instead of using
the swimming-ray glide lift. No scene, source model, collider or animation assets
were edited by this follow-up.

Validation: `python Tests/CreatureMovement/run_checks.py` now exercises the actual
final placement retry loop, overlap classification and final movement constraint
with substituted scene/physics queries. It covers shifted/scaled footprint inputs,
unsafe fallback exhaustion, final separation, sibling/self collider filtering,
query overflow, slope support, immediate ascent, smooth descent, obstructed travel,
midpoint and rotation checks. Actual Unity mesh intersection and animated appearance
still require Play Mode and Android checks.

After scripts recompile, reload SunlightZone and TwilightZone to create fresh
instances. Inspect the lobster's feet, all crown-star arms, and clam shell bases
at several specimen sizes and headings. Watch the moving benthics cross dune crests
and turn beside sponges/corals. They should contact the sand and route around solid
props. Check the Console for reduced placed/target counts in dense areas. The
read-only **HadalDescent > Species > Validate Movement and Grounding** menu now also
includes Sunlight benthics and checks generated support samples. Rebuild the APK
for device verification. No manual Inspector wiring or new art is required.


## Mino Nylon Shrimp circling during escape

The benthic escape branch previously classified every Malacostraca as a lobster,
then replaced the threat-aware steering direction with the negative of its current
body heading. The same controller rotated the body towards that movement direction,
so the escape heading kept moving as the body turned. The shrimp was additionally
limited to the lobster's 1.4–2 m/s escape speed instead of its authored 4.5 m/s.
Fleeing only ended after exceeding the spawn return distance, so circling near
spawn could leave the state active indefinitely. Obstacle deflection could also
switch it to wandering, allowing proximity to immediately restart the episode.

Mino Nylon Shrimp now follows the existing threat-aware steering goal at its
configured flee speed. Only `spiny_lobster_001` retains a backward phase, limited
to the first 0.8 seconds: travel follows the escape goal while body heading faces
opposite travel. Rotation no longer supplies the escape direction. Benthic and
surface escapes use horizontal threat separation; an overlapping/overhead threat
uses the direction captured at flee entry rather than the body's changing heading.
The original supplied threat position remains available when no player is found.

The shared flee state now ends after reaching a safe distance (at least 1.5 times
fleeRange or fleeRange + 3 m, after a minimum 0.75 seconds), after six seconds, or
at the existing maximum distance from spawn. It restores normal return speed and
applies a three-second cooldown to automatic proximity retriggers. Failed scans
can still explicitly request escape. Repeated Flee calls and obstacle deflection
do not reset the current episode's timer. Art, animation, prefabs and species
settings were preserved in this repair, including the shrimp's current wander
radius, speed and facing offset.

`python Tests/CreatureMovement/run_checks.py` now also runs production flee state,
goal and obstacle-deflection methods plus the escape-selection/body-heading
sections from CreatureLocomotion. It checks 578 escape cases in addition to the
existing 7,122 movement and 1,187 benthic-placement checks. Scene queries, terrain
and rigidbody side effects are substituted; native animation and physics remain
Play Mode/device checks.

Reload TwilightZone, approach a shrimp within its 10 m flee range, and observe it
move away instead of continuously turning around itself. Follow it for longer
than six seconds, then stop and let it return. Check escape near a prop and after
a failed scan. In SunlightZone, verify the lobster's short backward phase ends
and normal travel resumes. Rebuild the APK to verify the updated behaviour on
Android. No Inspector wiring or asset regeneration is required.
