# Hadal Descent: a simple technical guide for the whole group

Prepared on 20 October 2026 from the supplied Chapters 1-3 and the current Unity source. This explains the implementation; it does not report new gameplay tests, benchmarks, or learning results. Existing game files were not changed.

## The explanation everyone should remember

An **algorithm** is a set of steps for solving a problem.

In Hadal Descent, remember three jobs:

| Technique | Its job | Why this game uses it |
| --- | --- | --- |
| Perlin Noise | Build the seafloor's shape. | Generate varied terrain from settings instead of manually creating every layout. |
| Rule-based placement | Organize what appears in the world. | Apply conditions for habitats, depth, spacing, and usable locations. |
| Context Steering | Guide moving organisms. | React to obstacles in the environment that was generated. |

The sequence is **build the world -> populate it -> move creatures through it -> let the player explore and learn**.

**PCG**, or Procedural Content Generation, is the broader approach of generating content through programmed procedures. In this project, Perlin Noise and placement rules are parts of that approach.

These techniques are needed for the team's chosen procedural design. Other designs could use handcrafted maps or different navigation methods. Defend how your choices fit your requirements.

## 1. Perlin Noise: the landscape builder

**Imagine a flexible sheet.** Raising and lowering different points creates hills and basins. Perlin Noise provides smoothly changing values that help determine each point's height.

### How the game implements it

1. `TerrainGenerator` reads the selected zone's dimensions, frequency, amplitude, and seed.
2. `OceanFloorMeshGenerator` creates a grid of points. The script's default resolution is 96 by 96.
3. It samples Unity's `Mathf.PerlinNoise` at positions across that grid. Nearby samples vary smoothly instead of changing independently.
4. It combines several noise patterns and additional shaping to form broad basins, shelves, mounds, and small variations.
5. It applies height limits and edge shaping, then connects the points with triangles to create the visible 3D seafloor.
6. It stores heights so placement and movement systems can query the surface.

The team implemented the terrain-generation procedure around Unity's existing noise function. Do not say you invented Perlin Noise or coded its underlying mathematical implementation from scratch.

### Three settings to understand

- **Frequency:** changes the scale of the features. Lower values generally give broader formations; higher values give more closely spaced changes over the same area.
- **Amplitude:** changes the amount of vertical variation. Final heights also depend on the shaping and limits.
- **Seed:** selects a reproducible pattern. The same seed and terrain settings can recreate the terrain.

**Why use it?** Your design needs varied underwater environments across five zones. Perlin Noise provides a controllable starting point for continuous seafloor shapes and reduces the need to manually create every layout.

**What it contributes:** Terrain variety can encourage exploration. It does not supply marine facts or determine whether players enjoy the map; content, placement, visibility, and playtesting still matter.

**Defense answer:**

> We use Perlin Noise to generate varied seafloor shapes from configurable settings. We combine its smooth patterns with terrain shaping and limits so the generated surface fits our underwater exploration design.

## 2. Rule-based placement: the habitat organizer

**Imagine arranging a museum.** Exhibits belong in suitable sections, with enough room around them. Placement rules give the generated world a similar organization.

### How the game implements it

1. The species spawner gets the species configured for the selected zone.
2. It reads each species' data, including preferred habitat, depth range, and movement characteristics.
3. It proposes a location and checks relevant conditions, such as habitat, depth, ground height, slope, clearance, and spacing.
4. It accepts a suitable location or searches elsewhere. The implementation also scores candidates and uses additional searches and fallbacks.
5. Other placement systems arrange environmental props, landmarks, and debris using their own conditions.

A **biome** here means a configured habitat category, such as soft seabed, hard reef, rock, or open water.

**Actual code example:** The spawner places anemones before clownfish. When anemone positions are available, it places clownfish nearby and sets the host position as their wandering center. This is a specific authored relationship, rather than unrestricted random scattering.

**Why use it?** A varied map alone cannot organize marine life meaningfully. Placement rules connect the generated terrain to the team's habitat data and help keep organisms visible and accessible.

**What it contributes:** Meaningful distribution supports discovery and habitat associations. The rules only reflect the data and conditions the developers provide; they do not automatically discover ecological truth. Some fallback paths relax preferred conditions, so final placements need validation.

**Defense answer:**

> We use rule-based placement to organize species and environmental objects according to configured conditions such as zone, depth, habitat, and spacing. This gives our procedural world meaningful structure and supports the habitat information presented to players.

## 3. Context Steering: the movement guide

**Imagine walking toward a destination while checking for obstacles.** The destination tells you where you want to go; nearby obstacles influence your next step.

### How the game implements it

