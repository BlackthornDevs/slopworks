#!/usr/bin/env python3
"""Generate concept art for tower contract cards.

Uses the same Gemini image generation pipeline as generate-bible-art.py
but with tower-specific scene profiles for the 8 branch contracts + 1
classified placeholder.

Usage:
    GOOGLE_API_KEY=$(~/.claude/pass-get claude/api/gemini) python3 tools/generate-tower-art.py
    python3 tools/generate-tower-art.py --dump   # print prompts only
    python3 tools/generate-tower-art.py --force  # regenerate existing
"""

import os
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from bible_common import profile_to_prompt, VISUAL_IDENTITY

MODEL = "gemini-3-pro-image-preview"
OUTDIR = Path(__file__).parent.parent / "docs" / "assets" / "img"

# Each profile follows the scene-focused shape from bible_common.profile_to_prompt()
TOWER_PROFILES = {
    "tower-night-shift": {
        "title": "Night shift — 30 Rock power outage",
        "scene": {
            "subject": "a lone worker navigating a pitch-dark office corridor in 30 Rockefeller Plaza during a power outage, flashlight beam cutting through the blackness",
            "composition": "first-person perspective down a dark corporate hallway, 16:9 widescreen, flashlight beam center-frame",
            "camera": "slightly low angle, wide lens, deep perspective lines converging into darkness",
            "focal_point": "the bright amber flashlight cone illuminating overturned office furniture, with two pairs of glowing eyes visible at the beam's edge",
        },
        "environment": {
            "setting": "30 Rockefeller Plaza interior, art deco office building, decades abandoned",
            "time_of_day": "complete darkness except for the flashlight — power is out",
            "decay": "ceiling tiles hanging loose, papers scattered everywhere, smashed glass on floor, overturned desks",
        },
        "lighting": {
            "key_light": "single bright amber flashlight beam from the viewer's perspective, strong cone of light",
            "fill": "almost none — deep blacks outside the beam",
            "rim": "faint bioluminescent green-blue glow from stalker creatures lurking at edges",
            "mood": "claustrophobic, tense, isolated — horror survival",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "the flashlight beam should be the dominant visual element",
            "creature silhouettes barely visible at the edge of light",
            "art deco architectural details visible in the beam — crown molding, brass fixtures",
        ],
    },
    "tower-loading-docks": {
        "title": "Loading docks — MetLife freight zone",
        "scene": {
            "subject": "an industrial loading dock area inside the MetLife Building, massive freight elevators, stacked cargo containers, and pack runners prowling between crates",
            "composition": "wide establishing shot of a cavernous loading bay, 16:9 widescreen, high ceiling emphasizing scale",
            "camera": "medium-wide angle from a platform overlooking the dock floor",
            "focal_point": "a pack of fast-moving creatures weaving between cargo containers under dim overhead lights",
        },
        "environment": {
            "setting": "MetLife Building sub-level freight and logistics area, brutalist concrete architecture",
            "time_of_day": "interior, dim yellow sodium vapor lights still functional in patches",
            "decay": "rusted roll-up dock doors, crushed forklifts, scattered shipping containers with faded corporate logos",
        },
        "lighting": {
            "key_light": "sodium vapor overhead lamps casting amber pools on the concrete floor",
            "fill": "dim ambient gray between the light pools",
            "rim": "cold blue from exterior dock doors left partially open",
            "mood": "industrial, utilitarian, dangerous open spaces with limited cover",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "massive scale — high ceilings, freight elevators, industrial infrastructure",
            "pack runner creatures visible as fast blurs between cover",
            "reinforced armor plating visible on a shipping crate — the contract reward",
        ],
    },
    "tower-penthouse": {
        "title": "Penthouse — MetLife rooftop, wind exposure",
        "scene": {
            "subject": "the exposed rooftop penthouse level of the MetLife Building, wind-torn luxury space open to the NYC skyline, spitters perched on structural beams",
            "composition": "dramatic wide shot showing interior transitioning to open sky, 16:9 widescreen, skyline visible through blown-out windows",
            "camera": "eye-level, looking through a shattered penthouse wall toward the cityscape",
            "focal_point": "the boundary between sheltered interior and wind-blasted exterior, with creature silhouettes on exposed beams against the sky",
        },
        "environment": {
            "setting": "MetLife Building penthouse level, decades of weather damage after windows blew out",
            "time_of_day": "overcast twilight, dark purple-blue sky with faint amber on the horizon",
            "decay": "luxury fixtures destroyed by years of wind and rain, marble floors cracked, water damage everywhere, vegetation growing through gaps",
        },
        "lighting": {
            "key_light": "cold twilight sky pouring through missing walls and windows",
            "fill": "faint amber glow from deeper intact rooms behind the viewer",
            "rim": "wind-driven rain catching the light as silver streaks",
            "mood": "exposed, vertigo-inducing, beautiful desolation at altitude",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "NYC skyline silhouette visible — other ruined skyscrapers on the horizon",
            "wind effects visible: debris, tattered curtains, rain streaks",
            "spitter creatures perched on exposed structural steel like gargoyles",
        ],
    },
    "tower-sublevel-b": {
        "title": "Sublevel B — 30 Rock sub-basement, EM interference",
        "scene": {
            "subject": "a deep sub-basement level under 30 Rock filled with server racks and electrical infrastructure, crackling with electromagnetic interference from biomech hybrid creatures",
            "composition": "corridor between rows of server racks extending into depth, 16:9 widescreen, EM arcs and sparks as visual accents",
            "camera": "slightly low angle through the server aisle, forced perspective",
            "focal_point": "a biomech hybrid creature visible between server racks, its body crackling with EM energy that's disrupting the surrounding electronics",
        },
        "environment": {
            "setting": "30 Rock sub-basement B, originally NBC's broadcast infrastructure — server rooms, cable runs, electrical switchgear",
            "time_of_day": "interior, lit only by arcing electricity and status LEDs on equipment",
            "decay": "cables torn from trays, server racks partially collapsed, exposed wiring, pooled water on the floor reflecting light",
        },
        "lighting": {
            "key_light": "bright cyan-white electrical arcs jumping between equipment, casting sharp shadows",
            "fill": "scattered tiny teal and amber LED status lights on surviving equipment",
            "rim": "bioluminescent glow from the biomech hybrid's organic-machine fusion body",
            "mood": "electrified, dangerous, technological horror — the machines have become alive",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "EM interference visible as crackling arcs, screen distortion, sparking cables",
            "biomech hybrid creature: organic flesh fused with server components, wires growing through tissue",
            "HUD corruption effect: imagine the viewer's electronic equipment is being scrambled",
        ],
    },
    "tower-clock-tower": {
        "title": "Clock tower — Woolworth Building clock mechanism",
        "scene": {
            "subject": "the exposed clock mechanism in the Woolworth Building tower, massive bronze gears and escapements partially collapsed, with amber light filtering through the clock face",
            "composition": "looking up through the clock mechanism toward the translucent clock face, 16:9 widescreen, dramatic scale of the gears",
            "camera": "low angle looking up, wide lens emphasizing the towering mechanical structure",
            "focal_point": "warm amber sunlight streaming through the damaged clock face glass, silhouetting the massive gear teeth and structural cracks",
        },
        "environment": {
            "setting": "Woolworth Building clock tower interior, neo-Gothic architecture mixed with industrial clockwork",
            "time_of_day": "late afternoon, golden amber light filtering through the clock face",
            "decay": "structural cracks in the tower walls, some gears have fallen and crashed through floors below, wooden scaffolding partially collapsed",
        },
        "lighting": {
            "key_light": "warm amber sunlight through the clock face, creating god rays through dust",
            "fill": "reflected warm light bouncing off bronze gears and mechanisms",
            "rim": "cool shadow areas where the gear teeth block the light",
            "mood": "awe-inspiring mechanical beauty, vertigo from height, precarious instability",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "massive bronze clock gears — some intact and slowly turning, others broken",
            "structural cracks visible in walls and support beams — instability hazard",
            "neo-Gothic architectural details: pointed arches, terracotta ornament, gargoyle-like brackets",
        ],
    },
    "tower-server-farm": {
        "title": "Server farm — MetLife data center",
        "scene": {
            "subject": "a vast underground data center beneath the MetLife Building, endless rows of server racks in darkness with scattered status LEDs, stalker creatures hunting in the aisles",
            "composition": "symmetrical perspective down a long server aisle, 16:9 widescreen, the vanishing point deep in darkness",
            "camera": "eye-level center shot, the server racks forming clean lines into infinity, broken by creature movement",
            "focal_point": "hundreds of tiny LED status lights (teal and amber dots) creating a starfield effect, with a stalker's eyes glowing in the center distance",
        },
        "environment": {
            "setting": "MetLife Building underground data center, cold room with raised floor and overhead cable trays",
            "time_of_day": "complete interior darkness, only equipment LEDs and EM flashes for light",
            "decay": "some racks toppled, cables hanging from overhead trays like vines, water dripping from above",
        },
        "lighting": {
            "key_light": "scattered server status LEDs — tiny teal and amber pinpoints of light forming patterns in the darkness",
            "fill": "faint EM interference glow from biomech hybrid presence deeper in the room",
            "rim": "cool blue emergency light strip at floor level along the aisle",
            "mood": "eerie silence, digital darkness, being watched by things hiding between the racks",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "dual hazards: darkness AND EM interference both present",
            "the LED status lights should create an almost beautiful starfield pattern",
            "stalker creature barely visible — just glowing eyes and a silhouette between racks",
        ],
    },
    "tower-crypt": {
        "title": "The crypt — Woolworth sub-level, spore infestation",
        "scene": {
            "subject": "a deep vault beneath the Woolworth Building transformed into an alien hive, neo-Gothic stone arches covered in bioluminescent fungal growth and spore clouds, with the hive queen visible in the back",
            "composition": "cathedral-like interior overwhelmed by organic growth, 16:9 widescreen, the alien biology filling every surface",
            "camera": "medium-wide shot at entrance to the vault, looking in at the alien-transformed space",
            "focal_point": "the hive queen — a massive biomechanical creature integrated into the architecture, surrounded by drifting green-teal spore clouds",
        },
        "environment": {
            "setting": "Woolworth Building deep sub-level vault, originally a bank vault, now a hive",
            "time_of_day": "no natural light — lit entirely by bioluminescent fungal growth and spore glow",
            "decay": "the architecture isn't decayed but consumed — stone arches wrapped in organic tendrils, floor replaced by a living membrane, walls pulsing",
        },
        "lighting": {
            "key_light": "bright bioluminescent green-teal glow from fungal growths and spore clouds",
            "fill": "scattered warm amber from the hive queen's internal heat signature",
            "rim": "toxic green mist from drifting spore clouds, creating volumetric god rays",
            "mood": "alien cathedral, beautiful but deeply hostile, organic horror",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "spore clouds visible as thick green-teal mist drifting through the space",
            "hive queen: enormous creature integrated into the vault architecture, part building part beast",
            "neo-Gothic stone arches still visible beneath layers of organic overgrowth",
            "the most alien and visually distinctive of all contract locations",
        ],
    },
    "tower-sublevel-zero": {
        "title": "Sublevel zero — One World Trade Center deepest level",
        "scene": {
            "subject": "the deepest sub-level of One World Trade Center, massive foundation columns in a vast dark space, structural instability causing floor sections to crumble, EM interference sparking",
            "composition": "wide shot emphasizing overwhelming scale of the foundation infrastructure, 16:9 widescreen, multiple depth layers",
            "camera": "medium-low angle looking across a floor that's partially collapsed, revealing deeper levels below",
            "focal_point": "massive concrete columns scarred with EM burns and structural cracks, with the floor dropping away into a chasm where a lower level is visible",
        },
        "environment": {
            "setting": "One World Trade Center foundation level, the deepest accessible point in the tower system, bedrock visible",
            "time_of_day": "no natural light, deep underground, occasional EM flashes the only illumination",
            "decay": "catastrophic structural damage — floor sections collapsed, rebar exposed, concrete spalling, water seeping through cracks",
        },
        "lighting": {
            "key_light": "bright cyan EM interference arcs crawling across metal rebar and support structures",
            "fill": "faint amber glow from deep below — something is down there",
            "rim": "emergency red lighting from a still-functioning alarm system",
            "mood": "oppressive depth, geological scale, end-of-the-line finality, the most dangerous place in the game",
        },
        "details": [
            "16:9 aspect ratio",
            "no text",
            "dual hazards: EM interference AND structural instability",
            "massive scale — the columns are stories tall, the player is tiny by comparison",
            "floor sections visibly crumbling and falling into voids below",
            "this is where the game's darkest secrets are hidden — SLOP's original directives",
        ],
    },
    "tower-classified": {
        "title": "Classified — redacted tower contract",
        "scene": {
            "subject": "a corrupted surveillance photograph of an NYC skyscraper (Empire State or Chrysler Building silhouette), heavily redacted with black bars, scan lines, and CLASSIFIED stamps",
            "composition": "document/dossier aesthetic, 16:9 widescreen, the building barely visible through corruption and redaction",
            "camera": "flat, surveillance-camera or satellite imagery feel, not cinematic",
            "focal_point": "the building silhouette visible through layers of digital corruption, redaction bars, and scan lines",
        },
        "environment": {
            "setting": "a SLOP terminal display showing a classified tower reconnaissance image",
            "time_of_day": "the image appears to be dawn or dusk, amber sky behind the building",
            "decay": "the image itself is decayed — pixel corruption, scan lines, data loss artifacts",
        },
        "lighting": {
            "key_light": "CRT monitor glow casting the image in amber and teal tones",
            "fill": "scanline interference creating horizontal bands across the image",
            "rim": "none — flat surveillance image aesthetic",
            "mood": "secretive, redacted, something being hidden, tantalizing glimpse of future content",
        },
        "details": [
            "16:9 aspect ratio",
            "the word CLASSIFIED should appear prominently as stamped text on the image",
            "heavy black redaction bars covering parts of the building",
            "digital corruption: missing pixel blocks, color channel separation, noise",
            "this should look like a damaged classified document, not concept art",
            "amber and dark tones matching the Slopworks visual identity",
        ],
    },
}


