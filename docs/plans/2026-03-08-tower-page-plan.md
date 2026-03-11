# Tower page implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a new tower.html page with 3 interactive scenes (skyline, contract board, elevator shaft), 4 generated concept art images, and content sections covering the tower contract system.

**Architecture:** Static HTML page following the existing pattern (vanilla HTML/CSS/JS, Three.js via CDN importmap, no build step). Three scenes: two Three.js (skyline, elevator), one DOM/Canvas (contract board CRT). Art generated via standalone script (the `site/` directory is defunct — everything builds from `docs/`).

**Tech Stack:** HTML5, CSS3, vanilla JS (ES6), Three.js 0.160.0 (CDN), Gemini image generation API

**Design doc:** `docs/plans/2026-03-08-tower-page-design.md`

**Source material:** `docs/bible/systems/tower-contracts.md`

**Security note:** The scene-contracts.js file uses innerHTML for rendering hardcoded contract data with color-coded threat levels. All content is static string literals defined in the script — no user input is involved. This matches the existing pattern in slop.html's terminal (scene-diagnostic.js). The innerHTML usage is safe in this context.

---

### Task 1: Generate tower concept art

**Files:**
- Create: `/tmp/generate-tower-art.py` (standalone, disposable)
- Output: `docs/assets/img/tower-skyline.png`, `tower-contract-board.png`, `tower-elevator.png`, `tower-boss-rooftop.png`

**Note:** The `site/` directory is defunct. This task uses a standalone script that generates images directly to `docs/assets/img/`.

**Step 1: Write standalone generation script**

Create `/tmp/generate-tower-art.py` — a self-contained Python script using `google.generativeai` that:
- Reads API key from pass store (`~/.claude/pass-get claude/api/gemini`)
- Defines 4 tower art profiles using the same JSON structure as the old `site/generate-art.py` (scene, environment, lighting, details fields)
- Converts each profile to a detailed text prompt
- Calls `gemini-3-pro-image-preview` model to generate each image
- Saves PNGs directly to `docs/assets/img/`
- Skips existing files unless `--force` is passed
- Rate-limit handling (30s sleep on 429)

The 4 profiles:
- `tower-skyline` — Night NYC skyline from ground level, 4 skyscrapers glowing amber, post-apocalyptic overgrowth at street level
- `tower-contract-board` — Physical bulletin board in a bunker with pinned contracts, building photos, red string, amber desk lamp
- `tower-elevator` — First-person looking up inside a dark elevator shaft, cables, floor numbers, bioluminescent growth
- `tower-boss-rooftop` — Squad of 3 workers vs biomechanical creature on a skyscraper rooftop, NYC skyline behind, emergency floodlights

Use the exact same VISUAL_IDENTITY dict and `profile_to_prompt()` function from `site/generate-art.py`. Each profile must have the same depth of detail as the existing profiles (scene with subject/composition/camera/focal_point, environment with time_of_day/weather/structures/decay/scale, lighting with key_light/fill/accent/mood, and 4-5 specific details). The boss rooftop profile needs a characters array with 3 entries. Copy the VISUAL_IDENTITY and prompt builder verbatim from the original — the only difference is the output directory and the profiles list.

**Step 2: Generate art**

Run: `python3 /tmp/generate-tower-art.py`
Expected: 4 images in `docs/assets/img/tower-*.png`

**Step 3: Verify images**

Run: `file docs/assets/img/tower-*.png`
Expected: All 4 files report as PNG image data (not text/LFS pointers)

**Step 4: Commit**

```bash
cd /home/jamditis/projects/slopworks
git add docs/assets/img/tower-*.png
git commit -m "art: add tower contract concept art (4 images)"
```

---

### Task 2: Add Tower to navigation

**Files:**
- Modify: `docs/js/main.js` (line 13, NAV_PAGES array)

**Step 1: Add Tower entry to NAV_PAGES**

In `docs/js/main.js`, add between Explore and S.L.O.P.:

```javascript
{ href: 'tower.html', label: 'Tower', id: 'tower' },
```

Full array should be: Home, Story, Build, Explore, Tower, S.L.O.P., Bible

**Step 2: Verify**

Open any existing page in browser. Tower should appear in nav between Explore and S.L.O.P.

**Step 3: Commit**

```bash
cd /home/jamditis/projects/slopworks
git add docs/js/main.js
git commit -m "nav: add Tower link between Explore and S.L.O.P."
```

---

### Task 3: Add Tower card to homepage + CSS

