# Autobattler — Game Design Document

## Overview

A medieval fantasy autobattler where teams of units compete on a real-time world map to control a single capture point in a king-of-the-hill format. Players build teams of 7 units, allocate stat points to unlock archetype abilities, and program their behavior through a gambit system inspired by Final Fantasy XII. Battles resolve fully automatically — victory depends entirely on preparation: team composition, stat builds, positioning, and gambit programming.

Matches support a variable number of teams (configurable per match) and can be played in both PvP and PvE modes. The first team to reach the score threshold by holding the capture point wins.

---

## 1. World Map

### 1.1 Layout & Movement

- The world map is a navigable terrain with roads, chokepoints, open fields, forests, rivers, and structures.
- All teams move in **real-time**. There are no turns on the world map — time is always flowing.
- Each team moves as a single entity (the squad of 7 units) represented by a token or banner on the map.
- Movement speed on the world map may be influenced by terrain type (roads are faster, forests are slower, etc.).

### 1.2 Capture Point (King of the Hill)

- There is a **single capture point** located on the map.
- A team begins capturing the point by occupying it with no enemy teams present.
- While a team holds the capture point uncontested, they accumulate **score over time**.
- If multiple teams occupy the capture point simultaneously, a battle is triggered (see Section 2).
- The **win condition** is reaching a configurable **score threshold**. The first team to accumulate enough points wins the match.
- If the holding team is challenged and loses the battle, the capture point becomes neutral (or is seized by the victor).

### 1.3 Environmental Manipulation

Players can interact with and reshape the world map using both **destructive** and **constructive** actions. These actions cost resources (gold, items, or cooldown timers — specifics TBD) and are performed by the team on the world map in real-time.

#### Destructive / Denial

| Action            | Effect                                                                 |
| ----------------- | ---------------------------------------------------------------------- |
| Block Road        | Places a barricade on a road, forcing enemies to find alternate routes or spend time clearing it. |
| Set Fire          | Ignites an area, creating a damage-over-time zone that injures teams passing through. |
| Poison Area       | Contaminates a zone, applying a poison debuff to teams that enter.     |
| Set Trap          | Places a hidden trap that triggers when an enemy team moves over it, causing damage or a status effect. |
| Collapse Terrain  | Destroys a bridge or narrows a passage, cutting off a route entirely.  |

#### Constructive / Supportive

| Action             | Effect                                                                |
| ------------------ | --------------------------------------------------------------------- |
| Build Fortification | Creates a defensive structure at a location, granting a defensive bonus to the team that holds it during battle. |
| Healing Zone       | Establishes a zone that gradually restores HP to a team resting in it. |
| Build Bridge       | Creates a new crossing over a river or chasm, opening a new route.    |
| Watchtower         | Grants increased vision range from a position, revealing enemy movements and hidden traps. |
| Supply Cache       | Places a resource stash that your team (or others) can pick up later. |

### 1.4 Encounters

- When two or more teams occupy the same space on the world map, a **battle** is triggered automatically.
- Teams cannot pass through each other — meeting on a road forces a confrontation.
- A team may choose to **retreat** from an area before an enemy arrives, but once overlap occurs, the battle begins.
- If more than two teams collide simultaneously, the battle may be a multi-team free-for-all (rules TBD — possibly sequential battles or a three-way fight).

---

## 2. Auto-Battles

### 2.1 Core Concept

Battles are **fully automatic**. Once a battle starts, the player has zero control — all unit behavior is dictated by their pre-programmed **gambits** (see Section 5). The player observes the outcome and uses what they learn to refine their team for future encounters.

### 2.2 Formation & Positioning

Each team fields **7 units** arranged in a **front row** and **back row**:

- **Front row positions:** 0, 2, 4, 6 (4 units)
- **Back row positions:** 1, 3, 5 (3 units)

#### Protection Rules

Back row units are **shielded** by their adjacent front row units. A back row unit cannot be targeted by standard attacks until its protectors are defeated:

