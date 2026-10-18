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
**Legacy / kept as-is**
 - It will likely stay the same. It works, and the combat system is finalized for now.
 - It is horribly designed and does far too much, but rewriting working, finalized code isn't worth the time.
 - If combat needs to be functionally worked on again, it would be worth refactoring this code first.

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
 - It works and combat is finalized, but the architecture is poor because of all the additional functionality it implements itself.
 - It is a true god object, and it would be hard to work with if it needs to be edited again.

## What I'd Do Differently
 - Make the turn queue its own class that `BattleManager` calls.
 - Move the combat UI into a separate class that `BattleManager` calls.

## Tags
`god-object`, `singleton`, `turn-queue`, `tight-coupling`, `polymorphism`
