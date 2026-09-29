using System;
using UnityEngine;

/// <summary>
/// Stores the collection of 50 educational marine biodiversity facts categorized
/// across the 5 ocean depth zones of Hadal Descent.
/// </summary>
public static class LoadingScreenFacts
{
    // -----------------------------------------------------------------------
    // All 50 Educational Facts
    // -----------------------------------------------------------------------

    public static readonly string[] AllFacts = new string[]
    {
        // ── Zone 0: Sunlight Zone (Epipelagic: 0m – 200m) ──
        "Brightest Horizon: Powered by sunlight, the sunlight zone supports roughly 90% of all marine life, making it the ocean's primary biological powerhouse.",
        "Philippine Pride: The Philippine Sea sits within the Coral Triangle in the sunlight zone, an epicenter of biodiversity harboring over 500 species of reef-building corals.",
        "Speed Demon: While popular lore claims speeds over 100 km/h, research shows the Indo-Pacific Sailfish in the sunlight zone reaches realistic sprint bursts of 36–45 km/h (22–28 mph), fast enough to outmaneuver schooling prey without tearing its own muscles.",
        "Giant of the Shallows: The Gentle Whale Shark (Rhincodon typus) regularly gathers in shallow sunlight-zone bays like Donsol to filter feed on dense plankton and sergestid shrimp blooms.",
        "Solar Powered: Microscopic phytoplankton drifting in the sunlit epipelagic layer generate between 50% and 80% of the oxygen in Earth’s atmosphere.",
        "Sea Turtle Haven: Hawksbill sea turtles forage along shallow sunlight-zone coral reefs, feeding heavily on aggressive sea sponges to free up space for slow-growing corals.",
        "Camouflage Masters: Cuttlefish use specialized skin cells which are chromatophores, iridophores, and leucophores to alter their color, pattern, and physical 3D skin texture in milliseconds to blend into shallow reefs.",
        "Plastics Threat: Floating synthetic debris in the sunlight zone poses a severe hazard to juvenile sea turtles, which frequently mistake floating plastic bags for jellyfish and salps.",
        "Microscopic Engines: Single-celled dinoflagellates (zooxanthellae) live symbiotically within coral tissues in the sunlight zone, supplying up to 90% of the host coral's energy via photosynthesis.",
        "The Green Sea Turtle: Female green turtles navigate across thousands of miles of open sunlight-zone waters using geomagnetic imprinting to lay eggs on the exact beach where they hatched.",

        // ── Zone 1: Twilight Zone (Mesopelagic: 200m – 1,000m) ──
        "The Dim Boundary: Sunlight rapidly attenuates in the twilight zone until photosynthesis is impossible, forcing resident fauna to survive mainly on organic matter sinking from the surface.",
        "Counterillumination: Lanternfish utilize ventral, light-emitting photophores to match the color and intensity of faint downwelling skylight in the twilight zone, masking their silhouette from predators below.",
        "Marine Snow: Flakes of biological debris such as dead plankton, fecal matter, and mucus aggregates drift downward into the twilight zone, forming the primary nutritional base of the midwater food web.",
        "Mass Migration: Every night, trillions of twilight zone animals ascend to the ocean surface to feed under cover of darkness in the largest animal migration by biomass on Earth.",
        "Big Eyed Hunters: Deep-sea squid species in the twilight zone develop disproportionately massive eyes to detect scarce ambient light and pinpoint bioluminescent flashes in the gloom.",
        "Chambered Nautilus: Inhabiting Philippine deep-reef drop-offs down to 500 meters in the twilight zone, this ancient cephalopod regulates neutral buoyancy by adjusting fluid and gas inside its walled shell chambers.",
        "Barreleye Fish: Inhabiting the twilight zone, the bizarre Macropinna microstoma features a transparent, fluid-filled head canopy housing tubular green eyes that rotate from upward-pointing (spotting silhouettes) to forward-pointing (targeting food).",
        "Scaleless Wonders: Many twilight-zone fishes sport specialized, guanine-crystal mirrors or sleek, scaleless skin to reflect ambient blue-green light and blend into the dim open water.",
        "Biological Glow: Roughly 75% of all creatures living between the surface and 4,000 meters produce their own bioluminescent light for camouflage, signaling, or hunting.",
        "Microplastic Trap: Twilight-zone filter-feeders (such as giant larvaceans) ingest sinking microplastics, packaging them into rapidly sinking fecal pellets and discarded mucus houses.",

        // ── Zone 2: Midnight Zone (Bathypelagic: 1,000m – 4,000m) ──
        "Total Blackout: Solar rays cannot penetrate the midnight zone, where ambient water temperatures stay near freezing, hovering between 2°C and 4°C (35°F to 39°F).",
        "The Anglerfish Lure: Female ceratioid anglerfish in the midnight zone dangle a modified dorsal lure (esca) packed with luminous, symbiotic bacteria to draw inquisitive prey into striking distance.",
        "Vampire Squid: Despite its alarming name, the midnight-zone Vampyroteuthis infernalis does not drink blood; it deploys two long, sticky retractile filaments to collect drifting flakes of marine snow.",
        "Gulper Eel: The midnight-zone Eurypharynx pelecanoides possesses loosely hinged jaws and an elastic throat pouch capable of engulfing prey and water volumes larger than its own body.",
        "Sperm Whale Dives: Adult sperm whales can dive down to 2,000–3,000 meters into the midnight zone, holding their breath for over an hour to hunt deep-sea squid.",
        "Crushing Pressure: At 2,000 meters in the midnight zone, hydrostatic pressure exceeds 200 atmospheres (over 2,900 psi), enough to crush non-specialized, air-filled structures instantly.",
        "Glass Sponges: Anchored directly into bathypelagic sediment, hexactinellid sponges construct intricate, durable structural skeletons out of biogenic silica (natural glass).",
        "Dumbo Octopus: Deep-sea cirrate octopuses (Grimpoteuthis) flap paddle-like mantle fins above their eyes to hover and drift gracefully over the midnight-zone ocean floor.",
        "Slow Metabolism: Cold temperatures and sparse meals force midnight-zone animals to run exceptionally slow metabolic engines, conserving energy between rare feedings.",
        "Ghostly Predators: The midnight-zone Fangtooth fish (Anoplogaster cornuta) boasts the largest teeth relative to body size of any marine fish, requiring deep internal roof-of-mouth sockets to close its jaws.",

        // ── Zone 3: Abyss Zone (Abyssopelagic: 4,000m – 6,000m) ──
        "The Endless Plains: Covering roughly 50% of Earth’s surface (and the majority of the deep seafloor), the abyss zone consists predominantly of vast, sediment-covered abyssal plains.",
        "Hydrothermal Vents: Mineral-rich chimney spires superheated by sub-seafloor magma in the abyss zone support self-sustaining ecosystems that operate entirely independent of solar energy.",
        "Chemosynthesis: Specialized abyss-zone bacteria oxidize chemicals like hydrogen sulfide and methane to synthesize organic sugars, serving as the foundational producers of vent communities.",
        "Giant Isopods: Related to terrestrial pillbugs, abyssal scavengers like Bathynomus giganteus reach lengths of 30–40 cm (12–16 inches) due to deep-sea gigantism.",
        "Tripod Fish: The abyssal Bathypterois balances stationary atop three elongated, rigid fin rays, perching above soft seafloor mud facing upstream to intercept drifting zooplankton.",
        "Sea Cucumbers Rule: Deep-sea holothurians (such as transparent \"sea pigs\") are ecological vacuums that often account for 70% to over 90% of the living animal biomass on abyssal sediment.",
        "Bone Eaters: Red-plumed Osedax worms burrow root-like networks into sunken whale skeletons (whale falls) on the abyssal seafloor, relying on endosymbiotic bacteria to digest bone lipids and collagen.",
        "Durable Waste: Without ultraviolet sunlight, wave agitation, or warm water to accelerate breakdown, sunken aluminum and synthetic polymers remain largely intact in abyssal mud for centuries.",
        "Grenadier Fish: Also known as rattails, these active abyss-zone scavengers use acute chemosensory and olfactory systems to locate fallen carrion from kilometers away.",
        "Abyssal Gigantism: Cold temperatures, slow cell division, delayed sexual maturity, and metabolic selection for long-range foraging drive several abyss-zone species to grow far larger than their shallow-water relatives.",

        // ── Zone 4: Hadal Zone (Hadalpelagic: 6,000m – 11,000m) ──
        "Realm of Trenches: Named after the underworld realm of Hades, the hadal zone is confined entirely to V-shaped tectonic subduction zones, trenches, and deep structural troughs.",
        "Philippine Trench: Running along the eastern coast of the Philippine archipelago, the Philippine Trench plunges into the hadal zone to an extreme depth of roughly 10,540 meters at the Galathea Depth.",
        "Extreme Pressure: At depths past 8,000 meters in the hadal zone, water exerts over 800 atmospheres of hydrostatic pressure equivalent to the weight of an adult elephant focused onto a human thumb.",
        "Snailfish Supremacy: Hadal snailfishes (family Liparidae) are the deepest-living vertebrates known, producing massive amounts of the cellular stabilizer TMAO (trimethylamine N-oxide) to stop high pressure from distorting vital proteins.",
        "Amphipod Swarms: Gigantic, scavenging amphipods congregate in large colonies along hadal trench floors, shredding carrion and biological detritus funneling down the steep trench walls.",
        "Pliant Skeletons: Instead of having zero bone density, hadal snailfishes possess lightly calcified, flexible bones with unfused skull plates and water-rich, gelatinous flesh that equilibrates internal and external pressure.",
        "Trench Pollution: Deep submersibles visiting the Emden Deep in the hadal Philippine Trench have documented consumer plastic bags, food wrappers, and persistent chemical contaminants resting at the very bottom.",
        "The 8,400-Meter Barrier: Fish cannot physiologically survive deeper than approximately 8,400 meters in the hadal zone because the required cellular concentration of TMAO would make their bodily fluids hyperosmotic relative to seawater, disrupting cell function.",
        "Evolving Isolation: Deep hadal trenches act as inverted biogeographical islands; cut off by surrounding shallower abyssal sills, each trench houses distinct, highly endemic communities.",
        "Scale-Free Bodies: Living under thousands of pounds of pressure, hadal snailfishes have abandoned heavy scales in favor of soft, translucent, gelatinous tissue that flexes under extreme physical stress without fracturing."
    };

