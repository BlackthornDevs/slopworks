#!/usr/bin/env python3
"""
Bible art generator for Slopworks game bible.
Generates Gemini-powered concept art and item icons for all 189 bible entries.

Two image types:
  - Icons (square 1:1): item renderings on dark industrial background
  - Hero concept art (16:9): full cinematic scenes for key entries

Usage:
    export GOOGLE_API_KEY="your-key-here"
    python3 tools/generate-bible-art.py              # generate all missing images
    python3 tools/generate-bible-art.py --force       # regenerate everything
    python3 tools/generate-bible-art.py --only fauna   # only one category/subcategory
    python3 tools/generate-bible-art.py --dump         # print prompts, don't call API
    python3 tools/generate-bible-art.py --heroes-only  # only hero concept art pieces
"""

import os
import sys
import time
from pathlib import Path

# Import shared bible utilities
sys.path.insert(0, str(Path(__file__).parent))
from bible_common import (
    collect_all_entries, profile_to_prompt, VISUAL_IDENTITY,
    CATEGORIES, SUBCATEGORY_NAMES,
)

MODEL = "gemini-3-pro-image-preview"
OUTDIR = Path(__file__).parent.parent / "docs" / "assets" / "img" / "bible"

# --- Hero entries: these get full 16:9 cinematic concept art ---

HERO_ENTRIES = {
    # Weapons (all 6)
    "test_rifle", "salvage_pistol", "pipe_shotgun", "rebar_club", "arc_welder", "assault_rifle",
    # Boss/notable fauna
    "tower_boss", "hive_queen", "biomech_hybrid", "grunt", "spore_crawler", "pack_runner",
    # Machines
    "smelter_t1", "assembler_t1", "generator_t1",
    # Defenses
    "auto_turret_t1", "flamethrower_turret_t1",
    # Biomes (all 6)
    "Grassland", "Forest", "Wasteland", "Swamp", "Ruins", "OvergrownRuins",
    # Buildings
    "power_plant_alpha", "foundry_central",
    # Key items
    "power_cell", "key_fragment", "boss_blueprint",
    # NPCs
    "slop_terminal",
}

# --- Background themes by category ---

CATEGORY_BACKGROUNDS = {
    "items": {
        "style": "dark industrial workbench surface",
        "surface": "scarred metal workbench with tool marks, scattered bolts, faint oil stains",
        "ambient": "dim overhead shop light casting soft downward glow, dark vignette at edges",
    },
    "buildables": {
        "style": "factory floor setting",
        "surface": "cracked concrete factory floor with yellow safety line fragments, dust motes",
        "ambient": "diffuse industrial overhead lighting, faint sparks in background, shadowy machinery silhouettes",
    },
    "systems": {
        "style": "abstract circuit board pattern",
        "surface": "dark matte background with faint etched circuit traces in amber, subtle grid pattern",
        "ambient": "soft amber backlighting from circuit traces, cool blue rim light, technical blueprint feel",
    },
    "world": {
        "style": "environmental vista",
        "surface": "fading into distant landscape at edges, terrain and sky visible",
        "ambient": "golden hour atmospheric haze, volumetric light shafts, natural outdoor lighting",
    },
    "characters": {
        "style": "portrait backdrop",
        "surface": "dark mottled industrial wall with peeling paint, faint SLOP signage fragments",
        "ambient": "dramatic side lighting, warm key light, cool fill, shallow depth of field feel",
    },
}

# Fallback for unknown categories
DEFAULT_BACKGROUND = {
    "style": "dark industrial surface",
    "surface": "dark metal plate with subtle rivets and wear scratches",
    "ambient": "dim neutral lighting with soft vignette",
}


# --- Profile builders ---

