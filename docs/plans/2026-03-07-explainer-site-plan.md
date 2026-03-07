# Slopworks explainer site implementation plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a multi-page explainer website for Slopworks with Gemini-generated storyboard art, Three.js interactive scenes, and SLOP corporate-humor framing.

**Architecture:** Static HTML/CSS/JS site in `site/` directory. No build process. Three.js from CDN. Gemini CLI generates concept art via `gemini -p -y`. Each page is a standalone HTML file sharing `css/style.css` and `js/main.js`. Three.js scenes are per-page in separate script files.

**Tech Stack:** HTML5, CSS3 (custom properties, grid, animations), vanilla JS, Three.js (r170 from CDN), Google Fonts (Space Mono + Inter), Gemini CLI for image generation

---

## Task 1: Create directory structure and generate concept art batch script

**Files:**
- Create: `site/assets/img/.gitkeep`
- Create: `site/generate-art.sh`

**Step 1: Create the directory tree**

```bash
mkdir -p site/{css,js,assets/{img,textures}}
touch site/assets/img/.gitkeep
```

**Step 2: Write the art generation script**

Create `site/generate-art.sh` — a bash script that calls `gemini -p -y` for each concept art piece. Each prompt uses the shared style prefix. The script saves images to `site/assets/img/` with descriptive filenames.

Art pieces to generate (storyboard-style renderings):

**Lore and world:**
1. `hero-factory-ruins.png` — Panoramic view of the Slopworks Industrial complex in ruins. Massive factory buildings with broken smokestacks, overgrown with vines and moss, golden sunset light cutting through industrial haze. A lone figure in work coveralls stands at the entrance gate. Wide cinematic composition.
2. `slop-collapse.png` — The moment of the cascade failure. Multiple factory buildings mid-explosion, pipes bursting, electrical arcs, smoke and fire. SLOP's corporate logo on a sign in the foreground, still illuminated. Chaos and destruction, industrial disaster scene.
3. `before-after-complex.png` — Split composition: left half shows the Slopworks complex in its prime — clean, organized, workers, trucks, operational smokestacks. Right half shows the same view in ruins — overgrown, collapsed, fauna silhouettes in the shadows. Time has passed.
4. `slop-terminal-room.png` — A dark, abandoned office. Rows of smashed CRT monitors. One terminal still glowing amber with SLOP's interface — cheerful corporate dashboard with graphs all showing catastrophic decline. Dust particles in the screen light. Faded motivational posters on the walls.

**Characters and SLOP:**
5. `player-characters.png` — Four workers in worn industrial coveralls and hard hats, each with different scavenged gear modifications. Standing in front of a makeshift workbench. They look tired but determined. Tool belts, welding masks, improvised weapons.
6. `slop-personality.png` — A wall-mounted speaker system in a ruined corridor, SLOP's voice visualized as amber sound waves. Below the speaker, a faded safety poster reads "PRODUCTIVITY IS SAFETY" while the hallway behind is clearly dangerous — exposed wiring, cracked floor, glowing eyes in the dark.
7. `management-radio.png` — A battered radio receiver on a desk, crackling with static. A notepad with illegible handwritten production quotas. Through the window behind, the ruined factory complex stretches to the horizon.

**Environment and locations:**
8. `home-base-factory.png` — Isometric view of a player-built factory base. Foundation tiles on cleared ground, conveyor belts carrying glowing materials between machines, defensive walls with turrets at the perimeter. Warm amber lighting from furnaces. Organized industrial beauty amid post-apocalyptic surroundings.
9. `building-breach.png` — First-person perspective: a player's gloved hands pushing open a heavy industrial door. Beyond it, a dark corridor with overgrown pipes, dripping water, bioluminescent fungi on the walls. The beam of a flashlight cutting into the darkness. Tense atmosphere.
10. `mechanical-room.png` — A large mechanical room inside a reclaimed building. Massive HVAC ducts, pipe manifolds, electrical panels — all real-looking industrial infrastructure. Some systems restored and glowing with power, others still dark and damaged. A player working on a pipe junction.
11. `overworld-map.png` — Bird's-eye isometric view of the territory. Connected buildings shown as nodes with supply lines (glowing pipes/cables) running between them. The home base at the center, glowing warmly. The frontier at the edges fading into darkness and fog. Threat indicators pulsing red at distant buildings.
12. `warehouse-interior.png` — A massive warehouse interior, shelving units stretching into darkness. Shipping containers stacked haphazardly. Signs of fauna nesting — organic growths, claw marks, webs. Dramatic lighting from holes in the roof.
13. `power-plant.png` — A ruined power plant. Cooling towers visible through broken walls. The reactor room glows with residual energy. Massive scale, dwarfing the human figure in the doorway. Industrial sublime.

