# Idea: Wave Consequence Hybrid Model

## The Idea
Waves operate on a hybrid consequence system. Speed and cleanliness are rewarded. Damage is punishing but never permanently destructive. The floor is never total base wipe. The ceiling is always worth chasing.

## How It Works

### Time Bonus (Ceiling)
- The faster a wave is cleared the more bonus resources drop on top of standard loot
- Scaled to clear speed — a 2 minute clear pays out more than an 8 minute clear
- Incentivizes preparation, good loadouts, and aggressive play
- Rewards skill without punishing players who just survive

### Damage Consequences (Floor)
- Machines that get hit go **offline** — not destroyed, not gone, just offline
- Walls that get breached switch to a **destroyed model** with collider disabled
- Creatures path through breach naturally — navmesh handles it, no special logic
- Repair bots restore machines and walls when resources are available and a repair order is issued from the terminal
- Production debt: Management still expects output numbers while you're repairing

### What Is Never Lost
- Tower contract progress
- Researched recipes and blueprints
- Biome unlocks
- Character level
- The layout of what you built

### Stray Enemies
- Regular enemies that aren't cleared by wave end become strays — ambient hostiles in your base
- Bosses and elites always retreat at wave end — no permanent softlock from an unkillable enemy in your factory
- Strays create a cleanup loop before you can safely return to building

## The Melee Fallback
- Every class has a melee weapon that never runs out of ammo
- Prevents softlock scenarios where you're out of ammo and can't clear remaining enemies
- Intentionally bad against elites — creates tension not a free pass

## Open Questions
- Does the time bonus scale with difficulty — is a fast clear on a hard wave worth more than the same clear time on an easy one?
- How long do strays persist before despawning if not killed?
- Does production debt carry over between waves or reset each time?
