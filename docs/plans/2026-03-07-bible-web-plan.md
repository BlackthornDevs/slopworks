# Bible web pages implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Generate static web pages for all 189 game bible entries with Gemini-powered art, deployed via GitHub Pages.

**Architecture:** A Python build script reads 24 bible markdown files, parses YAML entries, and generates static HTML pages into `docs/bible/`. A separate art generation script creates item icons and concept art using the same structured JSON profile pattern as the existing `site/generate-art.py`. Both scripts share a common module for YAML parsing and visual identity constants.

**Tech Stack:** Python 3 (PyYAML, google-generativeai), static HTML/CSS/JS, Gemini image generation API

---

## File inventory

| File | Action | Purpose |
|------|--------|---------|
| `tools/bible_common.py` | Create | Shared YAML parsing, entry collection, visual identity constants |
| `tools/build-bible-web.py` | Create | Generate HTML pages from bible YAML |
| `tools/generate-bible-art.py` | Create | Generate Gemini art for bible entries |
| `docs/js/main.js` | Modify (line 13) | Add "Bible" to NAV_PAGES |
| `docs/css/style.css` | Modify (append) | Add bible-specific CSS classes |
| `docs/bible/index.html` | Generated | Main bible index page |
| `docs/bible/{category}/index.html` | Generated | Category listing pages (5 total) |
| `docs/bible/{entry_id}/index.html` | Generated | Individual entry pages (~189 total) |
| `docs/assets/img/bible/*.png` | Generated | Entry art images |

## Reference files (read before starting)

- `docs/bible/README.md` — bible structure, enums, conventions
- `docs/bible/items/raw-materials.md` — example bible file with schema + entries
- `docs/css/style.css` — design tokens and component classes
- `docs/index.html` — page structure pattern (nav, sections, art-frames)
- `site/generate-art.py` — existing Gemini art pipeline (VISUAL_IDENTITY, profile_to_prompt, generate_image)
- `tools/validate-bible.py` — existing YAML extraction logic (extract_yaml_blocks, find_id_field)
- `docs/plans/2026-03-07-bible-web-design.md` — approved design

## Design tokens (from `docs/css/style.css`)

```css
--bg: #0A0E14;           /* page background */
--bg-surface: #12171F;   /* section background */
--bg-card: #1A1F2B;      /* card background */
--border: #2A2A2A;       /* default border */
--border-rust: #8B3A1A;  /* art frame border */
--text: #C5CDD8;         /* body text */
--text-dim: #6B7A8D;     /* secondary text */
--accent: #E8A031;       /* headings, highlights */
--accent-dim: #8B6320;   /* hover accents */
--teal: #5CCFE6;         /* links, tags */
--red: #CC3333;          /* warnings */
--font-display: 'Oswald', sans-serif;
--font-body: 'IBM Plex Sans', sans-serif;
--font-mono: 'Space Mono', monospace;
```

## Existing CSS classes to reuse

- `.container` — max-width centered content
- `.section` — section with padding and scroll-reveal
- `.card-grid` — 2-column responsive grid
- `.card` / `.card-image` / `.card-body` / `.card-title` / `.card-desc` — card components
- `.art-frame` / `.art-frame img` — image container with rust border and inner shadow
- `.caution-divider` — striped section divider

## Entry counts by category

| Category | Subcategory files | Total entries |
|----------|-------------------|---------------|
| items/ | raw-materials (18), weapons (6), wearables (5), equipment (5), consumables (5), tower-loot (8), lore-items (6) | 53 |
| buildables/ | machines (7), defenses (7), structural (9), scenery (6) | 29 |
| systems/ | recipes (11), research-tree (11), upgrades (6), status-effects (8), wave-events (6) | 42 |
| world/ | biomes (6), reclaimed-buildings (8), env-hazards (7), supply-lines (4) | 25 |
| characters/ | fauna (10), npcs (4), slop-dialogue (20), narrative (6) | 40 |
| **Total** | **24 files** | **189 entries** |

---

### Task 1: Shared bible parsing module

**Files:**
- Create: `tools/bible_common.py`

**Step 1: Write the module**

This extracts the YAML parsing logic from `tools/validate-bible.py` into a reusable module. Both the web builder and art generator will import from it.

