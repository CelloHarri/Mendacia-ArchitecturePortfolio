# Mendacia: A Design and Architecture Retrospective
A code-level case study examining the software architecture decisions behind *Mendacia*, a turn-based tactics game built in Godot 4 / C#, with an honest look at what works, what doesn't, and what I'd do differently.

## Disclaimers:
 - This is not the games source code
 - *Mendacia* is in early development

## Purpose:
This is a technical analysis of *Mendacia's* many systems. I am using this to demonstrate how I evaluate and improve my architectural decisions for maintainable and scalable software.

## Game Premise
*Mendacia* has a turn-based tactics combat system mixed with a bullet-hell enemy attack system deriving from Undertale. A majority of *Mendacia's* systems come from this combat system, so those will be some of the systems analyzed in the Repository.

## Tech Stack
- **Godot 4** — open-source engine using a scene/node composition model. Compared to Unity, Godot provides a leaner core and expects more systems to be built from scratch rather than pulled from built-in packages or an asset marketplace.
- **C#** over GDScript — chosen for transferability to broader software engineering roles, familiarity from a C++ background, and access to the full .NET standard library.
