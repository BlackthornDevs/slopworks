"""Shared bible parsing module for Slopworks game bible tooling.

Provides YAML extraction, ID detection, entry collection, category metadata,
visual identity constants, and prompt building for both web page generation
and concept art generation scripts.

Usage:
    from bible_common import collect_all_entries, CATEGORIES, VISUAL_IDENTITY
"""

import re
from pathlib import Path

try:
    import yaml
except ImportError:
    import sys
    print("PyYAML not installed. Run: pip3 install pyyaml")
    sys.exit(1)

# --- Paths ---

BIBLE_DIR = Path(__file__).parent.parent / "docs" / "bible"

# --- ID field detection ---

KNOWN_ID_FIELDS = {
    "itemId", "weaponId", "machineId", "turretId", "foundationId", "wallId",
    "rampId", "recipeId", "faunaId", "buildingId", "sceneryId", "npcId",
    "lineId", "lineTypeId", "waveId", "nodeId", "upgradeId", "effectId",
    "hazardId", "chapterId", "biomeId", "defenseId", "id",
}

# --- Category metadata ---

CATEGORIES = {
    "items": ("Items", "Player-facing objects: materials, weapons, armor, tools, consumables, loot, lore"),
    "buildables": ("Buildables", "Placed in the world: machines, defenses, structural pieces, scenery"),
    "systems": ("Systems", "Rules and progression: recipes, research, upgrades, effects, waves"),
    "world": ("World", "Exploration and territory: biomes, buildings, hazards, supply lines"),
    "characters": ("Characters", "NPCs, enemies, dialogue, and narrative progression"),
}

SUBCATEGORY_NAMES = {
    # items
    "raw-materials": "Raw materials",
    "weapons": "Weapons",
    "wearables": "Wearables",
    "equipment": "Equipment",
    "consumables": "Consumables",
    "tower-loot": "Tower loot",
    "lore-items": "Lore items",
    # buildables
    "machines": "Machines",
    "defenses": "Defenses",
    "structural": "Structural",
    "scenery": "Scenery",
    # systems
    "recipes": "Recipes",
    "research-tree": "Research tree",
    "upgrades": "Upgrades",
    "status-effects": "Status effects",
    "wave-events": "Wave events",
    # world
    "biomes": "Biomes",
    "reclaimed-buildings": "Reclaimed buildings",
    "environmental-hazards": "Environmental hazards",
    "supply-lines": "Supply lines",
    # characters
    "fauna": "Fauna",
    "npcs": "NPCs",
    "slop-dialogue": "SLOP dialogue",
    "narrative-progression": "Narrative progression",
}

# --- Visual identity (matches site/generate-art.py) ---

VISUAL_IDENTITY = {
    "project": "Slopworks — post-apocalyptic co-op factory survival game",
    "medium": "cinematic digital illustration, storyboard concept art rendering",
    "color_palette": {
        "primary": "muted earth tones — rust browns, concrete grays, olive greens",
        "accent": "safety orange (#FF6600) and warm amber (#FFAA00) from fire, furnaces, and molten metal",
        "secondary": "cold steel blue for damaged/unpowered areas, bioluminescent blue-green for fauna",
        "atmosphere": "golden hour haze, industrial smoke, volumetric light shafts",
    },
    "style_notes": [
        "painterly brushwork with visible texture, not photorealistic",
        "cinematic composition with clear focal point and depth",
        "post-apocalyptic industrial decay: rust, overgrowth, crumbling concrete",
        "no text, watermarks, UI elements, or logos in the image",
        "16:9 aspect ratio preferred for all images",
    ],
    "world_details": {
        "setting": "a ruined automated factory complex decades after a catastrophic cascade failure",
        "flora": "thick vines, moss, fungal growths reclaiming industrial structures",
        "fauna": "mutated creatures that have incorporated machine parts into their biology",
        "technology": "1970s-2000s industrial aesthetic — CRT monitors, analog gauges, heavy steel machinery",
        "human_presence": "workers in patched coveralls and hard hats with scavenged/improvised gear",
    },
}


# --- YAML extraction ---

def extract_yaml_blocks(md_path):
    """Extract YAML code blocks from a markdown file.

    Returns list of (content, line_number) tuples. The line number is
    1-indexed and points to the opening ```yaml fence.
    """
    text = Path(md_path).read_text()
    blocks = []
    pattern = re.compile(r"```yaml\n(.*?)```", re.DOTALL)
    for match in pattern.finditer(text):
        line_num = text[:match.start()].count("\n") + 1
        blocks.append((match.group(1), line_num))
    return blocks


# --- ID detection ---

def find_id_field(entry):
    """Find the ID field name in an entry dict, or None if not found."""
    for field in KNOWN_ID_FIELDS:
        if field in entry:
            return field
    return None


def get_entry_id(entry):
    """Return the ID value from an entry, or None if no ID field exists."""
    field = find_id_field(entry)
    if field is None:
        return None
    return entry.get(field)


# --- Entry collection ---

def _is_schema_block(parsed):
    """Schema blocks parse as dicts. Entry blocks parse as lists of dicts."""
    return not isinstance(parsed, list)


