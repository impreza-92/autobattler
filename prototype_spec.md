# Prototype Specification — Auto-Battle System

## Goal

Build a **browser-based prototype** that validates the core auto-battle and gambit system. This prototype focuses exclusively on **team building and automated combat** — no world map, no progression, no environmental manipulation. The player builds two teams, programs their gambits, and watches them fight.

**Tech stack:** Svelte + Vite + TypeScript. Svelte's reactive data binding is ideal for this prototype — the team editor relies heavily on constrained sliders, dynamic archetype unlocking, and filtered gambit dropdowns, all of which benefit from reactive state management. The battle simulation engine is pure TypeScript (framework-agnostic); Svelte handles only the UI layer. Run with `npm run dev` (Vite dev server).

---

## Scope Summary

### Included in Prototype

| Feature                        | Notes                                                  |
| ------------------------------ | ------------------------------------------------------ |
| Team editor (2 teams)          | Stat allocation, positioning, gambit editing            |
| 7-unit formation with rows     | Front row (0, 2, 4, 6) and back row (1, 3, 5)         |
| Back row protection rules      | Back row units shielded by adjacent front row units     |
| ATB action gauge               | Speed-based gauge fill, execute gambit when full        |
| Gambit system (condition→action)| Priority list, evaluate top-to-bottom                  |
| Stat-threshold archetypes      | Stat allocation unlocks archetype abilities             |
| Basic combat math              | Physical/magical damage, defense/resistance             |
| Battle visualization           | Simple UI showing units, HP bars, action log            |
| Battle controls                | Start, pause, speed up (2×), step-through              |

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
| Status effects              | Keep first version to direct damage/healing only           |
| Gambit slot unlocks         | All slots available from the start in prototype            |
| Meta-progression            | No persistence needed for prototype                        |
| Shopping / treasure         | Depends on world map / progression                         |
| PvP networking              | Local-only prototype (both teams edited on same screen)    |

---

## Step-by-Step Implementation Plan

### Step 1: Data Model

Define the core data structures. No UI yet — just TypeScript types/interfaces.

**Unit:**
```
- id: string
- name: string
- position: number (0–6)
- stats: { hp, str, mag, def, res, spd } (all numbers, drawn from a pool of 50)
- currentHp: number (starts equal to computed max HP)
- actionGauge: number (0.0 to 1.0)
- gambits: array of { condition, action, enabled }
- unlockedArchetypes: derived from stats (computed, not stored)
- availableAbilities: derived from unlockedArchetypes (computed, not stored)
```

**Stats (prototype subset — 6 stats, no elemental affinities or luck):**

| Stat | Abbr | Effect |
| ---- | ---- | ------ |
| Hit Points | HP | Max HP is computed from this (e.g., `maxHp = 20 + hp * 8`). A unit with 0 HP stat points still has 20 HP. |
| Strength | Str | Physical damage: `baseDamage = str * 2` |
| Magic | Mag | Magical damage/healing: `baseDamage = mag * 2` |
| Defense | Def | Physical damage reduction: `finalDamage = max(1, damage - def)` |
| Resistance | Res | Magical damage reduction: `finalDamage = max(1, damage - res)` |
| Speed | Spd | Gauge fill rate per tick: `fillRate = 0.01 + spd * 0.005` |

- **Stat point pool:** 50 points per unit. Every stat starts at a **minimum of 1** (cannot go below 1). With 6 stats in the prototype, 6 points are pre-allocated, leaving **44 points freely distributable**.
- Minimum value per stat: 1. Maximum value per stat: 99 (unreachable with base 50 pool, but supports future growth from leveling/treasures).
- The player simply adds or removes points. When the remaining pool hits 0, no more points can be added to any stat. No clamping or proportional redistribution — keep it simple.
- The formulas above are starting points — they will need tuning during playtesting.

**Team:**
```
- name: string
- units: array of 7 Units
```

**Gambit:**
```
- condition: { type: string, params: object }  (e.g., { type: "enemy_lowest_hp" })
- action: { type: string, params: object }      (e.g., { type: "ability", abilityId: "power_strike" })
- enabled: boolean
```

**Ability:**
```
- id: string
- name: string
- type: "physical" | "magical" | "heal" | "buff" | "debuff"
- power: number
- target: "single_enemy" | "single_ally" | "self" | "all_enemies" | "all_allies"
- description: string
```

### Step 2: Archetype & Ability System

Implement the stat-threshold archetype system with a **reduced set** of archetypes and abilities for the prototype.

**Prototype Archetypes (4 basic + 3 advanced):**

