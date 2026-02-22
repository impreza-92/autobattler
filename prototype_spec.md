# Prototype Specification — Auto-Battle System

## Goal

Build a **Godot 4.6 prototype** that validates the core auto-battle and gambit system. This prototype focuses exclusively on **team building and automated combat** — no world map, no progression, no environmental manipulation. The player builds two teams, programs their gambits, and watches them fight.

**Tech stack:** Godot 4.6 (.NET) with C#. The battle simulation engine is **pure C#** (no Godot dependencies) — all game logic lives in plain classes under `Core/` and is fully testable without the engine. Godot scenes and nodes handle only the UI/rendering layer, reacting to engine events via signals. Run the prototype from the Godot editor or as an exported build.

---

## Scope Summary

### Included in Prototype

| Feature                         | Notes                                                  |
| ------------------------------- | ------------------------------------------------------ |
| Team editor (2 teams)           | Stat allocation, positioning, gambit editing            |
| 7-unit formation with rows      | Front row (0, 2, 4, 6) and back row (1, 3, 5)         |
| Back row protection rules        | Back row units shielded by adjacent front row units     |
| ATB action gauge                 | Speed-based gauge fill, execute gambit when full        |
| Gambit system (condition→action) | Priority list, evaluate top-to-bottom                  |
| Stat-threshold archetypes        | Stat allocation unlocks archetype abilities             |
| Basic combat math                | Physical/magical damage, defense/resistance             |
| Status effects                   | Shield Wall (damage reduction) and Taunt (forced targeting) function as real buffs/debuffs with duration tracking |
| Battle visualization (2D)        | Sprite-based unit display, HP bars, gauge bars, action log, buff/debuff icons |
| Battle controls                  | Start, pause, speed up (2×), step-through              |
| Visual feedback                  | Hit flashes, damage numbers, spell particles, screen shake |

### Deferred (NOT in Prototype)

| Feature                     | Reason                                                     |
| --------------------------- | ---------------------------------------------------------- |
| World map                   | Separate system — not needed to validate combat            |
| Environmental manipulation  | Depends on world map                                       |
| Capture point / scoring     | Depends on world map                                       |
| Lives system                | Depends on world map match flow                            |
| Multi-team battles (3+)     | Start with 1v1 only                                        |
| Equipment system            | Adds complexity; stat allocation alone is enough to test builds |
| Consumable items            | Can be added later once gambit system is validated          |
| Elemental affinities        | Simplify stats first; add elements in a later iteration    |
| Luck stat                   | Randomness complicates testing; defer to later             |
| Additional status effects   | Poison, blind, haste, slow, etc. — defer until core buff/debuff pipeline is validated with Shield Wall and Taunt |
| Gambit slot unlocks         | All slots available from the start in prototype            |
| Meta-progression            | No persistence needed for prototype                        |
| Shopping / treasure         | Depends on world map / progression                         |
| PvP networking              | Local-only prototype (both teams edited on same screen)    |

---

## Step-by-Step Implementation Plan

### Step 1: Data Model

Define the core data structures in pure C#. No Godot dependencies — these live in `Core/Models/`.

**Unit:**
```
- Id: string
- Name: string
- Position: int (0–6)
- Stats: { Hp, Str, Mag, Def, Res, Spd } (all ints, drawn from a pool of 50)
- CurrentHp: int (starts equal to computed MaxHp)
- ActionGauge: float (0.0 to 1.0)
- Gambits: List<GambitSlot> { Condition, Action, Enabled }
- ActiveEffects: List<ActiveStatusEffect>
- UnlockedArchetypes: derived from stats (computed, not stored)
- AvailableAbilities: derived from UnlockedArchetypes (computed, not stored)
```

**Stats (prototype subset — 6 stats, no elemental affinities or luck):**

