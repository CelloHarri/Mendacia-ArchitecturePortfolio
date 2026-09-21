# Dialogue System Analysis

---

## Files Covered
- DialogueAction.cs, DialogueChoiceOption.cs, DialogueManager.cs, DialogueSequence.cs, DialogueActions/*, DialogueBox.cs
- (Tool For Previewing Dialogue in DialoguePreviewer.tscn)

## Role in the System
This code's role in Mendacia is for dialogue. It handles all the different sequences of dialogue that can occur with each DialogueAction. Designed to be easily iterated upon with Resource Based Data Orientation.

## Status
**Active - no planned changes**
 - This is in active use in the game
 - It is currently fully functional and relatively easy to make content with while being easy to add new DialogueActions if needed.

## Strengths
 - Easy to design dialogue sequences with little friction. 
 - Easy to add new dialogueActions if needed
 - Flag Based branching System to check for temporary or persistant save flags to for branching dialogue.
   - Temporary Flags are for non-perminanent flags that allow for branching dialogue within a single dialogue sequence. For instance, when a character asks a question, it will set a temporary flag to a specific value, and depending on that value, the dialogue box will show different information
   - Persistant flags can be anything that has occured in the game that saved a flag. The Dialogue System can check that flags value and branch to different dialogue sequences from it. It can also create and set persistent flags as well.
 - Can customize text speed, blip sound, blip speed, and other various things using each of the DialogueActions available.

## Weaknesses / Anti-Patterns
 - Biggest Weakness: Requires the Creation of a new DialogueAction to have a coincided definition in DialogueManager rather than just defining the Dialogue Action.
 - Singleton Coupling: DialogueManager.cs Talks to SoundManager.cs which if instantiated into the scene tree incorrectly can lead to errors. 
   - It also makes it impossible to test the DialogueManager without SoundManager.
 - Hardcoded Scene Path: Singletons cannot use exported variables, so I must use a hardcoded scene path for the dialoguebox.

## Why It Lands Here
 - I gave it the Good Category because it follows good design principles. Only using public functions for external nodes to call making it easy to understand and requires no understanding of how the underlying code works. The Designer (also me) can just fill in the fields in various DialogueActions to "program" the dialogueSequence and in code, all that is needed is to PlaySequence(). If the designer wants, they can use Advance() or Close() for special cases, but aren't generally needed as Close is a DialogueAction, and it auto advances on proceeding the dialogue Sequence.

## What I'd Do Differently
 - I would design it so the DialogueActions have the functionality themselves, rather than requiring functionality written in seperate locations.

## Tags
`singleton`, `data-driven`, `flag-based-branching`, `resource-based`
