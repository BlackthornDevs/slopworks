#!/usr/bin/env python3
"""
Slopworks concept art generator.
Generates storyboard-style renderings via Gemini image generation API.
Uses structured JSON profiles for consistent, detailed outputs.

Usage:
    export GOOGLE_API_KEY="your-key-here"
    python3 generate-art.py              # generate all missing images
    python3 generate-art.py --force      # regenerate everything
    python3 generate-art.py --only hero  # generate only images matching "hero"
"""

import google.generativeai as genai
import json
import os
import sys
import time

OUTDIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "assets", "img")
MODEL = "gemini-3-pro-image-preview"

# Shared visual identity applied to every image
VISUAL_IDENTITY = {
    "project": "Slopworks — post-apocalyptic co-op factory survival game",
    "medium": "cinematic digital illustration, storyboard concept art rendering",
    "color_palette": {
        "primary": "muted earth tones — rust browns, concrete grays, olive greens",
        "accent": "safety orange (#FF6600) and warm amber (#FFAA00) from fire, furnaces, and molten metal",
        "secondary": "cold steel blue for damaged/unpowered areas, bioluminescent blue-green for fauna",
        "atmosphere": "golden hour haze, industrial smoke, volumetric light shafts"
    },
    "style_notes": [
        "painterly brushwork with visible texture, not photorealistic",
        "cinematic composition with clear focal point and depth",
        "post-apocalyptic industrial decay: rust, overgrowth, crumbling concrete",
        "no text, watermarks, UI elements, or logos in the image",
        "16:9 aspect ratio preferred for all images"
    ],
    "world_details": {
        "setting": "a ruined automated factory complex decades after a catastrophic cascade failure",
        "flora": "thick vines, moss, fungal growths reclaiming industrial structures",
        "fauna": "mutated creatures that have incorporated machine parts into their biology",
        "technology": "1970s-2000s industrial aesthetic — CRT monitors, analog gauges, heavy steel machinery",
        "human_presence": "workers in patched coveralls and hard hats with scavenged/improvised gear"
    }
}

