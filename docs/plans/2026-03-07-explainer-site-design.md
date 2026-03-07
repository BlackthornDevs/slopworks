# Slopworks explainer site design

**Date:** 2026-03-07
**Author:** Joe (brainstormed with Claude)
**Status:** Approved

---

## Purpose

A multi-page website that explains Slopworks to two audiences:
1. **Potential players** — people browsing who might want to play the game
2. **Friends and family** — people who ask "so what are you building?"

The site should sell the fantasy, convey the vibe, and make the game immediately understandable to someone who's never heard of Satisfactory or Factorio.

---

## Framing: employee orientation packet

The site is framed as a **Slopworks Industrial employee onboarding document**. SLOP narrates in corporate jargon. Each page is a "department briefing" with real game info wrapped in dark humor. Between SLOP's voice, normal callout boxes explain actual mechanics plainly.

This gives visitors a taste of the game's tone before they ever play it.

---

## Visual style

- **Dark background** — industrial charcoal/near-black
- **Accent color** — safety orange (#FF6600) and amber (#FFAA00)
- **Typography** — monospace for SLOP's voice (e.g. JetBrains Mono or Space Mono from Google Fonts), clean sans-serif for player-facing text (e.g. Inter)
- **Texture** — subtle worn metal/concrete background patterns, caution stripe dividers
- **Effects** — redacted text (black bars you can hover to reveal), flickering/glitch effects on SLOP text, scan lines on interactive terminals
- **Concept art** — Gemini-generated images in a consistent painterly/industrial style placed throughout

---

## Tech stack

- Static HTML/CSS/JS — no build process
- Three.js from CDN for interactive 3D scenes
- Gemini-generated concept art saved as images in `site/assets/img/`
- Lives in the Slopworks repo under `site/`

---

## Page structure

### 1. index.html — "Welcome to Slopworks Industrial"

SLOP's welcome message. Hero concept art of the ruined factory complex at sunset/dusk. Brief elevator pitch in normal text.

**Three.js:** Slow camera drift over a low-poly industrial landscape — rusted buildings, smokestacks, overgrown ground. Sets the mood immediately.

**Concept art needed:**
- Hero panorama: ruined factory complex at golden hour, overgrown, smoke/haze
- SLOP logo/emblem: corporate logo that's cracked and weathered

**Content:**
- SLOP welcome: "Welcome to Slopworks Industrial. You have been voluntarily reassigned to Restoration Division. Please review your orientation materials and report to your designated workstation."
- Plain-text pitch: what the game is in 2-3 sentences
- Navigation to other pages styled as "department briefings"

---

### 2. assignment.html — "Your assignment"

The core gameplay loop: scout, breach, clear, restore, connect, automate, defend, expand. Each step gets a brief explanation with concept art.

**Three.js:** An animated loop diagram — icons for each step connected by arrows that pulse in sequence, showing the cycle.

**Concept art needed:**
- Player approaching a ruined building entrance (first-person perspective)
- Isometric overworld map view showing connected buildings and supply lines
- Player restoring mechanical systems inside a building (pipes, ducts, panels)

**Content:**
- SLOP briefing: describes each step in corporate-speak ("Facility reclamation protocol")
- Plain callouts: what you actually do at each step
- The three world spaces explained: home base, reclaimed buildings, overworld

---

### 3. facilities.html — "Company facilities"

Factory automation and base building. The Satisfactory-style home base, conveyor belts, machines, the distributed production network.

**Three.js:** Interactive isometric factory grid. User can rotate/zoom. Animated conveyor belts with items flowing between machines. Maybe a small pre-built factory the user can examine.

**Concept art needed:**
- Isometric view of a home base factory with conveyors, machines, foundations
- Close-up of a conveyor belt carrying materials between machines
- Overworld supply chain diagram (abstract, showing buildings as nodes)

**Content:**
- SLOP briefing: "Productivity optimization guidelines"
- Plain explanation: how factory building works, how supply lines connect buildings
- The distributed production network concept — buildings as production nodes

---

### 4. fauna.html — "Unauthorized biological occupants"

Combat, creatures, wave defense, the threat meter. The fauna types by biome.

**Three.js:** A threat meter visualization that fills as you hover/scroll, showing how expansion increases danger. Or an animated wave approaching a defended base.

**Concept art needed:**
- Biomechanical hybrid creature in a ruined heavy manufacturing building
- Pack hunters swarming through a warehouse
- Apex predator in a power plant, glowing with energy
- Turret defense line with incoming wave

**Content:**
- SLOP briefing: insists the fauna are "within manageable parameters" while the art shows otherwise
- Plain explanation: combat mechanics, fauna types, threat meter, wave defense
- How defenses work: turrets, walls, power dependency

---

### 5. colleagues.html — "Colleague coordination"

Co-op multiplayer. 1-4 players, drop-in/drop-out, shared progress.

**Three.js:** Four player silhouettes in a base, with lines showing them splitting up to different tasks (one building, one defending, one exploring, one at the overworld map).

**Concept art needed:**
- 2-4 players defending a base together during a wave attack
- Players splitting up — one heading into a building while others manage the factory
- Co-op factory building moment

**Content:**
- SLOP briefing: "Mandatory team-building exercise protocols"
- Plain explanation: how co-op works, what's shared, drop-in/drop-out

---

### 6. slop.html — "Meet your supervisor"

The AI character. SLOP's personality, unreliability, dark humor. The endgame twist (hinted, not spoiled).

**Three.js:** Interactive SLOP terminal. A CRT-style monitor where the user can click pre-written prompts and SLOP responds with in-character lines. Glitch effects, scan lines, amber-on-black text.

**Concept art needed:**
- A battered CRT terminal in a ruined office, SLOP's interface glowing on screen
- A hallway with faded safety posters — absurd corporate messaging amid decay
- SLOP's "face" — whatever visual representation works (screen, logo, speaker system)

**Content:**
- SLOP speaks for itself here — extended dialogue samples
- Plain explanation: SLOP is an AI character, not a menu. It lies, it malfunctions, it's funny
- Tease the endgame mystery without spoiling it

---

### 7. blueprints.html — "What makes this different"

The BIM pipeline. Real buildings as game levels. Kevin's secret weapon.

**Three.js:** A real building wireframe (simplified) that transitions from clean blueprint to ruined/overgrown state. User can scrub a slider between "before" and "after."

**Concept art needed:**
- Split image: clean architectural blueprint on one side, ruined overgrown version on the other
- A real building's mechanical room rendered as a game level
- Side-by-side: Revit model vs. in-game exploration view

**Content:**
- SLOP briefing: "Facility documentation archive" (with corrupted/redacted sections)
- Plain explanation: every building is modeled from real architectural data
- Why this matters for gameplay: authentic layouts, real duct/pipe systems to restore

---

## Shared components

- **Navigation:** top bar styled as a Slopworks Industrial document header with "department" links
- **Footer:** fake corporate footer — "Slopworks Industrial LLC. All rights reserved. SLOP v2.7.1 (build status: NOMINAL)" with a wishlist/follow CTA
- **SLOP interjections:** small pop-up style asides from SLOP that appear on scroll, different per page
- **Consistent art style prompt prefix:** all Gemini art should use the same style prompt to maintain visual consistency

---

## Gemini art style prompt prefix

Use this prefix for all concept art prompts to maintain consistency:

> "Post-apocalyptic industrial concept art, painterly digital illustration style, muted earth tones with orange and amber accent lighting, overgrown ruins, rusted metal, atmospheric haze, cinematic composition. Game concept art for a factory survival game."

Append scene-specific details after this prefix.

---

## Implementation order

1. Generate all concept art via gemini CLI (batch the prompts)
2. Build shared layout: nav, footer, CSS theme, SLOP text effects
3. Build index.html with hero and Three.js landscape
4. Build each content page in order (assignment, facilities, fauna, colleagues, slop, blueprints)
5. Build Three.js interactives (start simple, add complexity)
6. Polish: transitions, scroll effects, SLOP interjections, mobile responsiveness

---

## File structure

```
site/
  index.html
  assignment.html
  facilities.html
  fauna.html
  colleagues.html
  slop.html
  blueprints.html
  css/
    style.css
  js/
    main.js          -- shared nav, SLOP interjections, scroll effects
    three-scenes.js  -- Three.js scene definitions per page
    slop-terminal.js -- interactive SLOP terminal (slop.html)
  assets/
    img/             -- Gemini-generated concept art
    textures/        -- metal, concrete background textures (can be generated or sourced)
```
