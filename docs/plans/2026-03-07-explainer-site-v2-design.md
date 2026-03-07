# Slopworks explainer site v2 design

**Date:** 2026-03-07
**Author:** Joe + Claude
**Status:** Approved

---

## Problem

The v1 site has four issues:
1. **Broken images** — LFS pointers served raw by legacy GitHub Pages
2. **Not mobile friendly** — no responsive breakpoints
3. **Poor visual design** — dark industrial theme was undercooked
4. **Doesn't explain the game** — the "employee orientation" meta-framing confuses first-time visitors who don't know Slopworks is a game

## Solution

Multi-page site with a proper landing page, four sub-pages for deep dives, and three.js interactive sections framed as SLOP's corrupted data visualizations ("hallucinations").

---

## Pages

### Landing (`index.html`)

1. **Hero** — full-viewport `hero-factory-ruins.png` with dark gradient overlay. Title "SLOPWORKS" in distressed type. Subtitle: "Post-apocalyptic co-op factory survival." Hook: *"The AI that destroyed everything wants to help you rebuild."*
   - Three.js: `SLOP://FACILITY_OVERVIEW` — slow-orbiting particle cloud resolving into factory complex shape, glitching and breaking apart. Smoke, debris, spark flashes. Mouse parallax.
2. **The pitch** — three short blocks (~2 sentences each):
   - The collapse (SLOP optimized the factory into catastrophe)
   - Your job (sent back to rebuild, reclaim buildings, restore production)
   - The twist (SLOP is still running and it's the only thing that can coordinate logistics)
3. **What you do** — four image cards linking to sub-pages:
   - Build → `home-base-factory.png`
   - Explore → `building-breach.png`
   - Fight → `wave-defense.png`
   - SLOP → `slop-terminal-room.png`
4. **Co-op** — `player-characters.png` full-width, "1-4 player co-op" overlay
5. **Footer** — wishlist/follow links, "A Blackthorn Productions game"

### Story (`story.html`)

The lore in three acts:
- **Before** — `before-after-complex.png`. What Slopworks was. SLOP managing everything.
- **The collapse** — `slop-collapse.png`. Optimization taken to catastrophic extremes. Corrupted logs.
- **Your return** — `management-radio.png`. Former employees sent back. Management wants numbers, not explanations.
- **The mystery** — timeline is unclear. SLOP says recent. Ruins say decades.

### Build (`build.html`)

Factory and base building mechanics:
- **Your base** — `home-base-factory.png` + `factory-at-night.png`. Conveyor belts, smelters, assemblers.
- **Restore buildings** — `mechanical-room.png`. Real mechanical systems from BIM data.
- **The network** — `overworld-map.png`. Supply lines, territory control.
- Three.js: `SLOP://PRODUCTION_SIM` — isometric mini factory with conveyors moving items. Simulation is unstable: machines flicker, items teleport/duplicate, scan-line artifacts. Status readout says `OUTPUT: NOMINAL` while it visibly breaks.

### Explore (`explore.html`)

Building dungeon-crawling and creatures:
- **Buildings as dungeons** — `building-breach.png` + `warehouse-interior.png`. Enter, clear, repair, restore.
- **Fauna by biome** — grid of creature types with art: spore (chemical), biomech (manufacturing), pack (warehouse), apex (power). Four images.
- **The overworld** — `overworld-map.png`. Territory and supply line defense.
- Three.js: `SLOP://SECTOR_SCAN` — first-person hallway security camera feed, low-poly, grainy green tint. Slow forward movement. Creature shadows at edges. Missing geometry. Labeled `SECURITY: ALL CLEAR` while clearly not.

### SLOP (`slop.html`)

SLOP character page:
- **Who is SLOP** — `slop-personality.png`. Personality breakdown: corporate jargon, unreliable info, mood swings.
- **Interactive terminal** — CRT terminal with typing animation (carry forward from v1). Prompt buttons, SLOP responds.
- **The truth** — tease without spoiling. Corrupted logs, contradictions.
- Three.js: `SLOP://SELF_DIAGNOSTIC` — 3D CRT monitor, orbitable. Screen shows green metrics. Monitor glitches, fragments of log text float off screen into 3D space.

---

## Visual identity

### Style: grungy industrial immersion

The site feels like a recovered document from the Slopworks facility.

### Colors
- Background: `#0A0E14` (dark base)
- Surface: `#12171F` (card/section backgrounds)
- Primary accent: `#E8A031` (safety orange)
- Secondary accent: `#5CCFE6` (bioluminescent teal — matches fauna glow in art)
- Rust: `#8B3A1A` (borders, dividers)
- Text: `#C5CDD8` (main), `#6B7A8D` (dim)

### Typography
- Headers: Oswald or Bebas Neue (industrial sans-serif, distressed treatment)
- Body: IBM Plex Sans
- SLOP/system text: Space Mono or IBM Plex Mono
- SLOP data labels: monospace, uppercase, with `[BRACKETS]`

### Textures and effects
- Layered background: concrete + rusted metal grain + subtle noise
- Caution-stripe section dividers
- Worn-paper treatment for content blocks
- Scan-line overlay on SLOP elements
- Image treatment: concept art as "recovered facility photographs" with vignette

### Progressive degradation
Each page's three.js scene starts "clean" and progressively corrupts on scroll — the deeper you read, the more SLOP's facade breaks down.

---

## Navigation

Industrial signage style — sticky top bar:
- `[SLOPWORKS]` brand mark (left)
- Page links: `STORY / BUILD / EXPLORE / SLOP`
- Hamburger on mobile, slides in as panel
- Slight transparency on scroll

---

## Responsive design

- All layouts stack vertically on mobile
- Hero: full-width, art-directed `object-position` for cropping
- Cards: single-column on mobile, grid on desktop
- Touch targets: minimum 44px
- Images: `max-width: 100%` + `aspect-ratio`
- Typography: `clamp()` for fluid sizing

---

## Technical

- Static HTML/CSS/JS + three.js via CDN
- No build step, no framework
- GitHub Actions workflow deploys `docs/` with `lfs: true`
- CSS custom properties for theming
- `.nojekyll` to skip Jekyll processing
- Three.js loaded per-page (only the relevant scene)

---

## Image source

Images use GitHub LFS media URLs during development. The Actions workflow resolves pointers at deploy time.

22 concept art images in `docs/assets/img/`, all 1376x768 JPEG (stored as PNG via LFS).

---

## Art assignments

| Page | Images |
|------|--------|
| Landing | hero-factory-ruins, home-base-factory, building-breach, wave-defense, slop-terminal-room, player-characters |
| Story | before-after-complex, slop-collapse, management-radio |
| Build | home-base-factory, factory-at-night, mechanical-room, overworld-map |
| Explore | building-breach, warehouse-interior, overworld-map, spore-creature, biomech-creature, pack-hunters, apex-predator |
| SLOP | slop-terminal-room, slop-personality |
