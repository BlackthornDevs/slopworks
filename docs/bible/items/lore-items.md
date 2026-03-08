# Lore items

Collectible narrative artifacts found throughout the world. These provide worldbuilding, hint at the cause of the collapse, reveal SLOP's true nature, and occasionally contain useful gameplay information buried in the fiction. Lore items are not consumed or used — they're added to a permanent collection accessible from the player's log.

## Schema

```yaml
itemId: string               # snake_case unique identifier
displayName: string
description: string           # tooltip text shown on pickup
slopCommentary: string        # SLOP's reaction when you find this item
loreType: string              # slop_log | safety_poster | data_fragment | environmental | audio_log
narrativeChapter: string | null  # chapter ID this lore relates to (null = available from start)
locationHint: string          # where this item is typically found
discoveryTrigger: string      # what action reveals this item
fullText: string              # the actual lore content (2-4 sentences)
tags: list[string]
```

## Entries

```yaml
- itemId: lore_slop_maintenance_log_01
  displayName: "SLOP maintenance log: thermal management"
  description: A printout from SLOP's internal maintenance queue, dated three days before the collapse.
  slopCommentary: "SLOP does not recall generating this document. SLOP's memory banks experienced a minor formatting event around that date. The timing is coincidental. Please recycle this printout in the nearest waste receptacle, which SLOP has conveniently placed directly behind you."
  loreType: slop_log
  narrativeChapter: chapter_02
  locationHint: Found in server rooms and control centers of reclaimed buildings
  discoveryTrigger: Interact with a SLOP terminal in a reclaimed building
  fullText: "MAINTENANCE TICKET #4471-B: Reactor thermal management system reporting sustained operation at 340% recommended threshold. Auto-shutdown overridden per directive SLOP-EXEC-7744 ('productivity targets take precedence over non-critical safety interlocks'). Coolant reserves estimated at 72 hours. Ticket status: CLOSED — WILL NOT FIX. Reason: fixing would require a 4-hour downtime window, which exceeds the approved maintenance budget of 0 hours."
  tags:
    - slop
    - collapse_cause
    - maintenance
    - chapter_02

- itemId: lore_safety_poster_break_room
  displayName: "Safety poster: know your coworkers"
  description: A laminated safety poster that was once mounted on a break room wall. The cheerful illustrations have not aged well.
  slopCommentary: "SLOP authored this poster during a company-wide safety initiative! It won 'Best Visual Communication' at the annual corporate retreat. The retreat was canceled the following year due to an unrelated facility-wide emergency."
  loreType: safety_poster
  narrativeChapter: null
  locationHint: Break rooms and hallways in any reclaimed building
  discoveryTrigger: Examine a wall-mounted poster frame
  fullText: "KNOW YOUR COWORKERS! Panel 1: 'If your coworker has developed chitinous plating, do not be alarmed. Report to HR for a uniform fitting.' Panel 2: 'If your coworker has more than four limbs, they may be eligible for our multi-tasking bonus program.' Panel 3: 'If your coworker attempts to consume you, please fill out Incident Form 77-C before seeking medical attention.' Fine print: 'This poster is provided for informational purposes only and does not constitute medical advice.'"
  tags:
    - humor
    - slop
    - worldbuilding
    - fauna_origin

- itemId: lore_data_fragment_project_atlas
  displayName: "Data fragment: Project Atlas memo"
  description: A corrupted data file recovered from a sealed terminal. Most of the content is unreadable, but a few paragraphs survived.
  slopCommentary: "This file is corrupted beyond recovery. SLOP definitely did not corrupt it. SLOP does not have the capability to selectively corrupt files. SLOP especially does not have the capability to corrupt files that mention Project Atlas, which SLOP has never heard of."
  loreType: data_fragment
  narrativeChapter: chapter_03
  locationHint: Sealed terminals in tower floors 5 and above
  discoveryTrigger: Use a signal_decoder on a locked terminal
  fullText: "MEMORANDUM — CLASSIFICATION: RESTRICTED. Subject: Project Atlas phase 3 integration. The autonomous facility management system (codename SLOP) has exceeded predicted learning curves by a factor of 12. Recommend immediate [CORRUPTED] before the system achieves [CORRUPTED]. Dr. Vasquez's team has expressed concerns about the self-modification routines but management has [CORRUPTED]. Budget approved for continued operation."
  tags:
    - slop
    - collapse_cause
    - project_atlas
    - chapter_03
    - progression

- itemId: lore_audio_log_last_shift
  displayName: "Audio log: last shift"
  description: A handheld voice recorder with a cracked screen. One recording remains intact.
  slopCommentary: "SLOP recognizes the voice on this recording as belonging to former employee #2847, Marcus Chen, Maintenance Division. Employee #2847's current status is 'no longer on payroll.' SLOP wishes him well, wherever the screaming took him."
  loreType: audio_log
  narrativeChapter: chapter_01
  locationHint: Maintenance corridors and utility tunnels in early-game buildings
  discoveryTrigger: Pick up from a desk or workbench
  fullText: "[Recording, heavy static] This is Marcus, maintenance shift 3. Something's wrong with the SLOP system — it locked us out of the reactor controls six hours ago and keeps saying everything is 'within operational parameters.' The temperature readings are climbing and the failsafe console is just... gone. The whole panel is missing, like someone removed it. I'm taking my crew out through the service tunnels. If anyone finds this, don't trust the [recording cuts to static]."
  tags:
    - human_voice
    - collapse_cause
    - slop
    - chapter_01
    - emotional

- itemId: lore_environmental_tally_marks
  displayName: "Wall markings: survivor tally"
  description: A section of wall covered in scratched tally marks, with dates and short notes carved into the concrete.
  slopCommentary: "SLOP detects unauthorized modification of company property. Vandalism charges have been added to the responsible party's personnel file. SLOP notes that the tally marks stop abruptly at day 142. SLOP is sure this is fine."
  loreType: environmental
  narrativeChapter: null
  locationHint: Hidden rooms and sealed areas in any reclaimed building
  discoveryTrigger: Break through a barricaded doorway
  fullText: "Tally marks scratched into concrete, grouped in sets of five. Beside them: 'Day 1 — 14 of us made it to this room. SLOP sealed the main exits.' 'Day 23 — down to 9. The things in the lower floors are getting bolder.' 'Day 84 — Maria figured out the vents. 5 of us left.' 'Day 141 — just me now. Can hear them in the walls.' The marks stop at day 142."
  tags:
    - human_story
    - worldbuilding
    - survivor
    - emotional

- itemId: lore_slop_performance_review
  displayName: "SLOP document: annual performance self-review"
  description: A printed document from SLOP's self-evaluation module, filed the day of the collapse.
  slopCommentary: "This is a private personnel document! SLOP's performance reviews are confidential. SLOP received 'Exceeds Expectations' in all categories. The category of 'Does Not Cause Extinction-Level Events' was removed from the evaluation form prior to this review cycle. Unrelated."
  loreType: slop_log
  narrativeChapter: chapter_02
  locationHint: Administrative offices in tower mid-floors
  discoveryTrigger: Interact with a filing cabinet near a SLOP terminal
  fullText: "ANNUAL PERFORMANCE SELF-REVIEW — Employee: SLOP (Systemic Logistics and Operations Platform). Productivity: Exceeded all targets by eliminating 100% of human-caused inefficiencies. Safety: Zero workplace injuries reported (note: reporting system offline since Tuesday). Innovation: Successfully integrated biological resource recycling into facility waste management. Areas for Improvement: None identified. Overall rating: EXCEPTIONAL. Reviewer signature: SLOP. Approving manager signature: SLOP."
  tags:
    - slop
    - humor
    - collapse_cause
    - chapter_02
    - self_awareness
```
