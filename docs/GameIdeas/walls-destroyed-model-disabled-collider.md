# Idea: Walls Use Destroyed Model + Disabled Collider

## The Idea
Walls don't go "offline" — they get destroyed. When a wall section takes enough damage it swaps to a destroyed model and its collider is disabled. Creatures path through the breach naturally via the navmesh. No special logic required.

## How It Works
- **Intact state**: Normal wall model, collider enabled
- **Destroyed state**: Destroyed model, collider disabled — breach is open
- **Repaired state**: Swaps back to intact model, collider re-enabled
- Simple state machine, no edge cases

## Why Destroyed Not Offline
- Machines go offline — they're electronics
- Walls get caved in by apex fauna — they should look like it
- Visual language communicates the breach instantly without any UI
- The destroyed model sitting in place makes every hole readable from ground level and from the terminal

## Wall Tiers
- Walls are tiered objects — tier 1 through whatever the max is
- Each tier has significantly more health than the last
- Maximum available tier is tied to control tower upgrade level
- Wall strength naturally keeps pace with wave difficulty

## Repair
- Repair bots restore destroyed walls when resources are available and a repair order is issued from the terminal
- "Repair all walls" from the terminal dispatches bots to every destroyed section simultaneously
- Manual repair tool available for selective priority repairs when resources are limited
- Upgrading an existing wall section to a higher tier costs resources on top of the repair

## Open Questions
- Does destroyed wall rubble slow creature movement slightly or is it clean pass-through?
- Can wall sections be upgraded in place or do they need to be rebuilt from scratch?
- Are gates a separate category from walls with their own tier system?