**Fauna and combat:**
14. `biomech-creature.png` — A biomechanical hybrid creature in a heavy manufacturing building. It has incorporated machine parts into its body — gears visible in its joints, a hydraulic piston as a limb, metal plating fused with organic tissue. Aggressive stance, glowing eyes. Industrial horror.
15. `pack-hunters.png` — A swarm of small, fast creatures pouring out of warehouse shelving. Dozens of them, coordinated, surrounding a player who has their back to a shipping container. Each creature is rat-sized but with metallic teeth and bioluminescent markings. Frantic energy.
16. `apex-predator.png` — A massive territorial creature in a power plant. It radiates heat and energy, its body crackling with electrical discharge. The size of a truck, blocking a corridor. The player is small in the frame, weapon raised. Boss encounter energy.
17. `wave-defense.png` — A player's fortified base perimeter during a wave attack. Turrets firing orange tracer rounds into a mass of approaching fauna. Spike walls slowing the advance. Explosions from landmines. A second player on a wall platform with a rifle. Chaotic, high-energy defense scene.
18. `spore-creature.png` — A slow-moving creature in a chemical processing building, surrounded by toxic spore clouds. Fungal growths covering its body and spreading to nearby walls. Eerie bioluminescent green and purple coloring. The player keeps distance, wearing an improvised gas mask.

**Major events:**
19. `first-restoration.png` — The moment a building comes online for the first time. Lights flickering on down a corridor, pipes pressurizing with steam, a control panel lighting up green. The player watching with satisfaction. A sense of accomplishment amid the decay.
20. `supply-line-attack.png` — Fauna attacking a supply line between buildings in the overworld. Creatures swarming a glowing pipeline connection. An explosion where they've breached it. Resources spilling. The player rushing to defend from a distance.
21. `factory-at-night.png` — The home base factory at night, fully operational. Warm amber light spilling from furnaces and conveyor systems. Turret spotlights sweeping the dark perimeter. Stars visible through industrial haze. Beautiful and foreboding.
22. `endgame-revelation.png` — A player standing in SLOP's central server room. Banks of old servers, blinking lights, cables everywhere. On the central monitor, SLOP's logs are displayed — showing the optimization decisions that caused the collapse. The player's posture suggests dawning realization. Dramatic, moody lighting.

**Step 3: Write the script**