def build_icon_profile(entry):
    """Build an object-focused icon profile for a bible entry."""
    entry_id = entry["_id"] or entry.get("displayName", "unknown")
    display_name = entry.get("displayName", entry_id)
    description = entry.get("description", "")
    model_style = entry.get("modelStyle", "")
    category = entry["_category"]
    subcategory = entry["_subcategory"]

    # Determine the subject description
    if model_style:
        subject = model_style
    elif description:
        # Use first sentence of description as subject
        first_sentence = description.split(".")[0].strip()
        subject = first_sentence
    else:
        subject = f"{display_name} from a post-apocalyptic factory survival game"

    # Pick background based on category
    bg = CATEGORY_BACKGROUNDS.get(category, DEFAULT_BACKGROUND)

    # Determine framing and angle based on subcategory
    framing, angle, scale = _icon_framing(subcategory)

    profile = {
        "title": f"{display_name} — item icon",
        "object": {
            "subject": subject,
            "framing": framing,
            "angle": angle,
            "scale": scale,
            "condition": "worn, scratched, industrial patina — clearly post-apocalyptic salvage",
        },
        "background": bg,
        "lighting": {
            "key_light": "warm overhead directional light from upper left",
            "fill": "soft ambient fill to preserve shadow detail",
            "rim": "subtle cool rim light on edges for separation from background",
            "mood": "utilitarian, gritty, industrial catalog feel",
        },
        "details": [
            "square aspect ratio (1:1)",
            "clearly identifiable at small sizes",
            "no text",
            f"category: {SUBCATEGORY_NAMES.get(subcategory, subcategory)}",
        ],
    }

    return profile


def _icon_framing(subcategory):
    """Return (framing, angle, scale) tuple based on subcategory."""
    framings = {
        # items
        "raw-materials": ("centered on surface", "three-quarter top-down view", "fills 60% of frame"),
        "weapons": ("centered, laid diagonally", "three-quarter view from above", "fills 80% of frame"),
        "wearables": ("centered, displayed flat", "front-facing slightly angled", "fills 70% of frame"),
        "equipment": ("centered on surface", "three-quarter view", "fills 70% of frame"),
        "consumables": ("centered, upright", "eye-level three-quarter view", "fills 60% of frame"),
        "tower-loot": ("centered on surface, slightly glowing", "three-quarter top-down view", "fills 65% of frame"),
        "lore-items": ("centered, slightly worn", "three-quarter view from above", "fills 65% of frame"),
        # buildables
        "machines": ("centered on floor", "isometric three-quarter view", "fills 75% of frame"),
        "defenses": ("centered on floor", "isometric three-quarter view", "fills 75% of frame"),
        "structural": ("centered, modular piece", "isometric three-quarter view", "fills 70% of frame"),
        "scenery": ("centered in environment", "eye-level three-quarter view", "fills 70% of frame"),
        # systems
        "recipes": ("schematic diagram style", "flat top-down view", "fills 80% of frame"),
        "research-tree": ("node diagram style", "flat front-facing view", "fills 70% of frame"),
        "upgrades": ("centered modification part", "three-quarter view", "fills 60% of frame"),
        "status-effects": ("abstract icon style", "front-facing flat", "fills 50% of frame, centered"),
        "wave-events": ("scene vignette style", "wide establishing angle", "fills 80% of frame"),
        # world
        "biomes": ("landscape thumbnail", "panoramic wide angle", "fills entire frame"),
        "reclaimed-buildings": ("building exterior", "isometric three-quarter view", "fills 80% of frame"),
        "environmental-hazards": ("hazard symbol with scene", "front-facing dramatic angle", "fills 70% of frame"),
        "supply-lines": ("route map style", "top-down diagram view", "fills 75% of frame"),
        # characters
        "fauna": ("creature portrait", "three-quarter view, slightly below eye level", "fills 80% of frame"),
        "npcs": ("portrait bust", "front-facing three-quarter turn", "head and shoulders fill 80% of frame"),
        "slop-dialogue": ("speech bubble icon style", "flat front-facing", "fills 50% of frame"),
        "narrative-progression": ("scene vignette style", "cinematic wide angle", "fills 80% of frame"),
    }
    return framings.get(subcategory, ("centered", "three-quarter view", "fills 70% of frame"))


def build_hero_profile(entry):
    """Build a scene-focused hero concept art profile for a bible entry."""
    entry_id = entry["_id"] or entry.get("displayName", "unknown")
    display_name = entry.get("displayName", entry_id)
    description = entry.get("description", "")
    model_style = entry.get("modelStyle", "")
    subcategory = entry["_subcategory"]

    # Route to specialized hero builders
    if subcategory == "weapons":
        return _hero_weapon(entry_id, display_name, description, model_style)
    elif subcategory == "fauna":
        return _hero_fauna(entry_id, display_name, description, model_style)
    elif subcategory == "biomes":
        return _hero_biome(entry_id, display_name, description)
    elif subcategory == "machines":
        return _hero_machine(entry_id, display_name, description, model_style)
    elif subcategory == "defenses":
        return _hero_defense(entry_id, display_name, description, model_style)
    elif subcategory == "npcs":
        return _hero_npc(entry_id, display_name, description, model_style)
    else:
        return _hero_default(entry_id, display_name, description, model_style)