```python
#!/usr/bin/env python3
"""Shared utilities for bible tooling: YAML extraction, entry collection, visual identity."""

import re
from pathlib import Path

try:
    import yaml
except ImportError:
    print("PyYAML not installed. Run: pip3 install pyyaml")
    raise SystemExit(1)

BIBLE_DIR = Path(__file__).parent.parent / "docs" / "bible"

# ID field detection (same as validate-bible.py)
KNOWN_ID_FIELDS = {
    "itemId", "weaponId", "machineId", "turretId", "foundationId", "wallId",
    "rampId", "recipeId", "faunaId", "buildingId", "sceneryId", "npcId",
    "lineId", "lineTypeId", "waveId", "nodeId", "upgradeId", "effectId",
    "hazardId", "chapterId", "biomeId", "defenseId", "id",
}

# Category metadata: directory name -> (display name, description)
CATEGORIES = {
    "items": ("Items", "Player-facing objects: materials, weapons, armor, tools, consumables, loot, lore"),
    "buildables": ("Buildables", "Placed in the world: machines, defenses, structural pieces, scenery"),
    "systems": ("Systems", "Rules and progression: recipes, research, upgrades, effects, waves"),
    "world": ("World", "Exploration and territory: biomes, buildings, hazards, supply lines"),
    "characters": ("Characters", "NPCs, enemies, dialogue, and narrative progression"),
}

# Maps subcategory file stems to display names
SUBCATEGORY_NAMES = {
    "raw-materials": "Raw materials",
    "weapons": "Weapons",
    "wearables": "Wearables",
    "equipment": "Equipment",
    "consumables": "Consumables",
    "tower-loot": "Tower loot",
    "lore-items": "Lore items",
    "machines": "Machines",
    "defenses": "Defenses",
    "structural": "Structural",
    "scenery": "Scenery",
    "recipes": "Recipes",
    "research-tree": "Research tree",
    "upgrades": "Upgrades",
    "status-effects": "Status effects",
    "wave-events": "Wave events",
    "biomes": "Biomes",
    "reclaimed-buildings": "Reclaimed buildings",
    "environmental-hazards": "Environmental hazards",
    "supply-lines": "Supply lines",
    "fauna": "Fauna",
    "npcs": "NPCs",
    "slop-dialogue": "SLOP dialogue",
    "narrative-progression": "Narrative progression",
}


def extract_yaml_blocks(md_path: Path) -> list[tuple[str, int]]:
    """Extract YAML code blocks from a markdown file. Returns (content, line_number) pairs."""
    text = md_path.read_text()
    blocks = []
    pattern = re.compile(r"```yaml\n(.*?)```", re.DOTALL)
    for match in pattern.finditer(text):
        line_num = text[:match.start()].count("\n") + 1
        blocks.append((match.group(1), line_num))
    return blocks


def find_id_field(entry: dict) -> str | None:
    """Find the ID field in an entry."""
    for field in KNOWN_ID_FIELDS:
        if field in entry:
            return field
    return None


def get_entry_id(entry: dict) -> str | None:
    """Get the ID value from an entry."""
    id_field = find_id_field(entry)
    if id_field and entry.get(id_field):
        return str(entry[id_field])
    return None


def collect_all_entries(bible_dir: Path | None = None) -> list[dict]:
    """Collect all entries from all bible files.

    Returns a list of dicts, each augmented with:
      _source_file: relative path of the source markdown file
      _category: top-level category directory name
      _subcategory: source file stem (e.g. "raw-materials")
      _id: the entry's ID value
      _id_field: the name of the ID field
    """
    bible_dir = bible_dir or BIBLE_DIR
    entries = []

    for md_file in sorted(bible_dir.rglob("*.md")):
        if md_file.name == "README.md":
            continue

        rel = md_file.relative_to(bible_dir)
        category = rel.parts[0] if len(rel.parts) > 1 else "uncategorized"
        subcategory = md_file.stem

        for block_text, _ in extract_yaml_blocks(md_file):
            try:
                parsed = yaml.safe_load(block_text)
            except yaml.YAMLError:
                continue

            if not isinstance(parsed, list):
                continue

            for entry in parsed:
                if not isinstance(entry, dict):
                    continue
                id_field = find_id_field(entry)
                if not id_field or not entry.get(id_field):
                    continue

                entry["_source_file"] = str(rel)
                entry["_category"] = category
                entry["_subcategory"] = subcategory
                entry["_id"] = str(entry[id_field])
                entry["_id_field"] = id_field
                entries.append(entry)

    return entries


# Gemini visual identity (shared between generate-art.py and generate-bible-art.py)
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
    ],
    "world_details": {
        "setting": "a ruined automated factory complex decades after a catastrophic cascade failure",
        "flora": "thick vines, moss, fungal growths reclaiming industrial structures",
        "fauna": "mutated creatures that have incorporated machine parts into their biology",
        "technology": "1970s-2000s industrial aesthetic — CRT monitors, analog gauges, heavy steel machinery",
        "human_presence": "workers in patched coveralls and hard hats with scavenged/improvised gear",
    },
}


def profile_to_prompt(profile: dict) -> str:
    """Convert a structured JSON profile into a detailed prompt string.
    Handles both scene-focused profiles (existing concept art) and
    object-focused profiles (bible item icons).
    """
    parts = [
        "Create a detailed concept art image.",
        f"Title: {profile['title']}",
    ]

    # Scene composition (for full concept art)
    scene = profile.get("scene", {})
    if scene:
        parts.append(f"Subject: {scene.get('subject', '')}")
        parts.append(f"Composition: {scene.get('composition', '')}")
        parts.append(f"Camera: {scene.get('camera', '')}")
        parts.append(f"Focal point: {scene.get('focal_point', '')}")

    # Object description (for item icons)
    obj = profile.get("object", {})
    if obj:
        parts.append("Object details:")
        for key, val in obj.items():
            parts.append(f"  {key}: {val}")

    # Background (for item icons)
    bg = profile.get("background", {})
    if bg:
        parts.append("Background:")
        for key, val in bg.items():
            parts.append(f"  {key}: {val}")

    # Creature design
    creature = profile.get("creature_design", {})
    if creature:
        parts.append("Creature design details:")
        for key, val in creature.items():
            if isinstance(val, dict):
                sub = "; ".join(f"{k}: {v}" for k, v in val.items())
                parts.append(f"  {key}: {sub}")
            else:
                parts.append(f"  {key}: {val}")

    # Characters
    characters = profile.get("characters", [])
    if characters:
        parts.append("Characters in scene:")
        for char in characters:
            parts.append(f"  {char['role']}: {char['appearance']}. Posture: {char['posture']}")

    # Action
    action = profile.get("action", {})
    if action:
        parts.append("Action elements:")
        for key, val in action.items():
            parts.append(f"  {key}: {val}")

    # Environment
    env = profile.get("environment", {})
    if env:
        parts.append("Environment details:")
        for key, val in env.items():
            if isinstance(val, dict):
                sub = "; ".join(f"{k}: {v}" for k, v in val.items())
                parts.append(f"  {key}: {sub}")
            else:
                parts.append(f"  {key}: {val}")

    # Lighting
    lighting = profile.get("lighting", {})
    if lighting:
        parts.append("Lighting:")
        for key, val in lighting.items():
            parts.append(f"  {key}: {val}")

    # Specific details
    details = profile.get("details", [])
    if details:
        parts.append("Important specific details to include:")
        for d in details:
            parts.append(f"  - {d}")

    # Global visual identity
    parts.append("")
    parts.append("Global style requirements:")
    parts.append(f"Medium: {VISUAL_IDENTITY['medium']}")
    palette = VISUAL_IDENTITY["color_palette"]
    parts.append(
        f"Color palette: primary {palette['primary']}, accent {palette['accent']}, "
        f"secondary {palette['secondary']}, atmosphere {palette['atmosphere']}"
    )
    for note in VISUAL_IDENTITY["style_notes"]:
        parts.append(f"  - {note}")

    return "\n".join(parts)
```

**Step 2: Verify it works**

Run: `python3 -c "from tools.bible_common import collect_all_entries; entries = collect_all_entries(); print(f'{len(entries)} entries loaded')"`

Expected: `189 entries loaded`

**Step 3: Commit**

```bash
git add tools/bible_common.py
git commit -m "Add shared bible parsing module for web and art tools"
```

---

### Task 2: HTML build script — core engine

**Files:**
- Create: `tools/build-bible-web.py`

**Step 1: Write the build script**

This is the main HTML generator. It reads all bible entries via `bible_common`, then generates:
1. `docs/bible/index.html` — main index
2. `docs/bible/{category}/index.html` — category pages (5)
3. `docs/bible/{entry_id}/index.html` — entry detail pages (~189)

