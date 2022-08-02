using Artisan.CraftingLogic;
using Artisan.RawInformation;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
            FFXIVClientStructs.Resolver.Initialize();

            pluginInterface.Create<Service>();

            Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(Service.Interface);

            Service.Address = new PluginAddressResolver();
            Service.Address.Setup();

            var imagePath = Path.Combine(Service.Interface.AssemblyLocation.Directory?.FullName!, "Icon.png");
            var artisanImg = Service.Interface.UiBuilder.LoadImage(imagePath);
            this.PluginUi = new PluginUI();

            Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the Artisan menu."
            });

            Service.Interface.UiBuilder.Draw += DrawUI;
            Service.Interface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.Condition.ConditionChange += CheckForCraftedState;
            Service.Framework.Update += FireBot;
            StepChanged += ResetRecommendation;



        }

        private void ResetRecommendation(object? sender, int e)
        {
            CurrentRecommendation = 0;
        }

        private void FireBot(Framework framework)
        {
            if (!Service.Condition[ConditionFlag.Crafting]) PluginUi.CraftingVisible = false;

            GetCraft();
            if (GetStatus(Buffs.FinalAppraisal)?.StackCount == 5 && CurrentRecommendation == Skills.FinalAppraisal)
            {
                FetchRecommendation(CurrentStep, CurrentStep);
            }
            if (CanUse(Skills.BasicSynth) && CurrentRecommendation == 0)
            {
                FetchRecommendation(CurrentStep, CurrentStep);
            }

            bool enableAutoRepeat = Service.Configuration.AutoCraft;
            if (enableAutoRepeat)
                RepeatActualCraft();
        }

        public static async void FetchRecommendation(object? sender, int e)
        {
            try
            {
                if (e == 0)
                {
                    CurrentCraft.CurrentRecommendation = 0;
                    CurrentCraft.ManipulationUsed = false;
                    CurrentCraft.JustUsedObserve = false;
                    CurrentCraft.VenerationUsed = false;
                    CurrentCraft.InnovationUsed = false;
                    CurrentCraft.WasteNotUsed = false;
                    CurrentCraft.JustUsedFinalAppraisal = false;

                    return;
                }

                var rec = CurrentCraft.GetRecommendation();
                CurrentCraft.CurrentRecommendation = rec;

                if (GetStatus(Buffs.FinalAppraisal)?.StackCount == 5)
                {
                    await Task.Delay(300);
                }

                if (rec != 0)
                {
                    if (LuminaSheets.ActionSheet.TryGetValue(rec, out var normalAct))
                    {
                        QuestToastOptions options = new QuestToastOptions() { IconId = normalAct.Icon };
                        Service.ToastGui.ShowQuest($"Use {normalAct.Name}", options);
                    }
                    if (LuminaSheets.CraftActions.TryGetValue(rec, out var craftAct))
                    {
                        QuestToastOptions options = new QuestToastOptions() { IconId = craftAct.Icon };
                        Service.ToastGui.ShowQuest($"Use {craftAct.Name}", options);
                    }

                    if (Service.Configuration.AutoMode)
                    {
                        Hotbars.ExecuteRecommended(rec);
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

            Service.CommandManager.RemoveHandler(commandName);

            Service.Interface.UiBuilder.OpenConfigUi -= DrawConfigUI;
            Service.Interface.UiBuilder.Draw -= DrawUI;
            CurrentCraft.StepChanged -= FetchRecommendation;
            Service.Framework.Update -= FireBot;


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
            this.PluginUi.SettingsVisible = true;
        }
    }
}
