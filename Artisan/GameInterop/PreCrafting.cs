using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.GameInterop.CSExt;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Game.ClientState.Conditions;
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
using System.Numerics;
using static ECommons.GenericHelpers;

namespace Artisan.GameInterop;

// manages 'outer loop' of crafting (equipping correct items, using consumables, etc, and finally initiating crafting)
public unsafe static class PreCrafting
{
    public enum CraftType { Normal, Quick, Trial }

    private static int equipAttemptLoops = 0;

    private static long NextTaskAt = 0;

    private delegate void ClickSynthesisButton(void* a1, void* a2);
    private static Hook<ClickSynthesisButton> _clickNormalSynthesisButtonHook;
    private static Hook<ClickSynthesisButton> _clickQuickSynthesisButtonHook;
    private static Hook<ClickSynthesisButton> _clickTrialSynthesisButtonHook;

    public enum TaskResult { Done, Retry, Abort }
    public static List<(Func<TaskResult> task, TimeSpan retryDelay)> Tasks = new();
    private static DateTime _nextRetry;

    static PreCrafting()
    {
        _clickNormalSynthesisButtonHook = Svc.Hook.HookFromSignature<ClickSynthesisButton>("E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 33 D2", ClickNormalSynthesisButtonDetour);
        _clickNormalSynthesisButtonHook.Enable();

        _clickQuickSynthesisButtonHook = Svc.Hook.HookFromSignature<ClickSynthesisButton>("E9 ?? ?? ?? ?? 4C 8B 44 24 ?? 49 8B D2 48 8B CB 48 83 C4 30 5B E9 ?? ?? ?? ?? 33 D2 49 8B CA E8 ?? ?? ?? ?? 83 CA FF", ClickQuickSynthesisButtonDetour);
        _clickQuickSynthesisButtonHook.Enable();

        _clickTrialSynthesisButtonHook = Svc.Hook.HookFromSignature<ClickSynthesisButton>("E9 ?? ?? ?? ?? 33 D2 49 8B CA E8 ?? ?? ?? ?? 83 CA FF", ClickTrialSynthesisButtonDetour);
        _clickTrialSynthesisButtonHook.Enable();
    }

    public static void Dispose()
    {
        _clickNormalSynthesisButtonHook.Dispose();
        _clickQuickSynthesisButtonHook.Dispose();
        _clickTrialSynthesisButtonHook.Dispose();
    }

    public static void Update()
    {
        if (DateTime.Now < _nextRetry)
            return;

        while (Tasks.Count > 0)
        {
            switch (Tasks[0].task())
            {
                case TaskResult.Done:
                    Tasks.RemoveAt(0);
                    break;
                case TaskResult.Retry:
                    _nextRetry = DateTime.Now.Add(Tasks[0].retryDelay);
                    return;
                case TaskResult.Abort:
                    Tasks.Clear();
                    return;
            }
        }
    }

