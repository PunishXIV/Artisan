using System.Linq;

using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.RawInformation;

using Dalamud.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Logging;

using ECommons.DalamudServices;

using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Artisan.ContextMenus
{
    using System;

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

                if (!LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == itemId)) return;

                if (CraftingListUI.selectedList.ID == 0)
                {
                    args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to New Crafting List")), _ => AddToNewList(itemId)));
                    args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to New Crafting List (with Sub-crafts)")), _ => AddToNewList(itemId, true)));
                }
                else
                {
                    args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to Current Crafting List")), _ => AddToList(itemId)));
                    args.AddCustomItem(new GameObjectContextMenuItem(new Dalamud.Game.Text.SeStringHandling.SeString(new UIForegroundPayload(706), new TextPayload($"{SeIconChar.BoxedLetterA.ToIconString()} "), UIForegroundPayload.UIForegroundOff, new TextPayload("Add to Current Crafting List (with Sub-crafts)")), _ => AddToList(itemId, true)));
                }
            }

            if (args.ParentAddonName == "CharacterInspect")
            {
                IntPtr agent = (nint)Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalId(FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId.Inspect);

            }

        }

        private static void AddToNewList(uint itemId, bool withPrecraft = false)
        {
            CraftingList list = new CraftingList();
            list.Name = itemId.NameOfItem();
            list.SetID();
            list.Save(true);
            CraftingListUI.selectedList = list;
            AddToList(itemId, withPrecraft);
        }

        private static void AddToList(uint itemId, bool withPrecraft = false)
        {
            CraftingListUI.listMaterialsNew.Clear();

            if (withPrecraft)
                CraftingListUI.AddAllSubcrafts(LuminaSheets.RecipeSheet[(uint)Handler.RecipeID], CraftingListUI.selectedList, 1, Service.Configuration.ContextMenuLoops);

            for (int i = 1; i <= Service.Configuration.ContextMenuLoops; i++)
            {
                if (CraftingListUI.selectedList.Items.IndexOf(LuminaSheets.RecipeSheet[(uint)Handler.RecipeID].RowId) == -1)
                {
                    CraftingListUI.selectedList.Items.Add(LuminaSheets.RecipeSheet[(uint)Handler.RecipeID].RowId);
                }
                else
                {
                    var indexOfLast = CraftingListUI.selectedList.Items.IndexOf(LuminaSheets.RecipeSheet[(uint)Handler.RecipeID].RowId);
                    CraftingListUI.selectedList.Items.Insert(indexOfLast, LuminaSheets.RecipeSheet[(uint)Handler.RecipeID].RowId);
                }
            }

            if (CraftingListUI.selectedList.ListItemOptions.TryGetValue(LuminaSheets.RecipeSheet[(uint)Handler.RecipeID].RowId, out var opts))
            {
                opts.NQOnly = CraftingListUI.selectedList.AddAsQuickSynth;
            }
            else
            {
                CraftingListUI.selectedList.ListItemOptions.TryAdd(LuminaSheets.RecipeSheet[(uint)Handler.RecipeID].RowId, new ListItemOptions { NQOnly = CraftingListUI.selectedList.AddAsQuickSynth });
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
}
