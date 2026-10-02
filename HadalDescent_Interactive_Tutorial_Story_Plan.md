# Hadal Descent — Interactive Tutorial & Story Narrative Plan

**Document Type:** Game Design Document (GDD) — Interactive Tutorial System & Story Arc  
**Target Engine:** Unity (C# / URP / Mobile Android)  
**Estimated Implementation Time:** 3–4 hours  
**Date:** October 2026  

---

## Table of Contents

1. [Story Narrative Arc](#1-story-narrative-arc)
   - 1A. [Premise & Setting](#1a-premise--setting)
   - 1B. [Opening Cinematic Sequence](#1b-opening-cinematic-sequence-sunlight-zone-start--ocean-descent-preview--cockpit-transition)
   - 1C. [High-Level Story Arc](#1c-high-level-story-arc-prologue--act-i--act-ii--act-iii)
   - 1D. [Skip Tutorial System (Player Agency & Instant Bypass)](#1d-skip-tutorial-system-player-agency--instant-bypass)
2. [Tutorial Design Philosophy & Resilience](#2-tutorial-design-philosophy--resilience)
   - 2A. [Research: How Acclaimed Games Teach Underwater Exploration](#2a-research-how-acclaimed-games-teach-underwater-exploration)
   - 2B. [Core Design Rules](#2b-core-design-rules)
   - 2C. [Tutorial Resilience: What Happens If the Player Disobeys or Refuses to Scan?](#2c-tutorial-resilience-what-happens-if-the-player-disobeys-or-refuses-to-scan)
3. [Interactive Tutorial Flow — Step-by-Step](#3-interactive-tutorial-flow)
4. [Technical Architecture](#4-technical-architecture)
5. [UI Component Specifications](#5-ui-component-specifications)
   - 5A. [Radio Textbox (Gameplay SDF, Non-Blocking, No Bold, Min 32px)](#5a-radio-textbox-non-blocking-comms--gameplay-sdf)
   - 5B. [Tutorial Directional Arrow & Sonar Legend](#5b-tutorial-directional-arrow--sonar-legend)
   - 5C. [Full-Screen Image Slideshow Briefing (`Arts/Tutorial`)](#5c-full-screen-image-slideshow-briefing-artstutorial)
   - 5D. [Skip Tutorial Button & Confirmation Prompt](#5d-skip-tutorial-button--confirmation-prompt)
6. [Asset Requirements Checklist](#6-asset-requirements-checklist)
7. [Implementation Timeline (3–4 Hours)](#7-implementation-timeline)
8. [Feasibility Analysis](#8-feasibility-analysis)
9. [Appendix A: Dr. Santos Radio Script (Complete Dialogue & Nudges)](#appendix-a-dr-santos-radio-script)
10. [Appendix B: Quick Reference — What Exists vs. What's New](#appendix-b-quick-reference--what-exists-vs-whats-new)

---

## 1. Story Narrative Arc

### 1A. Premise & Setting

> **Logline:** A marine biologist is deployed to the Philippine Sea aboard a state-of-the-art research submersible to build the world's first comprehensive deep-sea biodiversity database — cataloging 95 marine species from sunlit coral reefs down to the hadal abyss.

---

### 1B. Opening Cinematic Sequence (Sunlight Zone Orbit → Cockpit Transition)

The game opens with a **simplified in-engine cinematic** (~15 seconds) utilizing the **custom 3D Submarine model**. The sequence orbits the submarine in the **Sunlight Zone**, dollies into the cockpit, and hands off to the first radio transmission:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        OPENING CINEMATIC SEQUENCE                          │
│                     (~15 seconds, non-interactive)                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  BEAT 1 — SUNLIGHT ZONE ORBIT (Third Person, 5s)                           │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Camera orbits the player's 3D Submarine model cruising gracefully  │    │
│  │  through the vibrant Sunlight Zone (0–200m).                        │    │
│  │  Golden sunbeams penetrate the crystal turquoise water.             │    │
│  │  Text overlay fades in (Gameplay SDF, 36px, clean un-bolded):       │    │
│  │    "PHILIPPINE SEA — BENHAM RISE MARINE EXPEDITION"                 │    │
│  │    "BIODIVERSITY RESEARCH SUBMERSIBLE 'HADAL EXPLORER'"             │    │
│  │                                                                      │    │
│  │  [SKIP TUTORIAL] button visible in top-right corner from the start. │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  BEAT 2 — COCKPIT TRANSITION (Third → First Person, 5s)                    │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Camera smoothly dollies toward the forward cockpit viewport glass.  │    │
│  │  Camera passes through the glass pane into the 1st-person view.      │    │
│  │  Cockpit HUD instruments initialize and power on:                   │    │
│  │    • Depth gauge flickers on at 18m                                 │    │
│  │    • Sonar radar begins rotating sweep                              │    │
│  │    • Reticle powers up with cyan diagnostic circle                   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  BEAT 3 — FIRST RADIO TRANSMISSION (First Person, Comms Active)            │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  🔊 Audio: Radio static burst + radio chirp tone                    │    │
│  │                                                                      │    │
│  │  RADIO TEXTBOX slides up (bottom-center, Gameplay SDF, no bold):   │    │
│  │  ┌──────────────────────────────────────────────────────────────┐   │    │
│  │  │ 📻 DR. SANTOS — BENHAM RISE MARINE STATION   [SKIP TUTORIAL] │   │    │
│  │  │                                                               │   │    │
│  │  │ "Welcome aboard, Researcher. This is Dr. Santos at the       │   │    │
│  │  │  Benham Rise Marine Station. Submersible diagnostics are      │   │    │
│  │  │  green. You are currently deployed in the Sunlight Zone.     │   │    │
│  │  │                                                               │   │    │
│  │  │  Your mission: document every marine species in the           │   │    │
│  │  │  Philippine Sea to build our Marine Biodiversity Database.    │   │    │
│  │  │  Let's test your thrusters and navigational systems."         │   │    │
│  │  │                                                               │   │    │
│  │  │ OBJECTIVE: Move your submarine forward using the joystick ─── │   │    │
│  │  └──────────────────────────────────────────────────────────────┘   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  ► GAMEPLAY COMMENCES — Full player control active immediately              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 1C. High-Level Story Arc (Prologue → Act I → Act II → Act III)

| Act | Narrative Milestone | Gameplay Progression |
| :--- | :--- | :--- |
| **PROLOGUE** | **Deployment & Calibration** — Marine biologist arrives in the Philippine Sea aboard the research submersible. | In-engine 3D submarine cinematic (~15s): Sunlight Zone orbit → cockpit transition → comms calibration with Dr. Santos. *(Option to skip tutorial available at any point).* |
| **ACT I** | **Sunlight Zone Expedition & Research Certification** — Dr. Santos mentors the researcher through core exploration protocols. | Interactive tutorial steps occur seamlessly without pausing gameplay. Player masters piloting, sonar reading, both scanning minigames (with self-paced `[CONTINUE]` briefings and embedded `Arts/Tutorial` images), Bestiary encyclopedia, debris cleanup, hazard survival, and upgrade shop. |
| **ACT II** | **The Descent into the Deep** — Player descends progressively through Twilight, Midnight, Abyssal, and Hadal zones. Hull upgrades are required to survive mounting hydrostatic pressure. | Radio transmissions from Dr. Santos become rarer and more atmospheric as the radio signal weakens with depth. Environmental storytelling through habitat landmarks and bioluminescence. |
| **ACT III** | **Final Cataloging & 100% Completion** — Cataloging the rarest hadal trench species to achieve complete database coverage (95/95 species). | Database reaches full completion. *(Note: Ending sequence cinematics and global research center upload sequence are deferred for future implementation).* |

---

### 1D. Skip Tutorial System (Player Agency & Instant Bypass)

To support returning players, playtesters, and experienced pilots, the tutorial can be skipped at **any time**:

1. **Universal Top-Center HUD Skip Button:** A dedicated `[ SKIP TUTORIAL ]` button is visible and interactable at the top-center across all screens, modals, cinematic sequences, and active minigames (dedicated canvas sorting order 32,000, modal sorting order 32,500).
2. **Radio Textbox Header:** A discrete `[ SKIP ]` button is embedded on the right side of the radio communication header next to Dr. Santos' title.
3. **Clean 4-Button Pause Menu:** The Pause Menu maintains its standard, uncluttered 4-button layout (`RESUME`, `SAVE GAME`, `SETTINGS`, `EXIT GAME`) without redundant buttons, since the top-center skip button remains permanently accessible.

```
┌────────────────────────────────────────────────────────────────────────┐
│                   SKIP TUTORIAL CONFIRMATION PROMPT                    │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│               ╔══════════════════════════════════════════╗             │
│               ║             SKIP TUTORIAL?               ║             │
│               ║                                          ║             │
│               ║  Skip basic training and proceed         ║             │
│               ║  directly to free ocean exploration?     ║             │
│               ║  All standard equipment will be online.  ║             │
│               ║                                          ║             │
│               ║    [ RESUME TUTORIAL ]    [ SKIP NOW ]   ║             │
│               ╚══════════════════════════════════════════╝             │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

#### What Happens When Tutorial is Skipped (Including Inside Minigames):
- **Universal Availability:** The `[ SKIP TUTORIAL ]` button is rendered on a dedicated canvas overlay (`sortingOrder = 32000`) and the confirmation modal at `sortingOrder = 32500`, ensuring it is visible and interactable even during full-screen minigames or slideshow briefings.
- **Forced Minigame Cleanup:** If skipped while inside a minigame or briefing, `MinigameManager.Instance.CancelActiveMinigame()` and `MinigameInstructionOverlay.Instance.HideInstant()` are immediately invoked. This destroys all minigame panels, resets `_minigameActive = false`, restores `Time.timeScale = 1.0f`, and re-enables submarine thrusters and camera touch controls (`PlayerMovement` & `TouchDragZone`).
- Immediately closes any active radio transmission boxes, instruction overlays, or animated tutorial arrows.
- Sets persistent flag `GameManager.SetTutorialCompleted(true)`.
- Awards the standard `+100 RDP` expedition completion bonus so the player is not penalized for skipping.
- Enables standard exploration HUD (`UIManager.Instance.SetExplorationHUDVisible(true)`).
- Radio comms plays a brief sign-off (Gameplay SDF, 34px):  
  *"Submersible systems certified. Good luck out there, Researcher. Dr. Santos out."*
- Player is instantly free to explore the Sunlight Zone and interact with all world elements without guided prompts.

---

## 2. Tutorial Design Philosophy & Resilience

### 2A. Research: How Acclaimed Games Teach Underwater Exploration

| Reference Game | Core Technique | Application in Hadal Descent |
| :--- | :--- | :--- |
| **Subnautica** | **Diegetic Comms & Non-Intrusive Voice/Text** — Alterra HQ and PDA provide updates while the player continues swimming. | Dr. Santos contacts the submarine via radio without interrupting player steering or exploration. |
| **Zelda: Breath of the Wild** | **Sandbox Micro-Goals** — The player learns through interaction with nearby elements rather than tutorial modal cards. | The Sunlight Zone acts as our safe training ground. All interactive lessons occur within 100m of the starting point. |
| **Mega Man X** | **Forced Natural Encounter** — Obstacles and opportunities are placed directly along the player's path. | Two distinct marine species are placed within the player's immediate view cone to guarantee scanning encounters. |
| **Portal** | **Isolated Mechanic Mastery** — Introduce one tool, confirm understanding, then introduce the next. | Step progression is gated by player actions (e.g. moving the joystick for 2 seconds, completing a scan). |
| **Dead Space** | **Diegetic Cockpit Instruments** — UI is embedded within the vehicle instruments. | Gauges, radar, and buttons are explained with animated pointing arrows directing attention to the cockpit HUD. |

---

### 2B. Core Design Rules

> [!IMPORTANT]
> **Rule #1: NEVER pause the game or freeze controls for tutorial text.**  
> Exploration, swimming, and ocean ambience continue uninterrupted. The player reads or listens to radio transmissions while piloting.

> [!IMPORTANT]
> **Rule #2: Typography & Legibility Standards.**  
> - All radio dialog and tutorial instructions MUST use **Gameplay SDF** (`Assets/TextMesh Pro/Fonts/Gameplay SDF.asset`).
> - **DO NOT USE BOLD** for radio text — keep letterforms clean, open, and readable.
> - **Minimum text font size is 32px** across all tutorial elements, objective lines, and button labels (mobile readability guarantee).

> [!IMPORTANT]
> **Rule #3: Self-Paced Image Slideshow Overlays (No Instruction Text).**  
> Minigame briefings display tutorial images from `Assets/Arts/Tutorial/` in a **full-screen image slideshow** — one image at a time. All instruction text is **baked directly into the images** (each image already contains numbered step, instructions, and gameplay diagrams). The overlay shows only: the image in a cyan-bordered container, a page counter (`1 / 3`), `◀ PREV` / `NEXT ▶` arrows, a `[SKIP ▶]` button (top-right), and a `[CONTINUE]` button that replaces NEXT on the final slide. No auto-fade timer; fully self-paced.

> [!IMPORTANT]
> **Rule #4: Always provide a Skip Tutorial option.**  
> Allow players to skip the tutorial at any time without penalty via HUD, radio header, or pause menu.

---

### 2C. Tutorial Resilience: What Happens If the Player Disobeys or Refuses to Scan?

A frequent issue in open-world games occurs when players ignore tutorial objectives: *What if the player wanders away from the species? What if they stare at the seafloor? What if they stubbornly refuse to scan?*

Hadal Descent handles player non-compliance using a **4-Tier Non-Intrusive Escalation & Fail-Safe System**:

```
┌────────────────────────────────────────────────────────────────────────┐
│             PLAYER NON-COMPLIANCE & FAIL-SAFE FLOWCHART                │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│   Player receives scan objective (Step 4A / 4B)                        │
│   │                                                                    │
│   ├── [0–20s] Free Roam / Observation                                  │
│   │   • Player swims around, looks at corals. Controls 100% free.      │
│   │   • No nagging; ambient audio plays naturally.                     │
│   │                                                                    │
│   ├── [20–40s] TIER 1: Gentle Radio Nudge                              │
│   │   • Dr. Santos radios: "Researcher, check your sonar radar.        │
│   │     There's a biological contact right near your heading."         │
│   │   • Cyan blip on Sonar pulses with a gentle ripple wave.           │
│   │                                                                    │
│   ├── [40–60s] TIER 2: Active Navigation Assist & Creature Leashing    │
│   │   • Dr. Santos: "I'm highlighting the specimen on your HUD.        │
│   │     Align it with your forward viewport reticle."                  │
│   │   • A world-space 3D arrow appears in the ocean pointing to creature│
│   │   • Creature AI (Curiosity Behavior): If player wandered away,     │
│   │     the species circles back and glides smoothly across the front  │
│   │     cockpit glass, giving an effortless scan opportunity.          │
│   │                                                                    │
│   ├── [60s+] TIER 3: Soft Fail-Safe — [SKIP STEP] Appears              │
│   │   • On the Radio Textbox, an optional button appears:              │
│   │     [ SKIP STEP ] alongside [ SKIP TUTORIAL ].                     │
│   │   • If tapped: Dr. Santos radios: "Remote telemetry captured the   │
│   │     specimen's biological signature. Let's move on."               │
│   │   • Automatically logs species, awards RDP, and advances to Step 5.│
│   │                                                                    │
│   └── [Physical Boundaries] Can the player escape into the deep abyss? │
│       • Sunlight Zone (0–200m) is fully open to roam.                  │
│       • If player tries to descend past 200m into the Twilight Zone,   │
│         the Submarine Pressure Alarm sounds:                           │
│         "⚠️ PRESSURE WARNING: Hull strength insufficient for deeper   │
│          depths. Upgrades required at Station Shop."                   │
│       • The submarine cannot implode or soft-lock; player is gently    │
│         buoyant-pushed back to safe depth.                             │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

#### Core Safeguards Summary:
1. **Never Freeze Controls:** Player retains full joystick navigation and camera control at all times.
2. **Creature AI Assistance:** The tutorial species is tethered to a 25m patrol radius around the player and actively swims into the viewport if the player remains idle.
3. **Natural Depth Gating:** Hydrostatic pressure naturally prevents wandering into deeper zones before purchasing hull upgrades.
4. **Skip Step / Telemetry Bypass:** If the player ignores the objective for >60 seconds, a `[ SKIP STEP ]` option appears, ensuring the player is **never soft-locked**.
5. **Minigame Retry Protection:** If a player fails a scan minigame, the creature does not despawn; Dr. Santos encourages a retry, and after 2 failed attempts, the minigame automatically eases difficulty (wider green focus bar, slower piece timer).

---

## 3. Interactive Tutorial Flow — Step-by-Step

### Overview: 9 Interactive Gameplay Beats

All steps occur in the **Sunlight Zone (0–200m)** during the player's maiden dive.

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                    TUTORIAL STEP PROGRESSION FLOWCHART                          │
├────────────────────────────────────────────────────────────────────────────────┤
│                                                                                │
│  [CINEMATIC] 3D Submarine in Sunlight Zone ──► 3s Zone Previews ──► Cockpit    │
│       │                                                    │                   │
│       ▼                                                    │ [SKIP TUTORIAL]   │
│  ┌─────────────────┐    Player moves    ┌─────────────────┐│  (Available       │
│  │ STEP 0           │ ──────────────►   │ STEP 1           ││   anytime)       │
│  │ Mission Overview │   (auto: 8s)      │ HUD Orientation  ││                  │
│  │ (Radio call)     │                   │ (Sequential 32px││                  │
│  └─────────────────┘                    │  arrow pointers) ││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         Player moves stick │                  │
│                                                  │         │                  │
│                                                  ▼         │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ STEP 2           ││                  │
│                                         │ Navigation       ││                  │
│                                         │ (Arrow → Stick)  ││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         Player checks Sonar│                  │
│                                                  │         │                  │
│                                                  ▼         │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ STEP 3           ││                  │
│                                         │ Sonar Legend     ││                  │
│                                         │ • Cyan: Species  ││                  │
│                                         │ • Yellow: Habitat││                  │
│                                         │ • Orange: Debris ││                  │
│                                         │ • Red: Hazards   ││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         Species #1 in view │                  │
│                                                  │         │                  │
│                                                  ▼         │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ STEP 4A          ││                  │
│                                         │ Scan Encounter 1 ││                  │
│                                         │ ("Two scan types"││                  │
│                                         │  + Multi-image   ││                  │
│                                         │  Arts/Tutorial   ││                  │
│                                         │  + [CONTINUE] btn││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         Species #2 in view │                  │
│                                                  │         │                  │
│                                                  ▼         │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ STEP 4B          ││                  │
│                                         │ Scan Encounter 2 ││                  │
│                                         │ (Alternate scan  ││                  │
│                                         │  + Minigame 2 img││                  │
│                                         │  + [CONTINUE] btn││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         Both scans complete│                  │
│                                                  │         │                  │
│                                                  ▼         │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ STEP 5           ││                  │
│                                         │ Bestiary Review  ││                  │
│                                         │ • 3D Model Modal ││                  │
│                                         │ • Detail Modal   ││                  │
│                                         │ • Clues for lost ││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         Near Debris Cluster│                  │
│                                                  │         │                  │
│                                                  ▼         │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ STEP 6           ││                  │
│                                         │ Debris Cleanup   ││                  │
│                                         │ (Claw & sorting  ││                  │
│                                         │  + [CONTINUE] btn││                  │
│                                         │  to start MG3)   ││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         Reef Hazard Zone   │                  │
│                                                  │         │                  │
│                                                  ▼         │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ STEP 7           ││                  │
│                                         │ "Dangers Ahead"  ││                  │
│                                         │ (Dodge & Pearls ││                  │
│                                         │  + [CONTINUE] btn││                  │
│                                         │  to start MG4)   ││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         RDP Bonus Earned   │                  │
│                                                  │         │                  │
│                                                  ▼         │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ STEP 8           ││                  │
│                                         │ Upgrade Shop     ││                  │
│                                         │ (Hull, Engine,   ││                  │
│                                         │  Sonar tiers)    ││                  │
│                                         └────────┬────────┘│                  │
│                                                  │         │                  │
│                                         Tutorial Complete! │                  │
│                                         ┌─────────────────┐│                  │
│                                         │ GRADUATION       ││                  │
│                                         │ "Good luck"      ││                  │
│                                         │ +100 RDP Bonus   ││                  │
│                                         └─────────────────┘│                  │
│                                                  ▲         │                  │
│                                                  │         │                  │
│                                                  └─────────┴──────────────────┘
│                                                    Direct jump if skipped      │
└────────────────────────────────────────────────────────────────────────────────┘
```

---

### Step-by-Step Detailed Specifications

#### STEP 0 — Mission Overview
- **Trigger:** Automatic upon completion of the opening cinematic sequence.
- **Radio Speaker:** Dr. Santos — Benham Rise Marine Station.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Welcome aboard, Researcher. This is Dr. Santos at the Benham Rise Marine Station. Submersible diagnostics are green. You are currently deployed in the Sunlight Zone. Your mission: document every marine species in the Philippine Sea to build our Marine Biodiversity Database. Let's begin by testing your instruments."*
- **Objective Line (32px, green):** `OBJECTIVE: Review mission parameters`
- **Skip Option:** `[ SKIP TUTORIAL ]` button visible in top-right HUD and radio box.
- **Behavior:** Stays visible for 8 seconds or until player taps the screen. Controls are active.

---

#### STEP 1 — HUD & Cockpit Gauges Orientation
- **Trigger:** Immediate follow-up after Step 0.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Your submarine's instrument panel is online. Let me walk you through your displays."*
- **Sequential Pointer Arrows (32px labels, 1.5s per instrument):**
  1. ⬅️ Arrow → **Depth Gauge** (top-left): *"Current Depth (metres)"*
  2. ⬅️ Arrow → **Sonar Map** (top-left circle): *"Sonar Radar"*
  3. ➡️ Arrow → **RDP Counter** (top-right): *"Research Data Points"*
  4. ➡️ Arrow → **Shop Button** (top-right wrench icon): *"Submarine Upgrade Shop"*
  5. ➡️ Arrow → **Bestiary Button** (top-right book icon): *"Marine Biodiversity Bestiary"*
- **Objective Line (32px, green):** `OBJECTIVE: Familiarize yourself with HUD instruments`
- **Advance Condition:** All 5 arrow pointers complete their sequential pulse (~8s total).
- **Entity Visibility Invariant:** Marine species (mobile `SpeciesAI` and stationary `ScanTarget`) and debris clusters are strictly hidden and disabled during Step 1 (as well as Step 0 and Step 2). `SpeciesSpawner` spawns instances inactive prior to Step 3, `DistanceCullingManager` bypasses activation of inactive objects and is paused, and `TutorialManager` proactively enforces hidden state on `_Creatures` and `_DebrisClusters`. Creature #1 is only introduced when Step 3 officially starts.

---

#### STEP 2 — Submarine Navigation & Viewport Controls
- **Trigger:** Completion of Step 1.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Time to take the helm. Drag across the viewport to steer your camera view, and push the directional joystick to navigate forward."*
- **UI Pointers:**
  - ↕️ Center screen arrow: *"DRAG VIEWPORT TO LOOK"* (32px label)
  - ⬇️ Bottom-left joystick arrow: *"USE JOYSTICK TO NAVIGATE"* (32px label)
- **Objective Line (32px, green):** `OBJECTIVE: Steer submarine with joystick for 2 seconds`
- **Advance Condition:** Sustained joystick input `moveInput.sqrMagnitude > 0.05f` for ≥ 2.0 continuous seconds.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"Thrusters responding crisply. You're handling the submersible like a pro."*

---

#### STEP 3 — Sonar System & Color Radar Identification
- **Trigger:** 3 seconds after Step 2 completes.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Keep an eye on your sonar radar. Different contact signatures emit distinct frequencies: Cyan markers indicate marine species. Yellow markers pinpoint habitat landmarks. Orange markers reveal collectible debris, and Red markers highlight dangerous hazard zones. Steer toward the nearest cyan blip."*
- **UI Element:** Animated arrow pointing to the top-left Sonar Map, accompanied by a clean side HUD Legend card:
  ```
  ╔══════════════════════════════════════════════╗
  ║            SONAR CONTACT LEGEND              ║
  ║  🔷 Cyan:   Marine Species                   ║
  ║  🟡 Yellow: Habitat Landmarks                ║
  ║  🟠 Orange: Collectible Debris               ║
  ║  🔴 Red:    Hazard Zones                     ║
  ╚══════════════════════════════════════════════╝
  ```
- **Objective Line (32px, green):** `OBJECTIVE: Track and approach a Cyan species signature on sonar`
- **Advance Condition:** Submarine approaches within 35 metres of a scannable creature (`SpeciesAI`).
- **Resilience / Non-Compliance:** If the player wanders away for >25s, Dr. Santos issues a gentle reminder, and the nearest species AI gently steers into the player's path.
- **Early-Scan Resilience (Scanning Before Step 4A):** If the player locks reticle and scans Species #1 early during Step 3, the game forces Minigame 1 (`Capture & Focus`). Upon scan success, Dr. Santos immediately delivers the specimen logged congratulations, hides Step 3 sonar pointers, and smoothly transitions directly to **Step 4B** (Encounter 2), seamlessly bypassing Step 4A with no redundant prompts or soft-locks.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"Contact visual confirmed! Target is within scanning distance."*

---

#### STEP 4A — Species Encounter 1: First Scanning Module (Image Slideshow Briefing)
- **Trigger:** Target creature enters the scanning reticle.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"You have a creature in your sights! There are two types of scanning modules on your submersible depending on the specimen. Align the target in your reticle and tap SCAN."*
- **UI Pointers:**
  - Arrow → **Center Reticle**: *"ALIGN CREATURE"* (32px label)
  - Arrow → **SCAN Button** (bottom HUD): *"TAP TO SCAN"* (32px label)
- **Image Slideshow Briefing (2 slides):**
  - **Slide 1/2:** `Minigame 1-1.png` — "Hold anywhere in the screen to raise the green bar" (text baked into image)
  - **Slide 2/2:** `Minigame 1-2.png` — "Releasing the hold drops the green bar" (text baked into image)
  - **No separate instruction text** — images are self-contained with step numbers and diagrams
  - **Navigation:** `◀ PREV` / `NEXT ▶` arrows, page counter `1 / 2`, `[SKIP ▶]` top-right
  - **Last slide:** `[CONTINUE TO SCAN]` replaces NEXT (34px, Cyan accent)
- **Objective Line (32px, green):** `OBJECTIVE: Align creature in reticle and complete scan`
- **Non-Compliance Safeguard:** If player ignores for >60s, `[ SKIP STEP ]` option appears on the radio textbox.
- **Advance Condition:** Player completes Minigame 1.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"First specimen logged! Excellent work. But our survey requires data on another nearby creature to calibrate our secondary scanning protocol."*

---

#### STEP 4B — Species Encounter 2: Second Scanning Module (Image Slideshow Briefing)
- **Context:** To ensure the player is trained on both randomized scanning minigames, the tutorial spawns or highlights a second distinct species nearby.
- **Trigger:** Second creature comes into proximity.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Notice the different bio-signature on this second creature? Our automated scanner will switch to the alternative scanning module. Lock on and initiate scan."*
- **Image Slideshow Briefing (1 slide):**
  - **Slide 1/1:** `Minigame 2.png` — "Tap an adjacent tile next to the empty space to move it" (text baked into image)
  - **No separate instruction text** — image is self-contained
  - **Single slide:** `[CONTINUE TO SCAN]` button shown directly below image (34px, Cyan accent)
- **Objective Line (32px, green):** `OBJECTIVE: Scan second creature to calibrate all scanner modes`
- **Advance Condition:** Player completes the second minigame scan.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"Both scanning modules calibrated! All species data has been automatically synced with your on-board encyclopedia. Let's review the Bestiary."*

---

#### STEP 5 — Bestiary Marine Encyclopedia (3D Inspection & Scientific Details)
- **Trigger:** Immediately after completing Step 4B.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Open the Bestiary to review your documented specimens. Every discovered entry features two interactive views: the 3D Model modal to inspect full creature anatomy, and the Detail Modal to view biological field photos, scientific classifications, and ecological facts. If you ever feel lost or can't find a species, the search clues on undiscovered entries will guide your expedition."*
- **UI Pointers:**
  - Arrow → **Bestiary Button** (top-right book icon): *"TAP TO OPEN BESTIARY"* (32px label)
  - Inside Bestiary:
    - Arrow 1 → **3D Model Button / Thumbnail**: *"3D MODEL INSPECTION"* (32px label)
    - Arrow 2 → **Card Body / Detail Button**: *"SCIENTIFIC DETAIL MODAL"* (32px label)
    - Arrow 3 → **Undiscovered Silhouette / Clue Box**: *"SEARCH CLUES (HELP FOR LOST SPECIES)"* (32px label)
- **Objective Line (32px, green):** `OBJECTIVE: Open Bestiary and inspect your scanned species`
- **Advance Condition:** Player opens the Bestiary (`BestiaryManager.IsOpen == true`) and views an entry.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"With 95 species across 5 depth zones, those search clues will be invaluable when exploring deeper waters."*

---

#### STEP 6 — Environmental Debris Cleanup (Image Slideshow Briefing)
- **Trigger:** Player approaches within 30 metres of a guaranteed tutorial `DebrisCluster` (auto-spawned on the seabed ~35m from player start via code).
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Researcher, I'm detecting discarded ocean debris polluting the coral bed ahead. Human pollution disrupts fragile marine life. Maneuver in close and deploy your robotic collection arm to collect and sort the waste."*
- **UI Pointers:**
  - World-space 3D arrow floating directly over the physical debris cluster in the ocean.
  - Screen-space arrow → **INTERACT Button** (bottom HUD): *"TAP TO DEPLOY ARM"* (32px label)
- **Image Slideshow Briefing (3 slides):**
  - **Slide 1/3:** `Minigame 3-1.png` — "Move joystick to move the claw left or right" (text baked into image)
  - **Slide 2/3:** `Minigame 3-2.png` — "Tap the button to drop claw and collect trash" (text baked into image)
  - **Slide 3/3:** `Minigame 3-3.png` — "Sort collected trash into the correct bins" (text baked into image)
  - **No separate instruction text** — images are self-contained with step numbers and diagrams
  - **Navigation:** `◀ PREV` / `NEXT ▶` arrows, page counter `1 / 3`, `[SKIP ▶]` top-right
  - **Last slide:** `[CONTINUE TO CLEANUP]` replaces NEXT (34px, Cyan accent)
- **Objective Line (32px, green):** `OBJECTIVE: Approach debris and tap INTERACT to sort waste`
- **Advance Condition:** `EnvironmentalCleanupMinigame` completes successfully.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"Reef cleared! Removing debris revitalizes the ecosystem, increasing species encounter rates. You've earned research funding bonus."*

---

#### STEP 7 — Hazard Encounter: "Dangers Ahead" (Image Slideshow Briefing)
- **Context:** In the Sunlight Zone (0–200m), there are **no thermal vent plumes**. Hazards consist of turbulent reef currents, seamount terrain, sharp coral spires, and falling reef rockfall. Additionally, Minigame 4 features **multiple objectives**: surviving the hazard course while collecting valuable floating **Rare Pearls** dislodged by the ocean turbulence.
- **Trigger:** Automated by `TutorialManager` after Step 6 completes — directly calls `MinigameManager.TriggerHazardDodgeMinigame()` (no physical proximity trigger needed during tutorial).
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"⚠️ DANGERS AHEAD — Turbulent reef current and seamount terrain detected! Engage emergency evasive navigation. Swipe left and right across lanes to dodge falling rocks and vortexes. Collect floating rare pearls along the route to earn bonus research data!"*
- **Image Slideshow Briefing (3 slides):**
  - **Slide 1/3:** `Minigame 4-1.png` — "Swipe left or right across lanes to dodge obstacles" (text baked into image)
  - **Slide 2/3:** `Minigame 4-2.png` — "Collect pearls to earn bonus RDP" (text baked into image)
  - **Slide 3/3:** `Minigame 4-3.png` — "Avoid getting hit, 3 hits = hull compromised" (text baked into image)
  - **No separate instruction text** — images are self-contained with step numbers and diagrams
  - **Navigation:** `◀ PREV` / `NEXT ▶` arrows, page counter `1 / 3`, `[SKIP ▶]` top-right
  - **Last slide:** `[CONTINUE TO EVASIVE RUN]` replaces NEXT (34px, Red/Cyan accent)
- **Advance Condition:** `HazardDodgeMinigame` finishes.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  - If Survived: *"Masterful evasive piloting! You navigated the turbulence and secured valuable survey data."*
  - If Hit Taken: *"Hull alarms silenced. Even experienced deep-sea pilots take impacts. Reinforcing your hull at the Shop will increase your survival margin."*

---

#### STEP 8 — Submarine Upgrade Shop
- **Trigger:** Immediately after completing the hazard encounter.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"You've earned Research Data Points from species scanning, debris removal, and hazard navigation. Tap the Shop icon to inspect submarine upgrades."*
- **UI Pointer:** Arrow → **Shop Button** (top-right wrench icon): *"OPEN UPGRADE SHOP"* (32px label)
- **Inside Shop Highlight:**
  - Highlights the **Pressure Hull Upgrade**: *"Reinforces hull strength to withstand deeper water pressure, unlocking deeper ocean zones."*
  - Mentions **Engine, Sonar, and Scanner** upgrades.
- **Objective Line (32px, green):** `OBJECTIVE: Open the Upgrade Shop`
- **Advance Condition:** Player opens the Shop panel (`ShopManager.IsOpen == true`).
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"Keep upgrading your submersible as you descend. The Twilight Zone lies at 200 metres, where pressure mounts and sunlight fades."*

---

#### TUTORIAL GRADUATION
- **Trigger:** Player exits the Shop after Step 8.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"All systems verified and operational. Your training certification is complete, Researcher. The Philippine Sea holds 95 species waiting to be discovered across 5 depth zones. The deep ocean is calling. Dr. Santos out."*
- **Reward Notification:** Banner displays in Gameplay SDF (34px):  
  `TUTORIAL COMPLETE — +100 RDP EXPEDITION BONUS!`
- **Persistence:** `GameManager.SetTutorialCompleted(true)` saved to persistent player data.

---

## 4. Technical Architecture

### 4A. System Diagram

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    INTERACTIVE TUTORIAL ARCHITECTURE                      │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌────────────────────────┐   Events/Triggers  ┌───────────────────────┐ │
│  │   TutorialManager      │◄──────────────────►│  Gameplay Systems     │ │
│  │   (Refactored)         │                    │  • PlayerMovement     │ │
│  │                        │                    │  • ScannerSystem      │ │
│  │  • Non-blocking state  │                    │  • MinigameManager    │ │
│  │    machine (Steps 0–8) │                    │  • BestiaryManager    │ │
│  │  • Non-compliance nudges│                   │  • ShopManager        │ │
│  │  • Proximity triggers  │                    │  • DebrisCluster      │ │
│  │  • Dual-scan sequencer │                    │  • HazardDodgeMinigame│ │
│  │  • OnSkipTutorialClick │                    └───────────────────────┘ │
│  │  • Gameplay SDF driver │                                              │
│  └───────────┬────────────┘                                              │
│              │                                                           │
│              │ Dispatches UI updates                                     │
│              ▼                                                           │
│  ┌────────────────────────┐      ┌─────────────────────────┐             │
│  │  RadioTextboxUI        │      │  TutorialArrowUI        │             │
│  │  (New Component)       │      │  (New Component)        │             │
│  │                        │      │                         │             │
│  │  • Bottom-center docked│      │  • Screen-space pointers│             │
│  │  • Gameplay SDF font   │      │  • World-space pointers │             │
│  │  • NO BOLD font weight │      │  • Pulsing sine wave    │             │
│  │  • Min font size: 32px │      │  • Min 32px labels      │             │
│  │  • [SKIP TUTORIAL] btn │      │  • Sequential highlights│             │
│  │  • [SKIP STEP] fallback│      │  • Cyan glow shader     │             │
│  │  • Typewriter text anim│      │                         │             │
│  │  • Radio SFX trigger   │      │                         │             │
│  └────────────────────────┘      └─────────────────────────┘             │
│                                                                          │
│  ┌────────────────────────┐      ┌─────────────────────────┐             │
│  │  MinigameInstrOverlay  │      │  CinematicSequencer     │             │
│  │  (New Component)       │      │  (New Component)        │             │
│  │                        │      │                         │             │
│  │  • Embedded Arts/      │      │  • 3D Submarine orbit   │             │
│  │    Tutorial image cards│      │  • 3s Zone previews     │             │
│  │  • Button-matched frame│      │  • Cockpit camera dolly │             │
│  │  • Multi-image support │      │  • [SKIP] HUD button    │             │
│  │  • Prominent [CONTINUE]│      │  • Smooth HUD power-on  │             │
│  │  • NO auto-fade timer  │      │                         │             │
│  │  • Gameplay SDF (32px+)│      │                         │             │
│  └────────────────────────┘      └─────────────────────────┘             │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

### 4B. Script Modifications

| Existing Script | Location | Modifications Required |
| :--- | :--- | :--- |
| [TutorialManager.cs](file:///c:/Unity/HadalDescent/Assets/Scripts/UI/TutorialManager.cs) | `Assets/Scripts/UI/` | Refactor from modal card popup to non-blocking event-driven radio sequencer. Implement 4-tier non-compliance timers (20s nudge, 40s assist, 60s `[SKIP STEP]`). Hook `OnSkipTutorialClicked()` for instant bypass. |
| [UIManager.cs](file:///c:/Unity/HadalDescent/Assets/Scripts/UI/UIManager.cs) | `Assets/Scripts/UI/` | Expose RectTransform getters for arrow pointing: `GetDepthGaugeRect()`, `GetSonarMapRect()`, `GetRDPCounterRect()`, `GetScanButtonRect()`, `GetInteractButtonRect()`, `GetShopButtonRect()`, `GetBestiaryButtonRect()`. Add top-right Skip Tutorial HUD button reference. |
| [MinigameManager.cs](file:///c:/Unity/HadalDescent/Assets/Scripts/Minigames/MinigameManager.cs) | `Assets/Scripts/Minigames/` | Hook `MinigameInstructionOverlay` with multi-image panel before minigame initialization; pause gameplay until player presses `[ CONTINUE ]`. |
| [BestiaryManager.cs](file:///c:/Unity/HadalDescent/Assets/Scripts/UI/BestiaryManager.cs) | `Assets/Scripts/UI/` | Expose `IsOpen` property and getters for 3D inspection modal, detail modal, and search clues for tutorial step highlighting. |
| [HazardDodgeMinigame.cs](file:///c:/Unity/HadalDescent/Assets/Scripts/Minigames/HazardDodgeMinigame.cs) | `Assets/Scripts/Minigames/` | Ensure Sunlight Zone hazards display "DANGERS AHEAD" and emphasize the dual objectives (obstacle avoidance + rare pearl collection) on the `[CONTINUE]` briefing screen. |

---

### 4C. New Scripts to Create

| Script Name | Target Path | Purpose | Lines (est.) |
| :--- | :--- | :--- | :--- |
| **RadioTextboxUI.cs** | `Assets/Scripts/UI/` | Procedural non-blocking radio speech box using **Gameplay SDF**, no bold, min 32px text, typewriter reveal, audio static burst, and integrated `[ SKIP ]` button. | ~260 |
| **TutorialArrowUI.cs** | `Assets/Scripts/UI/` | Procedural animated directional pointer supporting both screen-space HUD targets and 3D world-space targets. Minimum label font size 32px. | ~200 |
| **MinigameInstructionOverlay.cs** | `Assets/Scripts/UI/` | Full-screen image slideshow briefing. Displays one tutorial image at a time from `Assets/Arts/Tutorial/` with page counter (`1/3`), `◀ PREV` / `NEXT ▶` arrows, `[SKIP ▶]` button, and `[CONTINUE]` on last slide. **No instruction text** — all text is baked into the images. | ~200 |
| **CinematicSequencer.cs** | `Assets/Scripts/Cinematics/` | Controls the simplified opening cinematic (~15s): 3D submarine Sunlight Zone orbit, cockpit camera dolly transition, HUD power-on, and top-right `[ SKIP TUTORIAL ]` button. | ~150 |

---

## 5. UI Component Specifications

### 5A. Radio Textbox (Non-Blocking Comms — Gameplay SDF)

```
┌────────────────────────────────────────────────────────────────────────┐
│                        RADIO TEXTBOX LAYOUT                            │
│                 (Bottom-center, non-blocking viewport)                 │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  Position: Bottom-center, anchored 60px above bottom screen margin     │
│  Dimensions: 880px width x 260px height (reference 1920x1080)          │
│  Background: Deep navy semi-transparent panel (0.03, 0.07, 0.14, 0.92) │
│  Border: 2px cyan line along top border (#00E5FF)                      │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  📻 DR. SANTOS — BENHAM RISE STATION             [SKIP TUTORIAL] │  │
│  │  ──────────────────────────────────────────────────────────────  │  │
│  │  "Welcome aboard, Researcher. Submersible diagnostics are        │  │
│  │   green. You are currently deployed in the Sunlight Zone..."     │  │
│  │                                                                  │  │
│  │  ──────────────────────────────────────────────────────────────  │  │
│  │  📋 OBJECTIVE: Move your submarine forward using the joystick   │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                        │
│  STRICT TYPOGRAPHY RULES:                                              │
│    • Font Family: Gameplay SDF (Assets/TextMesh Pro/Fonts/Gameplay SDF)│
│    • Font Weight: NORMAL ONLY — NO BOLD PERMITTED                      │
│    • Speaker Header: 38px, Normal Weight, Cyan (#4DDAFF)               │
│    • Body Text: 34px, Normal Weight, Crisp Off-White (#EEF2F7)         │
│    • Objective Text: 32px (MINIMUM), Normal Weight, Green (#33FF99)    │
│    • Skip Button Text: 32px, Normal Weight, Muted Cyan (#00E5FF99)     │
│                                                                        │
│  BEHAVIOR:                                                             │
│    • Non-blocking: Time.timeScale remains 1.0f (full game control)     │
│    • Text displays with smooth typewriter effect (35 char/sec)         │
│    • Tap anywhere on the textbox to instantly skip typewriter effect   │
│    • Tapping [SKIP TUTORIAL] opens prompt or immediately skips         │
│    • Auto-dismisses 4s after text completes OR when objective met      │
│    • Radio static burst (radio static.mp3) triggers on box appearance │
│    • Radio chirp (radio chirp.mp3) plays on BOTH box open AND close   │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

---

### 5B. Tutorial Directional Arrow & Sonar Legend

```
┌────────────────────────────────────────────────────────────────────────┐
│                     TUTORIAL POINTER SPECIFICATIONS                    │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  Visual: Procedural cyan chevron/arrow with soft outer bloom           │
│  Color: Vibrant Cyan (#00E5FF)                                         │
│  Animation: Bouncing sine wave along pointing axis (amplitude 6px, 2Hz)│
│                                                                        │
│  Label Specifications:                                                 │
│    • Font: Gameplay SDF                                                │
│    • Weight: Normal weight (NO BOLD)                                   │
│    • Font Size: 32px MINIMUM                                           │
│    • Text Color: Solid White (#FFFFFF) with 2px dark drop shadow       │
│    • Background: Dark pill capsule (0.04, 0.08, 0.16, 0.88)            │
│                                                                        │
│  Sonar Radar Contact Legend (Displayed during Step 3):                 │
│    • 🔷 Cyan   (#00EDE0) ── Marine Species                            │
│    • 🟡 Yellow (#FFE620) ── Habitat Landmarks                          │
│    • 🟠 Orange (#FFB826) ── Collectible Debris Clusters                │
│    • 🔴 Red    (#FF3B33) ── Hazard & Danger Zones                      │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

---

### 5C. Full-Screen Image Slideshow Briefing (`Arts/Tutorial`)

Tutorial images contain **all instruction text baked directly into each image** (step number, instructions, gameplay diagrams). The overlay simply displays one image at a time with navigation controls — no duplicate instruction text needed.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│             FULL-SCREEN IMAGE SLIDESHOW BRIEFING                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Anchoring: Center screen overlay (non-blocking, gameplay input disabled)   │
│  Background: Dark vignette (0.02, 0.04, 0.08, 0.92)                        │
│                                                                             │
│                                               [SKIP ▶]  ← Top-right, 32px │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                                                                       │  │
│  │               FULL TUTORIAL IMAGE DISPLAYED HERE                      │  │
│  │          (One image at a time, aspect ratio preserved)                │  │
│  │                                                                       │  │
│  │    All text is BAKED INTO the image — no overlay text needed          │  │
│  │    (numbered step, instructions, diagrams all in the image)           │  │
│  │                                                                       │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│   Image Container: 2px cyan border, 12px corners, dark navy backing, glow  │
│                                                                             │
│                                                                             │
│          ◀              1 / 3              ▶                               │
│       [PREV]      (Gameplay SDF, 34px)   [NEXT]                            │
│                                                                             │
│            (On LAST slide, NEXT becomes:)                                   │
│         ╔═══════════════════════════════════╗                               │
│         ║       [ CONTINUE ]  34px         ║                               │
│         ╚═══════════════════════════════════╝                               │
│           (2px Cyan Border, Gameplay SDF)                                   │
│                                                                             │
│  Navigation Behavior:                                                       │
│    • ◀ PREV: Goes to previous image (hidden on first slide)                │
│    • NEXT ▶: Goes to next image (visible on all except last slide)         │
│    • [CONTINUE]: Replaces NEXT on final slide only → starts minigame       │
│    • [SKIP ▶]: Skips all remaining slides → starts minigame immediately   │
│    • Swipe left/right: Natural mobile gesture navigation                    │
│    • No auto-advance timer — fully self-paced                              │
│                                                                             │
│  Image Asset Mapping from Assets/Arts/Tutorial/:                            │
│    • Minigame 1 (Capture & Focus) — 2 slides:                              │
│      - Slide 1/2: Minigame 1-1.png (Hold to raise green bar)              │
│      - Slide 2/2: Minigame 1-2.png (Release drops bar, fill meter)        │
│    • Minigame 2 (Reconstruction Scan) — 1 slide:                           │
│      - Slide 1/1: Minigame 2.png (Tap tiles to solve puzzle)              │
│    • Minigame 3 (Debris Cleanup) — 3 slides:                              │
│      - Slide 1/3: Minigame 3-1.png (Move joystick to move claw)           │
│      - Slide 2/3: Minigame 3-2.png (Tap button to drop claw)              │
│      - Slide 3/3: Minigame 3-3.png (Sort trash into correct bins)         │
│    • Minigame 4 (Hazard Dodge) — 3 slides:                                │
│      - Slide 1/3: Minigame 4-1.png (Swipe lanes to dodge)                 │
│      - Slide 2/3: Minigame 4-2.png (Collect pearls for RDP)               │
│      - Slide 3/3: Minigame 4-3.png (Avoid hits, 3 = hull compromised)    │
│                                                                             │
│  Image Container & Button Styling Synergy:                                  │
│    • Border: 2px solid cyan (#00E5FF)                                      │
│    • Fill: Deep navy translucent rgba(10, 22, 42, 0.85)                    │
│    • Corner Radius: 12px uniform rounded edges                              │
│    • Outer Glow: rgba(0, 229, 255, 0.15)                                   │
│    • Nav Arrows: 56×56px cyan touch targets                                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 5D. Skip Tutorial Button & Confirmation Prompt

```
┌────────────────────────────────────────────────────────────────────────┐
│                    SKIP TUTORIAL UI SPECIFICATIONS                     │
├────────────────────────────────────────────────────────────────────────┤
│                                                                        │
│  Locations:                                                            │
│    1. Top-Right Screen Corner: Floating subtle pill button             │
│       • Anchor: Top-Right (X: -160px, Y: -50px)                        │
│       • Size: 220px x 56px                                             │
│       • Font: Gameplay SDF, 32px, Normal weight                        │
│    2. Radio Textbox Header: Text button "[SKIP TUTORIAL]" (32px)       │
│    3. Pause Menu: Full-width button "[SKIP TUTORIAL]" (36px)           │
│                                                                        │
│  Confirmation Dialog (Optional / Instant Toggle):                      │
│    • Panel: 700px x 300px dark rounded container                       │
│    • Title: "SKIP TUTORIAL?" (38px, Gameplay SDF, Cyan)                │
│    • Message: "Jump straight into free exploration? All standard       │
│                submersible equipment will be calibrated." (34px)       │
│    • Button 1: [ RESUME TUTORIAL ] (32px, Muted Gray)                  │
│    • Button 2: [ SKIP NOW ] (32px, Cyan Accent)                        │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 6. Asset Requirements Checklist

### 6A. 3D & Scene Assets

| Asset | Status | Usage in Tutorial |
| :--- | :--- | :--- |
| **3D Submarine Model** | ✅ Available | Featured in the opening cinematic (Sunlight Zone orbit, cockpit dolly transition). |
| **Sunlight Zone Environment** | ✅ Implemented | Primary training arena (procedural coral reefs, sunbeam lighting). |
| **Tutorial Debris Cluster** | ✅ Code-driven | Auto-spawned on the seabed ~35m from player start via `DebrisSpawner` tutorial override. No manual scene placement needed. |
| **Tutorial Species (×2)** | ✅ Code-driven | Two distinct mobile species force-spawned 20–30m from player start via `SpeciesSpawner` tutorial override. No manual scene placement needed. |
| **Hazard Zone Trigger** | ✅ Code-driven | Automated by `TutorialManager` after Step 6 — directly calls `MinigameManager.TriggerHazardDodgeMinigame()`. Periodic surge disabled during tutorial. |

---

### 6B. UI & Sprite Assets (Including `Assets/Arts/Tutorial/`)

| Asset File | Path | Status | Usage |
| :--- | :--- | :--- | :--- |
| **Minigame 1-1.png** | `Assets/Arts/Tutorial/Minigame 1-1.png` | ✅ Exists | Slideshow Slide 1/2 for Step 4A (Hold to raise green bar). |
| **Minigame 1-2.png** | `Assets/Arts/Tutorial/Minigame 1-2.png` | ✅ Exists | Slideshow Slide 2/2 for Step 4A (Release drops bar, fill meter). |
| **Minigame 2.png** | `Assets/Arts/Tutorial/Minigame 2.png` | ✅ Exists | Slideshow Slide 1/1 for Step 4B (Tap tiles to solve puzzle). |
| **Minigame 3-1.png** | `Assets/Arts/Tutorial/Minigame 3-1.png` | ✅ Exists | Slideshow Slide 1/3 for Step 6 (Move joystick to move claw). |
| **Minigame 3-2.png** | `Assets/Arts/Tutorial/Minigame 3-2.png` | ✅ Exists | Slideshow Slide 2/3 for Step 6 (Tap button to drop claw). |
| **Minigame 3-3.png** | `Assets/Arts/Tutorial/Minigame 3-3.png` | ✅ Exists | Slideshow Slide 3/3 for Step 6 (Sort trash into correct bins). |
| **Minigame 4-1.png** | `Assets/Arts/Tutorial/Minigame 4-1.png` | ✅ Exists | Slideshow Slide 1/3 for Step 7 (Swipe lanes to dodge obstacles). |
| **Minigame 4-2.png** | `Assets/Arts/Tutorial/Minigame 4-2.png` | ✅ Exists | Slideshow Slide 2/3 for Step 7 (Collect pearls for bonus RDP). |
| **Minigame 4-3.png** | `Assets/Arts/Tutorial/Minigame 4-3.png` | ✅ Exists | Slideshow Slide 3/3 for Step 7 (Avoid hits, 3 = hull compromised). |
| **tutorial_arrow.png** | Procedural | ✅ Procedural | Cyan directional bouncing pointer (drawn via Unity UI Image rotation). |
| **radio_icon.png** | Procedural | ✅ Procedural | Speaker icon on radio transmission box. |

---

### 6C. Audio Assets

| Asset Name | Path | Status | Purpose |
| :--- | :--- | :--- | :--- |
| `radio static.mp3` | `Assets/Audio/radio static.mp3` | ✅ Exists | Radio static burst played at opening of each Dr. Santos transmission. |
| `radio chirp.mp3` | `Assets/Audio/radio chirp.mp3` | ✅ Exists | Radio open AND close tone (used for both). |
| `tutorial step chime.mp3` | `Assets/Audio/tutorial step chime.mp3` | ✅ Exists | Positive affirmation chime on step completion. |
| `species found beep.mp3` | `Assets/Audio/species found beep.mp3` | ✅ Exists | Reused for scan success during tutorial. |
| `debris found .mp3` | `Assets/Audio/debris found .mp3` | ✅ Exists | Reused for debris detection during tutorial. |

---

### 6D. Font & Typography Assets

| Asset Name | File Path | Usage Rules |
| :--- | :--- | :--- |
| **Gameplay SDF** | `Assets/TextMesh Pro/Fonts/Gameplay SDF.asset` | **Primary font for ALL radio comms dialogue, Dr. Santos transmissions, tutorial UI, and briefing overlays.**<br>• NO bold styling permitted.<br>• Minimum text size: **32px** everywhere. |
| **Antone DEMO SDF / Aloha Font** | `Assets/TextMesh Pro/Fonts/` | Standard HUD gauge numbers and general UI buttons. |

---

## 7. Implementation Timeline (3–4 Hours)

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    3–4 HOUR IMPLEMENTATION SCHEDULE                      │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  HOUR 1: Non-Blocking Radio UI with Gameplay SDF & Skip Feature          │
│  ├── [25 min] Build RadioTextboxUI.cs with procedural UI                 │
│  │            • Gameplay SDF font integration (`Gameplay SDF.asset`)     │
│  │            • Normal font weight enforcement (no bold)                 │
│  │            • Minimum 32px font size sizing checks                     │
│  │            • Embedded `[SKIP TUTORIAL]` button                        │
│  │            • Typewriter reveal coroutine                              │
│  └── [35 min] Refactor TutorialManager.cs state machine                  │
│               • Replace modal popups with non-blocking radio calls       │
│               • Implement `OnSkipTutorialClicked()` bypass flow          │
│               • Hook non-compliance nudge timers (20s, 40s, 60s)        │
│                                                                          │
│  HOUR 2: Image Slideshow Overlays & Dual-Scan Sequencing                │
│  ├── [25 min] Build MinigameInstructionOverlay.cs                        │
│  │            • Full-screen image slideshow (one image at a time)        │
│  │            • Page counter (1/3), ◀ PREV / NEXT ▶ arrows              │
│  │            • [CONTINUE] replaces NEXT on final slide                  │
│  │            • [SKIP ▶] top-right to skip remaining slides              │
│  ├── [20 min] Build TutorialArrowUI.cs                                   │
│  │            • Screen-space and world-space animated pointers           │
│  │            • Step 1 sequential HUD instrument pointers                │
│  │            • Step 3 Sonar color legend card (Cyan/Yellow/Orange/Red)  │
│  └── [15 min] Dual-Scan Logic (Steps 4A & 4B) & Bestiary Modals (Step 5) │
│               • Sequence two species encounters                          │
│               • Explains "There are two types of scanning"               │
│               • Bestiary 3D model modal & detail modal highlights        │
│                                                                          │
│  HOUR 3: Debris, Sunlight Hazard Run & Upgrade Shop                      │
│  ├── [20 min] Environmental Cleanup Step 6                               │
│  │            • Robotic arm + waste sorting with `[ CONTINUE ]` button   │
│  ├── [20 min] Hazard Encounter Step 7                                    │
│  │            • "DANGERS AHEAD" banner (turbulent current / seamount)    │
│  │            • Multiple objectives (dodge hazards + collect rare pearls)│
│  │            • `[ CONTINUE TO EVASIVE RUN ]` button                     │
│  └── [20 min] Shop Guidance Step 8 + Completion Reward (+100 RDP)        │
│                                                                          │
│  HOUR 4: Simplified Opening Cinematic & Polish                           │
│  ├── [25 min] CinematicSequencer.cs                                      │
│  │            • Sunlight Zone orbit around player's 3D Submarine model   │
│  │            • Cockpit camera dolly transition into first-person        │
│  │            • HUD instruments power-on sequence                        │
│  │            • Top-right `[SKIP TUTORIAL]` HUD trigger                  │
│  └── [25 min] Audio integration & playtest validation pass               │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 8. Feasibility Analysis

| Key Factor | Feasibility Verdict | Technical Enablers |
| :--- | :--- | :--- |
| **Non-Blocking Architecture** | High | Standard Unity Coroutines and canvas overlays avoid `Time.timeScale = 0`. |
| **Skip Tutorial & Fail-Safe Bypass** | High | `TutorialManager.cs` already contains `OnSkipTutorialClicked()` boilerplate; simple timer activates `[SKIP STEP]` if player stalls for 60s. |
| **Image Slideshow UI** | High | All 9 tutorial images already exist at `Assets/Arts/Tutorial/` with text baked in. Standard Unity UI `Image` + `Sprite` swap per slide, with `Button.onClick` for navigation. |
| **Self-Paced [CONTINUE] Buttons** | High | Replaces coroutine delay timer with standard Unity UI `Button.onClick` listener, making implementation simpler and more reliable. |
| **Gameplay SDF Integration** | High | Asset already exists at `Assets/TextMesh Pro/Fonts/Gameplay SDF.asset`; readily loaded via `Resources.Load` or direct serialized reference. |
| **3D Submarine Model Integration** | High | User has the 3D model ready; simple camera lerp path achieves the cinematic without heavy rig setups. |
| **Dual Scanning Tutorial** | High | `MinigameManager` already contains both `_mg1` and `_mg2` controllers; tutorial forces one on Encounter 1 and the other on Encounter 2. |
| **Sunlight Zone Hazard Safety** | High | `HazardDodgeMinigame` already supports customizable obstacles, 3 lanes, and glowing rare pearl pickups. |
| **Deferred Ending Sequence** | High | Postponing the ending sequence keeps scope tight, ensuring full focus on a flawless interactive opening and tutorial. |

---

## Appendix A: Dr. Santos Radio Script

*(All radio lines use Gameplay SDF, Normal weight, 34px body text, 32px objectives, no bold)*

```csharp
// STEP 0 — Mission Overview
"Welcome aboard, Researcher. This is Dr. Santos at the Benham Rise Marine Station. "
+ "Submersible diagnostics are green. You are currently deployed in the Sunlight Zone. "
+ "Your mission: document every marine species in the Philippine Sea "
+ "to build our Marine Biodiversity Database. Let's begin by testing your instruments."

// STEP 1 — HUD Orientation
"Your submarine's instrument panel is online. Let me walk you through your displays."

// STEP 2 — Navigation
"Time to take the helm. Drag across the viewport to steer your camera view, "
+ "and push the directional joystick to navigate forward."

// STEP 2 — Navigation Complete
"Thrusters responding crisply. You're handling the submersible like a pro."

// STEP 3 — Sonar System & Color Identification
"Keep an eye on your sonar radar. Different contact signatures emit distinct frequencies: "
+ "Cyan markers indicate marine species. Yellow markers pinpoint habitat landmarks. "
+ "Orange markers reveal collectible debris, and Red markers highlight dangerous hazard zones. "
+ "Steer toward the nearest cyan blip."

// STEP 3 — Sonar Approach Complete
"Contact visual confirmed! Target is within scanning distance."

// STEP 4A — First Species Encounter (Scanning Module A)
"You have a creature in your sights! There are two types of scanning modules on your submersible "
+ "depending on the specimen. Align the target in your reticle and tap SCAN."

// STEP 4A — Non-Compliance / Player Wandering Nudges
// (Nudge 1 - After 20 seconds of wandering)
"Researcher, check your sonar radar. There's a biological contact right near your heading."
// (Nudge 2 - After 40 seconds of wandering)
"I've highlighted the creature's coordinates on your HUD. Center your viewport to lock on."
// (Nudge 3 / Skip Step - After 60 seconds)
"Remote telemetry captured the specimen's biological signature. Let's proceed to the next system."

// STEP 4A — First Scan Complete
"First specimen logged! Excellent work. But our survey requires data on another nearby creature "
+ "to calibrate our secondary scanning protocol."

// STEP 4B — Second Species Encounter (Scanning Module B)
"Notice the different bio-signature on this second creature? Our automated scanner will switch "
+ "to the alternative scanning module. Lock on and initiate scan."

// STEP 4B — Second Scan Complete
"Both scanning modules calibrated! All species data has been automatically synced "
+ "with your on-board encyclopedia. Let's review the Bestiary."

// STEP 5 — Bestiary Marine Encyclopedia
"Open the Bestiary to review your documented specimens. Every discovered entry features two interactive views: "
+ "the 3D Model modal to inspect full creature anatomy, and the Detail Modal to view biological field photos, "
+ "scientific classifications, and ecological facts. If you ever feel lost or can't find a species, "
+ "the search clues on undiscovered entries will guide your expedition."

// STEP 5 — Bestiary Complete
"With 95 species across 5 depth zones, those search clues will be invaluable when exploring deeper waters."

// STEP 6 — Environmental Debris Cleanup
"Researcher, I'm detecting discarded ocean debris polluting the coral bed ahead. "
+ "Human pollution disrupts fragile marine life. Maneuver in close and deploy your robotic collection arm "
+ "to collect and sort the waste."

// STEP 6 — Debris Cleanup Complete
"Reef cleared! Removing debris revitalizes the ecosystem, increasing species encounter rates. "
+ "You've earned research funding bonus."

// STEP 7 — Hazard Encounter: Dangers Ahead
"⚠️ DANGERS AHEAD — Turbulent reef current and seamount terrain detected! "
+ "Engage emergency evasive navigation. Swipe left and right across lanes to dodge falling rocks and vortexes. "
+ "Collect floating rare pearls along the route to earn bonus research data!"

// STEP 7 — Hazard Complete (Win)
"Masterful evasive piloting! You navigated the turbulence and secured valuable survey data."

// STEP 7 — Hazard Complete (Hit Taken)
"Hull alarms silenced. Even experienced deep-sea pilots take impacts. "
+ "Reinforcing your hull at the Shop will increase your survival margin."

// STEP 8 — Submarine Upgrade Shop
"You've earned Research Data Points from species scanning, debris removal, and hazard navigation. "
+ "Tap the Shop icon to inspect submarine upgrades."

// STEP 8 — Shop Complete
"Keep upgrading your submersible as you descend. The Twilight Zone lies at 200 metres, "
+ "where pressure mounts and sunlight fades."

// TUTORIAL GRADUATION
"All systems verified and operational. Your training certification is complete, Researcher. "
+ "The Philippine Sea holds 95 species waiting to be discovered across 5 depth zones. "
+ "The deep ocean is calling. Dr. Santos out."

// TUTORIAL SKIPPED SIGN-OFF
"All systems good to go. Good luck out there, Researcher."
```

---

## Appendix B: Quick Reference — What Exists vs. What's New

| Component | Status | Required Action |
| :--- | :--- | :--- |
| `TutorialManager.cs` | ✅ Exists (772 lines) | **Refactor:** Convert from modal popups to non-blocking event-driven radio sequencer. Add 4-tier non-compliance timers. Wire `OnSkipTutorialClicked()` for instant bypass. |
| `UIManager.cs` | ✅ Exists | **Minor Edit:** Add RectTransform getters for tutorial arrow targets and Skip Tutorial HUD trigger. |
| `MinigameManager.cs` | ✅ Exists | **Minor Edit:** Hook `MinigameInstructionOverlay` with multi-image panel (`Arts/Tutorial/`) and `[ CONTINUE ]` button before minigame launch. |
| `ScannerSystem.cs` | ✅ Exists | **Minor Edit:** Fire `OnSpeciesScanned` event on scan victory. |
| `BestiaryManager.cs` | ✅ Exists | **Minor Edit:** Expose references for 3D inspection modal, detail modal, and search clues. |
| `HazardDodgeMinigame.cs`| ✅ Exists | **Minor Edit:** Verify Sunlight Zone hazards ("Dangers Ahead") and Rare Pearl rewards. |
| `RadioTextboxUI.cs` | 🆕 New Script | **Create:** Non-blocking radio box using **Gameplay SDF**, normal weight, ≥32px font, with `[SKIP]` and `[SKIP STEP]` fallback buttons. |
| `TutorialArrowUI.cs` | 🆕 New Script | **Create:** Animated screen-space and world-space pointers with ≥32px labels. |
| `MinigameInstructionOverlay.cs` | 🆕 New Script | **Create:** Full-screen image slideshow briefing. One image at a time from `Assets/Arts/Tutorial/` with page counter, `◀ PREV` / `NEXT ▶` arrows, `[SKIP ▶]`, and `[CONTINUE]` on last slide. No instruction text — all text baked into images. |
| `CinematicSequencer.cs` | 🆕 New Script | **Create:** Simplified ~15s cinematic: 3D submarine Sunlight Zone orbit, cockpit transition, HUD power-on, and skip trigger. |
