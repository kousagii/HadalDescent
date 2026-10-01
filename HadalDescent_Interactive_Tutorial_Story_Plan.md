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
   - 5C. [Self-Paced Minigame Briefing Overlay with Embedded Multi-Image Panel (`Arts/Tutorial`)](#5c-self-paced-minigame-briefing-overlay-with-embedded-multi-image-panel-artstutorial)
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

### 1B. Opening Cinematic Sequence (Sunlight Zone Start → Ocean Descent Preview → Cockpit Transition)

The game opens with an in-engine cinematic utilizing the **custom 3D Submarine model**. Rather than starting with an aerial surface shot, the sequence begins directly beneath the surface in the **Sunlight Zone**, previews the mysterious depths ahead, and seamlessly transitions into first-person gameplay:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        OPENING CINEMATIC SEQUENCE                          │
│                     (~35 seconds, non-interactive)                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  BEAT 1 — SUNLIGHT ZONE INTRODUCTION (Third Person, 5s)                    │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Camera orbits the player's 3D Submarine model cruising gracefully  │    │
│  │  through the vibrant Sunlight Zone (0–200m).                        │    │
│  │  Golden sunbeams penetrate the crystal turquoise water.             │    │
│  │  School of reef fish dart past the submarine hull.                  │    │
│  │  Text overlay fades in (Gameplay SDF, 36px, clean un-bolded):       │    │
│  │    "PHILIPPINE SEA — BENHAM RISE MARINE EXPEDITION"                 │    │
│  │    "BIODIVERSITY RESEARCH SUBMERSIBLE 'HADAL EXPLORER'"             │    │
│  │                                                                      │    │
│  │  [SKIP TUTORIAL] button visible in top-right corner from the start. │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  BEAT 2 — THE DESCENT PREVIEW (3 Seconds Per Zone, 12s total)              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  The camera tilts down and accelerates downward in a rapid preview  │    │
│  │  dive showcasing the five distinct ocean zones ahead:               │    │
│  │                                                                      │    │
│  │  • [0–3s] TWILIGHT ZONE (200–1,000m):                               │    │
│  │    Water shifts from turquoise to deep indigo. Faint silhouettes    │    │
│  │    of pelagic species and first bioluminescent flickers appear.     │    │
│  │                                                                      │    │
│  │  • [3–6s] MIDNIGHT ZONE (1,000–4,000m):                             │    │
│  │    Pitch-black water. Bioluminescent flashes from anglerfish and   │    │
│  │    lanternfish illuminate the submarine's exterior lights.          │    │
│  │                                                                      │    │
│  │  • [6–9s] ABYSSAL ZONE (4,000–6,000m):                              │    │
│  │    Submarine hull lights cut through freezing 4°C benthic plains;   │    │
│  │    silhouettes of deep-sea trenches and glass sponges emerge.       │    │
│  │                                                                      │    │
│  │  • [9–12s] HADAL ZONE (6,000–11,000m):                              │    │
│  │    The extreme abyss of the Philippine Trench. Extreme pressure     │    │
│  │    depth gauge vignette warning flashes briefly.                    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  BEAT 3 — RETURN & COCKPIT TRANSITION (Third → First Person, 8s)           │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  Rapid rewind/re-ascent back to the safe, tranquil Sunlight Zone.   │    │
│  │  Camera rotates around the 3D Submarine model and smoothly dollies   │    │
│  │  directly toward the forward cockpit viewport glass.                 │    │
│  │  Camera passes through the glass pane into the 1st-person view.      │    │
│  │  Cockpit HUD instruments initialize and power on:                   │    │
│  │    • Depth gauge flickers on at 18m                                 │    │
│  │    • Sonar radar begins rotating sweep                              │    │
│  │    • Reticle powers up with cyan diagnostic circle                   │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                                                             │
│  BEAT 4 — FIRST RADIO TRANSMISSION (First Person, Comms Active)            │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │  🔊 Audio: Radio static burst + crisp comms open beep tone          │    │
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
| **PROLOGUE** | **Deployment & Calibration** — Marine biologist arrives in the Philippine Sea aboard the research submersible. | In-engine 3D submarine cinematic → 3-second zone previews → cockpit transition → comms calibration with Dr. Santos. *(Option to skip tutorial available at any point).* |
| **ACT I** | **Sunlight Zone Expedition & Research Certification** — Dr. Santos mentors the researcher through core exploration protocols. | Interactive tutorial steps occur seamlessly without pausing gameplay. Player masters piloting, sonar reading, both scanning minigames (with self-paced `[CONTINUE]` briefings and embedded `Arts/Tutorial` images), Bestiary encyclopedia, debris cleanup, hazard survival, and upgrade shop. |
| **ACT II** | **The Descent into the Deep** — Player descends progressively through Twilight, Midnight, Abyssal, and Hadal zones. Hull upgrades are required to survive mounting hydrostatic pressure. | Radio transmissions from Dr. Santos become rarer and more atmospheric as the radio signal weakens with depth. Environmental storytelling through habitat landmarks and bioluminescence. |
| **ACT III** | **Final Cataloging & 100% Completion** — Cataloging the rarest hadal trench species to achieve complete database coverage (95/95 species). | Database reaches full completion. *(Note: Ending sequence cinematics and global research center upload sequence are deferred for future implementation).* |

---

### 1D. Skip Tutorial System (Player Agency & Instant Bypass)

To support returning players, playtesters, and experienced pilots, the tutorial can be skipped at **any time** through three distinct touchpoints:

1. **Top-Right HUD Skip Button:** A sleek `[ SKIP TUTORIAL ]` button is visible in the upper-right corner during the opening cinematic and throughout Steps 0–8.
2. **Radio Textbox Header:** A discrete `[ SKIP ]` button is embedded on the right side of the radio communication header next to Dr. Santos' title.
3. **Pause Menu Option:** A prominent `[ SKIP TUTORIAL ]` button appears inside the pause menu whenever the tutorial state is active.

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

#### What Happens When Tutorial is Skipped:
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
> **Rule #3: Self-Paced Overlays with Embedded Tutorial Art.**  
> Minigame instructions must NEVER disappear automatically on a timer. Each minigame briefing overlay embeds the official visual guides from `Assets/Arts/Tutorial/` (e.g., `Minigame 1-1.png`, `Minigame 1-2.png`, `Minigame 2.png`) inside containers that share the exact styling as the **[CONTINUE]** button (cyan border, dark navy panel, soft glow), letting players absorb the mechanics at their own pace.

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
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"Contact visual confirmed! Target is within scanning distance."*

---

#### STEP 4A — Species Encounter 1: First Scanning Module (Embedded Multi-Image Briefing)
- **Trigger:** Target creature enters the scanning reticle.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"You have a creature in your sights! There are two types of scanning modules on your submersible depending on the specimen. Align the target in your reticle and tap SCAN."*
- **UI Pointers:**
  - Arrow → **Center Reticle**: *"ALIGN CREATURE"* (32px label)
  - Arrow → **SCAN Button** (bottom HUD): *"TAP TO SCAN"* (32px label)
- **Self-Paced Minigame Briefing Overlay with Embedded Images (`Assets/Arts/Tutorial/`):**
  - **Embedded Images:** Features **two styled image panels** embedded side-by-side inside the card:
    - Left Panel: `Minigame 1-1.png` (holding screen to raise bar)
    - Right Panel: `Minigame 1-2.png` (keeping creature centered in green zone)
    - *Image Styling:* Each image is framed in a container styled identically to the Continue button (2px cyan border `#00E5FF`, dark navy backing, 12px rounded corners, soft glow).
  - **Instruction Text (Gameplay SDF, 34px):**  
    *"Hold touch anywhere to raise the focus bar. Keep the swimming creature centered inside the green zone until the scan completes!"*
  - **Action Button:** `[ CONTINUE TO SCAN ]` (34px, Cyan accent). Tapping button closes briefing and begins the minigame.
- **Objective Line (32px, green):** `OBJECTIVE: Align creature in reticle and complete scan`
- **Non-Compliance Safeguard:** If player ignores for >60s, `[ SKIP STEP ]` option appears on the radio textbox.
- **Advance Condition:** Player completes Minigame 1.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"First specimen logged! Excellent work. But our survey requires data on another nearby creature to calibrate our secondary scanning protocol."*

---

#### STEP 4B — Species Encounter 2: Second Scanning Module (Embedded Image Briefing)
- **Context:** To ensure the player is trained on both randomized scanning minigames, the tutorial spawns or highlights a second distinct species nearby.
- **Trigger:** Second creature comes into proximity.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Notice the different bio-signature on this second creature? Our automated scanner will switch to the alternative scanning module. Lock on and initiate scan."*
- **Self-Paced Minigame Briefing Overlay with Embedded Image:**
  - **Embedded Image:** Displays `Minigame 2.png` from `Assets/Arts/Tutorial/` embedded in the center with the signature cyan button border styling.
  - **Instruction Text (Gameplay SDF, 34px):**  
    *"Drag and connect biological scan fragments into the creature grid to reconstruct its profile!"*
  - **Action Button:** `[ CONTINUE TO SCAN ]` (34px, Cyan accent). No timed fadeout.
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

#### STEP 6 — Environmental Debris Cleanup (Self-Paced with [CONTINUE])
- **Trigger:** Player approaches within 30 metres of a guaranteed tutorial `DebrisCluster`.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"Researcher, I'm detecting discarded ocean debris polluting the coral bed ahead. Human pollution disrupts fragile marine life. Maneuver in close and deploy your robotic collection arm to collect and sort the waste."*
- **UI Pointers:**
  - World-space 3D arrow floating directly over the physical debris cluster in the ocean.
  - Screen-space arrow → **INTERACT Button** (bottom HUD): *"TAP TO DEPLOY ARM"* (32px label)
- **Self-Paced Minigame Briefing Overlay (Gameplay SDF, min 32px):**  
  - Instructions: *"Operate the robotic claw to extract debris. Drag items into the correct sorting bins: Plastic, Metal, or Hazardous!"*
  - Action Button: `[ CONTINUE TO CLEANUP ]` (34px, Cyan accent). No timer.
- **Objective Line (32px, green):** `OBJECTIVE: Approach debris and tap INTERACT to sort waste`
- **Advance Condition:** `EnvironmentalCleanupMinigame` completes successfully.
- **Completion Radio (Gameplay SDF, 34px, no bold):**  
  *"Reef cleared! Removing debris revitalizes the ecosystem, increasing species encounter rates. You've earned research funding bonus."*

---

#### STEP 7 — Hazard Encounter: "Dangers Ahead" (Self-Paced with [CONTINUE])
- **Context:** In the Sunlight Zone (0–200m), there are **no thermal vent plumes**. Hazards consist of turbulent reef currents, seamount terrain, sharp coral spires, and falling reef rockfall. Additionally, Minigame 4 features **multiple objectives**: surviving the hazard course while collecting valuable floating **Rare Pearls** dislodged by the ocean turbulence.
- **Trigger:** Scripted hazard zone placed ahead on the player's path.
- **Radio Dialogue (Gameplay SDF, 34px, no bold):**  
  *"⚠️ DANGERS AHEAD — Turbulent reef current and seamount terrain detected! Engage emergency evasive navigation. Swipe left and right across lanes to dodge falling rocks and vortexes. Collect floating rare pearls along the route to earn bonus research data!"*
- **Self-Paced Minigame Briefing Overlay (Gameplay SDF, min 32px):**  
  - Instructions:  
    *"← SWIPE LEFT / RIGHT TO DODGE HAZARDS →"*  
    *"COLLECT RARE PEARLS FOR BONUS RDP!"*
  - Dual Objectives clearly enumerated:
    1. Survive 3 navigation lanes down to 0 metres without depleting 3 Hull Integrity pips.
    2. Collect floating glowing Rare Pearls (+10 to +25 RDP each).
  - Action Button: `[ CONTINUE TO EVASIVE RUN ]` (34px, Red/Cyan accent). No timer.
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
| **MinigameInstructionOverlay.cs** | `Assets/Scripts/UI/` | Self-paced overlay embedding images from `Assets/Arts/Tutorial/` inside matching button-styled containers, with diagram, instructions (Gameplay SDF, 32px+), and a prominent **`[ CONTINUE ]`** button to proceed. Supports multi-image layouts. | ~160 |
| **CinematicSequencer.cs** | `Assets/Scripts/Cinematics/` | Controls the 3D submarine model camera orbit in Sunlight Zone, 3-second zone descent previews, cockpit camera dolly, and top-right `[ SKIP TUTORIAL ]` button. | ~190 |

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
│    • Radio static sound burst triggers on box appearance               │
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

### 5C. Self-Paced Minigame Briefing Overlay with Embedded Multi-Image Panel (`Arts/Tutorial`)

The briefing overlay features embedded tutorial artwork located in `Assets/Arts/Tutorial/`. Each embedded image container uses the **exact visual styling of the Continue button** (2px cyan border, dark navy translucent fill, rounded corners, soft glow) to create a cohesive, high-tech interface.

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│             SELF-PACED MINIGAME BRIEFING OVERLAY (MULTI-IMAGE EMBEDDED)          │
├──────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│  Anchoring: Center screen overlay (non-blocking pause while reading)             │
│  Background: Semi-transparent dark vignette (0.02, 0.04, 0.08, 0.90)             │
│  Dimensions: 1000px width x 620px height                                         │
│                                                                                  │
│  ┌────────────────────────────────────────────────────────────────────────────┐  │
│  │  SCANNING PROTOCOL: CAPTURE & FOCUS                                        │  │
│  │  ────────────────────────────────────────────────────────────────────────  │  │
│  │                                                                            │  │
│  │   EMBEDDED IMAGE CONTAINERS (MATCHING CONTINUE BUTTON STYLING):            │  │
│  │   ┌───────────────────────────┐      ┌───────────────────────────┐         │  │
│  │   │ 2px Cyan Border (#00E5FF) │      │ 2px Cyan Border (#00E5FF) │         │  │
│  │   │ Deep Navy Backing (0.08)  │      │ Deep Navy Backing (0.08)  │         │  │
│  │   │ 12px Rounded Corners      │      │ 12px Rounded Corners      │         │  │
│  │   │                           │      │                           │         │  │
│  │   │    [ Minigame 1-1.png ]   │      │    [ Minigame 1-2.png ]   │         │  │
│  │   │    Hold to raise bar      │      │    Keep creature centered │         │  │
│  │   │                           │      │                           │         │  │
│  │   └───────────────────────────┘      └───────────────────────────┘         │  │
│  │            STEP 1: ASCEND                     STEP 2: FOCUS                │  │
│  │                                                                            │  │
│  │  "Hold touch anywhere to raise the focus bar. Keep the swimming creature   │  │
│  │   centered inside the green zone until data capture reaches 100%!"         │  │
│  │                                                                            │  │
│  │                  ╔═════════════════════════════════════╗                   │  │
│  │                  ║         [ CONTINUE TO SCAN ]        ║                   │  │
│  │                  ╚═════════════════════════════════════╝                   │  │
│  │                     (2px Cyan Border, 34px Gameplay SDF)                   │  │
│  └────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                  │
│  Image Asset Mapping from Assets/Arts/Tutorial/:                                 │
│    • Minigame 1 (Capture & Focus):                                               │
│      - Image 1: Assets/Arts/Tutorial/Minigame 1-1.png (Raising the green bar)    │
│      - Image 2: Assets/Arts/Tutorial/Minigame 1-2.png (Holding creature in focus)│
│    • Minigame 2 (Reconstruction Scan):                                           │
│      - Image: Assets/Arts/Tutorial/Minigame 2.png (Connecting scan puzzle tiles) │
│    • Minigame 3 (Debris Cleanup):                                                │
│      - Procedural / custom diagram of claw grab and 3 sorting bins               │
│    • Minigame 4 (Hazard Dodge):                                                  │
│      - Procedural / custom 3-lane dodge diagram with glowing Rare Pearl badges   │
│                                                                                  │
│  Container & Button Styling Synergy:                                             │
│    • Border: 2px solid cyan (#00E5FF) on both the image containers & button     │
│    • Fill: Deep navy translucent `rgba(10, 22, 42, 0.85)`                       │
│    • Corner Radius: 12px–14px uniform rounded edges                              │
│    • Hover/Active Bloom: Soft cyan emissive glow (0.00, 0.90, 1.00, 0.35)        │
│                                                                                  │
└──────────────────────────────────────────────────────────────────────────────────┘
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
| **3D Submarine Model** | ✅ Available | Featured prominently in the opening cinematic (Sunlight Zone orbit, 3s zone preview descent, camera dolly into cockpit). |
| **Sunlight Zone Environment** | ✅ Implemented | Primary training arena (procedural coral reefs, sunbeam lighting). |
| **Tutorial Debris Cluster** | ✅ Configured | Spawned 35m from starting position for Step 6 cleanup. |
| **Tutorial Species Spawners** | ✅ Configured | Two distinct species guaranteed near spawn point for Steps 4A & 4B. |
| **Scripted Hazard Trigger** | ✅ Configured | Sunlight zone turbulence and coral rockfall corridor for Step 7. |

---

### 6B. UI & Sprite Assets (Including `Assets/Arts/Tutorial/`)

| Asset File | Path | Status | Usage |
| :--- | :--- | :--- | :--- |
| **Minigame 1-1.png** | `Assets/Arts/Tutorial/Minigame 1-1.png` | ✅ Exists | Embedded in Step 4A briefing overlay (Step 1: Ascend bar). |
| **Minigame 1-2.png** | `Assets/Arts/Tutorial/Minigame 1-2.png` | ✅ Exists | Embedded in Step 4A briefing overlay (Step 2: Center creature). |
| **Minigame 2.png** | `Assets/Arts/Tutorial/Minigame 2.png` | ✅ Exists | Embedded in Step 4B briefing overlay (Reconstruction puzzle tiles). |
| **tutorial_arrow.png** | `Assets/Arts/UI/` (or procedural) | ✅ Procedural | Cyan directional bouncing pointer. |
| **radio_icon.png** | `Assets/Arts/UI/` | ✅ Implemented | Speaker icon on radio transmission box. |

---

### 6C. Audio Assets

| Asset Name | Format | Duration | Purpose |
| :--- | :--- | :--- | :--- |
| `radio_static_burst.wav` | WAV | 0.6s | Played at the opening of each Dr. Santos transmission. |
| `radio_comms_open.wav` | WAV | 0.2s | High-tech comms open chirp. |
| `radio_comms_close.wav` | WAV | 0.2s | High-tech comms close tone. |
| `tutorial_step_chime.wav` | WAV | 0.8s | Subtle positive affirmation on step completion. |
| `data_pod_pickup.wav` / `pearl_pickup.wav` | WAV | 0.4s | Rewarding acoustic ping when collecting glowing rare pearls during hazard run. |

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
│  HOUR 2: Embedded Multi-Image Overlays & Dual-Scan Sequencing            │
│  ├── [25 min] Build MinigameInstructionOverlay.cs                        │
│  │            • Embed images from `Assets/Arts/Tutorial/`                │
│  │            • Multi-image container layout (Minigame 1-1 + 1-2)        │
│  │            • Match container styling with `[ CONTINUE ]` button       │
│  │            • Self-paced reading (no auto-fade timer)                  │
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
│  HOUR 4: Opening Cinematic with 3D Submarine & Polish                    │
│  ├── [35 min] CinematicSequencer.cs                                      │
│  │            • Sunlight Zone orbit around player's 3D Submarine model   │
│  │            • 3-second descent preview across ocean zones              │
│  │            • Cockpit camera dolly transition into first-person        │
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
| **Embedded Multi-Image UI** | High | Images already exist at `Assets/Arts/Tutorial/` (`Minigame 1-1.png`, `Minigame 1-2.png`, `Minigame 2.png`). Standard Unity UI `Image` elements in horizontal layout with matching button border material. |
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
| `MinigameInstructionOverlay.cs` | 🆕 New Script | **Create:** Self-paced briefing overlay embedding images from `Assets/Arts/Tutorial/` inside matching button-styled containers, with prominent **`[ CONTINUE ]`** button (no timer), Gameplay SDF, ≥32px text. |
| `CinematicSequencer.cs` | 🆕 New Script | **Create:** 3D submarine Sunlight Zone orbit, 3s zone preview descent, cockpit transition, and skip trigger. |