| Back Row Position | Protected By       | Targetable When...              |
| ----------------- | ------------------ | ------------------------------- |
| 1                 | Position 0 and 2   | Both position 0 AND 2 are defeated |
| 3                 | Position 2 and 4   | Both position 2 AND 4 are defeated |
| 5                 | Position 4 and 6   | Both position 4 AND 6 are defeated |

- Some abilities may bypass protection rules (e.g., long-range spells, assassin abilities that target the back row directly).
- AoE (area-of-effect) abilities may hit both rows depending on the ability's targeting rules.

### 2.3 Action Gauge (ATB System)

- Each unit has an **action gauge** that fills over time.
- The fill rate is determined by the unit's **Speed (Spd)** stat — higher Speed means the gauge fills faster.
- When the gauge is full, the unit executes its highest-priority valid gambit action (see Section 5).
- After acting, the gauge resets to zero and begins filling again.
- Some abilities or status effects may modify gauge fill rate (e.g., Haste doubles fill speed, Slow halves it).

### 2.4 Battle Resolution

- A battle ends when all units on one side are defeated (HP reduced to 0).
- Defeated units are **not permanently dead** — they are revived after the battle with reduced HP (exact amount TBD, e.g., 25% HP) or can be healed at healing zones on the world map.
- The victorious team remains on the world map at the battle location and may continue moving.
- The defeated team is pushed back / respawns at a designated location (TBD — could be their starting area or nearest safe point).

---

## 3. Team Management

### 3.1 Real-Time Editing with Risk

Team editing can be done **at any time**, including during an active match on the world map. However, this comes with significant risk:

- **The match does not pause** while editing. The player's team remains on the world map, vulnerable to attack and environmental hazards.
- All edits are **atomic** — changes are drafted in a staging state but are **not applied** until the player confirms and resumes play.
- **If the team is attacked while editing**, all pending changes are **discarded** and the team enters battle with its **pre-edit configuration**.
- This creates a core **risk/reward tension**: editing in a safe location wastes time that could be spent scoring, while editing near the action risks losing your changes.

### 3.2 What Can Be Edited

| Aspect              | Description                                                      |
| ------------------- | ---------------------------------------------------------------- |
| Team Composition    | Choose which 7 units (from your roster) to field.                |
| Positioning         | Assign units to specific front/back row positions (0–6).         |
| Stat Allocation     | Redistribute stat points for each unit (see Section 4.1).        |
| Equipment           | Equip or swap gear on units (weapons, armor, accessories).       |
| Gambits             | Edit the condition→action priority list for each unit (see Section 5). |
| Gambit Slots        | Assign unlocked gambit slots to units.                           |

### 3.3 Roster

- The player maintains a **roster** of units larger than 7 (exact roster size TBD).
- Only 7 units can be fielded at a time; the rest are reserves.
- Roster composition is managed between matches as part of meta-progression (see Section 6).

---

## 4. Unit System

### 4.1 Stats

Each unit has a **pool of stat points** (approximately **50 points** at base, increasing with level/progression) that the player distributes freely among the following stats:

| Stat                      | Abbreviation | Effect                                                                  |
| ------------------------- | ------------ | ----------------------------------------------------------------------- |
| Hit Points                | HP           | Total health. Unit is defeated when HP reaches 0.                       |
| Strength                  | Str          | Increases physical attack damage.                                       |
| Magic                     | Mag          | Increases magical ability damage and healing power.                     |
| Defense                   | Def          | Reduces incoming physical damage.                                       |
| Resistance                | Res          | Reduces incoming magical damage.                                        |
| Speed                     | Spd          | Determines how quickly the action gauge fills.                          |
| Luck                      | Lck          | Affects critical hit chance, status effect proc rates, and dodge chance. |
| Elemental Affinity: Fire  | Aff:Fire     | Increases Fire damage dealt and reduces Fire damage taken.              |
| Elemental Affinity: Water | Aff:Water    | Increases Water damage dealt and reduces Water damage taken.            |
| Elemental Affinity: Earth | Aff:Earth    | Increases Earth damage dealt and reduces Earth damage taken.            |
| Elemental Affinity: Wind  | Aff:Wind     | Increases Wind damage dealt and reduces Wind damage taken.              |
| Elemental Affinity: Light | Aff:Light    | Increases Light damage dealt and reduces Light damage taken.            |
| Elemental Affinity: Dark  | Aff:Dark     | Increases Dark damage dealt and reduces Dark damage taken.              |

