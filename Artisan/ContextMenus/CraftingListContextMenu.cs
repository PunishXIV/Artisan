using Artisan.CraftingLists;
using Artisan.RawInformation;
using Dalamud.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;
using System.Linq;
using OtterGui;
using Artisan.IPC;
using Artisan.Autocraft;

namespace Artisan.ContextMenus;

internal static class CraftingListContextMenu
{
    private static readonly DalamudContextMenu contextMenu = new();

    public static void Init()
    {
        contextMenu.OnOpenGameObjectContextMenu += AddMenu;
        contextMenu.OnOpenInventoryContextMenu += AddInventoryMenu;
    }

    private static void AddInventoryMenu(InventoryContextMenuOpenArgs args)
    {
        if (Service.Configuration.HideContextMenus) return;

        if (!LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == args.ItemId)) return;

        var recipeId = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == args.ItemId).RowId;

        args.AddCustomItem(new InventoryContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Open Recipe Log")), _ => CraftingListFunctions.OpenRecipeByID(recipeId, true)));
    }

    private unsafe static void AddMenu(GameObjectContextMenuOpenArgs args)
    {
        if (Service.Configuration.HideContextMenus) return;

        if (args.ParentAddonName == "RecipeNote")
        {
            IntPtr recipeNoteAgent = Svc.GameGui.FindAgentInterface(args.ParentAddonName);
            var itemId = *(uint*)(recipeNoteAgent + 0x398);
            var craftTypeIndex = *(uint*)(recipeNoteAgent + 944);

            if (!LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == itemId)) return;

            if (CraftingListUI.selectedList.ID == 0)
            {
                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to New Crafting List")), _ => AddToNewList(itemId, craftTypeIndex)));
                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to New Crafting List (with Sub-crafts)")), _ => AddToNewList(itemId, craftTypeIndex, true)));
            }
            else
            {
                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to Current Crafting List")), _ => AddToList(itemId, craftTypeIndex)));
                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to Current Crafting List (with Sub-crafts)")), _ => AddToList(itemId, craftTypeIndex, true)));
            }

            if (RetainerInfo.GetRetainerItemCount(itemId) > 0 && RetainerInfo.GetReachableRetainerBell() != null)
            {
                int amountToGet = 1;
                if (LuminaSheets.RecipeSheet[(uint)Handler.RecipeID].ItemResult.Row != itemId)
                {
                    amountToGet = LuminaSheets.RecipeSheet[(uint)Handler.RecipeID].UnkData5.First(y => y.ItemIngredient == itemId).AmountIngredient;
                }

                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Restock from Retainer")), _ => RetainerInfo.RestockFromRetainers(itemId, amountToGet)));
            }
        }
    }

    private static void AddToNewList(uint itemId, uint craftType, bool withPrecraft = false)
    {
        CraftingList list = new CraftingList();
        list.Name = itemId.NameOfItem();
        list.SetID();
        list.Save(true);
        CraftingListUI.selectedList = list;
        AddToList(itemId, craftType, withPrecraft);
    }

    private static void AddToList(uint itemId, uint craftType, bool withPrecraft = false)
    {
        CraftingListUI.listMaterialsNew.Clear();
        if (!LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == itemId && x.CraftType.Row == craftType, out var recipe))
        {
            recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == itemId);
        }

        if (withPrecraft)
            CraftingListUI.AddAllSubcrafts(recipe, CraftingListUI.selectedList, 1, Service.Configuration.ContextMenuLoops);

        for (int i = 1; i <= Service.Configuration.ContextMenuLoops; i++)
        {
            if (CraftingListUI.selectedList.Items.IndexOf(recipe.RowId) == -1)
            {
                CraftingListUI.selectedList.Items.Add(recipe.RowId);
            }
            else
            {
                var indexOfLast = CraftingListUI.selectedList.Items.IndexOf(recipe.RowId);
                CraftingListUI.selectedList.Items.Insert(indexOfLast, recipe.RowId);
            }
        }

        if (CraftingListUI.selectedList.ListItemOptions.TryGetValue(recipe.RowId, out var opts))
        {
            opts.NQOnly = CraftingListUI.selectedList.AddAsQuickSynth;
        }
        else
        {
            CraftingListUI.selectedList.ListItemOptions.TryAdd(recipe.RowId, new ListItemOptions { NQOnly = CraftingListUI.selectedList.AddAsQuickSynth });
        }

        CraftingListHelpers.TidyUpList(CraftingListUI.selectedList);

        Service.Configuration.Save();
    }

    public static void Dispose()
    {
        contextMenu.OnOpenGameObjectContextMenu -= AddMenu;
        contextMenu.OnOpenInventoryContextMenu -= AddInventoryMenu;
    }
}

