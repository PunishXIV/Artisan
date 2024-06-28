using Artisan.RawInformation.Character;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Artisan.GameInterop;

// wrapper around in-game action manager that provides nicer api + use-action hooking
public static unsafe class ActionManagerEx
{
    public static float AnimationLock => ((float*)ActionManager.Instance())[2];

    public static bool CanUseAction(ActionType actionType, uint actionID) => ActionManager.Instance()->GetActionStatus(actionType, actionID) == 0;

    public static bool CanUseSkill(Skills skill)
    {
        var actionId = skill.ActionId(CharacterInfo.JobID);
        return actionId != 0 ? CanUseAction(actionId >= 100000 ? ActionType.CraftAction : ActionType.Action, actionId) : false;
    }

    public static bool UseSkill(Skills skill)
    {
        var actionId = skill.ActionId(CharacterInfo.JobID);
        if (actionId == 0)
            return false;
        ActionType actionType = actionId >= 100000 ? ActionType.CraftAction : ActionType.Action;
        if (!CanUseAction(actionType, actionId))
            return false;
        Svc.Log.Debug($"Using skill {skill}: {actionType} {actionId}");
        ActionManager.Instance()->UseAction(actionType, actionId);
        //Reset AFK timer
        var module = UIModule.Instance()->GetInputTimerModule();
        module->AfkTimer = 0;
        module->ContentInputTimer = 0;
        module->InputTimer = 0;
        module->Unk1C = 0;
        return true;
    }

    public static bool UseItem(uint ItemId) => ActionManager.Instance()->UseAction(ActionType.Item, ItemId, extraParam: 65535);
    public static bool UseRepair() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);
    public static bool UseMateriaExtraction() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 14);
}
