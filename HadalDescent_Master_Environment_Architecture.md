# Hadal Descent — Master Environment & Procedural Prop Architecture

**Document Classification:** Thesis Technical Architecture & Asset Production Blueprint  
**Target Platform:** Unity (Universal Render Pipeline / Android Mobile)  
**Target Scope:** 5 Ocean Depth Zones, 95 Philippine Sea / Deep-Sea Species, 6-Month Development Timeline  
**Key Methodologies:** Perlin Noise Procedural Content Generation (PCG), BiomeBand Rule-Based Distribution, Low-Poly Modular Asset Kitbashing, Context Steering Obstacle Tagging.

---

## Executive Summary & Design Philosophy

In *Hadal Descent*, marine literacy is achieved through exploration and species discovery. To accurately represent marine biology without manually designing 95 separate diorama levels, the game couples **Perlin Noise Terrain Generation** with **Ecological Biome Clustering**.

By establishing 5 standardized **Biome Bands** across every ocean depth level:
1. **Open Water** (Pelagic zone — active swimmers, siphonophores, squids)
2. **Soft Biome** (Sediment, sand ripples, silty mud plains, biogenic ooze)
3. **Hard Biome** (Biogenic structures: corals, sponge gardens, sea lily anchors, nodule beds)
4. **Rock Biome** (Geological structures: continental shelves, boulder crevices, tectonic fault scarps)
5. **Special Biome** (Chemosynthetic oases: hydrothermal vents, cold seeps, sunken organic drift)

Both environment props and marine organisms query the exact same Perlin biome coordinate. This guarantees that stationary and demersal organisms (such as Bubble-tip Sea Anemones, Scaly-foot Snails, Tripod Fish, and Hadal Sea Pigs) naturally spawn directly on and around their corresponding environmental props.

```
                  ┌──────────────────────────────────────────────┐
                  │          Procedural Height & Biome           │
                  │         Perlin Noise Evaluator (PCG)         │
                  └──────────────────────┬───────────────────────┘
                                         │
                 ┌───────────────────────┴───────────────────────┐
                 ▼                                               ▼
   ┌───────────────────────────┐                   ┌───────────────────────────┐
   │    EnvPropScatterer.cs    │                   │     SpeciesSpawner.cs     │
   │  Scatters Biome-Specific  │                   │  Spawns Creatures In Their│
   │   Props & Geological Form │                   │    Preferred BiomeBand    │
   └─────────────┬─────────────┘                   └─────────────┬─────────────┘
                 │                                               │
                 └───────────────────────┬───────────────────────┘
                                         ▼
                 ┌───────────────────────────────────────────────┐
                 │       Co-Located Ecological Micro-Habitat     │
                 │  • Anemones + Clownfish on Coral Reef Mounds  │
                 │  • Scaly-foot Snails on Hydrothermal Smokers  │
                 │  • Tripod Fish on Silty Abyssal Mud Basins   │
                 │  • Sea Pigs on Hadal Trench Axis Silt Beds    │
                 └───────────────────────────────────────────────┘
```

---

## 1. Repurposing Existing Low-Poly Rocks Across All Zones & Biomes