```bash
#!/bin/bash
# generate-art.sh — batch generate Slopworks concept art via Gemini CLI
# Run from site/ directory. Requires gemini CLI authenticated.
# If quota is hit, the script pauses and retries.

OUTDIR="assets/img"
PREFIX="Storyboard-style concept art rendering, cinematic digital illustration, muted earth tones with orange and amber accent lighting, post-apocalyptic industrial setting, atmospheric haze, detailed and painterly. For a co-op factory survival video game called Slopworks."

generate() {
    local filename="$1"
    local description="$2"

    if [ -f "$OUTDIR/$filename" ]; then
        echo "SKIP: $filename already exists"
        return
    fi

    echo "GENERATING: $filename"
    gemini -p "Generate an image and save it to $(pwd)/$OUTDIR/$filename. The image should be: $PREFIX $description" -y --output-format text 2>&1

    if [ -f "$OUTDIR/$filename" ]; then
        echo "SUCCESS: $filename"
    else
        echo "FAILED: $filename — check quota or prompt"
    fi
    echo "---"
    sleep 2  # rate limit courtesy
}

# Lore and world
generate "hero-factory-ruins.png" "Panoramic view of a massive industrial factory complex in ruins. Broken smokestacks, overgrown with vines and moss, golden sunset light cutting through industrial haze. A lone figure in work coveralls stands at the entrance gate looking in. Wide cinematic composition, 16:9 aspect ratio."

generate "slop-collapse.png" "The moment of an industrial cascade failure. Multiple factory buildings mid-explosion, pipes bursting, electrical arcs, smoke and fire. A corporate logo on a sign in the foreground, still illuminated and reading SLOPWORKS INDUSTRIAL. Chaos and destruction, industrial disaster."

generate "before-after-complex.png" "Split composition: left half shows an industrial complex in its operational prime — clean buildings, workers, trucks, operational smokestacks, blue sky. Right half shows the exact same view decades later in ruins — overgrown, collapsed roofs, creature silhouettes in shadows, orange sunset. Clear dividing line between the two halves."

generate "slop-terminal-room.png" "A dark abandoned corporate office. Rows of smashed CRT monitors on desks. One terminal still glowing amber with a cheerful dashboard interface — graphs all showing catastrophic decline but the header reads STATUS: NOMINAL. Dust particles visible in the screen light. Faded motivational posters on concrete walls reading PRODUCTIVITY IS SAFETY."

# Characters
generate "player-characters.png" "Four industrial workers standing together in front of a makeshift workbench in a ruined factory. Each wears modified work coveralls and hard hats. One has a welding mask pushed up, another has an improvised rifle slung over their shoulder, one carries a large wrench, the last has a tool belt full of scavenged electronics. They look tired but determined. Team portrait composition."

generate "slop-personality.png" "A wall-mounted industrial speaker system in a ruined factory corridor. Amber sound waves visualized emanating from the speaker. Below it, a faded safety poster reads PRODUCTIVITY IS SAFETY with a smiling cartoon worker. The corridor behind is dangerous — exposed wiring sparking, cracked floor revealing pipes below, pairs of glowing eyes visible in the darkness at the far end."

generate "management-radio.png" "Close-up of a battered military-style radio receiver sitting on a metal desk, crackling with visible static lines. Next to it, a notepad with barely legible handwritten production quotas and deadlines. Through a grimy window behind the desk, a vast ruined industrial complex stretches to the horizon under an orange sky."

# Environments
generate "home-base-factory.png" "Isometric view of a player-built factory base in a post-apocalyptic setting. Concrete foundation tiles on cleared ground, conveyor belts carrying glowing orange materials between industrial machines (smelters, assemblers, workbenches). Defensive walls with mounted turrets at the perimeter. Warm amber light from furnaces. Organized industrial beauty surrounded by wasteland. Clean isometric perspective."

generate "building-breach.png" "First-person perspective view: a pair of gloved hands pushing open a heavy rusted industrial door. Beyond the door, a dark corridor with overgrown pipes on the ceiling, water dripping, bioluminescent blue-green fungi growing on walls. A flashlight beam cutting into the darkness revealing more corridor ahead. Tense, atmospheric, horror-adjacent."

generate "mechanical-room.png" "A large industrial mechanical room. Massive HVAC duct systems on the ceiling, pipe manifolds with valves, electrical panels on walls. Half the systems are restored — glowing with amber power indicators, humming. The other half dark, corroded, damaged. A worker in coveralls is reconnecting a pipe junction with a wrench, sparks flying. Realistic industrial infrastructure."

generate "overworld-map.png" "Bird's-eye isometric view of a territory map. Industrial buildings shown as small detailed nodes connected by glowing amber supply lines (pipes and cables). A fortified home base at the center glows warmly. Surrounding territory gets darker toward the edges — fog, unknown terrain, red threat indicators pulsing at distant buildings. Strategic map aesthetic blended with painterly style."

generate "warehouse-interior.png" "Interior of a massive abandoned warehouse. Industrial shelving units stretching into darkness, stacked with rusted containers. Shipping containers arranged haphazardly. Signs of creature nesting — organic growths between shelves, claw marks on metal, web-like structures. Dramatic lighting from holes in the corrugated metal roof, dust motes in light beams."

generate "power-plant.png" "A ruined industrial power plant interior. Concrete cooling towers visible through massive holes in the walls. A reactor room glowing with residual blue-green energy. Enormous industrial scale — turbines, generators, control consoles. A tiny human figure standing in a doorway for scale, dwarfed by the machinery. Industrial sublime, awe-inspiring."

# Fauna and combat
generate "biomech-creature.png" "A biomechanical hybrid creature standing in a heavy manufacturing building. The creature has incorporated machine parts into its biology — visible gears in its joints, a hydraulic piston functioning as one limb, rusted metal plating fused with organic muscular tissue. Aggressive crouching stance, eyes glowing amber. Industrial body horror creature design."

generate "pack-hunters.png" "A swarm of small fast creatures pouring out from between warehouse shelving units. Dozens of rat-sized creatures with metallic teeth and bioluminescent blue markings, moving in coordinated formation. A single worker with their back against a shipping container, flashlight in one hand and improvised weapon in the other, surrounded. Frantic, overwhelming energy."

generate "apex-predator.png" "A massive territorial creature inside a power plant corridor. The creature is the size of a truck, body crackling with electrical discharge, glowing veins of energy across its hide. It blocks the entire corridor. A small human figure in the foreground, weapon raised, looking up at it. Boss encounter scale and drama. Blue and amber lighting."

generate "wave-defense.png" "A fortified factory base perimeter during a nighttime wave attack. Automated turrets on walls firing orange tracer rounds into a mass of approaching creature silhouettes. Spike walls and barbed wire slowing the advance. Explosions from landmines lighting up the scene. A player on an elevated platform firing a rifle. Chaotic, high-energy, action scene."

generate "spore-creature.png" "A slow-moving creature in a ruined chemical processing building, surrounded by clouds of toxic green-purple spores. Fungal growths covering its bulky body and spreading to nearby walls and pipes. Eerie bioluminescent coloring. A player in the background keeping distance, wearing an improvised gas mask, aiming a weapon through the haze."

# Major events
generate "first-restoration.png" "The triumphant moment a ruined building comes online for the first time. Lights flickering on sequentially down a dark industrial corridor. Pipes pressurizing with visible steam. A control panel lighting up green. A worker standing in the corridor watching the lights come on, their posture showing satisfaction. The contrast between the restored glowing section and the still-dark ruins ahead."

generate "supply-line-attack.png" "Aerial view of creatures attacking a supply line between two industrial buildings. A glowing amber pipeline connection being swarmed by dark creature silhouettes. An explosion where the creatures have breached the line, orange resources spilling out. A player vehicle or figure rushing to defend from the left side of the frame. Overworld-scale perspective."

generate "factory-at-night.png" "A fully operational factory base at night. Warm amber light spilling from furnaces, conveyor belt systems glowing with transported materials. Turret spotlights sweeping the dark perimeter, creating dramatic light cones. Stars visible through wispy industrial haze above. The factory is an island of warm light in a dark, hostile world. Beautiful and atmospheric."

generate "endgame-revelation.png" "A lone worker standing in a central server room. Banks of old blinking servers on both sides, cables and wires everywhere. On a large central monitor, scrolling text logs are visible — optimization reports, safety override commands, escalating failure warnings. The worker's posture suggests dawning realization — shoulders dropped, head tilted reading the screen. Dramatic moody blue and amber lighting from the servers."

echo ""
echo "Generation complete. Check $OUTDIR/ for results."
echo "Failed images can be retried by deleting them and running this script again."
```