**Files:**
- Modify: `docs/index.html` (~line 143, after the S.L.O.P. card)
- Modify: `docs/css/style.css` (~line 378 after .card-desc, and mobile media query)

**Step 1: Add 5th-card CSS rule**

In `docs/css/style.css`, after the `.card-desc` rule, add:

```css
.card-grid .card:nth-child(5) {
    grid-column: span 2;
}
```

In the mobile media query (`@media (max-width: 768px)`), after `.card-grid { grid-template-columns: 1fr; }`, add:

```css
.card-grid .card:nth-child(5) {
    grid-column: span 1;
}
```

**Step 2: Add Tower card to index.html**

After the S.L.O.P. card, before `</div>` closing card-grid, add a Tower card linking to tower.html with tower-skyline.png image, title "Tower", and description about NYC skyscrapers as vertical dungeons.

**Step 3: Verify**

Open index.html. 5 cards: 2x2 grid + Tower spanning full width at bottom. Mobile: all stack.

**Step 4: Commit**

```bash
cd /home/jamditis/projects/slopworks
git add docs/index.html docs/css/style.css
git commit -m "homepage: add Tower card to card grid"
```

---

### Task 4: Create tower.html page structure

**Files:**
- Create: `docs/tower.html`

**Step 1: Create the full page**

Create `docs/tower.html` following the existing page pattern (explore.html as template). Include:

- Standard head with OG/Twitter meta tags using tower-skyline.png
- `<div id="nav"></div>`
- Page header: h1 "The tower", subtitle "Every building is a contract. Every contract has a ceiling."
- Scene container `#nyc-skyline` with label "S.L.O.P.://NYC_SECTOR [SCANNING]"
- Content split: contract board system text + tower-contract-board.png + SLOP commentary about "vertical asset recovery operations"
- Contract board CRT terminal: `.terminal#contract-terminal` with header and `#contract-output` div
- Building cards: 4 fauna-card style cards for 30 Rock (tier 1, low, 8 floors), MetLife (tier 1, moderate, 12 floors), Woolworth (tier 2, high, 15 floors), One World (tier 2, extreme, 20 floors)
- Content split (reverse): elevator + co-op system text + tower-elevator.png + SLOP commentary
- Scene container `#elevator-shaft` with label "S.L.O.P.://ELEVATOR_CAM [ASCENDING]"
- Content split: three-pillar loop text + tower-boss-rooftop.png + SLOP commentary about hazard compensation + ASCII-style loop diagram
- Footer div
- Script tags: main.js, importmap for Three.js, scene-skyline.js (module), scene-elevator.js (module), scene-contracts.js (regular script)
- SLOP interjection on buildings section about "structural soundness"

**Step 2: Verify**

Open tower.html. Page structure renders, nav highlights Tower, scene containers visible (empty until scene JS is built). Console errors only for missing scene files.

**Step 3: Commit**

```bash
cd /home/jamditis/projects/slopworks
git add docs/tower.html
git commit -m "page: create tower.html with full content structure"
```

---

### Task 5: Build scene-skyline.js (NYC skyline Three.js scene)

**Files:**
- Create: `docs/js/scene-skyline.js`

**Step 1: Create the skyline scene**

ES6 module importing Three.js. IIFE pattern matching scene-security.js. Key elements:

- Container: `#nyc-skyline`
- WebGL availability check + reduced motion detection
- ~40 background buildings as dark BoxGeometry meshes spread across 60-unit width
- 4 target buildings (30 Rock x:-12 h:14, MetLife x:-4 h:12, Woolworth x:5 h:16, One WTC x:14 h:22) in accent-dim color with window dot PlaneGeometry meshes on front faces (random 60% coverage, varying opacity)
- Ground plane
- 60 particles rising from target buildings (additive blending, PointsMaterial), reset when above building height
- Static flash overlay div for loop reset
- Scene label cycles through building names every 3 seconds via setInterval
- Camera: PerspectiveCamera at y:2 z:20, pans from x:-15 to x:18 over 16 seconds
- Resize handler
- Animation loop: advance pan, update particles (rise + opacity pulse), loop reset with flash

**Step 2: Verify**

Open tower.html. Skyline renders: dark city silhouette, 4 amber buildings with lit windows, particles rising, camera panning. Label flickers. Static flash on loop.

**Step 3: Commit**

```bash
cd /home/jamditis/projects/slopworks
git add docs/js/scene-skyline.js
git commit -m "scene: add NYC skyline Three.js scene for tower page"
```

---

