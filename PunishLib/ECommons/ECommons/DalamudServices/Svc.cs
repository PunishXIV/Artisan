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
using Dalamud.Plugin;
using ECommons.Logging;
using System;

namespace ECommons.DalamudServices
{
    //If one of services is not ready, whole service class will be unavailable.
    //This is inconvenient. Let's bypass it.
    public class Svc
    {
        public static DalamudPluginInterface PluginInterface { get; private set; }
        public static BuddyList Buddies { get; private set; }
        public static ChatGui Chat { get; private set; }
        public static ChatHandlers ChatHandlers { get; private set; }
        public static ClientState ClientState { get; private set; }
        public static CommandManager Commands { get; private set; }
        public static Condition Condition { get; private set; }
        public static DataManager Data { get; private set; }
        public static FateTable Fates { get; private set; }
        public static FlyTextGui FlyText { get; private set; }
        public static Framework Framework { get; private set; }
        public static GameGui GameGui { get; private set; }
        public static GameNetwork GameNetwork { get; private set; }
        public static JobGauges Gauges { get; private set; }
        public static KeyState KeyState { get; private set; }
        public static LibcFunction LibcFunction { get; private set; }
        public static ObjectTable Objects { get; private set; }
        public static PartyFinderGui PfGui { get; private set; }
        public static PartyList Party { get; private set; }
        public static SigScanner SigScanner { get; private set; }
        public static TargetManager Targets { get; private set; }
        public static ToastGui Toasts { get; private set; }

        internal static bool IsInitialized = false;
        public static void Init(DalamudPluginInterface pi)
        {
            if (IsInitialized)
            {
                PluginLog.Debug("Services already initialized, skipping");
            }
            IsInitialized = true;
            try
            {
                pi.Create<SDalamudPluginInterface>();
                PluginInterface = SDalamudPluginInterface.PluginInterface;

                pi.Create<SBuddyList>();
                Buddies = SBuddyList.Buddies;

                pi.Create<SChatGui>();
                Chat = SChatGui.Chat;

                pi.Create<SChatHandlers>();
                ChatHandlers = SChatHandlers.ChatHandlers;

                pi.Create<SClientState>();
                ClientState = SClientState.ClientState;

                pi.Create<SCommandManager>();
                Commands = SCommandManager.Commands;

                pi.Create<SCondition>();
                Condition = SCondition.Condition;

                pi.Create<SDataManager>();
                Data = SDataManager.Data;

                pi.Create<SFateTable>();
                Fates = SFateTable.Fates;

                pi.Create<SFlyTextGui>();
                FlyText = SFlyTextGui.FlyText;

                pi.Create<SFramework>();
                Framework = SFramework.Framework;

                pi.Create<SGameGui>();
                GameGui = SGameGui.GameGui;

                pi.Create<SGameNetwork>();
                GameNetwork = SGameNetwork.GameNetwork;

                pi.Create<SJobGauges>();
                Gauges = SJobGauges.Gauges;

                pi.Create<SKeyState>();
                KeyState = SKeyState.KeyState;

                pi.Create<SLibcFunction>();
                LibcFunction = SLibcFunction.LibcFunction;

                pi.Create<SObjectTable>();
                Objects = SObjectTable.Objects;

                pi.Create<SPartyFinderGui>();
                PfGui = SPartyFinderGui.PfGui;

                pi.Create<SPartyList>();
                Party = SPartyList.Party;

                pi.Create<SSigScanner>();
                SigScanner = SSigScanner.SigScanner;

                pi.Create<STargetManager>();
                Targets = STargetManager.Targets;

                pi.Create<SToastGui>();
                Toasts = SToastGui.Toasts;
            }
            catch(Exception ex)
            {
                ex.Log();
            }
        }
    }
}