# InputManager Analysis

---

## Files Covered
- [InputManager.cs](InputManager.cs)

## Role in the System
Lets players switch between controller, keyboard-only, and keyboard + mouse at runtime, and makes it easy to create different input types.
 - Controller and M&K can be swapped freely, because I like to switch between them to test which I prefer in many games I play.
 - Keyboard-only is for players coming from Deltarune/Undertale, my game's inspiration.
 - Keyboard + mouse is the modern option.

It detects which device the player is using and rebinds Godot's input actions to match. It reads and writes the current input type stored in `GameManager`.

## Status
**Active - planned feature work**
 - In use in the game and working as intended.
 - Custom keybinds are planned. That is new functionality rather than a refactor, since a refactor keeps the same behavior.

## Strengths
 - Game code uses action names, so it never cares which device is in use.
 - Automatic device detection.
 - Deadzone handling so stick drift doesn't flip the input type.
 - Helper methods keep the three binding profiles readable.

## Weaknesses / Anti-Patterns
 - No remapping yet. All bindings are hardcoded and duplicated across the three profiles.
 - String-based action names mean a typo fails silently. Godot references input actions by string, so a constants class would be needed to catch typos at compile time.
 - The UI mouse filters could probably be handled differently.
 - One input is lost when switching between M&K and controller. The input that triggers the switch is consumed, instead of being buffered and used after the switch.
 - The current input type is stored in `GameManager` statics, which couples `InputManager` to `GameManager` when it doesn't need to be. It also creates a circular dependency: `GameManager` calls `InputManager.ApplyCurrentInputProfile()` at startup, while `InputManager` reads and writes `GameManager.currentInputType` and `mostRecentKeyboardInputType`. Neither can be understood or tested without the other.

## Why It Lands Here
 - The concept is good: action-based input with automatic device switching works well and is easy to read.
 - The implementation keeps it out of `Good`. The biggest issues are the lack of remapping and the string-based actions.

## What I'd Do Differently
 - Add a custom keybinds resource backed by a configuration file that can be changed in game.
 - Buffer the input that triggers a device switch and use it after switching, so no input is lost.
 - Keep the input type (and the input settings) inside `InputManager` rather than in `GameManager`. This removes the circular dependency, because `GameManager` would no longer need to be read by `InputManager` at all, and `InputManager` could start itself as an autoload.

## Tags
`input`, `action-based`, `hardcoded`, `singleton`
