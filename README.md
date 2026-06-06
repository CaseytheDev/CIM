# CIM - Casey's Inventory Management

CIM is a Space Engineers inventory script built for the **Stone Industries** server.

> **Work in progress:** CIM is still being built and tested. So far it is being tuned to produce less lag than ISY's Inventory Manager on current servers, especially since ISY's has not had a major update in at least 3 years.

It runs in a Programmable Block and handles the usual base inventory stuff: sorting cargo, keeping some containers stocked, showing cargo/tank info on LCDs, listing item totals, and topping reactors with a small amount of uranium.

No timer block needed.

## Support

Stone Industries Discord:

https://discord.gg/sigaming

My Discord for CIM support/suggestions:

https://discord.gg/RsH33Wr6Vy

---

## Installing from GitHub

1. Open `CIM.cs` in this repo.
2. Click **Raw**.
3. Copy the whole page.
4. In Space Engineers, open a **Programmable Block**.
5. Click **Edit**.
6. Paste the script in.
7. Click **Check Code**.
8. Click **Remember & Exit**.

After that, it should start running on its own.

---

## Cargo tags

Put one of these tags in the name of each cargo container:

| Tag | Used for |
| --- | --- |
| `[CIM:Ore]` | ore |
| `[CIM:Ingot]` | ingots |
| `[CIM:Component]` | components |
| `[CIM:Tool]` | tools |
| `[CIM:Ammo]` | ammo |
| `[CIM:Bottle]` | bottles |
| `[CIM:All]` | all normal item types / missing typed categories |
| `[CIM:Unknown]` | unknown or modded items CIM cannot classify |

Example names:

```text
Large Cargo [CIM:Component]
Large Cargo [CIM:Ore]
Large Cargo [CIM:Ingot]
Large Cargo [CIM:All]
Large Cargo [CIM:Unknown]
```

ISYS-style plain names also work:

```text
Large Cargo Ores
Large Cargo Ingots
Large Cargo Components
Large Cargo Tools
Large Cargo Ammo
Large Cargo Bottles
Large Cargo All Items
Large Cargo Unknown Items
```

`Locked`, `Hidden`, `[No Sorting]`, `[No IIM]`, and `[No CIM]` are also understood as skip/no-sort style keywords.

If you do not tag any cargo, CIM can try to auto-tag empty/unlabeled cargo containers.

`[CIM:All]` is the all-purpose fallback for normal item types like ore, ingots, tools, ammo, bottles, and components when a specific tagged container is missing.

`[CIM:Unknown]` is for anything CIM cannot classify.

If an ingot container exists but is full, extra ingots stay where they are instead of getting dumped into `[CIM:All]`.

---

## LCDs

### Status LCD

Name an LCD:

```text
LCD [CIM:Status]
```

Shows what the script is doing.

### One cargo/tank LCD

Name an LCD:

```text
LCD [CIM:ContainerLCD]
```

Put this in the LCD Custom Data:

```text
Container=Large Cargo [CIM:Component]
```

Tank example:

```text
Container=Hydrogen Tank
```

### Whole-grid totals LCD

Put the item type in the LCD name/title with `[CIM:ItemsLCD]`.

Component LCD example:

```text
Components [CIM:ItemsLCD]
```

Ore LCD example:

```text
Ore [CIM:ItemsLCD]
```

Other choices:

```text
Ingots [CIM:ItemsLCD]
Ammo [CIM:ItemsLCD]
Tools [CIM:ItemsLCD]
Bottles [CIM:ItemsLCD]
All [CIM:ItemsLCD]
Unknown [CIM:ItemsLCD]
```

This shows totals from the whole managed grid, not just one box.
CIM scrolls long item LCDs downward and snaps back to the top when it reaches the bottom.

---

## Stocked containers

For a box that should always keep certain items, name it like this:

```text
Welder Supplies [CIM:Special]
```

Then put the wanted amounts in that cargo container's Custom Data:

```text
Component/SteelPlate=200
Component/Construction=100
HydrogenBottle=2
```

CIM will try to keep those items in that box.

