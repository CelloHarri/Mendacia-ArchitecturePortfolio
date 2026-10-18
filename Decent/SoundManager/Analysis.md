# SoundManager Analysis

---

## Files Covered
- [SoundManager.cs](SoundManager.cs)

## Role in the System
Handles the playing of SFX and music. It is built like an API that other files can call. It is an autoload, instantiated right after `GameManager`, so anything can call it.

## Status
**Active - no planned changes**
 - In use in the game and working as intended.

## Strengths
 - Small, clear API.
 - Sounds are loaded by scanning folders, so adding a sound means dropping in a file.
 - Separate audio buses for music and SFX.
 - Good error messages when a name isn't found.
 - Follows some good encapsulation methods.

## Weaknesses / Anti-Patterns
 - String-based lookups.
 - Hardcoded folder paths.
 - Singleton coupling. It is kind of intended to be an API anything can call, which is why it is instantiated right after `GameManager`.

## Why It Lands Here
 - It lands in `Decent` because its main issues are the string-based lookups and the singleton coupling, while its encapsulation and small API are solid.
 - The coupling is partly deliberate, since it is meant to be callable from anywhere.

## What I'd Do Differently
 - Keep the audio settings inside `SoundManager` instead of in `GameManager`.
 - Possibly use a resource for sounds.
 - I would keep the string lookups. They are easy to use, I don't have to write any code, and I don't run into ID issues of knowing which ID corresponds to which sound.

## Tags
`singleton`, `api`, `audio`, `string-lookup`