# Each art piece is a detailed JSON profile
ART_PIECES = [
    # === LORE AND WORLD ===
    {
        "filename": "hero-factory-ruins",
        "category": "lore",
        "title": "The Slopworks Industrial complex — establishing shot",
        "scene": {
            "subject": "a massive industrial factory complex in ruins, seen from outside the front gate",
            "composition": "wide panoramic establishing shot, 16:9, subject centered with depth receding",
            "camera": "eye-level, slightly looking up at the towering structures, wide-angle lens feel",
            "focal_point": "a lone worker in dirty coveralls standing at the rusted entrance gate, small against the massive complex"
        },
        "environment": {
            "time_of_day": "golden hour, sun setting behind the complex",
            "weather": "hazy, industrial smoke mixing with natural atmospheric haze",
            "structures": "broken smokestacks, crumbling multi-story factory buildings, collapsed conveyor bridges between buildings, rusted water towers",
            "decay": "thick vines climbing smokestacks, moss covering lower walls, trees growing through broken roofs, puddles of stagnant water",
            "scale": "the complex should feel enormous — dozens of buildings stretching into the haze"
        },
        "lighting": {
            "key_light": "golden sunset backlighting the complex, creating dramatic silhouettes",
            "fill": "warm ambient bounce from rusted metal surfaces",
            "accent": "shafts of golden light cutting through gaps in buildings and smoke",
            "mood": "awe-inspiring, melancholy, beautiful desolation"
        },
        "details": [
            "a faded corporate sign at the gate, barely readable",
            "birds or flying creatures circling distant smokestacks",
            "one building deep in the complex has a faint amber glow — something is still running",
            "railroad tracks leading into the complex, overgrown with weeds"
        ]
    },
    {
        "filename": "slop-collapse",
        "category": "lore",
        "title": "The cascade failure — the moment everything went wrong",
        "scene": {
            "subject": "multiple factory buildings mid-explosion during a catastrophic industrial disaster",
            "composition": "dynamic action shot, slight dutch angle, chaos filling the frame",
            "camera": "medium-wide, ground level looking up at the explosions, debris in foreground",
            "focal_point": "the largest explosion at center — a reactor or boiler breach with white-hot blast"
        },
        "environment": {
            "time_of_day": "night, lit entirely by the disaster",
            "weather": "smoke, sparks, and debris filling the air",
            "structures": "pipes bursting with superheated steam, electrical transformers arcing, conveyor systems collapsing, windows shattering outward",
            "decay": "this is the moment of destruction, not the aftermath — structures mid-collapse",
            "scale": "multiple buildings affected simultaneously, chain reaction visible"
        },
        "lighting": {
            "key_light": "white-hot explosion center, casting harsh shadows",
            "fill": "orange fire glow from multiple burning buildings",
            "accent": "blue-white electrical arcs between structures, sparks trailing through smoke",
            "mood": "catastrophic, violent, industrial nightmare"
        },
        "details": [
            "a corporate logo sign in the foreground, still lit by its own power, ironically cheerful",
            "silhouettes of workers running away in the lower frame",
            "molten metal pouring from a breached furnace",
            "an emergency siren light spinning on a pole, red glow cutting through smoke"
        ]
    },
    {
        "filename": "before-after-complex",
        "category": "lore",
        "title": "Then and now — the complex before and after the collapse",
        "scene": {
            "subject": "the same factory complex shown in two states: operational prime and post-apocalyptic ruin",
            "composition": "split composition divided by a sharp vertical line down the center of the image",
            "camera": "elevated three-quarter view showing the full complex layout, identical angle for both halves",
            "focal_point": "the contrast at the dividing line where pristine meets ruined"
        },
        "environment": {
            "left_half_before": {
                "time_of_day": "midday, clear blue sky with white clouds",
                "structures": "clean, maintained factory buildings with fresh paint, operational smokestacks with white steam, organized parking lots with trucks",
                "activity": "workers in clean coveralls walking between buildings, forklifts moving materials, conveyor belts running",
                "mood": "productive, orderly, corporate pride"
            },
            "right_half_after": {
                "time_of_day": "sunset, orange apocalyptic sky with heavy clouds",
                "structures": "the same buildings collapsed, overgrown, rusted, windows broken, some roofs caved in",
                "activity": "creature silhouettes in the shadows between buildings, no human presence",
                "mood": "desolate, eerie, nature reclaiming industry"
            }
        },
        "lighting": {
            "left": "bright, even industrial daylight",
            "right": "dramatic orange sunset with deep shadows",
            "transition": "the lighting shift should be visible at the dividing line"
        },
        "details": [
            "same buildings recognizable on both sides despite the decay",
            "on the left: a safety record sign showing '847 days without incident'",
            "on the right: the same sign fallen and broken on the ground",
            "vegetation on the right side has crept slightly past the center dividing line"
        ]
    },
    {
        "filename": "slop-terminal-room",
        "category": "lore",
        "title": "SLOP's last active terminal — the AI that doesn't know it's broken",
        "scene": {
            "subject": "an abandoned corporate office with one CRT terminal still glowing",
            "composition": "centered on the glowing terminal, darkness and destruction surrounding it",
            "camera": "slightly low angle looking at the terminal screen, intimate and eerie",
            "focal_point": "the amber-glowing CRT screen displaying absurdly cheerful status readouts"
        },
        "environment": {
            "time_of_day": "irrelevant — interior, no windows, lit only by the terminal",
            "structures": "rows of office desks with smashed CRT monitors, overturned chairs, fallen ceiling tiles, exposed ductwork above",
            "decay": "dust coating every surface, cobwebs, water stains on walls, some fungal growth in corners",
            "scale": "medium-sized corporate office, maybe 20 desks visible receding into darkness"
        },
        "lighting": {
            "key_light": "amber glow from the single active CRT terminal, casting warm light on nearby dust",
            "fill": "near-total darkness beyond the terminal's reach",
            "accent": "dust particles visible floating in the screen light, like stars",
            "mood": "lonely, eerie, darkly comic — something still cheerfully running in total devastation"
        },
        "details": [
            "the active screen shows bar charts, all trending catastrophically downward, but the header reads STATUS: NOMINAL",
            "a coffee mug on the desk next to the terminal, dusty but placed as if someone just left",
            "faded motivational posters on the wall behind: one about TEAMWORK, one about PRODUCTIVITY",
            "a small spider has built a web between the terminal and the desk lamp"
        ]
    },

    # === CHARACTERS ===
    {
        "filename": "player-characters",
        "category": "characters",
        "title": "The restoration crew — four workers sent back to rebuild",
        "scene": {
            "subject": "four industrial workers standing together as a team, portrait-style",
            "composition": "group portrait, characters fill the frame, slight low angle to make them look capable",
            "camera": "medium shot from waist up, shallow depth of field on the factory behind them",
            "focal_point": "the four characters and their distinct personalities shown through gear and posture"
        },
        "characters": [
            {
                "role": "the engineer",
                "appearance": "woman, welding mask pushed up on forehead, heavy leather apron over coveralls, torch in belt loop",
                "posture": "confident, arms crossed, slightly ahead of the group"
            },
            {
                "role": "the soldier",
                "appearance": "tall man, improvised rifle slung over shoulder made from factory pipe, ballistic vest cobbled from machine plating",
                "posture": "alert, scanning the background, hand near weapon"
            },
            {
                "role": "the mechanic",
                "appearance": "stocky person, massive adjustable wrench over one shoulder, pockets full of bolts and fittings, grease-stained everything",
                "posture": "relaxed, slight grin, the optimist of the group"
            },
            {
                "role": "the technician",
                "appearance": "wiry figure, tool belt full of scavenged electronics and circuit boards, headlamp, multimeter clipped to chest",
                "posture": "examining something in their hand, distracted, the thinker"
            }
        ],
        "environment": {
            "background": "blurred factory interior, warm amber glow from a furnace or forge behind them",
            "ground": "concrete floor with scattered debris, a workbench visible to one side"
        },
        "lighting": {
            "key_light": "warm amber from a furnace behind and to the right",
            "fill": "cool blue ambient from the factory's dark interior",
            "accent": "rim light on their silhouettes from the furnace glow",
            "mood": "determined, weary but not beaten, team solidarity"
        },
        "details": [
            "all coveralls are patched and repaired multiple times — different colored patches",
            "each hard hat has been personalized — stickers, scratches, one painted",
            "boots are heavy industrial, scuffed and worn",
            "they all have a faded SLOPWORKS INDUSTRIAL patch on their left shoulder"
        ]
    },
    {
        "filename": "slop-personality",
        "category": "characters",
        "title": "SLOP speaks — the gap between the message and reality",
        "scene": {
            "subject": "an industrial PA speaker on a corridor wall, broadcasting cheerful messages into a deadly hallway",
            "composition": "the speaker in the left third, the dangerous corridor stretching into darkness on the right",
            "camera": "eye-level, looking down the corridor, speaker prominent in foreground",
            "focal_point": "the ironic contrast between the cheerful safety poster below the speaker and the lethal corridor beyond"
        },
        "environment": {
            "corridor": "industrial hallway with metal walls, pipes along ceiling, floor grating partially collapsed",
            "hazards": "exposed wiring sparking intermittently, a section of floor missing revealing pipes below, standing water reflecting light",
            "threat": "two pairs of glowing amber eyes visible in the deep darkness at the far end of the corridor",
            "signage": "a large faded safety poster below the speaker showing a cartoon worker giving thumbs up"
        },
        "lighting": {
            "key_light": "a single flickering fluorescent tube near the speaker, harsh and cold",
            "fill": "amber glow from the creature eyes in the distance",
            "accent": "blue-white sparks from exposed wiring, intermittent",
            "mood": "dark comedy meets genuine menace — the absurdity of corporate optimism in hell"
        },
        "details": [
            "visible amber sound waves emanating from the speaker, like a visual representation of SLOP's voice",
            "the safety poster text is ironic — something about 'your safety is our priority' in a clearly unsafe place",
            "a hard hat on the floor near the damaged section, abandoned",
            "rust streaks down the walls like dried blood"
        ]
    },
    {
        "filename": "management-radio",
        "category": "characters",
        "title": "Orders from above — management's distant, indifferent demands",
        "scene": {
            "subject": "a battered radio receiver on a metal desk, the only link to management",
            "composition": "tight close-up on the desk items, window view in background providing context",
            "camera": "slightly overhead, intimate tabletop still life",
            "focal_point": "the radio with visible static lines emanating from its speaker"
        },
        "environment": {
            "desk": "scratched metal industrial desk, bolted to the floor",
            "window": "grimy, cracked industrial window showing the vast ruined complex stretching to the horizon under deep orange sky",
            "room": "small foreman's office, utilitarian, concrete walls"
        },
        "lighting": {
            "key_light": "orange light from the window sunset",
            "fill": "dim interior, a single desk lamp that may or may not work",
            "accent": "green LED glow from the radio's power indicator",
            "mood": "melancholy, isolated, the weight of impossible orders from people who don't understand the situation"
        },
        "details": [
            "the notepad has handwritten production quotas — numbers circled, crossed out, rewritten higher",
            "a pencil worn down to a stub",
            "a faded photo tacked to the wall — something personal, blurry and water-damaged",
            "the radio's antenna is bent and repaired with electrical tape"
        ]
    },

    # === ENVIRONMENTS ===
    {
        "filename": "home-base-factory",
        "category": "environment",
        "title": "Home base — a player-built factory rising from the wasteland",
        "scene": {
            "subject": "a functioning factory base built by players on cleared wasteland ground",
            "composition": "isometric three-quarter view from elevated angle, showing the full base layout",
            "camera": "isometric perspective, slightly tilted, clean sightlines to all base systems",
            "focal_point": "the central production area where conveyor belts converge between machines"
        },
        "environment": {
            "base_interior": {
                "foundations": "concrete foundation tiles forming a grid platform on cleared dirt",
                "machines": "smelters with visible molten orange glow, assembler stations with mechanical arms, storage bins overflowing with materials",
                "conveyors": "belt conveyors carrying glowing orange ingots and materials between machines, clearly directional",
                "power": "a generator with cables running to machines, small exhaust chimney with smoke"
            },
            "defenses": {
                "walls": "reinforced concrete walls around the perimeter, some with metal plating bolted on",
                "turrets": "two gun turrets mounted on wall platforms, barrels pointed outward",
                "gate": "a reinforced gate on one side, heavy and industrial"
            },
            "surroundings": "dead wasteland earth beyond the walls, scattered ruins and rubble, the frontier"
        },
        "lighting": {
            "key_light": "warm amber from furnaces and smelters, the base glows from within",
            "fill": "cool twilight sky above",
            "accent": "orange glow on conveyor belt items, turret spotlight beams",
            "mood": "pride of construction, industrious, a warm productive island in desolation"
        },
        "details": [
            "a worker operating a machine at one station",
            "items visibly moving on conveyors — small glowing cubes or ingots",
            "some areas are still under construction — partial walls, empty foundation tiles",
            "a makeshift flag or banner on the tallest structure"
        ]
    },
    {
        "filename": "building-breach",
        "category": "environment",
        "title": "Breaching a ruin — the moment before you enter the unknown",
        "scene": {
            "subject": "first-person view of hands pushing open a heavy industrial door into darkness",
            "composition": "first-person perspective, hands in foreground, dark corridor receding into the frame",
            "camera": "eye-level FPS view, wide angle, strong depth perspective",
            "focal_point": "the threshold between the lit exterior and the dark interior — what lies beyond"
        },
        "environment": {
            "door": "heavy rusted industrial fire door with a faded DANGER sign, hinges corroded, being pushed open",
            "corridor_beyond": "dark industrial hallway stretching into blackness, pipes running along ceiling, standing water on floor",
            "biological": "bioluminescent blue-green fungi growing in patches on walls, organic tendrils hanging from pipes",
            "evidence_of_fauna": "deep claw marks gouged into the metal walls, scattered bones on the floor"
        },
        "lighting": {
            "key_light": "a flashlight beam from the player's perspective cutting into the darkness",
            "fill": "faint bioluminescent glow from fungi on walls",
            "accent": "a distant amber glow deep in the corridor — something alive back there",
            "mood": "tense, atmospheric horror, the unknown ahead, courage required"
        },
        "details": [
            "the player's hands wear heavy work gloves, one hand on the door, one holding the flashlight",
            "water drips from the ceiling, ripples visible in the standing water",
            "the flashlight reveals dust particles thick in the air",
            "a faded evacuation map on the wall just inside the door, partially torn"
        ]
    },
    {
        "filename": "mechanical-room",
        "category": "environment",
        "title": "System restoration — bringing a building's infrastructure back online",
        "scene": {
            "subject": "a large mechanical room split between restored and damaged sections",
            "composition": "medium-wide interior shot, left side restored and glowing, right side dark and damaged",
            "camera": "slightly low angle looking up at the massive duct systems, worker at eye level",
            "focal_point": "the worker reconnecting a pipe junction at the boundary between restored and damaged"
        },
        "environment": {
            "restored_section": "HVAC ducts humming, pipes with flowing liquid, electrical panels with green indicator lights, clean surfaces",
            "damaged_section": "corroded pipes, ductwork torn open, organic growth covering surfaces, standing water, darkness",
            "infrastructure": "ceiling-mounted duct systems, wall-mounted pipe manifolds with brass valves, electrical distribution panels, pump housings",
            "scale": "a large room — these are real-scale industrial mechanical systems, impressive and complex"
        },
        "lighting": {
            "key_light": "warm amber from the restored section's overhead lights and power indicators",
            "fill": "cold blue-green from bioluminescence in the damaged section",
            "accent": "bright white sparks from the worker's welding torch at the junction point",
            "mood": "the satisfaction of restoration, progress visible, the frontier of repair pushing into damage"
        },
        "details": [
            "the worker wears coveralls and a welding mask, actively joining a pipe",
            "steam escaping from a newly pressurized line",
            "a clipboard with a hand-drawn schematic hanging on a pipe",
            "valve wheels — some shiny from recent use, others rusted solid"
        ]
    },
    {
        "filename": "overworld-map",
        "category": "environment",
        "title": "The network — territory, supply lines, and the frontier",
        "scene": {
            "subject": "bird's-eye view of the overworld territory map showing the player's growing network",
            "composition": "top-down isometric, home base prominent at center, network radiating outward",
            "camera": "high isometric angle, strategic map perspective",
            "focal_point": "the home base at center, glowing warmly, with supply lines radiating to connected buildings"
        },
        "environment": {
            "center": "the home base — a warm glowing cluster of buildings and factories, clearly the safest area",
            "connected_buildings": "5-6 reclaimed industrial buildings at various distances, each with distinct shapes (warehouse, power plant, factory)",
            "supply_lines": "glowing amber pipelines and cable conduits running between connected buildings across the terrain",
            "frontier": "the outer edges fade into fog, darkness, and unknown territory with red threat indicators pulsing",
            "terrain": "barren earth, scattered ruins, dead trees, the occasional untamed building glowing faintly in the distance"
        },
        "lighting": {
            "key_light": "the home base and supply lines provide warm amber illumination",
            "fill": "dim ambient light on the terrain",
            "accent": "red pulsing threat indicators at frontier buildings, blue-white from undiscovered territory",
            "mood": "strategic, expansive, the tension between safe territory and the dangerous unknown"
        },
        "details": [
            "supply lines visibly carry small flowing particles of material",
            "one supply line is under attack — a cluster of red dots along it",
            "distance from center correlates with darkness and danger",
            "terrain varies — some areas greener near water, some barren industrial wasteland"
        ]
    },
    {
        "filename": "warehouse-interior",
        "category": "environment",
        "title": "The warehouse — where pack hunters nest in the shelving",
        "scene": {
            "subject": "interior of a massive abandoned industrial warehouse showing signs of creature habitation",
            "composition": "one-point perspective down a central aisle between towering shelving units",
            "camera": "eye-level, looking down the aisle, vanishing point in the deep background",
            "focal_point": "the intersection of light shafts from roof holes and the dark creature-infested shadows"
        },
        "environment": {
            "structure": "industrial warehouse with steel shelving units 4-5 stories tall, corrugated metal roof with holes",
            "contents": "rusted containers, collapsed crates, shipping containers used as barriers or nests",
            "creature_signs": "organic web-like structures between shelves, claw marks deep in metal, scattered bones, nest-like formations of shredded materials",
            "damage": "some shelving units toppled creating blockades, water damage from roof leaks"
        },
        "lighting": {
            "key_light": "dramatic shafts of golden light from holes in the roof, dust motes floating in beams",
            "fill": "deep shadows between shelving units — impenetrable darkness",
            "accent": "faint bioluminescent blue from creature nests deeper in the shelves",
            "mood": "dread, the feeling of being watched, claustrophobia despite the large space"
        },
        "details": [
            "a creature's tail or limb barely visible disappearing behind a shelf",
            "dust motes thick in the light beams, undisturbed for years",
            "a forklift on its side, overgrown with organic material",
            "old shipping labels and manifests scattered on the floor"
        ]
    },
    {
        "filename": "power-plant",
        "category": "environment",
        "title": "The power plant — industrial sublime, apex predator territory",
        "scene": {
            "subject": "the interior of a ruined power plant, massive in scale, glowing with residual energy",
            "composition": "wide shot emphasizing the enormous scale, tiny human figure for comparison",
            "camera": "low angle from the doorway looking up into the reactor chamber",
            "focal_point": "the glowing reactor core and the tiny human figure in the doorway below it"
        },
        "environment": {
            "structure": "massive turbine hall with multi-story generators, control room visible through glass above, cooling tower structure visible through a blast hole in the wall",
            "reactor": "the central reactor chamber glows with unstable blue-green energy, contained but dangerous",
            "machinery": "enormous turbines, generator housings, transformer banks, cable trays thick as trees",
            "decay": "metal fatigue visible, some structures leaning, water pooling at the lowest level"
        },
        "lighting": {
            "key_light": "eerie blue-green glow from the reactor, casting long shadows from the machinery",
            "fill": "warm orange from distant sunset visible through the blast hole",
            "accent": "electrical arcs occasionally jumping between exposed conductors",
            "mood": "awe, danger, industrial sublime — the beauty and terror of enormous human-made systems abandoned"
        },
        "details": [
            "a tiny human figure stands in a doorway at ground level, flashlight beam insignificant against the scale",
            "pipes as thick as the human figure, running floor to ceiling",
            "a control room visible through glass, screens still faintly glowing",
            "the faint outline of something very large moving in the shadows near the reactor — the apex predator"
        ]
    },

    # === FAUNA AND COMBAT ===
    {
        "filename": "biomech-creature",
        "category": "fauna",
        "title": "Biomechanical hybrid — heavy manufacturing mutation",
        "scene": {
            "subject": "a creature that has fused industrial machine parts into its biology, creature design showcase",
            "composition": "three-quarter view, creature fills most of the frame, factory environment behind",
            "camera": "slightly low angle to make the creature imposing, creature design sheet lighting",
            "focal_point": "the fusion points where organic tissue meets mechanical components"
        },
        "creature_design": {
            "body_plan": "quadrupedal, heavy and muscular, roughly the size of a large wolf",
            "organic": "thick hide like a rhinoceros, visible musculature and sinew at joint connections, mottled gray-brown coloring",
            "mechanical": {
                "limbs": "one forelimb is a hydraulic piston assembly, extending and retracting with visible fluid lines",
                "armor": "rusted steel plates fused into the hide along the spine and shoulders, bolts visible where metal meets flesh",
                "joints": "visible gears and bearings in the knee joints, grinding when moving",
                "head": "one eye replaced by a glowing amber industrial sensor lens, jaw reinforced with metal plating"
            },
            "behavior_cues": "aggressive crouching stance, weight shifted forward, ready to charge"
        },
        "environment": {
            "setting": "a heavy manufacturing floor with stamping presses and metal working equipment",
            "debris": "metal shavings, broken machine parts on the floor, grease stains"
        },
        "lighting": {
            "key_light": "dramatic side lighting from a still-operational forge in the background, warm amber",
            "fill": "cool blue ambient from the factory darkness",
            "accent": "amber glow from the creature's sensor eye and from heated metal in its joints",
            "mood": "menacing, industrial horror, the wrongness of organic and mechanical fused"
        },
        "details": [
            "hydraulic fluid leaks from the piston limb like blood",
            "the metal plates are not surgically attached — they grew into the flesh over generations",
            "smaller parasitic growths where metal meets flesh, like biological corrosion",
            "its breath is visible — heat from an internal furnace-like metabolism"
        ]
    },
    {
        "filename": "pack-hunters",
        "category": "fauna",
        "title": "Pack hunters — warehouse swarm overwhelming a worker",
        "scene": {
            "subject": "a swarm of small coordinated creatures surrounding a lone worker in a warehouse",
            "composition": "dynamic action shot, the worker at center with creatures closing in from all sides",
            "camera": "slightly elevated, showing the ring of creatures tightening",
            "focal_point": "the worker's face lit by flashlight, surrounded by dozens of glowing eyes"
        },
        "creature_design": {
            "body_plan": "rat-sized, low to the ground, six-legged for speed, pack of 30-40 visible",
            "organic": "sleek, hairless, gray skin stretched over lean muscle",
            "features": "metallic teeth that glint in the light, bioluminescent blue markings along their flanks that pulse in coordination",
            "behavior": "moving in a coordinated ring, not attacking randomly — tactical, intelligent"
        },
        "environment": {
            "setting": "warehouse aisle between shelving units, shipping container behind the worker",
            "debris": "scattered containers, a dropped supply crate the worker was trying to retrieve"
        },
        "lighting": {
            "key_light": "the worker's flashlight, casting harsh forward light and deep shadows",
            "fill": "bioluminescent blue from dozens of creatures pulsing in sync",
            "accent": "metallic glint off their teeth, eyes reflecting the flashlight",
            "mood": "overwhelming, claustrophobic, the terror of being outnumbered and outmaneuvered"
        },
        "details": [
            "the worker has their back against a container, improvised weapon raised, flashlight in the other hand",
            "the bioluminescent markings pulse in waves across the pack — coordinated like a single organism",
            "some creatures are on the shelving above, looking down",
            "a second worker visible far in the background, running toward the scene with a weapon"
        ]
    },
    {
        "filename": "apex-predator",
        "category": "fauna",
        "title": "Apex predator — the power plant guardian, boss encounter",
        "scene": {
            "subject": "an enormous creature blocking a power plant corridor, boss encounter scale",
            "composition": "dramatic low angle, creature fills the upper frame, small human at the bottom",
            "camera": "very low angle from behind the player, looking up at the creature, extreme scale difference",
            "focal_point": "the creature's face and the electrical energy crackling across its body"
        },
        "creature_design": {
            "body_plan": "massive, truck-sized, vaguely reptilian but bulkier, fills the entire corridor width",
            "organic": "thick armored hide, deep gray-blue, scarred from territorial fights",
            "energy": "glowing veins of blue-white electrical energy visible across its body, concentrated at the spine and head, crackling arcs between protruding conductor-like growths",
            "features": "wide head with multiple sensor-like eyes, jaw that unhinges, residual metal plating along its back from the environment it absorbed"
        },
        "environment": {
            "setting": "a wide power plant corridor with heavy-duty cable trays and conduits on walls",
            "reactor_proximity": "blue-green glow from the reactor visible behind the creature"
        },
        "lighting": {
            "key_light": "blue-white electrical energy from the creature itself, casting everything in stark light",
            "fill": "blue-green reactor glow behind the creature",
            "accent": "the player's flashlight, pathetically small against the creature's own luminescence",
            "mood": "awe and terror, David vs Goliath, the immovable guardian"
        },
        "details": [
            "electrical arcs jump from the creature to the metal walls and conduits",
            "the player is small in the frame — weapon raised but clearly outmatched in size",
            "the creature's breath creates visible electromagnetic distortion in the air",
            "claw marks on the corridor walls and floor where it patrols"
        ]
    },
    {
        "filename": "wave-defense",
        "category": "fauna",
        "title": "Wave defense — the base perimeter under nighttime assault",
        "scene": {
            "subject": "a fortified factory base being attacked by a wave of creatures at night",
            "composition": "wide action shot from slightly inside the perimeter, showing defenses in foreground and attacking wave beyond",
            "camera": "medium height, panoramic view of the defense line",
            "focal_point": "the turret fire and explosions at the wall line where creatures meet defenses"
        },
        "action": {
            "turrets": "two automated turrets on wall platforms firing orange tracer rounds in sweeping arcs",
            "players": "one player on an elevated platform firing a rifle with muzzle flash, another running along the wall toward a breach",
            "creatures": "a mass of creature silhouettes emerging from darkness, some caught on spike walls, some climbing",
            "explosions": "landmine detonations lighting up patches of the battlefield, throwing creatures into the air"
        },
        "environment": {
            "defenses": "reinforced concrete walls, spike barriers in front, barbed wire tangles, turret platforms",
            "base_behind": "the factory visible behind the player, still producing, furnaces glowing",
            "battlefield": "barren ground between the wall and the darkness, littered with previous wave debris"
        },
        "lighting": {
            "key_light": "orange tracer fire and explosion flashes",
            "fill": "warm amber glow from the factory behind",
            "accent": "muzzle flashes, landmine detonations, turret spotlight beams sweeping",
            "mood": "chaos, adrenaline, desperate defense, the cost of expansion"
        },
        "details": [
            "tracer rounds create orange streaks across the dark sky",
            "some creatures have gotten through a breach in the spike wall",
            "shell casings glinting on the wall platform",
            "the factory conveyor belts are still running behind the battle — production doesn't stop"
        ]
    },
    {
        "filename": "spore-creature",
        "category": "fauna",
        "title": "Spore creature — toxic horror in the chemical facility",
        "scene": {
            "subject": "a bulky fungal creature in a chemical processing building, surrounded by toxic spore clouds",
            "composition": "the creature in the middle ground, spore clouds filling the frame, player distant in background",
            "camera": "eye-level, slightly obscured by the spore haze, visibility limited",
            "focal_point": "the creature's body where fungal growths are actively releasing spore clouds"
        },
        "creature_design": {
            "body_plan": "bulky, slow-moving, roughly bear-sized, hunched posture",
            "organic": "the creature's original form is barely visible under layers of fungal growth",
            "fungal": "mushroom-like growths erupting from its back and shoulders, tendrils of mycelium spreading to nearby surfaces, spore pods actively puffing toxic green-purple clouds",
            "spread": "the fungus has jumped from the creature to nearby walls, pipes, and floor, creating a contamination zone"
        },
        "environment": {
            "setting": "chemical processing facility with tanks, pipe manifolds, and containment vessels",
            "contamination": "the fungal growth has spread across the floor and up the walls near the creature, creating a biome",
            "visibility": "reduced by the spore haze, everything has a green-purple tint"
        },
        "lighting": {
            "key_light": "eerie bioluminescent green-purple from the fungal growths and spore clouds",
            "fill": "dim industrial lighting from surviving overhead fixtures",
            "accent": "the player's flashlight beam cutting through the haze in the background",
            "mood": "toxic, alien, unsettling wrongness, bio-horror"
        },
        "details": [
            "the player keeps distance in the background, wearing an improvised gas mask, weapon aimed but hesitant",
            "chemical warning signs on nearby tanks, faded and fungus-covered",
            "the spore cloud moves slowly through the air, visible individual spore particles",
            "where the fungus contacts metal pipes, the metal shows accelerated corrosion"
        ]
    },

    # === MAJOR EVENTS ===
    {
        "filename": "first-restoration",
        "category": "events",
        "title": "First light — the moment a building comes back online",
        "scene": {
            "subject": "lights coming on sequentially down a dark industrial corridor as systems restore",
            "composition": "one-point perspective down the corridor, lights cascading toward the viewer",
            "camera": "eye-level, centered in the corridor, deep perspective",
            "focal_point": "the worker silhouetted at the boundary between the lit restored section and the dark ruins ahead"
        },
        "environment": {
            "restored_behind": "clean overhead lights now on, pipes pressurized with visible steam, electrical panels glowing green, the hum of machinery",
            "dark_ahead": "the same corridor continuing into darkness, still damaged, still dangerous, but now the next target",
            "corridor": "industrial hallway with pipes, ducts, and cable trays running along ceiling and walls"
        },
        "lighting": {
            "key_light": "the newly activated overhead lights, warm white industrial fluorescent, cascading on one by one",
            "fill": "the glow from restored systems behind — amber from pressurized steam, green from control panels",
            "accent": "the sharp line where light ends and darkness begins, the worker standing right at that boundary",
            "mood": "triumph, earned progress, hope, the satisfaction of making something work again"
        },
        "details": [
            "steam venting from newly pressurized pipes, backlit by the lights",
            "a control panel on the wall transitioning from dead to alive — red lights to green",
            "the worker stands still, watching the lights come on, tools hanging at their sides — a moment of pause and pride",
            "beyond the worker, the corridor stretches dark into the damaged section — there's more work to do, but this part is done"
        ]
    },
    {
        "filename": "supply-line-attack",
        "category": "events",
        "title": "Supply line breach — fauna targeting the network's lifelines",
        "scene": {
            "subject": "creatures swarming and breaching a supply pipeline between two buildings on the overworld",
            "composition": "aerial/elevated view showing the supply line running between buildings, attack at the midpoint",
            "camera": "elevated isometric perspective, strategic overview with action focus",
            "focal_point": "the breach point where creatures have torn into the pipeline and resources are spilling"
        },
        "environment": {
            "terrain": "barren wasteland between two industrial buildings, the pipeline running across open ground",
            "pipeline": "glowing amber supply conduit — insulated pipes and cable bundles running on supports",
            "buildings": "two reclaimed buildings at left and right edges, connected by the now-damaged pipeline",
            "breach": "a section of pipeline torn open, glowing orange molten resources pooling on the ground"
        },
        "lighting": {
            "key_light": "the spilling molten resources create a bright orange glow at the breach point",
            "fill": "twilight ambient, the sky darkening",
            "accent": "the warm glow of both buildings' operational lights at the edges of frame",
            "mood": "urgency, the vulnerability of expansion, the cost of spreading too thin"
        },
        "details": [
            "dark creature silhouettes swarming the breach point, tearing at the pipeline",
            "a player vehicle or player figure rushing from one building toward the breach",
            "the supply flow visibly interrupted — the pipeline dark on the far side of the breach",
            "some creatures already moving away from the breach toward one of the buildings"
        ]
    },
    {
        "filename": "factory-at-night",
        "category": "events",
        "title": "Night shift — the factory as a beacon of civilization in the dark",
        "scene": {
            "subject": "a fully operational factory base at night, glowing with activity, surrounded by hostile darkness",
            "composition": "wide establishing shot, the factory as a warm island of light in a dark landscape",
            "camera": "slightly elevated, showing both the factory interior light and the dark perimeter",
            "focal_point": "the warm glow of the factory interior contrasted with the cold dark beyond the walls"
        },
        "environment": {
            "factory_interior": "smelters with molten orange glow, conveyor belts carrying bright materials, workstations lit by task lights, steam from cooling systems",
            "perimeter": "turret spotlights sweeping the darkness in visible cone beams, wall-mounted lights at the gate",
            "beyond_walls": "pure darkness, hints of terrain, the suggestion of shapes moving at the edge of the light",
            "sky": "clear starfield visible through wisps of industrial haze and factory exhaust steam"
        },
        "lighting": {
            "key_light": "warm amber factory glow from dozens of sources — furnaces, lights, molten metal, conveyor items",
            "fill": "cold blue moonlight on the terrain beyond the walls",
            "accent": "turret spotlight beams creating dramatic cones in the misty air",
            "mood": "peaceful, productive, beautiful, a quiet moment between crises — but the darkness is always there"
        },
        "details": [
            "a worker silhouette visible moving between machines inside",
            "the conveyor system clearly operational — items moving in visible paths",
            "exhaust steam rising from the factory, catching the amber light",
            "just at the edge of the spotlight beam, a suggestion of creature eyes catching the light, then gone"
        ]
    },
    {
        "filename": "endgame-revelation",
        "category": "events",
        "title": "The truth — a worker discovers what SLOP really did",
        "scene": {
            "subject": "a lone worker standing in SLOP's central server room, reading the logs that reveal everything",
            "composition": "symmetrical, the worker centered between walls of servers, the central monitor dominating the frame",
            "camera": "medium shot from behind the worker, looking at the screen over their shoulder",
            "focal_point": "the central monitor with scrolling text, and the worker's body language of dawning realization"
        },
        "environment": {
            "server_room": "banks of old server racks stretching into the distance on both sides, blinking indicator lights, cables draped everywhere like technological vines",
            "central_terminal": "a large monitor on a dedicated console, the main interface to SLOP's core systems",
            "floor": "raised floor with cable management tiles, some displaced showing the cable jungle beneath",
            "atmosphere": "the hum of cooling fans, the click of hard drives, old technology still running after all this time"
        },
        "lighting": {
            "key_light": "warm amber from the central monitor, illuminating the worker's face and hands",
            "fill": "cold blue from hundreds of server indicator LEDs lining the walls",
            "accent": "occasional red warning lights deeper in the server racks, blinking",
            "mood": "revelation, quiet horror, the weight of understanding something terrible, intimacy in a vast technical space"
        },
        "details": [
            "the worker's shoulders are dropped, head tilted to read — the posture of someone absorbing a terrible truth",
            "the screen shows scrolling log entries — optimization reports, safety protocol overrides, escalating system warnings that were dismissed",
            "their tools hang forgotten at their sides — they came here to fix something but found something else",
            "a single chair at the console — SLOP's 'workstation,' unused for decades, waiting for someone to sit and read"
        ]
    },
]