- Stat points can be **freely redistributed** during team editing (see Section 3).
- Equipment may grant **bonus stat points** or flat bonuses on top of allocated stats.

### 4.2 Stat-Threshold Archetype System

Instead of selecting a class at creation, units unlock **archetype abilities** by meeting stat thresholds. This allows deep build diversity and hybrid builds.

#### Basic Archetype Thresholds (Examples)

| Threshold             | Archetype Unlocked | Abilities Gained (Examples)                         |
| --------------------- | ------------------ | --------------------------------------------------- |
| Str ≥ 10              | Fighter            | Power Strike, Cleave                                |
| Mag ≥ 10              | Mage               | Fire Bolt, Ice Shard                                |
| Def ≥ 10              | Guardian           | Shield Wall, Taunt                                  |
| Res ≥ 10              | Warden             | Magic Barrier, Dispel                               |
| Spd ≥ 10              | Scout              | Quick Strike, Evasion Stance                        |
| Lck ≥ 10              | Trickster          | Lucky Strike, Steal                                 |
| HP ≥ 15               | Bulwark            | Endure, Last Stand                                  |

#### Advanced Archetype Thresholds (Examples)

Higher thresholds within a single stat or **combined thresholds** across multiple stats unlock advanced archetypes:

| Threshold                  | Archetype Unlocked | Abilities Gained (Examples)                      |
| -------------------------- | ------------------ | ------------------------------------------------ |
| Str ≥ 18                   | Berserker          | Frenzy, Reckless Blow                            |
| Mag ≥ 18                   | Archmage           | Meteor, Chain Lightning                          |
| Str ≥ 10 AND Mag ≥ 10     | Spellblade         | Enchanted Strike, Elemental Blade                |
| Def ≥ 10 AND Mag ≥ 10     | Paladin            | Holy Shield, Smite, Lay on Hands                 |
| Spd ≥ 10 AND Str ≥ 10     | Assassin           | Backstab (bypasses front row), Poison Blade      |
| Spd ≥ 10 AND Mag ≥ 10     | Enchanter          | Haste, Slow, Hex                                 |
| Mag ≥ 10 AND Lck ≥ 10     | Warlock            | Curse, Drain Life, Dark Pact                     |
| HP ≥ 15 AND Def ≥ 10      | Juggernaut         | Unstoppable, Fortress, Shockwave                 |
| Aff:Fire ≥ 8 AND Mag ≥ 10 | Pyromancer         | Fireball, Inferno, Flame Shield                  |

#### Design Notes

- A unit can meet **multiple archetype thresholds** simultaneously, gaining access to all qualifying abilities.
- This means a unit with Str 12, Mag 12, Spd 10 would unlock Fighter + Mage + Spellblade + Scout abilities — but at the cost of having lower stats elsewhere (only 6 points remaining from 50).
- The stat-point budget creates natural tension: **specialist builds** are strong in one area, while **hybrid builds** are versatile but less powerful per role.
- Available abilities from unlocked archetypes can be used in gambit action slots (see Section 5).

### 4.3 Elements

There are **6 elements** arranged in opposing pairs:

| Element | Strong Against | Weak Against |
| ------- | -------------- | ------------ |
| Fire    | Wind           | Water        |
| Water   | Fire           | Earth        |
| Earth   | Water          | Wind         |
| Wind    | Earth          | Fire         |
| Light   | Dark           | Dark         |
| Dark    | Light          | Light        |

- **Strong against**: Deals increased damage (e.g., 1.5× multiplier).
- **Weak against**: Deals reduced damage (e.g., 0.75× multiplier).
- **Elemental affinity stats** increase both the damage dealt with that element and resistance to it (see Section 4.1).
- Light and Dark are **mutually opposed** — each is both strong and weak against the other, making those matchups volatile and high-risk/high-reward.

---

## 5. Gambit System

### 5.1 Core Concept

