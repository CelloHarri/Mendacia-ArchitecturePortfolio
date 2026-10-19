# Mendacia: A Design and Architecture Retrospective
A code-level case study examining the software architecture decisions behind *Mendacia*, a turn-based tactics game built in Godot 4 / C#, with an honest look at what works, what doesn't, and what I'd do differently.

## Disclaimers:
 - This is not all of the game's source code and other associated files.
 - *Mendacia* is in early development.
 - AI assistance: many parts of this project has AI Assistance, primarily used for my learning as I like to understand why the code works, how it works, and why it isn't working. A lot of the code AI generates for my game does/did not work as I want or need, so most AI-written code was rewritten by me. Some self-contained functions may have been written by AI, since all I needed from them was input and output with no side effects. The architectural judgments in each `Analysis.md` are mine. AI was used to help structure and proofread them.

## Purpose:
This is a technical analysis of *Mendacia's* many systems. I am using this to demonstrate how I evaluate and possibly improve my architectural decisions for maintainable and scalable software.

## Layout
 - This repository is organized by level of architectural quality: `Poor`, `Decent`, and `Good` folders each contain systems demonstrating varying degrees of organization and coherence. Every system is paired with an `Analysis.md` describing my thought process, the system's role in the broader game, and why it lands at that quality level.
 - Every `Analysis.md` follows [`Analysis-Template.md`](Analysis-Template.md), so entries are comparable across folders.
 - I recommend reading `Godot.md` first to understand some Godot-specific concepts that the code doesn't explicitly demonstrate.

## A Note on `Poor`
Landing in `Poor` describes the architecture, not necessarily my current opinion of the code. Some of these scripts still ship in *Mendacia* as-is because they work, are low-risk, and aren't worth the refactor time right now (or possibly ever). Each `Poor` entry's `Analysis.md` includes a **Status** field calling out whether it's still active, planned for rework, or intentionally left alone — see the template for details.

## Game Premise
*Mendacia* has a turn-based tactics combat system mixed with a bullet-hell enemy attack system deriving from Undertale. A majority of *Mendacia's* systems come from this combat system, so those will be most of the systems analyzed in the Repository.

## Tech Stack
- **Godot 4** — open-source engine using a scene/node composition model. Compared to Unity, Godot provides a leaner core and expects more systems to be built from scratch rather than pulled from built-in packages.
- **C#** over GDScript — chosen for transferability to broader software engineering roles, familiarity from a C++ background, and access to the full .NET standard library.
  - GDScript is a custom Python-like language built just for Godot. It is better for device compatibility with mobile/web games, but worse in code features due to the lack of a standard library.