def collect_all_entries(bible_dir=None):
    """Collect all entries across all bible files.

    Returns a list of dicts, each augmented with metadata keys:
        _source_file  — relative path from bible_dir (e.g. "items/weapons.md")
        _category     — directory name (e.g. "items")
        _subcategory  — file stem (e.g. "weapons")
        _id           — the entry's ID value (or None)
        _id_field     — the entry's ID field name (or None)
    """
    if bible_dir is None:
        bible_dir = BIBLE_DIR
    bible_dir = Path(bible_dir)

    entries = []
    for md_file in sorted(bible_dir.rglob("*.md")):
        if md_file.name == "README.md":
            continue

        rel = md_file.relative_to(bible_dir)
        category = rel.parts[0] if len(rel.parts) > 1 else ""
        subcategory = md_file.stem

        for block_text, _ in extract_yaml_blocks(md_file):
            try:
                parsed = yaml.safe_load(block_text)
            except yaml.YAMLError:
                continue

            if not isinstance(parsed, list) or _is_schema_block(parsed):
                continue

            for entry in parsed:
                if not isinstance(entry, dict):
                    continue

                id_field = find_id_field(entry)
                entry_id = entry.get(id_field) if id_field else None

                augmented = dict(entry)
                augmented["_source_file"] = str(rel)
                augmented["_category"] = category
                augmented["_subcategory"] = subcategory
                augmented["_id"] = entry_id
                augmented["_id_field"] = id_field
                entries.append(augmented)

    return entries


# --- Prompt building ---

def _append_visual_identity(parts):
    """Append global style requirements to a prompt parts list."""
    parts.append("")
    parts.append("Global style requirements:")
    parts.append(f"Medium: {VISUAL_IDENTITY['medium']}")
    palette = VISUAL_IDENTITY["color_palette"]
    parts.append(
        f"Color palette: primary {palette['primary']}, "
        f"accent {palette['accent']}, "
        f"secondary {palette['secondary']}, "
        f"atmosphere {palette['atmosphere']}"
    )
    for note in VISUAL_IDENTITY["style_notes"]:
        parts.append(f"  - {note}")


def profile_to_prompt(profile):
    """Convert a structured JSON profile into a Gemini prompt string.

    Handles two profile shapes:

    Scene-focused (existing concept art pattern):
        scene, environment, lighting, creature_design, characters, action, details

    Object-focused (item icon pattern):
        object, background
    """
    # Detect which shape we're dealing with
    is_object_focused = "object" in profile and "scene" not in profile

    parts = [
        "Create a detailed storyboard-style concept art image.",
        f"Title: {profile['title']}",
    ]

    if is_object_focused:
        # --- Object-focused profile ---
        obj = profile.get("object", {})
        if obj:
            parts.append("Object details:")
            for key, val in obj.items():
                if isinstance(val, dict):
                    sub = "; ".join(f"{k}: {v}" for k, v in val.items())
                    parts.append(f"  {key}: {sub}")
                elif isinstance(val, list):
                    parts.append(f"  {key}:")
                    for item in val:
                        parts.append(f"    - {item}")
                else:
                    parts.append(f"  {key}: {val}")

        bg = profile.get("background", {})
        if bg:
            parts.append("Background:")
            for key, val in bg.items():
                if isinstance(val, dict):
                    sub = "; ".join(f"{k}: {v}" for k, v in val.items())
                    parts.append(f"  {key}: {sub}")
                else:
                    parts.append(f"  {key}: {val}")

        # Object profiles can still have details
        details = profile.get("details", [])
        if details:
            parts.append("Important specific details to include:")
            for d in details:
                parts.append(f"  - {d}")

    else:
        # --- Scene-focused profile ---
        scene = profile.get("scene", {})
        if scene:
            parts.append(f"Subject: {scene.get('subject', '')}")
            parts.append(f"Composition: {scene.get('composition', '')}")
            parts.append(f"Camera: {scene.get('camera', '')}")
            parts.append(f"Focal point: {scene.get('focal_point', '')}")

        creature = profile.get("creature_design", {})
        if creature:
            parts.append("Creature design details:")
            for key, val in creature.items():
                if isinstance(val, dict):
                    sub = "; ".join(f"{k}: {v}" for k, v in val.items())
                    parts.append(f"  {key}: {sub}")
                else:
                    parts.append(f"  {key}: {val}")

        characters = profile.get("characters", [])
        if characters:
            parts.append("Characters in scene:")
            for char in characters:
                parts.append(
                    f"  {char['role']}: {char['appearance']}. "
                    f"Posture: {char['posture']}"
                )

        action = profile.get("action", {})
        if action:
            parts.append("Action elements:")
            for key, val in action.items():
                parts.append(f"  {key}: {val}")

        env = profile.get("environment", {})
        if env:
            parts.append("Environment details:")
            for key, val in env.items():
                if isinstance(val, dict):
                    sub = "; ".join(f"{k}: {v}" for k, v in val.items())
                    parts.append(f"  {key}: {sub}")
                else:
                    parts.append(f"  {key}: {val}")

        lighting = profile.get("lighting", {})
        if lighting:
            parts.append("Lighting:")
            for key, val in lighting.items():
                parts.append(f"  {key}: {val}")

        details = profile.get("details", [])
        if details:
            parts.append("Important specific details to include:")
            for d in details:
                parts.append(f"  - {d}")

    _append_visual_identity(parts)
    return "\n".join(parts)