```python
#!/usr/bin/env python3
"""Build static HTML pages for the Slopworks game bible.

Usage:
    python3 tools/build-bible-web.py              # build all pages
    python3 tools/build-bible-web.py --clean       # remove generated HTML first
    python3 tools/build-bible-web.py --only items  # build only one category
"""

import os
import shutil
import sys
from pathlib import Path
from html import escape

# Add tools/ to path for bible_common import
sys.path.insert(0, str(Path(__file__).parent))
from bible_common import (
    BIBLE_DIR, CATEGORIES, SUBCATEGORY_NAMES,
    collect_all_entries, get_entry_id,
)

DOCS_DIR = BIBLE_DIR.parent
IMG_DIR = DOCS_DIR / "assets" / "img" / "bible"

# Relative path from a bible page back to docs/ root
# From docs/bible/index.html -> ../../ -> docs/
# From docs/bible/items/index.html -> ../../../ -> docs/
# From docs/bible/iron_ore/index.html -> ../../../ -> docs/
# Simplified: all pages use a base_path variable


def css_path(depth: int) -> str:
    """Return relative path to docs/css/style.css from a given depth."""
    return "../" * depth + "css/style.css"


def js_path(depth: int) -> str:
    """Return relative path to docs/js/main.js from a given depth."""
    return "../" * depth + "js/main.js"


def img_path(entry_id: str, depth: int) -> str:
    """Return relative path to an entry's image."""
    return "../" * depth + f"assets/img/bible/{entry_id}.png"


def root_path(depth: int) -> str:
    """Return relative path back to docs/ root."""
    return "../" * depth


def entry_url(entry_id: str, depth: int) -> str:
    """Return relative URL to an entry's detail page."""
    return "../" * depth + f"bible/{entry_id}/"


def category_url(category: str, depth: int) -> str:
    """Return relative URL to a category page."""
    return "../" * depth + f"bible/{category}/"


def rarity_class(rarity: str | None) -> str:
    """Return CSS class for rarity badge."""
    if not rarity:
        return ""
    return f"rarity-{rarity.lower()}"


def html_head(title: str, depth: int, description: str = "") -> str:
    """Generate <head> section matching main site pattern."""
    desc = description or f"{title} — Slopworks game bible"
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{escape(title)} — Slopworks bible</title>
    <meta name="description" content="{escape(desc)}">
    <link rel="icon" type="image/svg+xml" href="{root_path(depth)}favicon.svg">
    <link rel="stylesheet" href="{css_path(depth)}">
    <style>
        /* Bible-specific styles */
        .bible-breadcrumb {{
            font-family: var(--font-mono);
            font-size: 0.8rem;
            color: var(--text-dim);
            margin-bottom: 1.5rem;
        }}
        .bible-breadcrumb a {{
            color: var(--teal);
            text-decoration: none;
        }}
        .bible-breadcrumb a:hover {{
            text-decoration: underline;
        }}
        .bible-breadcrumb .sep {{
            margin: 0 0.5rem;
            color: var(--text-dim);
        }}
        .entry-header {{
            display: flex;
            gap: 2rem;
            align-items: flex-start;
            margin-bottom: 2rem;
        }}
        .entry-icon {{
            width: 200px;
            min-width: 200px;
            border-radius: 4px;
            border: 1px solid var(--border-rust);
        }}
        .entry-icon img {{
            width: 100%;
            display: block;
            border-radius: 3px;
        }}
        .entry-meta h1 {{
            font-family: var(--font-display);
            color: var(--accent);
            font-size: 2rem;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            margin-bottom: 0.5rem;
        }}
        .entry-meta .entry-id {{
            font-family: var(--font-mono);
            font-size: 0.85rem;
            color: var(--text-dim);
            margin-bottom: 0.75rem;
        }}
        .entry-meta .entry-desc {{
            line-height: 1.7;
        }}
        .slop-callout {{
            background: var(--bg-card);
            border-left: 3px solid var(--accent);
            padding: 1rem 1.25rem;
            margin: 1.5rem 0;
            font-style: italic;
            color: var(--text-dim);
            border-radius: 0 4px 4px 0;
        }}
        .slop-callout::before {{
            content: "S.L.O.P. says:";
            display: block;
            font-family: var(--font-mono);
            font-style: normal;
            font-size: 0.75rem;
            color: var(--accent);
            text-transform: uppercase;
            letter-spacing: 0.1em;
            margin-bottom: 0.5rem;
        }}
        .stats-table {{
            width: 100%;
            border-collapse: collapse;
            margin: 1.5rem 0;
        }}
        .stats-table th,
        .stats-table td {{
            padding: 0.5rem 0.75rem;
            text-align: left;
            border-bottom: 1px solid var(--border);
            font-size: 0.9rem;
        }}
        .stats-table th {{
            color: var(--text-dim);
            font-family: var(--font-mono);
            font-size: 0.8rem;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            width: 40%;
        }}
        .stats-table td {{
            color: var(--text);
        }}
        .tag-list {{
            display: flex;
            flex-wrap: wrap;
            gap: 0.4rem;
            margin: 0.5rem 0;
        }}
        .tag {{
            background: var(--bg-surface);
            border: 1px solid var(--border);
            border-radius: 3px;
            padding: 0.15rem 0.5rem;
            font-family: var(--font-mono);
            font-size: 0.75rem;
            color: var(--teal);
        }}
        .rarity-badge {{
            display: inline-block;
            padding: 0.15rem 0.6rem;
            border-radius: 3px;
            font-family: var(--font-mono);
            font-size: 0.75rem;
            text-transform: uppercase;
            letter-spacing: 0.05em;
        }}
        .rarity-common {{ background: #3a3a3a; color: #aaa; }}
        .rarity-uncommon {{ background: #1a3a1a; color: #5c5; }}
        .rarity-rare {{ background: #1a2a4a; color: #58f; }}
        .rarity-epic {{ background: #3a1a4a; color: #a5f; }}
        .rarity-legendary {{ background: #4a3a1a; color: var(--accent); }}
        .xref-link {{
            color: var(--teal);
            text-decoration: none;
        }}
        .xref-link:hover {{
            text-decoration: underline;
        }}
        .category-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
            gap: 1.5rem;
        }}
        .bible-search {{
            width: 100%;
            padding: 0.75rem 1rem;
            background: var(--bg-surface);
            border: 1px solid var(--border);
            border-radius: 4px;
            color: var(--text);
            font-family: var(--font-body);
            font-size: 1rem;
            margin-bottom: 2rem;
            outline: none;
        }}
        .bible-search:focus {{
            border-color: var(--accent-dim);
        }}
        .bible-search::placeholder {{
            color: var(--text-dim);
        }}
        .entry-card {{
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 4px;
            overflow: hidden;
            text-decoration: none;
            color: inherit;
            transition: border-color 0.3s, transform 0.3s;
            display: flex;
            gap: 0;
        }}
        .entry-card:hover {{
            border-color: var(--accent-dim);
            transform: translateY(-2px);
        }}
        .entry-card-icon {{
            width: 80px;
            min-width: 80px;
            height: 80px;
            object-fit: cover;
        }}
        .entry-card-body {{
            padding: 0.75rem 1rem;
            min-width: 0;
        }}
        .entry-card-title {{
            font-family: var(--font-display);
            font-size: 0.95rem;
            text-transform: uppercase;
            letter-spacing: 0.03em;
            color: var(--accent);
            margin-bottom: 0.25rem;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}
        .entry-card-sub {{
            font-size: 0.8rem;
            color: var(--text-dim);
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }}
        .section-label {{
            font-family: var(--font-display);
            font-size: 1.3rem;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            color: var(--accent);
            margin: 2rem 0 1rem;
            padding-bottom: 0.5rem;
            border-bottom: 1px solid var(--border);
        }}
        .img-pending {{
            width: 100%;
            aspect-ratio: 1;
            background: var(--bg-surface);
            border: 1px dashed var(--border);
            display: flex;
            align-items: center;
            justify-content: center;
            font-family: var(--font-mono);
            font-size: 0.7rem;
            color: var(--text-dim);
            border-radius: 3px;
        }}
        @media (max-width: 700px) {{
            .entry-header {{
                flex-direction: column;
            }}
            .entry-icon {{
                width: 100%;
                min-width: unset;
            }}
            .category-grid {{
                grid-template-columns: 1fr;
            }}
        }}
    </style>
</head>"""


def html_nav_and_open_body(depth: int) -> str:
    """Opening body, nav placeholder, and container open."""
    return f"""
<body>
    <div id="nav"></div>
    <div class="caution-divider"></div>
"""


def html_footer(depth: int) -> str:
    """Closing body with main.js."""
    return f"""
    <script src="{js_path(depth)}"></script>
</body>
</html>"""


def image_tag(entry_id: str, alt: str, depth: int, css_class: str = "") -> str:
    """Image tag with fallback for missing images."""
    src = img_path(entry_id, depth)
    cls = f' class="{css_class}"' if css_class else ""
    return (
        f'<img src="{src}" alt="{escape(alt)}"{cls} loading="lazy" '
        f'onerror="this.outerHTML=\'<div class=&quot;img-pending&quot;>[ART PENDING]</div>\'">'
    )


# --- Fields to skip in stats tables (internal or displayed elsewhere) ---
SKIP_FIELDS = {
    "_source_file", "_category", "_subcategory", "_id", "_id_field",
    "displayName", "description", "slopCommentary", "tags", "modelStyle",
    "obtainedFrom", "abstract",
}


def render_value(key: str, val, all_ids: set, depth: int) -> str:
    """Render a field value, with cross-reference links where applicable."""
    if val is None:
        return '<span style="color:var(--text-dim)">—</span>'
    if isinstance(val, bool):
        return "yes" if val else "no"
    if isinstance(val, list):
        parts = []
        for item in val:
            if isinstance(item, dict):
                sub = ", ".join(f"{k}: {v}" for k, v in item.items())
                parts.append(f"({sub})")
            elif isinstance(item, str) and item in all_ids:
                parts.append(f'<a href="{entry_url(item, depth)}" class="xref-link">{escape(item)}</a>')
            else:
                parts.append(escape(str(item)))
        return ", ".join(parts) if parts else "—"
    if isinstance(val, dict):
        sub = ", ".join(f"{k}: {v}" for k, v in val.items())
        return escape(f"({sub})")
    if isinstance(val, str) and val in all_ids:
        return f'<a href="{entry_url(val, depth)}" class="xref-link">{escape(val)}</a>'
    return escape(str(val))


def build_entry_page(entry: dict, all_ids: set, output_dir: Path):
    """Generate an individual entry detail page."""
    entry_id = entry["_id"]
    display_name = entry.get("displayName", entry_id.replace("_", " ").title())
    description = entry.get("description", "")
    slop = entry.get("slopCommentary", "")
    category = entry["_category"]
    subcategory = entry["_subcategory"]
    cat_name = CATEGORIES.get(category, (category.title(), ""))[0]
    subcat_name = SUBCATEGORY_NAMES.get(subcategory, subcategory.replace("-", " ").title())
    rarity = entry.get("rarity")
    tags = entry.get("tags", [])

    # depth from docs/bible/{entry_id}/index.html -> docs/ is 3
    depth = 3

    page_dir = output_dir / entry_id
    page_dir.mkdir(parents=True, exist_ok=True)

    # Build stats table rows (skip display fields, internal fields)
    id_field = entry["_id_field"]
    stats_rows = []
    for key, val in entry.items():
        if key in SKIP_FIELDS or key == id_field:
            continue
        stats_rows.append(
            f"<tr><th>{escape(key)}</th><td>{render_value(key, val, all_ids, depth)}</td></tr>"
        )

    tags_html = ""
    if tags:
        tag_spans = "".join(f'<span class="tag">{escape(str(t))}</span>' for t in tags)
        tags_html = f'<div class="tag-list">{tag_spans}</div>'

    rarity_html = ""
    if rarity:
        rarity_html = f'<span class="rarity-badge rarity-{rarity.lower()}">{escape(rarity)}</span>'

    slop_html = ""
    if slop:
        slop_html = f'<div class="slop-callout">{escape(slop)}</div>'

    obtained = entry.get("obtainedFrom", [])
    obtained_html = ""
    if obtained and isinstance(obtained, list):
        rows = ""
        for src in obtained:
            if isinstance(src, dict):
                rows += f"<tr><th>{escape(str(src.get('source', '')))}</th><td>{escape(str(src.get('details', '')))}</td></tr>"
        if rows:
            obtained_html = f"""
            <h3 class="section-label" style="font-size:1rem;">Obtained from</h3>
            <table class="stats-table">{rows}</table>"""

    html = html_head(display_name, depth, description)
    html += html_nav_and_open_body(depth)
    html += f"""
    <section class="section">
        <div class="container">
            <div class="bible-breadcrumb">
                <a href="{root_path(depth)}bible/">Bible</a>
                <span class="sep">/</span>
                <a href="{root_path(depth)}bible/{category}/">{escape(cat_name)}</a>
                <span class="sep">/</span>
                {escape(subcat_name)}
            </div>

            <div class="entry-header">
                <div class="entry-icon">
                    {image_tag(entry_id, display_name, depth)}
                </div>
                <div class="entry-meta">
                    <h1>{escape(display_name)}</h1>
                    <div class="entry-id">{escape(entry_id)} {rarity_html}</div>
                    <p class="entry-desc">{escape(description)}</p>
                    {tags_html}
                </div>
            </div>

            {slop_html}

            <table class="stats-table">
                {"".join(stats_rows)}
            </table>

            {obtained_html}
        </div>
    </section>
"""
    html += html_footer(depth)

    (page_dir / "index.html").write_text(html)


def build_category_page(category: str, entries: list[dict], all_ids: set, output_dir: Path):
    """Generate a category listing page."""
    cat_name, cat_desc = CATEGORIES.get(category, (category.title(), ""))
    # depth from docs/bible/{category}/index.html -> docs/ is 3
    depth = 3

    # Group by subcategory
    by_subcat: dict[str, list[dict]] = {}
    for e in entries:
        sub = e["_subcategory"]
        by_subcat.setdefault(sub, []).append(e)

    cat_dir = output_dir / category
    cat_dir.mkdir(parents=True, exist_ok=True)

    html = html_head(cat_name, depth, cat_desc)
    html += html_nav_and_open_body(depth)
    html += f"""
    <section class="section">
        <div class="container">
            <div class="bible-breadcrumb">
                <a href="{root_path(depth)}bible/">Bible</a>
                <span class="sep">/</span>
                {escape(cat_name)}
            </div>
            <h1 style="font-family:var(--font-display); color:var(--accent); font-size:2rem; text-transform:uppercase; letter-spacing:0.05em; margin-bottom:0.5rem;">{escape(cat_name)}</h1>
            <p style="color:var(--text-dim); margin-bottom:2rem;">{escape(cat_desc)}</p>
            <input type="text" class="bible-search" placeholder="Search {cat_name.lower()}..." id="bible-search" oninput="filterCards(this.value)">
"""

    for subcat, sub_entries in by_subcat.items():
        subcat_name = SUBCATEGORY_NAMES.get(subcat, subcat.replace("-", " ").title())
        html += f'<h2 class="section-label">{escape(subcat_name)} ({len(sub_entries)})</h2>'
        html += '<div class="category-grid">'
        for e in sub_entries:
            eid = e["_id"]
            name = e.get("displayName", eid.replace("_", " ").title())
            rarity = e.get("rarity", "")
            rarity_badge = f'<span class="rarity-badge rarity-{rarity.lower()}" style="margin-left:0.5rem;font-size:0.65rem;">{escape(rarity)}</span>' if rarity else ""
            html += f"""
                <a href="{entry_url(eid, depth)}" class="entry-card" data-name="{escape(name.lower())}" data-id="{escape(eid)}">
                    {image_tag(eid, name, depth, "entry-card-icon")}
                    <div class="entry-card-body">
                        <div class="entry-card-title">{escape(name)}{rarity_badge}</div>
                        <div class="entry-card-sub">{escape(eid)}</div>
                    </div>
                </a>"""
        html += "</div>"

    html += """
        </div>
    </section>
    <script>
    function filterCards(q) {
        q = q.toLowerCase();
        document.querySelectorAll('.entry-card').forEach(function(c) {
            var match = c.dataset.name.includes(q) || c.dataset.id.includes(q);
            c.style.display = match ? '' : 'none';
        });
    }
    </script>
"""
    html += html_footer(depth)

    (cat_dir / "index.html").write_text(html)


def build_index_page(entries: list[dict], output_dir: Path):
    """Generate the main bible index page."""
    # depth from docs/bible/index.html -> docs/ is 2
    depth = 2

    # Count per category
    cat_counts: dict[str, int] = {}
    for e in entries:
        cat = e["_category"]
        cat_counts[cat] = cat_counts.get(cat, 0) + 1

    html = html_head("Game bible", depth, "Complete catalog of every game element in Slopworks")
    html += html_nav_and_open_body(depth)
    html += f"""
    <section class="section">
        <div class="container">
            <h1 style="font-family:var(--font-display); color:var(--accent); font-size:2.5rem; text-transform:uppercase; letter-spacing:0.05em; margin-bottom:0.5rem;">Game bible</h1>
            <p style="color:var(--text-dim); margin-bottom:2rem;">{len(entries)} entries across {len(CATEGORIES)} categories. The canonical catalog of every game element in Slopworks.</p>

            <input type="text" class="bible-search" placeholder="Search all {len(entries)} entries..." id="bible-search" oninput="filterAll(this.value)">

            <div class="card-grid" style="margin-bottom:3rem;">
"""
    for cat_key, (cat_name, cat_desc) in CATEGORIES.items():
        count = cat_counts.get(cat_key, 0)
        html += f"""
                <a href="{category_url(cat_key, depth)}" class="card">
                    <div class="card-body">
                        <div class="card-title">{escape(cat_name)}</div>
                        <div class="card-desc">{escape(cat_desc)}</div>
                        <div style="font-family:var(--font-mono); font-size:0.8rem; color:var(--accent); margin-top:0.75rem;">{count} entries</div>
                    </div>
                </a>"""
    html += """
            </div>

            <h2 class="section-label">All entries</h2>
            <div class="category-grid" id="all-entries">
"""
    for e in entries:
        eid = e["_id"]
        name = e.get("displayName", eid.replace("_", " ").title())
        sub = SUBCATEGORY_NAMES.get(e["_subcategory"], e["_subcategory"])
        html += f"""
                <a href="{entry_url(eid, depth)}" class="entry-card" data-name="{escape(name.lower())}" data-id="{escape(eid)}">
                    {image_tag(eid, name, depth, "entry-card-icon")}
                    <div class="entry-card-body">
                        <div class="entry-card-title">{escape(name)}</div>
                        <div class="entry-card-sub">{escape(sub)}</div>
                    </div>
                </a>"""

    html += """
            </div>
        </div>
    </section>
    <script>
    function filterAll(q) {
        q = q.toLowerCase();
        document.querySelectorAll('.entry-card').forEach(function(c) {
            var match = c.dataset.name.includes(q) || c.dataset.id.includes(q);
            c.style.display = match ? '' : 'none';
        });
    }
    </script>
"""
    html += html_footer(depth)

    (output_dir / "index.html").write_text(html)


def main():
    clean = "--clean" in sys.argv
    only = None
    if "--only" in sys.argv:
        idx = sys.argv.index("--only")
        if idx + 1 < len(sys.argv):
            only = sys.argv[idx + 1]

    output_dir = BIBLE_DIR

    if clean:
        # Remove generated HTML files only (not .md source files)
        index = output_dir / "index.html"
        if index.exists():
            index.unlink()
        for cat in CATEGORIES:
            cat_index = output_dir / cat / "index.html"
            if cat_index.exists():
                cat_index.unlink()
        # Remove entry directories (they only contain generated index.html)
        for d in output_dir.iterdir():
            if d.is_dir() and d.name not in CATEGORIES and d.name not in {".", ".."}:
                idx_file = d / "index.html"
                if idx_file.exists():
                    shutil.rmtree(d)
        print("cleaned generated HTML")

    entries = collect_all_entries()
    all_ids = {e["_id"] for e in entries}

    if only:
        build_entries = [e for e in entries if e["_category"] == only]
        if not build_entries:
            print(f"no entries in category '{only}'")
            sys.exit(1)
        print(f"building {len(build_entries)} entries in '{only}'")
    else:
        build_entries = entries

    # Build entry pages
    for e in build_entries:
        build_entry_page(e, all_ids, output_dir)

    # Build category pages
    cats_to_build = {only} if only else set(CATEGORIES.keys())
    for cat in cats_to_build:
        cat_entries = [e for e in entries if e["_category"] == cat]
        build_category_page(cat, cat_entries, all_ids, output_dir)

    # Build index page (always)
    build_index_page(entries, output_dir)

    print(f"built {len(build_entries)} entry pages, {len(cats_to_build)} category pages, 1 index page")
    print(f"output: {output_dir}/")


if __name__ == "__main__":
    main()
```

