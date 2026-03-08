# Bible web pages design

**Date:** 2026-03-07
**Author:** Joe (brainstormed with Claude)
**Status:** Approved

---

## Summary

Static web pages for all 189 game bible entries, deployed via GitHub Pages from `docs/`. Two new tools: a build script that generates HTML from bible YAML, and an art generation script that creates Gemini-powered icons and concept art for every entry.

---

## URL structure

- **Index:** `blackthorndevs.com/slopworks/bible/` — category grid with search
- **Category:** `blackthorndevs.com/slopworks/bible/items/` — all entries in that category as cards
- **Detail:** `blackthorndevs.com/slopworks/bible/iron_ore` — individual entry page with stats, art, cross-references

---

## File layout

Generated HTML goes into the existing `docs/bible/` alongside the markdown source. The `.md` files are untouched — GitHub Pages won't render them as HTML pages.

```
docs/bible/
  index.html                     # main bible index (generated)
  items/index.html               # items category page (generated)
  buildables/index.html
  systems/index.html
  world/index.html
  characters/index.html
  iron_ore/index.html            # individual entry pages (~189 directories)
  copper_ore/index.html
  smelter_t1/index.html
  ...
  items/raw-materials.md         # source YAML (unchanged)
  items/weapons.md               # source YAML (unchanged)
```

Images output to `docs/assets/img/bible/[item_id].png`.

---

## Build script: `tools/build-bible-web.py`

Reads all 24 bible markdown files, parses YAML entries, generates static HTML. Uses the shared `docs/css/style.css` from the main site. Adds bible-specific CSS inline.

Entry pages include: icon/art image, stats table, SLOP commentary callout, cross-reference links to other entries, acquisition sources, tags.

Category pages: card grid with thumbnails, brief descriptions, tier/rarity badges.

Index page: category cards with entry counts, global search (client-side JS filtering).

---

## Art generation: `tools/generate-bible-art.py`

Extends the existing `site/generate-art.py` pattern (structured JSON profiles, shared VISUAL_IDENTITY, Gemini API).

Two image types:

**Icons (~160 entries):** Square item renderings on dark industrial background. Simplified JSON profiles focused on the object. Used for raw materials, components, consumables, structural pieces, scenery, recipes, research nodes, status effects, upgrades, supply lines, dialogue, narrative chapters, NPCs (portrait style).

**Concept art (~25 entries):** Full cinematic scenes using the same detailed profile format as the existing site art. Used for weapons, boss fauna, biomes, major machines, turrets, the tower, SLOP terminal.

The script reads bible YAML to auto-generate icon profiles from item descriptions and modelStyle fields. Manual override profiles for the ~25 "hero" entries that get full concept art.

---

## Page design

- Matches existing site theme: same CSS custom properties (`--bg`, `--accent`, `--font-body`, etc.), same fonts (IBM Plex Sans, Oswald, Space Mono), same layout patterns
- Nav bar links back to main site pages
- Caution stripe dividers between sections
- Art frame component for images (same `.art-frame` class)
- SLOP commentary in styled callout boxes
- Stats rendered as definition lists or compact tables
- Tags as pill badges
- Cross-references as clickable links to other bible entry pages
- Mobile responsive (same breakpoints as main site)

---

## Deployment

Generated HTML is committed to the repo and deployed automatically via GitHub Pages from `docs/`. The build script is run manually (`python3 tools/build-bible-web.py`) whenever bible content changes. Art generation is run separately (`python3 tools/generate-bible-art.py`).