**Step 4: Commit**

```bash
git add site/
git commit -m "Add site directory structure and art generation script"
```

---

## Task 2: Build the shared CSS theme

**Files:**
- Create: `site/css/style.css`

**Step 1: Write the full CSS theme**

The CSS establishes the industrial aesthetic used across all pages:
- CSS custom properties for all colors, fonts, spacing
- Dark background with subtle noise texture (CSS-generated, no image needed)
- Typography: Space Mono for SLOP voice, Inter for body text
- SLOP text effects: glitch animation, scan line overlay, flicker
- Redacted text: black bars that reveal on hover
- Caution stripe dividers
- Card/callout styles for "plain explanation" boxes
- Responsive grid layout
- Navigation bar styled as document header
- Footer styled as corporate boilerplate
- Concept art image containers with caption styling
- Three.js canvas container styling
- Page transition fade-in on load
- Scroll-triggered animations

This is the largest single file. Full CSS included in the step.

**Step 2: Commit**

```bash
git add site/css/style.css
git commit -m "Add shared CSS theme with industrial aesthetic"
```

---

## Task 3: Build shared JavaScript (navigation, SLOP interjections, scroll effects)

**Files:**
- Create: `site/js/main.js`

**Step 1: Write the shared JS**

`main.js` handles:
- Navigation component injection (each page includes `<div id="nav">` and `<div id="footer">`, JS populates them)
- Current-page highlighting in nav
- Scroll-triggered fade-in animations for content sections
- SLOP interjection system: small pop-up messages from SLOP that appear at scroll thresholds (configurable per page via data attributes)
- Redacted text hover-to-reveal behavior
- Smooth scroll for anchor links
- Mobile hamburger menu toggle