---

## Modded items

For learned item names, add an LCD named:

```text
LCD [CIM:LearnedLCD]
```

That LCD shows the item names CIM has seen. You can copy those names into stocked containers.


---

## Docked ships

By default, CIM can work across your own or same-faction connected grids. So if you dock your ship to your base, CIM can sort it and top up its reactors if the blocks are yours or shared with your faction.

It skips blocks that are not yours or faction-shared. That means it should not pull from or push into allied/enemy-owned grids.

If you do not want CIM touching a docked ship, tag the connector:

```text
Connector [CIM:NoDock]
```

When that connector is connected, the docked grid gets skipped.

If you only want CIM to top up the docked ship's reactors, but **not** pull/sort its cargo, put this on either connector:

```text
Connector [NoPull]
```

With `[NoPull]`, CIM skips that ship's inventory and only does reactor uranium stabilizing if it can.

---

## Reactors

CIM can top up reactors with uranium on your own grid, faction-shared grid, or docked ship.

Default is:

```text
5 uranium ingots per reactor
```

Change the normal amount near the top of the script:

```csharp
double UraniumIngotsPerReactor = 5;
```

Or override one reactor by putting this in that reactor's Custom Data:

```text
Uranium=10
```

It does not dump all uranium into reactors.

Allied/enemy-owned reactors are skipped unless they are actually shared to your faction.

---

## If it lags

For big bases, lower these near the top of the script:

CIM works in task steps so it does not try to do everything at once. The normal order is gas checks, ore sorting, ingot sorting, other sorting, special container restocking, then reactor top-off.

Item total LCDs are cached. CIM only writes new item LCD text when the displayed totals/text actually changed, so LCDs should not keep blanking while inventory is still being counted.

```csharp
const int MaxTransfersPerRun = 6;
const int RescanEveryRuns = 300;
const int MaxRenameUpdatesPerRun = 6;
const int CountEveryRuns = 1;
const int CountRefreshEveryRuns = 60;
const int LcdEveryRuns = 1;
const int MaxRescanBlocksPerRun = 200;
const int MaxCountBlocksPerRun = 16;
const int MaxSortSourcesPerTask = 10;
const int MaxReactorChecksPerTask = 8;
const int MaxContainerLcdUpdatesPerRun = 1;
const int MaxContainerDisplayLines = 60;
const int MaxItemLcdUpdatesPerRun = 1;
const int MaxItemTotalLines = 80;
const int ItemLcdVisibleLines = 18;
const double RuntimeCheckLimitMs = 1.00;
const double InstructionBudgetPercent = 0.60;
```

Lower values mean less lag, but slower sorting/LCD updates.

---

## Tag list

| Tag | What it does |
| --- | --- |
| `[CIM:Ore]` | ore cargo |
| `[CIM:Ingot]` | ingot cargo |
| `[CIM:Component]` | component cargo |
| `[CIM:Tool]` | tool cargo |
| `[CIM:Ammo]` | ammo cargo |
| `[CIM:Bottle]` | bottle cargo |
| `[CIM:All]` | all normal item types / fallback cargo |
| `[CIM:Unknown]` | unknown or unclassified cargo |
| `[CIM:Status]` | status LCD |
| `[CIM:ContainerLCD]` | one cargo/tank LCD |
| `[CIM:Container]` | short version of container LCD tag |
| `[CIM:ItemsLCD]` | whole-grid item totals LCD |
| `[CIM:LearnedLCD]` | learned item names LCD |
| `[CIM:Special]` | stocked cargo container |
| `[CIM:Ignore]` | ignore this block |
| `[CIM:Drain]` | force this block to empty |
| `[CIM:NoSort]` | do not sort this block/grid |
| `[CIM:NoDock]` | skip docked ship on this connector |
| `[NoPull]` | do not pull/sort docked ship cargo, only top up reactors |
| `[CIM:P1]` | priority cargo, lower number fills first |

---

## Notes

- This is for a Space Engineers Programmable Block.
- It is not a normal C# app.
- If an LCD list is too long, lower the LCD text size in-game.
