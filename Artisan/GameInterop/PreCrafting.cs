using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.GameInterop.CSExt;
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
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using static ECommons.GenericHelpers;

namespace Artisan.GameInterop;

// manages 'outer loop' of crafting (equipping correct items, using consumables, etc, and finally initiating crafting)
public unsafe static class PreCrafting
{
    public enum CraftType { Normal, Quick, Trial }

    public static int equipAttemptLoops = 0;
    public static int equipGearsetLoops = 0;
    public static int timeWasteLoops = 0;
    private static long NextTaskAt = 0;

    private delegate void ClickSynthesisButton(void* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData);
    private static Hook<ClickSynthesisButton> _clickButton;

    private delegate void* FireCallbackDelegate(AtkUnitBase* atkUnitBase, int valueCount, AtkValue* atkValues, byte updateVisibility);
    private static Hook<FireCallbackDelegate> _gearsetCallback;

    delegate nint AddonWKSRecipeNote_ReceiveEventDelegate(nint a1, ushort a2, uint a3, nint a4, nint a5);
    private static Hook<AddonWKSRecipeNote_ReceiveEventDelegate> _cosmicCallback;

    public enum TaskResult { Done, Retry, Abort }
    public static List<(Func<TaskResult> task, TimeSpan retryDelay)> Tasks = new();
    private static DateTime _nextRetry;

    static PreCrafting()
    {
        _clickButton = Svc.Hook.HookFromSignature<ClickSynthesisButton>("40 55 53 56 57 41 56 48 8D 6C 24 D1 48 81 EC C0 00 00 00", ClickSynthButtons);
        _clickButton?.Enable();

        _gearsetCallback = Svc.Hook.HookFromSignature<FireCallbackDelegate>("E8 ?? ?? ?? ?? 0F B6 E8 8B 44 24 20", CallbackDetour);

        _cosmicCallback = Svc.Hook.HookFromSignature<AddonWKSRecipeNote_ReceiveEventDelegate>("4C 8B DC 49 89 6B 20 41 56 48 83 EC 60", ClickCosmicButton);
        _cosmicCallback?.Enable();
    }

    private static nint ClickCosmicButton(nint a1, ushort a2, uint a3, nint a4, nint a5)
    {
        try
        {
            if (a2 == 25 && a3 == 0)
            {
                StartCraftingFromSynth(14);
                return 0;
            }
        }
        catch( Exception ex)
        {
            ex.Log();
        }
        return _cosmicCallback.Original(a1, a2, a3, a4, a5);
    }

    private static void* CallbackDetour(AtkUnitBase* atkUnitBase, int valueCount, AtkValue* atkValues, byte updateVisibility)
    {
        var name = atkUnitBase->NameString.TrimEnd();
        if (name.Length >= 11 && name.Substring(0, 11) == "SelectYesno")
        {
            var result = atkValues[0];
            if (result.Int == 1)
            {
                Svc.Log.Debug($"Select no, clearing tasks");
                Endurance.ToggleEndurance(false);
                if (CraftingListUI.Processing)
                {
                    CraftingListFunctions.Paused = true;
                }
                Tasks.Clear();
            }

            _gearsetCallback.Disable();

        }
        return _gearsetCallback.Original(atkUnitBase, valueCount, atkValues, updateVisibility);
    }

    public static void Dispose()
    {
        _clickButton?.Dispose();
        _gearsetCallback?.Dispose();
        _cosmicCallback?.Dispose();
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
            Svc.Log.Debug($"Starting {type} crafting: {recipe.RowId} '{recipe.ItemResult.Value.Name.ToDalamudString()}'");

            var requiredClass = Job.CRP + recipe.CraftType.RowId;
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipe.RowId) ?? new();

            bool hasIngredients = GetNumberCraftable(recipe) > 0;
            bool needClassChange = requiredClass != CharacterInfo.JobID;
            bool needEquipItem = recipe.ItemRequired.RowId > 0 && (needClassChange || !IsItemEquipped(recipe.ItemRequired.RowId));
            bool needConsumables = NeedsConsumablesCheck(type, config);
            bool hasConsumables = HasConsumablesCheck(config);

            // handle errors when we're forbidden from rectifying them automatically
            if (P.Config.DontEquipItems && needClassChange)
            {
                DuoLog.Error($"Can't craft {recipe.ItemResult.Value.Name.ToDalamudString()}: wrong class, {requiredClass} needed");
                return;
            }
            if (P.Config.DontEquipItems && needEquipItem)
            {
                DuoLog.Error($"Can't craft {recipe.ItemResult.Value.Name.ToDalamudString()}: required item {recipe.ItemRequired.Value.Name} not equipped");
                return;
            }
            if (P.Config.AbortIfNoFoodPot && needConsumables && !hasConsumables)
            {
                MissingConsumablesMessage(recipe, config);
                return;
            }

