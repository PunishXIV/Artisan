using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.IO;
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
            DrawCraftingWindow();

            if (!Visible)
            {
                return;
            }

            ImGui.SetWindowSize(new Vector2(500, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Artisan", ref visible, ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (ImGui.BeginTabBar("TabBar"))
                {
                    if (ImGui.BeginTabItem("Settings"))
                    {
                        DrawMainWindow();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("About"))
                    {
                        DrawAboutUs();
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
            }
        }

        private void DrawAboutUs()
        {
            
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

        public unsafe static void MarkChanceOfSuccess()
        {
            try
            {
                var recipeWindow = Service.GameGui.GetAddonByName("RecipeNote", 1);
                if (recipeWindow == IntPtr.Zero)
                    return;

                var addonPtr = (AtkUnitBase*)recipeWindow;
                if (addonPtr == null)
                    return;

                var baseX = addonPtr->X;
                var baseY = addonPtr->Y;

                var crafts = (AtkComponentNode*)addonPtr->UldManager.NodeList[67];
                if (crafts->AtkResNode.IsVisible)
                {
                    DrawSuccessRate(baseX, baseY, "LOOK AT ME");

                }
            }
            catch (Exception ex)
            {

            }
        }

        public static void DrawSuccessRate(float x, float y, string str)
        {
            ImGuiHelpers.ForceNextWindowMainViewport();
            var textSize = ImGui.CalcTextSize(str);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(x -  textSize.X, y - textSize.Y / 2f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(2f, 0f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, new Vector2(0f, 0f));
            ImGui.Begin("##enmityhp" + y, ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoNavFocus
                | ImGuiWindowFlags.AlwaysUseWindowPadding | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoSavedSettings);
            ImGui.TextUnformatted(str);
            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        public void DrawMainWindow()
        {
            ImGui.TextWrapped($"Here you can change some settings Artisan will use. Some of these can also be toggled during a craft.");
            bool autoEnabled = Service.Configuration.AutoMode;
            bool autoCraft = Service.Configuration.AutoCraft;
            bool failureCheck = Service.Configuration.DisableFailurePrediction;
            int maxQuality = Service.Configuration.MaxPercentage;
            bool useTricksGood = Service.Configuration.UseTricksGood;
            bool useTricksExcellent = Service.Configuration.UseTricksExcellent;
            bool useSpecialist = Service.Configuration.UseSpecialist;

            if (ImGui.Checkbox("Auto Mode Enabled", ref autoEnabled))
            {
                Service.Configuration.AutoMode = autoEnabled;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"Automatically use each recommended action.\nRequires the action to be on a visible hotbar.");
            if (ImGui.Checkbox($"Automatically Repeat Last Craft", ref autoCraft))
            {
                Service.Configuration.AutoCraft = autoCraft;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"Repeats the currently selected craft in your recipe list.\nWill only work whilst you have the items.\nThis will repeat using your set item quality settings.");
            if (ImGui.Checkbox($"Disable Failure Prediction", ref failureCheck))
            {
                Service.Configuration.DisableFailurePrediction = failureCheck;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"Disabling failure prediction may result in items failing to be crafted.\nUse at your own discretion.");

            if (ImGui.Checkbox("Use Tricks of the Trade - Good", ref useTricksGood))
            {
                Service.Configuration.UseTricksGood = useTricksGood;
                Service.Configuration.Save();
            }
            ImGui.SameLine();
            if (ImGui.Checkbox("Use Tricks of the Trade - Excellent", ref useTricksExcellent))
            {
                Service.Configuration.UseTricksExcellent = useTricksExcellent;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"These 2 options allow you to make Tricks of the Trade a priority when condition is Good or Excellent.\nOther skills that rely on these conditions will not be used.");
            if (ImGui.Checkbox("Use Specialist Actions", ref useSpecialist))
            {
                Service.Configuration.UseSpecialist = useSpecialist;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker("If the current job is a specialist, spends any Crafter's Delineation you may have.\nCareful Observation replaces Observe.");
            ImGui.TextWrapped("Max Quality%%");
            ImGuiComponents.HelpMarker($"Once quality has reached the below percentage, Artisan will focus on progress only.");
            if (ImGui.SliderInt("###SliderMaxQuality", ref maxQuality, 0, 100, $"{maxQuality}%%"))
            {
                Service.Configuration.MaxPercentage = maxQuality;
                Service.Configuration.Save();
            }


        }


    }
}
