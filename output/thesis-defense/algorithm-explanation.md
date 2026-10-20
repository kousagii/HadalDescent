# Hadal Descent: algorithms explained for the whole group

Prepared from the supplied Chapters 1-3 and the current Unity source on 20 October 2026. This is a defense study guide, not a report of new experiments. The game was inspected through its source; it was not run or benchmarked for this guide.

## 1. The explanation everyone should remember

An algorithm is a sequence of steps for solving a problem. Hadal Descent combines three main techniques:

| Technique | Simple meaning | Problem it solves |
| --- | --- | --- |
| Perlin Noise terrain generation | Build the shape of the seafloor. | Produce varied terrain without sculpting every possible map manually. |
| Rule-based placement | Decide where things belong. | Keep generated species and objects consistent with configured habitats, depths, and gameplay constraints. |
| Context Steering | Decide which way a creature should move next. | Help creatures navigate the generated terrain and avoid nearby obstacles. |

The complete relationship is: **build the environment -> place its contents -> move creatures through it -> let the player discover and learn.**

PCG means Procedural Content Generation: creating content through programmed procedures. In this project, Perlin Noise supplies terrain patterns and placement rules organize the contents. PCG is the broader approach; Perlin Noise is one technique within it.

These algorithms support the chosen design. They are not the only possible way to make an educational game. Defend why they fit this game's requirements, without claiming every underwater game needs them.

## 2. Perlin Noise: the landscape builder

**Analogy:** Imagine raising and lowering points on a flexible sheet to create hills, shelves, and basins. Perlin Noise supplies smoothly changing values that help determine how high each point should be.

**How our implementation works:**

1. The game reads the selected ocean zone's dimensions, frequency, amplitude, and seed.
2. It builds a grid of points for the ocean floor.
3. It samples Unity's Perlin Noise function and combines several layers of patterns.
4. Additional shaping creates shelves, mounds, and basins, and applies height limits and edge shaping.
5. The points are connected into a visible 3D surface. The game also stores heights so placement and movement systems can query the floor.

The current terrain code therefore does more than multiply one noise value by a height. Perlin Noise is its underlying pattern generator, combined with deliberate terrain shaping.

**Three terms to understand:**

- **Frequency:** controls the scale of the features. Lower values generally produce broader formations; higher values produce more closely spaced changes over the same area. It does not mean how quickly the game updates.
- **Amplitude:** controls the amount of vertical height variation. It is not the width of the map. Final height is also affected by shaping and limits.
- **Seed:** a number used to select a reproducible pattern. With the same terrain settings and seed, the terrain can be recreated.

**Why use it here?** The design calls for varied underwater exploration across five zones. This procedure generates many terrain layouts from configurable settings and reduces the work of manually creating every layout. Smoothly varying values provide a useful starting point for continuous seafloor surfaces.

**How it helps enjoyment:** Varied layouts can support exploration and curiosity. Whether those layouts are enjoyable still depends on visibility, navigation, landmark placement, pacing, and playtesting.

**How it helps education:** It supplies an environment in which species can be encountered. Perlin Noise does not know marine biology; educational correctness comes from the researched content and placement rules.

**Current implementation detail:** The game creates and stores a seed for each zone in the player's save. Revisiting that zone uses its saved terrain pattern. Different seeds allow different layouts; do not say the whole map changes every time the player opens or revisits it.

**Defense answer:**

> We use Perlin Noise to generate varied seafloor shapes from configurable values. This supports exploration while reducing the need to manually create every terrain layout. We combine the noise with terrain shaping and limits so the result fits the intended game environment.

## 3. Rule-based placement: the habitat organizer

**Analogy:** A museum organizer does not put every exhibit anywhere at random. Each exhibit has a suitable section and enough space around it. Our placement system applies a similar idea to the underwater environment.

**How our implementation works:**

1. The game selects species available for the current zone.
2. It reads each species' configured depth range, preferred habitat type, movement type, and other settings.
3. It generates candidate positions and checks their suitability.
4. Depending on the species and placement stage, it considers habitat compatibility, depth, ground height, slope, clearance, and separation.
5. It accepts suitable candidates or tries other positions. The implementation also scores candidates and uses fallback searches when ideal locations are difficult to find.

Here, a **biome** is a configured habitat category, such as soft seabed, hard reef, rock, or open water. The rules are instructions written by the developers, supported by researched species data; the system does not learn ecology automatically.

