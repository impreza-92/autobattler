# Architecture Guide — Design Patterns & Data Model

This document describes the recommended design patterns and data structures for the autobattler's gameplay systems. It is **framework-agnostic** — the patterns apply regardless of whether the UI is built with Svelte, React, Vue, or vanilla JS. The goal is to make the codebase data-driven, extensible, and easy to reason about.

---

## 1. Guiding Principles

| Principle | What It Means |
|-----------|---------------|
| **Data-driven** | Game behavior is defined by data (JSON-like definitions), not hardcoded logic. Adding a new ability or archetype should require adding a data entry, not writing new functions. |
| **Engine/UI separation** | All game logic (battle simulation, gambit evaluation, damage math) lives in pure functions with no UI dependencies. The UI reads state and dispatches user actions — nothing more. |
| **Immutable battle state** | During simulation, each tick produces a new state snapshot (or at minimum, state changes are tracked). This enables replay, step-through, and undo. |
| **Registry pattern everywhere** | Abilities, archetypes, conditions, and actions are all stored in registries (lookup maps keyed by ID). Systems reference things by ID, never by direct object reference. |

---

## 2. Design Patterns by System

### 2.1 Battle Simulation — State Machine + Game Loop

**Pattern: Finite State Machine (FSM)**

The battle has distinct phases with clear transitions:

```
[Setup] → [Running] → [Ended]
              ↕
          [Paused]
```

| State | Description |
|-------|-------------|
| `setup` | Battle is being initialized from team data. Units are placed, gauges reset to 0. |
| `running` | Tick loop is active. Gauges fill, actions execute. |
| `paused` | Tick loop is suspended. State is frozen. Can resume or step. |
| `ended` | Win condition met. Final state is preserved for display. |

The state machine ensures that tick processing only happens in the `running` state, UI controls trigger transitions (`pause → running`, `running → paused`), and the `ended` state is terminal (only a restart can leave it).

**Pattern: Game Loop (fixed timestep)**

The battle runs on a fixed-timestep loop, not frame-based animation. Each "tick" advances the simulation by one discrete step. This makes the simulation **deterministic** — the same inputs always produce the same outputs, regardless of frame rate.

```
function tick(state: BattleState): { newState: BattleState, events: BattleEvent[] }
```

The tick function is a **pure function**: it takes the current state and returns the next state plus a list of events that occurred. The UI subscribes to events for display purposes (animations, log entries) but does not influence the simulation.

**Why this pattern:** Determinism enables replay, step-through debugging, and eventually networked play (only inputs need to be synced). Separating events from state means the UI can animate at whatever speed it wants without affecting the simulation.

---

### 2.2 Gambit System — Chain of Responsibility + Strategy

**Pattern: Chain of Responsibility**

A unit's gambit list is a classic chain of responsibility. Each gambit slot is a link in the chain. When the unit's gauge is full:

1. The first gambit checks its condition.
2. If the condition is met → execute the action. Chain stops.
3. If not → pass to the next gambit in the list.
4. If no gambit matches → the unit idles (does nothing).

The chain is ordered by player-defined priority. Each link is independent and self-contained — it doesn't know about other links.

**Pattern: Strategy (for conditions and actions)**

Each **condition type** is a strategy — a function with a common interface that evaluates differently based on its type:

```
interface ConditionStrategy {
    evaluate(unit: Unit, allies: Unit[], enemies: Unit[]): Target | null
}
```

Each **action type** is also a strategy:

```
interface ActionStrategy {
    execute(source: Unit, target: Target, state: BattleState): ActionResult
}
```

Conditions and actions are looked up by ID from their respective registries. A gambit slot simply holds a condition ID + an action ID. The evaluation engine resolves these IDs to strategies at runtime.

**Why this pattern:** New conditions and actions can be added by registering a new strategy function — no changes to the evaluation loop. The gambit editor UI just needs to know the IDs and display names, not the implementation details.

---

### 2.3 Archetype System — Registry + Derived State

**Pattern: Registry (catalog)**

