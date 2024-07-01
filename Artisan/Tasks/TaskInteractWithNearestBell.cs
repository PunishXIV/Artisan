using ECommons.DalamudServices;
using ECommons.Reflection;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Numerics;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;
using static ECommons.GenericHelpers;
using Artisan.IPC;
using System.Collections.Generic;
using System.Linq;
using System;
using ECommons.Automation.LegacyTaskManager;

namespace Artisan.Tasks;

internal unsafe static class TaskInteractWithNearestBell
{
    internal static void EnqueueBell(this TaskManager TM)
    {
        TM.Enqueue(YesAlready.Lock);
        TM.Enqueue(PlayerWorldHandlers.SelectNearestBell);
        TM.Enqueue(PlayerWorldHandlers.InteractWithTargetedBell);
    }
}

internal static class YesAlready
{
    internal static Version Version => Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.IsLoaded && x.InternalName == "YesAlready")?.Version ?? new();
    internal static readonly Version NewVersion = new("1.4.0.0");
    internal static bool Reenable = false;
    internal static HashSet<string>? Data = null;

    internal static void GetData()
    {
        if (Data != null) return;
        if (Svc.PluginInterface.TryGetData<HashSet<string>>("YesAlready.StopRequests", out var data))
        {
            Data = data;
        }
    }

    internal static void Lock()
    {
        if (Version != null)
        {
            if (Version < NewVersion)
            {
                if (DalamudReflector.TryGetDalamudPlugin("Yes Already", out var pl, false, true))
                {
                    Svc.Log.Information("Disabling Yes Already (old)");
                    pl.GetStaticFoP("YesAlready.Service", "Configuration").SetFoP("Enabled", false);
                    Reenable = true;
                }
            }
            else
            {
                GetData();
                if (Data != null)
                {
                    Svc.Log.Information("Disabling Yes Already (new)");
                    Data.Add(Svc.PluginInterface.InternalName);
                    Reenable = true;
                }
            }
        }
    }

    internal static void Unlock()
    {
        if (Reenable && Version != null)
        {
            if (Version < NewVersion)
            {
                if (DalamudReflector.TryGetDalamudPlugin("Yes Already", out var pl, false, true))
                {
                    Svc.Log.Information("Enabling Yes Already");
                    pl.GetStaticFoP("YesAlready.Service", "Configuration").SetFoP("Enabled", true);
                    Reenable = false;
                }
            }
            else
            {
                GetData();
                if (Data != null)
                {
                    Svc.Log.Information("Enabling Yes Already (new)");
                    Data.Remove(Svc.PluginInterface.InternalName);
                    Reenable = false;
                }
            }
        }
    }

    internal static bool IsEnabled()
    {
        if (Version != null)
        {
            if (Version < NewVersion)
            {
                if (DalamudReflector.TryGetDalamudPlugin("Yes Already", out var pl, false, true))
                {
                    return pl.GetStaticFoP("YesAlready.Service", "Configuration").GetFoP<bool>("Enabled");
                }
            }
            else
            {
                GetData();
                if (Data != null)
                {
                    return !Data.Contains(Svc.PluginInterface.InternalName);
                }
            }
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
                    Svc.Log.Debug($"Set target to {x}");
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
                    Svc.Log.Debug($"Interacted with {x}");
                    return true;
                }
            }
        }
        return false;
    }
}