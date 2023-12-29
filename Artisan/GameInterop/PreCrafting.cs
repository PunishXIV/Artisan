using Artisan.Autocraft;
using Artisan.CraftingLogic;
using Artisan.GameInterop.CSExt;
using Artisan.IPC;
using Artisan.RawInformation.Character;
using Dalamud.Hooking;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;

namespace Artisan.GameInterop;

// manages 'outer loop' of crafting (equipping correct items, using consumables, etc, and finally initiating crafting)
public unsafe static class PreCrafting
{
    private delegate void ClickSynthesisButton(void* a1, void* a2);
    private static Hook<ClickSynthesisButton> _clickSynthesisButtonHook;

    private enum TaskResult { Done, Retry, Abort }
    private static List<(Func<TaskResult> task, TimeSpan retryDelay)> _tasks = new();
    private static DateTime _nextRetry;

    static PreCrafting()
    {
        _clickSynthesisButtonHook = Svc.Hook.HookFromSignature<ClickSynthesisButton>("E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 33 D2", ClickSynthesisButtonDetour);
        _clickSynthesisButtonHook.Enable();
    }

    public static void Dispose()
    {
        _clickSynthesisButtonHook.Dispose();
    }

    public static void Update()
    {
        if (DateTime.Now < _nextRetry)
            return;

        while (_tasks.Count > 0)
        {
            switch (_tasks[0].task())
            {
                case TaskResult.Done:
                    _tasks.RemoveAt(0);
                    break;
                case TaskResult.Retry:
                    _nextRetry = DateTime.Now.Add(_tasks[0].retryDelay);
                    return;
                default:
                    _tasks.Clear();
                    return;
            }
        }
    }