All archetypes are entries in a registry. Each entry defines:
- A unique ID
- A display name
- The stat thresholds required to unlock it
- A list of ability IDs it grants

```
archetypeRegistry: Map<ArchetypeId, ArchetypeDefinition>
```

**Pattern: Derived/Computed State**

A unit's unlocked archetypes and available abilities are **never stored directly**. They are always derived (computed) from the unit's current stats:

```
function getUnlockedArchetypes(stats: Stats): ArchetypeId[]
function getAvailableAbilities(stats: Stats): AbilityId[]
```

This means when the player adjusts a stat slider, the UI simply re-derives the archetype list. There is no sync problem — the stats are the single source of truth.

**Why this pattern:** Adding a new archetype is just a new registry entry. The derivation function loops over all registry entries and checks thresholds — it doesn't need to know about specific archetypes. This is critical for extensibility when moving from prototype to full game (7 archetypes → 20+).

---

### 2.4 Combat Math — Strategy + Pipeline

**Pattern: Strategy (for damage types)**

Different damage types (physical, magical, healing) use different formulas. Each is a strategy:

```
interface DamageCalculator {
    calculate(source: Unit, target: Unit, ability: Ability): number
}
```

For the prototype there are only two (physical and magical), but the pattern scales to elemental damage, hybrid damage, true damage, etc. in the full game.

**Pattern: Pipeline (for damage modifiers)**

Damage calculation flows through a pipeline of modifiers:

```
Raw damage → apply ability power multiplier → apply attack stat
    → subtract defense → apply buffs/debuffs → apply minimum (1)
    → final damage
```

Each step in the pipeline is a function that takes the current damage value and returns the modified value. Steps can be added or removed without changing the others. This is where buffs like Shield Wall (50% damage reduction) or Defend are applied.

**Why this pattern:** The full game will have many more modifiers (elemental weakness, equipment effects, critical hits, Luck-based procs). A pipeline makes it easy to insert/remove modifiers without tangling the base formula.

---

### 2.5 Targeting — Filter + Sort

**Pattern: Filter chain + Sort**

Target resolution (deciding which unit a gambit targets) follows a filter-then-sort approach:

```
All units → filter by side (ally/enemy)
         → filter by alive
         → filter by protection rules (if applicable)
         → filter by condition criteria (e.g., HP below threshold)
         → sort by priority (e.g., lowest HP first)
         → take first
```

Each filter is a predicate function. The condition strategy composes filters to produce the final target. Protection rules are one specific filter in the chain — abilities with `ignoresProtection: true` simply skip that filter.

**Why this pattern:** Clean separation of targeting concerns. Adding new conditions (e.g., "Enemy: Highest Mag stat") means adding one new filter function while the pipeline stays the same.

---

### 2.6 Battle Events — Observer / Event Bus

**Pattern: Observer (publish/subscribe)**

The battle engine emits **events** as things happen. The UI subscribes to events and renders accordingly. The engine never calls UI code directly.

Event types:
- `gauge_filled` — a unit's gauge reached 1.0
- `action_executed` — a unit performed an action (includes source, target, ability, result)
- `damage_dealt` — HP was reduced (includes amount, target, source)
- `healing_done` — HP was restored
- `unit_defeated` — a unit's HP hit 0
- `buff_applied` / `buff_expired` — a buff started or ended
- `battle_ended` — a win condition was met

The battle log is simply an observer that appends a text description for each event. The animation system is another observer that triggers visual effects. They don't know about each other.

**Why this pattern:** Multiple independent UI systems (log, animations, stat summary, sound effects in the future) can all react to the same events without coupling. The engine stays pure and testable.

---

### 2.7 Team Editor — Constrained State + Reactive Derivation

**Pattern: Constrained State**

The stat allocation system is a constrained state problem: 6 sliders that must always sum to ≤ 50. There are two viable approaches:

**Approach A — Clamp on change (simpler):**
When the player drags a slider, calculate the new total. If it exceeds 50, clamp the changed slider so the total is exactly 50. The UI displays the "remaining points" counter.

**Approach B — Proportional redistribution (smoother):**
When the player increases one stat, proportionally decrease the others. This is more complex but feels better interactively.

