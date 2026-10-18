# Analysis Template

Copy this into a folder's `Analysis.md` and fill in each section. Delete any section that genuinely doesn't apply, but default to keeping all of them.

---

## Files Covered
- List the file(s)/scene(s) this analysis discusses. Link relative to this folder.

## Role in the System
What does this code do, and where does it sit in *Mendacia's* architecture? Who calls it, what does it depend on?

## Status
Pick one and briefly justify it. This matters most in `Poor/`, where "bad architecture" and "worth touching" are independent axes.
- **Active - no planned changes**: Works, ships, not worth the refactor risk/time right now.
- **Active - planned feature work**: Works and ships, but new functionality (not a refactor, since a refactor keeps the same behavior) is planned.
- **Active - refactor planned**: Known issues, on the list to fix.
- **Legacy / kept as-is**: Architecturally rough, but stable, low-traffic, or low-risk enough that rewriting it has negative ROI. Explain why it's fine to leave alone.
- **Superseded**: Replaced by a better pattern elsewhere; kept in the repo only as a reference point.

## Strengths
What this code gets right, even if the overall grade is "Poor". Nothing is 100% bad — call out the decent parts.

## Weaknesses / Anti-Patterns
Specific problems: tight coupling, God-object tendencies, magic values, missing abstraction, poor testability, etc. Name the anti-pattern where one applies (e.g. Singleton overuse, feature envy, primitive obsession).

## Why It Lands Here
Tie the strengths/weaknesses back to the `Good` / `Decent` / `Poor` label. Be explicit about the tradeoffs that were made and whether they were deliberate or accidental.

## What I'd Do Differently
Concrete refactor ideas or the pattern you'd reach for with hindsight. If it's in `Poor` and marked **Legacy / kept as-is**, this section can just describe the ideal design without committing to doing it.

## Tags
Optional short list of keywords for skimming across the repo, e.g. `singleton`, `state-machine`, `data-driven`, `tight-coupling`.