            bool needExitCraft = Crafting.CurState == Crafting.State.IdleBetween && (needClassChange || needEquipItem || needConsumables);

            // TODO: pre-setup solver for incoming craft
            Tasks.Clear();
            _nextRetry = default;
            if (needExitCraft)
                Tasks.Add((TaskExitCraft, default));
            if (needClassChange)
            {
                equipGearsetLoops = 0;
                Tasks.Add((() => TaskClassChange(requiredClass), TimeSpan.FromMilliseconds(200))); // TODO: avoid delay and just wait until operation is done
            }

            if (!hasIngredients && type != CraftType.Trial)
            {
                List<string> missingIngredients = MissingIngredients(recipe);

                DuoLog.Error($"Not all ingredients for {recipe.ItemResult.Value.Name.ToDalamudString()} found.\r\nMissing: {string.Join(", ", missingIngredients)}");
                return;
            }

            if (needEquipItem)
            {
                equipAttemptLoops = 0;
                Tasks.Add((() => TaskEquipItem(recipe.ItemRequired.RowId), default));
            }

            bool needFood = config != default && ConsumableChecker.HasItem(config.RequiredFood, config.RequiredFoodHQ) && !ConsumableChecker.IsFooded(config);
            bool needPot = config != default && ConsumableChecker.HasItem(config.RequiredPotion, config.RequiredPotionHQ) && !ConsumableChecker.IsPotted(config);
            bool needManual = config != default && ConsumableChecker.HasItem(config.RequiredManual, false) && !ConsumableChecker.IsManualled(config);
            bool needSquadronManual = config != default && ConsumableChecker.HasItem(config.RequiredSquadronManual, false) && !ConsumableChecker.IsSquadronManualled(config);

            if (needFood || needPot || needManual || needSquadronManual)
                Tasks.Add((() => TaskUseConsumables(config, type), default));
            Tasks.Add((() => TaskSelectRecipe(recipe), TimeSpan.FromMilliseconds(500)));
            timeWasteLoops = 1;
            Tasks.Add((() => TimeWasteLoop(), TimeSpan.FromMilliseconds(10))); //This is needed for controller players, else if they're near an NPC it will target them and exit the craft as the button is interpreted as target and not confirm.
            Tasks.Add((() => TaskStartCraft(type), default));

