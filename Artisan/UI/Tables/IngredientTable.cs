using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Logging;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Artisan.UI.Tables
{
    internal class IngredientTable : Table<Ingredient>, IDisposable
    {
        private static float _nameColumnWidth = 0;
        private static float _requiredColumnWidth = 80;
        private static float _idColumnWidth = 80;
        private static float _inventoryColumnWidth = 80;
        private static float _retainerColumnWidth = 80;
        private static float _remainingColumnWidth = 100;
        private static float _canCraftColumnWidth = 100;
        private static float _craftableCountColumnWidth = 100;
        private static float _craftItemsColumnWidth = 0;
        private static float _itemCategoryColumnWidth = 0;
        private static float _gatherItemLocationColumWidth = 0;
        private static float _craftingJobsColumnWidth = 100;

        private readonly IdColumn _idColumn = new() { Label = "ID" };
        private readonly NameColumn _nameColumn = new() { Label = "Item Name" };
        private readonly RequiredColumn _requiredColumn = new() { Label = "Required" };
        private readonly InventoryCountColumn _inventoryColumn = new() { Label = "Inventory" };
        private readonly RetainerCountColumn _retainerColumn = new() { Label = "Retainers" };
        private readonly RemaingCountColumn _remainingColumn = new() { Label = "Remaining Needed" };
        private readonly CraftableColumn _craftableColumn = new() { Label = "Craftable" };
        private readonly CraftableCountColumn _craftableCountColumn = new() { Label = "Number Craftable" };
        private readonly CraftItemsColumn _craftItemsColumn = new() { Label = "Used to Craft" };
        private readonly ItemCategoryColumn _itemCategoryColumn = new() { Label = "Category" };
        private readonly GatherItemLocationColumn _gatherItemLocationColumn = new() { Label = "Gathered Zone" };

        private static bool GatherBuddy =>
            DalamudReflector.TryGetDalamudPlugin("GatherBuddy", out var gb, false, true);

        private static bool ItemVendor =>
            DalamudReflector.TryGetDalamudPlugin("Item Vendor Location", out var ivl, false, true);

        private static bool MonsterLookup =>
            DalamudReflector.TryGetDalamudPlugin("Monster Loot Hunter", out var mlh, false, true);

        private static unsafe void SearchItem(uint item) => ItemFinderModule.Instance()->SearchForItem(item);

        public List<Ingredient> ListItems;

        public IngredientTable(List<Ingredient> ingredientList)
            : base("IngredientTable", ingredientList)
        {
            Headers.Add(_nameColumn);
            Headers.Add(_requiredColumn);
            Headers.Add(_inventoryColumn);
            if (RetainerInfo.ATools) Headers.Add(_retainerColumn);
            Headers.Add(_remainingColumn);
            Headers.Add(_craftableColumn);
            Headers.Add(_craftableCountColumn);
            Headers.Add(_craftItemsColumn);
            Headers.Add(_itemCategoryColumn);
            Headers.Add(_gatherItemLocationColumn);
            Headers.Add(_idColumn);
            
            Sortable = true;
            ListItems = ingredientList;
            Flags |= ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable;

            _nameColumn.OnContextMenuRequest += OpenContextMenu;

            foreach (var item in Items)
            {
                item.OnRemainingChange += SetFilterDirty;
            }
        }

        private void SetFilterDirty(object? sender, bool e)
        {
            this.FilterDirty = true;
        }

        public void Dispose()
        {
            _nameColumn.OnContextMenuRequest -= OpenContextMenu;

            foreach (var item in Items)
            {
                item.OnRemainingChange -= SetFilterDirty;
            }
        }

        private sealed class NameColumn : ColumnString<Ingredient>
        {
            public NameColumn()
               => Flags |= ImGuiTableColumnFlags.NoHide;

            public override string ToName(Ingredient item)
            {
                return item.Data.Name.RawString;
            }

            public override float Width => _nameColumnWidth * ImGuiHelpers.GlobalScale;

            public override void DrawColumn(Ingredient item, int _)
            {
                ImGuiUtil.HoverIcon(item.Icon, Interface.LineIconSize);
                ImGui.SameLine();

                var selected = ImGui.Selectable(item.Data.Name.RawString);
                InvokeContextMenu(item);

                if (selected)
                {
                    ImGui.SetClipboardText(item.Data.Name.RawString);
                    Notify.Success("Name copied to clipboard");
                }
            }
        }

        private sealed class RequiredColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _requiredColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Required.CompareTo(rhs.Required);

            public override void DrawColumn(Ingredient item, int _)
                => ImGuiUtil.Center($"{ToName(item)}");

            public override string ToName(Ingredient item)
            {
                return item.Required.ToString();
            }
        }

        private sealed class IdColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _idColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Data.RowId.CompareTo(rhs.Data.RowId);

            public override string ToName(Ingredient item)
            {
                return item.Data.RowId.ToString();
            }
        }

        private sealed class InventoryCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _inventoryColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Inventory.CompareTo(rhs.Inventory);

            public override void DrawColumn(Ingredient item, int _)
                => ImGuiUtil.Center($"{ToName(item)}");

            public override string ToName(Ingredient item)
            {
                return item.Inventory.ToString();
            }
        }

        private sealed class RetainerCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _retainerColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.RetainerCount.CompareTo(rhs.RetainerCount);

            public override void DrawColumn(Ingredient item, int _)
                => ImGuiUtil.Center($"{ToName(item)}");

            public override string ToName(Ingredient item)
            {
                return item.RetainerCount.ToString();
            }
        }

        private sealed class CraftableCountColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _craftableCountColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.TotalCraftable.CompareTo(rhs.TotalCraftable);

            public override void DrawColumn(Ingredient item, int _)
                => ImGuiUtil.Center(ToName(item));


            public override string ToName(Ingredient item)
            {
                return item.CanBeCrafted ? item.TotalCraftable.ToString() : "N/A";
            }
        }

        private sealed class CraftItemsColumn : ColumnString<Ingredient>
        {
            public override float Width
                => _craftItemsColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.UsedInCrafts.First().CompareTo(rhs.UsedInCrafts.First());

            public override string ToName(Ingredient item)
            {
                return string.Join(", ", item.UsedInCrafts.Select(x => x.NameOfRecipe()));
            }

        }

        private sealed class GatherItemLocationColumn : ItemFilterColumn
        {
            public GatherItemLocationColumn() 
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.GatherZone, ItemFilter.NoGatherZone, ItemFilter.TimedNode, ItemFilter.NonTimedNode);
                SetNames("Gather Zone", "No Gather Zone", "Timed Node", "Non-Timed Node");

            }
            public override float Width
                => _gatherItemLocationColumWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.GatherZone.PlaceName.Value.Name.RawString.CompareTo(rhs.GatherZone.PlaceName.Value.Name.RawString);

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGui.Text(item.GatherZone.PlaceName.Value.Name.RawString);
            }

            public override bool FilterFunc(Ingredient item)
            {
                bool zone = item.GatherZone.RowId switch
                {                 
                    1 => FilterValue.HasFlag(ItemFilter.NoGatherZone),
                    _ => FilterValue.HasFlag(ItemFilter.GatherZone)
                };

                bool timed = item.TimedNode switch
                {
                    true => FilterValue.HasFlag(ItemFilter.TimedNode),
                    false => FilterValue.HasFlag(ItemFilter.NonTimedNode)
                };

                return zone & timed;
            }
        }

        private sealed class ItemCategoryColumn : ItemFilterColumn
        {
            public ItemCategoryColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.NonCrystals, ItemFilter.Crystals);
                SetNames("Non-Crystals", "Crystals");
            }


            public override float Width
                => _itemCategoryColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Category.CompareTo(rhs.Category);

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGui.Text(Svc.Data.Excel.GetSheet<ItemSearchCategory>().GetRow(item.Category).Name.RawString);
            }

            public override bool FilterFunc(Ingredient item)
            {
                return item.Category switch
                {
                    58 => FilterValue.HasFlag(ItemFilter.Crystals),
                    _ => FilterValue.HasFlag(ItemFilter.NonCrystals)
                };
            }
        }

        private class ItemFilterColumn : ColumnFlags<ItemFilter, Ingredient>
        {
            private ItemFilter[] FlagValues = Array.Empty<ItemFilter>();
            private string[] FlagNames = Array.Empty<string>();

            protected void SetFlags(params ItemFilter[] flags)
            {
                FlagValues = flags;
                AllFlags = FlagValues.Aggregate((f, g) => f | g);
            }

            protected void SetFlagsAndNames(params ItemFilter[] flags)
            {
                SetFlags(flags);
                SetNames(flags.Select(f => f.ToString()).ToArray());
            }

            protected void SetNames(params string[] names)
                => FlagNames = names;

            protected sealed override IReadOnlyList<ItemFilter> Values
                => FlagValues;

            protected sealed override string[] Names
                => FlagNames;

            public sealed override ItemFilter FilterValue
                => P.config.ShowItems;

            protected sealed override void SetValue(ItemFilter f, bool v)
            {
                var tmp = v ? FilterValue | f : FilterValue & ~f;
                if (tmp == FilterValue)
                    return;

                P.config.ShowItems = tmp;
                P.config.Save();
            }
        }

        private sealed class RemaingCountColumn : ItemFilterColumn
        {
            public RemaingCountColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.MissingItems, ItemFilter.NoMissingItems);
                SetNames("Missing Items", "No Missing Items");
            }

            public override float Width
                => _remainingColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.Remaining.CompareTo(rhs.Remaining);

            public override void DrawColumn(Ingredient item, int idx)
            {
                ImGuiUtil.Center($"{item.Remaining}");

                if (ImGui.IsItemHovered())
                {
                    if (item.CanBeCrafted)
                    {
                        if (RetainerInfo.ATools)
                            ImGuiUtil.HoverTooltip("Adds the number in your inventory, retainers and craftable amounts and subtracts from required.");
                        else
                            ImGuiUtil.HoverTooltip("Adds the number in your inventory and craftable amounts and subtracts from required.");
                    }
                    else
                    {
                        ImGuiUtil.HoverTooltip($"Factors in crafted materials on this list that uses this ingredient you already have in your inventory{(RetainerInfo.ATools ? " or retainer" : "")}.\r\n" +
                            "Example: You have both Maple Logs and Maple Lumber on the ingredient list.\nFor every Maple Lumber you already have, the remaining Maple Log count is reduced by 3 (the amount required per lumber)");
                    }
                }
                
            }

            public override bool FilterFunc(Ingredient item)
            {
                return item.Remaining switch
                {
                    0 => FilterValue.HasFlag(ItemFilter.NoMissingItems),
                    _ => FilterValue.HasFlag(ItemFilter.MissingItems)
                };
            }
        }

        private sealed class CraftableColumn : ItemFilterColumn
        {
            public CraftableColumn()
            {
                Flags -= ImGuiTableColumnFlags.NoResize;
                SetFlags(ItemFilter.True, ItemFilter.False);
                SetNames("Can Craft", "Only Gathered");
            }

            public override float Width
                => _canCraftColumnWidth;

            public override int Compare(Ingredient lhs, Ingredient rhs)
                => lhs.CanBeCrafted.CompareTo(rhs.CanBeCrafted);

            public override void DrawColumn(Ingredient item, int idx)
                => ImGui.Text($"{(item.CanBeCrafted ? "Crafted" : (ItemVendor && ItemVendorLocation.ItemHasVendor(item.Data.RowId) ? "Vendor" : "Gathered"))}");

            public override bool FilterFunc(Ingredient item)
            {
                return item.CanBeCrafted switch
                {
                    true => FilterValue.HasFlag(ItemFilter.True),
                    false => FilterValue.HasFlag(ItemFilter.False)
                };
            }
        }

        private void OpenContextMenu(object? sender, Ingredient item)
        {
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup(item.Data.Name.RawString);

            using var popup = ImRaii.Popup(item.Data.Name.RawString);
            if (!popup)
                return;

            DrawGatherItem(item);
            DrawSearchItem(item);
            DrawItemVendorLookup(item);
            DrawMonsterLootLookup(item);
            DrawFilterOnCrafts(item);
        }

        private void DrawFilterOnCrafts(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (FilteredItems.Count() == Items.Count() || Headers.Any(x => x.FilterFunc(item)) && !FilteredItems.All(x => !x.Item1.CanBeCrafted))
            {
                if (item.CanBeCrafted && item.OriginList.Items.Any(x => LuminaSheets.RecipeSheet.Values.Any(y => y.ItemResult.Row == item.Data.RowId && y.RowId == x)))
                {
                    if (!ImGui.Selectable($"Show ingredients used for this"))
                        return;

                    FilteredItems.Clear();
                    var idx = 0;
                    foreach (var ingredient in CraftingListHelpers.GetIngredientRecipe(item.Data.RowId).UnkData5.Where(x => x.AmountIngredient > 0))
                    {
                        if (Items.FindFirst(x => x.Data.RowId == ingredient.ItemIngredient, out var result))
                            FilteredItems.Add((result, idx));
                        idx++;
                    }
                }
            }
            else
            {
                if (!ImGui.Selectable($"Clear Filters"))
                    return;

                FilterDirty = true;
                
            }
        }

        private static void DrawMonsterLootLookup(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (MonsterLookup)
            {
                if (!ImGui.Selectable("Monster Loot Lookup"))
                    return;

                try
                {
                    Chat.Instance.SendMessage($"/mloot {item.Data.Name.RawString}");
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }
            else
            {
                ImGui.TextDisabled("Monster Loot Lookup (Please install Monster Loot Hunter)");
            }
        }

        private static void DrawItemVendorLookup(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (ItemVendor)
            {
                if (IPC.ItemVendorLocation.ItemHasVendor(item.Data.RowId))
                {
                    if (!ImGui.Selectable("Item Vendor Lookup"))
                        return;

                    try
                    {
                        IPC.ItemVendorLocation.OpenContextMenu(item.Data.RowId);
                    }
                    catch (Exception e)
                    {
                        e.Log();
                    }
                }
            }
            else
            {
                ImGui.TextDisabled("Item Vendor Lookup (Please install Item Vendor Location)");
            }
        }

        private static void DrawSearchItem(Ingredient item)
        {
            if (item.Data.RowId == 0)
                return;

            if (!ImGui.Selectable("Search for Item"))
                return;

            try
            {
                SearchItem(item.Data.RowId);
            }
            catch (Exception e)
            {
                e.Log();
            }

        }

        private static void DrawGatherItem(Ingredient item)
        {
            if (item.Data.RowId == 0 || item.CanBeCrafted)
                return;

            if (GatherBuddy)
            {
                if (!ImGui.Selectable("Gather Item"))
                    return;

                try
                {
                    if (LuminaSheets.GatheringItemSheet!.Any(x => x.Value.Item == item.Data.RowId))
                        Chat.Instance.SendMessage($"/gather {item.Data.Name.RawString}");
                    else
                        Chat.Instance.SendMessage($"/gatherfish {item.Data.Name.RawString}");
                }
                catch (Exception e)
                {
                    e.Log();
                }
            }
            else
            {
                ImGui.TextDisabled("Gather Item (Please install Gatherbuddy)");
            }
        }
    }

    [Flags]
    public enum ItemFilter
    {
        NoItems = 0,
        MissingItems = 0x000001,
        NoMissingItems = 0x000002,

        True = 0x000010,
        False = 0x000020,

        NonCrystals = 0x000100,
        Crystals = 0x000200,

        GatherZone = 0x001000,
        NoGatherZone = 0x002000,
        TimedNode = 0x004000,
        NonTimedNode = 0x008000,

        All = 0x071FFF,
    }
}
