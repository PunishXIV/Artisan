using Artisan.CraftingLists;
using Artisan.RawInformation;
using Dalamud.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using System;
using System.Linq;
using OtterGui;
using Artisan.IPC;
using Artisan.Autocraft;
using Artisan.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;

namespace Artisan.ContextMenus;

internal static class CraftingListContextMenu
{
    private static DalamudContextMenu? contextMenu;
    private static Chat2IPC? Chat2IPC;

    public static void Init()
    {
        contextMenu = new(Svc.PluginInterface);
        contextMenu.OnOpenGameObjectContextMenu += AddMenu;
        contextMenu.OnOpenInventoryContextMenu += AddInventoryMenu;

        Chat2IPC = new(Svc.PluginInterface);
        Chat2IPC.Enable();
        Chat2IPC.OnOpenChatTwoItemContextMenu += AddChat2Menu;
    }

    private static void AddChat2Menu(uint ItemId)
    {
        if (P.Config.HideContextMenus) return;

        if (!LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == ItemId)) return;

        var recipeId = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == ItemId).RowId;

        if (ImGui.Selectable($"Open Recipe Log"))
        {
            CraftingListFunctions.OpenRecipeByID(recipeId);
        }
    }

    private static void AddInventoryMenu(InventoryContextMenuOpenArgs args)
    {
        if (P.Config.HideContextMenus) return;

        if (!LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == args.ItemId)) return;

        var recipeId = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == args.ItemId).RowId;

        args.AddCustomItem(new InventoryContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Open Recipe Log")), _ => CraftingListFunctions.OpenRecipeByID(recipeId, true)));
    }

    private unsafe static void AddMenu(GameObjectContextMenuOpenArgs args)
    {
        if (P.Config.HideContextMenus) return;
        
        if (args.ParentAddonName == "RecipeNote")
        {
            IntPtr recipeNoteAgent = Svc.GameGui.FindAgentInterface(args.ParentAddonName);
            var ItemId = *(uint*)(recipeNoteAgent + 0x398);
            var craftTypeIndex = *(uint*)(recipeNoteAgent + 944);
            
            if (RetainerInfo.GetRetainerItemCount(ItemId) > 0 && RetainerInfo.GetReachableRetainerBell() != null)
            {
                int amountToGet = 1;
                if (LuminaSheets.RecipeSheet[Endurance.RecipeID].ItemResult.Row != ItemId)
                {
                    amountToGet = LuminaSheets.RecipeSheet[Endurance.RecipeID].UnkData5.First(y => y.ItemIngredient == ItemId).AmountIngredient;
                }

                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Withdraw from Retainer")), _ => RetainerInfo.RestockFromRetainers(ItemId, amountToGet)));
            }

            if (!LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == ItemId, out var recipe)) return;
            
            bool ingredientsSubCraft = recipe.UnkData5.Any(x => CraftingListHelpers.GetIngredientRecipe((uint)x.ItemIngredient) != null);
            
            if (CraftingListUI.selectedList.ID == 0)
            {
                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to New Crafting List")), _ => AddToNewList(ItemId, craftTypeIndex)));
                if (ingredientsSubCraft)
                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to New Crafting List (with Sub-crafts)")), _ => AddToNewList(ItemId, craftTypeIndex, true)));
            }
            else
            {
                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to Current Crafting List")), _ => AddToList(ItemId, craftTypeIndex)));
                if (ingredientsSubCraft)
                args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to Current Crafting List (with Sub-crafts)")), _ => AddToList(ItemId, craftTypeIndex, true)));
            }
        }

        if (args.ParentAddonName == "ChatLog")
        {
            var ItemId = GetObjectItemId("ChatLog", 0x948);
            if (ItemId > 500_000)
                ItemId -= 500_000;

            if (!LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == ItemId)) return;

            var recipeId = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == ItemId).RowId;

            args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Open Recipe Log")), _ => CraftingListFunctions.OpenRecipeByID(recipeId, true)));

        }
    }

    private static uint GetObjectItemId(uint ItemId)
    {
        if (ItemId > 500000)
            ItemId -= 500000;

        return ItemId;
    }

    private unsafe static uint? GetObjectItemId(IntPtr agent, int offset)
        => agent != IntPtr.Zero ? GetObjectItemId(*(uint*)(agent + offset)) : null;

    private static uint? GetObjectItemId(string name, int offset)
        => GetObjectItemId(Svc.GameGui.FindAgentInterface(name), offset);

    private static void AddToNewList(uint ItemId, uint craftType, bool withPrecraft = false)
    {
        NewCraftingList list = new NewCraftingList();
        list.Name = ItemId.NameOfItem();
        list.SetID();
        list.Save(true);
        CraftingListUI.selectedList = list;
        AddToList(ItemId, craftType, withPrecraft);
    }

    private static void AddToList(uint ItemId, uint craftType, bool withPrecraft = false)
    {
        CraftingListUI.listMaterialsNew.Clear();
        if (!LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == ItemId && x.CraftType.Row == craftType, out var recipe))
        {
            recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == ItemId);
        }

        if (withPrecraft)
            CraftingListUI.AddAllSubcrafts(recipe, CraftingListUI.selectedList, 1, P.Config.ContextMenuLoops);


        if (CraftingListUI.selectedList.Recipes.Any(x => x.ID == recipe.RowId))
        {
            CraftingListUI.selectedList.Recipes.First(x => x.ID == recipe.RowId).Quantity += P.Config.ContextMenuLoops;
        }
        else
        {
            CraftingListUI.selectedList.Recipes.Add(new ListItem() { ID = recipe.RowId, Quantity = P.Config.ContextMenuLoops, ListItemOptions = new ListItemOptions() { NQOnly = CraftingListUI.selectedList.AddAsQuickSynth } });   
        }

        CraftingListHelpers.TidyUpList(CraftingListUI.selectedList);
        foreach (var w in P.ws.Windows)
        {
            if (w.WindowName == $"List Editor###{CraftingListUI.selectedList.ID}")
            {
                (w as ListEditor).RecipeSelector.Items = CraftingListUI.selectedList.Recipes.ToList();
                (w as ListEditor).RefreshTable(null, true);
            }
        }

        P.Config.Save();
    }

    public static void Dispose()
    {
        contextMenu.OnOpenGameObjectContextMenu -= AddMenu;
        contextMenu.OnOpenInventoryContextMenu -= AddInventoryMenu;
        contextMenu?.Dispose();
        Chat2IPC.OnOpenChatTwoItemContextMenu -= AddChat2Menu;
        Chat2IPC.Disable();
    }
}

