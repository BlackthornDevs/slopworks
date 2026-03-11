# Tower page design

Date: 2026-03-08

## Summary

New `tower.html` page for the slopworks explainer site. Showcases the tower contract system — 4 real NYC skyscrapers as vertical dungeon expeditions. Three Three.js/canvas scenes, 4 generated concept art images, and content sections covering the contract board, elevator co-op, and three-pillar progression loop.

## Page structure

```
tower.html
├── Page header: "The tower" / "Every building is a contract. Every contract has a ceiling."
├── Scene 1: NYC skyline silhouette (scene-skyline.js)
├── Content split: Contract board system + tower-contract-board.png
├── Caution divider
├── Scene 2: Contract board CRT (DOM/Canvas terminal animation)
├── Building cards: 4 cards (30 Rock, MetLife, Woolworth, One World Trade)
│   └── Reuses fauna-card pattern: image + name + tier + threat + SLOP quote
├── Caution divider
├── Content split (reverse): Elevator system + co-op + tower-elevator.png
├── Scene 3: Elevator shaft first-person (scene-elevator.js)
├── Content split: Three-pillar loop + loot + tower-boss-rooftop.png
└── Footer
```

## Three.js scenes

### Scene 1 — scene-skyline.js (NYC skyline)

- Dark skyline built from Three.js box geometries
- 4 target buildings (30 Rock, MetLife, Woolworth, One World Trade) glow in `--accent` (#E8A031)
- Remaining buildings use `--bg-surface` (#12171F)
- Slow horizontal camera pan across the skyline
- Particles drift upward from target buildings (additive blending)
- SLOP label flickers between building names as they enter frame
- Loop resets with static flash (same pattern as scene-security.js)
- Reduced motion: static frame, no pan

### Scene 2 — contract board CRT (DOM/Canvas)

- NOT Three.js — pure DOM animation inside a `.terminal` container
- Matches the existing SLOP terminal pattern from slop.html
- Contracts type in one by one with cursor effect
- Threat levels blink (low=teal, moderate=accent, high=red, extreme=red+pulse)
- Tier progress bar fills as contracts "complete"
- CRT scan lines overlay via CSS
- Loops on a timer, clears and re-types

### Scene 3 — scene-elevator.js (elevator shaft)

- First-person perspective looking up through a dark elevator shaft
- Floor numbers painted on shaft walls scroll down as camera rises
- Ceiling pipes and structural beams pass through frame
- Bioluminescent particles float in the shaft (same particle pattern as scene-security.js)
- Creature shadow crosses at a random floor (reuses creature cross pattern)
- Green night-vision tint + grain overlays (same DOM overlay pattern as scene-security.js)
- Loop: ride up ~12 floors over 10 seconds, static flash, reset
- Reduced motion: static frame at floor 1

## Concept art (4 images)

Generate via standalone script with JSON profiles (same depth/structure as existing art in the repo):

1. **tower-skyline.png** — Night NYC skyline from ground level. 4 specific skyscrapers lit with warm amber glow against dark sky. Post-apocalyptic overgrowth at street level, broken vehicles, bioluminescent patches. 16:9, moody establishing shot.

2. **tower-contract-board.png** — Physical bulletin board in a dimly lit bunker/office. Paper contracts pinned with building photographs, floor plans, and red string connecting intel. Amber desk lamp casting warm pools of light. Worn table surface. 16:9.

3. **tower-elevator.png** — First-person view looking up inside a dark industrial elevator shaft. Steel cables visible, concrete walls with painted floor numbers, bioluminescent fungal growth on surfaces, dim amber emergency lighting from below. 16:9.

4. **tower-boss-rooftop.png** — Boss encounter on a skyscraper rooftop at night. Squad of 2-3 players in industrial gear facing a large biomechanical hybrid creature. NYC skyline behind, amber emergency floodlights, debris and broken HVAC equipment. 16:9.

## Navigation changes

### main.js NAV_PAGES

Add between Explore and S.L.O.P.:
```javascript
{ href: 'tower.html', label: 'Tower', id: 'tower' },
```

### index.html card grid

Add 5th card linking to tower.html. Use tower-skyline.png as card image. The 5th card spans full width at bottom via CSS:

```css
.card-grid .card:nth-child(5) {
    grid-column: span 2;
}

@media (max-width: 768px) {
    .card-grid .card:nth-child(5) {
        grid-column: span 1;
    }
}
```

Card content:
- Title: "Tower"
- Description: "Real NYC skyscrapers repurposed as vertical dungeons. Take contracts from Management, clear floors of fauna, hit elevator checkpoints, extract loot that feeds your factory. 1-4 player co-op scaling."

## Content sections

### Section 1 — Contract board system

Content split with tower-contract-board.png.

Text: Management provides a contract progression tree — not random missions, but a structured climb through increasingly dangerous buildings. Each contract shows intel: threat level, suspected fauna, guaranteed loot, recommended squad size. You choose which to take, but you can't skip tiers.

SLOP commentary: "Management has assigned you a routine vertical asset recovery operation. SLOP has reviewed the building's pre-collapse tenant records and can confirm that none of the current occupants have valid leases. Proceed with confidence."

### Section 2 — Building cards (4 cards)

Grid of 4 cards, one per skyscraper:

| Building | Tier | Threat | Players | SLOP quote |
|----------|------|--------|---------|------------|
| 30 Rockefeller Plaza | 1 | Low | 1 | "...none of the current occupants have valid leases." |
| MetLife Building | 1 | Moderate | 2 | "...a more direct approach to hostile takeovers." |
| Woolworth Building | 2 | High | 2 | "Concern is not within my operational parameters." |
| One World Trade Center | 2 | Extreme | 4 | "Hazard compensation has never been collected." |

Each card shows: building name, tier badge, threat level indicator, floor count, SLOP one-liner.

### Section 3 — Elevator + co-op

Content split (reverse) with tower-elevator.png.

Text: Every tower uses the same elevator checkpoint framework. Clear floor milestones to unlock checkpoints. When a friend joins mid-contract, they spawn at floor 1 and ride the elevator up to your checkpoint — a brief solo gauntlet that orients them before arrival. No special rejoin logic. The elevator is the mechanic.

SLOP commentary: "SLOP detects incoming personnel in the elevator shaft. Their life signs are stable. SLOP will continue monitoring from a safe distance. A very safe distance."

### Section 4 — Three-pillar loop

Content split with tower-boss-rooftop.png.

Text: The tower is the unlock gate. Alternate factory recipes, building blueprints, weapon upgrade components, and narrative fragments — all from tower loot. You can't build a better factory without it. You can't survive harder waves without factory gear. The three pillars form a closed loop.

Visual: ASCII or styled diagram of the loop:
```
Tower contracts → loot → Factory upgrades → better gear → Harder tower tiers
                                           → wave defense → territory expansion
```

## Files to create/modify

### New files
- `docs/tower.html` — page
- `docs/js/scene-skyline.js` — Three.js skyline scene
- `docs/js/scene-elevator.js` — Three.js elevator scene
- `docs/assets/img/tower-skyline.png` — generated art
- `docs/assets/img/tower-contract-board.png` — generated art
- `docs/assets/img/tower-elevator.png` — generated art
- `docs/assets/img/tower-boss-rooftop.png` — generated art

### Modified files
- `docs/js/main.js` — add Tower to NAV_PAGES
- `docs/css/style.css` — add 5th-card span rule, any tower-specific styles
- `docs/index.html` — add Tower card to card grid
