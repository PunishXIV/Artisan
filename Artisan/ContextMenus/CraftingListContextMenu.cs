using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.ContextMenu;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Artisan.ContextMenus;

internal static class CraftingListContextMenu
{
    private static IContextMenu? contextMenu;
    private static Chat2IPC? Chat2IPC;

    public const int SatisfactionSupplyItemIdx = 0x54;
    public const int SatisfactionSupplyItem1Id = 0x80 + 1 * 0x3C;
    public const int SatisfactionSupplyItem2Id = 0x80 + 2 * 0x3C;
    public const int ContentsInfoDetailContextItemId = 0x17CC;
    public const int RecipeNoteContextItemId = 0x398;
    public const int AgentItemContextItemId = 0x28;
    public const int GatheringNoteContextItemId = 0xA0;
    public const int ItemSearchContextItemId = 6192;
    public const int ChatLogContextItemId = 2496;

    public const int SubmarinePartsMenuContextItemId = 0x54;
    public const int ShopExchangeItemContextItemId = 0x54;
    public const int ShopContextMenuItemId = 0x54;
    public const int ShopExchangeCurrencyContextItemId = 0x54;
    public const int HWDSupplyContextItemId = 0x38C;
    public const int GrandCompanySupplyListContextItemId = 0x54;
    public const int GrandCompanyExchangeContextItemId = 0x54;
    public const int AgentMiragePrismPrismItemDetailId = 84;


    public static void Init()
    {
        contextMenu = Svc.ContextMenu;
        contextMenu.OnMenuOpened += AddMenu;

        Chat2IPC = new(Svc.PluginInterface);
        Chat2IPC.Enable();
        Chat2IPC.OnOpenChatTwoItemContextMenu += AddChat2Menu;

        Svc.Log.Debug($"Init Context Menus.");
    }

    private static void AddChat2Menu(uint ItemId)
    {
        if (P.Config.HideContextMenus) return;

        if (!LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.RowId == ItemId)) return;

