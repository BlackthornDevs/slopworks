# NPCs

Non-player characters the player encounters during exploration and base management. The NPC roster is intentionally small — the world is mostly empty, and human contact is rare enough to feel significant. Most communication comes through SLOP or radio transmissions. There is no existing ScriptableObject for NPCs yet; this bible file defines the canonical schema for future implementation.

## Schema

```yaml
# No existing SO — this schema defines the future NPC data structure
npcId: string                 # snake_case unique identifier
displayName: string           # human-readable name
description: string           # in-game codex text
npcType: enum [trader, quest_giver, radio_contact, ambient]
location: string              # where this NPC is found or heard
faction: string               # group affiliation
inventory: list[string] | null  # itemId references for traders, null for non-traders
questsOffered: list[string]   # quest or objective IDs this NPC can give
dialogueTreeId: string | null # reference to dialogue system, null if no branching dialogue
tags: list[string]
modelStyle: string            # art direction note
voiceStyle: string            # voice acting direction
```

## Entries

```yaml
- npcId: management_radio
  displayName: Regional management
  description: A crackling radio signal from somewhere beyond the complex perimeter, claiming to be Slopworks Industrial's regional management office. They demand production reports, issue quotas with deadlines, and express polite disappointment when targets aren't met. Whether anyone is actually on the other end of the signal — or whether this is just another layer of SLOP's automation — is left ambiguous until late game.
  npcType: radio_contact
  location: Radio transmissions received at the home base communications array. Signal strength improves as the player restores buildings with Electrical MEP systems. Initially only heard during scripted events, later available on demand via the restored radio.
  faction: slopworks_corporate
  inventory: null
  questsOffered:
    - quest_restore_power_plant
    - quest_monthly_quota
    - quest_tower_assessment
  dialogueTreeId: dialogue_management_radio
  tags:
    - radio
    - corporate
    - ambiguous_identity
    - narrative_critical
    - quota_giver
  modelStyle: No visual model — audio only. Radio static overlay on all transmissions. Occasional signal degradation mid-sentence that conveniently cuts out before revealing too much.
  voiceStyle: Mid-Atlantic corporate accent, gender-neutral. Sounds like a 1950s training film narrator who has been reading quarterly reports for eternity. Never raises voice. Disappointment is expressed through increasingly precise diction, not volume. Pauses are longer than comfortable.

- npcId: scavenger_trader
  displayName: Mack
  description: A scarred, practical survivor who operates a mobile trading post from a converted cargo truck. Mack roams between buildings on a circuit that takes several in-game days to complete. They trade rare materials, blueprints, and information for whatever the player can offer. Mack knows things about the complex that predate the collapse and will share fragments in exchange for high-value items.
  npcType: trader
  location: Roaming. Appears at reclaimed buildings on a rotation cycle. First encounter is scripted at the warehouse after the player's first successful building reclamation. Can be radioed to request a visit once the communications array is restored.
  faction: independent
  inventory:
    - reinforced_plating
    - signal_decoder
    - power_cell
    - chemicals
    - quartz
    - copper_ingot
    - steel_ingot
  questsOffered:
    - quest_supply_run
    - quest_rare_materials
  dialogueTreeId: dialogue_mack
  tags:
    - trader
    - roaming
    - lore_source
    - rare_materials
    - independent
  modelStyle: Weathered human figure in patched-together industrial coveralls. Heavy tool belt with visible wear. Facial scars from chemical burns on the left side. Cargo truck behind them is armored with welded scrap plates and has a hand-painted sign reading "MACK'S — fair trades, no questions, no refunds."
  voiceStyle: Gravelly, tired but not defeated. Speaks in short declarative sentences. Laughs rarely but genuinely. Drops lore casually — "that building used to be the cafeteria before the thing with the pipes" — without elaboration unless prompted. New Jersey accent.

- npcId: fellow_worker
  displayName: Dana
  description: Another former Slopworks employee who was "voluntarily reassigned" to restoration duty, same as the player. Dana arrived weeks earlier and has been surviving alone in a fortified corner of the warehouse complex. They serve as the player's introduction to the world's lore, explaining what they've figured out about the fauna, the buildings, and SLOP's behavior patterns. Dana is not a combatant and stays at their shelter.
  npcType: ambient
  location: Warehouse complex, fortified storage bay on the second floor. Present after the warehouse is first entered, before full reclamation. Remains at this location permanently.
  faction: slopworks_employee
  inventory: null
  questsOffered:
    - quest_dana_cache
    - quest_dana_radio_parts
  dialogueTreeId: dialogue_dana
  tags:
    - ambient
    - lore_source
    - tutorial_adjacent
    - fellow_employee
    - warehouse
  modelStyle: Average build, Slopworks-branded jumpsuit that has been repaired many times. DIY armor pieces made from warehouse shelving brackets. Nervous energy — always fidgeting with something. Workspace behind them is organized with obsessive precision, covered in hand-drawn maps and notes pinned to a corkboard.
  voiceStyle: Talks fast when nervous, which is always. Alternates between gallows humor and genuine concern for the player. Uses Slopworks corporate jargon ironically — "per the employee handbook" when suggesting something dangerous. Occasionally quotes SLOP verbatim in a mocking tone. East coast accent, mid-twenties.

- npcId: slop_terminal
  displayName: SLOP terminal
  description: Interactive access points scattered throughout the complex where players can query SLOP directly. Each terminal provides building-specific information, production status, and SLOP's commentary on current events. Terminals in different buildings give different information based on what SLOP's local sensors can detect. Technically an NPC because the player interacts with it through a dialogue-style interface, though it's a wall-mounted screen showing SLOP's interface.
  npcType: ambient
  location: One per reclaimed building, plus a primary terminal at the home base. Building terminals activate when the building's Electrical MEP system is restored. The home base terminal is always active.
  faction: slop
  inventory: null
  questsOffered:
    - quest_slop_diagnostic
    - quest_production_optimization
  dialogueTreeId: dialogue_slop_terminal
  tags:
    - terminal
    - interactive
    - slop_voice
    - information
    - every_building
    - narrative_critical
  modelStyle: Wall-mounted industrial monitor with a cracked screen and exposed cable connections. SLOP's interface displays as a retro green-on-black terminal with a blinking cursor. The screen occasionally glitches, showing fragments of pre-collapse data. A small speaker grille below the screen crackles with SLOP's voice. Warning stickers on the frame are faded but legible.
  voiceStyle: SLOP's standard voice — processed, synthetic, relentlessly upbeat corporate tone with occasional digital artifacts. Pitch shifts slightly when SLOP is being evasive. Static increases when SLOP encounters topics related to the collapse. Terminal-specific responses are more technical and less conversational than SLOP's ambient commentary.
```
