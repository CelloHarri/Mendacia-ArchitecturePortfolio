# GameManager Analysis

---

## Files Covered
- [GameManager.cs](GameManager.cs)

## Role in the System
Intended to jump-start the game's systems and to hold global state: game version, pause permission, game state (World/Battle), current input type, audio settings, and save data. Its genuine, simple purpose is the game version and whether pausing is allowed. It is an autoload, so other systems read its state through static fields.

It is unfinished. The save and load functions are placeholders.

## Status
**Active - refactor planned**
 - It works, but it is dangerous and unscalable.
 - The refactor options: 
    - Just move state into their own singletons and handle those startup processes within those singletons
    - Above and moving singleton functionality out of singleton objects.

## Strengths
 - Creates a default config on first run.
 - An easy place to look for global state.
 - Small right now.

## Weaknesses / Anti-Patterns
 - Its purpose in my current architecture is very simple: it holds the game version and whether pausing is allowed. Everything else it holds belongs to another system. Each major system is its own singleton, and those systems should start themselves rather than be jump-started here. This is the biggest issue.
 - All it really does is hold state, and that forces coupling. Other singletons read from it, which is resolved by moving the state out.
 - Save and load are unfinished placeholders.

## Why It Lands Here
 - It lands in `Poor` because its real purpose is very small (game version and whether pausing is allowed), and the rest of what it holds mostly forces coupling.
 - Over time I realized the systems I made should handle their own startup.

## What I'd Do Differently
 - Keep only what is truly global in `GameManager`: the game version and whether pausing is allowed.
 - Derive the game state (World/Battle) from `BattleManager` instead of storing it here.
 - Move the input type into `InputManager`.
 - Move audio settings into `SoundManager`.
 - Move save data into a new `SaveManager`.
 - In short: put each designated state into its own system.

## Tags
`singleton`, `global-state`, `tight-coupling`