**Step 2: Test the build**

Run: `python3 tools/build-bible-web.py`

Expected output:
```
built 189 entry pages, 5 category pages, 1 index page
output: docs/bible/
```

Verify: `ls docs/bible/index.html docs/bible/items/index.html docs/bible/iron_ore/index.html`

All three files should exist.

**Step 3: Verify HTML renders**

Run: `python3 -m http.server 8080 --directory docs/` in background, then open `http://localhost:8080/bible/` in a browser. Check:
- Index page shows 5 category cards and 189 entry cards
- Category pages show grouped entries with search
- Entry pages show header, stats, SLOP callout, breadcrumbs
- Nav bar works and links back to main site
- Image placeholders show "[ART PENDING]" (images don't exist yet)
- Search filtering works on index and category pages

**Step 4: Commit**

```bash
git add tools/build-bible-web.py
git commit -m "Add bible web page builder: index, category, and entry pages"
```

---

### Task 3: Add bible link to site navigation

**Files:**
- Modify: `docs/js/main.js:8-14`

**Step 1: Add bible to NAV_PAGES**

Add the bible link after "S.L.O.P." in the navigation array. The bible index lives at `bible/index.html` relative to the docs root, but nav links from subpages need a relative path. Since the nav builder uses `href` directly, and bible pages are deeper, we need to handle this.

The simplest approach: add the bible entry to NAV_PAGES, but the existing nav builder constructs links assuming all pages are at the same level as `index.html`. Bible pages are nested deeper. Two options:

**Option A (simple):** Use an absolute path starting from the site root.
**Option B (keep relative):** Add to NAV_PAGES and adjust the nav builder.

Since the existing nav uses relative hrefs like `index.html`, `story.html`, and bible pages are at `bible/index.html` (one level deeper from docs root), the link `bible/` works from the root level pages. But from bible pages themselves (at `bible/iron_ore/index.html`), the same `bible/` href would be wrong.

The cleanest fix: make the nav builder use paths relative to the docs root with a base path. Change the NAV_PAGES entry to use `bible/` and update buildNav to detect the current depth and adjust.

Actually, the simpler approach: just add the entry and let bible pages set a `<base>` tag or use `../` prefixed paths. But since all existing pages are at the docs root level, just add the link — bible pages will need to handle this in their own nav. Since bible pages include `main.js` with the correct relative path, and the nav links are relative to the HTML file, the nav links from `bible/iron_ore/index.html` with href `story.html` would try to load `bible/iron_ore/story.html` which doesn't exist.

The right fix: add a `data-root` attribute to the nav div that bible pages set to their depth, and have buildNav prepend it.

Edit `docs/js/main.js` lines 8-14:

```javascript
const NAV_PAGES = [
    { href: 'index.html', label: 'Home', id: 'index' },
    { href: 'story.html', label: 'Story', id: 'story' },
    { href: 'build.html', label: 'Build', id: 'build' },
    { href: 'explore.html', label: 'Explore', id: 'explore' },
    { href: 'slop.html', label: 'S.L.O.P.', id: 'slop' },
    { href: 'bible/', label: 'Bible', id: 'bible' },
];
```

Then modify `buildNav()` (around line 16-50) to support a `data-root` attribute on the `#nav` div:

```javascript
function buildNav() {
    var container = document.getElementById('nav');
    if (!container) return;

    var rootPrefix = container.dataset.root || '';
    var currentPage = window.location.pathname.split('/').pop() || 'index.html';
    // ... rest of buildNav stays the same, but prepend rootPrefix to hrefs:
    // a.href = rootPrefix + p.href;
```

Then in the bible HTML templates, output the nav div as:
- `docs/bible/index.html`: `<div id="nav" data-root="../"></div>`
- `docs/bible/items/index.html`: `<div id="nav" data-root="../../"></div>`
- `docs/bible/iron_ore/index.html`: `<div id="nav" data-root="../../"></div>`

**Step 2: Update the build script's nav output**

In `build-bible-web.py`, update `html_nav_and_open_body()` to include the data-root attribute:

```python
def html_nav_and_open_body(depth: int) -> str:
    return f"""
<body>
    <div id="nav" data-root="{root_path(depth)}"></div>
    <div class="caution-divider"></div>
"""
```

**Step 3: Rebuild and verify**

Run: `python3 tools/build-bible-web.py`

Check that nav links from bible pages correctly navigate back to the main site pages.

**Step 4: Commit**

```bash
git add docs/js/main.js tools/build-bible-web.py
git commit -m "Add bible to site navigation with depth-aware link prefixing"
```

---

### Task 4: Art generation script — icon profiles

**Files:**
- Create: `tools/generate-bible-art.py`

**Step 1: Write the art generation script**

This script reads all bible entries, generates structured JSON profiles for each, and calls Gemini to create images. Icons use a simpler profile focused on the object; hero entries use full scene profiles.

```python
#!/usr/bin/env python3
"""Generate concept art and item icons for the Slopworks game bible.

Usage:
    export GOOGLE_API_KEY="your-key-here"
    python3 tools/generate-bible-art.py              # generate all missing images
    python3 tools/generate-bible-art.py --force       # regenerate everything
    python3 tools/generate-bible-art.py --only fauna   # only one category
    python3 tools/generate-bible-art.py --dump         # print prompts, don't generate
    python3 tools/generate-bible-art.py --heroes-only  # only hero concept art pieces
"""

import os
import sys
import time
from pathlib import Path

try:
    import google.generativeai as genai
except ImportError:
    print("google-generativeai not installed. Run: pip3 install google-generativeai")
    sys.exit(1)

sys.path.insert(0, str(Path(__file__).parent))
from bible_common import (
    collect_all_entries, profile_to_prompt, VISUAL_IDENTITY,
    CATEGORIES, SUBCATEGORY_NAMES,
)

OUTDIR = Path(__file__).parent.parent / "docs" / "assets" / "img" / "bible"
MODEL = "gemini-3-pro-image-preview"

# Hero entries get full cinematic concept art scenes.
# All other entries get object-on-background icons.
HERO_ENTRIES = {
    # Weapons
    "test_rifle", "salvage_pistol", "pipe_shotgun", "rebar_club", "arc_welder", "assault_rifle",
    # Boss fauna
    "tower_boss", "hive_queen", "biomech_hybrid",
    # Regular fauna (notable)
    "grunt", "spore_crawler", "pack_runner",
    # Machines
    "smelter_t1", "assembler_t1", "generator_t1",
    # Defenses
    "auto_turret_t1", "flamethrower_turret_t1",
    # Biomes
    "Grassland", "Forest", "Wasteland", "Swamp", "Ruins", "OvergrownRuins",
    # Buildings
    "power_plant_alpha", "foundry_central",
    # Key items
    "power_cell", "key_fragment", "boss_blueprint",
    # NPCs
    "slop_terminal",
}


def make_icon_profile(entry: dict) -> dict:
    """Generate an icon-style profile from a bible entry."""
    entry_id = entry["_id"]
    display_name = entry.get("displayName", entry_id.replace("_", " ").title())
    description = entry.get("description", "")
    model_style = entry.get("modelStyle", "")
    category = entry["_category"]
    subcategory = entry["_subcategory"]

    # Build object description from available fields
    obj_desc = model_style or description
    if not obj_desc:
        obj_desc = f"a {display_name} from a post-apocalyptic factory game"

    # Category-specific background and framing
    bg_map = {
        "items": {
            "style": "dark industrial workbench surface, slightly out of focus",
            "surface": "scarred metal table with scattered bolts, oil stains, and tool marks",
            "ambient": "warm amber light from a nearby furnace, casting soft shadows",
        },
        "buildables": {
            "style": "factory floor, slightly out of focus",
            "surface": "concrete floor with cracks, oil puddles, and painted safety markings",
            "ambient": "overhead industrial lighting, fluorescent with warm orange accent from distant machinery",
        },
        "systems": {
            "style": "abstract dark background with subtle circuit board traces",
            "surface": "matte dark surface with faint etched lines suggesting technical diagrams",
            "ambient": "cool blue-teal glow from below, as if from a holographic display",
        },
        "world": {
            "style": "environmental vista, atmospheric depth",
            "surface": "natural ground — grass, mud, cracked asphalt, or overgrown concrete depending on biome",
            "ambient": "natural lighting, golden hour or overcast, atmospheric haze in the distance",
        },
        "characters": {
            "style": "portrait-style framing, dark moody background",
            "surface": "indistinct dark environment, suggesting interior or exterior",
            "ambient": "dramatic single-source lighting, face partially in shadow",
        },
    }
    bg = bg_map.get(category, bg_map["items"])

    # Aspect ratio: square for icons
    profile = {
        "filename": entry_id,
        "category": category,
        "title": f"{display_name} — game item icon",
        "object": {
            "subject": obj_desc,
            "framing": "centered in frame, filling roughly 60-70% of the image",
            "angle": "three-quarter view, slightly elevated",
            "scale": "close-up, object fills the frame",
            "condition": "used, worn, post-apocalyptic — dents, scratches, patina, but functional",
        },
        "background": bg,
        "lighting": {
            "key_light": "warm amber from upper left",
            "fill": "cool blue ambient from the right",
            "rim": "subtle edge light separating object from background",
            "mood": "industrial, gritty, functional beauty",
        },
        "details": [
            f"this is a {subcategory.replace('-', ' ')} from a post-apocalyptic factory game",
            "square aspect ratio (1:1) for use as an icon/thumbnail",
            "the object should be clearly identifiable and readable at small sizes",
            "no text or labels in the image",
        ],
    }

    return profile


def make_hero_profile(entry: dict) -> dict:
    """Generate a full cinematic scene profile for hero entries."""
    entry_id = entry["_id"]
    display_name = entry.get("displayName", entry_id.replace("_", " ").title())
    description = entry.get("description", "")
    model_style = entry.get("modelStyle", "")
    category = entry["_category"]
    subcategory = entry["_subcategory"]
    slop = entry.get("slopCommentary", "")

    # Build a rich scene prompt based on what we know about the entry
    obj_desc = model_style or description

    # Category-specific scene templates
    if subcategory == "weapons":
        return {
            "filename": entry_id,
            "category": category,
            "title": f"{display_name} — weapon concept art",
            "scene": {
                "subject": f"a post-apocalyptic worker wielding {display_name.lower()} in a ruined factory environment",
                "composition": "dynamic action pose, 16:9, weapon as clear focal point",
                "camera": "medium shot, slightly low angle to make the weapon imposing",
                "focal_point": f"the {display_name.lower()} itself, with detail on its improvised construction",
            },
            "environment": {
                "setting": "interior of a damaged factory building with broken machinery and overgrowth",
                "lighting_source": "shafts of light through holes in the ceiling",
                "decay": "rust, vines, cracked concrete, scattered debris",
            },
            "lighting": {
                "key_light": "warm amber shaft light hitting the weapon and upper body",
                "fill": "cool blue ambient from deep in the factory",
                "accent": "sparks or muzzle flash if appropriate for the weapon type",
                "mood": "tense, ready for combat, industrial grit",
            },
            "details": [
                f"the weapon is: {obj_desc}" if obj_desc else f"an improvised {display_name.lower()}",
                "the worker wears patched coveralls, hard hat, and scavenged gear",
                "the environment suggests danger — the worker is alert",
                "16:9 aspect ratio, cinematic composition",
            ],
        }

    if subcategory == "fauna":
        attack_dmg = entry.get("attackDamage", "")
        move_speed = entry.get("moveSpeed", "")
        return {
            "filename": entry_id,
            "category": category,
            "title": f"{display_name} — creature concept art",
            "creature_design": {
                "species": display_name,
                "description": obj_desc or f"a mutated creature called {display_name}",
                "biology": "organic tissue fused with machine components — hydraulic joints, corroded plating, bioluminescent patches",
                "behavior": entry.get("behavior", "hostile, territorial"),
                "scale": "large enough to threaten an adult human" if (attack_dmg and float(str(attack_dmg)) > 15) else "medium-sized, pack hunter proportions",
            },
            "scene": {
                "subject": f"a {display_name.lower()} in its natural habitat within the ruined factory complex",
                "composition": "dramatic portrait, the creature filling the frame, 16:9",
                "camera": "eye level with the creature, confrontational",
                "focal_point": "the creature's face and most threatening feature",
            },
            "environment": {
                "setting": "ruined factory interior or overgrown industrial exterior",
                "atmosphere": "misty, bioluminescent spore particles in the air",
            },
            "lighting": {
                "key_light": "bioluminescent blue-green glow from the creature itself",
                "fill": "dim amber from distant factory lights",
                "accent": "reflected light from wet surfaces and corroded metal",
                "mood": "menacing, alien, beautiful in a horrible way",
            },
            "details": [
                f"creature description: {obj_desc}" if obj_desc else "biomechanical horror",
                "the creature should look like it evolved in this industrial environment",
                "visible machine parts integrated into its biology",
                "16:9 aspect ratio, cinematic composition",
            ],
        }

    if subcategory == "biomes":
        vertex_colors = entry.get("vertexColors", {})
        return {
            "filename": entry_id,
            "category": category,
            "title": f"{display_name} biome — landscape concept art",
            "scene": {
                "subject": f"a panoramic vista of the {display_name} biome surrounding the ruined factory complex",
                "composition": "wide establishing shot, 16:9, expansive landscape",
                "camera": "elevated viewpoint, showing terrain stretching to the horizon",
                "focal_point": "the distinctive biome features in the middle ground",
            },
            "environment": {
                "biome": display_name,
                "description": description or f"the {display_name} biome",
                "factory_remnants": "rusted industrial structures partially visible, integrated into the landscape",
                "time_of_day": "golden hour",
            },
            "lighting": {
                "key_light": "warm golden sunset light",
                "fill": "ambient sky light",
                "accent": "volumetric light shafts through haze",
                "mood": "vast, atmospheric, beautiful desolation",
            },
            "details": [
                f"biome description: {description}" if description else f"the {display_name} biome",
                "industrial ruins partially reclaimed by nature",
                "the landscape should feel expansive and explorable",
                "16:9 aspect ratio",
            ],
        }

    # Default hero: scene-based concept art
    return {
        "filename": entry_id,
        "category": category,
        "title": f"{display_name} — concept art",
        "scene": {
            "subject": f"{display_name} in the Slopworks factory complex",
            "composition": "cinematic framing, 16:9, dramatic composition",
            "camera": "medium shot with environmental context",
            "focal_point": display_name,
        },
        "environment": {
            "setting": "ruined industrial factory complex",
            "atmosphere": "post-apocalyptic, overgrown, atmospheric haze",
        },
        "lighting": {
            "key_light": "warm amber industrial light",
            "fill": "cool blue ambient",
            "accent": "orange accent from furnaces or fire",
            "mood": "cinematic, gritty, atmospheric",
        },
        "details": [
            f"description: {obj_desc}" if obj_desc else display_name,
            "post-apocalyptic industrial setting",
            "16:9 aspect ratio, cinematic composition",
        ],
    }


def generate_image(profile: dict, force: bool = False) -> bool:
    """Generate a single image from a profile via Gemini."""
    filename = profile["filename"]
    filepath = OUTDIR / f"{filename}.png"

    if filepath.exists() and not force:
        print(f"  SKIP: {filename} (exists)")
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
                print(f"  OK: {filename} ({size_kb:.0f} KB)")
                return True

        print(f"  WARN: {filename} — no image returned")
        return False

    except Exception as e:
        err = str(e)
        if "exhausted" in err.lower() or "quota" in err.lower() or "429" in err:
            print(f"  QUOTA: {filename} — rate limited, waiting 30s...")
            time.sleep(30)
            return generate_image(profile, force)
        print(f"  FAIL: {filename} — {err[:200]}")
        return False


def main():
    api_key = os.environ.get("GOOGLE_API_KEY")
    force = "--force" in sys.argv
    dump = "--dump" in sys.argv
    heroes_only = "--heroes-only" in sys.argv

    only = None
    if "--only" in sys.argv:
        idx = sys.argv.index("--only")
        if idx + 1 < len(sys.argv):
            only = sys.argv[idx + 1]

    entries = collect_all_entries()

    if only:
        entries = [e for e in entries if e["_category"] == only or e["_subcategory"] == only]
        if not entries:
            print(f"no entries matching '{only}'")
            sys.exit(1)

    # Build profiles
    profiles = []
    for entry in entries:
        eid = entry["_id"]
        if heroes_only and eid not in HERO_ENTRIES:
            continue
        if eid in HERO_ENTRIES:
            profiles.append(make_hero_profile(entry))
        else:
            profiles.append(make_icon_profile(entry))

    if dump:
        for p in profiles:
            print(f"=== {p['filename']} ({p['category']}) ===")
            print(profile_to_prompt(p))
            print()
        print(f"{len(profiles)} profiles total ({sum(1 for p in profiles if p['filename'] in HERO_ENTRIES)} hero, {sum(1 for p in profiles if p['filename'] not in HERO_ENTRIES)} icon)")
        return

    if not api_key:
        print("Error: set GOOGLE_API_KEY environment variable")
        print("  Get one at: https://aistudio.google.com/apikey")
        sys.exit(1)

    genai.configure(api_key=api_key)
    OUTDIR.mkdir(parents=True, exist_ok=True)

    total = len(profiles)
    hero_count = sum(1 for p in profiles if p["filename"] in HERO_ENTRIES)
    icon_count = total - hero_count
    print(f"Generating {total} bible art pieces ({hero_count} hero concept art, {icon_count} icons)")
    print(f"Output: {OUTDIR}/")
    if force:
        print("Force mode: regenerating all images")
    print()

    success = 0
    failed = []

    for i, profile in enumerate(profiles):
        ptype = "HERO" if profile["filename"] in HERO_ENTRIES else "icon"
        print(f"[{i+1}/{total}] {profile['filename']} ({ptype})")
        if generate_image(profile, force):
            success += 1
        else:
            failed.append(profile["filename"])

        if i < total - 1:
            time.sleep(2)

    print()
    print(f"Done: {success}/{total} generated")
    if failed:
        print(f"Failed: {', '.join(failed)}")
        print("Re-run to retry failed images.")


if __name__ == "__main__":
    main()
```

**Step 2: Test with --dump (no API key needed)**

Run: `python3 tools/generate-bible-art.py --dump --only items 2>&1 | head -50`

Expected: structured prompts printed for each item, with object/background sections for icons and scene sections for hero entries (weapons).

Run: `python3 tools/generate-bible-art.py --dump --heroes-only 2>&1 | tail -5`

Expected: count line showing ~28 hero profiles, 0 icon profiles.

**Step 3: Commit**

```bash
git add tools/generate-bible-art.py
git commit -m "Add bible art generator with icon and hero concept art profiles"
```

---

### Task 5: Generate art and rebuild pages

This task requires a `GOOGLE_API_KEY`. Run interactively.

**Step 1: Generate hero art first (smaller batch)**

```bash
export GOOGLE_API_KEY="your-key-here"
python3 tools/generate-bible-art.py --heroes-only
```

Expected: ~28 images generated in `docs/assets/img/bible/`. Takes ~15-20 minutes with rate limiting.

**Step 2: Generate all remaining icons**

```bash
python3 tools/generate-bible-art.py
```

Expected: ~161 additional icon images. Skips already-generated heroes. Takes ~1-2 hours.

**Step 3: Rebuild HTML pages**

```bash
python3 tools/build-bible-web.py
```

Pages now reference real images instead of showing "[ART PENDING]".

**Step 4: Verify in browser**

```bash
python3 -m http.server 8080 --directory docs/
```

Open `http://localhost:8080/bible/` and verify:
- Index page shows category cards and entry grid
- Entry cards show generated thumbnails
- Individual entry pages show full images
- Hero entries (weapons, bosses, biomes) have cinematic 16:9 art
- Icon entries (materials, components) have square item icons
- Nav links work from all page depths
- Search filtering works

**Step 5: Commit generated content**

```bash
git add docs/assets/img/bible/
git add docs/bible/
git commit -m "Add bible web pages and generated art for all 189 entries"
```

---

### Task 6: Add generated HTML to .gitignore (optional)

Decide whether to commit generated HTML or regenerate on each build. Since GitHub Pages deploys from committed files, the HTML must be committed. But we should document that it's generated.

**Step 1: Add build note to bible README**

Add a section to `docs/bible/README.md` noting that `index.html` files are generated by `tools/build-bible-web.py` and should be regenerated after editing bible YAML.

**Step 2: Commit**

```bash
git add docs/bible/README.md
git commit -m "Document bible web page build process in README"
```

---

### Task 7: Push and create PR

**Step 1: Push to joe/main**

```bash
git push origin joe/main
```

**Step 2: Create PR**

```bash
gh pr create --base master --head joe/main --title "Add bible web pages with generated art" --body "$(cat <<'EOF'
## Summary
- Bible web pages for all 189 game entries at /bible/
- Index page with category cards and global search
- Category listing pages with filtered entry grids
- Individual entry detail pages with stats, SLOP commentary, cross-references
- Gemini-generated concept art (28 hero scenes) and item icons (161 icons)
- Shared bible parsing module for web and art tools
- Bible link added to site navigation

## Test plan
- [ ] Open /bible/ and verify category cards show correct counts
- [ ] Search filters entries by name and ID
- [ ] Click through to category pages and entry detail pages
- [ ] Verify nav links work from all page depths
- [ ] Verify images load for hero and icon entries
- [ ] Check mobile responsive layout

Generated with Claude Code
EOF
)"
```