| Threshold | Archetype | Abilities |
| --------- | --------- | --------- |
| Str ≥ 10 | Fighter | **Power Strike** (physical, 1.5× Str damage, single enemy), **Cleave** (physical, 1.0× Str damage, all enemies) |
| Mag ≥ 10 | Mage | **Fire Bolt** (magical, 1.8× Mag damage, single enemy), **Heal** (magical, 1.5× Mag healing, single ally) |
| Def ≥ 10 | Guardian | **Shield Wall** (buff, reduce damage taken by 50% for 3 actions, self), **Taunt** (debuff, force all enemies to target this unit for 2 actions) |
| Spd ≥ 10 | Scout | **Quick Strike** (physical, 1.0× Str damage + fills gauge 50% after use, single enemy) |
| Str ≥ 10 AND Mag ≥ 10 | Spellblade | **Enchanted Strike** (physical+magical, 1.0× Str + 1.0× Mag damage, single enemy) |
| Def ≥ 10 AND Mag ≥ 10 | Paladin | **Smite** (magical, 1.5× Mag damage, single enemy), **Lay on Hands** (magical, 2.5× Mag healing, single ally) |
| Spd ≥ 10 AND Str ≥ 10 | Assassin | **Backstab** (physical, 2.0× Str damage, single enemy, **ignores front row protection**) |

**Universal abilities (always available):**
- **Attack** (physical, 1.0× Str damage, single enemy)
- **Defend** (reduce all incoming damage by 50% until next action, self)

### Step 3: Gambit Evaluation Engine

Implement the core gambit evaluation loop. This is the heart of the game.

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

**Evaluation algorithm (per unit, when gauge is full):**
1. Iterate through the unit's gambit list from slot 1 to slot N.
2. Skip disabled gambits.
3. For each gambit, evaluate the **condition**. If it returns a valid target (or `true` for self-targeting):
   a. Check if the unit has the required ability (from unlocked archetypes or universal abilities).
   b. If yes, execute the **action** on the resolved target. Done.
4. If no gambit matches, the unit does nothing (wastes the turn). This incentivizes the player to always have an `Always → Attack` fallback.

### Step 4: Battle Simulation Engine

Implement the tick-based battle loop.

**Battle loop (runs in a `requestAnimationFrame` or `setInterval` loop):**

1. **Check win condition:** If all units on one team are defeated → end battle, declare winner.
2. **Tick all gauges:** For each living unit on both teams, increase `actionGauge` by that unit's `fillRate` (derived from Spd).
3. **Resolve actions:** Collect all units whose `actionGauge ≥ 1.0`. Sort them by gauge value (highest first — if two units fill at the same tick, faster one acts first; break ties randomly).
4. For each acting unit:
   a. Run the **gambit evaluation** (Step 3) to determine the action + target.
   b. Execute the action: calculate damage/healing using the formulas from Step 1, apply to target.
   c. Check if target is defeated (HP ≤ 0). Mark as dead, remove from valid targets.
   d. Reset the unit's `actionGauge` to 0.
   e. Log the action to the **battle log** (e.g., "Unit A uses Power Strike on Unit B for 12 damage").
5. Repeat from step 1.

**Protection rule enforcement:**
- When an action targets a single enemy, the targeting system must respect protection rules.
- A back row unit at position P is only targetable if its front row protectors are both defeated:
  - Position 1: protected by 0 and 2
  - Position 3: protected by 2 and 4
  - Position 5: protected by 4 and 6
- Abilities with the `ignores_protection` flag (e.g., Backstab) bypass this check.
- If a condition selects a target that is protected, skip that gambit and evaluate the next one.

**Speed / timing:**
- The simulation should have a configurable `tickRate` (e.g., 100ms per tick at 1× speed, 50ms at 2× speed).
- Provide a **pause** button and a **step** button (advance one tick at a time) for debugging and studying battle flow.

### Step 5: Team Editor UI

Build the interface for configuring both teams before battle. This is a single-page UI with two team panels.

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
│                   [ START BATTLE ]                       │
└─────────────────────────────────────────────────────────┘
```

**Functionality:**
- Click a position (0–6) in the formation to select that unit for editing.
- **Stat sliders** are constrained: moving one slider up reduces remaining points. Cannot exceed the pool of 50.
- **Archetypes unlocked** updates dynamically as stats change — show which archetypes are active and grayed-out ones that aren't met.
- **Gambit editor** shows dropdown selectors for condition and action. The action dropdown only shows abilities the unit currently has access to (based on unlocked archetypes + universal abilities).
- Gambits can be **reordered** (drag-and-drop or up/down buttons), **enabled/disabled** (checkbox), and **removed**.
- Each unit gets **6 gambit slots** in the prototype (no unlocking mechanic — all available from the start).
- **Preset teams** button: populate both teams with reasonable default builds so the developer (and you) can quickly test without manually building 14 units every time.

### Step 6: Battle View UI

Build the interface for watching the battle play out.

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
│  - Name                  │  - Name                      │
│  - HP bar (current/max)  │  - HP bar (current/max)      │
│  - Action gauge bar      │  - Action gauge bar          │
│  - [DEAD] overlay        │  - [DEAD] overlay            │
│                          │                              │
├──────────────────────────┴──────────────────────────────┤
│  BATTLE LOG (scrollable)                                │
│  > Unit A uses Power Strike on Unit B for 12 dmg       │
│  > Unit C uses Heal on Unit D for 8 HP                  │
│  > Unit B is defeated!                                   │
│  > ...                                                   │
├─────────────────────────────────────────────────────────┤
│  [⏸ Pause] [▶ Play] [⏩ 2×] [⏭ Step] [🔄 Restart]     │
│  [← Back to Editor]                                     │
└─────────────────────────────────────────────────────────┘
```

