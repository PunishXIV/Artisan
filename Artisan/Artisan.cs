using Artisan.Autocraft;
using Artisan.RawInformation;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan
{
    public sealed class Artisan : IDalamudPlugin
    {
        public string Name => "Artisan";
        private const string commandName = "/artisan";
        private PluginUI PluginUi { get; init; }
        private bool currentCraftFinished = false;

        public Artisan(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            pluginInterface.Create<Service>();
            //FFXIVClientStructs.Resolver.Initialize();
            Service.Plugin = this;

            Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(Service.Interface);


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
            if (e == 0)
            {

            }
            CurrentRecommendation = 0;
        }

        private bool CheckIfCraftFinished()
        {
            if (MaxProgress == 0) return false;
            if (CurrentProgress == MaxProgress) return true;
            if (CurrentProgress < MaxProgress && CurrentDurability == 0) return true;
            currentCraftFinished = false;
            return false;
        }

        private void FireBot(Framework framework)
        {
            PluginUi.CraftingVisible = Service.Condition[ConditionFlag.Crafting];
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
                Task.Factory.StartNew(() => FetchRecommendation(CurrentStep));
            }

            if (CheckIfCraftFinished() && !currentCraftFinished)
            {
                currentCraftFinished = true;

                if (Handler.Enable && Service.Configuration.CraftingX && Service.Configuration.CraftX > 0)
                {
                    Service.Configuration.CraftX -= 1;
                    if (Service.Configuration.CraftX == 0)
                        Handler.Enable = false;
                }

#if DEBUG
                if (PluginUi.repeatTrial && Service.Configuration.CraftingX && Service.Configuration.CraftX > 0)
                {
                    Service.Configuration.CraftX -= 1;
                    if (Service.Configuration.CraftX == 0)
                        PluginUi.repeatTrial = false;
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

                if (Service.Configuration.UseMacroMode && Service.Configuration.SetMacro != null && MacroStep < Service.Configuration.SetMacro.MacroActions.Count)
                {
                    if (Service.Configuration.SetMacro.MacroOptions.SkipQualityIfMet)
                    {
                        if (CurrentQuality >= MaxQuality)
                        {
                            while (ActionIsQuality())
                            {
                                MacroStep++;
                            }
                        }
                    }

                    if (Service.Configuration.SetMacro.MacroOptions.UpgradeActions && ActionUpgradable(out uint newAction))
                    {
                        CurrentRecommendation = newAction;
                    }
                    else
                    {
                        CurrentRecommendation = Service.Configuration.SetMacro.MacroActions[MacroStep];
                    }
                }
                else
                {
                    CurrentRecommendation = Recipe.IsExpert ? GetExpertRecommendation() : GetRecommendation();
                }

                if (CurrentRecommendation != 0)
                {
                    if (LuminaSheets.ActionSheet.TryGetValue(CurrentRecommendation, out var normalAct))
                    {
                        if (normalAct.ClassJob.Value.RowId != CharacterInfo.JobID())
                        {
                            var newAct = LuminaSheets.ActionSheet.Values.Where(x => x.Name.RawString == normalAct.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                            CurrentRecommendation = newAct.RowId;
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
                            CurrentRecommendation = newAct.RowId;
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
                        Task.Delay(Service.Configuration.AutoDelay).Wait();
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

        private static bool ActionUpgradable(out uint newAction)
        {
            newAction = Service.Configuration.SetMacro.MacroActions[MacroStep];
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

        private static bool ActionIsQuality()
        {
            var currentAction = Service.Configuration.SetMacro.MacroActions[MacroStep];
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
