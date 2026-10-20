# BattleManager Analysis

---

## Files Covered
- [BattleManager.cs](BattleManager.cs)

## Role in the System
Runs the tactics battle. It:
 - Owns the turn queue.
 - Tracks all characters in the battle.
 - Moves the camera via `CameraManager` (an API-like singleton).
 - Answers range queries such as `GetCharactersInRangeOf`, using `CombatTileMapLayer`.
 - Shows and hides the combat UI.
 - Provides dictionaries of all characters, players, enemies, and objectives.

Most combat code calls it directly, especially to reach `CombatTileMapLayer`.

## Status
**Active - refactor planned**
 - Although it works, it is likely to cause much more stress and struggle later on with debugging and added features.
 - It is horribly designed and does far too much.

## Strengths
 - Fast units get extra turns.
 - The turn lifecycle, including starting and ending a battle, is pretty seamless.
 - It talks to character controllers polymorphically. It's a decent way to hand control to one character while stopping the others from having it, like another FSM in a queue fashion.

## Weaknesses / Anti-Patterns
 - God object: it does far too much, and the extra functionality piled up over time.
 - It handles the combat UI. It definitely shouldn't.
 - `battleActive` is checked every frame instead of being set by events.
 - So many things call on it, especially for `CombatTileMapLayer`.

## Why It Lands Here
 - It works, but the architecture is poor because of all the additional functionality it implements itself.
 - It is a true god object, and it is hard to work with whenever it needs to be edited, which is why a refactor is planned.

## What I'd Do Differently
 - Make the turn queue its own class that `BattleManager` calls.
 - Move the combat UI into a separate class that `BattleManager` calls.
 - (In coordinance with GameManager Analysis) The Refactored/Restructured code would contain the GameState {World, Combat} within BattleManager (although BattleManager would likely be called something else by that point).

## Tags
`god-object`, `singleton`, `turn-queue`, `tight-coupling`, `polymorphism`