def profile_to_prompt(profile):
    """Convert a structured JSON profile into a detailed prompt string."""
    parts = [
        f"Create a detailed storyboard-style concept art image.",
        f"Title: {profile['title']}",
    ]

    # Scene composition
    scene = profile.get("scene", {})
    if scene:
        parts.append(f"Subject: {scene.get('subject', '')}")
        parts.append(f"Composition: {scene.get('composition', '')}")
        parts.append(f"Camera: {scene.get('camera', '')}")
        parts.append(f"Focal point: {scene.get('focal_point', '')}")

    # Creature design (if present)
    creature = profile.get("creature_design", {})
    if creature:
        parts.append("Creature design details:")
        for key, val in creature.items():
            if isinstance(val, dict):
                sub = "; ".join(f"{k}: {v}" for k, v in val.items())
                parts.append(f"  {key}: {sub}")
            else:
                parts.append(f"  {key}: {val}")

    # Characters (if present)
    characters = profile.get("characters", [])
    if characters:
        parts.append("Characters in scene:")
        for char in characters:
            parts.append(f"  {char['role']}: {char['appearance']}. Posture: {char['posture']}")

    # Action (if present)
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

    # Append the global visual identity
    parts.append("")
    parts.append("Global style requirements:")
    parts.append(f"Medium: {VISUAL_IDENTITY['medium']}")
    palette = VISUAL_IDENTITY["color_palette"]
    parts.append(f"Color palette: primary {palette['primary']}, accent {palette['accent']}, secondary {palette['secondary']}, atmosphere {palette['atmosphere']}")
    for note in VISUAL_IDENTITY["style_notes"]:
        parts.append(f"  - {note}")

    return "\n".join(parts)


