using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.RawInformation;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using ImGuiNET;
using System;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan.UI
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    unsafe internal class PluginUI : Window
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public OpenWindow OpenWindow { get; private set; } = OpenWindow.None;

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

        public PluginUI() : base($"{P.Name} {P.GetType().Assembly.GetName().Version}###Artisan")
        {
            this.SizeConstraints = new()
            {
                MinimumSize = new(250, 100),
                MaximumSize = new(9999, 9999)
            };
            P.ws.AddWindow(this);
        }

        public override void PreDraw()
        {
            if (!P.config.DisableTheme)
            {
                P.Style.Push();
                P.StylePushed = true;
            }

        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                P.StylePushed = false;
            }
        }

        public void Dispose()
        {

        }

        public override void Draw()
        {

            ImGui.Columns(2, "###MainWindow", false);
            ImGui.SetColumnWidth(0, ImGui.GetContentRegionAvail().Length() / 3);

            if (ThreadLoadImageHandler.TryGetTextureWrap("https://love.puni.sh/resources/artisan.png", out var logo))
            {
                ImGui.Image(logo.ImGuiHandle, new(ImGui.GetContentRegionAvail().X - 30f.Scale(), ImGui.GetContentRegionAvail().X - 30f.Scale()));
            }

            if (ImGui.Selectable("Settings"))
            {
                OpenWindow = OpenWindow.Main;
            }

            if (ImGui.Selectable("Endurance"))
            {
                OpenWindow = OpenWindow.Endurance;
            }

            if (ImGui.Selectable("Crafting Lists"))
            {
                OpenWindow = OpenWindow.Lists;
            }

            if (ImGui.Selectable("About"))
            {
                OpenWindow = OpenWindow.About;
            }

            if (ImGui.Selectable("DEBUG"))
            {
                OpenWindow = OpenWindow.Debug;
            }

            if (OpenWindow == OpenWindow.Main)
            {
                ImGui.NextColumn();
                DrawMainWindow();
                ImGui.NextColumn();
            }

            if (OpenWindow == OpenWindow.Endurance)
            {
                ImGui.NextColumn();
                Handler.Draw();
                ImGui.NextColumn();
            }

            if (OpenWindow == OpenWindow.Lists)
            {
                ImGui.NextColumn();
                CraftingListUI.Draw();
                ImGui.NextColumn();
            }

            if (OpenWindow == OpenWindow.About)
            {
                ImGui.NextColumn();
                PunishLib.ImGuiMethods.AboutTab.Draw(P);
                ImGui.NextColumn();
            }

            if (OpenWindow == OpenWindow.Debug)
            {
                ImGui.NextColumn();
                AutocraftDebugTab.Draw();
                ImGui.NextColumn();
            }

            ImGui.Columns(1);


            //            if (ImGui.BeginTabBar("TabBar"))
            //            {
            //                if (ImGui.BeginTabItem("Settings"))
            //                {
            //                    DrawMainWindow();
            //                    ImGui.EndTabItem();
            //                }
            //                if (ImGui.BeginTabItem("Endurance/Auto-Repeat Mode"))
            //                {
            //                    Handler.Draw();
            //                    ImGui.EndTabItem();
            //                }
            //                if (ImGui.BeginTabItem("Macros"))
            //                {
            //                    MacroUI.Draw();
            //                    ImGui.EndTabItem();
            //                }
            //                if (ImGui.BeginTabItem("Crafting List (BETA)"))
            //                {
            //                    CraftingListUI.Draw();
            //                    ImGui.EndTabItem();
            //                }

            //                if (ImGui.BeginTabItem("About"))
            //                {
            //                    PunishLib.ImGuiMethods.AboutTab.Draw(P);
            //                    ImGui.EndTabItem();
            //                }
            //#if DEBUG
            //                if (ImGui.BeginTabItem("Debug"))
            //                {
            //                    AutocraftDebugTab.Draw();
            //                    ImGui.EndTabItem();
            //                }
            //#endif
            //                ImGui.EndTabBar();
            //            }
            //            if (!visible)
            //            {
            //                Service.Configuration.Save();
            //                PluginLog.Information("Configuration saved");
            //            }

        }

        //private static string CalculateEstimate(string itemName)
        //{
        //    var sheetItem = LuminaSheets.RecipeSheet?.Values.Where(x => x.ItemResult.Value.Name!.RawString.Equals(itemName)).FirstOrDefault();
        //    if (sheetItem == null)
        //        return "Unknown Item - Check Selected Recipe Window";
        //    var recipeTable = sheetItem.RecipeLevelTable.Value;

        //    if (!sheetItem.ItemResult.Value.CanBeHq && !sheetItem.IsExpert && !sheetItem.ItemResult.Value.IsCollectable)
        //        return $"Item cannot be HQ.";

        //    if (CharacterInfo.Craftsmanship() < sheetItem.RequiredCraftsmanship || CharacterInfo.Control() < sheetItem.RequiredControl)
        //        return "Unable to craft with current stats.";

        //    if (CharacterInfo.CharacterLevel() >= 80 && CharacterInfo.CharacterLevel() >= sheetItem.RecipeLevelTable.Value.ClassJobLevel + 10 && !sheetItem.IsExpert)
        //        return "EHQ: Guaranteed.";

        //    var simulatedPercent = Service.Configuration.UseSimulatedStartingQuality && sheetItem.MaterialQualityFactor != 0 ? Math.Floor(((double)Service.Configuration.CurrentSimulated / ((double)sheetItem.RecipeLevelTable.Value.Quality * ((double)sheetItem.QualityFactor / 100))) * 100) : 0;
        //    simulatedPercent = CurrentSelectedCraft is null || CurrentSelectedCraft != sheetItem.ItemResult.Value.Name!.RawString ? 0 : simulatedPercent;
        //    var baseQual = BaseQuality(sheetItem);
        //    var dur = recipeTable.Durability;
        //    var baseSteps = baseQual * (dur / 10);
        //    var maxQual = (double)recipeTable.Quality;
        //    bool meetsRecCon = CharacterInfo.Control() >= recipeTable.SuggestedControl;
        //    bool meetsRecCraft = CharacterInfo.Craftsmanship() >= recipeTable.SuggestedCraftsmanship;
        //    var q1 = baseSteps / maxQual;
        //    var q2 = CharacterInfo.MaxCP / sheetItem.QualityFactor / 1.5;
        //    var q3 = CharacterInfo.IsManipulationUnlocked() ? 2 : 1;
        //    var q4 = sheetItem.RecipeLevelTable.Value.Stars * 6;
        //    var q5 = meetsRecCon && meetsRecCraft ? 3 : 1;
        //    var q6 = Math.Floor((q1 * 100) + (q2 * 3 * q3 * q5) - q4 + simulatedPercent);
        //    var chance = q6 > 100 ? 100 : q6;
        //    chance = chance < 0 ? 0 : chance;

        //    return chance switch
        //    {
        //        < 20 => "EHQ: Do not attempt.",
        //        < 40 => "EHQ: Very low chance.",
        //        < 60 => "EHQ: Average chance.",
        //        < 80 => "EHQ: Good chance.",
        //        < 90 => "EHQ: High chance.",
        //        < 100 => "EHQ: Very high chance.",
        //        _ => "EHQ: Guaranteed.",
        //    };
        //}

        public static void DrawMainWindow()
        {
            ImGui.TextWrapped($"Here you can change some settings Artisan will use. Some of these can also be toggled during a craft.");
            ImGui.TextWrapped($"In order to use Artisan's manual highlight, please slot every crafting action you have unlocked to a visible hotbar.");
            bool autoEnabled = Service.Configuration.AutoMode;
            bool delayRec = Service.Configuration.DelayRecommendation;
            bool failureCheck = Service.Configuration.DisableFailurePrediction;
            int maxQuality = Service.Configuration.MaxPercentage;
            bool useTricksGood = Service.Configuration.UseTricksGood;
            bool useTricksExcellent = Service.Configuration.UseTricksExcellent;
            bool useSpecialist = Service.Configuration.UseSpecialist;
            //bool showEHQ = Service.Configuration.ShowEHQ;
            //bool useSimulated = Service.Configuration.UseSimulatedStartingQuality;
            bool useMacroMode = Service.Configuration.UseMacroMode;
            bool disableGlow = Service.Configuration.DisableHighlightedAction;
            bool disableToasts = Service.Configuration.DisableToasts;
            bool disableMini = Service.Configuration.DisableMiniMenu;

            //ImGui.Separator();
            if (ImGui.CollapsingHeader("Mode Selections"))
            {
                if (ImGui.Checkbox("Auto Mode Enabled", ref autoEnabled))
                {
                    Service.Configuration.AutoMode = autoEnabled;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker($"Automatically use each recommended action.");
                if (autoEnabled)
                {
                    var delay = Service.Configuration.AutoDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("Set delay (ms)###ActionDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        Service.Configuration.AutoDelay = delay;
                        Service.Configuration.Save();
                    }
                }

                if (Service.Configuration.UserMacros.Count > 0)
                {
                    if (ImGui.Checkbox("Macro Mode Enabled", ref useMacroMode))
                    {
                        Service.Configuration.UseMacroMode = useMacroMode;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker($"Use a macro to craft instead of Artisan making its own decisions.\r\n" +
                        $"Priority is individual recipe macros followed by the selected macro below.\r\n" +
                        $"If you wish to only use individual recipe macros then leave below unset.\r\n" +
                        $"If the macro ends before a craft is complete, Artisan will make its own suggestions until the end of the craft.");

                    if (useMacroMode)
                    {
                        string preview = Service.Configuration.SetMacro == null ? "" : Service.Configuration.SetMacro.Name!;
                        if (ImGui.BeginCombo("Select Macro", preview))
                        {
                            if (ImGui.Selectable(""))
                            {
                                Service.Configuration.SetMacro = null;
                                Service.Configuration.Save();
                            }
                            foreach (var macro in Service.Configuration.UserMacros)
                            {
                                bool selected = Service.Configuration.SetMacro == null ? false : Service.Configuration.SetMacro.ID == macro.ID;
                                if (ImGui.Selectable(macro.Name, selected))
                                {
                                    Service.Configuration.SetMacro = macro;
                                    Service.Configuration.Save();
                                }
                            }

                            ImGui.EndCombo();
                        }
                    }
                }
                else
                {
                    useMacroMode = false;
                }
            }

            if (ImGui.CollapsingHeader("Execution Settings"))
            {
                if (ImGui.Checkbox("Delay Getting Recommendations", ref delayRec))
                {
                    Service.Configuration.DelayRecommendation = delayRec;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("Use this if you're having issues with Final Appraisal not triggering when it's supposed to.");

                if (delayRec)
                {
                    var delay = Service.Configuration.RecommendationDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("Set delay (ms)###RecommendationDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        Service.Configuration.RecommendationDelay = delay;
                        Service.Configuration.Save();
                    }
                }

                if (ImGui.Checkbox($"Use {LuminaSheets.CraftActions[Skills.Tricks].Name} - {LuminaSheets.AddonSheet[227].Text.RawString}", ref useTricksGood))
                {
                    Service.Configuration.UseTricksGood = useTricksGood;
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"Use {LuminaSheets.CraftActions[Skills.Tricks].Name} - {LuminaSheets.AddonSheet[228].Text.RawString}", ref useTricksExcellent))
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

                //bool useExperimental = Service.Configuration.UseExperminentalRotation;
                //if (ImGui.Checkbox("Use Experimental Rotation (non-expert crafts)", ref useExperimental))
                //{
                //    Service.Configuration.UseExperminentalRotation = useExperimental;
                //    Service.Configuration.Save();
                //}
                //ImGuiComponents.HelpMarker($"This is a new experimental rotation which currently doesn't work with many settings. It also hasn't been tweaked for lower level use, so your mileage may vary.");
            }

            if (ImGui.CollapsingHeader("UI Settings"))
            {
                if (ImGui.Checkbox("Disable highlighting box", ref disableGlow))
                {
                    Service.Configuration.DisableHighlightedAction = disableGlow;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("This is the box that highlights the actions on your hotbars for manual play.");

                if (ImGui.Checkbox($"Disable recommendation toasts", ref disableToasts))
                {
                    Service.Configuration.DisableToasts = disableToasts;
                    Service.Configuration.Save();
                }

                ImGuiComponents.HelpMarker("These are the pop-ups whenever a new action is recommended.");

                if (ImGui.Checkbox("Disable Recipe List mini-menu", ref disableMini))
                {
                    Service.Configuration.DisableMiniMenu = disableMini;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("Hides the mini-menu for config settings in the recipe list. Still shows individual macro menu.");

                bool lockMini = Service.Configuration.LockMiniMenu;
                if (ImGui.Checkbox("Keep Recipe List mini-menu position attached to Recipe List.", ref lockMini))
                {
                    Service.Configuration.LockMiniMenu = lockMini;
                    Service.Configuration.Save();
                }
                if (ImGui.Button("Reset Recipe List mini-menu position"))
                {
                    AtkResNodeFunctions.ResetPosition = true;
                }

                bool hideQuestHelper = Service.Configuration.HideQuestHelper;
                if (ImGui.Checkbox($"Hide Quest Helper", ref hideQuestHelper))
                {
                    Service.Configuration.HideQuestHelper = hideQuestHelper;
                    Service.Configuration.Save();
                }

                bool hideTheme = Service.Configuration.DisableTheme;
                if (ImGui.Checkbox("Disable Custom Theme", ref hideTheme))
                {
                    Service.Configuration.DisableTheme = hideTheme;
                    Service.Configuration.Save();
                }

            }
        }
    }

    public enum OpenWindow
    {
        None = 0,
        Main = 1,
        Endurance = 2,
        Macro = 3,
        Lists = 4,
        About = 5,
        Debug = 6
    }
}
