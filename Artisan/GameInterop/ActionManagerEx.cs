using Artisan.RawInformation.Character;
using Dalamud.Hooking;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;

namespace Artisan.GameInterop;

// wrapper around in-game action manager that provides nicer api + use-action hooking
public static unsafe class ActionManagerEx
{
    // note: this event is invoked when action is about to be used, if we can't find a reason why it would fail - it's not guaranteed to actually succeed
    private static Action<Skills>? _actionUsed;
    public static event Action<Skills> ActionUsed
    {
        add
        {
            _actionUsed += value;
            UpdateHookActivation();
        }
        remove
        {
            _actionUsed += value;
            UpdateHookActivation();
        }
    }

    private static bool _blockAction;
    public static bool BlockAction
    {
        get => _blockAction;
        set
        {
            _blockAction = value;
            UpdateHookActivation();
        }
    }

    // hook is automatically enabled when useful
    private delegate byte UseActionDelegate(ActionManager* actionManager, ActionType actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget);
    private static Hook<UseActionDelegate> _useActionHook;

    static ActionManagerEx()
    {
        _useActionHook = Svc.Hook.HookFromAddress<UseActionDelegate>((nint)ActionManager.Addresses.UseAction.Value, UseActionDetour);
    }

    public static void Dispose()
    {
        _useActionHook.Dispose();
    }

    public static bool CanUseAction(ActionType actionType, uint actionID) => ActionManager.Instance()->GetActionStatus(actionType, actionID) == 0;

    public static bool CanUseSkill(Skills skill)
    {
        var actionId = skill.ActionId(CharacterInfo.JobID);
        return actionId != 0 ? CanUseAction(actionId >= 100000 ? ActionType.CraftAction : ActionType.Action, actionId) : false;
    }

    // if true is returned, the action execution is unblocked
    public static bool UseSkill(Skills skill)
    {
        var actionId = skill.ActionId(CharacterInfo.JobID);
        if (actionId == 0)
            return false;
        ActionType actionType = actionId >= 100000 ? ActionType.CraftAction : ActionType.Action;
        if (!CanUseAction(actionType, actionId))
            return false;
        BlockAction = false;
        ActionManager.Instance()->UseAction(actionType, actionId);
        return true;
    }

    public static bool UseItem(uint itemId) => ActionManager.Instance()->UseAction(ActionType.Item, itemId, a4: 65535);
    public static bool UseRepair() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);
    public static bool UseMateriaExtraction() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 14);

    private static void UpdateHookActivation()
    {
        if (_blockAction || _actionUsed != null)
            _useActionHook.Enable();
        else
            _useActionHook.Disable();
    }

    private static byte UseActionDetour(ActionManager* actionManager, ActionType actionType, uint actionID, long targetObjectID, uint param, uint useType, int pvp, bool* isGroundTarget)
    {
        if (BlockAction)
            return 0;

        if (CanUseAction(actionType, actionID))
        {
            var skill = SkillActionMap.ActionToSkill(actionID);
            Svc.Log.Debug($"Used action: {skill}");
            _actionUsed?.Invoke(skill);
        }

        return _useActionHook.Original(actionManager, actionType, actionID, targetObjectID, param, useType, pvp, isGroundTarget);
    }
}