Recommendation: **Approach A** for the prototype. Simpler to implement and debug.

**Pattern: Reactive Derivation**

The editor UI forms a **derivation chain**:

```
Stat sliders (user input)
    → Stat values (constrained state)
        → Unlocked archetypes (derived)
            → Available abilities (derived)
                → Valid gambit actions (filtered dropdown)
```

Every level recomputes when the level above changes. This is where a reactive framework (Svelte, Vue, React) shines — these derivations can be expressed as computed/derived values and the framework handles re-rendering.

**Critical edge case:** If the player changes stats such that an archetype is *lost*, any gambits referencing abilities from that archetype become **invalid**. The UI should:
1. Visually mark invalid gambits (red highlight / warning icon).
2. Keep the gambit in place (don't auto-delete — the player might re-adjust stats).
3. During battle, invalid gambits are skipped as if their condition failed.

---

### 2.8 State Persistence — Serialization / Memento

**Pattern: Memento (serialize/deserialize)**

Team configurations and battle state should be serializable to plain JSON. This enables:
- **Save/load** to localStorage
- **Export/import** for sharing builds
- **Presets** defined as static JSON
- **Battle replay** by serializing the initial state + sequence of events (or just initial state, since simulation is deterministic)

All data objects should use plain IDs (strings) for references rather than object references. This makes serialization trivial — no circular reference issues.

```
SerializedUnit = {
    name: string,
    position: number,
    stats: { hp: number, str: number, ... },
    gambits: { conditionId: string, actionId: string, enabled: boolean }[]
}
```

---

## 3. Complete Data Model

### 3.1 Static Data (definitions — loaded once, never mutated)

These are the game's "database" — they define what exists in the game.

#### Ability Definition

```
AbilityDefinition {
    id: string                  // "power_strike", "fire_bolt", "heal", etc.
    name: string                // "Power Strike"
    description: string         // "A heavy physical strike dealing 1.5× Str damage."
    type: DamageType            // "physical" | "magical" | "heal" | "buff" | "debuff"
    targeting: TargetType       // "single_enemy" | "single_ally" | "self" | "all_enemies" | "all_allies"
    power: number               // Multiplier for the base stat (e.g., 1.5)
    scalingStat: StatId         // "str" | "mag" — which stat the power multiplier applies to
    ignoresProtection: boolean  // true for abilities like Backstab
    special: SpecialEffect[]    // Additional effects (e.g., gauge refill, apply buff)
}
```

#### Special Effect (sub-object on abilities)

```
SpecialEffect {
    type: string                // "gauge_refill" | "apply_buff" | "apply_debuff"
    params: object              // { amount: 0.5 } for gauge refill, { buffId: "shield_wall", duration: 3 } for buffs
}
```

#### Archetype Definition

```
ArchetypeDefinition {
    id: string                  // "fighter", "mage", "paladin", etc.
    name: string                // "Fighter"
    description: string         // "Unlocked when Str ≥ 10. Physical combat specialist."
    thresholds: StatThreshold[] // [{ stat: "str", minimum: 10 }]
    abilityIds: string[]        // ["power_strike", "cleave"]
}
```

#### Stat Threshold (sub-object on archetypes)

```
StatThreshold {
    stat: StatId                // "hp" | "str" | "mag" | "def" | "res" | "spd"
    minimum: number             // The minimum stat value required
}
```

#### Gambit Condition Definition

```
ConditionDefinition {
    id: string                  // "enemy_lowest_hp", "ally_hp_below_50", etc.
    name: string                // "Enemy: Lowest HP"
    description: string         // "Targets the living enemy with the lowest current HP."
    targetSide: "enemy" | "ally" | "self"
    evaluate: function          // (unit, allies, enemies) => Target | null
}
```

#### Buff Definition

```
BuffDefinition {
    id: string                  // "shield_wall", "defend", "taunt"
    name: string                // "Shield Wall"
    description: string         // "Reduces incoming damage by 50%."
    duration: number            // Number of actions (not ticks) the buff lasts
    effect: BuffEffect          // What the buff does (see below)
}
```

#### Buff Effect (sub-object on buffs)

```
BuffEffect {
    type: string                // "damage_reduction" | "force_target" | "gauge_modifier"
    params: object              // { factor: 0.5 } for 50% damage reduction
}
```

---

### 3.2 Player Data (configured by the player, persisted)

#### Unit Configuration

```
UnitConfig {
    id: string                  // Unique ID (e.g., UUID or "team1_pos0")
    name: string                // Player-chosen name
    position: number            // 0–6 (formation position)
    stats: {
        hp: number              // 0–50, all six must sum to ≤ 50
        str: number
        mag: number
        def: number
        res: number
        spd: number
    }
    gambits: GambitSlot[]       // Ordered list (index = priority)
}
```

#### Gambit Slot

```
GambitSlot {
    conditionId: string         // References a ConditionDefinition.id
    actionId: string            // References an AbilityDefinition.id (or "attack" / "defend")
    enabled: boolean            // Can be toggled without removing the gambit
}
```

#### Team Configuration

```
TeamConfig {
    name: string                // Player-chosen team name
    units: UnitConfig[]         // Exactly 7 units
}
```

#### Preset Configuration

```
PresetConfig {
    id: string                  // "balanced", "glass_cannon", "turtle"
    name: string                // "Balanced"
    description: string         // "A mix of Fighter, Mage, Guardian, and Scout builds."
    teams: [TeamConfig, TeamConfig]  // Two pre-built teams
}
```

---

### 3.3 Runtime Data (exists only during battle, created from player data)

#### Battle Unit (runtime version of a unit)

```
BattleUnit {
    configId: string            // Reference back to UnitConfig.id
    name: string
    teamIndex: number           // 0 or 1
    position: number            // 0–6

    // --- Computed from stats ---
    maxHp: number               // Derived: 20 + hp * 8
    attackPower: number         // Derived: str * 2
    magicPower: number          // Derived: mag * 2
    defense: number             // Derived from def stat
    resistance: number          // Derived from res stat
    fillRate: number            // Derived: 0.01 + spd * 0.005

    // --- Mutable runtime state ---
    currentHp: number           // Starts at maxHp, decremented by damage
    actionGauge: number         // 0.0 to 1.0+
    isAlive: boolean            // false when currentHp ≤ 0
    activeBuffs: ActiveBuff[]   // Currently active buffs with remaining duration

    // --- Copied from config ---
    gambits: GambitSlot[]       // Copied from UnitConfig, evaluated each action
    unlockedAbilityIds: string[] // Derived from stats at battle start
}
```

#### Active Buff (runtime)

```
ActiveBuff {
    buffId: string              // References BuffDefinition.id
    remainingDuration: number   // Actions remaining (decremented each time this unit acts)
    sourceUnitId: string        // Who applied this buff
}
```

#### Battle State (the complete snapshot of a battle)

```
BattleState {
    phase: "setup" | "running" | "paused" | "ended"
    tick: number                // Current tick count (starts at 0)
    teams: [BattleUnit[], BattleUnit[]]  // Two arrays of 7 units each
    winner: number | null       // null during battle, 0 or 1 when ended
    eventLog: BattleEvent[]     // Complete event history
}
```

#### Battle Event

```
BattleEvent {
    tick: number                // Which tick this event occurred on
    type: EventType             // See event types below
    data: object                // Event-specific payload
}
```

#### Event Types and Payloads

```
// A unit's gauge filled and it is about to act
{ type: "gauge_filled", data: { unitId: string } }

// A unit executed an ability
{ type: "action_executed", data: {
    sourceId: string,
    targetId: string | string[],    // Array for AoE
    abilityId: string,
    result: "hit" | "healed" | "buffed" | "debuffed"
}}

// Damage was dealt
{ type: "damage_dealt", data: {
    sourceId: string,
    targetId: string,
    abilityId: string,
    amount: number,                 // Final damage after all modifiers
    damageType: "physical" | "magical",
    previousHp: number,
    newHp: number
}}

// Healing was done
{ type: "healing_done", data: {
    sourceId: string,
    targetId: string,
    abilityId: string,
    amount: number,
    previousHp: number,
    newHp: number
}}

// A unit was defeated
{ type: "unit_defeated", data: {
    unitId: string,
    killedBy: string                // Source unit ID
}}

// A buff was applied
{ type: "buff_applied", data: {
    sourceId: string,
    targetId: string,
    buffId: string,
    duration: number
}}

// A buff expired
{ type: "buff_expired", data: {
    unitId: string,
    buffId: string
}}

// A gambit was skipped (condition not met or ability invalid)
{ type: "gambit_skipped", data: {
    unitId: string,
    gambitIndex: number,
    reason: "condition_not_met" | "target_protected" | "ability_invalid"
}}

// No gambit matched — unit idles
{ type: "unit_idle", data: {
    unitId: string
}}

// Battle ended
{ type: "battle_ended", data: {
    winnerTeamIndex: number,
    totalTicks: number,
    survivingUnits: string[]
}}
```

---

### 3.4 Data Relationships Diagram

```
                    ┌───────────────────┐
                    │  STATIC DATA      │  (loaded once, read-only)
                    └───────────────────┘

  ┌─────────────┐      ┌──────────────────┐      ┌──────────────────┐
  │ConditionDef │      │ ArchetypeDef     │      │  BuffDefinition  │
  │  .id        │      │  .id             │      │  .id             │
  │  .evaluate()│      │  .thresholds[]──┐│      │  .duration       │
  └──────┬──────┘      │  .abilityIds[]─┐││      │  .effect         │
         │             └────────────────┘││      └────────┬─────────┘
         │                    │          ││               │
         │                    │          │▼               │
         │                    │    ┌─────┴──────────┐    │
         │                    │    │ AbilityDef      │    │
         │                    │    │  .id            │    │
         │                    │    │  .type          │    │
         │                    │    │  .power         │    │
         │                    │    │  .special[]─────┼────┘
         │                    │    └────────┬────────┘
         │                    │             │
         ▼                    ▼             ▼
  ┌───────────────────────────────────────────────┐
  │              PLAYER DATA                       │  (configured, persisted)
  │                                                │
  │  TeamConfig                                    │
  │  ├── name                                      │
  │  └── units[7]: UnitConfig                      │
  │       ├── name, position                       │
  │       ├── stats { hp, str, mag, def, res, spd }│
  │       └── gambits[]: GambitSlot                │
  │            ├── conditionId ──→ ConditionDef.id │
  │            ├── actionId ────→ AbilityDef.id    │
  │            └── enabled                         │
  └──────────────────────┬────────────────────────┘
                         │
                         │  (initialized at battle start)
                         ▼
  ┌───────────────────────────────────────────────┐
  │              RUNTIME DATA                      │  (exists during battle only)
  │                                                │
  │  BattleState                                   │
  │  ├── phase, tick, winner                       │
  │  ├── teams[2]: BattleUnit[]                    │
  │  │    ├── maxHp, currentHp, actionGauge        │
  │  │    ├── isAlive, fillRate                    │
  │  │    ├── activeBuffs[] ──→ BuffDefinition.id  │
  │  │    ├── gambits[] (copied from config)       │
  │  │    └── unlockedAbilityIds[] (derived)       │
  │  └── eventLog[]: BattleEvent                   │
  │       ├── tick, type                           │
  │       └── data (event-specific)                │
  └───────────────────────────────────────────────┘
```

---

## 4. Data Flow Summary

### 4.1 Team Editing Flow

```
User drags stat slider
    → Update stat value (constrained to pool of 50)
    → Recompute unlocked archetypes (lookup archetype registry, check thresholds)
    → Recompute available abilities (collect all ability IDs from unlocked archetypes + universal)
    → Revalidate gambits (mark any gambit whose actionId is no longer in available abilities)
    → Update UI (archetype labels, ability dropdowns, gambit validity indicators)
```

### 4.2 Battle Initialization Flow

```
Player clicks "Start Battle"
    → For each UnitConfig in both teams:
        → Create BattleUnit (compute maxHp, fillRate, etc. from stats)
        → Derive unlockedAbilityIds from stats
        → Copy gambits
        → Set currentHp = maxHp, actionGauge = 0, isAlive = true
    → Create BattleState { phase: "running", tick: 0, teams: [...], eventLog: [] }
    → Start tick loop
```

### 4.3 Battle Tick Flow

```
tick(state) → { newState, events }

    1. For each living unit: actionGauge += fillRate
    2. Collect units where actionGauge ≥ 1.0 (sort by gauge desc, then speed desc)
    3. For each acting unit (in order):
        a. Evaluate gambit chain:
            - For each enabled gambit slot (top to bottom):
                - Resolve condition → get candidate target
                - If no target → emit "gambit_skipped", continue to next slot
                - Check protection rules (is target accessible?)
                - If protected → emit "gambit_skipped", continue
                - Check ability validity (does unit have this ability?)
                - If invalid → emit "gambit_skipped", continue
                - ✓ Valid gambit found → break
            - If no gambit matched → emit "unit_idle", skip to next unit
        b. Execute action:
            - Look up AbilityDefinition by actionId
            - Run damage pipeline: base → multiplier → stat scaling → defense → buffs → min 1
            - Apply result to target (reduce HP / restore HP / apply buff)
            - Emit events: "action_executed", "damage_dealt" or "healing_done"
            - Process special effects (gauge refill, buff application)
        c. Check if target is defeated:
            - If currentHp ≤ 0 → isAlive = false, emit "unit_defeated"
        d. Tick down buffs on this unit:
            - For each activeBuff: remainingDuration -= 1
            - If remainingDuration ≤ 0 → remove buff, emit "buff_expired"
        e. Reset actionGauge = 0
    4. Check win condition:
        - If all units on team 0 are dead → winner = 1, emit "battle_ended"
        - If all units on team 1 are dead → winner = 0, emit "battle_ended"
        - Set phase = "ended"
    5. Increment tick counter
    6. Return new state + collected events
```

### 4.4 Event → UI Flow

```
Battle engine emits events
    → Battle log observer: appends formatted text line
    → Animation observer: triggers visual effect (highlight unit, flash damage number)
    → Stats observer: updates HP bars, gauge bars, alive/dead status
    → End screen observer: shows victory banner when "battle_ended" fires
```

---

## 5. Pattern Quick Reference

| System | Primary Pattern | Secondary Pattern | Key Benefit |
|--------|----------------|-------------------|-------------|
| Battle simulation | State Machine | Game Loop (fixed timestep) | Deterministic, replayable |
| Gambit evaluation | Chain of Responsibility | Strategy (per condition/action) | Player-defined AI priority |
| Targeting | Filter Chain + Sort | — | Composable, extensible |
| Damage calculation | Pipeline | Strategy (per damage type) | Easy to add modifiers |
| Archetype system | Registry | Derived State | Data-driven, extensible |
| Ability system | Registry | — | Add abilities without code changes |
| Battle events | Observer / Event Bus | — | Decouples engine from UI |
| Team editor state | Constrained State | Reactive Derivation | Enforces budget, auto-updates UI |
| Persistence | Memento (serialize) | — | Save/load/export/replay |

---

## 6. Extensibility Notes

The patterns above are chosen specifically because the full game (beyond the prototype) adds significant complexity. Here's how each pattern scales:

| Future Feature | Pattern That Handles It |
|---------------|------------------------|
| Elemental affinities (6 elements) | Add to stat model, add elemental filter to damage pipeline |
| Luck stat (crits, dodges) | Add randomness step to damage pipeline |
| Status effects (poison, blind, etc.) | New buff/debuff definitions in registry, new pipeline steps |
| Equipment system | Stat modifiers layer on top of base stats before derivation |
| Consumable items | New action type in ability registry with a "consumable" flag |
| Multi-team battles (3+ teams) | BattleState.teams becomes N-length array, win condition checks all teams |
| World map integration | Battle engine is a black box: takes two teams in, returns a result. World map just calls it. |
| PvP networking | Deterministic simulation means only initial state needs to be synced |
