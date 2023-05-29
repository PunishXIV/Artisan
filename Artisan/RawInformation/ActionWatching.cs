using Artisan.CraftingLogic;
using Artisan.RawInformation.Character;
using Dalamud.Hooking;
using Dalamud.Logging;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using static Artisan.CraftingLogic.CurrentCraft;
using static ECommons.GenericHelpers;

namespace Artisan.RawInformation
{
    internal unsafe class ActionWatching
    {
        public delegate byte UseActionDelegate(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
        public static Hook<UseActionDelegate> UseActionHook;
        public static uint LastUsedAction = 0;

        private delegate void* ClickSynthesisButton(void* a1, void* a2);
        private static Hook<ClickSynthesisButton> clickSysnthesisButtonHook;
        private static byte UseActionDetour(ActionManager* actionManager, uint actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
        {
            try
            {
                if (CurrentCraftMethods.CanUse(actionID))
                {
                    PluginLog.Debug($"{actionID.NameOfAction()}");
                    PreviousAction = actionID;

                    if (LuminaSheets.ActionSheet.TryGetValue(actionID, out var act1))
                    {
                        string skillName = act1.Name;
                        var allOfSameName = LuminaSheets.ActionSheet.Where(x => x.Value.Name == skillName).Select(x => x.Key);

                        if (allOfSameName.Any(x => x == Skills.Manipulation))
                            ManipulationUsed = true;

                        if (allOfSameName.Any(x => x == Skills.WasteNot || x == Skills.WasteNot2))
                            WasteNotUsed = true;

                        if (allOfSameName.Any(x => x == Skills.FinalAppraisal))
                        {
                            JustUsedFinalAppraisal = true;
                            CurrentRecommendation = 0;
                            Artisan.Tasks.Clear();
                        }
                        else
                            JustUsedFinalAppraisal = false;

                        if (allOfSameName.Any(x => x == Skills.GreatStrides))
                            JustUsedGreatStrides = true;
                        else
                            JustUsedGreatStrides = false;

                        if (allOfSameName.Any(x => x == Skills.Innovation))
                            InnovationUsed = true;

                        if (allOfSameName.Any(x => x == Skills.Veneration))
                            VenerationUsed = true;

                        JustUsedObserve = false;
                        BasicTouchUsed = false;
                        StandardTouchUsed = false;
                        AdvancedTouchUsed = false;

                    }
                    if (LuminaSheets.CraftActions.TryGetValue(actionID, out var act2))
                    {
                        string skillName = act2.Name;
                        var allOfSameName = LuminaSheets.CraftActions.Where(x => x.Value.Name == skillName).Select(x => x.Key);

                        if (allOfSameName.Any(x => x == Skills.Observe))
                            JustUsedObserve = true;
                        else
                            JustUsedObserve = false;

                        if (allOfSameName.Any(x => x == Skills.BasicTouch))
                            BasicTouchUsed = true;
                        else
                            BasicTouchUsed = false;

                        if (allOfSameName.Any(x => x == Skills.StandardTouch))
                            StandardTouchUsed = true;
                        else
                            StandardTouchUsed = false;

                        if (allOfSameName.Any(x => x == Skills.AdvancedTouch))
                            AdvancedTouchUsed = true;
                        else
                            AdvancedTouchUsed = false;

                        JustUsedFinalAppraisal = false;

                        if (allOfSameName.Any(x => x == Skills.HeartAndSoul))
                        {
                            CurrentRecommendation = 0;
                            Artisan.Tasks.Clear();
                        }

                        if (allOfSameName.Any(x => x == Skills.CarefulObservation))
                        {
                            CurrentRecommendation = 0;
                            Artisan.Tasks.Clear();
                        }
                    }
                    if (Service.Configuration.UseMacroMode)
                    {
                        MacroStep++;
                    }
                }
                return UseActionHook!.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "UseActionDetour");
                return UseActionHook!.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);
            }
        }

        static ActionWatching()
        {
            UseActionHook ??= Hook<UseActionDelegate>.FromAddress((nint)ActionManager.Addresses.UseAction.Value, UseActionDetour);
            clickSysnthesisButtonHook ??= Hook<ClickSynthesisButton>.FromAddress(Svc.SigScanner.ScanText("E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 33 D2"), ClickSynthesisButtonDetour);
            clickSysnthesisButtonHook?.Enable();
        }