### Task 6: Build scene-contracts.js (CRT contract board)

**Files:**
- Create: `docs/js/scene-contracts.js`

**Step 1: Create the contract board terminal animation**

Regular script (NOT ES6 module — no Three.js needed). IIFE pattern. Key elements:

- Container: `#contract-output` inside `#contract-terminal`
- Reduced motion detection (shows all text immediately if enabled)
- 4 contract data objects with id, name, building, tier, threat, floors, players, status
- Threat color mapping: LOW=#5CCFE6, MODERATE=#E8A031, HIGH=#CC3333, EXTREME=#CC3333
- Builds line array: system messages (loading, auth verified, tier access), then for each contract: separator, name, building/floors/squad details, threat level with color-coded span + status
- Typewriter effect: character-by-character with variable speed (15-35ms normal, +80ms on punctuation)
- HTML-aware typing: skip tag characters, only count visible chars for progress
- Each line creates a div.line with appropriate CSS class (system-text, prompt-text, response-text)
- Ends with SLOP recommendation lines (73% survival rate)
- Auto-scrolls output div
- Loops: after all lines typed, 5s pause, clear output, restart
- IntersectionObserver triggers animation only when terminal scrolls into view

**Security context:** All content rendered via innerHTML is hardcoded string literals defined in the CONTRACTS array and threatColors object. No user input, URL parameters, or external data is used. This matches the existing pattern in slop.html's terminal.

**Step 2: Verify**

Open tower.html, scroll to contract board. Terminal types in contracts one by one. Threat levels are color-coded. Loops after 5s pause.

**Step 3: Commit**

```bash
cd /home/jamditis/projects/slopworks
git add docs/js/scene-contracts.js
git commit -m "scene: add contract board CRT terminal animation"
```

---

### Task 7: Build scene-elevator.js (elevator shaft Three.js scene)

**Files:**
- Create: `docs/js/scene-elevator.js`

**Step 1: Create the elevator shaft scene**

ES6 module importing Three.js. IIFE pattern matching scene-security.js. Key elements:

- Container: `#elevator-shaft`
- WebGL check + reduced motion (speedMultiplier = 0 if reduced)
- Green tint overlay (rgba(0,255,65,0.08), mix-blend-mode: multiply) — same pattern as scene-security.js
- Grain canvas overlay (128x128, opacity 0.06, mix-blend-mode: screen, updated every 80ms)
- Static flash overlay for loop reset
- Shaft geometry: 4 PlaneGeometry walls (3x50 units), cross-beams (BoxGeometry) at each floor level, 3 vertical cables (BoxGeometry 0.04 wide)
- Floor number sprites: CanvasTexture with amber monospace numbers on PlaneGeometry, one per floor on back wall
- 15 bioluminescent particles (same pattern as security scene)
- Creature shadow: PlaneGeometry 1.5x0.8, rotated horizontal, crosses shaft at ~60% ride progress
- Camera: PerspectiveCamera FOV 70, starts at y:1, rises to floor 12 over 10 seconds, lookAt straight up (y + 10)
- Label updates with current floor number
- Loop: ride up, flash, reset creature trigger flag

**Step 2: Verify**

Open tower.html, scroll to elevator. Dark shaft, floor numbers scrolling past, particles floating, creature shadow crosses at ~floor 8, green tint + grain. Loops with flash.

**Step 3: Commit**

```bash
cd /home/jamditis/projects/slopworks
git add docs/js/scene-elevator.js
git commit -m "scene: add elevator shaft Three.js scene for tower page"
```

---

### Task 8: Final verification and push

**Step 1: Full page verification checklist**

Open `docs/tower.html` and check:
- Nav shows Tower between Explore and S.L.O.P., highlighted active
- Skyline scene renders and pans with particles
- Contract board content split has image + text + SLOP commentary
- Contract board CRT types in contracts one by one with color-coded threat levels
- Building cards show all 4 buildings with tier/threat info
- Elevator content split has image + text + SLOP commentary
- Elevator shaft scene renders, camera rises, creature crosses
- Three-pillar loop section has image + text + diagram + SLOP commentary
- All 4 concept art images display (or graceful placeholder fallback)
- Footer renders
- Mobile responsive (check at 375px width)
- No console errors

Open `docs/index.html` and check:
- Tower card appears as 5th card spanning full width
- Card links to tower.html
- Image loads or shows placeholder

**Step 2: Commit any final fixes**

```bash
cd /home/jamditis/projects/slopworks
git add -A
git commit -m "tower: final polish and verification"
```