def generate_image(entry_id, profile, outdir, force=False):
    """Generate a single image. Returns True on success."""
    import google.generativeai as genai

    filepath = outdir / f"{entry_id}.png"

    if filepath.exists() and not force:
        print(f"  SKIP: {entry_id} (exists, use --force to regenerate)")
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
                print(f"  OK: {entry_id} ({size_kb:.0f} KB)")
                return True

        print(f"  WARN: {entry_id} -- no image in response (text only)")
        return False

    except Exception as e:
        err = str(e)
        if "exhausted" in err.lower() or "quota" in err.lower() or "429" in err:
            print(f"  QUOTA: rate limited, waiting 30s...")
            time.sleep(30)
            return generate_image(entry_id, profile, outdir, force)
        print(f"  FAIL: {entry_id} -- {err[:200]}")
        return False


def main():
    force = "--force" in sys.argv
    dump = "--dump" in sys.argv

    if dump:
        for entry_id, profile in TOWER_PROFILES.items():
            print(f"=== {entry_id} ===")
            print(profile_to_prompt(profile))
            print()
        print(f"--- {len(TOWER_PROFILES)} tower contract images ---")
        return

    api_key = os.environ.get("GOOGLE_API_KEY")
    if not api_key:
        print("error: set GOOGLE_API_KEY")
        print("  GOOGLE_API_KEY=$(/home/jamditis/.claude/pass-get claude/api/gemini) python3 tools/generate-tower-art.py")
        sys.exit(1)

    import google.generativeai as genai
    genai.configure(api_key=api_key)

    outdir = Path(OUTDIR)
    outdir.mkdir(parents=True, exist_ok=True)

    total = len(TOWER_PROFILES)
    success = 0
    failed = []

    print(f"generating {total} tower contract card images using {MODEL}")
    print(f"output: {outdir}/")
    if force:
        print("force mode: regenerating all images")
    print()

    for i, (entry_id, profile) in enumerate(TOWER_PROFILES.items()):
        print(f"[{i+1}/{total}] {entry_id} -- {profile['title']}")
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


if __name__ == "__main__":
    main()
