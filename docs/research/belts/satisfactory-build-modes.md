# Satisfactory Belt Build Modes

Research compiled 2026-03-09.

## Overview

Exactly three build modes. Cycled by pressing or holding R during placement (between first and second click). Preview updates in real-time.

- Default and Straight: available since 1.0 (September 2024)
- Curve: added in Patch 1.1.0.0 (April 2025)

No "Noodle" or "Free" mode -- those were community requests never implemented.

## Default Mode

The "noodle" mode. Most flexible, recommended for general use.

- Creates a smooth curved path between start and end connection points
- Curve respects position AND orientation of both endpoints
- If starting from a building's output port, belt initially follows port's forward direction before curving toward target
- Auto-calculates tangent magnitudes based on displacement between endpoints (longer distance = larger tangent = gentler curve)
- Handles height changes well (decomposes into turn-then-ramp or ramp-then-turn)
- Works down to 2m minimum turn radius
- Best for: general use, height changes, tight spaces, short belts

## Straight Mode

Forces belt into straight-line segments with sharp corners. For grid-aligned factories.

- Piecewise-linear straight segments connected by 90-degree bends
- L-shape (perpendicular endpoints): two straight segments meeting at 90-degree corner. Turn point = where forward axis of start intersects perpendicular axis of end
- S-shape (parallel endpoints facing same direction but offset laterally): two 90-degree turns to traverse the lateral offset
- Same-line (directly aligned): single straight segment, no turns
- Handles height differences POORLY -- community consensus is same-elevation only. Combining turns + height produces graphical glitches, weird kinks, crooked belts
- Known bug: belt ending right at 90-degree corner can have few degrees of slant
- Prioritizes architectural cleanliness over shortest path
- Best for: grid-aligned factories at same elevation

## Curve Mode (Added 1.1)

Railway-style smooth curves. Tangent-driven.

Official description: creates belts "curved in a way similar to railways, according to the position and orientation of the starting and ending points."

- Tangent-driven spline where curve shape is controlled by forward directions of both start and end points
- Gives start/end orientations much stronger influence over curve shape than Default mode
- Best used with deliberately angled conveyor poles to sculpt wide, sweeping arcs
- Practical difference from Default: Default auto-routes and minimizes curve complexity. Curve mode respects endpoint orientations more faithfully, producing wider arcs at cost of potentially longer belt paths
- If start/end directions would require tight S-curve in Default mode, Curve mode creates wider sweeping arc instead
- Same 2m minimum turn radius
- Not intended for very short/tight turns
- Best for: aesthetic curved routes with pre-placed poles, wide sweeping arcs

## Comparison Table

| Aspect | Default | Straight | Curve |
|---|---|---|---|
| Path shape | Auto-routed smooth curve | Piecewise linear + 90-degree corners | Tangent-driven smooth arc |
| Tangent source | Auto-calculated from displacement | N/A (no curves) | Port/support forward directions |
| Height changes | Good (turn then ramp) | Poor (glitchy) | Moderate |
| Short distances | Best option | Not recommended | Not recommended |
| Tight turns | Works down to 2m radius | Sharp 90-degree only | Prefers wider arcs |
| Aesthetic | Organic/flexible | Grid-aligned/clean | Sweeping/railway-like |
| Best for | General use, height changes | Grid-aligned factories | Aesthetic curved routes |

## Shared Constraints (All Modes)

| Parameter | Value |
|---|---|
| Minimum segment length | ~0.5m |
| Maximum segment length | 56m (7 foundations) |
| Minimum turn radius | 2m |
| Maximum incline | 35 degrees |
| Max vertical rise | 31m vertical over 45m horizontal |
| Simultaneous turn + incline | Not allowed |

## Edge Cases by Mode

| Scenario | Default | Straight | Curve |
|---|---|---|---|
| Very short belt (<2m) | Works | Not recommended | Not recommended |
| Very long belt (>56m) | Fails -- use intermediate supports | Same | Same |
| Same height, different horizontal | Smooth curve | L-shape | Sweeping arc |
| Different heights | Turn then ramp (works well) | Glitchy/kinked | May produce unexpected shapes |
| 180-degree reversal | Cannot directly connect | Cannot directly connect | Cannot directly connect |
| Clipping through buildings | Yellow warning, allowed | Yellow warning, allowed | Yellow warning, allowed |

## Sources

- [Conveyor Belts - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Belts)
- [Patch 1.1.0.0 - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Patch_1.1.0.0)
- [September 2024 Video - Conveyor Belt Build Modes](https://archive.satisfactory.video/transcriptions/yt-qtPseN3OyNU)
- [Belt Straight Mode Discussion - Steam](https://steamcommunity.com/app/526870/discussions/0/4751949102181693765/)
- [Noodle Build Mode Request - Satisfactory Q&A](https://questions.satisfactorygame.com/post/62adab7fca608e0803515179)