**Step 2: Commit**

```bash
git add site/js/main.js
git commit -m "Add shared JS: navigation, SLOP interjections, scroll effects"
```

---

## Task 4: Build index.html — landing page

**Files:**
- Create: `site/index.html`
- Create: `site/js/scene-hero.js`

**Step 1: Write index.html**

The landing page:
- Full-viewport hero section with concept art background (`hero-factory-ruins.png`) or Three.js canvas fallback
- SLOP welcome message in monospace with glitch effect
- Plain-text elevator pitch (2-3 sentences)
- "Department briefings" navigation grid — 6 cards linking to other pages, each styled as a department folder/dossier with an icon and brief description
- Footer

**Step 2: Write the hero Three.js scene**

`scene-hero.js` creates a simple low-poly industrial landscape:
- Flat ground plane with brownish-green texture
- 5-10 box geometries as ruined buildings (dark gray, varying heights)
- Cylinder geometries for smokestacks
- Subtle fog (orange-tinted)
- Slow orbiting camera
- Ambient + directional light (warm sunset tone)
- Responsive resize handling

This runs behind/below the hero text as atmospheric background. Falls back gracefully if WebGL unavailable.

**Step 3: Commit**

```bash
git add site/index.html site/js/scene-hero.js
git commit -m "Add landing page with hero section and Three.js landscape"
```

---

## Task 5: Build assignment.html — core gameplay loop

**Files:**
- Create: `site/assignment.html`
- Create: `site/js/scene-loop.js`

**Step 1: Write assignment.html**

Page content:
- SLOP briefing header: "Facility reclamation protocol — mandatory reading"
- The 8-step gameplay loop, each step as a card with:
  - Step name and icon
  - SLOP's corporate description (e.g. "Unauthorized biological occupant removal")
  - Plain explanation of what you actually do
  - Concept art image for key steps
- The three world spaces section with concept art for each
- Three.js canvas for the animated loop diagram

**Step 2: Write the loop diagram Three.js scene**

`scene-loop.js`:
- 8 nodes arranged in a circle
- Each node is a labeled 3D panel (text rendered on canvas texture mapped to plane)
- Animated arrow/particle trail connecting nodes in sequence
- A glowing "pulse" that travels the loop continuously
- User can click nodes to highlight that step's description

**Step 3: Commit**

```bash
git add site/assignment.html site/js/scene-loop.js
git commit -m "Add assignment page with gameplay loop and Three.js diagram"
```

---

## Task 6: Build facilities.html — factory automation

**Files:**
- Create: `site/facilities.html`
- Create: `site/js/scene-factory.js`

