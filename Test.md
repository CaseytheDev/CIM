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

Plain cargo names also work:

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

## New and improved autocrafting

CIM now has a simpler component autocrafting setup. It can auto-load all known component names into an LCD's Custom Data, then selected assemblers will craft from those rules in small task steps.

### 1) Select which assemblers can autocraft

Put this tag on every assembler CIM is allowed to use:

```text
Assembler [CIM:Craft]
```

CIM only queues autocraft jobs on assemblers with `[CIM:Craft]`. When it queues a job, it also switches that assembler to assembly mode.

### 2) Make a component autocraft LCD

Make a component item LCD. CIM will auto-load all known component autocraft lines into that LCD's Custom Data:

```text
Components [CIM:ItemsLCD]
```

You can also use a dedicated craft rules LCD:

```text
LCD [CIM:CraftLCD]
```

After CIM finishes its first full inventory count, open that LCD's Custom Data. CIM will add lines for normal components and learned/modded components.

### 3) Enable the LCD rules and set min/max amounts

In that LCD Custom Data, set autocrafting on:

```text
AutoCraft=true
```

Then edit the min/max values:

```text
AutoCraft=true
SteelPlate=1000,2000
Construction=500,1000
InteriorPlate=500,1000
```

CIM starts new auto-loaded lines as `0,0`, so they are only listed until you change them.

First number = minimum to keep.
Second number = craft up to this amount.

Example: `SteelPlate=1000,2000` means if steel plates drop below 1000, CIM queues enough to bring the total plus existing assembler queue up to 2000.

### 4) Optional rule locations

The recommended place for autocraft rules is the component LCD Custom Data, because CIM can keep adding newly learned component lines there.

These also work:

- `[CIM:Component]` cargo container Custom Data
- `[CIM:Craft]` assembler Custom Data, if you want one assembler to carry its own extra rules

Duplicate rules are skipped, so the first matching component rule CIM reads wins.

### 5) Low-lag behavior

CIM uses the last completed inventory count, checks existing assembler queues, and only adds a few craft jobs per task step so it does not spike the server.

Autocrafting runs in the normal CIM task order after gas, ore, ingot, and other sorting tasks.

For new or modded components, use a learned LCD:

```text
LCD [CIM:LearnedLCD]
```

When CIM sees components, the component craft LCD Custom Data is updated with missing component rule lines. The learned LCD also lists autocraft examples.

Autocrafting learning has two parts:

1. **Item learning** — CIM learns the component name when it sees the item in inventory.
2. **Recipe learning** — CIM learns the assembler blueprint when it sees that item queued in an assembler.

To teach a new or modded autocraft recipe:

1. Put `[CIM:Craft]` on an assembler, or use `[CIM:LearnCraft]` on a teaching assembler.
2. Manually queue one of the component in that assembler.
3. Let CIM run a craft task step.
4. CIM records the blueprint and saves it for future autocrafting.
5. Use the component name in the autocraft LCD Custom Data, such as `ModdedComponent=100,200`.

`[CIM:LearnCraft]` is useful if you want an assembler to teach recipes without being one of the normal autocraft assemblers.

Autocrafting is also stepped for lag control. CIM checks only a small number of craft rules per task step and only queues a few jobs at a time.

Quick checklist:

1. Name assembler `Assembler [CIM:Craft]`.
2. Name LCD `Components [CIM:ItemsLCD]` or `LCD [CIM:CraftLCD]`.
3. Wait for CIM to finish the first inventory count.
4. Open the LCD Custom Data.
5. Set `AutoCraft=true`.
6. Change wanted lines from `0,0` to real min/max values, such as `SteelPlate=1000,2000`.
7. For unknown recipes, queue one manually in `[CIM:Craft]` or `[CIM:LearnCraft]` so CIM can learn its blueprint.

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

CIM works in task steps so it does not try to do everything at once. The normal order is gas checks, ore sorting, ingot sorting, other sorting, autocrafting, special container restocking, then reactor top-off.

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
const int MaxCraftRulesCheckedPerTask = 12;
const int MaxCraftQueueAddsPerTask = 4;
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
| `[CIM:CraftLCD]` | auto-filled component autocraft rules LCD |
| `[CIM:Craft]` | assembler autocrafting min/max component rules |
| `[CIM:LearnCraft]` | assembler used to learn autocraft blueprints from its queue |
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