def _hero_weapon(entry_id, display_name, description, model_style):
    """Weapon hero: action pose with worker wielding the weapon."""
    weapon_desc = model_style if model_style else description.split(".")[0]
    return {
        "title": f"{display_name} — weapon concept art",
        "scene": {
            "subject": f"a factory worker wielding {display_name}: {weapon_desc}",
            "composition": "dynamic action shot, 16:9 widescreen, subject left-of-center",
            "camera": "low angle looking up at the worker, wide-angle lens feel",
            "focal_point": f"the weapon ({display_name}) in the worker's hands, mid-action",
        },
        "characters": [
            {
                "role": "factory worker",
                "appearance": "patched coveralls, hard hat with mounted flashlight, welding goggles pushed up, determined expression",
                "posture": f"action stance wielding the {display_name}, braced for combat",
            },
        ],
        "environment": {
            "setting": "ruined factory interior with broken machinery and rubble",
            "time_of_day": "interior, lit by industrial work lights and muzzle flash",
            "decay": "crumbling concrete walls, exposed rebar, puddles on floor",
        },
        "lighting": {
            "key_light": "harsh directional light from overhead work lamp",
            "fill": "warm orange bounce from sparks or muzzle flash",
            "rim": "cool blue backlight from broken skylight",
            "mood": "tense, combat-ready, gritty",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            f"weapon details: {weapon_desc}",
            "post-apocalyptic improvised weapon aesthetic",
        ],
    }


def _hero_fauna(entry_id, display_name, description, model_style):
    """Fauna hero: creature portrait with biomechanical details."""
    creature_desc = model_style if model_style else description.split(".")[0]
    return {
        "title": f"{display_name} — creature concept art",
        "scene": {
            "subject": f"{display_name}: {creature_desc}",
            "composition": "dramatic creature portrait, 16:9 widescreen, creature dominating frame",
            "camera": "slightly below eye level looking up, medium-close shot",
            "focal_point": f"the {display_name}'s face and most threatening feature",
        },
        "creature_design": {
            "base_form": creature_desc,
            "biomechanical_elements": "visible machine parts fused with organic tissue — gears in joints, wiring through muscle, metal plating grown over with skin",
            "surface_texture": "mix of organic skin/chitin and corroded industrial metal",
            "eyes": "bioluminescent blue-green glow",
        },
        "environment": {
            "setting": "the creature's natural habitat in the ruined factory complex",
            "time_of_day": "dim, atmospheric, the creature partially emerging from shadow",
            "decay": "industrial ruins overgrown with mutant flora, the creature at home in the wreckage",
        },
        "lighting": {
            "key_light": "dramatic side light revealing texture and form",
            "fill": "bioluminescent glow from creature's own markings",
            "rim": "strong cool backlight for silhouette separation",
            "mood": "threatening, alien, awe-inspiring",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            f"creature: {creature_desc}",
            "biomechanical fusion of organic and industrial",
            "threatening posture, ready to attack",
        ],
    }


def _hero_biome(entry_id, display_name, description):
    """Biome hero: panoramic landscape vista."""
    # Use first two sentences of description for the vista
    sentences = [s.strip() for s in description.split(".") if s.strip()]
    vista_desc = ". ".join(sentences[:2]) if sentences else display_name

    return {
        "title": f"{display_name} biome — landscape concept art",
        "scene": {
            "subject": f"panoramic vista of the {display_name} biome: {vista_desc}",
            "composition": "wide panoramic landscape, 16:9 widescreen, horizon at lower third",
            "camera": "elevated vantage point looking across the terrain, wide-angle lens",
            "focal_point": "a distant point of interest — ruins, a factory tower, or a creature silhouette",
        },
        "environment": {
            "setting": f"{display_name} biome in the post-apocalyptic overworld",
            "time_of_day": "golden hour, long shadows and warm atmospheric haze",
            "terrain": vista_desc,
            "scale": "vast, stretching to the horizon, factory complex silhouettes in the far distance",
        },
        "lighting": {
            "key_light": "golden hour sunlight from frame left, long dramatic shadows",
            "fill": "warm atmospheric haze softening distant details",
            "rim": "golden light catching edges of terrain features and vegetation",
            "mood": "beautiful desolation, awe-inspiring scale, lonely exploration",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "panoramic landscape composition",
            "post-apocalyptic nature reclaiming industrial ruins",
            "sense of vast explorable space",
        ],
    }