1. `SpeciesAI` chooses a behavior and target, such as wandering, fleeing, or returning.
2. `ContextSteering` evaluates possible directions around the organism, including vertical and diagonal directions.
3. An **interest value** represents how useful each direction is for movement toward the target, with additional movement and boundary adjustments.
4. A **danger value** represents nearby obstacle and boundary risks. Spherecasts act like invisible probes with thickness to check clearance.
5. It scores directions, filters high-danger ones, and blends useful safe directions. It also has fallback and escape behavior.
6. Turning, smoothing, and locomotion apply the resulting direction. The process repeats as the organism moves.

**Important distinction:** The behavior system chooses what the creature is doing. Context Steering helps choose its next movement direction. Locomotion and animation determine how that movement looks.

### The formula in simple language

The thesis and code use this directional score:

**S = I x (1 - D)^2**

- **I** = interest, from 0 to 1.
- **D** = danger, from 0 to 1.
- **S** = resulting score.

With interest 0.8 and danger 0.2, the score is 0.512. With the same interest but danger 0.7, the score falls to 0.072. A direction becomes much less attractive as danger increases. The code also filters and blends directions, so the formula is one part of the decision.

**Example:** A fish wants to move forward, but a rock is ahead. The forward direction receives danger. An open direction to the side can contribute more strongly, allowing the fish to turn around the rock.

**Why use it?** The terrain is generated, so organisms need to respond to the actual surroundings. Local steering supports this without manually creating a fixed route for every possible terrain layout.

**What it contributes:** Moving organisms can make exploration and scanning more engaging. This is simplified game behavior, not a complete biological simulation. Local steering also does not guarantee the shortest route or escape from every enclosure.

**Defense answer:**

> We use Context Steering so moving organisms can respond to the generated environment. It evaluates directions using interest and danger, then favors useful directions with clearance. This supports autonomous movement and obstacle avoidance.

## How these systems support the objectives

Your five specific objectives appear on PDF page 13.

| Objective | How the implementation supports it | Evidence to prepare |
| --- | --- | --- |
| 1. Bestiary with factual descriptions and 3D models | Scanning and discovery connect species models to their information. | Demonstrate a successful scan and the correct unlocked entry; trace the factual sources. |
| 2. Dynamic environment across five depth zones | Perlin Noise builds terrain; placement rules populate it. | Show controlled terrain variation and inspect species placements across zones and seeds. |
| 3. Progression, RDP, upgrades, and zone unlocking | Discovery, rewards, purchasing, and unlock checks organize progression. | Demonstrate reward calculation, upgrade effects, unlock requirements, and saved progress. |
| 4. Autonomous movement and obstacle avoidance | Behavior, Context Steering, and locomotion work together. | Show roaming, obstacle response, boundaries, and recovery in a controlled scenario. |
| 5. Biodiversity awareness and understanding | Encounters, habitat associations, Bestiary content, and learning activities expose players to marine information. | Review content accuracy and evaluate player understanding using the approved research design. |

The three main algorithms directly address Objectives 2 and 4. Bestiary content and progression systems are also needed to achieve the other objectives.

## Connecting fun with learning

The intended loop is:

**Explore -> encounter a species -> complete a scan activity -> unlock information and earn RDP -> upgrade and meet progression requirements -> explore deeper.**

Each part has a purpose:

- Terrain and creatures give the player something to explore and observe.
- Scanning gives the encounter an activity and a goal.
- The Bestiary connects the discovery to species information.
- Rewards and upgrades give players reasons to continue exploring.

This explains the design intention. To show it works, evaluate the technical systems, enjoyment, and understanding separately. Working code does not prove learning, and a high usability rating does not automatically prove enjoyment.

Your Chapter 3 questionnaires ask about functionality, interaction, reliability, and educational content. They can capture reported experience and understanding. A claim about measured knowledge improvement needs an appropriate knowledge assessment; a pre/post assessment would be a possible research-design addition if approved by your adviser.

## What asset teammates should understand and say

The asset team supplies the visible content the algorithms organize and move. Models support recognition; habitats give context; readable UI makes information usable. Model scale, origin, and facing direction also affect placement and movement. Asset complexity affects rendering performance.

> Our models make marine organisms and habitats visible and recognizable. Perlin Noise builds the seafloor, placement rules organize the content, and Context Steering guides mobile organisms around obstacles. The assets, researched information, and algorithms work together to support exploration and learning.

Every teammate should be able to explain each technique's problem, input, basic steps, output, and limitation. Memorizing code syntax is less useful than understanding those five things.

## Short answers for likely defense questions

**Why not build the map manually?**

Manual maps are valid. Our chosen design includes varied generated terrain, so procedures reduce the work of authoring every layout. The models, rules, and terrain shaping still require design work.

**Why not use unrestricted randomness?**

Randomness supplies variation. Rules supply conditions for meaningful and usable placement. We need both for this design.

