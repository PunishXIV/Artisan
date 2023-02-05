using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan
{
    public sealed class Artisan : IDalamudPlugin
    {
        public string Name => "Artisan";
        private const string commandName = "/artisan";
        public static PluginUI? PluginUi { get; set; }
        public static bool currentCraftFinished = false;
        internal BlockingTask BotTask = new();
        public static Stopwatch Benchmark = new();

        public Artisan(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            pluginInterface.Create<Service>();
            //FFXIVClientStructs.Resolver.Initialize();
            Service.Plugin = this;

            Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(Service.Interface);


            ECommons.ECommons.Init(pluginInterface, this);
            PluginUi = new PluginUI(this);

            Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the Artisan menu."
            });

            Service.Interface.UiBuilder.Draw += DrawUI;
            Service.Interface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.Condition.ConditionChange += CheckForCraftedState;
            Service.Framework.Update += FireBot;
            Service.ClientState.Logout += DisableEndurance;
            Service.ClientState.Login += DisableEndurance;
            ActionWatching.Enable();
            StepChanged += ResetRecommendation;
            ConsumableChecker.Init();
            Handler.Init();
            CleanUpIndividualMacros();
        }

        private void DisableEndurance(object? sender, EventArgs e)
        {
            Handler.Enable = false;
            CraftingListUI.Processing = false;
        }

        public static void CleanUpIndividualMacros()
        {
            foreach (var item in Service.Configuration.IndividualMacros)
            {
                if (item.Value is null || !Service.Configuration.UserMacros.Any(x => x.ID == item.Value.ID))
                {
                    Service.Configuration.IndividualMacros.Remove(item.Key);
                    Service.Configuration.Save();
                }
            }
        }

        private void ResetRecommendation(object? sender, int e)
        {
            if (e == 0)
            {

            }
            CurrentRecommendation = 0;
        }

        public static bool CheckIfCraftFinished()
        {
            //if (QuickSynthMax > 0 && QuickSynthCurrent == QuickSynthMax) return true;
            if (MaxProgress == 0) return false;
            if (CurrentProgress == MaxProgress) return true;
            if (CurrentProgress < MaxProgress && CurrentDurability == 0) return true;
            currentCraftFinished = false;
            return false;
        }

        private void FireBot(Framework framework)
        {
            //if (BotTask.TryBlockOrExecute())
            //{
            //    return;
            //}

            if (!Service.ClientState.IsLoggedIn)
            {
                Handler.Enable = false;
                CraftingListUI.Processing = false;
            }
            PluginUi.CraftingVisible = Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.PreparingToCraft];
            if (!PluginUi.CraftingVisible)
            {
                ActionWatching.TryDisable();
                return;
            }
            else
                ActionWatching.TryEnable();

            GetCraft();
            if (CanUse(Skills.BasicSynth) && CurrentRecommendation == 0)
            {
                Benchmark.Restart();
                FetchRecommendation(CurrentStep);
            }

            if (CheckIfCraftFinished() && !currentCraftFinished)
            {
                currentCraftFinished = true;

                if (CraftingLists.CraftingListUI.Processing)
                {
                    Dalamud.Logging.PluginLog.Verbose("Advancing Crafting List");
                    CraftingLists.CraftingListFunctions.CurrentIndex++;
                }


                if (Handler.Enable && Service.Configuration.CraftingX && Service.Configuration.CraftX > 0)
                {
                    Service.Configuration.CraftX -= 1;
                    if (Service.Configuration.CraftX == 0)
                    {
                        Service.Configuration.CraftingX = false;
                        Handler.Enable = false;
                    }
                }

#if DEBUG
                if (PluginUi.repeatTrial && Service.Configuration.CraftingX && Service.Configuration.CraftX > 0)
                {
                    Service.Configuration.CraftX -= 1;
                    if (Service.Configuration.CraftX == 0)
                    {
                        Service.Configuration.CraftingX = false;
                        PluginUi.repeatTrial = false;
                    }
                }
#endif
            }


#if DEBUG
            if (PluginUi.repeatTrial)
            {
                RepeatTrialCraft();
            }