Gambits are pre-programmed AI instructions that control a unit's behavior in battle. Inspired by Final Fantasy XII, each gambit is a **condition → action** pair arranged in a **priority list**. The unit evaluates gambits from top to bottom and executes the first one whose condition is met.

### 5.2 Structure

Each unit has a number of **gambit slots** (starting at 3, expandable to a maximum via treasures and progression, e.g., 8–10 max). Each slot contains:

1. **Condition** — A boolean check on the battle state (e.g., "Ally HP < 50%").
2. **Action** — An ability or command to execute if the condition is true (e.g., "Cast Heal on target").

The unit evaluates its gambit list from **slot 1 (highest priority) to slot N (lowest priority)** each time its action gauge is full. It performs the **first valid** gambit.

### 5.3 Conditions (Examples)

Conditions are unlockable — players start with basic conditions and find or earn more advanced ones as treasures (see Section 6).

#### Target Conditions

| Condition                  | Description                                      |
| -------------------------- | ------------------------------------------------ |
| Any Enemy                  | Always true if any enemy is alive.               |
| Nearest Enemy              | Targets the closest enemy unit.                  |
| Enemy: Lowest HP           | Targets the enemy with the lowest current HP.    |
| Enemy: Highest HP          | Targets the enemy with the highest current HP.   |
| Enemy: Back Row            | Targets a back row enemy (if accessible).        |
| Enemy: [Element] Weak      | Targets an enemy weak to a specific element.     |
| Any Ally                   | Targets any ally (including self).               |
| Ally: Lowest HP            | Targets the ally with the lowest current HP.     |
| Self                       | Targets the unit itself.                         |

#### State Conditions

| Condition                       | Description                                                |
| ------------------------------- | ---------------------------------------------------------- |
| Target HP < X%                  | Target's HP is below a percentage threshold.               |
| Target HP > X%                  | Target's HP is above a percentage threshold.               |
| Target has [Status]             | Target is affected by a specific status effect.            |
| Target does NOT have [Status]   | Target is free of a specific status effect.                |
| Allies alive < X                | Fewer than X allies remain on the field.                   |
| Enemies alive < X               | Fewer than X enemies remain on the field.                  |
| No conditions (Always)          | Always true — used as a fallback in the lowest gambit slot.|

### 5.4 Actions (Examples)

Actions are drawn from the unit's **unlocked archetype abilities** (see Section 4.2) plus universal actions:

| Action         | Type     | Description                                      |
| -------------- | -------- | ------------------------------------------------ |
| Attack         | Physical | Basic physical attack. Always available.         |
| Defend         | Utility  | Reduces incoming damage until next action.       |
| [Ability Name] | Varies   | Any ability from unlocked archetypes.            |
| Use Item       | Utility  | Uses a consumable item (if items are carried).   |

### 5.5 Example Gambit Setup

A Paladin-type unit (Def ≥ 10, Mag ≥ 10) might have:

| Priority | Condition                    | Action          |
| -------- | ---------------------------- | --------------- |
| 1        | Ally: Lowest HP < 30%       | Lay on Hands    |
| 2        | Self HP < 50%               | Holy Shield     |
| 3        | Any Ally has [Poison]       | Dispel          |
| 4        | Enemy: Lowest HP            | Smite           |
| 5        | Always                      | Attack          |

This unit prioritizes emergency healing, then self-defense, then cleansing debuffs, then finishing off weak enemies, with a basic attack as fallback.

### 5.6 Gambit Slots & Unlocks

- Units start with a base number of gambit slots (e.g., 3).
- Additional **gambit slots** can be found as treasures on the world map or earned through progression.
- New **conditions** and **actions** (beyond archetype abilities) can also be discovered as treasures.
- This means gambit programming depth grows over time — early-game units have simple behavior, late-game units can have sophisticated AI.

---

## 6. Progression

### 6.1 In-Match Progression

During a match on the world map, teams can acquire resources and upgrades through treasure pickups:

#### Treasure Types