| Stat | Abbr | Effect |
| ---- | ---- | ------ |
| Hit Points | HP | Max HP is computed from this: `MaxHp = 20 + Hp * 8`. A unit with 0 HP stat points (not possible due to min 1) still has 28 HP. |
| Strength | Str | Physical damage: `BaseDamage = Str * 2` |
| Magic | Mag | Magical damage/healing: `BaseDamage = Mag * 2` |
| Defense | Def | Physical damage reduction: `FinalDamage = Max(1, damage - Def)` |
| Resistance | Res | Magical damage reduction: `FinalDamage = Max(1, damage - Res)` |
| Speed | Spd | Gauge fill rate per tick: `FillRate = 0.01f + Spd * 0.005f` |

- **Stat point pool:** 50 points per unit. Every stat starts at a **minimum of 1** (cannot go below 1). With 6 stats in the prototype, 6 points are pre-allocated, leaving **44 points freely distributable**.
- Minimum value per stat: 1. Maximum value per stat: 99 (unreachable with base 50 pool, but supports future growth from leveling/treasures).
- The player simply adds or removes points. When the remaining pool hits 0, no more points can be added to any stat. No clamping or proportional redistribution — keep it simple.
- The formulas above are starting points — they will need tuning during playtesting.

**Team:**
```
- Name: string
- Units: List<Unit> (exactly 7)
```

**Gambit Slot:**
```
- ConditionId: string      (e.g., "enemy_lowest_hp")
- ActionId: string         (e.g., "power_strike")
- Enabled: bool
```

**Ability Definition:**
```
- Id: string
- Name: string
- Type: AbilityType         (Physical, Magical, Heal, Buff, Debuff)
- Power: float              (Multiplier for the scaling stat)
- ScalingStat: StatId       (Str or Mag)
- Target: TargetType        (SingleEnemy, SingleAlly, Self, AllEnemies, AllAllies)
- IgnoresProtection: bool
- Description: string
- SpecialEffects: List<SpecialEffect>   (e.g., gauge refill, apply status effect)
```

**Special Effect (sub-object on abilities):**
```
- Type: SpecialEffectType   (GaugeRefill, ApplyBuff, ApplyDebuff)
- Params: Dictionary<string, object>   (e.g., { "amount": 0.5 } or { "effectId": "shield_wall", "duration": 3 })
```

**Status Effect Definition:**
```
- Id: string                (e.g., "shield_wall", "taunt", "defend")
- Name: string
- Description: string
- Duration: int             (number of actions the effect lasts)
- EffectType: StatusEffectType  (DamageReduction, ForcedTarget, GaugeModifier)
- Params: Dictionary<string, object>   (e.g., { "factor": 0.5 } for 50% damage reduction)
```

**Active Status Effect (runtime instance on a unit):**
```
- EffectId: string          (references StatusEffectDefinition.Id)
- RemainingDuration: int    (actions remaining, decremented when the affected unit acts)
- SourceUnitId: string      (who applied this effect)
```

**Battle State:**
```
- Phase: BattlePhase        (Setup, Running, Paused, Ended)
- Tick: int                 (current tick count, starts at 0)
- Teams: BattleUnit[2][]    (two arrays of 7 units each)
- Winner: int?              (null during battle, 0 or 1 when ended)
- EventLog: List<BattleEvent>
```

### Step 2: Archetype & Ability System

Implement the stat-threshold archetype system with a **reduced set** of archetypes and abilities. These are defined as data registries in `Core/Data/`.

**Prototype Archetypes (4 basic + 3 advanced):**

| Threshold | Archetype | Abilities |
| --------- | --------- | --------- |
| Str ≥ 10 | Fighter | **Power Strike** (physical, 1.5× Str damage, single enemy), **Cleave** (physical, 1.0× Str damage, all enemies) |
| Mag ≥ 10 | Mage | **Fire Bolt** (magical, 1.8× Mag damage, single enemy), **Heal** (magical, 1.5× Mag healing, single ally) |
| Def ≥ 10 | Guardian | **Shield Wall** (buff, applies `shield_wall` status effect: reduce damage taken by 50% for 3 actions, self), **Taunt** (debuff, applies `taunt` status effect: force all enemies to target this unit for 2 actions, self) |
| Spd ≥ 10 | Scout | **Quick Strike** (physical, 1.0× Str damage + refills gauge 50% after use, single enemy) |
| Str ≥ 10 AND Mag ≥ 10 | Spellblade | **Enchanted Strike** (physical+magical, 1.0× Str + 1.0× Mag damage, single enemy) |
| Def ≥ 10 AND Mag ≥ 10 | Paladin | **Smite** (magical, 1.5× Mag damage, single enemy), **Lay on Hands** (magical, 2.5× Mag healing, single ally) |
| Spd ≥ 10 AND Str ≥ 10 | Assassin | **Backstab** (physical, 2.0× Str damage, single enemy, **ignores front row protection**) |