    private static void StartCrafting(Recipe recipe, bool trial)
    {
        try
        {
            Svc.Log.Debug($"Starting {(trial ? "trial" : "real")} crafting: {recipe.RowId} '{recipe.ItemResult.Value?.Name}'");

            var requiredClass = Job.CRP + recipe.CraftType.Row;
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipe.RowId);

            bool needClassChange = requiredClass != CharacterInfo.JobID;
            bool needEquipItem = recipe.ItemRequired.Row > 0 && (needClassChange || !IsItemEquipped(recipe.ItemRequired.Row));
            // TODO: repair & extract materia
            bool needConsumables = !ConsumableChecker.IsFooded(config) || !ConsumableChecker.IsPotted(config) || !ConsumableChecker.IsManualled(config) || !ConsumableChecker.IsSquadronManualled(config);
            bool hasConsumables = config != default ? ConsumableChecker.HasItem(config.RequiredFood, config.RequiredFoodHQ) && ConsumableChecker.HasItem(config.RequiredPotion, config.RequiredPotionHQ) && ConsumableChecker.HasItem(config.RequiredManual, false) && ConsumableChecker.HasItem(config.RequiredSquadronManual, false) : true;

            // handle errors when we're forbidden from rectifying them automatically
            if (P.Config.DontEquipItems && needClassChange)
            {
                DuoLog.Error($"Can't craft {recipe.ItemResult.Value?.Name}: wrong class, {requiredClass} needed");
                return;
            }
            if (P.Config.DontEquipItems && needEquipItem)
            {
                DuoLog.Error($"Can't craft {recipe.ItemResult.Value?.Name}: required item {recipe.ItemRequired.Value?.Name} not equipped");
                return;
            }
            if (P.Config.AbortIfNoFoodPot && needConsumables && !hasConsumables)
            {
                DuoLog.Error($"Can't craft {recipe.ItemResult.Value?.Name}: required consumables not up");
                return;
            }

            bool needExitCraft = Crafting.CurState == Crafting.State.IdleBetween && (needClassChange || needEquipItem || needConsumables);

            // TODO: pre-setup solver for incoming craft
            _tasks.Clear();
            if (needExitCraft)
                _tasks.Add((TaskExitCraft, default));
            if (needClassChange)
                _tasks.Add((() => TaskClassChange(requiredClass), TimeSpan.FromMilliseconds(200))); // TODO: avoid delay and just wait until operation is done
            if (needEquipItem)
                _tasks.Add((() => TaskEquipItem(recipe.ItemRequired.Row), default));
            if (needConsumables)
                _tasks.Add((() => TaskUseConsumables(config), default));
            _tasks.Add((() => TaskSelectRecipe(recipe), default));
            _tasks.Add((() => TaskStartCraft(trial), default));
        }
        catch (Exception ex)
        {
            ex.Log();
        }
    }

    private static TaskResult TaskExitCraft()
    {
        switch (Crafting.CurState)
        {
            case Crafting.State.IdleNormal:
                return TaskResult.Done;
            case Crafting.State.IdleBetween:
                var addon = (AddonRecipeNote*)Svc.GameGui.GetAddonByName("RecipeNote");
                if (addon != null && addon->AtkUnitBase.IsVisible)
                {
                    Svc.Log.Debug("Closing recipe menu to exit crafting state");
                    Callback.Fire(&addon->AtkUnitBase, true, -1);
                }
                return TaskResult.Retry;
            default:
                Svc.Log.Error($"Unexpected state {Crafting.CurState} while trying to exit crafting mode");
                return TaskResult.Abort;
        }
    }

    private static TaskResult TaskClassChange(Job job)
    {
        if (job == CharacterInfo.JobID)
            return TaskResult.Done;

        var gearsets = RaptureGearsetModule.Instance();
        foreach (ref var gs in gearsets->EntriesSpan)
        {
            if ((Job)gs.ClassJob == job)
            {
                var result = gearsets->EquipGearset(gs.ID);
                Svc.Log.Debug($"Tried to equip gearset {gs.ID} for {job}, result={result}");
                return result < 0 ? TaskResult.Abort : TaskResult.Retry;
            }
        }

        Svc.Log.Error($"Failed to find gearset for {job}");
        return TaskResult.Abort;
    }

    private static TaskResult TaskEquipItem(uint itemId)
    {
        if (IsItemEquipped(itemId))
            return TaskResult.Done;

        var pos = FindItemInInventory(itemId, [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4, InventoryType.ArmoryMainHand, InventoryType.ArmoryHands]);
        if (pos == null)
        {
            Svc.Log.Error($"Failed to find item #{itemId} in inventory");
            return TaskResult.Abort;
        }

        var agentId = pos.Value.inv is InventoryType.ArmoryMainHand or InventoryType.ArmoryHands ? AgentId.ArmouryBoard : AgentId.Inventory;
        var addonId = AgentModule.Instance()->GetAgentByInternalId(agentId)->GetAddonID();
        var ctx = AgentInventoryContext.Instance();
        ctx->OpenForItemSlot(pos.Value.inv, pos.Value.slot, addonId);

        var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu");
        if (contextMenu != null)
        {
            var firstEntryIsEquip = ctx->EventIdSpan[7] == 25; // i'th entry will fire eventid 7+i; eventid 25 is 'equip'
            if (firstEntryIsEquip)
                Svc.Log.Debug($"Equipping item #{itemId} from {pos.Value.inv} @ {pos.Value.slot}");
            Callback.Fire(contextMenu, true, 0, firstEntryIsEquip ? 0 : -1, 0, 0, 0); // p2=-1 is close, p2=0 is exec first command
        }
        return TaskResult.Retry;
    }

    private static TaskResult TaskUseConsumables(RecipeConfig? config)
    {
        if (ActionManagerEx.AnimationLock > 0)
            return TaskResult.Retry; // waiting for animation lock to end

        if (!ConsumableChecker.IsFooded(config))
        {
            var foodId = config.RequiredFood + (config.RequiredFoodHQ ? 1000000u : 0);
            if (ActionManagerEx.CanUseAction(ActionType.Item, foodId))
            {
                Svc.Log.Debug($"Using food: {foodId}");
                ActionManagerEx.UseItem(foodId);
                return TaskResult.Retry;
            }
            else
            {
                DuoLog.Error("Can't use food required for crafting");
                return TaskResult.Abort;
            }
        }

        if (!ConsumableChecker.IsPotted(config))
        {
            var potId = config.RequiredPotion + (config.RequiredPotionHQ ? 1000000u : 0);
            if (ActionManagerEx.CanUseAction(ActionType.Item, potId))
            {
                Svc.Log.Debug($"Using pot: {potId}");
                ActionManagerEx.UseItem(potId);
                return TaskResult.Retry;
            }
            else
            {
                DuoLog.Error("Can't use medicine required for crafting");
                return TaskResult.Abort;
            }
        }

        if (!ConsumableChecker.IsManualled(config))
        {
            if (ActionManagerEx.CanUseAction(ActionType.Item, config.RequiredManual))
            {
                Svc.Log.Debug($"Using manual: {config.RequiredManual}");
                ActionManagerEx.UseItem(config.RequiredManual);
                return TaskResult.Retry;
            }
            else
            {
                DuoLog.Error("Can't use manual required for crafting");
                return TaskResult.Abort;
            }
        }

        if (!ConsumableChecker.IsSquadronManualled(config))
        {
            if (ActionManagerEx.CanUseAction(ActionType.Item, config.RequiredSquadronManual))
            {
                Svc.Log.Debug($"Using squadron manual: {config.RequiredSquadronManual}");
                ActionManagerEx.UseItem(config.RequiredSquadronManual);
                return TaskResult.Retry;
            }
            else
            {
                DuoLog.Error("Can't use squadron manual required for crafting");
                return TaskResult.Abort;
            }
        }

        return TaskResult.Done;
    }

    private static TaskResult TaskSelectRecipe(Recipe recipe)
    {
        var rd = RecipeNoteRecipeData.Ptr();
        var re = rd != null && rd->Recipes != null ? rd->Recipes + rd->SelectedIndex : null;
        if (re != null && re->RecipeId == recipe.RowId)
            return TaskResult.Done;

        Svc.Log.Debug($"Opening recipe {recipe.RowId}");
        AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RowId);
        return TaskResult.Retry;
    }

    private static TaskResult TaskStartCraft(bool trial)
    {
        var addon = (AddonRecipeNote*)Svc.GameGui.GetAddonByName("RecipeNote");
        if (addon == null)
            return TaskResult.Retry;

        Svc.Log.Debug($"Starting {(trial ? "trial" : "real")} craft");
        Callback.Fire(&addon->AtkUnitBase, true, trial ? 10 : 8);
        return TaskResult.Done;
    }

    private static bool IsItemEquipped(uint itemId) => InventoryManager.Instance()->GetItemCountInContainer(itemId, InventoryType.EquippedItems) > 0;

    private static (InventoryType inv, int slot)? FindItemInInventory(uint itemId, IEnumerable<InventoryType> inventories)
    {
        foreach (var inv in inventories)
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for (int i = 0; i < cont->Size; ++i)
            {
                if (cont->GetInventorySlot(i)->ItemID == itemId)
                {
                    return (inv, i);
                }
            }
        }
        return null;
    }

    private static void ClickSynthesisButtonDetour(void* a1, void* a2)
    {
        var rd = RecipeNoteRecipeData.Ptr();
        var re = rd != null && rd->Recipes != null ? rd->Recipes + rd->SelectedIndex : null;
        var recipe = re != null ? Svc.Data.GetExcelSheet<Recipe>()?.GetRow(re->RecipeId) : null;
        if (recipe != null)
            StartCrafting(recipe, false);
    }
}