        private static void* ClickSynthesisButtonDetour(void* a1, void* a2)
        {
            try
            {
                if (Service.Configuration.DontEquipItems)
                    return clickSysnthesisButtonHook.Original(a1, a2);

                uint requiredClass = 0;
                var readyState = GetCraftReadyState(ref requiredClass, out var selectedRecipeId);
                var recipe = LuminaSheets.RecipeSheet[selectedRecipeId];
                if (recipe.ItemRequired.Row > 0)
                {
                    bool hasItem = InventoryManager.Instance()->GetInventoryItemCount(recipe.ItemRequired.Row) +
                        InventoryManager.Instance()->GetItemCountInContainer(recipe.ItemRequired.Row, InventoryType.ArmoryMainHand) +
                        InventoryManager.Instance()->GetItemCountInContainer(recipe.ItemRequired.Row, InventoryType.ArmoryHands) >= 1;

                    if (hasItem)
                    {
                        if (InventoryManager.Instance()->GetInventoryItemCount(recipe.ItemRequired.Row, false, false, false) == 1)
                        {
                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory1)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory1)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.Inventory1, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory2)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory2)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.Inventory2, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory3)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory3)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.Inventory3, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory4)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Inventory4)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.Inventory4, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Inventory)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                        }
                        if (InventoryManager.Instance()->GetItemCountInContainer(recipe.ItemRequired.Row, InventoryType.ArmoryHands) == 1)
                        {

                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmoryHands)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmoryHands)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.ArmoryHands, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.ArmouryBoard)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                        }
                        if (InventoryManager.Instance()->GetItemCountInContainer(recipe.ItemRequired.Row, InventoryType.ArmoryMainHand) == 1)
                        {

                            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmoryMainHand)->Size; i++)
                            {
                                var item = InventoryManager.Instance()->GetInventoryContainer(InventoryType.ArmoryMainHand)->GetInventorySlot(i);
                                if (item->ItemID == recipe.ItemRequired.Row)
                                {
                                    var ag = AgentInventoryContext.Instance();
                                    ag->OpenForItemSlot(InventoryType.ArmoryMainHand, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.ArmouryBoard)->GetAddonID());
                                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                                    if (contextMenu != null)
                                        Callback.Fire(contextMenu, true, 0, 0, 0, 0, 0);
                                }
                            }
                        }

                    }
                }

            }
            catch
            {

            }
            return clickSysnthesisButtonHook.Original(a1, a2);
        }

        public static void TryEnable()
        {
            if (!UseActionHook.IsEnabled)
                UseActionHook?.Enable();
        }

        public static void TryDisable()
        {
            if (UseActionHook.IsEnabled)
                UseActionHook?.Disable();
        }
        public static void Enable()
        {
            UseActionHook?.Enable();
        }

        public static void Disable()
        {
            UseActionHook?.Disable();
        }

        public static void Dispose()
        {
            UseActionHook?.Dispose();
            clickSysnthesisButtonHook?.Dispose();
        }

        public enum CraftReadyState
        {
            NotReady,
            Ready,
            WrongClass,
            AlreadyCrafting,
        }

        private static CraftReadyState GetCraftReadyState(out ushort selectedRecipeId)
        {
            uint requiredClass = 0;
            return GetCraftReadyState(ref requiredClass, out selectedRecipeId);
        }

        private static CraftReadyState GetCraftReadyState(ref uint requiredClass, out ushort selectedRecipeId)
        {
            selectedRecipeId = 0;
            if (Service.ClientState.LocalPlayer == null) return CraftReadyState.NotReady;
            var uiRecipeNote = RecipeNote.Instance();
            if (uiRecipeNote == null || uiRecipeNote->RecipeList == null) return CraftReadyState.NotReady;
            var selectedRecipe = uiRecipeNote->RecipeList->SelectedRecipe;
            if (selectedRecipe == null) return CraftReadyState.NotReady;
            selectedRecipeId = selectedRecipe->RecipeId;
            requiredClass = uiRecipeNote->Jobs[selectedRecipe->CraftType];
            var requiredJob = Svc.Data.Excel.GetSheet<ClassJob>()?.GetRow(requiredClass);
            if (requiredJob == null) return CraftReadyState.NotReady;
            if (Service.ClientState.LocalPlayer.ClassJob.Id == requiredClass) return CraftReadyState.Ready;
            var localPlayer = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)Service.ClientState.LocalPlayer.Address;
            return localPlayer->EventState == 5 ? CraftReadyState.AlreadyCrafting : CraftReadyState.WrongClass;
        }

    }
}
