# Team System Analysis

## Overview

The team system controls collision, targeting, and damage interactions between entities. It uses a 256x256 compatibility lookup table to determine if two entities are friendly or hostile.

**Namespace:** `Game::Components` (Actor, Positioned, etc.), `Game::GameObject`

---

## Team Field Location

### Path: GameObject → Team

**Direct Path (most common):**
```
GameObject + 0x90 (144) ──► Positioned*
  └─► Positioned + 0x1E0 (480) ──► Team (uint16)
```

**Via ActorComponent (roundabout - same result):**
```
GameObject
  └─► GetActorComponent(gameObject)
        └─► ActorComponent + 0x08 ──► GameObject* (back-reference to owner)
              └─► GameObject + 0x90 (144) ──► Positioned*
                    └─► Positioned + 0x1E0 (480) ──► Team (uint16)
```

> **Note:** ActorComponent+0x08 stores a back-reference to its owning GameObject.
> Both paths lead to the same Positioned component.

### Team Field Format (16-bit)

| Bits | Mask | Description |
|------|------|-------------|
| 0-14 | `0x7FFF` | Team ID (0-32767) |
| 15 | `0x8000` | Is Minion flag |

---

## Team Compatibility Table

### Properties

| Property | Value |
|----------|-------|
| **Address** | `0x7FF68455F590` |
| **Name** | `g_TeamCompatibilityTable` |
| **Size** | 65,536 bytes (256 × 256) |
| **Segment** | `.data` (compiled into binary) |

### Lookup Formula

```c
bool is_friendly = g_TeamCompatibilityTable[256 * attacker_team + target_team];
// Returns: 1 = friendly (no damage), 0 = hostile (can damage)
```

### Signature
```
// Reference pattern for table access
48 8D 05 ?? ?? ?? ?? 0F B7  // lea rax, g_TeamCompatibilityTable
```

---

## Team Categories

| Category | Team IDs | Description |
|----------|----------|-------------|
| **Monster Base** | 0 | Primary monster team |
| **Player** | 1 | Main player team |
| **Monsters** | 2-68 | Various monster types |
| **Minion Controller** | 69 | Special team friendly to player |
| **Neutral** | 70 | Friendly to BOTH player and monsters |
| **Monster Extended** | 71-119 | Additional monster variants |
| **Environment (Anti-Player)** | 120-125 | Damages players/minions, not monsters |
| **Environment (Anti-Monster)** | 126-127 | Damages monsters, not players |
| **Player Minions** | 128-255 | All player summons (128 slots) |

---

## Team Relationships

### Player (Team 1) - Friendly With:
- Team 1 (self)
- Team 69 (minion controller)
- Team 70 (neutral)
- Teams 128-255 (all player minions)
- **Total: 131 friendly teams**

### Player (Team 1) - Hostile To:
- Team 0 (monster base)
- Teams 2-68 (monsters)
- Teams 71-119 (extended monsters)
- Teams 120-127 (environment hazards)
- **Total: 125 hostile teams**

### Monster (Team 0) - Friendly With:
- Team 0 (self)
- Team 70 (neutral)
- Teams 120-127 (environment)

### Player Minions (Teams 128-255) - Friendly With:
- Team 1 (player)
- Self only

### Environment Teams:
- **Teams 120-125**: Hit players and minions, NOT monsters (monster traps)
- **Teams 126-127**: Hit monsters, NOT players (player-friendly environment)

---

## Key Functions

### Team Check Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x7FF6804DA880` | `IsEnemy_TeamCheck` | Returns true if teams are hostile (lookup=0) |
| `0x7FF6804DA960` | `IsAlly_TeamCheck` | Returns true if teams are friendly (lookup=1) |
| `0x7FF681C5E940` | `AreTeamsHostile` | Simple hostile check (87 bytes) |
| `0x7FF6805F1900` | `CanInteract_TeamOrNPC` | Team + NPC interaction check |
| `0x7FF681C4A230` | `TeamFilter_WithStatCheck` | Team check with stat 13458 |

