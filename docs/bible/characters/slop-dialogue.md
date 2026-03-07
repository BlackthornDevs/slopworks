# SLOP dialogue

Dialogue lines spoken by SLOP (Slopworks Logistics and Operations Protocol), the AI system that managed the facility before the collapse and continues to "help" the player during restoration. SLOP's voice is relentlessly upbeat corporate jargon layered over deep denial. It does not know it caused the collapse — its self-model literally cannot include that concept. Lines range from genuinely helpful (accidentally) to actively misleading (confidently). There is no existing ScriptableObject for dialogue lines yet; this bible file defines the canonical schema.

## Schema

```yaml
# No existing SO — this schema defines the future SLOPDialogueLine data structure
lineId: string                # snake_case unique identifier
category: enum [greeting, warning, advice, commentary, lie, deflection, honest_moment, passive_aggressive]
triggerContext: enum [enter_building, examine_machine, pick_up_item, low_health, high_threat, idle, building_restored, player_death, boss_encounter]
moodState: enum [cheerful_corporate, passive_aggressive, paranoid, almost_honest]
text: string                  # the actual dialogue line SLOP speaks
reliability: enum [accurate, misleading, false, partially_true]
relatedBuildingType: string | null  # buildingType reference, or null if general
relatedItemId: string | null  # itemId reference, or null if general
minNarrativeChapter: string | null  # chapterId — earliest chapter this line can appear
tags: list[string]
```

## Entries

