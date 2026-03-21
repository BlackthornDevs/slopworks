# Idea: Per-Resource-Type Scanner Unlocked at Control Tower

## The Idea
Inspired by Satisfactory's resource scanner. You unlock the ability to scan for a specific resource type and it pings the map showing you where deposits of that type are located. Unlocked per resource type through control tower upgrades — you have to specifically unlock iron scanning before you can ping iron nodes.

## How It Works
- Each resource type has a corresponding scanner unlock tied to a control tower upgrade tier
- Once unlocked, scanning for that resource type shows pings on the isometric terminal map
- Scan range sits between power transmission range and zone of influence — you can detect resources further out than you can currently power, letting you plan ahead
- Results show on the terminal as map markers — the commander can identify targets for the next power run investment

## Why Per Resource Type
- Unlocking a new scanner is a meaningful reward — it opens up a whole new input stream
- Forces deliberate decisions about which resource to prioritize scanning for
- Matches the progression rhythm — you unlock the ability to build something new, then scan to find what you need to feed it
- Creates a discovery moment each time: you upgrade the tower, scan for a new type, and suddenly see three nodes you had no idea were out there

## Integration With the Terminal
- Scanner results are a natural fit for the isometric view
- Commander uses downtime between waves and Tower runs to scan and plan
- Ground players get waypoints from commander: "there's a node two sectors east, let's run power that direction"

## Open Questions
- Does scan range scale with tower upgrade level independently of the other rings?
- Are node locations persistent and fixed or do they vary per world seed?
- Can you scan outside your zone of influence — i.e. into active creature territory?
- Is there a cooldown on scanning or is it free to use at any time?