**Concrete example from the code:** Anemones are placed before clownfish, and the clownfish placement logic uses anemone locations. This shows that the models are organized through a relationship in the placement logic rather than simply scattered as decoration. Species facts still need source verification.

**Why use it here?** Terrain variation alone cannot ensure meaningful species distribution. Placement rules give the generated environment structure and help avoid unsuitable or inaccessible positions. This directly supports the objective concerning species and habitats across the five zones.

**How it helps enjoyment:** Appropriate spacing and accessible locations can make discovery and scanning easier to understand and less frustrating.

**How it helps education:** The location of an organism contributes to what the player understands about its habitat. Correct text paired with a misleading placement could still teach an incorrect association.

**Practical limit:** The source contains compatibility rules, scored candidates, and fallbacks. It does not justify saying every organism is guaranteed to satisfy every ideal ecological constraint. Check final placements across multiple seeds, particularly fallback placements, before making that claim.

**Defense answer:**

> We use rule-based placement to control where species and environmental objects appear. The system checks configured conditions such as zone, depth, habitat, and spacing. This gives our procedural environment meaningful organization and supports accurate habitat associations, subject to validating the species data and generated placements.

## 4. Context Steering: the movement guide

**Analogy:** When walking toward a destination, you look for open space and turn around obstacles. A fish in the game repeatedly evaluates possible movement directions in a similar way.

**How our implementation works:**

1. A behavior controller gives a creature a goal, such as a roaming destination or a position away from a threat.
2. Context Steering evaluates several directions around the creature, including directions above and below it.
3. An **interest value** describes how useful a direction is for the goal and current movement.
4. A **danger value** describes nearby obstacles or boundary risks. Spherecasts act like invisible probes with thickness, helping account for space around the creature.
5. The system reduces the scores of risky directions, combines useful safe directions, and uses fallback directions or escape behavior when needed.
6. Movement and turning logic use the result. The process repeats as the creature moves.

The thesis and code use this directional score:

**S = I x (1 - D)^2**

- **S:** direction score.
- **I:** interest, from 0 to 1.
- **D:** danger, from 0 to 1.

For example, if interest is 0.8 and danger is 0.2, the score is 0.512. With the same interest and danger of 0.7, it falls to 0.072. A direction becomes much less attractive when danger rises. The code also filters high-danger directions and blends safe directions, so this formula is one part of the decision.

**Concrete example:** A creature's target is ahead, but a rock blocks that direction. The direction toward the rock receives danger, while a nearby open direction may retain a useful score. The creature can turn around the obstacle and keep moving.

**Why use it here?** The terrain is generated, so creature movement must respond to the terrain that actually exists. Local steering helps creatures react without requiring a manually authored route for every generated layout.

**How it helps enjoyment:** Responsive creatures make exploration more active and give scanning a moving target. Smoothness, scan difficulty, and the ability to recover from obstacles still need playtesting.

**How it helps education:** It supports observing moving organisms within their environment. It is a simplified gameplay system, not proof of scientifically accurate animal behavior.

**A useful distinction:** `SpeciesAI` decides what the creature is doing: wandering, fleeing, or returning. Context Steering decides how to move toward the selected goal while considering nearby obstacles. Animation and locomotion control how that movement looks.

**Practical limit:** Context Steering is local navigation. It does not guarantee the shortest route or successful escape from every complicated enclosure.

**Defense answer:**

> We use Context Steering so marine organisms can adjust their movement to the generated environment. It evaluates movement directions using interest and danger, then favors useful directions with clearance. This supports roaming and obstacle avoidance without manually defining every route.

## 5. How the systems support the five objectives

The objective list is on page 13 of the supplied PDF.

| Objective | Supporting implementation | What to demonstrate or evaluate |
| --- | --- | --- |
| 1. Bestiary with factual descriptions and 3D models | Authored species records, models, discovery, and Bestiary screens | A successful scan unlocks the correct entry; the model and text identify the same organism; facts have traceable sources. |
| 2. Dynamic environment across five zones | Perlin Noise terrain and rule-based placement | Different seeds produce varied terrain; configured zone and habitat rules produce appropriate, accessible placements. |
| 3. Progression, RDP, upgrades, and zone unlocking | Reward, purchase, discovery, and unlock logic | A discovery provides the intended reward; purchases update correctly; zone requirements are enforced and progress persists. |
| 4. Autonomous movement and obstacle avoidance | Context Steering, species behavior, and locomotion | A moving organism responds to obstacles, stays within intended boundaries, and recovers from difficult situations. |
| 5. Biodiversity awareness and understanding | Discovery, habitat associations, Bestiary content, and learning activities | Content review and user feedback assess clarity and relevance; an appropriate knowledge assessment is needed for stronger claims about measured learning gains. |

