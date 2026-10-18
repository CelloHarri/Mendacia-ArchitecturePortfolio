# ControllerStateMachine Analysis

---

## Files Covered
- [Character.cs](Character.cs)
- [ControllerBaseClass.cs](ControllerBaseClass.cs)
- [WorldController.cs](WorldController.cs)
- [TileCombatController.cs](TileCombatController.cs)
- [TileCombatPlayerController.cs](TileCombatPlayerController.cs)
- [PlayerCombatStates/](PlayerCombatStates): [PlayerCombatState.cs](PlayerCombatStates/PlayerCombatState.cs) (abstract base), plus [Move](PlayerCombatStates/PlayerCombatMoveState.cs), [Attack](PlayerCombatStates/PlayerCombatAttackState.cs), [Act](PlayerCombatStates/PlayerCombatActState.cs), [Magic](PlayerCombatStates/PlayerCombatMagicState.cs), [Item](PlayerCombatStates/PlayerCombatItemState.cs), [Defend](PlayerCombatStates/PlayerCombatDefendState.cs), and [End](PlayerCombatStates/PlayerCombatEndState.cs) states
- [EnemyAI/](EnemyAI) (the enemy's combat controller):
  - [TileCombatAIStateMachine.cs](EnemyAI/TileCombatAIStateMachine.cs)
  - [States/TileCombatAIState.cs](EnemyAI/States/TileCombatAIState.cs) (abstract base), plus [Evaluate](EnemyAI/States/EvaluateState.cs), [Move](EnemyAI/States/MoveState.cs), [Attack](EnemyAI/States/AttackState.cs), [Heal](EnemyAI/States/HealState.cs), [PostAttackMove](EnemyAI/States/PostAttackMoveState.cs), and [EndTurn](EnemyAI/States/EndTurnState.cs) states
  - [TacticalScorer/TacticalScorer.cs](EnemyAI/TacticalScorer/TacticalScorer.cs) and [TacticalScorer/ActionCandidate.cs](EnemyAI/TacticalScorer/ActionCandidate.cs)
  - [EnemyAIProfile.cs](EnemyAI/EnemyAIProfile.cs) (a `Resource` of tunable weights)

## Role in the System
Lets a `Character` switch between tile-based combat movement and free-roam world movement (WASD/Arrows) seamlessly and in an organized way. Each movement mode is a controller node (a child of `Character`) that acts as one state of a finite state machine. `BattleManager` triggers the transitions when a battle starts or ends. `ControllerState` also declares `Cutscene` and `Dialogue` states. Those controllers are planned but have not been made yet.

There are two layers of state machine:
 - `Character` switches between controllers (World and TileCombat).
 - Inside a combat turn, a `TileCombatController` subclass runs its own state machine, one state per phase of the turn. There are two implementations, which `BattleManager` drives polymorphically through `StartTurn`, `ProcessTurn`, and `EndTurn`:
   - Player: `TileCombatPlayerController` switches between `PlayerCombatState` children (Move, Attack, Act, Magic, Item, Defend, End). The states read player input.
   - Enemy: `TileCombatAIStateMachine` switches between `TileCombatAIState` children. Each turn runs `EvaluateState`, then `MoveState`, then `AttackState` or `HealState`, then `PostAttackMoveState`, then `EndTurnState`.

Each state is a node, and the controller finds its state children in `_Ready`.

### How the enemy decides
`EvaluateState` builds a list of `ActionCandidate`s: every reachable cell, combined with every attack or heal that could be made from it (or a plain reposition if nothing can be hit). `TacticalScorer` gives each candidate a score using the weights in the enemy's `EnemyAIProfile`. These cover target HP and defense, area-of-effect hit count, sticking to the last target, preferred range, exposure to nearby players, clustering with allies, and fleeing toward a healer when hurt. `PickWithRandomness` then adds bounded random noise to each score before choosing the best, so enemies are not perfectly predictable. Since the profile is a `Resource`, each enemy's personality is tuned in the editor with no code.

## Status
**Active - no planned changes**
 - In use in the game and working as intended.

## Strengths
 - The finite state machine is organized and helpful. I learned FSMs and diagrammed this one before coding it.
 - Easy to iterate on. Adding a new state is easy.
 - Different characters (player or enemy) can define different movement and turn behavior.
 - The player's turn is split into small states (Move, Attack, Act, Magic, Item, Defend, End), so each part of a turn has a clear place to live.
 - Enemy AI is data-driven. Weights live in an `EnemyAIProfile` resource, so new enemy behaviors are made by tuning values in the inspector.
 - Enemy decisions are separated from enemy actions: `EvaluateState` and the static `TacticalScorer` decide, and the other states only carry out the chosen `ActionCandidate`.

## Weaknesses / Anti-Patterns
 - `Character` is a state machine that also holds a state machine. `Character` transitions between controllers, and the controllers have states to transition between different actions (specifically `TileCombatController`).
 - `Character` is saturated with responsibilities.
 - BattleManager Coupling: the combat controllers, player and enemy, depend directly on `BattleManager`.
 - Too many public variables across the files.
 - Duplicated code in the player combat states: Attack, Act, Magic, and Item each re-implement similar hover, facing, and confirm logic, and the shared base `PlayerCombatState` carries lots of input-handling fields.
 - States are switched to by string name (`ChangeCombatState("PlayerCombatMoveState")`, `ChangeAIState("EndTurnState")`), so a typo fails at runtime instead of compile time.
 - `EvaluateState` duplicates its attack and heal candidate-building code, and helpers like the recursive `BattleBox` search are re-written in both the AI and player states instead of shared.
 - The AI profile has many weights, and the scoring contains magic numbers (such as dividing by 10 or assuming 6 as the maximum area-of-effect targets).

## Why It Lands Here
 - As a single-person project, the `BattleManager` god object is understandable to me, so depending on it everywhere is not the biggest issue.
 - The state machine itself is well-organized and easy to extend, and I designed it deliberately rather than by accident.
 - The player and enemy controllers share the same base class and the same pattern, so a new kind of combat controller is mostly a matter of adding states.

## What I'd Do Differently
 - Reduce how much `Character` does, likely by moving work into more helper classes.
 - Reduce the number of public variables in everything.
 - Pull the shared hover/facing/confirm logic of the Attack, Act, Magic, and Item states into the base class or a helper, and replace the string state names with an enum or constants.
 - Share the attack and heal candidate generation in `EvaluateState`, and name the scoring constants.

## Tags
`state-machine`, `fsm`, `composition`, `polymorphism`, `tight-coupling`, `utility-ai`, `data-driven`

