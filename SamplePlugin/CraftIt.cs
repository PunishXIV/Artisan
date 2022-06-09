using CraftIt.CraftingLogic;
using CraftIt.RawInformation;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.IO;
using System.Threading.Tasks;
using ClickLib;
using NAudio.Wave;

namespace CraftIt
{
    public sealed class CraftIt : IDalamudPlugin
    {
        public string Name => "Craft-It";

        private const string commandName = "/craftit";
        private PluginUI PluginUi { get; init; }

        private IWavePlayer waveOut;
        private Mp3FileReader mp3FileReader;

        public CraftIt(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager)
        {
            FFXIVClientStructs.Resolver.Initialize();

            pluginInterface.Create<Service>();

            Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.Configuration.Initialize(Service.Interface);

            Service.Address = new PluginAddressResolver();
            Service.Address.Setup();

            // you might normally want to embed resources and load them from the manifest stream
            var imagePath = Path.Combine(Service.Interface.AssemblyLocation.Directory?.FullName!, "Icon.png");
            var slothImg = Service.Interface.UiBuilder.LoadImage(imagePath);
            this.PluginUi = new PluginUI(Service.Configuration, slothImg);
            this.PluginUi.CraftingWindowStateChanged += PluginUi_CraftingWindowStateChanged;

            Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the Craft-It menu."
            });

            Service.Interface.UiBuilder.Draw += DrawUI;
            Service.Interface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Service.ToastGui.Enable();
            Service.Condition.ConditionChange += CheckForCraftedState;
            CurrentCraft.StepChanged += FetchRecommendation;

            this.waveOut = new WaveOutEvent(); // or new WaveOutEvent() if you are not using WinForms/WPF
            this.mp3FileReader = new Mp3FileReader(@"C:\Users\thero\Desktop\b!tches I'm back!.mp3");
            this.waveOut.Init(mp3FileReader);

        }

        private void PluginUi_CraftingWindowStateChanged(object? sender, bool e)
        {
            Dalamud.Logging.PluginLog.Debug(e.ToString());
            if (!e)
                StopMp3();
            else
                PlayMp3();
        }

        private async void FetchRecommendation(object? sender, int e)
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

                    return;
                }

                var rec = CurrentCraft.GetRecommendation();
                CurrentCraft.CurrentRecommendation = rec;

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

                    await Task.Delay(1000);
                    if (Service.Configuration.AutoMode)
                    {
                        Hotbars.ExecuteRecommended(rec);
                    }
                }
            }
            catch (Exception ex)
            {
                Dalamud.Logging.PluginLog.Error(ex, ex.StackTrace);
            }

        }

        private void PlayMp3()
        {
            StopMp3();
            this.mp3FileReader.Position = 0;
            this.waveOut.Play();
        }

        private void StopMp3()
        {
            this.waveOut.Stop();
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