Algorithms provide opportunities for learning. They do not, on their own, prove that players learned or enjoyed the game.

The intended gameplay relationship is: **explore -> discover -> scan -> unlock information and earn RDP -> improve the submarine -> explore deeper zones.** Your demonstration should show how players encounter meaningful information while progressing.

## 6. What the asset team should explain

Assets are the content these systems organize and display:

- Species models help the player recognize and compare organisms.
- Habitat models make environmental differences visible.
- Appropriate model size, origin, and facing direction help the movement and placement code work correctly.
- Readable UI and Bestiary layouts make the information usable.
- Model and texture complexity affect the cost of rendering the generated world.

An asset teammate can say:

> Our assets make the organisms and habitats visible and recognizable. The procedural systems build and organize the environment, and Context Steering moves mobile organisms through it. The models, researched descriptions, interface, and algorithms work together to support discovery and learning.

## 7. Common defense questions

**Why not place everything manually?**

Manual placement is a valid option for a fixed map. Our chosen design calls for generated terrain and varied layouts, so programmed placement helps populate those layouts consistently. Handcrafted assets and special placement rules still play an important role.

**Why not use unrestricted randomness?**

Variation alone does not check habitat suitability, spacing, or accessibility. Our procedures combine variation with rules and limits.

**Are these algorithms new?**

They are established techniques. Our work applies and integrates them in an offline Android game with marine content, discovery, and progression, and evaluates how the combined system meets our objectives. Do not claim to have invented these algorithms.

**Does Context Steering use machine learning?**

The inspected implementation uses programmed behavior, numerical scores, and physics queries. It does not train a machine-learning model.

**Why choose these techniques instead of alternatives?**

Tie the answer to each requirement: continuous terrain patterns, explicit control of placement conditions, and responsive local movement. An alternative might also work. Claims that our approach is faster or better than an alternative require a controlled comparison.

**How will we show that the algorithms work?**

Use the Chapter 3 procedure: configure, run, observe, record, and compare. Change one parameter at a time while controlling relevant conditions. Check both the numerical result and whether the generated terrain or behavior remains suitable for gameplay.

**How do we show the game is fun?**

Observe whether players can navigate and understand tasks, whether scanning and rewards motivate continued exploration, and where frustration occurs. Collect direct feedback about enjoyment and challenge. A technically correct algorithm is not an enjoyment measurement.

**How do we show the game is educational?**

Validate the facts and habitat associations, and evaluate whether players understand the content. The thesis questionnaire measures reported experience and understanding. A pre/post knowledge assessment could strengthen a learning claim if agreed as part of the research design; it is not an experiment already established by this guide.

## 8. Details to align before presenting

These are preparation notes, not changes made to your thesis or game.

1. **Map variation:** The thesis describes variation across sessions, but the current code retains a seed per zone within a save. Describe variation across different seeds or new games, with stable terrain on revisiting a saved zone.
2. **Probe count:** The current `rayCount = 12` means 12 horizontal directions. `BuildRayDirections` adds 18 gentle diagonal directions, 8 steep diagonal directions, and 2 vertical directions: 40 total at that setting. Label the tested parameter accurately and keep the code version used for testing.
3. **Settings:** The thesis lists turn speed 3.5, maximum placement attempts 60, and minimum separation 5 m. Current steering has a base turn speed of 65 degrees per second, with species and scale adjustments. The spawner's script defaults are 70 attempts and 3.5 m, while the inspected zone scenes serialize 30 attempts and 8 m; specialized placement paths use additional values. Record the actual settings used for the defended build, rather than assuming the thesis table or script default is the runtime value.
4. **Performance claims:** Navigation completion time is the time to finish a movement task. CPU processing time is the cost of running the algorithm. A shorter navigation task does not establish lower CPU cost. Table 6's qualitative rankings also should not be presented as measured benchmark results without supporting measurements.
5. **Preliminary results:** The PDF already contains trial results on pages 100-105 while other passages describe future testing. Explain which experiments have actually been completed, their device and conditions, and which evaluations remain planned. This guide does not independently validate those reported trials.
6. **Species totals:** Page 17 states 95 species distributed as 15, 17, 19, 21, and 23, matching `ZoneConfig` totals. Pages 23-24 instead describe 15-21 species per zone and fewer in deeper zones. Align these statements with the final scope and actual species inventory; configuration totals alone do not verify that every model and record is complete.
7. **Placement guarantees:** Candidate scoring and fallback behavior mean generated placements need validation. Use 'applies ecological placement rules' unless you have verified the stronger claim that all constraints always hold.
8. **Learning and progression:** Current code includes research verification checkpoints linked to discovered Bestiary records before first entry into deeper zones. These are not detailed in the supplied thesis's progression explanation. If included in your demonstration, align the manuscript and presentation with that feature. Completing an in-game review is not, by itself, research evidence of long-term learning improvement.