**Universal abilities (always available):**
- **Attack** (physical, 1.0× Str damage, single enemy)
- **Defend** (applies `defend` status effect: reduce all incoming damage by 50% until next action, self)

**Status Effect Definitions (prototype set):**

| Effect ID | Name | Type | Duration | Params |
| --------- | ---- | ---- | -------- | ------ |
| `shield_wall` | Shield Wall | DamageReduction | 3 actions | `{ "factor": 0.5 }` — incoming damage multiplied by 0.5 |
| `taunt` | Taunt | ForcedTarget | 2 actions | `{}` — all enemies must target this unit for single-target attacks |
| `defend` | Defend | DamageReduction | 1 action | `{ "factor": 0.5 }` — incoming damage multiplied by 0.5 (expires on the unit's next action) |

**Archetype derivation rules (pure function, no side effects):**
- `GetUnlockedArchetypes(stats)` — iterate the archetype registry, return all where every threshold is met.
- `GetAvailableAbilities(stats)` — collect all ability IDs from unlocked archetypes, plus universal abilities. Return as a flat list with no duplicates.
- These are **always recomputed from stats**, never stored. Stats are the single source of truth.

**Edge case — invalidated gambits:** If the player changes stats such that an archetype is lost, any gambits referencing abilities from that archetype become invalid. The editor should visually mark invalid gambits (red highlight / warning icon) but **not** auto-delete them. During battle, invalid gambits are skipped as if their condition failed.

### Step 3: Gambit Evaluation Engine

Implement the core gambit evaluation loop in `Core/Engine/GambitEvaluator.cs`. This is the heart of the game.

**Conditions to implement (prototype set):**

| Condition ID | Display Name | Logic |
| ------------ | ------------ | ----- |
| `always` | Always | Always true. |
| `enemy_any` | Any Enemy | True if any enemy is alive. |
| `enemy_lowest_hp` | Enemy: Lowest HP | Targets living enemy with lowest current HP. |
| `enemy_highest_hp` | Enemy: Highest HP | Targets living enemy with highest current HP. |
| `enemy_front_row` | Enemy: Front Row | Targets a living enemy in the front row. |
| `ally_hp_below_50` | Ally HP < 50% | Targets living ally below 50% max HP (lowest HP first). |
| `ally_hp_below_30` | Ally HP < 30% | Targets living ally below 30% max HP (lowest HP first). |
| `self_hp_below_50` | Self HP < 50% | True if this unit's HP is below 50%. Target = self. |
| `self_hp_below_30` | Self HP < 30% | True if this unit's HP is below 30%. Target = self. |
| `allies_alive_below_4` | Allies Alive < 4 | True if fewer than 4 allies are alive. |

Each condition implements a common interface:
```csharp
interface ICondition
{
    BattleUnit? Evaluate(BattleUnit source, List<BattleUnit> allies, List<BattleUnit> enemies);
}
```
Returns the resolved target, or `null` if the condition is not met.

**Taunt interaction with targeting:** When resolving single-enemy targets, the targeting system checks if any living enemy has the `taunt` status effect active. If so, the target is forced to the taunting unit (regardless of what the condition selected). This check occurs in the targeting pipeline after the condition resolves but before protection checks.

**Evaluation algorithm (per unit, when gauge is full):**
1. Iterate through the unit's gambit list from slot 1 to slot N.
2. Skip disabled gambits.
3. For each gambit, evaluate the **condition**. If it returns a valid target (or self for self-targeting):
   a. If the action targets an enemy and any enemy has `taunt` active, redirect the target to the taunting unit.
   b. Check if the target is accessible (protection rules). Skip if protected, unless ability has `IgnoresProtection = true`.
   c. Check if the unit has the required ability (from unlocked archetypes or universal abilities). Skip if invalid.
   d. If all checks pass, execute the **action** on the resolved target. Done.
4. If no gambit matches, the unit does nothing (wastes the turn). This incentivizes the player to always have an `Always → Attack` fallback.

### Step 4: Battle Simulation Engine

Implement the tick-based battle loop in `Core/Engine/BattleEngine.cs`. The engine is a **pure C# class** with no Godot dependencies.

**Core method signature:**
```csharp
public static (BattleState newState, List<BattleEvent> events) Tick(BattleState state)
```

This is a **pure function**: same inputs always produce same outputs. The simulation is deterministic (use seeded RNG for any tie-breaking randomness).

**Battle loop (driven by the Godot scene via a Timer or `_Process`):**

1. **Check win condition:** If all units on one team are defeated → set phase to `Ended`, emit `battle_ended`, declare winner.
2. **Tick all gauges:** For each living unit on both teams, increase `ActionGauge` by that unit's `FillRate` (derived from Spd).
3. **Resolve actions:** Collect all units whose `ActionGauge ≥ 1.0`. Sort them by gauge value (highest first — if two units fill at the same tick, faster one acts first; break ties with seeded random).
4. For each acting unit:
   a. Run the **gambit evaluation** (Step 3) to determine the action + target.
   b. Execute the action: calculate damage/healing using the formulas from Step 1, apply to target. Run through the **damage pipeline**: base damage → ability power multiplier → stat scaling → subtract defense/resistance → apply damage reduction buffs (Shield Wall, Defend) → apply minimum of 1 → final damage.
   c. Process special effects: apply status effects (Shield Wall, Taunt, Defend), refill gauge (Quick Strike), etc.
   d. Check if target is defeated (HP ≤ 0). Mark as dead, remove from valid targets. Emit `unit_defeated`.
   e. **Tick down status effects** on the acting unit: decrement `RemainingDuration` for each active effect. Remove expired effects and emit `buff_expired`.
   f. Reset the unit's `ActionGauge` to 0.
   g. Log the action events (e.g., `action_executed`, `damage_dealt`, `healing_done`, `buff_applied`).
5. Repeat from step 1.

**Status effect processing details:**
- **Shield Wall / Defend (DamageReduction):** When calculating incoming damage to a unit, check for active effects with type `DamageReduction`. Multiply the final damage by the effect's `factor` param (e.g., 0.5 for 50% reduction). Multiple damage reduction effects stack multiplicatively.
- **Taunt (ForcedTarget):** Handled during gambit evaluation (Step 3). When the taunting unit is defeated or the effect expires, targeting returns to normal.
- **Defend:** Duration is 1 action. Applied when the unit uses the Defend ability, expires when the unit next acts (duration ticks down at the end of the unit's action phase).

**Protection rule enforcement:**
- When an action targets a single enemy, the targeting system must respect protection rules.
- A back row unit at position P is only targetable if its front row protectors are both defeated:
  - Position 1: protected by 0 and 2
  - Position 3: protected by 2 and 4
  - Position 5: protected by 4 and 6
- Abilities with `IgnoresProtection = true` (e.g., Backstab) bypass this check.
- If a condition selects a target that is protected, skip that gambit and evaluate the next one.

**Speed / timing:**
- The simulation tick rate is driven by the Godot-side timer: e.g., 100ms per tick at 1× speed, 50ms at 2× speed.
- Provide a **pause** toggle and a **step** button (advance one tick at a time) for debugging and studying battle flow.
- The engine itself has no concept of real time — it only knows ticks. The Godot scene controls how frequently `Tick()` is called.

### Step 5: Team Editor UI

Build the interface using Godot Control nodes. This is a single scene with two team panels side by side.

**Layout:**

```
┌─────────────────────────────────────────────────────────┐
│                    TEAM EDITOR                           │
├──────────────────────────┬──────────────────────────────┤
│       TEAM 1             │          TEAM 2              │
│                          │                              │
│  Formation:              │  Formation:                  │
│  [0] [2] [4] [6]  front │  [0] [2] [4] [6]  front     │
│    [1] [3] [5]    back  │    [1] [3] [5]    back      │
│                          │                              │
│  Selected Unit: ____     │  Selected Unit: ____         │
│  Name: [________]        │  Name: [________]            │
│                          │                              │
│  Stats (50 pts):         │  Stats (50 pts):             │
│  HP:  [slider] 10       │  HP:  [slider] 10           │
│  Str: [slider] 8        │  Str: [slider] 8            │
│  Mag: [slider] 5        │  Mag: [slider] 5            │
│  Def: [slider] 12       │  Def: [slider] 12           │
│  Res: [slider] 5        │  Res: [slider] 5            │
│  Spd: [slider] 10       │  Spd: [slider] 10           │
│  Remaining: 0            │  Remaining: 0                │
│                          │                              │
│  Computed:               │  Computed:                   │
│  Max HP: 100             │  Max HP: 100                 │
│  Atk Power: 16           │  Atk Power: 16              │
│  Mag Power: 10           │  Mag Power: 10              │
│  Fill Rate: 0.06         │  Fill Rate: 0.06            │
│                          │                              │
│  Archetypes: Fighter,    │  Archetypes: Guardian,       │
│    Scout                 │    Paladin                   │
│                          │                              │
│  Gambits:                │  Gambits:                    │
│  1. [condition▼][action▼]│  1. [condition▼][action▼]    │
│  2. [condition▼][action▼]│  2. [condition▼][action▼]    │
│  3. [condition▼][action▼]│  3. [condition▼][action▼]    │
│  ...                     │  ...                         │
│  [+ Add Gambit]          │  [+ Add Gambit]              │
│                          │                              │
├──────────────────────────┴──────────────────────────────┤
│           [ Presets ▼ ]      [ START BATTLE ]            │
└─────────────────────────────────────────────────────────┘
```

**Godot implementation details:**
- **Formation grid:** A custom scene (`FormationGrid.tscn`) using `TextureButton` nodes arranged in two rows. Clicking a position selects that unit for editing. The selected position is highlighted.
- **Stat sliders:** `HSlider` nodes constrained by the point pool. When a slider value changes, recompute `RemainingPoints`. If remaining would go negative, clamp the slider value. Use Godot signals (`value_changed`) for reactive updates.
- **Archetype display:** `Label` or `RichTextLabel` listing unlocked archetypes. Grayed-out text for archetypes whose thresholds are not met, showing what stats are needed.
- **Computed stats display:** Read-only labels showing MaxHp, attack power, magic power, and fill rate derived from current stats.
- **Gambit editor:** Each gambit row is a scene instance (`GambitRow.tscn`) containing two `OptionButton` dropdowns (condition + action), a `CheckBox` for enable/disable, and up/down `Button` nodes for reordering. The action dropdown is dynamically filtered to only show abilities the unit currently has access to (based on unlocked archetypes + universal abilities).
- **Gambit reordering:** Up/down arrow buttons to move gambits in the priority list. Optionally implement drag-and-drop via `Control._get_drag_data()` / `_drop_data()`.
- **Gambit limits:** Each unit gets **6 gambit slots** in the prototype (no unlocking mechanic — all available from the start).
- **Preset button:** An `OptionButton` dropdown with preset names. Selecting a preset populates both teams with predefined builds.
- **Ability tooltips:** Hovering over an ability name in the gambit action dropdown shows a `TooltipText` with the ability's description and stats.

### Step 6: Battle View UI

Build the interface for watching the battle play out. This is a 2D scene with sprite-based unit display.

**Layout:**

```
┌─────────────────────────────────────────────────────────┐
│                    BATTLE VIEW                           │
├──────────────────────────┬──────────────────────────────┤
│       TEAM 1             │          TEAM 2              │
│                          │                              │
│  Back:   (1) (3) (5)    │    (5) (3) (1)   :Back      │
│  Front: (0)(2)(4)(6)    │  (6)(4)(2)(0) :Front        │
│                          │                              │
│  Each unit shows:        │  Each unit shows:            │
│  - Sprite (shape-based)  │  - Sprite (shape-based)      │
│  - Name label            │  - Name label                │
│  - HP bar (current/max)  │  - HP bar (current/max)      │
│  - Action gauge bar      │  - Action gauge bar          │
│  - Status effect icons   │  - Status effect icons       │
│  - [DEAD] overlay        │  - [DEAD] overlay            │
│                          │                              │
├──────────────────────────┴──────────────────────────────┤
│  BATTLE LOG (scrollable, color-coded)                   │
│  > [red] Unit A uses Power Strike on Unit B for 12 dmg │
│  > [green] Unit C uses Heal on Unit D for 8 HP          │
│  > [blue] Unit E uses Shield Wall (3 actions)           │
│  > [gray] Unit B is defeated!                            │
│  > ...                                                   │
├─────────────────────────────────────────────────────────┤
│  [⏸ Pause] [▶ Play] [⏩ 2×] [⏭ Step] [🔄 Restart]     │
│  [← Back to Editor]                                     │
└─────────────────────────────────────────────────────────┘
```

**Godot implementation details:**

**Unit sprites (`UnitSprite.tscn`):**
- Each unit is a `Node2D` scene containing:
  - A `Sprite2D` with a placeholder geometric shape (circle for mages, square for guardians, triangle for fighters, etc.) or simple character sprites. Color-coded by team.
  - A `Label` with the unit name.
  - An `HpBar` scene (custom `ProgressBar` with red/green fill and text overlay showing `currentHp/maxHp`).
  - A `GaugeBar` scene (custom `ProgressBar` with blue/yellow fill showing action gauge progress).
  - A `HBoxContainer` for status effect icons — small colored squares or simple icons indicating active buffs (blue for Shield Wall, yellow for Defend, red for Taunt).
  - A `ColorRect` or shader overlay for the defeated state (grayscale + "X" or skull icon).
- Units are positioned in their formation layout: front row closer to center, back row behind, mirrored for Team 2.

**Visual effects:**
- **Hit flash:** When a unit takes damage, briefly modulate its sprite white using a `Tween` (0.1s white → return to normal).
- **Damage numbers:** Spawn a floating `Label` at the target's position showing the damage amount in red (or healing in green). Tween it upward + fade out over 0.8s. Use `DamageNumber.tscn` scene.
- **Action highlight:** When a unit acts, briefly highlight it with a pulsing border or glow effect (a `Tween` on a surrounding `NinePatchRect` or `Light2D`).
- **Targeting line:** Briefly show a line or arrow from the acting unit to the target during action execution.
- **Screen shake:** On large hits (damage > 20% of target's MaxHp), apply a small camera shake via `Tween` on the `Camera2D` offset.
- **Spell particles:** For magical abilities (Fire Bolt, Heal, Smite), use `GPUParticles2D` with simple particle effects (sparks for fire, green motes for healing, golden burst for smite).
- **Buff/debuff application:** When a status effect is applied, flash the corresponding icon and show a brief text popup ("Shield Wall!" or "Taunted!").
- **Death animation:** When a unit is defeated, play a brief fade-out + fall-down tween, then show the dead overlay.

**Battle log:**
- A `RichTextLabel` inside a `ScrollContainer`. Auto-scrolls to the bottom on new entries.
- Color-coded using BBCode tags: `[color=red]` for damage, `[color=green]` for healing, `[color=dodgerblue]` for buffs/debuffs, `[color=gray]` for defeats and idle actions.
- Each log entry includes the tick number for reference.

**Battle controls (`BattleControls.tscn`):**
- `HBoxContainer` with `Button` nodes: Pause, Play (1×), Fast (2×), Step, Restart, Back to Editor.
- The Play/Fast buttons control the interval of a Godot `Timer` node that calls `BattleEngine.Tick()`.
  - 1× speed: Timer interval = 0.1s (100ms per tick)
  - 2× speed: Timer interval = 0.05s (50ms per tick)
- Pause: stops the `Timer`.
- Step: calls `BattleEngine.Tick()` once manually while paused.
- Restart: re-initializes the `BattleState` from the same team configs and resets the scene.
- Back to Editor: transitions to the editor scene (via `SceneTree.ChangeSceneToFile()` or scene swapping on the root node).

**Victory screen:**
- When `battle_ended` event fires, overlay a `Panel` with:
  - Winner team name in large text.
  - Summary stats: total damage dealt per team, total healing per team, units remaining, total ticks elapsed.
  - "Rematch" button (restarts the same battle) and "Back to Editor" button.

### Step 7: Presets & Polish

Once the core loop works, add quality-of-life features to speed up testing.

**Preset teams (at least 3 presets):**

1. **"Balanced"** — Mix of Fighter, Mage, Guardian, Scout builds. Standard gambits (heal injured allies, attack weakest enemy, defend when low).
2. **"Glass Cannon"** — All units are high-Str or high-Mag with minimal Def/Res. Fast kills but fragile. Aggressive gambits (always attack highest HP enemy, no defensive actions).
3. **"Turtle"** — High-Def, high-HP units with Guardian/Paladin builds. Slow but durable. Defensive gambits (Shield Wall when HP < 50%, heal allies below 30%, taunt with front row guardians).

Each preset configures **both teams** with different builds so a fight can be observed immediately.

**Additional polish:**
- **Save/load teams** using `System.Text.Json` serialization to the user's filesystem (via `Godot.FileAccess` to `user://` path), so configurations persist across sessions.
- **Export/import team as JSON** — copy to clipboard and paste from clipboard for sharing builds.
- **Smooth scene transitions** — `Tween`-based fade-out/fade-in when switching between editor and battle scenes.
- **Ability tooltips** — `TooltipText` on gambit action dropdowns and ability names explaining what the ability does, including status effect details.
- **Camera transitions** — smooth pan/zoom when battle starts, slight zoom on action resolution.

---

## File Structure

```
autobattle/
└── prototype/
    ├── project.godot                    — Godot project file
    ├── prototype.sln                    — .NET solution file
    ├── prototype.csproj                 — .NET project file (C# 12, .NET 8+)
    ├── Core/                            — Pure C#, NO Godot dependencies
    │   ├── Models/
    │   │   ├── Unit.cs                  — UnitConfig, BattleUnit records/classes
    │   │   ├── Team.cs                  — TeamConfig record
    │   │   ├── Gambit.cs                — GambitSlot record
    │   │   ├── Ability.cs               — AbilityDefinition, SpecialEffect
    │   │   ├── StatusEffect.cs          — StatusEffectDefinition, ActiveStatusEffect
    │   │   ├── BattleState.cs           — BattleState, BattleEvent, EventType
    │   │   └── Enums.cs                 — AbilityType, TargetType, StatusEffectType, BattlePhase, etc.
    │   ├── Data/
    │   │   ├── Archetypes.cs            — Archetype definitions registry (thresholds + ability lists)
    │   │   ├── Abilities.cs             — Ability definitions registry (name, power, type, target, effects)
    │   │   ├── Conditions.cs            — Gambit condition definitions + ICondition implementations
    │   │   ├── StatusEffects.cs         — Status effect definitions registry
    │   │   └── Presets.cs               — Preset team configurations (Balanced, Glass Cannon, Turtle)
    │   └── Engine/
    │       ├── BattleEngine.cs          — Battle loop: Tick(state) → (newState, events). FSM transitions.
    │       ├── CombatResolver.cs        — Damage/healing pipeline: base → multiplier → defense → buffs → min 1
    │       ├── GambitEvaluator.cs       — Gambit chain evaluation (condition checking + target resolution)
    │       ├── TargetingSystem.cs       — Filter+Sort pipeline, protection rules, taunt redirection
    │       └── StatusEffectProcessor.cs — Apply/tick/expire status effects, buff stacking logic
    ├── Scenes/
    │   ├── Main.tscn                    — Root scene: manages scene switching (editor ↔ battle)
    │   ├── Main.cs
    │   ├── Editor/
    │   │   ├── TeamEditor.tscn          — Full team editor panel (one instance per team)
    │   │   ├── TeamEditor.cs
    │   │   ├── UnitEditor.tscn          — Stat sliders, archetype display, computed stats, gambit list
    │   │   ├── UnitEditor.cs
    │   │   ├── FormationGrid.tscn       — Clickable front/back row grid (TextureButton nodes)
    │   │   ├── FormationGrid.cs
    │   │   ├── GambitRow.tscn           — Single gambit slot: condition + action dropdowns + controls
    │   │   └── GambitRow.cs
    │   ├── Battle/
    │   │   ├── BattleView.tscn          — Battle visualization: hosts unit sprites, log, controls
    │   │   ├── BattleView.cs            — Drives the Timer, calls BattleEngine.Tick(), dispatches events
    │   │   ├── UnitSprite.tscn          — Single unit: sprite, name label, HP bar, gauge bar, status icons
    │   │   ├── UnitSprite.cs
    │   │   ├── BattleLog.tscn           — RichTextLabel in ScrollContainer for color-coded action log
    │   │   ├── BattleLog.cs
    │   │   ├── BattleControls.tscn      — Pause/Play/Fast/Step/Restart/Back buttons
    │   │   └── BattleControls.cs
    │   └── UI/
    │       ├── HpBar.tscn               — Reusable HP bar (ProgressBar + label overlay)
    │       ├── HpBar.cs
    │       ├── GaugeBar.tscn            — Reusable action gauge bar
    │       ├── GaugeBar.cs
    │       ├── StatusIcon.tscn          — Small buff/debuff indicator icon
    │       ├── StatusIcon.cs
    │       ├── DamageNumber.tscn        — Floating damage/heal number with tween animation
    │       └── DamageNumber.cs
    ├── Assets/
    │   ├── Sprites/                     — Unit placeholder shapes, icons, UI elements
    │   ├── Shaders/                     — Grayscale shader for dead units, hit flash shader
    │   ├── Fonts/                       — UI fonts
    │   └── Audio/                       — (optional) hit sounds, UI clicks
    └── Resources/
        └── Themes/
            └── DefaultTheme.tres        — Godot theme for consistent UI styling (buttons, sliders, panels)
```

**Architecture note:** The `Core/` directory contains **pure C#** with no `using Godot;` statements. The battle simulation, damage calculations, gambit evaluation, and status effect processing are fully testable with standard .NET unit tests (xUnit, NUnit, etc.) and framework-agnostic. Godot scenes in `Scenes/` only handle rendering, user input, and timing — they read from and write to the engine's state, reacting to `BattleEvent` lists returned by `Tick()`. This separation means the engine can be extracted and reused if the project moves to a different frontend in the future.

---

## Definition of Done

The prototype is complete when:

- [ ] A player can configure 2 teams of 7 units each (stats, positioning, gambits)
- [ ] Stat allocation enforces the 50-point budget with reactive slider constraints
- [ ] Archetype abilities unlock/lock dynamically as stats change
- [ ] Gambit action dropdowns only show abilities the unit has access to
- [ ] Invalid gambits (from archetype loss) are visually marked and skipped during battle
- [ ] Battles simulate correctly: gauge fills by speed, gambit evaluation fires per the priority list, damage/healing math works
- [ ] **Status effects work:** Shield Wall reduces incoming damage by 50% for 3 actions, Taunt forces enemies to target the taunting unit for 2 actions, Defend halves damage until next action
- [ ] **Status effects are visible:** Active buffs/debuffs shown as icons on unit sprites, with application/expiry feedback
- [ ] Front/back row protection rules are enforced (back row units are untargetable until protectors fall)
- [ ] Taunt overrides target selection for single-target abilities
- [ ] Battle can be paused, played at 1× and 2× speed, and stepped through tick-by-tick
- [ ] Battle log shows all actions with targets, numbers, and status effect changes (color-coded)
- [ ] **Visual feedback:** Damage numbers float and fade, hit flashes on damaged units, spell particles for magical abilities, screen shake on big hits
- [ ] Victory is declared when all units on one side are defeated, with a summary screen
- [ ] At least 3 preset teams are available for quick testing
- [ ] Teams can be saved/loaded to persist across sessions
- [ ] The game runs in the Godot 4.6 editor or as an exported build
- [ ] The `Core/` engine has no Godot dependencies and can be tested independently
