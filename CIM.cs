// Casey's Inventory Management (CIM)
// Original Space Engineers Programmable Block script.
// Paste this whole file into a programmable block and compile.
    // =========================================================
    // Setup tags
    // =========================================================
    // Cargo categories: [CIM:Ore] [CIM:Ingot] [CIM:Component] [CIM:Tool] [CIM:Ammo] [CIM:Bottle] [CIM:All] [CIM:Unknown]
    // If categories are missing, CIM can auto-tag empty/unlabeled cargo containers for you.
    // Special loadouts: [CIM:Special] on cargo + Custom Data lines like Component/SteelPlate=200

    const string TagOre = "[CIM:Ore]";
    const string TagIngot = "[CIM:Ingot]";
    const string TagComponent = "[CIM:Component]";
    const string TagTool = "[CIM:Tool]";
    const string TagAmmo = "[CIM:Ammo]";
    const string TagBottle = "[CIM:Bottle]";
    const string TagAll = "[CIM:All]";
    const string TagUnknown = "[CIM:Unknown]";
    const string TagSpecial = "[CIM:Special]";
    const string TagStatus = "[CIM:Status]";
    const string TagContainerLCD = "[CIM:ContainerLCD]";
    const string TagContainerLCDShort = "[CIM:Container]";
    const string TagItemsLCD = "[CIM:ItemsLCD]";
    const string TagLearnedLCD = "[CIM:LearnedLCD]";
    const string TagIgnore = "[CIM:Ignore]";
    const string TagDrain = "[CIM:Drain]";
    const string TagNoSort = "[CIM:NoSort]";
    const string TagNoDock = "[CIM:NoDock]";
    const string TagNoPull = "[CIM:NoPull]";
    const string TagNoPullShort = "[NoPull]";

    // Performance knobs. Lower MaxTransfersPerRun for giant bases.
    const int MaxTransfersPerRun = 16;
    const int RescanEveryRuns = 30;
    const int RenameEveryRuns = 10;
    const int MaxItemLcdUpdatesPerRun = 2;
    const int ItemLcdVisibleLines = 18;
    const double RuntimeCheckLimitMs = 0.80;
    const double InstructionBudgetPercent = 0.80;

    // Behavior knobs.
    bool IncludeConnectedSameConstruct = true;
    bool OnlySameFaction = true;
    bool DrainUntaggedCargo = true;
    bool ShowFillPercentInNames = true;
    bool AutoAssignContainers = true;
    bool AutoAssignOnlyEmptyCargo = false;
    bool AutoLearnItems = true;
    bool EnableReactorBalancing = true;
    double UraniumIngotsPerReactor = 5;
    double DefaultFillLimitPercent = 98;

    List<IMyTerminalBlock> _blocks = new List<IMyTerminalBlock>();
    List<IMyTerminalBlock> _sources = new List<IMyTerminalBlock>();
    List<IMyCubeGrid> _blockedDockedGrids = new List<IMyCubeGrid>();
    List<IMyCubeGrid> _noPullDockedGrids = new List<IMyCubeGrid>();
    List<IMyGasTank> _gasTanks = new List<IMyGasTank>();
    List<IMyReactor> _reactors = new List<IMyReactor>();
    List<IMyTextSurface> _statusSurfaces = new List<IMyTextSurface>();
    List<IMyTextSurface> _learnedSurfaces = new List<IMyTextSurface>();
    List<ContainerDisplay> _containerDisplays = new List<ContainerDisplay>();
    List<ItemDisplay> _itemDisplays = new List<ItemDisplay>();
    List<MyInventoryItem> _items = new List<MyInventoryItem>();
    List<IMyCargoContainer> _unassignedCargo = new List<IMyCargoContainer>();
    List<TargetBin> _allTargets = new List<TargetBin>();
    List<TargetBin> _specialTargets = new List<TargetBin>();
    Dictionary<string, List<TargetBin>> _targets = new Dictionary<string, List<TargetBin>>();
    Dictionary<string, MyFixedPoint> _counts = new Dictionary<string, MyFixedPoint>();
    Dictionary<string, MyFixedPoint> _itemTotals = new Dictionary<string, MyFixedPoint>();
    Dictionary<string, string> _learnedItems = new Dictionary<string, string>();
    Dictionary<long, int> _itemLcdScrollLines = new Dictionary<long, int>();
    List<string> _displayLines = new List<string>();
    StringBuilder _text = new StringBuilder(4096);

    int _runCounter;
    int _renameCounter;
    int _sourceCursor;
    int _specialCursor;
    int _itemLcdCursor;
    int _lastTransferCount;
    int _specialTransferCount;
    int _reactorTransferCount;
    int _totalTransfers;
    int _autoAssignedCount;
    int _learnedNewThisRun;
    double _hydrogenFilled;
    double _hydrogenCapacity;
    double _oxygenFilled;
    double _oxygenCapacity;
    bool _paused;
    string _lastMessage = "Starting";

    string[] _categoryNames = new string[]
    {
        "Ore", "Ingot", "Component", "Tool", "Ammo", "Bottle", "All", "Unknown"
    };

    string[] _autoAssignOrder = new string[]
    {
        "All", "Unknown", "Component", "Ore", "Ingot", "Tool", "Ammo", "Bottle"
    };

    class TargetBin
    {
        public IMyTerminalBlock Block;
        public IMyInventory Inventory;
        public string Category;
        public int Priority;
        public double FillLimitPercent;
        public bool Special;
        public Dictionary<string, MyFixedPoint> Loadout = new Dictionary<string, MyFixedPoint>();
    }

    class ContainerDisplay
    {
        public IMyTerminalBlock Block;
        public IMyTextSurface Surface;
        public string Match;
    }

    class ItemDisplay
    {
        public IMyTerminalBlock Block;
        public IMyTextSurface Surface;
        public string Category;
        public int ScrollLine;
    }

    public Program()
    {
        Runtime.UpdateFrequency = UpdateFrequency.Update100;
        InitTargets();
        LoadLearnedItems();
        Rescan();
    }

    public void Save()
    {
        SaveLearnedItems();
    }

    public void Main(string argument, UpdateType updateSource)
    {
        argument = (argument ?? "").Trim().ToLowerInvariant();

        if (argument == "pause")
        {
            _paused = true;
            _lastMessage = "Paused";
        }
        else if (argument == "resume" || argument == "start")
        {
            _paused = false;
            _lastMessage = "Running";
        }
        else if (argument == "rescan" || argument == "scan")
        {
            Rescan();
            _lastMessage = "Manual rescan complete";
        }
        else if (argument == "rename")
        {
            UpdateContainerNames();
            _lastMessage = "Fill names updated";
        }
        else if (argument == "status" || argument == "help" || argument == "")
        {
            // Status is printed below; empty timer runs continue sorting.
        }
        else
        {
            _lastMessage = "Unknown argument: " + argument;
        }

        _lastTransferCount = 0;
        _specialTransferCount = 0;
        _reactorTransferCount = 0;
        _learnedNewThisRun = 0;

        if (!_paused && argument != "help")
        {
            _runCounter++;
            _renameCounter++;

            if (_runCounter >= RescanEveryRuns)
            {
                _runCounter = 0;
                Rescan();
            }

            FillSpecialLoadouts();
            BalanceReactors();
            SortStep();

            if (_lastTransferCount > 0)
                _totalTransfers += _lastTransferCount;

            if (ShowFillPercentInNames && _renameCounter >= RenameEveryRuns)
            {
                _renameCounter = 0;
                UpdateContainerNames();
            }
        }

        CountItems();
        CountGasTanks();
        WriteStatus(argument == "help");
        WriteContainerDisplays();
        WriteItemDisplays();
        WriteLearnedDisplays();
    }

    void InitTargets()
    {
        _targets.Clear();
        for (int i = 0; i < _categoryNames.Length; i++)
            _targets[_categoryNames[i]] = new List<TargetBin>();
    }

    void Rescan()
    {
        InitTargets();
        _sources.Clear();
        _blockedDockedGrids.Clear();
        _noPullDockedGrids.Clear();
        _gasTanks.Clear();
        _reactors.Clear();
        _statusSurfaces.Clear();
        _learnedSurfaces.Clear();
        _containerDisplays.Clear();
        _itemDisplays.Clear();
        _unassignedCargo.Clear();
        _allTargets.Clear();
        _specialTargets.Clear();
        _blocks.Clear();

        GridTerminalSystem.GetBlocks(_blocks);
        FindBlockedDockedGrids();

        for (int i = 0; i < _blocks.Count; i++)
        {
            IMyTerminalBlock block = _blocks[i];
            if (!IsAllowedGrid(block) || HasToken(block, TagIgnore) || IsNoSortBlocked(block))
                continue;

            if (IsNoPullDockedGrid(block.CubeGrid))
            {
                IMyReactor noPullReactor = block as IMyReactor;
                if (noPullReactor != null)
                    _reactors.Add(noPullReactor);

                continue;
            }

            RegisterStatusSurface(block);
            RegisterContainerDisplay(block);
            RegisterItemDisplay(block);
            RegisterLearnedSurface(block);

            IMyGasTank gasTank = block as IMyGasTank;
            if (gasTank != null)
                _gasTanks.Add(gasTank);

            IMyReactor reactor = block as IMyReactor;
            if (reactor != null)
                _reactors.Add(reactor);

            if (!block.HasInventory)
                continue;

            TargetBin target = MakeTarget(block);
            if (target != null)
            {
                RegisterTarget(target);
                continue;
            }

            IMyCargoContainer cargo = block as IMyCargoContainer;
            if (cargo != null && IsAutoAssignableCargo(cargo))
            {
                _unassignedCargo.Add(cargo);
                continue;
            }

            if (IsSafeSource(block))
                _sources.Add(block);
        }

        AutoAssignMissingContainers();

        for (int i = 0; i < _unassignedCargo.Count; i++)
        {
            IMyCargoContainer cargo = _unassignedCargo[i];
            if (cargo != null && cargo.HasInventory && IsSafeSource(cargo))
                _sources.Add(cargo);
        }

        SortTargetLists();

        if (_sourceCursor >= _sources.Count)
            _sourceCursor = 0;
        if (_specialCursor >= _specialTargets.Count)
            _specialCursor = 0;
        if (_itemLcdCursor >= _itemDisplays.Count)
            _itemLcdCursor = 0;
    }

    void RegisterTarget(TargetBin target)
    {
        if (target.Special)
        {
            _specialTargets.Add(target);
        }
        else
        {
            _targets[target.Category].Add(target);
            _sources.Add(target.Block); // Lets typed containers clean misplaced items out.
        }

        _allTargets.Add(target);
    }

    bool IsAutoAssignableCargo(IMyCargoContainer cargo)
    {
        if (!AutoAssignContainers || cargo == null)
            return false;

        if (HasAnyCimTag(cargo) || GetContainerCategory(cargo) != "")
            return false;

        if (AutoAssignOnlyEmptyCargo && cargo.GetInventory(0).CurrentVolume > (MyFixedPoint)0)
            return false;

        return true;
    }

    void AutoAssignMissingContainers()
    {
        if (!AutoAssignContainers || _unassignedCargo.Count == 0)
            return;

        for (int i = 0; i < _autoAssignOrder.Length; i++)
        {
            string category = _autoAssignOrder[i];
            if (_targets[category].Count > 0)
                continue;

            IMyCargoContainer cargo = TakeBestUnassignedCargo();
            if (cargo == null)
                return;

            cargo.CustomName = RemoveFillTag(cargo.CustomName) + " [CIM:" + category + "] [AUTO]";

            TargetBin target = MakeTarget(cargo);
            if (target != null)
            {
                RegisterTarget(target);
                _autoAssignedCount++;
            }
        }
    }

    IMyCargoContainer TakeBestUnassignedCargo()
    {
        if (_unassignedCargo.Count == 0)
            return null;

        int bestIndex = 0;
        double bestVolume = -1;

        for (int i = 0; i < _unassignedCargo.Count; i++)
        {
            IMyCargoContainer cargo = _unassignedCargo[i];
            if (cargo == null || !cargo.HasInventory)
                continue;

            double volume = (double)cargo.GetInventory(0).MaxVolume;
            if (volume > bestVolume)
            {
                bestVolume = volume;
                bestIndex = i;
            }
        }

        IMyCargoContainer best = _unassignedCargo[bestIndex];
        _unassignedCargo.RemoveAt(bestIndex);
        return best;
    }

    bool IsAllowedGrid(IMyTerminalBlock block)
    {
        if (block == null)
            return false;

        if (OnlySameFaction && !IsSameFactionOrMine(block))
            return false;

        if (IsBlockedDockedGrid(block.CubeGrid))
            return false;

        if (IncludeConnectedSameConstruct)
            return block.IsSameConstructAs(Me);

        return block.CubeGrid == Me.CubeGrid;
    }

    bool IsSameFactionOrMine(IMyTerminalBlock block)
    {
        if (block == null)
            return false;

        if (block.OwnerId == 0)
            return true;

        MyRelationsBetweenPlayerAndBlock relation = block.GetUserRelationToOwner(Me.OwnerId);
        return relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare;
    }

    void FindBlockedDockedGrids()
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            IMyShipConnector connector = _blocks[i] as IMyShipConnector;
            if (connector == null)
                continue;

            if (!connector.IsSameConstructAs(Me))
                continue;

            if (!HasToken(connector, TagNoDock) && !HasToken(connector, TagNoSort))
            {
                if (!HasNoPullToken(connector))
                    continue;

                if (connector.Status != MyShipConnectorStatus.Connected || connector.OtherConnector == null)
                    continue;

                if (connector.CubeGrid == Me.CubeGrid)
                    AddNoPullDockedGrid(connector.OtherConnector.CubeGrid);
                else
                    AddNoPullDockedGrid(connector.CubeGrid);

                continue;
            }

            if (connector.Status != MyShipConnectorStatus.Connected || connector.OtherConnector == null)
                continue;

            AddBlockedDockedGrid(connector.OtherConnector.CubeGrid);
        }
    }

    void AddNoPullDockedGrid(IMyCubeGrid grid)
    {
        if (grid == null || grid == Me.CubeGrid)
            return;

        for (int i = 0; i < _noPullDockedGrids.Count; i++)
        {
            if (_noPullDockedGrids[i] == grid)
                return;
        }

        _noPullDockedGrids.Add(grid);
    }

    void AddBlockedDockedGrid(IMyCubeGrid grid)
    {
        if (grid == null || grid == Me.CubeGrid)
            return;

        for (int i = 0; i < _blockedDockedGrids.Count; i++)
        {
            if (_blockedDockedGrids[i] == grid)
                return;
        }

        _blockedDockedGrids.Add(grid);
    }

    bool IsBlockedDockedGrid(IMyCubeGrid grid)
    {
        for (int i = 0; i < _blockedDockedGrids.Count; i++)
        {
            if (_blockedDockedGrids[i] == grid)
                return true;
        }

        return false;
    }

    bool IsNoPullDockedGrid(IMyCubeGrid grid)
    {
        for (int i = 0; i < _noPullDockedGrids.Count; i++)
        {
            if (_noPullDockedGrids[i] == grid)
                return true;
        }

        return false;
    }

    bool IsNoSortBlocked(IMyTerminalBlock block)
    {
        return HasToken(block, TagNoSort) || HasToken(block, TagNoDock);
    }

    bool IsSafeSource(IMyTerminalBlock block)
    {
        if (HasToken(block, TagDrain)) return true;
        if (block is IMyCargoContainer) return DrainUntaggedCargo;
        if (block is IMyShipConnector) return true;
        if (block is IMyShipDrill) return true;
        if (block is IMyShipGrinder) return true;
        if (block is IMyShipWelder) return true;
        if (block is IMyCollector) return true;
        if (block is IMyCockpit) return true;
        if (block is IMyRemoteControl) return true;
        if (block is IMyAssembler) return true;
        if (block is IMyRefinery) return true;
        return false;
    }

    TargetBin MakeTarget(IMyTerminalBlock block)
    {
        if (!(block is IMyCargoContainer))
            return null;

        bool special = HasToken(block, TagSpecial);
        string category = special ? "Special" : GetContainerCategory(block);
        if (category == "")
            return null;

        TargetBin bin = new TargetBin();
        bin.Block = block;
        bin.Inventory = block.GetInventory(0);
        bin.Category = category;
        bin.Priority = GetPriority(block);
        bin.FillLimitPercent = GetFillLimitPercent(block);
        bin.Special = special;

        if (special)
            ParseLoadout(block.CustomData, bin.Loadout);

        return bin;
    }

    string GetContainerCategory(IMyTerminalBlock block)
    {
        string data = (block.CustomName + "\n" + block.CustomData).ToLowerInvariant();
        if (Contains(data, TagOre) || Contains(data, "ores")) return "Ore";
        if (Contains(data, TagIngot) || Contains(data, "ingots")) return "Ingot";
        if (Contains(data, TagComponent) || Contains(data, "components")) return "Component";
        if (Contains(data, TagTool) || Contains(data, "tools")) return "Tool";
        if (Contains(data, TagAmmo) || Contains(data, "ammo")) return "Ammo";
        if (Contains(data, TagBottle) || Contains(data, "bottles")) return "Bottle";
        if (Contains(data, TagAll) || Contains(data, "all items")) return "All";
        if (Contains(data, TagUnknown) || Contains(data, "unknown items")) return "Unknown";
        return "";
    }

    int GetPriority(IMyTerminalBlock block)
    {
        string data = block.CustomName + "\n" + block.CustomData;
        int token = data.IndexOf("[CIM:P", StringComparison.OrdinalIgnoreCase);
        if (token >= 0)
        {
            int start = token + 6;
            int end = data.IndexOf("]", start, StringComparison.OrdinalIgnoreCase);
            if (end > start)
            {
                int parsed;
                if (int.TryParse(data.Substring(start, end - start), out parsed))
                    return parsed;
            }
        }

        string value = GetSettingValue(block.CustomData, "Priority");
        int priority;
        if (int.TryParse(value, out priority))
            return priority;

        return 5;
    }

    double GetFillLimitPercent(IMyTerminalBlock block)
    {
        string value = GetSettingValue(block.CustomData, "Limit");
        if (value == "")
            value = GetSettingValue(block.CustomData, "FillLimit");

        double limit;
        if (double.TryParse(value, out limit))
        {
            if (limit < 1) limit = 1;
            if (limit > 100) limit = 100;
            return limit;
        }

        return DefaultFillLimitPercent;
    }

    void SortTargetLists()
    {
        for (int i = 0; i < _categoryNames.Length; i++)
            _targets[_categoryNames[i]].Sort(CompareTargets);

        _specialTargets.Sort(CompareTargets);
    }

    int CompareTargets(TargetBin a, TargetBin b)
    {
        int priority = a.Priority.CompareTo(b.Priority);
        if (priority != 0)
            return priority;

        MyFixedPoint aFree = a.Inventory.MaxVolume - a.Inventory.CurrentVolume;
        MyFixedPoint bFree = b.Inventory.MaxVolume - b.Inventory.CurrentVolume;
        return ((double)bFree).CompareTo((double)aFree);
    }

    void RegisterStatusSurface(IMyTerminalBlock block)
    {
        if (!HasToken(block, TagStatus))
            return;

        IMyTextPanel panel = block as IMyTextPanel;
        if (panel != null)
        {
            panel.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
            panel.Font = "Monospace";
            _statusSurfaces.Add(panel);
            return;
        }

        IMyTextSurfaceProvider provider = block as IMyTextSurfaceProvider;
        if (provider != null && provider.SurfaceCount > 0)
        {
            IMyTextSurface surface = provider.GetSurface(0);
            surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
            surface.Font = "Monospace";
            _statusSurfaces.Add(surface);
        }
    }

    void RegisterContainerDisplay(IMyTerminalBlock block)
    {
        if (!HasToken(block, TagContainerLCD) && !HasToken(block, TagContainerLCDShort))
            return;

        IMyTextSurface surface = GetFirstTextSurface(block);
        if (surface == null)
            return;

        surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
        surface.Font = "Monospace";

        ContainerDisplay display = new ContainerDisplay();
        display.Block = block;
        display.Surface = surface;
        display.Match = GetContainerDisplayMatch(block);
        _containerDisplays.Add(display);
    }

    void RegisterLearnedSurface(IMyTerminalBlock block)
    {
        if (!HasToken(block, TagLearnedLCD))
            return;

        IMyTextSurface surface = GetFirstTextSurface(block);
        if (surface == null)
            return;

        surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
        surface.Font = "Monospace";
        _learnedSurfaces.Add(surface);
    }

    void RegisterItemDisplay(IMyTerminalBlock block)
    {
        if (!HasToken(block, TagItemsLCD))
            return;

        IMyTextSurface surface = GetFirstTextSurface(block);
        if (surface == null)
            return;

        surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
        surface.Font = "Monospace";

        ItemDisplay display = new ItemDisplay();
        display.Block = block;
        display.Surface = surface;
        display.Category = GetItemDisplayCategory(block);
        _itemLcdScrollLines.TryGetValue(block.EntityId, out display.ScrollLine);
        _itemDisplays.Add(display);
    }

    IMyTextSurface GetFirstTextSurface(IMyTerminalBlock block)
    {
        IMyTextPanel panel = block as IMyTextPanel;
        if (panel != null)
            return panel;

        IMyTextSurfaceProvider provider = block as IMyTextSurfaceProvider;
        if (provider != null && provider.SurfaceCount > 0)
            return provider.GetSurface(0);

        return null;
    }

    string GetContainerDisplayMatch(IMyTerminalBlock block)
    {
        string match = GetSettingValue(block.CustomData, "Container");
        if (match == "") match = GetSettingValue(block.CustomData, "Target");
        if (match == "") match = GetSettingValue(block.CustomData, "Match");
        if (match != "") return match.Trim();

        string name = block.CustomName;
        name = RemoveToken(name, TagContainerLCD);
        name = RemoveToken(name, TagContainerLCDShort);
        name = RemoveFillTag(name);
        return name.Trim();
    }

    string GetItemDisplayCategory(IMyTerminalBlock block)
    {
        string name = block.CustomName;
        name = RemoveToken(name, TagItemsLCD);
        name = RemoveFillTag(name).Trim();
        string nameCategory = NormalizeCategory(name);
        if (nameCategory != "All" || Contains(name, "all"))
            return nameCategory;

        string category = GetSettingValue(block.CustomData, "Category");
        if (category == "") category = GetSettingValue(block.CustomData, "Type");
        if (category == "") category = GetSettingValue(block.CustomData, "Show");
        if (category != "") return NormalizeCategory(category);

        return "All";
    }

    void FillSpecialLoadouts()
    {
        if (_specialTargets.Count == 0 || _lastTransferCount >= MaxTransfersPerRun)
            return;

        int checkedSpecials = 0;
        while (!ShouldYield() && checkedSpecials < _specialTargets.Count)
        {
            if (_specialCursor >= _specialTargets.Count)
                _specialCursor = 0;

            TargetBin special = _specialTargets[_specialCursor];
            _specialCursor++;
            checkedSpecials++;

            foreach (KeyValuePair<string, MyFixedPoint> request in special.Loadout)
            {
                if (ShouldYield())
                    break;

                MyFixedPoint have = CountKeyInInventory(special.Inventory, request.Key);
                if (have >= request.Value)
                    continue;

                MyFixedPoint need = request.Value - have;
                IMyInventory source;
                int itemIndex;
                MyFixedPoint available;
                if (!FindSourceItem(request.Key, special.Inventory, out source, out itemIndex, out available))
                    continue;

                MyFixedPoint move = available < need ? available : need;
                if (move <= (MyFixedPoint)0)
                    continue;

                if (source.TransferItemTo(special.Inventory, itemIndex, null, true, move))
                {
                    _lastTransferCount++;
                    _specialTransferCount++;
                }
            }
        }
    }

    void BalanceReactors()
    {
        if (!EnableReactorBalancing || _reactors.Count == 0 || _lastTransferCount >= MaxTransfersPerRun)
            return;

        MyFixedPoint target = (MyFixedPoint)UraniumIngotsPerReactor;

        for (int r = 0; r < _reactors.Count && !ShouldYield(); r++)
        {
            IMyReactor reactor = _reactors[r];
            if (reactor == null || !reactor.HasInventory || HasToken(reactor, TagIgnore) || HasToken(reactor, TagNoSort) || HasToken(reactor, TagNoDock))
                continue;

            if (!IsAllowedGrid(reactor))
                continue;

            IMyInventory reactorInventory = reactor.GetInventory(0);
            MyFixedPoint current = CountKeyInInventory(reactorInventory, "Ingot/Uranium");
            if (current >= target)
                continue;

            MyFixedPoint need = target - current;
            IMyInventory source;
            int itemIndex;
            MyFixedPoint available;
            if (!FindSourceItem("Ingot/Uranium", reactorInventory, out source, out itemIndex, out available))
                continue;

            MyFixedPoint move = available < need ? available : need;
            if (move <= (MyFixedPoint)0)
                continue;

            if (source.TransferItemTo(reactorInventory, itemIndex, null, true, move))
            {
                _lastTransferCount++;
                _reactorTransferCount++;
            }
        }
    }

    bool FindSourceItem(string wantedKey, IMyInventory skipInventory, out IMyInventory source, out int itemIndex, out MyFixedPoint amount)
    {
        source = null;
        itemIndex = -1;
        amount = (MyFixedPoint)0;

        for (int s = 0; s < _sources.Count; s++)
        {
            IMyTerminalBlock block = _sources[s];
            if (block == null || !block.HasInventory || HasToken(block, TagIgnore))
                continue;

            int inventoryCount = block.InventoryCount;
            for (int invIndex = 0; invIndex < inventoryCount; invIndex++)
            {
                if (!ShouldDrainInventory(block, invIndex))
                    continue;

                IMyInventory inv = block.GetInventory(invIndex);
                if (inv == skipInventory)
                    continue;

                _items.Clear();
                inv.GetItems(_items);
                for (int i = 0; i < _items.Count; i++)
                {
                    if (KeyMatches(_items[i].Type, wantedKey))
                    {
                        source = inv;
                        itemIndex = i;
                        amount = _items[i].Amount;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    void SortStep()
    {
        if (_sources.Count == 0 || ShouldYield())
            return;

        int checkedSources = 0;
        while (!ShouldYield() && checkedSources < _sources.Count)
        {
            if (_sourceCursor >= _sources.Count)
                _sourceCursor = 0;

            IMyTerminalBlock sourceBlock = _sources[_sourceCursor];
            _sourceCursor++;
            checkedSources++;

            if (sourceBlock == null || !sourceBlock.HasInventory || HasToken(sourceBlock, TagIgnore))
                continue;

            int inventoryCount = sourceBlock.InventoryCount;
            for (int invIndex = 0; invIndex < inventoryCount && !ShouldYield(); invIndex++)
            {
                if (!ShouldDrainInventory(sourceBlock, invIndex))
                    continue;

                TrySortInventory(sourceBlock, sourceBlock.GetInventory(invIndex));
            }
        }

    }

    bool ShouldDrainInventory(IMyTerminalBlock block, int inventoryIndex)
    {
        if (HasToken(block, TagDrain)) return true;
        if (block is IMyAssembler || block is IMyRefinery) return inventoryIndex == 1;
        return true;
    }

    void TrySortInventory(IMyTerminalBlock sourceBlock, IMyInventory source)
    {
        _items.Clear();
        source.GetItems(_items);

        for (int i = _items.Count - 1; i >= 0 && !ShouldYield(); i--)
        {
            MyInventoryItem item = _items[i];
            string category = GetItemCategory(item.Type);
            if (category == "") category = "Unknown";

            if (IsAlreadyInCorrectTarget(sourceBlock, category))
                continue;

            TargetBin destination = FindDestination(category, source, item);
            if (destination == null)
                continue;

            if (source.TransferItemTo(destination.Inventory, i, null, true, null))
                _lastTransferCount++;
        }
    }

    bool IsAlreadyInCorrectTarget(IMyTerminalBlock sourceBlock, string category)
    {
        if (HasToken(sourceBlock, TagSpecial))
            return true;

        string sourceCategory = GetContainerCategory(sourceBlock);
        if (sourceCategory == "")
            return false;

        return sourceCategory == category;
    }

    TargetBin FindDestination(string category, IMyInventory source, MyInventoryItem item)
    {
        TargetBin best = FindBestTarget(_targets[category], source, item);
        if (best != null) return best;

        // [CIM:All] is the general fallback when a typed container is missing.
        // If a typed container exists but is full/limited, leave the item where it is.
        if (category != "All" && category != "Unknown" && _targets[category].Count == 0)
            return FindBestTarget(_targets["All"], source, item);

        // Unknown items prefer [CIM:Unknown], then [CIM:All] if no Unknown box exists.
        if (category == "Unknown" && _targets["Unknown"].Count == 0)
            return FindBestTarget(_targets["All"], source, item);

        if (category == "All")
            return FindBestTarget(_targets["All"], source, item);

        return null;
    }

    TargetBin FindBestTarget(List<TargetBin> candidates, IMyInventory source, MyInventoryItem item)
    {
        TargetBin best = null;

        for (int i = 0; i < candidates.Count; i++)
        {
            TargetBin bin = candidates[i];
            if (bin == null || bin.Inventory == null || bin.Inventory == source)
                continue;

            if (!bin.Inventory.CanItemsBeAdded(item.Amount, item.Type))
                continue;

            double fillPercent = GetFillPercent(bin.Inventory);
            if (fillPercent >= bin.FillLimitPercent)
                continue;

            if (best == null || CompareTargets(bin, best) < 0)
                best = bin;
        }

        return best;
    }

    string GetItemCategory(MyItemType type)
    {
        string typeId = type.TypeId.ToString();
        if (EndsWith(typeId, "_Ore")) return "Ore";
        if (EndsWith(typeId, "_Ingot")) return "Ingot";
        if (EndsWith(typeId, "_Component")) return "Component";
        if (EndsWith(typeId, "_AmmoMagazine")) return "Ammo";
        if (EndsWith(typeId, "_OxygenContainerObject")) return "Bottle";
        if (EndsWith(typeId, "_GasContainerObject")) return "Bottle";
        if (EndsWith(typeId, "_PhysicalGunObject")) return "Tool";

        string subtype = NormalizeSubtypeName(type.SubtypeId.ToString());
        if (subtype == "tech2x" || subtype == "tech4x" || subtype == "tech8x" || subtype == "tech16x" || subtype == "tech32x" ||
            subtype == "tellerium" || subtype == "telleriumtech" || subtype == "prosonic" || subtype == "prosonictech" ||
            subtype == "aryxlynxonfusioncomponent" || subtype == "adaptivedynocapacitor" || subtype == "graphinegrid" || subtype == "zonechip")
            return "Component";

        // Fallbacks for mods that use custom object builders with recognizable names.
        if (Contains(typeId, "Ore")) return "Ore";
        if (Contains(typeId, "Ingot")) return "Ingot";
        if (Contains(typeId, "Component")) return "Component";
        if (Contains(typeId, "Ammo")) return "Ammo";
        if (Contains(typeId, "ContainerObject")) return "Bottle";
        if (Contains(typeId, "PhysicalGunObject")) return "Tool";
        return "";
    }

    void LearnItem(MyItemType type)
    {
        if (!AutoLearnItems)
            return;

        string key = GetItemKey(type);
        if (_learnedItems.ContainsKey(key))
            return;

        string category = GetItemCategory(type);
        if (category == "") category = "Unknown";

        _learnedItems[key] = category + "/" + type.SubtypeId.ToString() + " = " + type.TypeId.ToString();
        _learnedNewThisRun++;
    }

    string GetItemKey(MyItemType type)
    {
        string category = GetItemCategory(type);
        string subtype = type.SubtypeId.ToString();
        if (category == "") category = "Unknown";
        return (category + "/" + subtype).ToLowerInvariant();
    }

    bool KeyMatches(MyItemType type, string wantedKey)
    {
        string key = GetItemKey(type);
        string subtype = type.SubtypeId.ToString().ToLowerInvariant();
        string wanted = wantedKey.ToLowerInvariant();
        return key == wanted || subtype == wanted || key.EndsWith("/" + wanted, StringComparison.OrdinalIgnoreCase);
    }

    void ParseLoadout(string customData, Dictionary<string, MyFixedPoint> loadout)
    {
        loadout.Clear();
        string[] lines = (customData ?? "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line == "" || line.StartsWith("//") || line.StartsWith("#") || line.StartsWith("["))
                continue;

            int equals = line.IndexOf("=");
            if (equals <= 0)
                continue;

            string key = line.Substring(0, equals).Trim().ToLowerInvariant();
            string value = line.Substring(equals + 1).Trim();
            double amount;
            if (!double.TryParse(value, out amount) || amount <= 0)
                continue;

            loadout[key] = (MyFixedPoint)amount;
        }
    }

    MyFixedPoint CountKeyInInventory(IMyInventory inv, string wantedKey)
    {
        MyFixedPoint total = (MyFixedPoint)0;
        _items.Clear();
        inv.GetItems(_items);
        for (int i = 0; i < _items.Count; i++)
        {
            if (KeyMatches(_items[i].Type, wantedKey))
                total += _items[i].Amount;
        }
        return total;
    }

    void CountItems()
    {
        _counts.Clear();
        _itemTotals.Clear();
        for (int b = 0; b < _blocks.Count; b++)
        {
            IMyTerminalBlock block = _blocks[b];
            if (block == null || !block.HasInventory || !IsAllowedGrid(block) || HasToken(block, TagIgnore) || IsNoSortBlocked(block) || IsNoPullDockedGrid(block.CubeGrid))
                continue;

            for (int invIndex = 0; invIndex < block.InventoryCount; invIndex++)
                CountInventory(block.GetInventory(invIndex));
        }
    }

    void CountInventory(IMyInventory inv)
    {
        _items.Clear();
        inv.GetItems(_items);
        for (int i = 0; i < _items.Count; i++)
        {
            MyInventoryItem item = _items[i];
            LearnItem(item.Type);

            string category = GetItemCategory(item.Type);
            if (category == "") category = "Unknown";

            string key = GetItemKey(item.Type);
            MyFixedPoint itemCurrent;
            _itemTotals.TryGetValue(key, out itemCurrent);
            _itemTotals[key] = itemCurrent + item.Amount;

            MyFixedPoint current;
            _counts.TryGetValue(category, out current);
            _counts[category] = current + item.Amount;
        }
    }

    void CountGasTanks()
    {
        _hydrogenFilled = 0;
        _hydrogenCapacity = 0;
        _oxygenFilled = 0;
        _oxygenCapacity = 0;

        for (int i = 0; i < _gasTanks.Count; i++)
        {
            IMyGasTank tank = _gasTanks[i];
            if (tank == null || !IsAllowedGrid(tank) || HasToken(tank, TagIgnore))
                continue;

            double capacity = tank.Capacity;
            double filled = capacity * tank.FilledRatio;

            if (IsHydrogenTank(tank))
            {
                _hydrogenCapacity += capacity;
                _hydrogenFilled += filled;
            }
            else if (IsOxygenTank(tank))
            {
                _oxygenCapacity += capacity;
                _oxygenFilled += filled;
            }
        }
    }

    bool IsHydrogenTank(IMyGasTank tank)
    {
        string text = (tank.CustomName + " " + tank.DefinitionDisplayNameText).ToLowerInvariant();
        return text.IndexOf("hydrogen", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool IsOxygenTank(IMyGasTank tank)
    {
        string text = (tank.CustomName + " " + tank.DefinitionDisplayNameText).ToLowerInvariant();
        return text.IndexOf("oxygen", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void UpdateContainerNames()
    {
        if (!ShowFillPercentInNames)
            return;

        for (int i = 0; i < _allTargets.Count; i++)
        {
            TargetBin bin = _allTargets[i];
            if (bin == null || bin.Block == null || bin.Inventory == null)
                continue;

            int percent = (int)Math.Round(GetFillPercent(bin.Inventory));
            string clean = RemoveFillTag(bin.Block.CustomName).TrimEnd();
            string wanted = clean + " [CIM " + percent + "%]";
            if (bin.Block.CustomName != wanted)
                bin.Block.CustomName = wanted;
        }
    }

    string RemoveFillTag(string name)
    {
        if (name == null)
            return "";

        int start = name.IndexOf("[CIM ", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return name;

        int end = name.IndexOf("%]", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
            return name;

        return (name.Substring(0, start) + name.Substring(end + 2)).Trim();
    }

    string RemoveToken(string text, string token)
    {
        if (text == null || token == null || token == "")
            return text;

        int start = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return text;

        return (text.Substring(0, start) + text.Substring(start + token.Length)).Trim();
    }

    double GetFillPercent(IMyInventory inv)
    {
        if (inv == null || inv.MaxVolume <= (MyFixedPoint)0)
            return 0;

        return ((double)inv.CurrentVolume / (double)inv.MaxVolume) * 100d;
    }

    bool ShouldYield()
    {
        if (_lastTransferCount >= MaxTransfersPerRun)
            return true;

        if (Runtime.CurrentInstructionCount >= Runtime.MaxInstructionCount * InstructionBudgetPercent)
            return true;

        if (_lastTransferCount > 0 && Runtime.LastRunTimeMs >= RuntimeCheckLimitMs)
            return true;

        return false;
    }

    void WriteContainerDisplays()
    {
        for (int i = 0; i < _containerDisplays.Count; i++)
        {
            ContainerDisplay display = _containerDisplays[i];
            if (display == null || display.Surface == null)
                continue;

            IMyTerminalBlock targetBlock = FindDisplayBlock(display.Match);
            _text.Clear();
            _text.AppendLine("CIM Block Inventory");

            if (display.Match == "")
            {
                _text.AppendLine("No target set");
                _text.AppendLine();
                _text.AppendLine("LCD Custom Data:");
                _text.AppendLine("Container=container name");
                display.Surface.WriteText(_text.ToString(), false);
                continue;
            }

            if (targetBlock == null)
            {
                _text.AppendLine("Target not found:");
                _text.AppendLine(display.Match);
                _text.AppendLine();
                _text.AppendLine("Use exact or partial cargo/tank name.");
                display.Surface.WriteText(_text.ToString(), false);
                continue;
            }

            IMyGasTank tank = targetBlock as IMyGasTank;
            if (tank != null)
            {
                WriteTankDisplay(display.Surface, tank);
                continue;
            }

            IMyInventory inv = targetBlock.GetInventory(0);
            _text.AppendLine(RemoveFillTag(targetBlock.CustomName));
            _text.AppendLine("Fill: " + GetFillPercent(inv).ToString("0.0") + "%");
            _text.AppendLine("Vol: " + ((double)inv.CurrentVolume).ToString("0.###") + " / " + ((double)inv.MaxVolume).ToString("0.###"));
            _text.AppendLine();

            _items.Clear();
            inv.GetItems(_items);

            if (_items.Count == 0)
            {
                _text.AppendLine("Empty");
            }
            else
            {
                for (int itemIndex = 0; itemIndex < _items.Count; itemIndex++)
                {
                    MyInventoryItem item = _items[itemIndex];
                    string subtype = GetFriendlySubtypeName(item.Type.SubtypeId.ToString());
                    _text.AppendLine(FormatAmount(item.Amount) + "  " + subtype);
                }
            }

            display.Surface.WriteText(_text.ToString(), false);
        }
    }

    void WriteTankDisplay(IMyTextSurface surface, IMyGasTank tank)
    {
        double capacity = tank.Capacity;
        double filled = capacity * tank.FilledRatio;

        _text.Clear();
        _text.AppendLine("CIM Tank Capacity");
        _text.AppendLine(RemoveFillTag(tank.CustomName));
        _text.AppendLine(IsHydrogenTank(tank) ? "Gas: Hydrogen" : IsOxygenTank(tank) ? "Gas: Oxygen" : "Gas: Unknown");
        _text.AppendLine("Fill: " + (tank.FilledRatio * 100d).ToString("0.0") + "%");
        _text.AppendLine("Stored: " + FormatGas(filled));
        _text.AppendLine("Capacity: " + FormatGas(capacity));
        _text.AppendLine("Stockpile: " + (tank.Stockpile ? "ON" : "OFF"));
        surface.WriteText(_text.ToString(), false);
    }

    void WriteItemDisplays()
    {
        if (_itemDisplays.Count == 0 || ShouldYield())
            return;

        int updated = 0;
        int checkedDisplays = 0;
        while (!ShouldYield() && checkedDisplays < _itemDisplays.Count && updated < MaxItemLcdUpdatesPerRun)
        {
            if (_itemLcdCursor >= _itemDisplays.Count)
                _itemLcdCursor = 0;

            ItemDisplay display = _itemDisplays[_itemLcdCursor];
            _itemLcdCursor++;
            checkedDisplays++;

            if (display == null || display.Surface == null)
                continue;

            string category = display.Category;
            if (category == "")
                category = "All";

            _displayLines.Clear();
            _displayLines.Add("CIM Inventory Totals");
            _displayLines.Add("Showing: " + category);
            _displayLines.Add("");

            int shown = 0;
            foreach (KeyValuePair<string, MyFixedPoint> total in _itemTotals)
            {
                if (!ShouldShowItemKey(total.Key, category))
                    continue;

                string name = GetSubtypeFromKey(total.Key);
                _displayLines.Add(PadRight(name, 24) + FormatAmount(total.Value));
                shown++;
            }

            if (shown == 0)
            {
                _displayLines.Add("No items found.");
                _displayLines.Add("");
                _displayLines.Add("Put category in LCD name:");
                _displayLines.Add("Components [CIM:ItemsLCD]");
                _displayLines.Add("Ore [CIM:ItemsLCD]");
                _displayLines.Add("All [CIM:ItemsLCD]");
                _displayLines.Add("Unknown [CIM:ItemsLCD]");
            }

            WriteScrolledItemDisplay(display, _displayLines);
            updated++;
        }
    }

    void WriteScrolledItemDisplay(ItemDisplay display, List<string> lines)
    {
        int visibleLines = ItemLcdVisibleLines;
        if (visibleLines < 4)
            visibleLines = 4;

        _text.Clear();

        if (lines.Count <= visibleLines)
        {
            display.ScrollLine = 0;
            if (display.Block != null)
                _itemLcdScrollLines[display.Block.EntityId] = display.ScrollLine;

            for (int i = 0; i < lines.Count; i++)
                _text.AppendLine(lines[i]);

            display.Surface.WriteText(_text.ToString(), false);
            return;
        }

        if (display.ScrollLine < 0 || display.ScrollLine > lines.Count - visibleLines)
            display.ScrollLine = 0;

        int start = display.ScrollLine;
        int end = start + visibleLines;
        if (end > lines.Count)
            end = lines.Count;

        for (int i = start; i < end; i++)
            _text.AppendLine(lines[i]);

        display.Surface.WriteText(_text.ToString(), false);

        display.ScrollLine++;
        if (display.ScrollLine > lines.Count - visibleLines)
            display.ScrollLine = 0;

        if (display.Block != null)
            _itemLcdScrollLines[display.Block.EntityId] = display.ScrollLine;
    }

    bool ShouldShowItemKey(string key, string category)
    {
        if (category == "All")
            return true;

        return key.StartsWith(category.ToLowerInvariant() + "/");
    }

    string GetSubtypeFromKey(string key)
    {
        int slash = key.IndexOf("/");
        if (slash < 0 || slash >= key.Length - 1)
            return GetFriendlySubtypeName(key);

        return GetFriendlySubtypeName(key.Substring(slash + 1));
    }

    string GetFriendlySubtypeName(string subtype)
    {
        if (subtype == null)
            return "";

        string normalized = NormalizeSubtypeName(subtype);
        if (normalized == "tech2x") return "Common Tech";
        if (normalized == "tech4x") return "Rare Tech";
        if (normalized == "tech8x") return "Elite Tech";
        if (normalized == "tech16x") return "Prosonic Tech";
        if (normalized == "tech32x") return "Prosonic Tech";
        if (normalized == "tellerium" || normalized == "telleriumtech" || normalized == "prosonic" || normalized == "prosonictech") return "Prosonic Tech";
        if (normalized == "aryxlynxonfusioncomponent") return "Fusion Coils";
        if (normalized == "adaptivedynocapacitor") return "Dyno Capacitor";
        if (normalized == "graphinegrid") return "Graphine Grid";
        if (normalized == "zonechip") return "Zone Chips";

        return subtype;
    }

    string NormalizeSubtypeName(string subtype)
    {
        if (subtype == null)
            return "";

        return subtype.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
    }

    void WriteLearnedDisplays()
    {
        if (_learnedSurfaces.Count == 0)
            return;

        _text.Clear();
        _text.AppendLine("CIM Learned Item Catalog");
        _text.AppendLine("Items: " + _learnedItems.Count + " | New this run: " + _learnedNewThisRun);
        _text.AppendLine();
        _text.AppendLine("Use these names in loadouts:");
        _text.AppendLine("  Component/SteelPlate=200");
        _text.AppendLine("  SubtypeOnly=10 also works");
        _text.AppendLine();

        AppendLearnedCategory("Component");
        AppendLearnedCategory("Ore");
        AppendLearnedCategory("Ingot");
        AppendLearnedCategory("Ammo");
        AppendLearnedCategory("Bottle");
        AppendLearnedCategory("Tool");
        AppendLearnedCategory("Unknown");

        string output = _text.ToString();
        for (int i = 0; i < _learnedSurfaces.Count; i++)
            _learnedSurfaces[i].WriteText(output, false);
    }

    void AppendLearnedCategory(string category)
    {
        bool wroteHeader = false;
        foreach (KeyValuePair<string, string> learned in _learnedItems)
        {
            if (!learned.Key.StartsWith(category.ToLowerInvariant() + "/"))
                continue;

            if (!wroteHeader)
            {
                _text.AppendLine(category + ":");
                wroteHeader = true;
            }

            _text.AppendLine("  " + learned.Value);
        }

        if (wroteHeader)
            _text.AppendLine();
    }

    void LoadLearnedItems()
    {
        _learnedItems.Clear();
        string[] lines = (Storage ?? "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (!line.StartsWith("CIMLEARN|"))
                continue;

            string[] parts = line.Split('|');
            if (parts.Length < 3)
                continue;

            string key = parts[1].Trim();
            string value = parts[2].Trim();
            if (key != "" && value != "" && !_learnedItems.ContainsKey(key))
                _learnedItems[key] = value;
        }
    }

    void SaveLearnedItems()
    {
        _text.Clear();
        foreach (KeyValuePair<string, string> learned in _learnedItems)
            _text.AppendLine("CIMLEARN|" + learned.Key + "|" + learned.Value);

        Storage = _text.ToString();
    }

    IMyTerminalBlock FindDisplayBlock(string match)
    {
        if (match == null || match.Trim() == "")
            return null;

        string wanted = match.Trim();
        IMyTerminalBlock partial = null;

        for (int i = 0; i < _blocks.Count; i++)
        {
            IMyTerminalBlock block = _blocks[i];
            if (block == null || !IsAllowedGrid(block) || HasToken(block, TagIgnore) || IsNoPullDockedGrid(block.CubeGrid))
                continue;

            if (!(block is IMyCargoContainer) && !(block is IMyGasTank))
                continue;

            string cleanName = RemoveFillTag(block.CustomName).Trim();
            if (cleanName.Equals(wanted, StringComparison.OrdinalIgnoreCase) || block.CustomName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
                return block;

            if (partial == null && Contains(cleanName, wanted))
                partial = block;
        }

        return partial;
    }

    void WriteStatus(bool showHelp)
    {
        _text.Clear();
        _text.AppendLine("Casey's Inventory Management");
        _text.AppendLine(_paused ? "State: PAUSED" : "State: RUNNING");
        _text.AppendLine("Last: " + _lastMessage);
        _text.AppendLine("Sources: " + _sources.Count + " | Targets: " + _allTargets.Count + " | Specials: " + _specialTargets.Count);
        _text.AppendLine("Dock rules: " + _blockedDockedGrids.Count + " no-dock, " + _noPullDockedGrids.Count + " no-pull");
        _text.AppendLine("LCDs: " + _statusSurfaces.Count + " status, " + _containerDisplays.Count + " container, " + _itemDisplays.Count + " items, " + _learnedSurfaces.Count + " learned");
        _text.AppendLine("Auto-assigned: " + _autoAssignedCount + " | Unassigned cargo: " + _unassignedCargo.Count);
        _text.AppendLine("Learned items: " + _learnedItems.Count + " | New: " + _learnedNewThisRun);
        _text.AppendLine("Transfers/run: " + _lastTransferCount + " (special " + _specialTransferCount + ", reactor " + _reactorTransferCount + ") | Total: " + _totalTransfers);
        _text.AppendLine("Runtime: " + Runtime.LastRunTimeMs.ToString("0.000") + " ms");
        _text.AppendLine("Budget: " + RuntimeCheckLimitMs.ToString("0.00") + " ms / " + (InstructionBudgetPercent * 100d).ToString("0") + "% instructions");
        _text.AppendLine("Access: " + (OnlySameFaction ? "own/faction blocks only" : "all accessible blocks"));
        _text.AppendLine();

        _text.AppendLine("Gas Tanks:");
        AppendGasLine("Hydrogen", _hydrogenFilled, _hydrogenCapacity);
        AppendGasLine("Oxygen", _oxygenFilled, _oxygenCapacity);
        _text.AppendLine();

        _text.AppendLine("Targets:");
        for (int i = 0; i < _categoryNames.Length; i++)
        {
            string category = _categoryNames[i];
            _text.AppendLine("  " + PadRight(category, 10) + _targets[category].Count);
        }
        _text.AppendLine("  " + PadRight("Special", 10) + _specialTargets.Count);

        _text.AppendLine();
        _text.AppendLine("Totals:");
        AppendCount("Ore");
        AppendCount("Ingot");
        AppendCount("Component");
        AppendCount("Tool");
        AppendCount("Ammo");
        AppendCount("Bottle");
        AppendCount("Unknown");

        if (showHelp)
        {
            _text.AppendLine();
            _text.AppendLine("Container tags:");
            _text.AppendLine("  [CIM:Ore] [CIM:Ingot] [CIM:Component]");
            _text.AppendLine("  [CIM:Tool] [CIM:Ammo] [CIM:Bottle]");
            _text.AppendLine("  [CIM:All] [CIM:Unknown]");
            _text.AppendLine("  Missing categories can be auto-tagged as [AUTO]");
            _text.AppendLine("  [CIM:P1] priority, lower number fills first");
            _text.AppendLine();
            _text.AppendLine("Special loadout example in Custom Data:");
            _text.AppendLine("  [CIM:Special]");
            _text.AppendLine("  Component/SteelPlate=200");
            _text.AppendLine("  Ingot/Uranium=5");
            _text.AppendLine("  HydrogenBottle=2");
            _text.AppendLine();
            _text.AppendLine("Settings in container Custom Data:");
            _text.AppendLine("  Priority=1");
            _text.AppendLine("  Limit=80");
            _text.AppendLine();
            _text.AppendLine("Tanks/reactors:");
            _text.AppendLine("  Container LCDs can target gas tanks too");
            _text.AppendLine("  Reactors are topped on owned/faction grids");
            _text.AppendLine("  Allied/enemy-owned blocks are skipped");
            _text.AppendLine("  [CIM:NoDock] on connector skips docked ship");
            _text.AppendLine("  [NoPull] on connector only tops reactors");
            _text.AppendLine();
            _text.AppendLine("Learning:");
            _text.AppendLine("  [CIM:LearnedLCD] shows discovered item names");
            _text.AppendLine("  Modded components are learned when seen");
            _text.AppendLine();
            _text.AppendLine("Utility: [CIM:Status] [CIM:ContainerLCD] [CIM:ItemsLCD] [CIM:LearnedLCD] [CIM:Ignore] [CIM:Drain] [CIM:NoSort] [CIM:NoDock] [NoPull]");
        }

        string output = _text.ToString();
        Echo(output);
        for (int i = 0; i < _statusSurfaces.Count; i++)
            _statusSurfaces[i].WriteText(output, false);
    }

    void AppendCount(string category)
    {
        MyFixedPoint amount;
        _counts.TryGetValue(category, out amount);
        _text.AppendLine("  " + PadRight(category, 10) + FormatAmount(amount));
    }

    void AppendGasLine(string name, double filled, double capacity)
    {
        double percent = capacity > 0 ? (filled / capacity) * 100d : 0;
        _text.AppendLine("  " + PadRight(name, 10) + percent.ToString("0.0") + "%  " + FormatGas(filled) + " / " + FormatGas(capacity));
    }

    string GetSettingValue(string customData, string key)
    {
        string[] lines = (customData ?? "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            int equals = line.IndexOf("=");
            if (equals <= 0)
                continue;

            string left = line.Substring(0, equals).Trim();
            if (left.Equals(key, StringComparison.OrdinalIgnoreCase))
                return line.Substring(equals + 1).Trim();
        }
        return "";
    }

    string NormalizeCategory(string text)
    {
        string value = (text ?? "").Trim().ToLowerInvariant();
        value = value.Replace("[cim:itemslcd]", "").Trim();

        if (value == "" || value == "all" || value == "everything") return "All";
        if (value == "component" || value == "components" || value.Contains("component")) return "Component";
        if (value == "ore" || value == "ores" || value.Contains("ore")) return "Ore";
        if (value == "ingot" || value == "ingots" || value.Contains("ingot")) return "Ingot";
        if (value == "tool" || value == "tools" || value.Contains("tool")) return "Tool";
        if (value == "ammo" || value == "ammunition" || value.Contains("ammo")) return "Ammo";
        if (value == "bottle" || value == "bottles" || value.Contains("bottle")) return "Bottle";
        if (value == "unknown" || value == "other" || value == "misc" || value.Contains("unknown") || value.Contains("other")) return "Unknown";

        return "All";
    }

    string FormatAmount(MyFixedPoint amount)
    {
        double value = (double)amount;
        if (value >= 1000000) return (value / 1000000d).ToString("0.##") + "M";
        if (value >= 1000) return (value / 1000d).ToString("0.##") + "k";
        return value.ToString("0.##");
    }

    string FormatGas(double liters)
    {
        if (liters >= 1000000000d) return (liters / 1000000000d).ToString("0.##") + " GL";
        if (liters >= 1000000d) return (liters / 1000000d).ToString("0.##") + " ML";
        if (liters >= 1000d) return (liters / 1000d).ToString("0.##") + " kL";
        return liters.ToString("0.##") + " L";
    }

    bool HasToken(IMyTerminalBlock block, string token)
    {
        return Contains(block.CustomName, token) || Contains(block.CustomData, token);
    }

    bool HasNoPullToken(IMyTerminalBlock block)
    {
        return HasToken(block, TagNoPull) || HasToken(block, TagNoPullShort);
    }

    bool HasAnyCimTag(IMyTerminalBlock block)
    {
        return Contains(block.CustomName, "[CIM:") || Contains(block.CustomData, "[CIM:") ||
            Contains(block.CustomName, "[AUTO]") || Contains(block.CustomData, "[AUTO]");
    }

    bool Contains(string text, string value)
    {
        if (text == null || value == null) return false;
        return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    bool EndsWith(string text, string value)
    {
        if (text == null || value == null) return false;
        return text.EndsWith(value, StringComparison.OrdinalIgnoreCase);
    }

    string PadRight(string text, int width)
    {
        if (text.Length >= width) return text;
        return text + new string(' ', width - text.Length);
    }