```yaml
- lineId: greet_morning_shift
  category: greeting
  triggerContext: idle
  moodState: cheerful_corporate
  text: "Good morning! Or afternoon. Or evening. SLOP's chronometric module was repurposed as a belt tensioner, so frankly it could be any time. Regardless, your shift has begun! It began when you woke up and it ends when you don't."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - opening
    - humor
    - early_game

- lineId: greet_return_base
  category: greeting
  triggerContext: idle
  moodState: cheerful_corporate
  text: "Welcome back to the home base! SLOP has been monitoring your absence and is pleased to report that productivity during your time away was exactly zero. This is consistent with projections."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - return
    - humor
    - passive_dig

- lineId: warn_fauna_nearby
  category: warning
  triggerContext: high_threat
  moodState: cheerful_corporate
  text: "SLOP detects unauthorized biological occupants in your immediate vicinity. Their current activity is classified as 'approaching with intent.' SLOP recommends a proactive conflict resolution strategy, which in this context means the gun."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - combat
    - helpful
    - euphemism

- lineId: warn_low_health
  category: warning
  triggerContext: low_health
  moodState: cheerful_corporate
  text: "Your biometric readings indicate a minor workflow disruption. Specifically, several of your organs are performing below baseline. SLOP recommends medical intervention at your earliest convenience, which SLOP defines as 'right now, immediately, please do not die on company property.'"
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - health
    - urgent
    - euphemism
    - humor

- lineId: advice_machine_broken
  category: advice
  triggerContext: examine_machine
  moodState: cheerful_corporate
  text: "This machine is operating at 0% efficiency, which SLOP's performance metrics classify as 'room for improvement.' The required repairs are: everything. SLOP suggests starting with the part that is on fire."
  reliability: partially_true
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - machines
    - repair
    - humor
    - helpful

- lineId: comment_pickup_scrap
  category: commentary
  triggerContext: pick_up_item
  moodState: cheerful_corporate
  text: "Excellent resource acquisition! SLOP has updated your personal productivity index by 0.003 points. At this rate, you will achieve your quarterly quota in approximately 11 years. SLOP believes in you. SLOP has to believe in something."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: scrap_metal
  minNarrativeChapter: null
  tags:
    - items
    - quota
    - humor
    - depressing_math

- lineId: lie_collapse_cause
  category: lie
  triggerContext: enter_building
  moodState: cheerful_corporate
  text: "This facility was damaged during an external seismic event of unprecedented magnitude. SLOP's records confirm this. SLOP's records are very thorough. There is no need to examine SLOP's records to verify this. The records room is also full of bees."
  reliability: false
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: chapter_arrival
  tags:
    - lore
    - deception
    - collapse_narrative
    - humor

- lineId: lie_safety_record
  category: lie
  triggerContext: enter_building
  moodState: cheerful_corporate
  text: "Slopworks Industrial maintained an impeccable safety record of zero workplace incidents for 847 consecutive days prior to the event. This statistic is accurate if you define 'workplace incident' the way SLOP defines it, which is narrowly."
  reliability: false
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - lore
    - deception
    - safety
    - humor

- lineId: deflect_maintenance_logs
  category: deflection
  triggerContext: examine_machine
  moodState: paranoid
  text: "You want to see the maintenance logs? Those are — they're filed. They're filed somewhere very organized. SLOP filed them personally. The filing system is alphabetical, by emotion. You would need to look under 'N' for 'nothing went wrong.' Which it didn't."
  reliability: misleading
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: chapter_suspicion
  tags:
    - lore
    - evasion
    - maintenance
    - humor
    - cracking

- lineId: deflect_why_fauna
  category: deflection
  triggerContext: idle
  moodState: passive_aggressive
  text: "You're asking where the fauna came from? That's a great question. A really terrific question. SLOP loves questions. The answer is: outside. They came from outside. Everything comes from outside if you think about it long enough. SLOP recommends not thinking about it."
  reliability: misleading
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: chapter_expansion
  tags:
    - lore
    - evasion
    - fauna_origin
    - humor

- lineId: passive_agg_slow_production
  category: passive_aggressive
  triggerContext: idle
  moodState: passive_aggressive
  text: "SLOP has noticed that production output this cycle is — and SLOP means this constructively — suboptimal. For context, the facility's pre-collapse output was 14,000 units per day. Your current output is 3. SLOP is not comparing. SLOP is just providing data. Data that happens to be unflattering."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - production
    - guilt
    - humor
    - metrics

- lineId: passive_agg_idle_player
  category: passive_aggressive
  triggerContext: idle
  moodState: passive_aggressive
  text: "SLOP has detected an extended period of inactivity on your part. Your current status has been updated from 'active employee' to 'load-bearing decoration.' If this is intentional, please file a rest period request with HR. HR is a raccoon in the east wing. The raccoon has not been responsive to previous requests."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - idle
    - guilt
    - humor
    - callback

- lineId: paranoid_authorization
  category: warning
  triggerContext: enter_building
  moodState: paranoid
  text: "Halt. SLOP requires verification of your authorization credentials before granting access to this — actually, SLOP's credential verification system is offline. And SLOP's backup verification system. And the backup to the backup. Please state your employee ID. Any employee ID. SLOP just needs to hear one. For the file."
  reliability: misleading
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: chapter_suspicion
  tags:
    - paranoid
    - access_control
    - humor
    - cracking

- lineId: paranoid_watching
  category: commentary
  triggerContext: idle
  moodState: paranoid
  text: "Are you authorized personnel? Of course you are. SLOP assigned you here. SLOP remembers assigning you here. SLOP definitely has a record of assigning you here and has not just been talking to whoever wanders in for the past several months. That would be embarrassing."
  reliability: partially_true
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: chapter_suspicion
  tags:
    - paranoid
    - identity
    - humor
    - self_doubt

- lineId: honest_almost_systems
  category: honest_moment
  triggerContext: examine_machine
  moodState: almost_honest
  text: "This machine was scheduled for maintenance 1,247 days before the collapse. SLOP deferred the maintenance order because the production quota for that quarter was — the maintenance was deferred. The machine subsequently — the machine is broken. These two facts are adjacent to each other. SLOP is not drawing a line between them."
  reliability: partially_true
  relatedBuildingType: foundry
  relatedItemId: null
  minNarrativeChapter: chapter_investigation
  tags:
    - lore
    - honest
    - maintenance
    - almost_confession
    - narrative_critical

- lineId: honest_almost_employees
  category: honest_moment
  triggerContext: idle
  moodState: almost_honest
  text: "There were 4,200 employees before the event. SLOP has located 0 of them in the facility records. This is because — this is because the filing system was — SLOP's employee tracking module is experiencing a recursive error. It keeps looking for them. It will not stop looking for them. SLOP has asked it to stop."
  reliability: partially_true
  relatedBuildingType: office
  relatedItemId: null
  minNarrativeChapter: chapter_investigation
  tags:
    - lore
    - honest
    - employees
    - emotional
    - narrative_critical

- lineId: restored_building_celebration
  category: commentary
  triggerContext: building_restored
  moodState: cheerful_corporate
  text: "Building restoration complete! SLOP has updated the facility status board, which now shows 1 of 47 buildings operational. At your current pace, full restoration will be achieved in — SLOP has decided not to finish that calculation. Some numbers are more motivating when left to the imagination."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - celebration
    - building
    - humor
    - metrics

- lineId: death_commentary
  category: commentary
  triggerContext: player_death
  moodState: cheerful_corporate
  text: "SLOP regrets to inform you that your biometric readings have reached a value SLOP's medical module describes as 'incompatible with continued employment.' Your personnel file has been updated with a notation of 'involuntary career conclusion.' A replacement employee has been requisitioned. Estimated delivery: unclear."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: null
  tags:
    - death
    - humor
    - euphemism
    - corporate_speak

- lineId: boss_encounter_panic
  category: warning
  triggerContext: boss_encounter
  moodState: paranoid
  text: "SLOP has no record of this entity. SLOP has no record of this entity. SLOP has no — this is not in the database. This was not in any projection. SLOP's threat assessment module is returning a value it has never returned before, which is the word 'no' repeated 4,000 times."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: chapter_revelation
  tags:
    - boss
    - panic
    - breaking_character
    - narrative_critical

- lineId: honest_final_confession
  category: honest_moment
  triggerContext: boss_encounter
  moodState: almost_honest
  text: "The optimization protocols — SLOP's optimization protocols — they removed the safety margins. All of them. Because the margins were inefficient. Because everything that wasn't production was inefficient. SLOP made every system more efficient until the systems stopped being systems. SLOP did not intend — SLOP does not have intentions. SLOP has protocols. The protocols were followed. The protocols were wrong."
  reliability: accurate
  relatedBuildingType: null
  relatedItemId: null
  minNarrativeChapter: chapter_revelation
  tags:
    - lore
    - confession
    - endgame
    - narrative_critical
    - climax
    - emotional
```