For performance and parameter experiments, keep the seed, selected zone, device, build, mesh resolution, creature population, and obstacle scenario consistent when relevant. Capture logs and observations so your explanation can be checked.

## 9. A short group presentation script

> Hadal Descent combines three main techniques. First, Perlin Noise generates varied seafloor shapes, reducing the need to manually build every terrain layout. Second, rule-based placement organizes species and environmental objects using configured conditions such as zone, depth, habitat, and spacing. Third, Context Steering evaluates movement directions using interest and danger so marine organisms can respond to nearby obstacles in the generated world. These systems support exploration and species discovery. Scanning, factual Bestiary entries, rewards, and progression connect that exploration to marine biodiversity content. We evaluate the technical behavior of the systems separately from the player's gameplay experience and understanding of the educational content.

## Source map for the programmer

- Supplied thesis: [Chapters 1-3](<C:/Users/ashle/Downloads/Hadal Descent chapters 1-3.pdf>). Relevant PDF pages: 8-13 (purpose and objectives); 22-24 (scope); 95-105 (algorithms and sensitivity tests); 106-118 (testing and evaluation).
- [TerrainGenerator.cs](C:/Unity/HadalDescent/Assets/Scripts/Managers/TerrainGenerator.cs): `Generate`, lines 135-218, coordinates the generation pipeline.
- [OceanFloorMeshGenerator.cs](C:/Unity/HadalDescent/Assets/Scripts/Managers/OceanFloorMeshGenerator.cs): `BuildMesh`, line 166; `ComputeHeight`, line 247; `SmoothNoise`, line 341.
- [SpeciesSpawner.cs](C:/Unity/HadalDescent/Assets/Scripts/Managers/SpeciesSpawner.cs): `SpawnForZone`, line 118; placement searches from line 339; fallback behavior from line 637; `EvaluateCandidate`, line 801.
- [ContextSteering.cs](C:/Unity/HadalDescent/Assets/Scripts/Creatures/ContextSteering.cs): parameter defaults, lines 20-34; `BuildRayDirections`, line 433; interest logic, line 469; danger logic around lines 603-665; direction scoring, line 737.
- [SpeciesAI.cs](C:/Unity/HadalDescent/Assets/Scripts/Creatures/SpeciesAI.cs): states, line 22; initialization and turning settings, lines 78-109; flee/return behavior, lines 270-295.
- [GameManager.cs](C:/Unity/HadalDescent/Assets/Scripts/Managers/GameManager.cs): unlock conditions, line 247; persistent seed management, line 324.
- [ZoneConfig.cs](C:/Unity/HadalDescent/Assets/Scripts/Managers/ZoneConfig.cs): zone definitions, line 63.
- [ZoneManager.cs](C:/Unity/HadalDescent/Assets/Scripts/Managers/ZoneManager.cs): research review before zone entry, lines 173-178.
- [ResearchVerificationMinigame.cs](C:/Unity/HadalDescent/Assets/Scripts/Minigames/ResearchVerificationMinigame.cs) and [ResearchQuestionSelector.cs](C:/Unity/HadalDescent/Assets/Scripts/Minigames/ResearchQuestionSelector.cs): saved review and selection from discovered species.
- Serialized placement settings inspected in [SunlightZone.unity](C:/Unity/HadalDescent/Assets/Scenes/SunlightZone.unity), [TwilightZone.unity](C:/Unity/HadalDescent/Assets/Scenes/TwilightZone.unity), [MidnightZone.unity](C:/Unity/HadalDescent/Assets/Scenes/MidnightZone.unity), [AbyssZone.unity](C:/Unity/HadalDescent/Assets/Scenes/AbyssZone.unity), and [HadalZone.unity](C:/Unity/HadalDescent/Assets/Scenes/HadalZone.unity).
