using Artisan.Autocraft;
using Artisan.RawInformation;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using System.Linq;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan
{
    public sealed class Artisan : IDalamudPlugin
    {
        public string Name => "Artisan";
        private const string commandName = "/artisan";
        private PluginUI PluginUi { get; init; }

        public Artisan(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            pluginInterface.Create<Service>();
            FFXIVClientStructs.Resolver.Initialize();
            Service.Plugin = this;

            Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(Service.Interface);

            Service.Address = new PluginAddressResolver();
            Service.Address.Setup();

            ECommons.ECommons.Init(pluginInterface);
            this.PluginUi = new PluginUI(this);

            Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the Artisan menu."
            });

            Service.Interface.UiBuilder.Draw += DrawUI;
            Service.Interface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.Condition.ConditionChange += CheckForCraftedState;
            Service.Framework.Update += FireBot;
            ActionWatching.Enable();
            StepChanged += ResetRecommendation;
            ConsumableChecker.Init();
            Autocraft.Handler.Init();
        }

        private void ResetRecommendation(object? sender, int e)
        {
            CurrentRecommendation = 0;
        }

        private void FireBot(Framework framework)
        {
            PluginUi.CraftingVisible = Service.Condition[ConditionFlag.Crafting];

            GetCraft();
            if (CanUse(Skills.BasicSynth) && CurrentRecommendation == 0)
            {
                FetchRecommendation(CurrentStep);
            }

#if DEBUG
            if (PluginUi.repeatTrial)
            {
                RepeatTrialCraft();
            }
#endif
            if (Autocraft.Handler.Enable)
            {
                return;
            }

            bool enableAutoRepeat = Service.Configuration.AutoCraft;
            if (enableAutoRepeat)
            {
                PluginLog.Debug($"Looping");
                RepeatActualCraft();
            }
        }

        public static void FetchRecommendation(int e)
        {
            try
            {
                if (e == 0)
                {
                    CurrentRecommendation = 0;
                    ManipulationUsed = false;
                    JustUsedObserve = false;
                    VenerationUsed = false;
                    InnovationUsed = false;
                    WasteNotUsed = false;
                    JustUsedFinalAppraisal = false;
                    BasicTouchUsed = false;
                    StandardTouchUsed = false;
                    AdvancedTouchUsed = false;
                    ExpertCraftOpenerFinish = false;

                    return;
                }

                CurrentRecommendation = Recipe.IsExpert ? GetExpertRecommendation() : GetRecommendation();

                if (CurrentRecommendation != 0)
                {
                    if (LuminaSheets.ActionSheet.TryGetValue(CurrentRecommendation, out var normalAct))
                    {
                        if (normalAct.ClassJob.Value.RowId != CharacterInfo.JobID())
                        {
                            var newAct = LuminaSheets.ActionSheet.Values.Where(x => x.Name.RawString == normalAct.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                            QuestToastOptions options = new() { IconId = newAct.Icon };
                            Service.ToastGui.ShowQuest($"Use {newAct.Name}", options);

                        }
                        else
                        {
                            QuestToastOptions options = new() { IconId = normalAct.Icon };
                            Service.ToastGui.ShowQuest($"Use {normalAct.Name}", options);
                        }
                    }

                    if (LuminaSheets.CraftActions.TryGetValue(CurrentRecommendation, out var craftAction))
                    {
                        if (craftAction.ClassJob.Row != CharacterInfo.JobID())
                        {
                            var newAct = LuminaSheets.CraftActions.Values.Where(x => x.Name.RawString == craftAction.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                            QuestToastOptions options = new() { IconId = newAct.Icon };
                            Service.ToastGui.ShowQuest($"Use {newAct.Name}", options);
                        }
                        else
                        {
                            QuestToastOptions options = new() { IconId = craftAction.Icon };
                            Service.ToastGui.ShowQuest($"Use {craftAction.Name}", options);
                        }
                    }

                    if (Service.Configuration.AutoMode)
                    {
                        Hotbars.ExecuteRecommended(CurrentRecommendation);
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "Crafting Step Change");
            }

        }

        private void CheckForCraftedState(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.Crafting && value)
            {
                this.PluginUi.CraftingVisible = true;
            }
        }

        public void Dispose()
        {
            this.PluginUi.Dispose();
            Autocraft.Handler.Dispose();
            ECommons.ECommons.Dispose();

            Service.CommandManager.RemoveHandler(commandName);

            Service.Interface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            Service.Interface.UiBuilder.Draw -= DrawUI;
            Service.Framework.Update -= FireBot;

            ActionWatching.Dispose();
            Service.Plugin = null!;
        }

        private void OnCommand(string command, string args)
        {
            this.PluginUi.Visible = true;
        }

        private void DrawUI()
        {
            this.PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            this.PluginUi.Visible = true;
        }
    }
}