def _hero_machine(entry_id, display_name, description, model_style):
    """Machine hero: the machine in full operation in a factory setting."""
    machine_desc = model_style if model_style else description.split(".")[0]
    return {
        "title": f"{display_name} — machine concept art",
        "scene": {
            "subject": f"{display_name} in full operation: {machine_desc}",
            "composition": "cinematic medium shot, 16:9 widescreen, machine center-frame",
            "camera": "slightly low angle, isometric feel, showing full machine and surroundings",
            "focal_point": f"the {display_name} actively processing materials, sparks or glow visible",
        },
        "environment": {
            "setting": "player-built factory floor in a reclaimed industrial ruin",
            "time_of_day": "interior, lit by the machine's own glow and overhead work lights",
            "surroundings": "conveyor belts feeding into the machine, stacked materials nearby, other machines in background",
        },
        "lighting": {
            "key_light": "warm orange glow from the machine's active process (smelting, welding, etc.)",
            "fill": "dim ambient factory lighting",
            "rim": "cool blue from distant skylights or broken ceiling",
            "mood": "productive, industrial, the factory coming back to life",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            f"machine details: {machine_desc}",
            "conveyor belts and automation visible",
            "the machine should look improvised but functional",
        ],
    }


def _hero_defense(entry_id, display_name, description, model_style):
    """Defense hero: turret or defense structure in action during a wave."""
    defense_desc = model_style if model_style else description.split(".")[0]
    return {
        "title": f"{display_name} — defense concept art",
        "scene": {
            "subject": f"{display_name} firing at incoming creatures: {defense_desc}",
            "composition": "dynamic action shot, 16:9 widescreen, defense structure left-of-center with targets approaching from right",
            "camera": "low angle behind the defense, looking outward at the threat",
            "focal_point": f"the {display_name} actively firing, muzzle flash or flame visible",
        },
        "environment": {
            "setting": "factory perimeter wall during a creature wave attack at dusk",
            "time_of_day": "dusk, dark sky with the last orange glow on the horizon",
            "surroundings": "makeshift barricades, sandbags, other defenses in a defensive line",
        },
        "lighting": {
            "key_light": "muzzle flash and tracer fire from the defense",
            "fill": "warm amber from factory lights behind the defense line",
            "rim": "bioluminescent glow from approaching creatures in the darkness",
            "mood": "intense, desperate, last-stand energy",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            f"defense details: {defense_desc}",
            "creature silhouettes approaching in the background",
            "spent casings or fuel canisters visible",
        ],
    }


def _hero_npc(entry_id, display_name, description, model_style):
    """NPC hero: character portrait in their environment."""
    npc_desc = model_style if model_style else description.split(".")[0]
    return {
        "title": f"{display_name} — character concept art",
        "scene": {
            "subject": f"{display_name}: {npc_desc}",
            "composition": "character portrait with environment, 16:9 widescreen, subject right-of-center",
            "camera": "eye-level medium shot, shallow depth of field feel",
            "focal_point": f"the {display_name}'s face and most distinctive feature",
        },
        "environment": {
            "setting": "the character's location within the factory complex",
            "time_of_day": "interior, moody directional lighting",
            "surroundings": "their personal workspace or habitat, showing personality through environment",
        },
        "lighting": {
            "key_light": "warm directional light from a work lamp or CRT monitor glow",
            "fill": "dim ambient, deep shadows preserved",
            "rim": "subtle cool backlight for separation",
            "mood": "mysterious, characterful, inviting conversation",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            f"character: {npc_desc}",
            "personality visible through posture and environment",
            "post-apocalyptic but with personal touches",
        ],
    }


def _hero_default(entry_id, display_name, description, model_style):
    """Default hero: cinematic scene with the subject in factory context."""
    subject_desc = model_style if model_style else (description.split(".")[0] if description else display_name)
    return {
        "title": f"{display_name} — concept art",
        "scene": {
            "subject": f"{display_name}: {subject_desc}",
            "composition": "cinematic scene, 16:9 widescreen, subject center-frame with factory context",
            "camera": "three-quarter view, medium-wide shot",
            "focal_point": f"the {display_name} as the clear center of attention",
        },
        "environment": {
            "setting": "ruined factory complex interior or exterior",
            "time_of_day": "golden hour light filtering through broken ceiling",
            "surroundings": "industrial decay, overgrown vegetation, repurposed machinery",
        },
        "lighting": {
            "key_light": "warm directional golden hour light from frame left",
            "fill": "soft ambient bounce from concrete surfaces",
            "rim": "cool blue from open sky through broken roof",
            "mood": "atmospheric, cinematic, post-apocalyptic beauty",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            f"subject: {subject_desc}",
            "post-apocalyptic factory survival context",
        ],
    }


