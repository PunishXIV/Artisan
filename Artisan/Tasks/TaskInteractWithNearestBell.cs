using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Logging;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Reflection;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Numerics;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using static ECommons.GenericHelpers;
using Artisan.IPC;

namespace Artisan.Tasks;

internal unsafe static class TaskInteractWithNearestBell
{
    internal static void EnqueueBell(this TaskManager TM)
    {
        TM.Enqueue(YesAlready.DisableIfNeeded);
        TM.Enqueue(PlayerWorldHandlers.SelectNearestBell);
        TM.Enqueue(PlayerWorldHandlers.InteractWithTargetedBell);
    }
}

internal static class YesAlready
{
    internal static bool Reenable = false;
    internal static void DisableIfNeeded()
    {
        if (DalamudReflector.TryGetDalamudPlugin("Yes Already", out var pl, false, true))
        {
            PluginLog.Information("Disabling Yes Already");
            pl.GetStaticFoP("YesAlready.Service", "Configuration").SetFoP("Enabled", false);
            Reenable = true;
        }
    }

    internal static void EnableIfNeeded()
    {
        if (Reenable && DalamudReflector.TryGetDalamudPlugin("Yes Already", out var pl, false, true))
        {
            PluginLog.Information("Enabling Yes Already");
            pl.GetStaticFoP("YesAlready.Service", "Configuration").SetFoP("Enabled", true);
            Reenable = false;
        }
    }

    internal static bool IsEnabled()
    {
        if (DalamudReflector.TryGetDalamudPlugin("Yes Already", out var pl, false, true))
        {
            return pl.GetStaticFoP("YesAlready.Service", "Configuration").GetFoP<bool>("Enabled");
        }
        return false;
    }

    internal static bool? WaitForYesAlreadyDisabledTask()
    {
        return !IsEnabled();
    }
}

internal unsafe static class PlayerWorldHandlers
{
    internal static bool? SelectNearestBell()
    {
        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell]) return true;
        if (!IsOccupied())
        {
            var x = RetainerInfo.GetReachableRetainerBell();
            if (x != null)
            {
                if (RetainerInfo.GenericThrottle)
                {
                    Svc.Targets.Target = x;
                    PluginLog.Debug($"Set target to {x}");
                    return true;
                }
            }
        }
        return false;
    }

    internal static bool? InteractWithTargetedBell()
    {
        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell]) return true;
        var x = Svc.Targets.Target;
        if (x != null && (x.ObjectKind == ObjectKind.Housing || x.ObjectKind == ObjectKind.EventObj) && x.Name.ToString().EqualsIgnoreCaseAny(RetainerInfo.BellName, "リテイナーベル") && !IsOccupied())
        {
            if (Vector3.Distance(x.Position, Svc.ClientState.LocalPlayer.Position) < RetainerInfo.GetValidInteractionDistance(x) && x.IsTargetable())
            {
                if (RetainerInfo.GenericThrottle && EzThrottler.Throttle("InteractWithBell", 5000))
                {
                    TargetSystem.Instance()->InteractWithObject((GameObject*)x.Address, false);
                    PluginLog.Debug($"Interacted with {x}");
                    return true;
                }
            }
        }
        return false;
    }
}