**Functionality:**
- Units are displayed in their formation positions. Dead units are grayed out / crossed out.
- **HP bars** update in real time as damage and healing occur.
- **Action gauge bars** fill visually, showing which unit will act next.
- When a unit acts, briefly **highlight** it and its target (e.g., flash or border color change).
- **Battle log** scrolls automatically, showing each action with damage numbers, healing amounts, and defeat messages.
- **Controls:** Pause, Play (1× speed), Fast (2× speed), Step (advance one tick), Restart (replay same matchup), Back to Editor.
- When one team wins, show a **victory banner** with stats summary (total damage dealt, units remaining, etc.).

### Step 7: Presets & Polish

Once the core loop works, add quality-of-life features to speed up testing.

**Preset teams (at least 3 presets):**

1. **"Balanced"** — Mix of Fighter, Mage, Guardian, Scout builds. Standard gambits.
2. **"Glass Cannon"** — All units are high-Str or high-Mag. Fast kills but fragile.
3. **"Turtle"** — High-Def, high-HP units with Guardian/Paladin builds. Slow but durable.

**Additional polish:**
- Save/load teams to browser `localStorage` so configurations persist across page refreshes.
- Export/import team as JSON (copy-paste) for sharing builds.
- Display computed stats (max HP, damage, fill rate) alongside raw stat values in the editor.
- Show a tooltip on each ability explaining what it does.
- Color-code action log entries by type (damage = red, healing = green, buff = blue).

---

## File Structure

```
autobattle/
├── prototype/
│   ├── index.html              — Vite entry point
│   ├── vite.config.ts          — Vite config with Svelte plugin
│   ├── svelte.config.js        — Svelte config
│   ├── tsconfig.json
│   ├── package.json            — Dependencies: svelte, @sveltejs/vite-plugin-svelte, typescript, vite
│   ├── src/
│   │   ├── main.ts             — Entry point, mounts the Svelte app
│   │   ├── App.svelte          — Root component: switches between Editor and Battle views
│   │   ├── lib/
│   │   │   ├── types.ts        — All interfaces/types (Unit, Team, Gambit, Ability, etc.)
│   │   │   ├── data/
│   │   │   │   ├── archetypes.ts — Archetype definitions (thresholds + ability lists)
│   │   │   │   ├── abilities.ts  — Ability definitions (name, power, type, target)
│   │   │   │   ├── conditions.ts — Gambit condition definitions + evaluation functions
│   │   │   │   └── presets.ts    — Preset team configurations
│   │   │   └── engine/
│   │   │       ├── battle.ts     — Battle loop, tick processing, win condition checks
│   │   │       ├── combat.ts     — Damage/healing calculation, protection rule enforcement
│   │   │       └── gambits.ts    — Gambit evaluation engine (condition checking + target resolution)
│   │   └── components/
│   │       ├── TeamEditor.svelte     — Full team editor panel (one per team)
│   │       ├── UnitEditor.svelte     — Stat sliders, archetype display, gambit list for one unit
│   │       ├── FormationView.svelte  — Clickable front/back row grid for unit selection
│   │       ├── GambitRow.svelte      — Single gambit slot: condition dropdown + action dropdown + controls
│   │       ├── BattleView.svelte     — Battle visualization: unit cards, HP/gauge bars, log, controls
│   │       └── UnitCard.svelte       — Single unit display during battle (name, HP bar, gauge bar, status)
│   └── public/                 — Static assets (if any)
```

**Architecture note:** The `lib/engine/` directory contains **pure TypeScript** with no Svelte dependencies. The battle simulation, damage calculations, and gambit evaluation are fully testable and framework-agnostic. Svelte components in `components/` only handle rendering and user input, reading from and writing to the engine's state.

---

## Definition of Done

The prototype is complete when:

- [ ] A player can configure 2 teams of 7 units each (stats, positioning, gambits)
- [ ] Stat allocation enforces the 50-point budget
- [ ] Archetype abilities unlock/lock dynamically as stats change
- [ ] Gambit dropdowns only show abilities the unit has access to
- [ ] Battles simulate correctly: gauge fills by speed, gambit evaluation fires per the priority list, damage/healing math works
- [ ] Front/back row protection rules are enforced (back row units are untargetable until protectors fall)
- [ ] Battle can be paused, played at 1× and 2× speed, and stepped through tick-by-tick
- [ ] Battle log shows all actions with targets and numbers
- [ ] Victory is declared when all units on one side are defeated
- [ ] At least 3 preset teams are available for quick testing
- [ ] The game runs in a modern browser via `npm run dev` (Vite dev server)