**Step 1: Write facilities.html**

Page content:
- SLOP briefing: "Productivity optimization guidelines — output targets enclosed"
- Home base factory section: foundations, machines, conveyors, the Satisfactory-style building
- Distributed production network section: buildings as nodes, supply lines, the overworld logistics view
- Supply chain example diagram (HTML/CSS, not an image)
- Concept art throughout
- Three.js interactive factory canvas

**Step 2: Write the interactive factory Three.js scene**

`scene-factory.js`:
- Isometric camera (orthographic)
- A small pre-built factory on a grid:
  - Foundation tiles (flat boxes)
  - 3 machine blocks (colored cubes with labels)
  - Conveyor belts between them (animated boxes moving along paths)
  - A storage container at each end
- Items (small colored spheres) move along conveyors
- User can orbit/zoom with mouse
- Labels float above machines

**Step 3: Commit**

```bash
git add site/facilities.html site/js/scene-factory.js
git commit -m "Add facilities page with factory demo and Three.js interactive"
```

---

## Task 7: Build fauna.html — combat and creatures

**Files:**
- Create: `site/fauna.html`
- Create: `site/js/scene-threat.js`

**Step 1: Write fauna.html**

Page content:
- SLOP briefing: "Unauthorized biological occupant status report — all sectors within manageable parameters"
- Fauna types by biome — grid of creature cards with concept art and descriptions
  - Chemical: spore creatures
  - Manufacturing: biomechanical hybrids
  - Warehouse: pack hunters
  - Power: apex predators
  - Overworld: general mutated wildlife
- Defense systems section: turrets, walls, power dependency
- Threat meter explanation
- Wave defense concept art
- Three.js threat meter visualization

**Step 2: Write the threat meter Three.js scene**

`scene-threat.js`:
- A vertical/horizontal gauge that fills as the user scrolls down the page
- As it fills, the scene changes:
  - Low threat: calm, few particles, green tint
  - Medium: more particles, amber tint, distant silhouettes
  - High: lots of particles, red tint, creature shapes approaching
- Simple but atmospheric

**Step 3: Commit**

```bash
git add site/fauna.html site/js/scene-threat.js
git commit -m "Add fauna page with creature gallery and threat meter"
```

---

## Task 8: Build colleagues.html — co-op multiplayer

**Files:**
- Create: `site/colleagues.html`
- Create: `site/js/scene-coop.js`

**Step 1: Write colleagues.html**

Page content:
- SLOP briefing: "Mandatory team-building exercise protocols — attendance is not optional"
- 1-4 players, drop-in/drop-out
- What's shared: world, progress, factory, threat level
- How players split up: one builds, one defends, one explores, one manages overworld
- Co-op concept art
- Three.js co-op visualization

**Step 2: Write the co-op Three.js scene**

`scene-coop.js`:
- Top-down view of a simplified base
- 4 colored dot/silhouette figures
- Animated: they split up and move to different areas (factory, perimeter, building entrance, overworld terminal)
- Lines/trails showing their paths
- Labels appear as each reaches their task area

**Step 3: Commit**

```bash
git add site/colleagues.html site/js/scene-coop.js
git commit -m "Add colleagues page with co-op visualization"
```

---

## Task 9: Build slop.html — interactive SLOP terminal

**Files:**
- Create: `site/slop.html`
- Create: `site/js/slop-terminal.js`

**Step 1: Write slop.html**

Page content:
- SLOP speaks for itself — no "briefing" framing, SLOP IS the page
- Character introduction: who SLOP is, its personality
- Dialogue samples showing different modes: corporate optimism, passive-aggressive, paranoid, accidentally honest
- SLOP's unreliability: bad map data, wrong crafting advice, selective memory
- Endgame tease: "SLOP doesn't know what it doesn't know" (no spoilers)
- Interactive terminal section: full-width CRT terminal with scan lines
- Concept art of SLOP's physical presence in the world

**Step 2: Write the SLOP terminal**