            Update();
        }
        catch (Exception ex)
        {
            ex.Log();
        }
    }

    internal static void MissingConsumablesMessage(Recipe recipe, RecipeConfig? config)
    {
        List<string> missingConsumables = MissingConsumables(config);

        DuoLog.Error($"Can't craft {recipe.ItemResult.Value.Name.ToDalamudString()}: required consumables not up and missing {string.Join(", ", missingConsumables)}");
    }

    internal static bool NeedsConsumablesCheck(CraftType type, RecipeConfig? config)
    {
        // TODO: repair & extract materia
        return (type == CraftType.Normal || (type == CraftType.Trial && P.Config.UseConsumablesTrial) || (type == CraftType.Quick && P.Config.UseConsumablesQuickSynth)) && (!ConsumableChecker.IsFooded(config) || !ConsumableChecker.IsPotted(config) || !ConsumableChecker.IsManualled(config) || !ConsumableChecker.IsSquadronManualled(config));
    }

    internal static bool HasConsumablesCheck(RecipeConfig? config)
    {
        return config != default ?
            (ConsumableChecker.HasItem(config.RequiredFood, config.RequiredFoodHQ) || ConsumableChecker.IsFooded(config)) &&
            (ConsumableChecker.HasItem(config.RequiredPotion, config.RequiredPotionHQ) || ConsumableChecker.IsPotted(config)) &&
            (ConsumableChecker.HasItem(config.RequiredManual, false) || ConsumableChecker.IsManualled(config)) &&
            (ConsumableChecker.HasItem(config.RequiredSquadronManual, false) || ConsumableChecker.IsSquadronManualled(config)) : true;
    }

    public static List<string> MissingConsumables(RecipeConfig? config)
    {
        List<string> missingConsumables = new List<string>();
        if (!ConsumableChecker.HasItem(config.RequiredFood, config.RequiredFoodHQ) && !ConsumableChecker.IsFooded(config))
            missingConsumables.Add(config.FoodName);

        if (!ConsumableChecker.HasItem(config.RequiredPotion, config.RequiredPotionHQ) && !ConsumableChecker.IsPotted(config))
            missingConsumables.Add(config.PotionName);

        if (!ConsumableChecker.HasItem(config.RequiredManual, false) && !ConsumableChecker.IsManualled(config))
            missingConsumables.Add(config.ManualName);

        if (!ConsumableChecker.HasItem(config.RequiredSquadronManual, false) && !ConsumableChecker.IsSquadronManualled(config))
            missingConsumables.Add(config.SquadronManualName);
        return missingConsumables;
    }

    public static List<string> MissingIngredients(Recipe recipe)
    {
        List<string> missingIngredients = new();
        foreach (var ing in recipe.Ingredients())
        {
            if (ing.Amount > 0)
            {
                if (CraftingListUI.NumberOfIngredient(ing.Item.RowId) < ing.Amount)
                {
                    missingIngredients.Add(ing.Item.RowId.NameOfItem());
                }
            }
        }

        return missingIngredients;
    }

    public static TaskResult TimeWasteLoop()
    {
        if (timeWasteLoops > 0)
        {
            timeWasteLoops--;
            return TaskResult.Retry;
        }

        return TaskResult.Done;
    }

    public static int GetNumberCraftable(Recipe recipe)
    {
        if (TryGetAddonByName<AddonRecipeNote>("RecipeNote", out var addon) && addon->SelectedRecipeQuantityCraftableFromMaterialsInInventory != null)
        {
            if (int.TryParse(addon->SelectedRecipeQuantityCraftableFromMaterialsInInventory->NodeText.ToString(), out int output))
                return output;
        }
        if (TryGetAddonByName<AtkUnitBase>("WKSRecipeNotebook", out var cosmic) && cosmic->UldManager.NodeList[24] != null)
        {
            if (int.TryParse(cosmic->UldManager.NodeList[24]->GetAsAtkTextNode()->NodeText.ToString(), out int output))
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
                var addon2 = (AtkUnitBase*)Svc.GameGui.GetAddonByName("WKSRecipeNotebook");
                if (addon2 != null && addon2->IsVisible)
                {
                    Svc.Log.Debug("Closing recipe menu to exit crafting state");
                    Callback.Fire(addon2, true, -1);
                }
                return TaskResult.Retry;
        }

        return TaskResult.Retry;
    }

    public static TaskResult TaskClassChange(Job job)
    {
        if (job == CharacterInfo.JobID)
            return TaskResult.Done;

        if (equipGearsetLoops >= 5)
        {
            DuoLog.Error("Unable to switch gearsets.");
            return TaskResult.Abort;
        }

        var gearsets = RaptureGearsetModule.Instance();
        foreach (ref var gs in gearsets->Entries)
        {
            if (!RaptureGearsetModule.Instance()->IsValidGearset(gs.Id)) continue;
            if ((Job)gs.ClassJob == job)
            {
                if (gs.Flags.HasFlag(RaptureGearsetModule.GearsetFlag.MainHandMissing))
                {
                    if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var selectyesno))
                    {
                        if (selectyesno->AtkUnitBase.IsVisible)
                            return TaskResult.Retry;
                    }
                    else
                    {
                        equipGearsetLoops++;
                        _gearsetCallback?.Enable();
                        var r = gearsets->EquipGearset(gs.Id);
                        return r < 0 ? TaskResult.Abort : TaskResult.Retry;
                    }
                }

                var result = gearsets->EquipGearset(gs.Id);
                equipGearsetLoops++;
                Svc.Log.Debug($"Tried to equip gearset {gs.Id} for {job}, result={result}, flags={gs.Flags}");
                return result < 0 ? TaskResult.Abort : TaskResult.Retry;
            }
        }

        DuoLog.Error($"Failed to find gearset for {job}");
        return TaskResult.Abort;
    }

    public static TaskResult TaskEquipItem(uint ItemId)
    {
        if (IsItemEquipped(ItemId))
            return TaskResult.Done;

        var pos = FindItemInInventory(ItemId, [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4, InventoryType.ArmoryMainHand, InventoryType.ArmoryHands]);
        if (pos == null)
        {
            DuoLog.Error($"Failed to find item {LuminaSheets.ItemSheet[ItemId].Name} (ID: {ItemId}) in inventory");
            Endurance.ToggleEndurance(false);
            if (CraftingListUI.Processing)
                CraftingListFunctions.Paused = true;

            return TaskResult.Abort;
        }

        var agentId = pos.Value.inv is InventoryType.ArmoryMainHand or InventoryType.ArmoryHands ? AgentId.ArmouryBoard : AgentId.Inventory;
        var addonId = AgentModule.Instance()->GetAgentByInternalId(agentId)->GetAddonId();
        var ctx = AgentInventoryContext.Instance();
        ctx->OpenForItemSlot(pos.Value.inv, pos.Value.slot, addonId);

        var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu");
        if (contextMenu != null)
        {
            for (int i = 0; i < contextMenu->AtkValuesCount; i++)
            {
                var firstEntryIsEquip = ctx->EventIds[i] == 25; // i'th entry will fire eventid 7+i; eventid 25 is 'equip'
                if (firstEntryIsEquip)
                {
                    Svc.Log.Debug($"Equipping item #{ItemId} from {pos.Value.inv} @ {pos.Value.slot}, index {i}");
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

        if (!ConsumableChecker.IsSquadronManualled(config) && InventoryManager.Instance()->GetInventoryItemCount(config.RequiredSquadronManual) != 0)
        {
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

        if (!ConsumableChecker.IsManualled(config) && InventoryManager.Instance()->GetInventoryItemCount(config.RequiredManual) != 0)
        {
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

        var foodId = config.RequiredFood + (config.RequiredFoodHQ ? 1000000u : 0);
        if (!ConsumableChecker.IsFooded(config) && InventoryManager.Instance()->GetInventoryItemCount(config.RequiredFood, config.RequiredFoodHQ) != 0)
        {
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

        var potId = config.RequiredPotion + (config.RequiredPotionHQ ? 1000000u : 0);
        if (!ConsumableChecker.IsPotted(config) && InventoryManager.Instance()->GetInventoryItemCount(config.RequiredPotion, config.RequiredPotionHQ) != 0)
        {
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
        if ((re != null && re->RecipeId == recipe.RowId) || (Crafting.CurState is not Crafting.State.IdleBetween and not Crafting.State.IdleNormal))
            return TaskResult.Done;

        if (recipe.Number == 0)
        {
            var addon = Crafting.GetCosmicAddon();

            if (addon == null)
            {
                AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RowId);
                return TaskResult.Retry;
            }

            var rd = RecipeNoteRecipeData.Ptr();
            if (rd == null)
                return TaskResult.Retry;

            for (int i = 0; i < rd->RecipesCount; i++)
            {
                try
                {
                    Callback.Fire(addon, false, 0, i);
                    re = Operations.GetSelectedRecipeEntry();
                    if (re != null && re->RecipeId == recipe.RowId)
                        return TaskResult.Done;
                }
                catch (Exception ex)
                {
                    return TaskResult.Done;
                }
            }
        }
        else
        {
            AgentRecipeNote.Instance()->OpenRecipeByRecipeId(recipe.RowId);
        }
        return TaskResult.Retry;
    }

    public static TaskResult TaskStartCraft(CraftType type)
    {
        if (TryGetAddonByName<AtkUnitBase>("WKSRecipeNotebook", out var cosmicAddon))
        {
            if (cosmicAddon == null)
                return TaskResult.Retry;

            Svc.Log.Debug($"Starting actual cosmic craft");
            Callback.Fire(cosmicAddon, true, 6);

            return TaskResult.Done;

        }

        var addon = (AddonRecipeNote*)Svc.GameGui.GetAddonByName("RecipeNote");
        if (addon == null)
            return TaskResult.Retry;

        Svc.Log.Debug($"Starting {type} craft");
        Callback.Fire(&addon->AtkUnitBase, true, 8 + (int)type);
        return TaskResult.Done;
    }

    public static bool IsItemEquipped(uint ItemId) => InventoryManager.Instance()->GetItemCountInContainer(ItemId, InventoryType.EquippedItems) > 0;

    private static (InventoryType inv, int slot)? FindItemInInventory(uint ItemId, IEnumerable<InventoryType> inventories)
    {
        foreach (var inv in inventories)
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for (int i = 0; i < cont->Size; ++i)
            {
                if (cont->GetInventorySlot(i)->ItemId == ItemId)
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

    private static void ClickSynthButtons(void* thisPtr, AtkEventType eventType, int eventParam, AtkEvent* atkEvent, AtkEventData* atkEventData)
    {
        if (eventType == AtkEventType.ButtonClick && eventParam is 14 or 15 or 16)
        {
            StartCraftingFromSynth(eventParam);
        }
        else
        {
            _clickButton?.OriginalDisposeSafe(thisPtr, eventType, eventParam, atkEvent, atkEventData);
        }

    }

    private static void StartCraftingFromSynth(int eventParam)
    {
        var re = Operations.GetSelectedRecipeEntry();
        var recipe = re != null ? Svc.Data.GetExcelSheet<Recipe>()?.GetRow(re->RecipeId) : null;
        if (recipe != null)
            StartCrafting(recipe.Value, eventParam is 14 ? CraftType.Normal : eventParam is 15 ? CraftType.Quick : CraftType.Trial);
        else
            DuoLog.Error($"Somehow recipe is null. Please report this on the Discord.");
    }
}