### AoE/Collision Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x7FF680505FB0` | `CheckTargetTeamCompatibility` | Full target compatibility check |
| `0x7FF68052C020` | `FindNearestHostileEntity` | Find nearest hostile with team filter |
| `0x7FF6815A2200` | `CollisionCheck_WithTeamFilter` | Collision check with full team logic |
| `0x7FF6815A25E0` | `AoE_CheckHostileAndSpawnSpellEffect` | AoE + spawn spell effect |
| `0x7FF6815A27C0` | `AoE_CheckHostileAndSpawnParticle` | AoE + spawn particle effect |
| `0x7FF6815A3A80` | `AoE_CheckHostileAndSetFlag` | AoE + set collision flag |
| `0x7FF681FFCBF0` | `Raycast_BresenhamLineCheck` | Line-of-sight raycast |

### Visual Effect Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x7FF681595DC0` | `SpawnSpellMicrotransactionEffect` | Spawn MTX spell effect |
| `0x7FF681595B60` | `SpawnElementalDamageParticle` | Spawn elemental damage particle |

### Component Getter Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x7FF680522040` | `GetActorComponent` | Get Actor component by name lookup |
| `0x7FF6805221E0` | `GetNPCComponent` | Get NPC component by name lookup |
| `0x7FF680522520` | `GetLifeComponent` | Get Life component by name lookup |
| `0x7FF680522590` | `GetStatsComponent` | Get Stats component by name lookup |
| `0x7FF68050DED0` | `GameObject_GetComponentLookup` | Get component map from DataObject |

### Debug Functions

| Address | Name | Description |
|---------|------|-------------|
| `0x7FF682089D80` | `DebugPrint_PositionedInfo` | Prints Team, IsMinion, position info |

---

## Important Stats

| Stat ID | Purpose |
|---------|---------|
| **2459** | Blocks damage |
| **876** | Additional protection |
| **13458** | **Bypasses team table - requires EXACT team match** |
| **6481** | Immunity/protection (skips targeting) |

### Stat 13458 Special Behavior

Entities with Stat 13458 bypass the team compatibility table and only interact with entities of the **exact same team ID**:

```c
if (HasStat(entity, 13458)) {
    // Only target if SAME team ID (exact match)
    if (source_team != target_team) return;  // Skip
} else {
    // Normal: Use compatibility table
    if (g_TeamCompatibilityTable[256 * source_team + target_team]) return;  // Skip if friendly
}
```

---

## Collision Check Flow

```
┌─────────────────────────────────────────────────────────────┐
│                  Collision/Targeting Check                  │
├─────────────────────────────────────────────────────────────┤
│  1. Check (gameObject+0x84) & 8  ─── "Is Targetable" flag   │
│                                                             │
│  2. Check Stats 2459, 876 ─── Damage blocking               │
│                                                             │
│  3. GetActorComponent(gameObject)                           │
│                                                             │
│  4. Check Stat 13458 ────────┬─ YES: Exact team match only  │
│                              └─ NO:  Use compatibility table│
│                                                             │
│  5. Team Check:                                             │
│     table[256 * source_team + target_team]                  │
│     - 1 = friendly → skip                                   │
│     - 0 = hostile  → continue                               │
│                                                             │
│  6. Check NPCComponent+0x20 flag (skip if set)              │
│                                                             │
│  7. Check Stat 6481 (immunity - skip if set)                │
│                                                             │
│  8. Raycast_BresenhamLineCheck() ─── Line-of-sight          │
│                                                             │
│  9. Execute collision/damage                                │
└─────────────────────────────────────────────────────────────┘
```

---

## Code Examples

### Reading Team from GameObject (C)

```c
// Direct path
uint16_t GetTeamFromGameObject(void* gameObject) {
    void* positioned = *(void**)((char*)gameObject + 0x90);  // GameObject + 0x90
    uint16_t team_raw = *(uint16_t*)((char*)positioned + 0x1E0);  // Positioned + 0x1E0
    return team_raw & 0x7FFF;  // Mask off minion flag
}

bool IsMinionFromGameObject(void* gameObject) {
    void* positioned = *(void**)((char*)gameObject + 0x90);
    uint16_t team_raw = *(uint16_t*)((char*)positioned + 0x1E0);
    return (team_raw >> 15) & 1;
}
```

### Check if Hostile to Player (C)

```c
bool CanDamagePlayer(uint16_t team_id) {
    // Friendly to player: 1, 69, 70, 128-255
    if (team_id == 1 || team_id == 69 || team_id == 70 || team_id >= 128)
        return false;
    return true;  // Teams 0, 2-68, 71-127 are hostile
}
```

