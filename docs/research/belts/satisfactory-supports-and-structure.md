# Satisfactory Conveyor Supports and Structure

Research compiled 2026-03-09.

## Support Types

### Standard Conveyor Pole (Tier 0)
- Adjustable height: 1-7m in 2m increments
- Look up/down during placement to adjust height, scroll wheel to rotate
- Default pole type auto-placed during belt construction
- Two-step placement: first click sets XY, then adjust height + confirm

### Stackable Conveyor Pole (Tier 2)
- Base unit: 3m tall, each stacked unit adds 2m
- Can stack indefinitely (players report 30-50+)
- Includes ladders on both sides
- Key use case: belt buses at staggered heights (1m, 3m, 5m, 7m...)
- Cannot be auto-placed during belt construction -- must be pre-placed independently
- Snaps onto other stackable supports and onto the ground

### Conveyor Wall Mount (AWESOME Shop)
- Attaches to wall/foundation sides only (not ceilings)
- Fixed 1m height, no adjustment

### Conveyor Ceiling Mount (AWESOME Shop)
- Attaches to foundation undersides
- Stackable downward (multiple hanging levels)

### Conveyor Wall Hole / Floor Hole (AWESOME Shop)
- Pass-through supports for belts/lifts penetrating walls or floors
- Floor holes come in 2m, 3m, and 5m height variants

## Key Behaviors

- All pole types allow a belt from both sides, but only if direction is consistent (both flowing same way through pole)
- Poles are cosmetic after placement -- dismantling a pole leaves the belt floating and fully functional
- Poles have "soft clearance" allowing overlap with other buildables
- Both "belt-first" (auto-pole) and "pole-first" (manual routing) workflows supported

## Segment Structure

Each span between two connection points (pole-to-pole, machine-to-pole, machine-to-machine) is a separate discrete segment/object.

- Single segment length: 0.5 to 56 meters (Euclidean distance)
- Items flow continuously across segment boundaries via distance-offset transfer
- Segments process in reverse order (endpoint first, backward) for parallelism

## Role of Supports in the Spline

- Supports define position only -- they are directionless anchor points
- Machine ports define both position AND direction (port faces a specific way)
- Tangent/direction at a support is determined by the belt segments, not the support itself
- Supports are essentially waypoints where one segment ends and another begins

## Belt-to-Support vs Belt-to-Machine Connection

| Property | Machine Port | Support Pole |
|----------|-------------|-------------|
| Direction | Fixed (port faces specific way) | Directionless (belt determines flow) |
| Position | Fixed on machine body | Adjustable height |
| Connection capacity | One belt per port | One belt per side (two total, same direction) |
| Direction determination | Port type (input/output) forces direction | Construction order determines direction |
| Auto-placement | No (machine already exists) | Yes (standard pole auto-placed with belt) |
| Visual indicators | Green arrows (output), orange (input) | None -- direction implicit from construction |

## Conveyor Lifts vs Angled Belts

Conveyor Lifts:
- Height range: 7-51m per lift (chainable with Floor Holes)
- Dimensions: 2x2 footprint
- Six tiers matching belt marks (60-1200 items/min)
- Items cannot be picked up from or placed onto lifts
- Splitters/Mergers can attach at endpoints or along length

When to use which:
- Lifts: vertical-only transport, compact footprint, floor-to-floor
- Angled belts: combined horizontal+vertical displacement, gentle ramps
- Practical threshold: >few meters vertical + short horizontal = use lift

## Sources

- [Conveyor Poles - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Poles)
- [Conveyor Belts - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Belts)
- [Conveyor Lifts - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Lifts)
- [Stackable Conveyor Pole discussion](https://steamcommunity.com/app/526870/discussions/0/4625855174854931803/)
