# CIM — Casey's Inventory Management

CIM is a **Space Engineers Programmable Block inventory management script**.

It was made specifically for the **Stone Industries Space Engineers server**.

It sorts cargo, manages LCDs, tracks whole-grid item totals, supports special stocked containers, helps with modded item names, can top up reactors, and can avoid docked ships when configured.

> Paste the script into a **Programmable Block**. No timer block is required.

## Support

Need help or want to report an issue?

Join the Stone Industries Discord:

> **SI Discord:** https://discord.gg/sigaming

Join my discord for support and suggestions!

> **My Discord:** https://discord.gg/RsH33Wr6Vy


---

## Quick install from GitHub

1. Open **CIM.cs** in this GitHub repository.
2. Click **Raw**.
3. Select all text and copy it.
4. In Space Engineers, build or open a **Programmable Block**.
5. Click **Edit**.
6. Paste the full script.
7. Click **Check Code**.
8. Click **Remember & Exit**.

CIM starts running by itself on the grid the Programmable Block is on.

---

## Basic cargo setup

Add one CIM tag to each cargo container name.

| Tag | Stores |
| --- | --- |
| `[CIM:Ore]` | Ore |
| `[CIM:Ingot]` | Ingots |
| `[CIM:Component]` | Components |
| `[CIM:Tool]` | Tools |
| `[CIM:Ammo]` | Ammo |
| `[CIM:Bottle]` | Bottles |
| `[CIM:All]` | Unknown items / missing categories |

Example cargo names:

```text
Large Cargo [CIM:Component]
Large Cargo [CIM:Ore]
Large Cargo [CIM:Ingot]
Large Cargo [CIM:All]
```

If you do not tag cargo containers, CIM can try to auto-tag empty/unlabeled cargo containers for you.

### Important `[CIM:All]` behavior

- `[CIM:All]` is mostly for unknown items or categories that do not have a container.
- If `[CIM:Ingot]` exists but is full, ingots are left where they are instead of being dumped into `[CIM:All]`.
- Example: nickel ingots should go to `[CIM:Ingot]`, not `[CIM:All]`.

---

## LCD setup

### Main status LCD

Name an LCD:

```text
LCD [CIM:Status]
```

This shows CIM status, targets, gas totals, transfer counts, and runtime budget.

---

### One-container or one-tank LCD

Name an LCD:

```text
LCD [CIM:ContainerLCD]
```

Then put this in the LCD **Custom Data**:

```text
Container=Large Cargo [CIM:Component]
```

Tank example:

```text
Container=Hydrogen Tank
```

This LCD shows only the matched cargo container or tank.

---

### Whole-grid item totals LCD

Name an LCD:

```text
LCD [CIM:ItemsLCD]
```

Then put one category in the LCD **Custom Data**:

```text
Category=Component
```

Other supported categories:

```text
Category=Ore
Category=Ingot
Category=Ammo
Category=Tool
Category=Bottle
Category=All
```

This LCD shows totals from the entire managed grid, not just one cargo container.

Only LCDs with `[CIM:ItemsLCD]` receive these item-total updates.

---

## Special stocked containers

Use this when you want a cargo container to always keep certain items stocked.

Name a cargo container:

```text
Welder Supplies [CIM:Special]
```

Put item targets in that container's **Custom Data**:

```text
Component/SteelPlate=200
Component/Construction=100
HydrogenBottle=2
```

CIM will try to pull those items into that container.

---

## Modded items and learned names

CIM can learn item names when it sees them.

Name an LCD:

```text
LCD [CIM:LearnedLCD]
```

This LCD shows discovered item names that can be copied into special stocked containers.

CIM also has friendly display aliases for these tech items:

| Raw subtype | Display name |
| --- | --- |
| `Tech2x` | Common Tech |
| `Tech4x` | Rare Tech |
| `Tech16x` | Prosonic |
| `Tech32x` | Prosonic Tech |

These are treated as components for sorting.

---

## Docked ships

By default, CIM may manage connected ships on the same construct.

If you do **not** want CIM to touch a docked ship, tag the connector:

```text
Connector [CIM:NoDock]
```

When that connector is connected, CIM skips the docked grid.

---

## Reactors

CIM can top up reactors with uranium.

Default amount:

```text
5 uranium ingots per reactor
```

It does **not** dump all uranium into reactors.

---

## Performance settings

For very large bases, lower these near the top of **CaseysInventoryManagement.cs**:

```csharp
const int MaxTransfersPerRun = 16;
const int MaxItemLcdUpdatesPerRun = 2;
const double RuntimeCheckLimitMs = 0.80;
const double InstructionBudgetPercent = 0.80;
```

Suggested lower-lag values:

```csharp
const int MaxTransfersPerRun = 6;
const int MaxItemLcdUpdatesPerRun = 1;
const double RuntimeCheckLimitMs = 0.50;
const double InstructionBudgetPercent = 0.60;
```

Lower values reduce lag but make sorting and LCD updates slower.

---

## Tag cheat sheet

| Tag | Use |
| --- | --- |
| `[CIM:Ore]` | Ore cargo |
| `[CIM:Ingot]` | Ingot cargo |
| `[CIM:Component]` | Component cargo |
| `[CIM:Tool]` | Tool cargo |
| `[CIM:Ammo]` | Ammo cargo |
| `[CIM:Bottle]` | Bottle cargo |
| `[CIM:All]` | Unknown / missing-category cargo |
| `[CIM:Status]` | Main status LCD |
| `[CIM:ContainerLCD]` | LCD for one cargo container or tank |
| `[CIM:Container]` | Short version of `[CIM:ContainerLCD]` |
| `[CIM:ItemsLCD]` | LCD for whole-grid item totals |
| `[CIM:LearnedLCD]` | LCD for learned modded item names |
| `[CIM:Special]` | Cargo that should stay stocked |
| `[CIM:Ignore]` | Ignore this block |
| `[CIM:Drain]` | Force CIM to empty this block |
| `[CIM:NoSort]` | Do not sort this block/grid |
| `[CIM:NoDock]` | Ignore docked ship on this connector |
| `[CIM:P1]` | Priority cargo. Lower number fills first |

---

## Optional manual run words

Normal use does **not** require manual run words.

If you open the Programmable Block and press **Run**, you can enter one of these:

| Word | Action |
| --- | --- |
| `status` | Refreshes status output |
| `rescan` | Finds blocks again |
| `pause` | Pauses CIM |
| `resume` | Resumes CIM |
| `rename` | Updates cargo fill names |

---

## Notes

- CIM only works inside a Space Engineers **Programmable Block**.
- It is not a normal desktop C# program.
- It manages the grid/construct the Programmable Block can access.
- LCD text size can be changed manually in the LCD settings if a list is too long.