**Why not use fixed paths for creatures?**

Fixed paths require routes that fit the environment. Local steering responds to the obstacles in the generated layout. Other navigation approaches could also work; superiority would require comparison.

**Is this machine learning? Did you invent these algorithms?**

The inspected implementation uses established procedures, authored rules, numerical scores, and physics queries. It does not train a machine-learning model. The project's contribution is their application and integration with marine content and gameplay, evaluated against the objectives.

**How will you test them?**

Follow Chapter 3's configure, run, observe, record, and compare process. Change one parameter at a time while keeping relevant conditions fixed. Record terrain generation time, navigation completion time, placement validity, and observed behavior. Measure CPU cost separately if making processing-performance claims.

**What are the trade-offs?**

Terrain generation needs mesh data and processing. Restrictive placement rules can make suitable locations harder to find. More steering probes create more queries to evaluate. Actual device cost needs measurement; do not claim automatic memory savings or that one technique is fastest based only on its name.

## Details to align before the defense

These are source-review findings, not changes made to your game or manuscript:

1. **Terrain variation:** The current game keeps a terrain seed per zone in the save. Different seeds can give different terrain, while revisiting a saved zone uses its retained terrain pattern. Do not promise a new terrain layout on every revisit.
2. **Steering directions:** The default `rayCount = 12` specifies horizontal directions. `BuildRayDirections` adds 18 gentle diagonals, 8 steep diagonals, and 2 vertical directions: 40 total at that setting. Describe the tested parameter accurately.
3. **Actual settings:** PDF pages 103 and 108 list turn speed 3.5; the current script's base default is 65 degrees per second, before adjustments. PDF page 107 lists 60 placement attempts and 5 m separation. Current source and scene settings differ, and the main random search uses at least 90 attempts. Record the actual defended build's settings, units, overrides, and search stage.
4. **Performance interpretation:** Shorter navigation completion time means finishing a movement task sooner. It does not demonstrate lower CPU processing cost. The manuscript's qualitative time and memory rankings need measurements if presented as benchmark findings.
5. **Research status:** The PDF includes trial tables on pages 100-105 and future-tense testing descriptions elsewhere. Clearly identify completed trials, their conditions and build version, and evaluations still planned.
6. **Current progression:** The code includes research review checkpoints before first entry into deeper zones. If shown in the demo, explain them consistently in the presentation and manuscript. Passing an in-game review alone does not establish long-term learning gains.

## One-minute group script

> Hadal Descent uses three main techniques that work together. Perlin Noise generates varied seafloor shapes. Rule-based placement organizes species and environmental objects using conditions such as zone, depth, habitat, and spacing. Context Steering helps moving organisms react to obstacles by evaluating useful and dangerous directions. These systems support exploration and discovery. Scanning, factual Bestiary entries, rewards, and progression connect exploration to marine biodiversity content. Our evaluation checks whether the systems work, whether players can use and enjoy the game, and whether the educational content supports understanding. The algorithms provide the structure, while our assets and researched content give that structure meaning.

## Sources checked

- [Supplied Chapters 1-3](<C:/Users/ashle/Downloads/Hadal Descent chapters 1-3.pdf>): page 13 for objectives; pages 95-111 for algorithms, experiments, and parameters; pages 112-118 for evaluation.
- [Terrain orchestration](C:/Unity/HadalDescent/Assets/Scripts/Managers/TerrainGenerator.cs:139), [terrain mesh and height generation](C:/Unity/HadalDescent/Assets/Scripts/Managers/OceanFloorMeshGenerator.cs:166), and [saved terrain seeds](C:/Unity/HadalDescent/Assets/Scripts/Managers/GameManager.cs:322).
- [Species placement](C:/Unity/HadalDescent/Assets/Scripts/Managers/SpeciesSpawner.cs:339) and [candidate scoring](C:/Unity/HadalDescent/Assets/Scripts/Managers/SpeciesSpawner.cs:801).
- [Direction construction](C:/Unity/HadalDescent/Assets/Scripts/Creatures/ContextSteering.cs:433), [interest](C:/Unity/HadalDescent/Assets/Scripts/Creatures/ContextSteering.cs:469), [danger](C:/Unity/HadalDescent/Assets/Scripts/Creatures/ContextSteering.cs:543), and [direction scoring and blending](C:/Unity/HadalDescent/Assets/Scripts/Creatures/ContextSteering.cs:737).
- [Species behavior states](C:/Unity/HadalDescent/Assets/Scripts/Creatures/SpeciesAI.cs:22), [scan discovery and rewards](C:/Unity/HadalDescent/Assets/Scripts/Player/ScannerSystem.cs:665), and [research review before zone entry](C:/Unity/HadalDescent/Assets/Scripts/Managers/ZoneManager.cs:172).