#endif

        }

        public static void FetchRecommendation(int e)
        {
            try
            {
                if (e == 0)
                {
                    Benchmark.Reset();
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
                    MacroStep = 0;

                    return;
                }

                CurrentRecommendation = Recipe.IsExpert ? GetExpertRecommendation() : GetRecommendation();

                if (Service.Configuration.UseMacroMode && Service.Configuration.UserMacros.Count > 0)
                {
                    if (Service.Configuration.IndividualMacros.TryGetValue(Recipe.RowId, out var macro))
                    {
                        macro = Service.Configuration.UserMacros.First(x => x.ID == macro.ID);
                        if (MacroStep < macro.MacroActions.Count)
                        {
                            if (macro.MacroOptions.SkipQualityIfMet)
                            {
                                if (CurrentQuality >= MaxQuality)
                                {
                                    while (ActionIsQuality(macro))
                                    {
                                        MacroStep++;
                                    }
                                }
                            }

                            if (macro.MacroOptions.UpgradeActions && ActionUpgradable(macro, out uint newAction))
                            {
                                CurrentRecommendation = newAction;
                            }
                            else
                            {
                                CurrentRecommendation = macro.MacroActions[MacroStep];
                            }
                        }
                    }
                    else
                    {
                        if (Service.Configuration.SetMacro != null && MacroStep < Service.Configuration.SetMacro.MacroActions.Count)
                        {
                            if (Service.Configuration.SetMacro.MacroOptions.SkipQualityIfMet)
                            {
                                if (CurrentQuality >= MaxQuality)
                                {
                                    while (ActionIsQuality(Service.Configuration.SetMacro))
                                    {
                                        MacroStep++;
                                    }
                                }
                            }

                            if (Service.Configuration.SetMacro.MacroOptions.UpgradeActions && ActionUpgradable(Service.Configuration.SetMacro, out uint newAction))
                            {
                                CurrentRecommendation = newAction;
                            }
                            else
                            {
                                CurrentRecommendation = Service.Configuration.SetMacro.MacroActions[MacroStep];
                            }
                        }
                    }
                }

                RecommendationName = CurrentRecommendation.NameOfAction();

                if (CurrentRecommendation != 0)
                {
                    if (LuminaSheets.ActionSheet.TryGetValue(CurrentRecommendation, out var normalAct))
                    {
                        if (normalAct.ClassJob.Value.RowId != CharacterInfo.JobID())
                        {
                            var newAct = LuminaSheets.ActionSheet.Values.Where(x => x.Name.RawString == normalAct.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                            CurrentRecommendation = newAct.RowId;
                            if (!Service.Configuration.DisableToasts)
                            {
                                QuestToastOptions options = new() { IconId = newAct.Icon };
                                Service.ToastGui.ShowQuest($"Use {newAct.Name}", options);
                            }

                        }
                        else
                        {
                            if (!Service.Configuration.DisableToasts)
                            {
                                QuestToastOptions options = new() { IconId = normalAct.Icon };
                                Service.ToastGui.ShowQuest($"Use {normalAct.Name}", options);
                            }
                        }
                    }

                    if (LuminaSheets.CraftActions.TryGetValue(CurrentRecommendation, out var craftAction))
                    {
                        if (craftAction.ClassJob.Row != CharacterInfo.JobID())
                        {
                            var newAct = LuminaSheets.CraftActions.Values.Where(x => x.Name.RawString == craftAction.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                            CurrentRecommendation = newAct.RowId;
                            if (!Service.Configuration.DisableToasts)
                            {
                                QuestToastOptions options = new() { IconId = newAct.Icon };
                                Service.ToastGui.ShowQuest($"Use {newAct.Name}", options);
                            }
                        }
                        else
                        {
                            if (!Service.Configuration.DisableToasts)
                            {
                                QuestToastOptions options = new() { IconId = craftAction.Icon };
                                Service.ToastGui.ShowQuest($"Use {craftAction.Name}", options);
                            }
                        }
                    }

                    if (Service.Configuration.AutoMode)
                    {
                        Service.Framework.RunOnTick(() => Hotbars.ExecuteRecommended(CurrentRecommendation), TimeSpan.FromMilliseconds(Service.Configuration.AutoDelay));

                        //Service.Plugin.BotTask.Schedule(() => Hotbars.ExecuteRecommended(CurrentRecommendation), Service.Configuration.AutoDelay);
                    }

                    return;
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, "Crafting Step Change");
            }

        }

        private static bool ActionUpgradable(Macro macro, out uint newAction)
        {
            newAction = macro.MacroActions[MacroStep];
            if (CurrentCondition is CraftingLogic.CurrentCraft.Condition.Good or CraftingLogic.CurrentCraft.Condition.Excellent)
            {
                switch (newAction)
                {
                    case Skills.FocusedSynthesis:
                    case Skills.Groundwork:
                    case Skills.PrudentSynthesis:
                    case Skills.CarefulSynthesis:
                    case Skills.BasicSynth:
                        newAction = Skills.IntensiveSynthesis;
                        break;
                    case Skills.HastyTouch:
                    case Skills.FocusedTouch:
                    case Skills.PreparatoryTouch:
                    case Skills.AdvancedTouch:
                    case Skills.StandardTouch:
                    case Skills.BasicTouch:
                        newAction = Skills.PreciseTouch;
                        break;
                }

                return CanUse(newAction);
            }

            return false;
        }

        public static bool ActionIsQuality(Macro macro)
        {
            var currentAction = macro.MacroActions[MacroStep];
            switch (currentAction)
            {
                case Skills.HastyTouch:
                case Skills.FocusedTouch:
                case Skills.PreparatoryTouch:
                case Skills.AdvancedTouch:
                case Skills.StandardTouch:
                case Skills.BasicTouch:
                case Skills.GreatStrides:
                case Skills.Innovation:
                case Skills.ByregotsBlessing:
                case Skills.TrainedFinesse:
                    return true;
                default:
                    return false;
            }
        }

        private void CheckForCraftedState(ConditionFlag flag, bool value)
        {
            if (flag == ConditionFlag.Crafting && value)
            {
                PluginUi.CraftingVisible = true;
            }
        }

        public void Dispose()
        {
            PluginUi.Dispose();
            Handler.Dispose();
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
            PluginUi.Visible = !PluginUi.Visible;
        }

        private void DrawUI()
        {
            PluginUi.Draw();
        }

        private void DrawConfigUI()
        {
            PluginUi.Visible = true;
        }
    }
}