# --- Image generation ---

def generate_image(entry_id, profile, outdir, force=False):
    """Generate a single image from a profile. Returns True on success."""
    import google.generativeai as genai

    filepath = outdir / f"{entry_id}.png"

    if filepath.exists() and not force:
        print(f"  SKIP: {entry_id} (already exists)")
        return True

    prompt = profile_to_prompt(profile)

    try:
        model = genai.GenerativeModel(MODEL)
        response = model.generate_content(prompt)

        for part in response.candidates[0].content.parts:
            if hasattr(part, "inline_data") and part.inline_data:
                with open(filepath, "wb") as f:
                    f.write(part.inline_data.data)
                size_kb = len(part.inline_data.data) / 1024
                print(f"  OK: {entry_id} ({size_kb:.0f} KB, {part.inline_data.mime_type})")
                return True

        print(f"  WARN: {entry_id} -- model returned text only, no image")
        return False

    except Exception as e:
        err = str(e)
        if "exhausted" in err.lower() or "quota" in err.lower() or "429" in err:
            print(f"  QUOTA: {entry_id} -- rate limited, waiting 30s...")
            time.sleep(30)
            return generate_image(entry_id, profile, outdir, force)
        print(f"  FAIL: {entry_id} -- {err[:200]}")
        return False


# --- Main ---

def main():
    force = "--force" in sys.argv
    dump = "--dump" in sys.argv
    heroes_only = "--heroes-only" in sys.argv

    only = None
    if "--only" in sys.argv:
        idx = sys.argv.index("--only")
        if idx + 1 < len(sys.argv):
            only = sys.argv[idx + 1].lower()

    # Collect all bible entries
    entries = collect_all_entries()
    if not entries:
        print("error: no bible entries found")
        sys.exit(1)

    # Build profiles for each entry
    profiles = []  # list of (entry_id, profile, is_hero)
    for entry in entries:
        entry_id = entry["_id"]
        if entry_id is None:
            continue

        is_hero = entry_id in HERO_ENTRIES

        if heroes_only and not is_hero:
            continue

        if only:
            # Filter by category or subcategory
            cat = entry["_category"].lower()
            subcat = entry["_subcategory"].lower()
            if only != cat and only != subcat:
                continue

        if is_hero:
            profile = build_hero_profile(entry)
        else:
            profile = build_icon_profile(entry)

        profiles.append((entry_id, profile, is_hero))

    if not profiles:
        filter_desc = only or ("heroes" if heroes_only else "all")
        print(f"no entries matching filter '{filter_desc}'")
        sys.exit(1)

    hero_count = sum(1 for _, _, h in profiles if h)
    icon_count = len(profiles) - hero_count

    # Dump mode: print prompts and exit
    if dump:
        for entry_id, profile, is_hero in profiles:
            art_type = "HERO" if is_hero else "ICON"
            print(f"=== {entry_id} [{art_type}] ===")
            print(profile_to_prompt(profile))
            print()
        print(f"--- {len(profiles)} profiles ({hero_count} hero, {icon_count} icon) ---")
        return

    # Generation mode: require API key
    api_key = os.environ.get("GOOGLE_API_KEY")
    if not api_key:
        print("error: set GOOGLE_API_KEY environment variable")
        print("  get one at: https://aistudio.google.com/apikey")
        sys.exit(1)

    import google.generativeai as genai
    genai.configure(api_key=api_key)

    outdir = Path(OUTDIR)
    outdir.mkdir(parents=True, exist_ok=True)

    total = len(profiles)
    success = 0
    failed = []

    print(f"generating {total} bible art images ({hero_count} hero, {icon_count} icon) using {MODEL}")
    print(f"output: {outdir}/")
    if force:
        print("force mode: regenerating all images")
    print()

    for i, (entry_id, profile, is_hero) in enumerate(profiles):
        art_type = "HERO" if is_hero else "ICON"
        print(f"[{i+1}/{total}] {entry_id} [{art_type}] -- {profile['title']}")
        if generate_image(entry_id, profile, outdir, force):
            success += 1
        else:
            failed.append(entry_id)

        if i < total - 1:
            time.sleep(2)

    print()
    print(f"done: {success}/{total} generated")
    if failed:
        print(f"failed: {', '.join(failed)}")
        print("re-run the script to retry failed images.")


if __name__ == "__main__":
    main()
