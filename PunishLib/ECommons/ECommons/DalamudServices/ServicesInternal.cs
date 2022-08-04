using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.JobGauge;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Party;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Game.Gui.PartyFinder;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Libc;
using Dalamud.Game.Network;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.DalamudServices
{
    internal class SDalamudPluginInterface
    {
        [PluginService] internal static DalamudPluginInterface PluginInterface { get; private set; }
    }

    internal class SBuddyList
    {
        [PluginService] internal static BuddyList Buddies { get; private set; }
    }

    internal class SChatGui
    {
        [PluginService] internal static ChatGui Chat { get; private set; }
    }

    internal class SChatHandlers
    {
        [PluginService] internal static ChatHandlers ChatHandlers { get; private set; }
    }

    internal class SClientState
    {
        [PluginService] internal static ClientState ClientState { get; private set; }
    }

    internal class SCommandManager
    {
        [PluginService] internal static CommandManager Commands { get; private set; }
    }

    internal class SCondition
    {
        [PluginService] internal static Condition Condition { get; private set; }
    }

    internal class SDataManager
    {
        [PluginService] internal static DataManager Data { get; private set; }
    }

    internal class SFateTable
    {
        [PluginService] internal static FateTable Fates { get; private set; }
    }

    internal class SFlyTextGui
    {
        [PluginService] internal static FlyTextGui FlyText { get; private set; }
    }

    internal class SFramework
    {
        [PluginService] internal static Framework Framework { get; private set; }
    }

    internal class SGameGui
    {
        [PluginService] internal static GameGui GameGui { get; private set; }
    }

    internal class SGameNetwork
    {
        [PluginService] internal static GameNetwork GameNetwork { get; private set; }
    }

    internal class SJobGauges
    {
        [PluginService] internal static JobGauges Gauges { get; private set; }
    }

    internal class SKeyState
    {
        [PluginService] internal static KeyState KeyState { get; private set; }
    }

    internal class SLibcFunction
    {
        [PluginService] internal static LibcFunction LibcFunction { get; private set; }
    }

    internal class SObjectTable
    {
        [PluginService] internal static ObjectTable Objects { get; private set; }
    }

    internal class SPartyFinderGui
    {
        [PluginService] internal static PartyFinderGui PfGui { get; private set; }
    }

    internal class SPartyList
    {
        [PluginService] internal static PartyList Party { get; private set; }
    }

    internal class SSigScanner
    {
        [PluginService] internal static SigScanner SigScanner { get; private set; }
    }

    internal class STargetManager
    {
        [PluginService] internal static TargetManager Targets { get; private set; }
    }

    internal class SToastGui
    {
        [PluginService] internal static ToastGui Toasts { get; private set; }
    }
}
