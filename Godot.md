# Godot Concepts

Godot has a handful of engine-level concepts that show up throughout *Mendacia's* code. If you come from a non-Godot background (or Unity), some architectural choices here will look unusual unless you know these first.

## Scene Tree, Node Ownership, and Resources
Mostly everything in Godot is either a `Node` in a tree or a Data-Oriented Object called a `Resource`.
- Node: lifetime/ownership is tied to that scene tree — a node is freed when its parent is freed, and `_EnterTree`/`_Ready`/`_ExitTree` are lifecycle hooks tied to tree membership, not construction/destruction. Code frequently instantiates children at runtime (`AddChild`) rather than wiring dependencies through constructors, because constructors run before a node is in the tree and many engine APIs aren't safe to call yet.
  - Godot Provided Nodes: CollisionShape2D, Area2D, Sprite2D, Node, Node2D, Control, Label and many, many more. 
- Resource: Data-Oriented Objects that contain data. They do not have `_EnterTree`/`_Ready`/`_ExitTree`/`_Process`/`_PhysicsProcess` to run anything at runtime. They are designed to be data objects for nodes to call upon at runtime. `Resource` subclasses can be saved as `.tres` files and edited directly in the Godot editor's inspector, without code. 
  - Godot Provided Resources: Shape2D (the data a `CollisionShape2D` or `Area2D` node uses), Texture2D, Script, Shader, TileSet and many more.

## PackedScene / Scene Instancing
 - A `.tscn` is a file type that contains the data needed to construct a scene tree. It contains Nodes and Resources.
 - A `.tscn` scene can be loaded (`GD.Load<PackedScene>`) and instantiated multiple times at runtime (`Instantiate<T>()`), similar to a prefab in Unity. `DialogueManager` loads and instances the `DialogueBox` scene this way rather than having it pre-placed in a scene — this is how Godot does "spawn a UI/entity from a template" when the thing isn't known until runtime.

## `_Process` vs `_PhysicsProcess`
Godot splits per-frame logic into two callbacks: `_Process` runs once per rendered frame (variable delta), `_PhysicsProcess` runs at a fixed timestep.
- Code that must stay in sync with physics (movement, anything gated on collision) or needs deterministic timing belongs in `_PhysicsProcess`; everything else (animation, UI polling, text scrolling) goes in `_Process`.
- Mixing the two carelessly is a common source of one-frame-timing bugs — e.g. `DialogueManager` explicitly defers `NextAction()` to a physics tick in some paths via `scheduleNextActionOnPhysicsTick` to avoid a same-frame ordering issue.

## `_EnterTree` vs `_Ready`
Both fire once when a node enters the scene tree, but at different points, and the difference matters for anything ordering-sensitive.
- `_EnterTree` runs top-down, the instant a node is added to the tree — a parent's `_EnterTree` runs before its children's, and at this point sibling/child nodes may not exist yet or may not have entered the tree themselves.
- `_Ready` runs bottom-up, after the *entire* subtree under a node (all children, grandchildren, etc.) has already entered the tree and had its own `_Ready` called — so by the time a node's `_Ready` runs, it's safe to assume its children exist and are initialized.
- This is why `DialogueManager` assigns `instance` in `_EnterTree` rather than `_Ready`: other autoloads/nodes may call into it as soon as it exists, and `_EnterTree` guarantees `instance` is set as early as possible, before anything else's `_Ready` runs. Code that instead needs to safely reference children (e.g. `dialogueBox`) waits for `_Ready`.

## Autoloads (Singletons)
Autoloads are scripts/scenes registered in Project Settings that Godot instantiates once and keeps alive in the scene tree for the entire game, accessible globally without an explicit reference. `DialogueManager`, `SoundManager`, and `GameManager`, etc. are all used this way — instantiated once, then reached via a static `instance` field set in the singleton's `_EnterTree` or `_Ready`.
- This is Godot's *sanctioned* way to get global state/services, unlike C# where a bare static singleton is usually a smell. It's baked into the engine's project structure.
- The tradeoff is the same as any singleton: hidden dependencies, harder testing, and load-order coupling (an autoload can't safely assume another autoload has finished `_Ready` yet).
  - It is best to avoid singleton coupling to avoid this problem, but you will see a decent amount of it in this repository. (I am learning from this problem for sure)

## Signals vs. C# Events
- Godot's native pub/sub mechanism is signals (`[Signal]`, `EmitSignal`, connected via `+=` on a `SignalName`), which are the idiomatic way to decouple nodes without direct references and are visible/connectable from the editor.
- In C#, it's easy to reach for plain C# `event Action` instead — that works, but loses editor-visible wiring and Godot's built-in deferred-call/thread-safety handling. Knowing which one a given system uses (and why) matters when judging coupling.

## `[Export]`
- The `[Export]` attribute exposes a field or property to the Godot editor's Inspector, letting a value be set per-instance (or per-scene) without touching code — e.g. a `float` speed, a `PackedScene` reference, or a `Resource` like a `DialogueSequence`.
- This is the main way data flows from designer-facing editor tools into a node, and it's why some values that look like they should be constants are instead editor-assigned fields: they're meant to be tuned/authored outside of code, per node instance, rather than hardcoded once for the whole class.

## CanvasLayer
- UI meant to render independent of the game world's camera/transform (HUD, dialogue box) is parented under a `CanvasLayer`, which draws in its own layer above/below normal scene content regardless of camera movement or z-ordering of 2D/3D world nodes. `DialogueManager` creates one specifically to host the dialogue box.

## Godot Collections vs C# Generics
- Godot's C# bindings wrap engine-native containers (`Godot.Collections.Array`, `Godot.Collections.Dictionary`) that are distinct from `System.Collections.Generic` equivalents — they're needed when marshalling to/from the engine (e.g. exported properties, signals) but are slower and less ergonomic for pure C# logic.
- Code often uses both in the same file (e.g. `Array<Button>` for engine-facing state, `Dictionary<string,int>` for internal logic) — that's not inconsistency, it's picking the right container for whether the engine needs to see it.