    // -----------------------------------------------------------------------
    // Retrieval API
    // -----------------------------------------------------------------------

    private static int _lastFactIndex = -1;

    /// <summary>
    /// Returns any of the 50 educational facts at random, avoiding immediate repeats.
    /// </summary>
    public static string GetRandomFact()
    {
        if (AllFacts == null || AllFacts.Length == 0)
            return "The ocean covers more than 70% of Earth's surface and holds over 97% of the planet's water.";

        int idx;
        if (AllFacts.Length == 1)
        {
            idx = 0;
        }
        else
        {
            do
            {
                idx = UnityEngine.Random.Range(0, AllFacts.Length);
            }
            while (idx == _lastFactIndex);
        }

        _lastFactIndex = idx;
        return AllFacts[idx];
    }

    /// <summary>
    /// Returns an educational fact tailored to the target depth zone (0 = Sunlight, 1 = Twilight, etc.).
    /// </summary>
    public static string GetFactForZone(int zoneIndex)
    {
        if (zoneIndex < 0 || zoneIndex > 4)
            return GetRandomFact();

        int start = zoneIndex * 10;
        int end   = start + 10;

        if (start < 0 || end > AllFacts.Length)
            return GetRandomFact();

        int idx = UnityEngine.Random.Range(start, end);
        _lastFactIndex = idx;
        return AllFacts[idx];
    }
}