The project currently contains **30 FBX rock meshes (and 60 prefabs)** located in:  
[`Assets/Prefabs/Environment/Rocks/`](file:///c:/Unity/HadalDescent/Assets/Prefabs/Environment/Rocks/)  
*(Cone, Cube, Cylinder, Icosphere, Sphere, bisect)*

Because 3D models in an underwater game are viewed through depth-dependent atmospheric fog, custom lighting, and variable water clarity, geometry is perceived primarily by its **silhouette** and **surface tint**. You do **NOT** need new rock models for every zone. You can reuse your existing rock pack to represent 60% of all geological and structural features across the entire ocean column.

### The Multi-Zone Rock Transformation Matrix

| Existing Rock Mesh | Zone & Biome Applied | Transform / Scale Formula | Material / Color Recipe | What the Player Sees |
| :--- | :--- | :--- | :--- | :--- |
| **Cone / Cone_001 / Cone_002** | Sunlight (Hard) | `Scale(1.0, 1.2, 1.0)` | Sun-bleached limestone (`#7B8C78`) | Coral reef pinnacle / limestone spire |
| **Cone / Cone_001 / Cone_002** | Midnight (Special) | `Scale(0.8, 3.5, 0.8)` *(Tall)* | Pyrite/sulfide black (`#1A1715`) + Orange emission | Hydrothermal black smoker chimney |
| **Cone / Cone_001 / Cone_002** | Hadal (Rock) | `Scale(1.5, 2.8, 1.2)` + $15^\circ$ tilt | Deep fracture slate (`#141618`) | Jagged tectonic fault pinnacle |
| **Icosphere / 001–007** | Sunlight (Rock) | Uniform `Scale(1.5, 1.5, 1.5)` | Weathered sand-speckled granite (`#6C7475`) | Coastal reef boulder |
| **Icosphere / 001–007** | Midnight (Hard) | Cluster of 4–6, `Scale(0.8, 0.5, 0.8)` *(Squat)* | Volcanic obsidian black (`#1E1F22`, Smoothness 0.3) | Pillow Basalt lava flow |
| **Icosphere / 001–007** | Abyss (Hard) | Dense clusters, `Scale(0.2, 0.15, 0.2)` *(Tiny)* | Dark manganese brown-black (`#181513`, Matte) | Manganese Nodule field |
| **Cube / 001–010 & bisect** | Sunlight (Hard) | `Scale(3.0, 0.4, 3.0)` *(Flat)* | Algae-stained limestone (`#5D6E58`) | Shallow reef flat / plateau ledge |
| **Cube / 001–010 & bisect** | Twilight (Rock) | `Scale(4.0, 0.6, 2.5)` | Mesopelagic silted shelf (`#364047`) | Continental shelf drop-off step |
| **Cube / 001–010 & bisect** | Hadal (Rock) | `Scale(3.5, 8.0, 1.5)` *(Sheer)* | Ultra-dark chert/basalt (`#0F1113`) | Sheer Subduction Fault Scarp Wall |
| **Sphere / 001–005** | Twilight (Soft) | `Scale(2.5, 0.4, 2.5)` | Soft greenish-gray silt (`#454D48`) | Deep sediment mound / burrow hillock |
| **Sphere / 001–005** | Abyss (Soft) | `Scale(3.5, 0.25, 3.5)` | Pale biogenic pelagic ooze (`#484742`) | Abyssal plain sediment swell |
| **Cylinder / 001** | Midnight (Special) | `Scale(0.6, 2.5, 0.6)` | Rusty sulfide crust (`#2D2318`) | Hydrothermal venting conduit pipe |
| **Cylinder / 001** | Hadal (Hard) | `Scale(1.2, 0.3, 1.2)` | Consolidated trench bedrock (`#18191B`) | Tectonic bedrock anchor platform |

> [!TIP]
> **Automatic Seabed Alignment**: In [EnvPropScatterer.cs](file:///c:/Unity/HadalDescent/Assets/Scripts/Managers/EnvPropScatterer.cs), the `OrientRockFlat()` algorithm automatically detects the shortest axis (thickness) of each rock mesh and aligns it to the seabed normal, ensuring the longer width always lays flat against the sea floor.

---

## 2. Current Assets in the Project (Status & Inventory)

Below is the exact inventory of assets currently built, textured, and integrated in the Unity project:

### A. Environment Props & Biological Models
| Asset Name | Type / File Format | Location in Project | Biome Assigned | Status |
| :--- | :--- | :--- | :--- | :--- |
| **prop_sun_seagrass_patch** | 3D Model (`.glb` / Prefab) | `Assets/Prefabs/Environment/Sunlight Zone/` | Soft Biome (Sand Flats) | **Integrated & Active** |
| **prop_sun_buried_clam_shell** | 3D Model (`.glb` / Prefab) | `Assets/Prefabs/Environment/Sunlight Zone/` | Soft Biome (Sand Flats) | **Integrated & Active** |
| **prop_sun_sand_ripple** | Procedural Mesh / Prefab | `Assets/Prefabs/Environment/Sunlight Zone/` | Soft Biome (Sand Flats) | **Integrated & Active** |
| **Orange Coral (Acropora)** | 3D Model (`.glb` / Prefab) | `Assets/Prefabs/Environment/Sunlight Zone/` | Hard Biome (Coral Reef) | **Integrated & Active** |
| **Brain Coral (Kelli Ray)** | 3D Model (`.glb` / Prefab) | `Assets/Prefabs/Environment/Sunlight Zone/` | Hard Biome (Coral Reef) | **Integrated & Active** |
| **prop_sun_coral_fan** | 3D Model (`.glb` / Prefab) | `Assets/Prefabs/Environment/Sunlight Zone/` | Hard Biome (Coral Reef) | **Integrated & Active** |
| **Reusable Rocks Pack (30 meshes)** | 60 Prefabs (`.prefab`) | `Assets/Prefabs/Environment/Rocks/` | All Biomes (Multi-Zone) | **Integrated & Active** |

### B. Atmosphere & Visual Effects
| VFX Component | Description | Zone Target | Status |
| :--- | :--- | :--- | :--- |
| **SunlightAtmosphereVFX** | Procedural oscillating godray light shafts + plankton dust particle simulation | Zone 0 (Sunlight) strictly | **Active (Guarded against Twilight)** |
| **Zone Linear Fog** | Atmospheric color gradient, fog distances & density per zone (`ZoneConfig.cs`) | All 5 Zones | **Configured & Active** |
| **Procedural Ocean Floor** | Dynamic 96x96 vertex fBm noise mesh with stepped reef plateaus (`OceanFloorMeshGenerator.cs`) | All 5 Zones | **Active & Enabled** |

### C. ScriptableObject Configurations
| Asset Name | Config Type | Path | Status |
| :--- | :--- | :--- | :--- |
| **SunlightProps.asset** | `EnvPropSet` | `Assets/Scripts/ScriptableObjects/Environment/` | Complete with biomes, scales & weights |
| **TwilightProps.asset** | `EnvPropSet` | `Assets/Scripts/ScriptableObjects/Environment/` | Configured with rock kitbashing matrix |
| **SpeciesRegistry.asset** | `SpeciesRegistry` | `Assets/Resources/` | Contains all 5-zone species definitions |

---

## 3. Future Assets to be Made (Targeted 5-Zone Production Blueprint)

To complement your reusable rock starter pack, here is the curated production checklist of biological and specialized assets to model in **Blender**.

### Zone 0: Sunlight Zone (0–200 m)
*Primary Physical Conditions: High illumination, photosynthesis, warm temperature, high carbonate reef development.*
- **Current Coverage:** Complete. Seagrass, clam shells, sand ripples, staghorn coral, brain coral, fan coral, and boulders are active.
- **Future Enhancements (Kitbash / Texture Overrides):**
  1. `prop_sun_rock_crevice`: Modular overhang archway kitbashed from two `Cube_004` rocks for lobsters and sea kraits.
  2. `prop_sun_algae_boulder`: Existing `Icosphere_001` mapped with an olive-green algae rim material.

---

### Zone 1: Twilight Zone (200–1,000 m) — 17 Species
*Primary Physical Conditions: Dysphotic (rapidly attenuating light), 4–12°C, continental slope ledges, sponge & cold-water coral dominance.*

#### 1. Soft Biome (Deep Silty Terraces)
- **Species Hosted:** Albatross Coffinfish (*Chaunax albatrossae*), Ring Triangular Batfish (*Malthopsis annulifera*), Deep-water Stingray (*Plesiobatis daviesi*), Heart Urchin (*Echinocardium cordatum*), Volsellate Crown Star (*Coronaster volsellatus*).
- **Future Assets to Model:**
  - `prop_twi_silt_mound`: Low, gentle mud swell with 2–3 dark organism burrow holes (~250 tris).
  - `prop_twi_marine_snow_drift`: Soft biogenic sediment ribbon accumulation (~180 tris).

#### 2. Hard Biome (Cold-Water Coral Gardens & Sponge Grounds)
- **Species Hosted:** Deep-water Coral (*Dendrophyllia arbuscula*), Deep-water Sponge (*Corallistes masoni*), Mino Nylon Shrimp (*Heterocarpus sibogae*).
- **Future Assets to Model:**
  - `prop_twi_coldcoral_bush`: Bleached white/pale yellowish branching deep-sea coral tree (Lophelia style, ~500 tris).
  - `prop_twi_glass_vase_sponge`: Upright chalice/goblet glass sponge with a translucent silica mesh look (~300 tris).
  - `prop_twi_sponge_cluster`: Trio of small tubular cup sponges on a stony base (~320 tris).

#### 3. Rock Biome (Continental Slope Drop-Offs & Basalt Cliffs)
- **Species Hosted:** Philippine Deep-water Clam (*Acesta philippinensis*), Silver Chimaera (*Chimaera phantasma*), Shorttail Lanternshark, Philippine Spurdog.
- **Future Assets to Model / Kitbash:**
  - `prop_twi_shelf_ledge`: Wide horizontal rock shelf slab (reusing `Cube_004` scaled flat) for Acesta clams to anchor.
  - `prop_twi_angular_cliff_block`: Stepped boulder block with vertical fracture joints (~300 tris).

---

### Zone 2: Midnight Zone (1,000–4,000 m) — 19 Species
*Primary Physical Conditions: Aphotic (100% blackness), immense pressure, bathyal plains, hydrothermal volcanic belts.*

#### 1. Special Biome (Hydrothermal Vent Fields / Black Smokers)
- **Species Hosted:** Scaly-foot Snail (*Chrysomallon squamiferum*), Deep-sea Squat Lobster (*Munidopsis lauensis*), Deep-water Clam (*Elliptiolucina labeyriei*).
- **Future Assets to Model:**
  - `prop_mid_smoker_chimney`: Tall craggy mineral sulfide spire (reusing `Cone_001` scaled tall) with smoke particle emitter (~450 tris).
  - `prop_mid_smoker_cluster`: Clump of 3 low venting mineral cones encrusted with golden-yellow sulfur crystals (~500 tris).
  - `prop_mid_sulfide_crust`: Broad, flat iron-sulfide mineral slab resting on the seabed floor (~220 tris).

#### 2. Hard Biome (Volcanic Pillow Basalt & Carnivorous Sponge Fields)
- **Species Hosted:** Harp Sponge (*Chondrocladia lyra*), Tribute Spiderfish (*Bathypterois guentheri*), Rudis Rattail (*Coryphaenoides rudis*).
- **Future Assets to Model:**
  - `prop_mid_harp_sponge_prop`: Stylized harp sponge frame (vertical vanes with terminal spheres, ~400 tris).
  - `prop_mid_pillow_basalt_mound`: Cluster of 5–8 smooth rounded volcanic lumps (reusing `Icosphere_003`, ~350 tris).

#### 3. Rock Biome (Seamount Slopes & Trench Drop-Offs)
- **Species Hosted:** Portuguese Dogfish (*Centroscymnus coelolepis*), Philippine Spurdog, Blackbelly Lanternshark.
- **Future Assets to Model / Kitbash:**
  - `prop_mid_volcanic_spine`: Sharp, vertical volcanic basalt needle (~280 tris).
  - `prop_mid_collapsed_column`: Fallen basalt hexagonal pillar lying horizontal (~220 tris).

---

### Zone 3: Abyssal Zone (4,000–6,000 m) — 21 Species
*Primary Physical Conditions: Flat abyssal plain covered in biogenic ooze, near freezing (1–2°C), polymetallic nodules, organic falls.*

#### 1. Soft Biome (Pelagic Sediment / Abyssal Ooze Plain)
- **Species Hosted:** Abyssal Spiderfish (*Bathypterois longipes*), Gummy Squirrel (*Psychropotes longicauda*), Giant Sea Spider (*Colossendeis megalonyx*), Pudgy Cusk-eel (*Spectrunculus grandis*).
- **Future Assets to Model:**
  - `prop_aby_ooze_crater`: Gentle circular depression in mud with feeding trails / worm burrows (~200 tris).
  - `prop_aby_sediment_stalk`: Slender biogenic worm tube protruding 0.5m vertically out of the mud (~120 tris).

#### 2. Hard Biome (Polymetallic / Manganese Nodule Fields)
- **Species Hosted:** Highfin Lizardfish (*Bathysaurus mollis*), Philippine Cutthroat Eel (*Ilyophis robinsae*), Arrowtooth Eel (*Histiobranchus bathybius*).
- **Future Assets to Model:**
  - `prop_aby_manganese_cluster`: Cluster of 10–20 small, dark brownish-black rounded nodules scattered on a sandy disc (~350 tris).
  - `prop_aby_glass_hexactinellid`: Solitary glass sponge with a twisted silica stalk rooted among nodules (~300 tris).

#### 3. Special Biome (Whale Fall / Deep Chemosynthetic Cold Seep)
- **Species Hosted:** Abyssal scavengers, deep grenadiers, eelpouts, chemosynthetic mats.
- **Future Assets to Model:**
  - `prop_aby_whalefall_rib`: Large curved cetacean rib bone protruding from the silt (~350 tris).
  - `prop_aby_whalefall_vertebra`: Massive bone disc half-buried in the sediment (~320 tris).
  - `prop_aby_carbonate_slab`: Authigenic methane-derived carbonate rock slab hosting pale bacterial mats (~250 tris).

---

### Zone 4: Hadal Zone (6,000–11,000+ m) — 23 Species
*Primary Physical Conditions: Subduction trenches (Philippine Trench, Mariana Trench). V-shaped canyon walls, hydrostatic pressure >1,000 atm, frequent seismic talus slides, endemic trench fauna.*

#### 1. Soft Biome (Trench Axis & Ultra-Fine Silty Drift)
- **Species Hosted:** Hadal Snailfishes (*Pseudoliparis*), Philippine Trench Sea Pig (*Scotoplanes galatheae*), Trench Sea Cucumber (*Myriotrochus longissimus*), Palau Trench Starfish (*Porcellanaster ivanovi*), Hadal Amphipods (*Hirondellea gigas*).
- **Future Assets to Model:**
  - `prop_had_trench_silt_windrow`: Elongated silt drift formed by trench gravity currents (~220 tris).
  - `prop_had_xenophyophore_clump`: Delicate frilly mud-ball agglomeration (giant deep-sea single-cell protist structure, ~250 tris).
  - `prop_had_debris_fall`: Sunken organic clump / plant detritus fallen into the trench axis (~250 tris).

#### 2. Rock Biome (Subduction Fault Scarps & Canyon Walls)
- **Species Hosted:** Galathea Cusk-eel (*Abyssobrotula galatheae*), predatory amphipods (*Princaxelia*), plated amphipods (*Lepechinella*).
- **Future Assets to Model / Kitbash:**
  - `prop_had_fault_scarp`: Monumental angular rock slab with pronounced vertical tectonic slip lines (reusing `Cube_001` scaled `(4, 12, 2)`).
  - `prop_had_overhang_ledge`: Projecting fracture ledge providing sheltered crevices (~280 tris).

#### 3. Hard Biome (Tectonic Talus Scree & Anchored Crinoids)
- **Species Hosted:** Palau Trench Sea Lily (*Bathycrinus kirilli*), Long-stemmed Sea Lily (*Bathycrinus longicolumnalis*).
- **Future Assets to Model:**
  - `prop_had_talus_rubble`: Dense pile of sharp, shattered, angular stone fragments from seismic rockslides (~300 tris).
  - `prop_had_crinoid_base_boulder`: Flat stone anchor plate designed specifically to mount *Bathycrinus* sea lilies (~200 tris).

#### 4. Special Biome (Serpentinite Fluid Seeps & Trench Fissures)
- **Species Hosted:** Deep Trench Clam (*Vesicomya sergeevi* — chemosynthetic subduction vent fauna).
- **Future Assets to Model:**
  - `prop_had_tectonic_fissure`: Narrow floor crevice with subtle geothermal particle haze (~240 tris).
  - `prop_had_serpentinite_crust`: Greenish-black slippery mineral pavement slab (~200 tris).

---

## 4. Master Asset Production Checklist

```
[X] = Already Created & Integrated in Unity
[ ] = Future Asset to Model in Blender / Kitbash

--- ZONE 0: SUNLIGHT ZONE (0–200m) ---
[X] prop_sun_seagrass_patch (Low-poly grass blades, 49KB)
[X] prop_sun_buried_clam_shell (Half-buried shell, 428KB)
[X] prop_sun_sand_ripple (Sediment drift plane)
[X] prop_sun_coral_branching (Orange Staghorn Coral, 160KB)
[X] prop_sun_coral_mound (Brain Coral by Kelli Ray, 42KB)
[X] prop_sun_coral_fan (Large Fan Coral, 3.5MB)
[X] Reusable Rock Pack (30 FBX meshes / 60 prefabs)
[ ] prop_sun_rock_crevice (Kitbash overhang from 2x Cube_004)
[ ] prop_sun_algae_boulder (Icosphere + green algae material)

--- ZONE 1: TWILIGHT ZONE (200–1,000m) ---
[X] Multi-Zone Rock Transformation Matrix for Twilight (Cube_004, Icosphere_003, bisect)
[ ] prop_twi_coldcoral_bush (Branching Lophelia tree, ~500 tris)
[ ] prop_twi_glass_vase_sponge (Hexactinellid goblet sponge, ~300 tris)
[ ] prop_twi_sponge_cluster (Trio of cup sponges, ~320 tris)
[ ] prop_twi_silt_mound (Deep mud swell with burrows, ~250 tris)
[ ] prop_twi_marine_snow_drift (Sediment ribbon, ~180 tris)
[ ] prop_twi_angular_cliff_block (Fractured drop-off boulder, ~300 tris)

--- ZONE 2: MIDNIGHT ZONE (1,000–4,000m) ---
[X] Multi-Zone Basalt Rock Kitbash (Pillow basalt icospheres, vertical cones)
[ ] prop_mid_smoker_chimney (Hydrothermal chimney + smoke emitter, ~450 tris)
[ ] prop_mid_smoker_cluster (Triple mini cones with sulfur, ~500 tris)
[ ] prop_mid_sulfide_crust (Iron-sulfide floor slab, ~220 tris)
[ ] prop_mid_harp_sponge_prop (Candelabra carnivorous sponge, ~400 tris)
[ ] prop_mid_volcanic_spine (Sharp basalt needle, ~280 tris)
[ ] prop_mid_collapsed_column (Fallen basalt column, ~220 tris)

--- ZONE 3: ABYSSAL ZONE (4,000–6,000m) ---
[X] Multi-Zone Ooze Swells & Slabs (Sphere & Cube transformations)
[ ] prop_aby_manganese_cluster (Nodule disc scatter, ~350 tris)
[ ] prop_aby_glass_hexactinellid (Stalked glass sponge, ~300 tris)
[ ] prop_aby_ooze_crater (Sediment feeding depression, ~200 tris)
[ ] prop_aby_sediment_stalk (Biogenic worm tube, ~120 tris)
[ ] prop_aby_whalefall_rib (Curved whale rib bone, ~350 tris)
[ ] prop_aby_whalefall_vertebra (Massive bone disc, ~320 tris)
[ ] prop_aby_carbonate_slab (Methane seep crust, ~250 tris)

--- ZONE 4: HADAL ZONE (6,000–11,000+m) ---
[X] Multi-Zone Fault Scarps & Spires (Tall Cube & Cone transformations)
[ ] prop_had_xenophyophore_clump (Frilly organic silt ball, ~250 tris)
[ ] prop_had_debris_fall (Sunken detritus cluster, ~250 tris)
[ ] prop_had_trench_silt_windrow (Gravity current silt ridge, ~220 tris)
[ ] prop_had_talus_rubble (Shattered rockslide rubble pile, ~300 tris)
[ ] prop_had_crinoid_base_boulder (Anchor plate for Bathycrinus, ~200 tris)
[ ] prop_had_tectonic_fissure (Floor fissure with haze, ~240 tris)
[ ] prop_had_serpentinite_crust (Greenish-black mineral slab, ~200 tris)
```

---

## 5. The 6-Month Asset Production Fast-Track

```
┌────────────────────────┐      ┌────────────────────────┐      ┌────────────────────────┐
│   Modular 3D Blockout  │  ──► │  Palette UV Mapping   │  ──► │ Unity URP Prefab Bake  │
│  (Blender: 15-25 mins) │      │  (Atlas: 5-10 mins)    │      │  (Colliders + Layers)  │
└────────────────────────┘      └────────────────────────┘      └────────────────────────┘
```

### 1. Palette Texturing (Zero UV Painting Workflow)
- Build a single **$512 \times 512$ PNG Master Color Palette** containing 16–24 color blocks:
  - **Top Row (Sunlight):** Coral Pinks, Orange, Vibrant Turquoise, Algae Green, Sand Tan.
  - **Middle Row (Twilight & Midnight):** Slate Gray, Silt Olive, Basalt Black, Smoker Pyrite Rust, Sulfur Yellow.
  - **Bottom Row (Abyss & Hadal):** Bone Off-White, Nodule Dark Brown, Deep Chert, Translucent Glass Gray, Serpentinite Dark Green.
- In Blender: Unwrap your low-poly mesh (`U` $\rightarrow$ `Smart UV Project` or `Project from View`), scale all UV vertices down to `0.01`, and place them into the corresponding solid color block.
- **Benefits:** $100\%$ visual cohesion, zero Photoshop/Substance painting time, and near-zero GPU memory impact on Android mobile.

### 2. Mobile Mesh Budgets
- **Environment Props:** $150$ to $600$ triangles per prop.
- **Mobile Organisms:** $400$ to $1,200$ triangles per creature.
- **Draw Call Batching:** Because all props in a zone share the unified palette material, Unity's SRP Batcher and Static Batching reduce draw calls to single digits.

---

## 6. Month-by-Month Execution Schedule

- **Month 1: Systems Integration & Data Setup (COMPLETED)**
  - `EnvPropSet` connected with `BiomeBand` filtering.
  - `SunlightProps.asset` and `TwilightProps.asset` populated.
  - All 95 species defined in `SpeciesRegistry.asset` and species definitions.
  - Procedural terrain and dynamic atmospheric lighting verified.
- **Month 2: Sunlight Polish & Twilight Props (Blender)**
  - Model 3 core Twilight biological props: `prop_twi_coldcoral_bush`, `prop_twi_glass_vase_sponge`, and `prop_twi_silt_mound`.
  - Assemble `prop_sun_rock_crevice` overhang kitbash.
  - Texture using the single 512x512 color palette.
- **Month 3: Deep Sea Props (Midnight, Abyss, Hadal)**
  - Model Midnight hydrothermal smoker & harp sponge.
  - Model Abyssal manganese nodule cluster & whale fall rib.
  - Model Hadal scree rubble, xenophyophore & crinoid base.
  - Create the remaining 3 zone rock material overrides (Midnight Basalt, Abyssal Pelagic Stone, Hadal Chert).
- **Month 4: High-Priority Representative Species Models**
  - Rig & animate 1 iconic mobile creature per zone (e.g. Clownfish, Coffinfish, Anglerfish, Grenadier, Snailfish).
  - Model static meshes for stationary species (Anemones, Clams, Crinoids).
- **Month 5: Gameplay Polish & Balancing**
  - Calibrate Capture & Focus + Reconstruction Scan minigames across all 5 zones.
  - Submarine upgrade tree tuning (Hull, Sonar, Scanner, Engine, Floodlights, Utilities).
  - Sound & VFX polish (underwater hydrophone hum, marine snow particle tuning).
- **Month 6: Evaluation, Playtesting & Thesis Writing**
  - Conduct marine literacy pre-test / post-test evaluation with target student cohort.
  - Compile statistical data on species recognition and engagement.
  - Finalize thesis manuscript and prepare defense presentation.
