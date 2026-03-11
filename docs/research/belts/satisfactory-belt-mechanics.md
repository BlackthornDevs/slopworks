# Satisfactory Belt Mechanics

Research compiled 2026-03-09.

## Placement Workflow

Two-click placement:
- First click: aim at an output port (or input port, or empty ground) to set start point. Port direction becomes entry tangent.
- Second click: aim at destination port or empty space to finalize. Belt curves between endpoints.

Direction rules:
- Click output port first: items flow away from that port.
- Click input port first: items flow toward it.
- Neither endpoint is a port (empty poles): flow goes first-click to second-click.
- Once placed, direction cannot be reversed.

## Curvature / Spline System

Built on Unreal Engine's cubic Hermite splines (mathematically equivalent to cubic Bezier).

Each spline point stores position + tangent vector:
- Start tangent = forward direction of output port (or player facing direction from a pole)
- End tangent = forward direction of input port at destination
- Tangent magnitude controls how far the curve "pulls" before bending

Critical constraint: belts cannot incline vertically while turning horizontally simultaneously. They decompose the path into sequential phases -- turn then rise, or rise then turn. This applies to ALL build modes.

## Snapping Behavior

- Port snapping: auto-snap to machine input/output ports when cursor is near. Color-coded (green = output, orange = input).
- Conveyor poles: intermediate snap points. Directionless -- belt determines flow.
- Splitters/Mergers: separate buildings, can be placed on existing belt segments (belt splits into two segments).
- Belt-to-belt: no direct merging. All merging/splitting goes through explicit Merger/Splitter buildings.
- Upgrade in-place: aim at belt with higher-tier belt selected to upgrade without removing.

## Height Changes

- Maximum slope: 35 degrees (arctan formula)
- Maximum vertical rise: 31m over 45m horizontal
- Conveyor Lifts: separate building for vertical-only transport. 7-51m height range, 2x2 footprint.
- Practical threshold: if vertical change > few meters and horizontal distance is short, lifts are preferred.

## Item Transport (Simulation)

Distance-offset model (confirmed by DOTS reimplementation):
- Each segment stores items as a list sorted by distance
- Each item: type + distance-to-next (integer subdivisions, e.g. 16 per unit)
- Each tick, distance decrements by belt speed factor
- When distance = 0, item attempts transfer to next segment
- Processing order: back-to-front (last segment first) to prevent cascading

One entry point, one exit point per segment. Segments processable independently (parallelizable).

## Visual Representation

- Spline mesh instancing via custom shader
- Belt dimensions: 2m wide, 1m tall
- Spline data written to global texture (position + orientation)
- Items are instanced meshes (not individual actors)
- Factory_Baked shader reads item belt-distance, looks up position from texture, generates World Position Offset
- Item positions computed entirely on GPU -- no per-item CPU transform updates
- Items occupy ~1.186m of belt length each (843 items per 1000m)

## Key Constraints

| Constraint | Value |
|---|---|
| Minimum segment length | ~0.5m |
| Maximum segment length | 56m (7 foundation lengths) |
| Minimum turn radius | 2m |
| Maximum slope angle | 35 degrees |
| Max vertical rise per segment | 31m over 45m horizontal |
| No simultaneous turn + slope | Hard constraint |
| Belt width | 2m |
| Belt height | 1m |
| Item spacing on belt | ~1.186m per item |

Speed tiers: Mk.1 (60/min) through Mk.6 (1200/min).

## Sources

- [Conveyor Belts - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Belts)
- [Conveyor Rendering - Satisfactory Modding Documentation](https://docs.ficsit.app/satisfactory-modding/latest/Development/Satisfactory/ConveyorRendering.html)
- [Implementing Conveyor Belts a la Satisfactory with DOTS in Unity](https://theor.xyz/dots-burst-satisfactory-belts/)
- [Conveyor Lifts - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Lifts)
- [Conveyor Belt - Satisfactory Fandom Wiki](https://satisfactory.fandom.com/wiki/Conveyor_Belt)