        var recipeId = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.RowId == ItemId).RowId;

        if (ImGui.Selectable($"Open Recipe Log"))
        {
            CraftingListFunctions.OpenRecipeByID(recipeId);
        }
    }

    private static void AddInventoryMenu(IMenuOpenedArgs args)
    {
        if (P.Config.HideContextMenus) return;


    }

    private unsafe static void AddMenu(IMenuOpenedArgs args)
    {
        if (P.Config.HideContextMenus) return;
        if (args.AddonName != "RecipeNote")
        {
            uint? id = GetGameObjectItemId(args);
            if (id == null)
                return;

            var itemId = id.Value;
            var item = LuminaSheets.ItemSheet[itemId];
            Svc.Log.Debug($"{item.Name}");

            var recipe = LuminaSheets.RecipeSheet.Values.FirstOrNull(x => x.ItemResult.RowId == itemId);
            if (recipe != null)
            {
                var menuItem = new MenuItem();
                menuItem.Name = "Open Recipe Log";
                menuItem.PrefixChar = 'A';
                menuItem.PrefixColor = 706;
                menuItem.OnClicked += clickedArgs => CraftingListFunctions.OpenRecipeByID(recipe.Value.RowId, true);

                args.AddMenuItem(menuItem);

                bool ingredientsSubCraft = recipe.Value.Ingredients().Any(x => CraftingListHelpers.GetIngredientRecipe(x.Item.RowId) != null);

                var subMenu = new MenuItem();
                subMenu.IsSubmenu = true;
                subMenu.Name = "Artisan Crafting List";
                subMenu.PrefixChar = 'A';
                subMenu.PrefixColor = 706;

                subMenu.OnClicked += args => OpenArtisanCraftingListSubmenu(args, itemId, recipe.Value.CraftType.RowId, ingredientsSubCraft);

                args.AddMenuItem(subMenu);
            }

            if (item.ItemUICategory.RowId is 112) //Outfits
            {
                var listOfRecipes = new List<uint>();
                var outfitSet = Svc.Data.GetExcelSheet<MirageStoreSetItem>().GetRow(itemId);
                foreach (var piece in outfitSet.Items)
                {
                    if (piece.RowId != 0 && LuminaSheets.RecipeSheet.Values.TryGetFirst(x => x.ItemResult.RowId == piece.RowId, out var rec))
                    {
                        Svc.Log.Debug($"This is craftable");
                        listOfRecipes.Add(rec.RowId);
                    }
                }

                if (listOfRecipes.Count > 0)
                {
                    var menuItem = new MenuItem();
                    menuItem.Name = "Create Artisan Crafting List For Outfit";
                    menuItem.IsSubmenu = true;
                    menuItem.PrefixChar = 'A';
                    menuItem.PrefixColor = 706;
                    menuItem.OnClicked += args => OpenOutfitCraftingListSubmenu(args, item.Name.ToString(), listOfRecipes);

                    args.AddMenuItem(menuItem);
                }
            }
        }

        if (args.AddonName == "RecipeNote")
        {
            IntPtr recipeNoteAgent = Svc.GameGui.FindAgentInterface(args.AddonName);
            var ItemId = *(uint*)(recipeNoteAgent + 0x398);
            var craftTypeIndex = *(uint*)(recipeNoteAgent + 944);

            if (RetainerInfo.GetRetainerItemCount(ItemId) > 0 && RetainerInfo.GetReachableRetainerBell() != null)
            {
                int amountToGet = 1;
                if (LuminaSheets.RecipeSheet[Endurance.RecipeID].ItemResult.RowId != ItemId)
                {
                    amountToGet = LuminaSheets.RecipeSheet[Endurance.RecipeID].Ingredients().First(y => y.Item.RowId == ItemId).Amount;
                }

                var menuItem = new MenuItem();
                menuItem.Name = "Withdraw from Retainer";
                menuItem.PrefixChar = 'A';
                menuItem.PrefixColor = 706;
                menuItem.OnClicked += clickedArgs => RetainerInfo.RestockFromRetainers(ItemId, amountToGet);

                args.AddMenuItem(menuItem);
            }

            if (!LuminaSheets.RecipeSheet.Values.TryGetFirst(x => x.ItemResult.RowId == ItemId, out var recipe)) return;

            bool ingredientsSubCraft = recipe.Ingredients().Any(x => CraftingListHelpers.GetIngredientRecipe(x.Item.RowId) != null);

            var subMenu = new MenuItem();
            subMenu.IsSubmenu = true;
            subMenu.Name = "Artisan Crafting List";
            subMenu.PrefixChar = 'A';
            subMenu.PrefixColor = 706;

            subMenu.OnClicked += args => OpenArtisanCraftingListSubmenu(args, ItemId, craftTypeIndex, ingredientsSubCraft);

            args.AddMenuItem(subMenu);
        }
    }

    private static void OpenOutfitCraftingListSubmenu(IMenuItemClickedArgs args, string outfitName, List<uint> listOfRecipes)
    {
        var menuItems = new List<MenuItem>();

        var noSubs = new MenuItem();
        noSubs.Name = "Without Sub-Crafts";
        noSubs.PrefixChar = 'A';
        noSubs.PrefixColor = 706;
        noSubs.OnClicked += clickedArgs => CreateOutfitList(outfitName, listOfRecipes, false);
        menuItems.Add(noSubs);

        var withSubs = new MenuItem();
        withSubs.Name = "With Sub-Crafts";
        withSubs.PrefixChar = 'A';
        withSubs.PrefixColor = 706;
        withSubs.OnClicked += clickedArgs => CreateOutfitList(outfitName, listOfRecipes, true);
        menuItems.Add(withSubs);

        args.OpenSubmenu(menuItems);
    }

    private async static void CreateOutfitList(string name, List<uint> listOfRecipes, bool withSubCrafts)
    {
        NewCraftingList list = new NewCraftingList();
        list.Name = name;
        list.SetID();
        list.Save(true);
        list.Locked = true;
        CraftingListUI.selectedList = list;
        foreach (var rec in listOfRecipes)
        {
            var recipe = LuminaSheets.RecipeSheet[rec];
            await AddToList(recipe.ItemResult.RowId, recipe.CraftType.RowId, withSubCrafts);
        }
        CraftingListHelpers.TidyUpList(list);
        list.Locked = false;
    }

    private static unsafe void OpenArtisanCraftingListSubmenu(IMenuItemClickedArgs args, uint ItemId, uint craftTypeIndex, bool ingredientsSubCraft)
    {
        var menuItems = new List<MenuItem>();
        if (CraftingListUI.selectedList.ID == 0)
        {
            var menuItem = new MenuItem();
            menuItem.Name = "Add to New Artisan Crafting List";
            menuItem.PrefixChar = 'A';
            menuItem.PrefixColor = 706;
            menuItem.OnClicked += clickedArgs => AddToNewList(ItemId, craftTypeIndex);

            menuItems.Add(menuItem);
            if (ingredientsSubCraft)
            {
                var menuItem2 = new MenuItem();
                menuItem2.Name = "Add to New Artisan Crafting List (with Sub-crafts)";
                menuItem2.PrefixChar = 'A';
                menuItem2.PrefixColor = 706;
                menuItem2.OnClicked += clickedArgs => AddToNewList(ItemId, craftTypeIndex, true);

                menuItems.Add(menuItem2);
            }
        }
        else
        {
            var menuItem = new MenuItem();
            menuItem.Name = "Add to Current Artisan Crafting List";
            menuItem.PrefixChar = 'A';
            menuItem.PrefixColor = 706;
            menuItem.OnClicked += clickedArgs => AddToList(ItemId, craftTypeIndex);

            menuItems.Add(menuItem);
            if (ingredientsSubCraft)
            {
                var menuItem2 = new MenuItem();
                menuItem2.Name = "Add to Current Artisan Crafting List (with Sub-crafts)";
                menuItem2.PrefixChar = 'A';
                menuItem2.PrefixColor = 706;
                menuItem2.OnClicked += clickedArgs => AddToList(ItemId, craftTypeIndex, true);

                menuItems.Add(menuItem2);
            }
        }
        if (menuItems.Count > 0)
            args.OpenSubmenu(menuItems);
    }

    private unsafe static uint? GetGameObjectItemId(IMenuOpenedArgs args)
    {
        Svc.Log.Debug($"{args.AddonName}");
        uint? item;
        if (args.AddonName == "MiragePrismPrismBoxCrystallize")
        {
            var itemDetail = UIModule.Instance()->GetAgentModule()->GetAgentByInternalId(AgentId.ItemDetail);
            if (itemDetail != null && itemDetail->IsAgentActive())
            {
                AgentItemDetail* agentItemDetail = (AgentItemDetail*)itemDetail;
                item = agentItemDetail->ItemId;
            }
            else
            {
                item = GetObjectItemId(AgentById(AgentId.MiragePrismPrismItemDetail), AgentMiragePrismPrismItemDetailId);
            }
        }
        else
            item = args.AddonName switch
            {
                null => HandleNulls(),
                "Shop" => GetObjectItemId("Shop", ShopContextMenuItemId),
                "GrandCompanySupplyList" => GetObjectItemId("GrandCompanySupplyList", GrandCompanySupplyListContextItemId),
                "GrandCompanyExchange" => GetObjectItemId("GrandCompanyExchange", GrandCompanyExchangeContextItemId),
                "ShopExchangeCurrency" => GetObjectItemId("ShopExchangeCurrency", ShopExchangeCurrencyContextItemId),
                "SubmarinePartsMenu" => GetObjectItemId("SubmarinePartsMenu", SubmarinePartsMenuContextItemId),
                "ShopExchangeItem" => GetObjectItemId("ShopExchangeItem", ShopExchangeItemContextItemId),
                "ContentsInfoDetail" => GetObjectItemId("ContentsInfo", ContentsInfoDetailContextItemId),
                "RecipeNote" => GetObjectItemId("RecipeNote", RecipeNoteContextItemId),
                "RecipeTree" => GetObjectItemId(AgentById(AgentId.RecipeItemContext), AgentItemContextItemId),
                "RecipeMaterialList" => GetObjectItemId(AgentById(AgentId.RecipeItemContext), AgentItemContextItemId),
                "RecipeProductList" => GetObjectItemId(AgentById(AgentId.RecipeItemContext), AgentItemContextItemId),
                "GatheringNote" => GetObjectItemId("GatheringNote", GatheringNoteContextItemId),
                "ItemSearch" => GetObjectItemId(args.AgentPtr, ItemSearchContextItemId),
                "ChatLog" => GetObjectItemId("ChatLog", ChatLogContextItemId),
                _ => null,
            };

        if (item == null)
        {
            var guiHoveredItem = Svc.GameGui.HoveredItem;
            if (guiHoveredItem >= 2000000 || guiHoveredItem == 0) return null;
            item = (uint)guiHoveredItem % 500_000;
        }

        return item;
    }

    private static unsafe IntPtr AgentById(AgentId id)
    {
        var uiModule = (UIModule*)Svc.GameGui.GetUIModule().Address;
        var agents = uiModule->GetAgentModule();
        var agent = agents->GetAgentByInternalId(id);
        return (IntPtr)agent;
    }

    private static uint GetObjectItemId(uint ItemId)
    {
        if (ItemId > 500000)
            ItemId -= 500000;

        return ItemId;
    }

    private static unsafe uint? HandleSatisfactionSupply()
    {
        var agent = Svc.GameGui.FindAgentInterface("SatisfactionSupply");
        if (agent == IntPtr.Zero)
            return null;

        var itemIdx = *(byte*)(agent + SatisfactionSupplyItemIdx);
        return itemIdx switch
        {
            1 => GetObjectItemId(*(uint*)(agent + SatisfactionSupplyItem1Id)),
            2 => GetObjectItemId(*(uint*)(agent + SatisfactionSupplyItem2Id)),
            _ => null,
        };
    }
    private static unsafe uint? HandleHWDSupply()
    {
        var agent = Svc.GameGui.FindAgentInterface("HWDSupply");
        if (agent == IntPtr.Zero)
            return null;

        return GetObjectItemId(*(uint*)(agent + HWDSupplyContextItemId));
    }
    private static uint? HandleNulls()
    {
        var itemId = HandleSatisfactionSupply() ?? HandleHWDSupply();
        return itemId;
    }

    private unsafe static uint? GetObjectItemId(IntPtr agent, int offset)
        => agent != IntPtr.Zero ? GetObjectItemId(*(uint*)(agent + offset)) : null;


    private static unsafe void DebugObjectItemId(IntPtr agent, uint itemId)
    {
        for (int i = 0; i <= 9999; i++)
        {
            try
            {
                var id = GetObjectItemId(*(uint*)(agent + i));
                if (id == itemId)
                    Svc.Log.Debug($"Your target is {i}");
            }
            catch
            {

            }
        }
    }

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

    private static Task AddToList(uint ItemId, uint craftType, bool withPrecraft = false)
    {
        return Task.Run(() =>
        {
            CraftingListUI.listMaterialsNew.Clear();
            if (!LuminaSheets.RecipeSheet.Values.TryGetFirst(x => x.ItemResult.RowId == ItemId && x.CraftType.RowId == craftType, out var recipe))
            {
                recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.RowId == ItemId);
            }
            if (recipe.Number == 0) return;
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

            P.Config.Save();
        });
    }

    public static void Dispose()
    {
        contextMenu.OnMenuOpened -= AddMenu;
        Chat2IPC.OnOpenChatTwoItemContextMenu -= AddChat2Menu;
        Chat2IPC.Disable();
    }
}

