# CombatTileMapLayer Analysis

---

## Files Covered
- [CombatTileMapLayer.cs](CombatTileMapLayer.cs)

## Role in the System
Establishes pathfinding on the combat grid, highlights move and attack ranges, and holds data about the location of every character and objective on the field. It is called by `BattleManager`, `TileCombatController`, and `Character`. It extends Godot's `TileMapLayer`, and because a `TileMapLayer` only holds one layer of tiles, two additional layers are attached to it for indicator tiles and props.

## Status
**Active - no planned changes**
 - In use in the game and working well.

## Strengths
 - Maps are painted in the editor rather than coded, with walkability and movement cost stored as tile data.
 - Hostile units block movement while friendly units can be passed through.
 - Terrain weights affect movement, and slower terrain also slows the movement animation.
 - Self-contained.
 - Note: the pathfinding algorithms (A* and the weighted reachability search) were written with Claude. The design around them (the tile data, the blocking rules, and how movement cost feeds the animation) is mine.

## Weaknesses / Anti-Patterns
 - God object: one class handles pathfinding, move and attack range highlighting, and the position data for every character and objective.
   - This is my only significant issue with it.
 - Because of how `TileMapLayer` works, I needed to attach two additional layers (indicators and props) to this class, which added to its responsibilities.

## Why It Lands Here
 - It works pretty well and is self-contained, but it does far too much in one class.
 - That is why it is `Decent` rather than `Good`. The behavior is solid, but the structure is not.

## What I'd Do Differently
 - Split the responsibilities into separate classes, and have the tilemap call them for readability.
 - This is not planned. For a solo project, a refactor of working code would slow down making the game, so the ROI is negative right now.

## Tags
`god-object`, `pathfinding`, `a-star`, `grid`, `data-driven`
