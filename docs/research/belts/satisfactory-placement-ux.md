# Satisfactory Belt Placement UX

Research compiled 2026-03-09. Frame-by-frame breakdown of the placement workflow.

## First Click Behavior

### On a machine output port
- Belt starts directly from port, no support pole placed
- Port's outward-facing direction becomes entry tangent of spline
- Visual indicators: outputs show green arrows, inputs show orange symbols

### On empty ground
- Conveyor pole auto-placed at click location
- Pole placement is two-step: first click sets XY, then look up/down to adjust height (1-7m in 2m increments), scroll wheel to rotate, second click confirms
- Placing belt on empty ground = 3 clicks total (pole confirm + belt endpoint)

### On an existing conveyor pole
- Belt starts from that pole's connector
- Each pole allows belt from both sides (one in, one out), consistent direction required

### Visual feedback after first click
- Translucent blue hologram preview of belt spline appears immediately
- Stretches from start point toward cursor
- Start point locked, end point follows mouse

## Preview During Mouse Movement

### Spline update
- Belt hologram is a spline mesh rendered via custom shader
- As mouse moves, endpoint follows cursor's raycast hit in 3D
- Spline recalculates every frame

### Grid snapping
- Belts do NOT snap to a world grid -- follow mouse freely in 3D
- DO snap to building ports and existing poles
- Aiming at a building auto-snaps to nearest appropriate port

### Height control
- Cursor position in 3D determines height
- Looking up raises endpoint, looking down lowers it
- Belt spline adjusts to accommodate height difference

### Hologram colors (three-tier validity)
- **Blue** (default): valid placement, no conflicts
- **Yellow**: soft clearance warning -- belt clips through objects. Placement STILL ALLOWED. Intentional design for creative freedom.
- **Red**: hard clearance violation -- placement BLOCKED. Causes: invalid port connections (output-to-output), turn radius too tight (<2m), belt too long/short/steep, impossible geometry

### Endpoint preview
- When cursor is over empty ground (not a port), preview shows ghost pole at endpoint location along with belt spline

## Second Click Behavior

### On an input port
- Belt connects directly, no support pole at that end
- Port's inward direction becomes exit tangent of spline
- Belt curves to arrive aligned with port direction

### On empty space
- Conveyor pole auto-placed, belt connects to it
- Same two-step height adjustment applies

### On an existing pole
- Belt connects to pole's open slot
- Both sides can have a belt, same direction required

### If curve too tight
- Hologram turns red, placement blocked
- Relates to 2m minimum turn radius
- Player must move endpoint further away or less extreme angle

### Direction rule
- First snap point = input (items flow IN here)
- Second snap point = output (items flow OUT here)
- Exception: if first point connects to a building's input port, direction reverses for correct flow

## Support Pole Mechanics During Placement

- Auto-placed poles match height needed for belt endpoint
- Player controls height by looking up/down between first and second click of pole placement
- Standard pole height: 1-7m in 2m increments
- Stackable poles cannot be auto-placed -- must be pre-placed, then belts connected to them
- Scroll wheel rotates the pole, NOT the belt curve

## Multi-Segment / Chain Placement

- After placing a belt, build tool remains active but does NOT auto-start next belt
- Must manually click on new pole/port to start next segment
- Since pole is right under cursor, clicking immediately feels nearly continuous
- Belt direction continues logically from output end of previous belt

## Edge Cases

- Belts CAN clip through each other (yellow warning, placement allowed)
- Belts CAN clip through terrain (yellow warning, allowed)
- Output-to-output / Input-to-input: hologram won't snap, placement blocked (red)
- Length limits: 0.5m minimum, 56m maximum per segment
- Maximum slope: 35 degrees

## Sources

- [Conveyor Belts - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Belts)
- [Conveyor Poles - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Conveyor_Poles)
- [Build Gun - Official Satisfactory Wiki](https://satisfactory.wiki.gg/wiki/Build_Gun)
- [Buildable Holograms - Satisfactory Modding Documentation](https://docs.ficsit.app/satisfactory-modding/latest/Development/Satisfactory/BuildableHolograms.html)
- [Why must I click twice to place conveyor belts? - Steam](https://steamcommunity.com/app/526870/discussions/0/3875966763788525220/)
- [How do you set height of a conveyor belt? - Steam](https://steamcommunity.com/app/526870/discussions/0/2527030866853577470/)