| Treasure Type      | Effect                                                        |
| ------------------ | ------------------------------------------------------------- |
| Gold               | Currency used for in-match purchases (if applicable) or carried over to meta-progression. |
| Equipment          | Weapons, armor, accessories that grant stat bonuses or special effects. |
| Gambit Conditions  | Unlocks new conditions for the gambit system (e.g., "Enemy: [Element] Weak"). |
| Gambit Actions     | Unlocks new actions/abilities beyond archetype-granted ones.  |
| Gambit Slots       | Increases a unit's maximum gambit slot count by 1.            |
| Stat Point Bonus   | Grants additional stat points to a unit's allocatable pool.   |
| Consumable Items   | One-time-use items that can be assigned as gambit actions.     |

#### Treasure Sources

| Source                    | Description                                                            |
| ------------------------- | ---------------------------------------------------------------------- |
| Fixed Locations           | Treasure chests at known map locations — race to claim them first.     |
| Random Spawns             | Treasures appear at random locations throughout the match over time.   |
| NPC-Guarded Camps         | Neutral enemies guard valuable treasure. Defeat them in an auto-battle to claim the reward. |

### 6.2 Meta-Progression (Between Matches)

Between matches, the player can:

| Activity             | Description                                                              |
| -------------------- | ------------------------------------------------------------------------ |
| Roster Management    | Recruit new units, retire old ones, manage the total roster.             |
| Permanent Upgrades   | Spend currency earned from matches on permanent stat pool increases, gambit slots, etc. |
| Equipment Management | Manage gear inventory, buy/sell/craft equipment.                         |
| Gambit Library       | View and organize all unlocked conditions and actions.                   |
| Strategy Review      | Review battle replays to study what went right/wrong (stretch goal).    |

### 6.3 Unit Growth

- Units may gain **experience** from battles, increasing their base stat pool (e.g., from 50 to 55 points).
- Leveling up may also unlock higher stat thresholds for more advanced archetypes.
- The exact leveling curve and stat pool scaling are TBD and subject to balancing.

---

## 7. PvP & PvE

### 7.1 PvP (Player vs Player)

- Matches pit human players against each other on the world map.
- Team count is **configurable per match** (2, 4, 6, 8, etc.).
- All players interact in real-time on the same world map.
- Environmental manipulation and treasure racing create strategic depth beyond just combat.
- Matchmaking and ranking systems TBD.

### 7.2 PvE (Player vs Environment)

- The player controls one team against AI-controlled opponents.
- AI teams follow their own gambit programming and make world map decisions (moving, capturing, manipulating environment).
- PvE can serve as:
  - A **campaign/story mode** with hand-crafted scenarios, escalating difficulty, and narrative.
  - **Practice matches** for testing team builds before entering PvP.
  - **Challenge modes** with specific constraints or objectives.
- AI difficulty levels can vary the quality of enemy gambit programming, stat builds, and map strategy.

### 7.3 Shared Systems

Both PvP and PvE share all core systems (world map, battles, gambits, progression). The only difference is whether opposing teams are controlled by humans or AI. This ensures the player can practice and refine strategies in PvE that transfer directly to PvP.

---

## Appendix: Open Questions & TBD

| Topic                         | Status | Notes                                                   |
| ----------------------------- | ------ | ------------------------------------------------------- |
| Multi-team battles (3+ teams) | TBD    | How do free-for-all battles with 3+ teams work on the grid? |
| Environmental action costs    | TBD    | What resource do environmental actions cost? Cooldowns? Gold? |
| Exact stat pool numbers       | TBD    | Base pool of 50 is placeholder — needs playtesting.     |
| Archetype threshold values    | TBD    | All threshold numbers are examples — needs balancing.   |
| Roster size                   | TBD    | How many units can the player own beyond the active 7?  |
| Defeat penalty                | TBD    | What happens to a defeated team? Respawn where? HP penalty? |
| Match duration / score target | TBD    | Typical match length and score threshold values.        |
| Item/consumable system        | TBD    | Are there consumable items? How do they interact with gambits? |
| Equipment details             | TBD    | Weapon/armor types, rarity tiers, special effects.      |
| Map size & topology           | TBD    | How large is the world map? Procedurally generated or fixed? |