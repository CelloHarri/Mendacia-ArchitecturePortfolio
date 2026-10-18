# AttackPattern Analysis

---

## Files Covered
- [AttackPattern.cs](AttackPattern.cs)

## Role in the System
Defines the shape of an area-of-effect (splash) attack on the tile grid, such as a 2x2 zone for a fireball-style spell. It is used for all attacks, acts, magic, and items, and built so new ones are easy to add. It is queried by `BattleManager` (`GetCharactersInRangeOf`) and `CombatTileMapLayer` (range and border drawing). It depends on nothing else in the game.

## Status
**Active - no planned changes**
 - In use in the game and working as intended.

## Strengths
 - Built as a `Resource`, so patterns can be authored and transformed in the editor with no code, which is great for me as the designer.
 - Rotates custom shapes (e.g. an L shape like a knight's move) based on the offset from the character's position to the hovered tile.
 - Self-contained, with no dependencies on other game systems.
 - Simple and easy to customize and iterate on.

## Weaknesses / Anti-Patterns
 - The biggest issue is `IsValidOffset`. Every call passes the pattern's own min and max range back into itself, even though the resource now has those fields.
   - History: originally the resource had no range variables, so callers supplied them. When I added the fields, the parameters stayed.
 - Custom shapes ignore range. I don't consider this a problem.

## Why It Lands Here
 - The redundant parameters are a small issue. Passing the range in makes it very explicit in the calling code what the range is.
 - The design itself is resource- and data-driven, built around the editor for on-the-fly iteration.

## What I'd Do Differently
 - Have `IsValidOffset` read the resource's own range fields instead of taking them as parameters.
 - Possibly rename it, as there is a separate system for BulletPatterns that `AttackPattern` could easily be mistaken for.

## Tags
`data-driven`, `resource-based`, `self-contained`, `grid`
