# DialogueManager Analysis

---

## Files Covered
- [DialogueManager.cs](DialogueManager.cs)
- [DialogueSequence.cs](DialogueSequence.cs)
- [DialogueAction.cs](DialogueAction.cs)
- [DialogueActions/](DialogueActions) (one file per action: Bark, ChangeSpeed, CheckFlag, Choice, ClearText, Close, SetBlip, SetFlag, SetSpeaker, Sound, Text, Wait)
- [DialogueChoiceOption.cs](DialogueChoiceOption.cs)
- [DialogueBox.cs](DialogueBox.cs)
- [DialoguePreviewer.tscn](DialoguePreviewer.tscn) (tool for previewing dialogue)

## Role in the System
This code's role in *Mendacia* is dialogue. It handles all the different sequences of dialogue that can occur with each `DialogueAction`. It is designed to be easily iterated upon with resource-based data orientation.

It sits in the `Good` folder, but really in a middle ground between `Good` and `Decent`. The contrast between how easy it is to use and how okay its architecture is makes it hard to place (see **Why It Lands Here**).

## Status
**Active - no planned changes**
 - This is in active use in the game.
 - It is currently fully functional and relatively easy to make content with, while also being easy to add new DialogueActions if needed.

## Strengths
 - Easy to design dialogue sequences with little friction.
 - Flag-based branching system that checks temporary or persistent save flags to branch dialogue.
   - Temporary flags are non-permanent flags that allow for branching dialogue within a single dialogue sequence. For instance, when a character asks a question, it sets a temporary flag to a specific value, and depending on that value, the dialogue box shows different information.
   - Persistent flags can be anything that has occurred in the game and saved a flag. The dialogue system can check that flag's value and branch to different dialogue sequences from it. It can also create and set persistent flags.
 - Can customize text speed, blip sound, blip speed, and various other things using each of the available DialogueActions.

## Weaknesses / Anti-Patterns
 - Easy to add a new `DialogueAction` data class, but it needs to be implemented in a second place.
 - Singleton Coupling: `DialogueManager` talks to `SoundManager`, which can cause errors if it is instantiated into the scene tree incorrectly.
   - It also makes it impossible to test `DialogueManager` without `SoundManager`.
 - Hardcoded Scene Path: Singletons cannot use exported variables, so I must use a hardcoded scene path for the `DialogueBox`.

## Why It Lands Here
 - I gave it the `Good` category because it follows good design principles, and because it is very easy to use.
 - It only uses public functions for external nodes to call, making it easy to understand and requiring no knowledge of how the underlying code works.
 - The designer (also me) can just fill in the fields in various `DialogueAction`s to "program" the `DialogueSequence`, and in code, all that is needed is to call `PlaySequence()`.
   - If the designer wants, they can use `Advance()` or `Close()` for special cases, but these generally aren't needed, since Close is a `DialogueAction` and the system auto-advances when proceeding through the `DialogueSequence`.
 - However, the `Good` category is likely due to bias toward its functionality, while its architectural structure likely lands closer to `Decent`. That is why I consider it a middle ground between the two.

## What I'd Do Differently
 - I would design it so the `DialogueAction`s have the functionality themselves, rather than requiring functionality written in separate locations.

## Tags
`singleton`, `data-driven`, `flag-based-branching`, `resource-based`