`slop-terminal.js`:
- CRT monitor aesthetic: rounded corners, scan lines (CSS), slight screen curvature, amber-on-black text
- Pre-written prompt buttons the user can click:
  - "What happened here?" → SLOP deflects
  - "Is it safe?" → SLOP lies cheerfully
  - "What are those creatures?" → "Unauthorized biological occupants"
  - "Tell me about yourself" → corporate mission statement
  - "What caused the collapse?" → defensive, evasive
  - "Are you lying?" → offended corporate response
  - "Show me the logs" → [REDACTED] with glitch effect
- Typing animation for SLOP's responses
- Occasional random glitch/flicker
- Ambient CRT hum sound effect (optional, user-toggled)

**Step 3: Commit**

```bash
git add site/slop.html site/js/slop-terminal.js
git commit -m "Add SLOP page with interactive terminal"
```

---

## Task 10: Build blueprints.html — BIM pipeline / what makes it different

**Files:**
- Create: `site/blueprints.html`
- Create: `site/js/scene-blueprint.js`

**Step 1: Write blueprints.html**

Page content:
- SLOP briefing: "Facility documentation archive — [SECTIONS REDACTED BY ORDER OF MANAGEMENT]"
- What makes Slopworks different: every building is from real architectural data
- The BIM pipeline explained simply: real buildings → game levels
- What this means for gameplay: authentic layouts, real mechanical rooms, real duct/pipe systems
- Concept art: blueprint vs. ruin comparisons, mechanical room as game level
- Three.js before/after building visualization

**Step 2: Write the blueprint transition Three.js scene**

`scene-blueprint.js`:
- A simple wireframe building (boxes for rooms, cylinders for pipes/ducts)
- Two states: "blueprint" (clean, white/blue wireframe) and "ruins" (displaced geometry, green vegetation particles, warm amber tones, some sections missing)
- A slider or scroll-linked transition that morphs between the two states
- Geometry displaces, colors shift, particles appear/disappear

**Step 3: Commit**

```bash
git add site/blueprints.html site/js/scene-blueprint.js
git commit -m "Add blueprints page with BIM pipeline explanation and Three.js transition"
```

---

## Task 11: Run concept art generation

**Step 1: Run the generation script**

```bash
cd site && bash generate-art.sh
```

This may need to be run in batches if Gemini's quota throttles. The script skips already-generated images, so it's safe to re-run.

**Step 2: Review generated images**

Open each image and verify quality. Re-generate any that don't match the desired style by deleting the file and re-running the script (or tweaking the prompt in the script).

**Step 3: Commit the art**

```bash
git add site/assets/img/
git commit -m "Add Gemini-generated concept art for explainer site"
```

---

## Task 12: Polish and mobile responsiveness

**Files:**
- Modify: `site/css/style.css`
- Modify: `site/js/main.js`

**Step 1: Add mobile breakpoints**

- Hamburger menu for nav at < 768px
- Single-column layout for content sections
- Three.js canvases resize responsively
- Concept art images scale properly
- Touch-friendly SLOP terminal buttons

**Step 2: Add page transitions**

- Fade-in on page load
- Scroll-triggered section reveals
- SLOP interjection timing tweaks per page

**Step 3: Cross-browser test**

Open in Safari and Chrome. Verify:
- Three.js scenes render
- Fonts load
- Animations play
- Mobile layout works (use responsive mode in dev tools)

**Step 4: Commit**

```bash
git add site/
git commit -m "Polish: mobile responsiveness, transitions, cross-browser fixes"
```

---

## Task 13: Final review and local server test

**Step 1: Serve locally**

```bash
cd site && python3 -m http.server 3000
```

**Step 2: Walk through every page**

Verify:
- All concept art loads (no broken images)
- Three.js scenes run without console errors
- Navigation works between all pages
- SLOP terminal responds to all prompts
- Scroll effects fire at correct positions
- Footer appears on every page
- Mobile layout works

**Step 3: Fix any issues found**

**Step 4: Final commit**

```bash
git add site/
git commit -m "Slopworks explainer site: complete with concept art, Three.js, and SLOP terminal"
```