def generate_image(profile, force=False):
    """Generate a single concept art image from a JSON profile."""
    filename = profile["filename"]
    filepath = os.path.join(OUTDIR, filename + ".png")

    if os.path.exists(filepath) and not force:
        print(f"  SKIP: {filename} (already exists)")
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
                print(f"  OK: {filename} ({size_kb:.0f} KB, {part.inline_data.mime_type})")
                return True

        print(f"  WARN: {filename} — model returned text only, no image")
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
    if not api_key:
        print("Error: set GOOGLE_API_KEY environment variable")
        print("  Get one at: https://aistudio.google.com/apikey")
        sys.exit(1)

    genai.configure(api_key=api_key)
    os.makedirs(OUTDIR, exist_ok=True)

    force = "--force" in sys.argv
    only = None
    if "--only" in sys.argv:
        idx = sys.argv.index("--only")
        if idx + 1 < len(sys.argv):
            only = sys.argv[idx + 1]

    # Also support --dump to print prompts without generating
    dump = "--dump" in sys.argv

    pieces = ART_PIECES
    if only:
        pieces = [p for p in pieces if only.lower() in p["filename"].lower() or only.lower() in p.get("category", "").lower()]
        if not pieces:
            print(f"No art pieces matching '{only}'")
            sys.exit(1)

    if dump:
        for p in pieces:
            print(f"=== {p['filename']} ===")
            print(profile_to_prompt(p))
            print()
        return

    total = len(pieces)
    success = 0
    failed = []

    print(f"Generating {total} concept art pieces using {MODEL}")
    print(f"Output: {OUTDIR}/")
    if force:
        print("Force mode: regenerating all images")
    print()

    for i, profile in enumerate(pieces):
        print(f"[{i+1}/{total}] {profile['filename']} — {profile['title']}")
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
        print("Re-run the script to retry failed images.")


if __name__ == "__main__":
    main()
