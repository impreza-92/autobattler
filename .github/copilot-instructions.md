# Copilot Instructions for Autobattle (Godot 4.6 C#)

## 1. Godot 4.6 C# Conventions
- Use C# for all scripting. Prefer Godot's C# API patterns: signals (`+=` syntax), `[Export]` for inspector properties, and node lifecycle methods (`_Ready()`, `_Process()`, etc.).
- Use `GetNode<T>("path")` for node access. Prefer strong typing.
- Use `CallDeferred` for safe scene tree changes during callbacks.
- Be aware of thread safety: only interact with nodes on the main thread.
- Scene instantiation: use `PackedScene` and `Instance()` for dynamic nodes.

## 2. Engine/UI Separation
- **All game logic** (battle simulation, gambit evaluation, combat math, data models) must be implemented in **pure C#** under `Core/` with **no Godot dependencies** (no `using Godot;`).
- **Godot scenes and scripts** in `Scenes/` handle only UI, rendering, user input, and timing. They interact with the engine via state and event lists, never by calling engine internals directly.

## 3. Data-Driven & Registry Patterns
- Use **data registries** (lookup maps keyed by ID) for abilities, archetypes, conditions, actions, and status effects. Reference by ID, not by direct object reference.
- Adding new content (abilities, archetypes, conditions) should require only new data entries, not new logic.

## 4. Immutability & Pure Functions
- The battle simulation is **deterministic**: each tick is a pure function (`Tick(state) → (newState, events)`), producing a new state and event list.
- All state changes are tracked; support for replay, undo, and step-through debugging is required.

## 5. UI/Engine Communication
- The engine emits **event lists** (e.g., `BattleEvent`) for the UI to observe and react to (animations, logs, stat updates).
- The UI **never** calls into engine internals; it only reads state and dispatches user actions.

## 6. Coding Standards & Best Practices
- **Stateless, functional style** for all simulation logic.
- **No side effects** in engine functions; all outputs must be explicit.
- **All references** between systems (e.g., gambit to ability) are by string ID.
- **Archetypes and abilities** are always derived from current stats, never stored directly.
- **Invalid gambits** (due to stat changes) are visually marked and skipped, not auto-deleted.

## 7. Patterns by System
- **Battle simulation**: Finite State Machine + fixed-timestep game loop.
- **Gambit evaluation**: Chain of Responsibility + Strategy pattern.
- **Targeting**: Filter chain + sort pipeline.
- **Damage calculation**: Pipeline of modifiers, each a pure function.
- **Persistence**: Memento pattern (serialize/deserialize to JSON).

## 8. UI Implementation Notes
- Use Godot Control nodes for UI (stat sliders, dropdowns, log, etc.).
- All UI state is derived reactively from the engine state.
- Visual feedback (damage numbers, hit flashes, status icons) is handled in Godot, triggered by engine events.

## 9. Extensibility
- All systems must be designed for easy extension (new archetypes, abilities, status effects, etc.) via data, not code changes.
- The engine must remain framework-agnostic and portable.

## 10. File/Folder Conventions
- **Core/Models/**: Data models (Unit, Team, Gambit, Ability, StatusEffect, BattleState, etc.).
- **Core/Data/**: Registries for archetypes, abilities, conditions, status effects, presets.
- **Core/Engine/**: Simulation logic (BattleEngine, GambitEvaluator, CombatResolver, etc.).
- **Scenes/**: Godot scenes and scripts for UI and visualization.
- **docs/Godot/**: Curated Godot XML API reference files for agent use.

## 11. Reference to Godot XML Docs
- For Godot API usage, consult the XML files in `./docs/Godot` for authoritative reference on node types, signals, properties, and methods relevant to this project.

## 12. Common Pitfalls
- Do not mix Godot and pure C# logic. Never reference Godot types in `Core/`.
- Avoid direct manipulation of engine state from UI scripts.
- Use event-driven updates for UI; never poll engine state in a loop.

---
These instructions are mandatory for all Copilot agents and contributors working on this project. Adhere strictly to the architecture, separation of concerns, and coding standards described above.
