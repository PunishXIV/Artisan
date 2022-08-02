using Artisan.CraftingLogic;
using Artisan.RawInformation;
using Dalamud.Interface.Components;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        private bool craftingVisible = false;
        public bool CraftingVisible
        {
            get { return this.craftingVisible; }
            set { if (this.craftingVisible != value) CraftingWindowStateChanged?.Invoke(this, value); this.craftingVisible = value; }
        }

        public PluginUI()
        {

        }

        public void Dispose()
        {
            
        }

        public void Draw()
        {
            DrawMainWindow();
            DrawCraftingWindow();
        }

        private void DrawCraftingWindow()
        {
            if (!CraftingVisible)
            {
                return;
            }

            CraftingVisible = craftingVisible;

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Artisan Crafting Window", ref this.craftingVisible, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            {
                Hotbars.MakeButtonsGlow(CurrentRecommendation);

                bool autoMode = Service.Configuration.AutoMode;

                if (ImGui.Checkbox("Auto Mode", ref autoMode))
                {
                    Service.Configuration.AutoMode = autoMode;
                    Service.Configuration.Save();
                }

                bool enableAutoRepeat = Service.Configuration.AutoCraft;

                if (ImGui.Checkbox("Automatically Repeat Last Craft", ref enableAutoRepeat))
                {
                    Service.Configuration.AutoCraft = enableAutoRepeat;
                    Service.Configuration.Save();
                }

                bool failureCheck = Service.Configuration.DisableFailurePrediction;

                if (ImGui.Checkbox($"Disable Failure Prediction", ref failureCheck))
                {
                    Service.Configuration.DisableFailurePrediction = failureCheck;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker($"Disabling failure prediction may result in items failing to be crafted.\nUse at your own discretion.");

                ImGui.Text("Semi-Manual Mode");

                if (ImGui.Button("Execute recommended action"))
                {
                    Hotbars.ExecuteRecommended(CurrentRecommendation);
                }
                if (ImGui.Button("Fetch Recommendation"))
                {
                    Artisan.FetchRecommendation(null, 0);
                }

            }
            ImGui.End();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Artisan Config", ref this.visible, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextWrapped($"Here you can change some settings Artisan will use. Some of these can also be toggled during a craft.");
                bool autoEnabled = Service.Configuration.AutoMode;
                bool autoCraft = Service.Configuration.AutoCraft;
                bool failureCheck = Service.Configuration.DisableFailurePrediction;
                int maxQuality = Service.Configuration.MaxPercentage;


                if (ImGui.Checkbox("Auto Mode Enabled", ref autoEnabled))
                {
                    Service.Configuration.AutoMode = autoEnabled;
                    Service.Configuration.Save();
                }
                if (ImGui.Checkbox($"Automatically Repeat Last Craft", ref autoCraft))
                {
                    Service.Configuration.AutoCraft = autoCraft;
                    Service.Configuration.Save();
                }
                if (ImGui.Checkbox($"Disable Failure Prediction", ref failureCheck))
                {
                    Service.Configuration.DisableFailurePrediction = failureCheck;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker($"Disabling failure prediction may result in items failing to be crafted.\nUse at your own discretion.");

                ImGui.TextWrapped("Max Quality%%");
                ImGui.TextWrapped("Once quality has reached the below percentage, Artisan will focus on progress only.");
                if (ImGui.SliderInt("###SliderMaxQuality", ref maxQuality, 0, 100, $"{maxQuality}%%"))
                {
                    Service.Configuration.MaxPercentage = maxQuality;
                    Service.Configuration.Save();
                }
            }
            ImGui.End();
        }

        
    }
}