    private static void StartCrafting(Recipe recipe, CraftType type)
    {
        try
        {
            Svc.Log.Debug($"Starting {type} crafting: {recipe.RowId} '{recipe.ItemResult.Value?.Name}'");

            var requiredClass = Job.CRP + recipe.CraftType.Row;
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipe.RowId);

            bool hasIngredients = GetNumberCraftable(recipe) > 0;
            bool needClassChange = requiredClass != CharacterInfo.JobID;
            bool needEquipItem = recipe.ItemRequired.Row > 0 && (needClassChange || !IsItemEquipped(recipe.ItemRequired.Row));
            // TODO: repair & extract materia
            bool needConsumables = (type == CraftType.Normal || (type == CraftType.Trial && P.Config.UseConsumablesTrial) || (type == CraftType.Quick && P.Config.UseConsumablesQuickSynth)) && (!ConsumableChecker.IsFooded(config) || !ConsumableChecker.IsPotted(config) || !ConsumableChecker.IsManualled(config) || !ConsumableChecker.IsSquadronManualled(config));
            bool hasConsumables = config != default ? ConsumableChecker.HasItem(config.RequiredFood, config.RequiredFoodHQ) && ConsumableChecker.HasItem(config.RequiredPotion, config.RequiredPotionHQ) && ConsumableChecker.HasItem(config.RequiredManual, false) && ConsumableChecker.HasItem(config.RequiredSquadronManual, false) : true;

            // handle errors when we're forbidden from rectifying them automatically
            if (!hasIngredients && type != CraftType.Trial)
            {
                DuoLog.Error($"Not all ingredients for {recipe.ItemResult.Value?.Name} found.");
                return;
            }
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
            Tasks.Clear();
            _nextRetry = default;
            if (needExitCraft)
                Tasks.Add((TaskExitCraft, default));
            if (needClassChange)
                Tasks.Add((() => TaskClassChange(requiredClass), TimeSpan.FromMilliseconds(200))); // TODO: avoid delay and just wait until operation is done
            if (needEquipItem)
            {
                equipAttemptLoops = 0;
                Tasks.Add((() => TaskEquipItem(recipe.ItemRequired.Row), default));
            }
            if (needConsumables)
                Tasks.Add((() => TaskUseConsumables(config, type), default));
            Tasks.Add((() => TaskSelectRecipe(recipe), default));
            Tasks.Add((() => TaskStartCraft(type), default));

            Update();
        }
        catch (Exception ex)
        {
            ex.Log();
        }
    }

    public static int GetNumberCraftable(Recipe recipe)
    {
        if (TryGetAddonByName<AddonRecipeNoteFixed>("RecipeNote", out var addon) && addon->SelectedRecipeQuantityCraftableFromMaterialsInInventory != null)
        {
            if (int.TryParse(addon->SelectedRecipeQuantityCraftableFromMaterialsInInventory->NodeText.ToString(), out int output))
            return output;
        }
        return -1;
    }

    public static TaskResult TaskExitCraft()
    {
        switch (Crafting.CurState)
        {
            case Crafting.State.WaitFinish:
            case Crafting.State.QuickCraft:
            case Crafting.State.WaitAction:
            case Crafting.State.InProgress:
                return TaskResult.Retry;
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
        }

        return TaskResult.Retry;
    }

    public static TaskResult TaskClassChange(Job job)
    {
        if (job == CharacterInfo.JobID)
            return TaskResult.Done;

        var gearsets = RaptureGearsetModule.Instance();
        foreach (ref var gs in gearsets->EntriesSpan)
        {
            if (!RaptureGearsetModule.Instance()->IsValidGearset(gs.ID)) continue;
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
            DuoLog.Error($"Failed to find item {LuminaSheets.ItemSheet[itemId].Name} (ID: {itemId}) in inventory");
            return TaskResult.Abort;
        }

        var agentId = pos.Value.inv is InventoryType.ArmoryMainHand or InventoryType.ArmoryHands ? AgentId.ArmouryBoard : AgentId.Inventory;
        var addonId = AgentModule.Instance()->GetAgentByInternalId(agentId)->GetAddonID();
        var ctx = AgentInventoryContext.Instance();
        ctx->OpenForItemSlot(pos.Value.inv, pos.Value.slot, addonId);

        var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu");
        if (contextMenu != null)
        {
            for (int i = 0; i < contextMenu->AtkValuesCount; i++)
            {
                var firstEntryIsEquip = ctx->EventIdSpan[i] == 25; // i'th entry will fire eventid 7+i; eventid 25 is 'equip'
                if (firstEntryIsEquip)
                {
                    Svc.Log.Debug($"Equipping item #{itemId} from {pos.Value.inv} @ {pos.Value.slot}, index {i}");
                    Callback.Fire(contextMenu, true, 0, i - 7, 0, 0, 0); // p2=-1 is close, p2=0 is exec first command
                }
            }
            Callback.Fire(contextMenu, true, 0, -1, 0, 0, 0);
            equipAttemptLoops++;

            if (equipAttemptLoops >= 5)
            {
                DuoLog.Error($"Equip option not found after 5 attempts. Aborting.");
                return TaskResult.Abort;
            }
        }
        return TaskResult.Retry;
    }

    public static TaskResult TaskUseConsumables(RecipeConfig? config, CraftType type)
    {
        if (ActionManagerEx.AnimationLock > 0)
            return TaskResult.Retry; // waiting for animation lock to end

        if ((!P.Config.UseConsumablesQuickSynth && type == CraftType.Quick) ||
            (!P.Config.UseConsumablesTrial && type == CraftType.Trial))
            return TaskResult.Done;

        if (Occupied())
            return TaskResult.Retry;

        if (!ConsumableChecker.IsSquadronManualled(config))
        {
            if (InventoryManager.Instance()->GetInventoryItemCount(config.RequiredSquadronManual) == 0) return TaskResult.Abort;
            if (ActionManagerEx.CanUseAction(ActionType.Item, config.RequiredSquadronManual))
            {
                Svc.Log.Debug($"Using squadron manual: {config.RequiredSquadronManual}");
                ActionManagerEx.UseItem(config.RequiredSquadronManual);
                return TaskResult.Retry;
            }
            else
            {
                return TaskResult.Retry;
            }
        }

        if (!ConsumableChecker.IsManualled(config))
        {
            if (InventoryManager.Instance()->GetInventoryItemCount(config.RequiredManual) == 0) return TaskResult.Abort;
            if (ActionManagerEx.CanUseAction(ActionType.Item, config.RequiredManual))
            {
                Svc.Log.Debug($"Using manual: {config.RequiredManual}");
                ActionManagerEx.UseItem(config.RequiredManual);
                return TaskResult.Retry;
            }
            else
            {
                return TaskResult.Retry;
            }
        }

        if (!ConsumableChecker.IsFooded(config))
        {
            var foodId = config.RequiredFood + (config.RequiredFoodHQ ? 1000000u : 0);
            if (InventoryManager.Instance()->GetInventoryItemCount(config.RequiredFood, config.RequiredFoodHQ) == 0) return TaskResult.Abort;
            if (ActionManagerEx.CanUseAction(ActionType.Item, foodId))
            {
                Svc.Log.Debug($"Using food: {foodId}");
                ActionManagerEx.UseItem(foodId);
                return TaskResult.Retry;
            }
            else
            {
                return TaskResult.Retry;
            }
        }

        if (!ConsumableChecker.IsPotted(config))
        {
            var potId = config.RequiredPotion + (config.RequiredPotionHQ ? 1000000u : 0);
            if (InventoryManager.Instance()->GetInventoryItemCount(config.RequiredPotion, config.RequiredPotionHQ) == 0) return TaskResult.Abort;
            if (ActionManagerEx.CanUseAction(ActionType.Item, potId))
            {
                Svc.Log.Debug($"Using pot: {potId}");
                ActionManagerEx.UseItem(potId);
                return TaskResult.Retry;
            }
            else
            {
                return TaskResult.Retry;
            }
        }

        return TaskResult.Done;
    }

    public static TaskResult TaskSelectRecipe(Recipe recipe)
    {
        var re = Operations.GetSelectedRecipeEntry();
        if (re != null && re->RecipeId == recipe.RowId)
            return TaskResult.Done;

        Svc.Log.Debug($"Opening recipe {recipe.RowId}");
        AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RowId);
        return TaskResult.Retry;
    }

    public static TaskResult TaskStartCraft(CraftType type)
    {
        var addon = (AddonRecipeNote*)Svc.GameGui.GetAddonByName("RecipeNote");
        if (addon == null)
            return TaskResult.Retry;

        Svc.Log.Debug($"Starting {type} craft");
        Callback.Fire(&addon->AtkUnitBase, true, 8 + (int)type);
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

    public static bool Occupied()
    {
        return Svc.Condition[ConditionFlag.Occupied]
           || Svc.Condition[ConditionFlag.Occupied30]
           || Svc.Condition[ConditionFlag.Occupied33]
           || Svc.Condition[ConditionFlag.Occupied38]
           || Svc.Condition[ConditionFlag.Occupied39]
           || Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
           || Svc.Condition[ConditionFlag.OccupiedInEvent]
           || Svc.Condition[ConditionFlag.OccupiedInQuestEvent]
           || Svc.Condition[ConditionFlag.OccupiedSummoningBell];
    }

    private static void ClickNormalSynthesisButtonDetour(void* a1, void* a2)
    {
        var re = Operations.GetSelectedRecipeEntry();
        var recipe = re != null ? Svc.Data.GetExcelSheet<Recipe>()?.GetRow(re->RecipeId) : null;
        if (recipe != null)
            StartCrafting(recipe, CraftType.Normal);
    }

    private static void ClickQuickSynthesisButtonDetour(void* a1, void* a2)
    {
        var re = Operations.GetSelectedRecipeEntry();
        var recipe = re != null ? Svc.Data.GetExcelSheet<Recipe>()?.GetRow(re->RecipeId) : null;
        if (recipe != null)
            StartCrafting(recipe, CraftType.Quick);
    }
    private static void ClickTrialSynthesisButtonDetour(void* a1, void* a2)
    {
        var re = Operations.GetSelectedRecipeEntry();
        var recipe = re != null ? Svc.Data.GetExcelSheet<Recipe>()?.GetRow(re->RecipeId) : null;
        if (recipe != null)
            StartCrafting(recipe, CraftType.Trial);
    }
}
