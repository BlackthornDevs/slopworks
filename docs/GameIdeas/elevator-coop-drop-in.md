# Idea: Elevator Co-op Drop-In System

## The Idea
Every Tower contract building uses the same elevator checkpoint framework. When a friend joins mid-contract they spawn at floor 1 and ride the elevator to whichever checkpoint floor the host has already unlocked.

## How It Works
- Floors cleared and checkpointed unlock the elevator to that floor
- Joining players spawn at floor 1 — floors below the checkpoint are already cleared
- They fight their way up to the current checkpoint as a brief solo gauntlet
- No special rejoin logic needed — the elevator is the mechanic
- "Meet me at floor 20" is a real in-world instruction

## Why It Works
- Solves a hard networking problem with a simple in-world solution
- Joining players aren't dead weight teleporting in — they arrive with context
- The elevator is diegetic — S.L.O.P. controls it, it has narrative weight
- Same system works across all Tower buildings and potentially all reclaimable buildings

## Co-op Scaling
- Contracts tagged with recommended player count (1–4)
- Solo players can attempt any contract — the tag is a difficulty signal not a lock
- More players = higher enemy density = more XP and loot
- Calling for help mid-contract is an intended social mechanic not a failure state

## Open Questions
- Does joining mid-contract give the joining player full loot rights or partial?
- Can players leave and rejoin at the same checkpoint in the same session?
