using Artisan.RawInformation;
using Dalamud.Interface.Components;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
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

        private IDalamudPlugin Plugin;
        public PluginUI(Artisan plugin)
        {
            Plugin = plugin;
        }

        public void Dispose()
        {

        }

        public void Draw()
        {
            DrawCraftingWindow();

            if (Service.Configuration.ShowEHQ)
                MarkChanceOfSuccess();
            Hotbars.MakeButtonsGlow(CurrentRecommendation);

            if (!Visible)
            {
                return;
            }

            ImGui.SetWindowSize(new Vector2(500, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("Artisan", ref visible, ImGuiWindowFlags.AlwaysUseWindowPadding))
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
                        PunishLib.ImGuiMethods.AboutTab.Draw(Plugin);
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
            }
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

                var visCheck = (AtkComponentNode*)addonPtr->UldManager.NodeList[6];
                if (!visCheck->AtkResNode.IsVisible)
                    return;

                var selectedCraftNameNode = (AtkTextNode*)addonPtr->UldManager.NodeList[49];
                var selectedCraftName = selectedCraftNameNode->NodeText.ToString()[14..];
                selectedCraftName = selectedCraftName.Remove(selectedCraftName.Length - 10, 10);
                if (!char.IsLetterOrDigit(selectedCraftName[^1]))
                {
                    selectedCraftName = selectedCraftName.Remove(selectedCraftName.Length - 1, 1).Trim();
                }

                string selectedCalculated = CalculateEstimate(selectedCraftName);
                AtkResNodeFunctions.DrawSuccessRate(&selectedCraftNameNode->AtkResNode, selectedCalculated);

                var craftCount = (AtkTextNode*)addonPtr->UldManager.NodeList[63];
                string count = craftCount->NodeText.ToString();
                int maxCrafts = Convert.ToInt32(count.Split("-")[^1]);

                var crafts = (AtkComponentNode*)addonPtr->UldManager.NodeList[67];
                if (crafts->AtkResNode.IsVisible)
                {
                    var currentShownNodes = 0;

                    for (int i = 1; i <= 13; i++)
                    {
                        var craft = (AtkComponentNode*)crafts->Component->UldManager.NodeList[i];
                        if (craft->AtkResNode.IsVisible && craft->AtkResNode.Y >= 0 && craft->AtkResNode.Y < 340 && currentShownNodes < 10 && currentShownNodes < maxCrafts)
                        {
                            currentShownNodes++;
                            var craftNameNode = (AtkTextNode*)craft->Component->UldManager.NodeList[14];
                            var ItemName = craftNameNode->NodeText.ToString()[14..];
                            ItemName = ItemName.Remove(ItemName.Length - 10, 10);
                            if (!char.IsLetterOrDigit(ItemName[^1]))
                            {
                                ItemName = ItemName.Remove(ItemName.Length - 1, 1).Trim();
                            }

                            string calculatedPercentage = CalculateEstimate(ItemName);

                            AtkResNodeFunctions.DrawSuccessRate(&craft->AtkResNode, $"{calculatedPercentage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Dalamud.Logging.PluginLog.Error(ex, "DrawRecipeChance");
            }
        }

        private static string CalculateEstimate(string itemName)
        {

            var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.RawString.Equals(itemName)).FirstOrDefault();
            if (sheetItem == null)
                return "Unknown Item - Check Selected Recipe Window";
            var recipeTable = sheetItem.RecipeLevelTable.Value;

            if (!sheetItem.ItemResult.Value.CanBeHq && !sheetItem.IsExpert && !sheetItem.ItemResult.Value.IsCollectable)
                return $"Item cannot be HQ.";

            if (CharacterInfo.Craftsmanship() < sheetItem.RequiredCraftsmanship || CharacterInfo.Control() < sheetItem.RequiredControl)
                return "Unable to craft with current stats.";

            if (CharacterInfo.CharacterLevel() >= 80 && CharacterInfo.CharacterLevel() >= sheetItem.RecipeLevelTable.Value.ClassJobLevel + 10 && !sheetItem.IsExpert)
                return "EHQ: Guaranteed.";

            var difficulty = recipeTable.Difficulty;
            var baseQual = BaseQuality(sheetItem);
            var dur = recipeTable.Durability;
            var baseSteps = baseQual * (dur / 10);
            var maxQual = (double)recipeTable.Quality;
            bool meetsRecCon = CharacterInfo.Control() >= recipeTable.SuggestedControl;
            bool meetsRecCraft = CharacterInfo.Craftsmanship() >= recipeTable.SuggestedCraftsmanship;
            var q1 = baseSteps / maxQual;
            var q2 = CharacterInfo.MaxCP / sheetItem.QualityFactor / 1.5;
            var q3 = CharacterInfo.IsManipulationUnlocked() ? 2 : 1;
            var q4 = recipeTable.Stars > 0 ? 7 * (recipeTable.Stars * recipeTable.Stars) : 0;
            var q5 = meetsRecCon && meetsRecCraft ? 3 : 1;
            var q6 = Math.Floor((q1 * 100) + (q2 * 3 * q3 * q5) - q4);
            var chance = q6 > 100 ? 100 : q6;
            chance = chance < 0 ? 0 : chance;

            switch (chance)
            {
                case < 20:
                    return "EHQ: Do not attempt.";
                case < 40:
                    return "EHQ: Very low chance.";
                case < 60:
                    return "EHQ: Average chance.";
                case < 80:
                    return "EHQ: Good chance.";
                case < 90:
                    return "EHQ: High chance.";
                case < 100:
                    return "EHQ: Very high chance.";
                default:
                    return "EHQ: Guaranteed.";
            }
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
            ImGui.TextWrapped($"Here you can change some settings Artisan will use. Some of these can also be toggled during a craft.");
            ImGui.TextWrapped($"In order to use Artisan, please slot every crafting action you have unlocked to a visible hotbar.");
            bool autoEnabled = Service.Configuration.AutoMode;
            bool autoCraft = Service.Configuration.AutoCraft;
            bool failureCheck = Service.Configuration.DisableFailurePrediction;
            int maxQuality = Service.Configuration.MaxPercentage;
            bool useTricksGood = Service.Configuration.UseTricksGood;
            bool useTricksExcellent = Service.Configuration.UseTricksExcellent;
            bool useSpecialist = Service.Configuration.UseSpecialist;
            bool showEHQ = Service.Configuration.ShowEHQ;

            ImGui.Separator();
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

            if (ImGui.Checkbox("Show Estimated HQ on Recipe (EHQ)", ref showEHQ))
            {
                Service.Configuration.ShowEHQ = showEHQ;
                Service.Configuration.Save();
            }
            ImGuiComponents.HelpMarker($"This will mark in the crafting list an estimated HQ chance based on your current stats.\nThis does not factor in any HQ items used as materials.\nIt is also only a rough estimate due to the nature of crafting.");

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