### Using Compatibility Table (C)

```c
extern uint8_t g_TeamCompatibilityTable[256 * 256];  // At 0x7FF68455F590

bool AreTeamsFriendly(uint16_t team_a, uint16_t team_b) {
    team_a &= 0x7FFF;
    team_b &= 0x7FFF;
    return g_TeamCompatibilityTable[256 * team_a + team_b] == 1;
}

bool AreTeamsHostile(uint16_t team_a, uint16_t team_b) {
    team_a &= 0x7FFF;
    team_b &= 0x7FFF;
    return g_TeamCompatibilityTable[256 * team_a + team_b] == 0;
}
```

---

## Memory Layout Reference

### GameObject Structure (`Game::GameObject`)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| +0x08 | 8 | ptr | DataObject* (resource/definition data) |
| +0x10 | 8 | ptr | Components[] array |
| +0x70 | 8 | ptr | ComponentLookup |
| +0x84 | 1 | byte | Flags (bit 3 = targetable) |
| +0x90 | 8 | ptr | Positioned* |

### Positioned Component (`Game::Components::Positioned`)

*Offsets verified via `DebugPrint_PositionedInfo` at `0x7FF682089D80`*

| Offset | Size | Type | Field |
|--------|------|------|-------|
| +0x08 | 8 | ptr | Owner GameObject* (back-reference) |
| +0x158 | 8 | ptr | ScaleData* (has scale at +0x5C) |
| +0x1E0 | 2 | uint16 | **team** (bits 0-14 = ID, bit 15 = is_minion) |
| +0x1E3 | 1 | byte | object_size |
| +0x1E5 | 1 | byte | flags1 (bit 4 = flipped, bit 3 = collidable check) |
| +0x1E6 | 1 | byte | flags2 (bit 1 = moving) |
| +0x1E7 | 1 | byte | flags3 (bit 0 = collidable check, bit 1 = static position) |
| +0x240 | 4 | float | movement_speed |
| +0x290 | 4 | int32 | cached_height (0x7FFFFFFF = uncached) |
| +0x294 | 4 | int32 | world_x (tile coords) |
| +0x298 | 4 | int32 | world_y (tile coords) |
| +0x29C | 4 | float | orientation (radians) |
| +0x2B0 | 4 | float | scale |
| +0x2B8 | 4 | float | position_x (world coords) |
| +0x2BC | 4 | float | position_y (world coords) |

### ActorComponent (`Game::Components::Actor`)

| Offset | Size | Type | Field |
|--------|------|------|-------|
| +0x08 | 8 | ptr | GameObject* (back-reference to owner) |

### Component Lookup Pattern

All `Get*Component` functions follow the same pattern:
```c
__int64 GetActorComponent(__int64 gameObject) {
    // 1. Get component lookup from DataObject
    __int64 lookup = GameObject_GetComponentLookup(gameObject + 0x08);

    // 2. Find component by name in the map at lookup+0x28
    _QWORD* result = ComponentMap_FindByName(lookup + 0x28, &out, "Actor");

    // 3. Get component index from result
    int index = *(int*)(*result + 8);
    if (index == -1) return 0;

    // 4. Return component from array at gameObject+0x10
    return *(gameObject + 0x10)[index];
}
```

Known component names: `"Actor"`, `"NPC"`, `"Stats"`, `"Life"`, `"Positioned"`, `"Transitionable"`

---

## Signatures

### IsEnemy_TeamCheck
```
40 53 48 83 EC ?? 48 8B D9 E8 ?? ?? ?? ?? 48 85 C0 74 ?? 80 78 20 00
```

### IsAlly_TeamCheck
```
40 53 48 83 EC ?? 48 8B D9 E8 ?? ?? ?? ?? 48 85 C0 74 ?? 80 78 20 00
```

### Team Table Access Pattern
```
0F B7 ?? ?? 01 00 00    // movzx reg, word ptr [reg+0x1E0]
25 FF 7F 00 00          // and eax, 0x7FFF
48 8D ?? ?? ?? ?? ??    // lea reg, g_TeamCompatibilityTable
```

---

*Document generated from IDA analysis session*
