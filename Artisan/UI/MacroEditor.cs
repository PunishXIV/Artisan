using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Numerics;

namespace Artisan.UI
{
    internal class MacroEditor : Window
    {
        private MacroSolverSettings.Macro SelectedMacro;
        private bool renameMode = false;
        private string renameMacro = "";
        private int selectedStepIndex = -1;
        private bool Raweditor = false;
        private static string _rawMacro = string.Empty;
        private bool raphael_cache = false;

        public MacroEditor(MacroSolverSettings.Macro macro, bool raphael_cache = false) : base($"Macro Editor###{macro.ID}", ImGuiWindowFlags.None)
        {
            this.raphael_cache = raphael_cache;
            SelectedMacro = macro;
            selectedStepIndex = macro.Steps.Count - 1;
            this.IsOpen = true;
            P.ws.AddWindow(this);
            this.Size = new Vector2(600, 600);
            this.SizeCondition = ImGuiCond.Appearing;
            ShowCloseButton = true;

            Crafting.CraftStarted += OnCraftStarted;
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
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

        public override void OnClose()
        {
            Crafting.CraftStarted -= OnCraftStarted;
            base.OnClose();
            P.ws.RemoveWindow(this);
        }

        public override void Draw()
        {
            if (SelectedMacro.ID != 0)
            {
                if (!renameMode)
                {
                    ImGui.TextUnformatted($"Selected Macro: {SelectedMacro.Name}");
                    ImGui.SameLine();
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Pen))
                    {
                        renameMode = true;
                    }
                }
                else
                {
                    renameMacro = SelectedMacro.Name!;
                    if (ImGui.InputText("", ref renameMacro, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        SelectedMacro.Name = renameMacro;
                        P.Config.Save();

                        renameMode = false;
                        renameMacro = String.Empty;
                    }
                }
                if (ImGui.Button("Delete Macro (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                {
                    if (raphael_cache)
                    {
                        var copy = P.Config.RaphaelSolverCacheV2.Where(kv => kv.Value == SelectedMacro);
                        //really should be just one but is it for sure??
                        foreach (var kv in copy)
                        {
                            P.Config.RaphaelSolverCacheV2.TryRemove(kv);
                        }
                    }
                    else
                    {
                        P.Config.MacroSolverConfig.Macros.Remove(SelectedMacro);
                        foreach (var e in P.Config.RecipeConfigs)
                            if (e.Value.SolverType == typeof(MacroSolverDefinition).FullName && e.Value.SolverFlavour == SelectedMacro.ID)
                                P.Config.RecipeConfigs.Remove(e.Key); // TODO: do we want to preserve other configs?..
                    }
                    P.Config.Save();
                    SelectedMacro = new();
                    selectedStepIndex = -1;

                    this.IsOpen = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("Raw Editor"))
                {
                    _rawMacro = string.Join("\r\n", SelectedMacro.Steps.Select(x => $"{x.Action.NameOfAction()}"));
                    Raweditor = !Raweditor;
                }

                ImGui.SameLine();
                var exportButton = ImGuiHelpers.GetButtonSize("Export Macro");
                ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - exportButton.X);

                if (ImGui.Button("Export Macro###ExportButton"))
                {
                    ImGui.SetClipboardText(JsonConvert.SerializeObject(SelectedMacro));
                    Notify.Success("Macro Copied to Clipboard.");
                }

                ImGui.Spacing();
                if (ImGui.Checkbox("Skip quality actions if at 100%", ref SelectedMacro.Options.SkipQualityIfMet))
                {
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker("Once you're at 100% quality, the macro will skip over all actions relating to quality, including buffs.");
                ImGui.SameLine();
                if (ImGui.Checkbox("Skip Observes If Not Poor", ref SelectedMacro.Options.SkipObservesIfNotPoor))
                {
                    P.Config.Save();
                }


                if (ImGui.Checkbox("Upgrade Quality Actions", ref SelectedMacro.Options.UpgradeQualityActions))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("If you get a Good or Excellent condition and your macro is on a step that increases quality (not including Byregot's Blessing) then it will upgrade the action to Precise Touch.");
                ImGui.SameLine();

                if (ImGui.Checkbox("Upgrade Progress Actions", ref SelectedMacro.Options.UpgradeProgressActions))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("If you get a Good or Excellent condition and your macro is on a step that increases progress then it will upgrade the action to Intensive Synthesis.");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("Minimum Craftsmanship", ref SelectedMacro.Options.MinCraftsmanship))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("Artisan will not start crafting if you do not meet this minimum craftsmanship with this macro selected.");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("Minimum Control", ref SelectedMacro.Options.MinControl))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("Artisan will not start crafting if you do not meet this minimum control with this macro selected.");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("Minimum CP", ref SelectedMacro.Options.MinCP))
                    P.Config.Save();
                ImGuiComponents.HelpMarker("Artisan will not start crafting if you do not meet this minimum CP with this macro selected.");

                if (!Raweditor)
                {
                    if (ImGui.Button($"Insert New Action ({Skills.BasicSynthesis.NameOfAction()})"))
                    {
                        SelectedMacro.Steps.Insert(selectedStepIndex + 1, new() { Action = Skills.BasicSynthesis });
                        ++selectedStepIndex;
                        P.Config.Save();
                    }

                    if (selectedStepIndex >= 0)
                    {
                        if (ImGui.Button($"Insert New Action - Same As Previous ({SelectedMacro.Steps[selectedStepIndex].Action.NameOfAction()})"))
                        {
                            SelectedMacro.Steps.Insert(selectedStepIndex + 1, new() { Action = SelectedMacro.Steps[selectedStepIndex].Action });
                            ++selectedStepIndex;
                            P.Config.Save();
                        }
                    }


                    ImGui.Columns(2, "actionColumns", true);
                    ImGui.SetColumnWidth(0, 220f.Scale());
                    ImGuiEx.ImGuiLineCentered("###MacroActions", () => ImGuiEx.TextUnderlined("Macro Actions"));
                    ImGui.Indent();
                    for (int i = 0; i < SelectedMacro.Steps.Count; i++)
                    {
                        var step = SelectedMacro.Steps[i];
                        var selectedAction = ImGui.Selectable($"{i + 1}. {(step.Action == Skills.None ? "Artisan Recommendation" : step.Action.NameOfAction())}{(step.HasExcludeCondition ? " | " : "")}{(step.HasExcludeCondition && step.ReplaceOnExclude ? step.ReplacementAction.NameOfAction() : step.HasExcludeCondition ? "Skip" : "")}###selectedAction{i}", i == selectedStepIndex);
                        if (selectedAction)
                            selectedStepIndex = i;
                    }
                    ImGui.Unindent();
                    if (selectedStepIndex >= 0)
                    {
                        var step = SelectedMacro.Steps[selectedStepIndex];

                        ImGui.NextColumn();
                        ImGuiEx.CenterColumnText($"Selected Action: {(step.Action == Skills.None ? "Artisan Recommendation" : step.Action.NameOfAction())}", true);
                        if (selectedStepIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft))
                            {
                                selectedStepIndex--;
                            }
                        }

                        if (selectedStepIndex < SelectedMacro.Steps.Count - 1)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowRight))
                            {
                                selectedStepIndex++;
                            }
                        }

                        ImGui.Dummy(new Vector2(0, 0));
                        ImGui.SameLine();
                        if (ImGui.Checkbox($"Skip Upgrades For This Action", ref step.ExcludeFromUpgrade))
                            P.Config.Save();

                        ImGui.Spacing();
                        ImGuiEx.CenterColumnText($"Skip on these conditions", true);

                        ImGui.BeginChild("ConditionalExcludes", new Vector2(ImGui.GetContentRegionAvail().X, step.HasExcludeCondition ? 200f : 100f), false, ImGuiWindowFlags.AlwaysAutoResize);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                        ImGui.Columns(3, null, false);
                        if (ImGui.Checkbox($"Normal", ref step.ExcludeNormal))
                            P.Config.Save();
                        if (ImGui.Checkbox($"Poor", ref step.ExcludePoor))
                            P.Config.Save();
                        if (ImGui.Checkbox($"Good", ref step.ExcludeGood))
                            P.Config.Save();
                        if (ImGui.Checkbox($"Excellent", ref step.ExcludeExcellent))
                            P.Config.Save();

                        ImGui.NextColumn();

                        if (ImGui.Checkbox($"Centered", ref step.ExcludeCentered))
                            P.Config.Save();
                        if (ImGui.Checkbox($"Sturdy", ref step.ExcludeSturdy))
                            P.Config.Save();
                        if (ImGui.Checkbox($"Pliant", ref step.ExcludePliant))
                            P.Config.Save();
                        if (ImGui.Checkbox($"Malleable", ref step.ExcludeMalleable))
                            P.Config.Save();

                        ImGui.NextColumn();

                        if (ImGui.Checkbox($"Primed", ref step.ExcludePrimed))
                            P.Config.Save();
                        if (ImGui.Checkbox($"Good Omen", ref step.ExcludeGoodOmen))
                            P.Config.Save();

                        ImGui.Columns(1);
                        ImGui.PopStyleVar();

                        if (step.HasExcludeCondition)
                        {
                            ImGuiEx.CenterColumnText($"Exclude options", true);
                            if (ImGui.Checkbox($"Instead of skipping replace with:", ref step.ReplaceOnExclude))
                                P.Config.Save();

                            if (step.ReplaceOnExclude)
                            {
                                if (ImGui.BeginCombo("###Select Replacement", step.ReplacementAction.NameOfAction()))
                                {
                                    if (ImGui.Selectable($"Artisan Recommendation"))
                                    {
                                        step.ReplacementAction = Skills.None;
                                        P.Config.Save();
                                    }

                                    ImGuiComponents.HelpMarker("Uses a recommendation from the appropriate default solver, i.e Standard Recipe Solver for regular recipes, Expert Recipe Solver for expert recipes.");

                                    if (ImGui.Selectable($"Touch Combo"))
                                    {
                                        step.ReplacementAction = Skills.TouchCombo;
                                        P.Config.Save();
                                    }

                                    ImGuiComponents.HelpMarker("This will use the appropriate step of the 3-step touch combo, depending on the last action actually used. Useful if upgrading quality actions or skipping on conditions.");

                                    if (ImGui.Selectable($"Touch Combo (Refined Touch Route)"))
                                    {
                                        step.ReplacementAction = Skills.TouchComboRefined;
                                        P.Config.Save();
                                    }

                                    ImGuiComponents.HelpMarker($"Similar to the other touch combo, this will alternate between Basic Touch & Refined Touch depending on the previous action used.");

                                    ImGui.Separator();

                                    foreach (var opt in Enum.GetValues(typeof(Skills)).Cast<Skills>().OrderBy(y => y.NameOfAction()))
                                    {
                                        if (ImGui.Selectable(opt.NameOfAction()))
                                        {
                                            step.ReplacementAction = opt;
                                            P.Config.Save();
                                        }
                                    }

                                    ImGui.EndCombo();
                                }
                            }
                        }
                        ImGui.EndChild();

                        if (ImGui.Button("Delete Action (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                        {
                            SelectedMacro.Steps.RemoveAt(selectedStepIndex);
                            P.Config.Save();
                            if (selectedStepIndex == SelectedMacro.Steps.Count)
                                selectedStepIndex--;
                        }

                        if (ImGui.BeginCombo("###ReplaceAction", "Replace Action"))
                        {
                            if (ImGui.Selectable($"Artisan Recommendation"))
                            {
                                step.Action = Skills.None;
                                P.Config.Save();
                            }

                            ImGuiComponents.HelpMarker("Uses a recommendation from the appropriate default solver, i.e Standard Recipe Solver for regular recipes, Expert Recipe Solver for expert recipes.");

                            if (ImGui.Selectable($"Touch Combo"))
                            {
                                step.Action = Skills.TouchCombo;
                                P.Config.Save();
                            }

                            ImGuiComponents.HelpMarker("This will use the appropriate step of the 3-step touch combo, depending on the last action actually used. Useful if upgrading quality actions or skipping on conditions.");

                            if (ImGui.Selectable($"Touch Combo (Refined Touch Route)"))
                            {
                                step.Action = Skills.TouchComboRefined;
                                P.Config.Save();
                            }

                            ImGuiComponents.HelpMarker($"Similar to the other touch combo, this will alternate between Basic Touch & Refined Touch depending on the previous action used.");

                            ImGui.Separator();

                            foreach (var opt in Enum.GetValues(typeof(Skills)).Cast<Skills>().OrderBy(y => y.NameOfAction()))
                            {
                                if (ImGui.Selectable(opt.NameOfAction()))
                                {
                                    step.Action = opt;
                                    P.Config.Save();
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.Text("Re-order Action");
                        if (selectedStepIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                            {
                                SelectedMacro.Steps.Reverse(selectedStepIndex - 1, 2);
                                selectedStepIndex--;
                                P.Config.Save();
                            }
                        }

                        if (selectedStepIndex < SelectedMacro.Steps.Count - 1)
                        {
                            ImGui.SameLine();
                            if (selectedStepIndex == 0)
                            {
                                ImGui.Dummy(new Vector2(22));
                                ImGui.SameLine();
                            }

                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                            {
                                SelectedMacro.Steps.Reverse(selectedStepIndex, 2);
                                selectedStepIndex++;
                                P.Config.Save();
                            }
                        }

                    }
                    ImGui.Columns(1);
                }
                else
                {
                    ImGui.Text($"Macro Actions (line per action)");
                    ImGuiComponents.HelpMarker("You can either copy/paste macros directly as you would a normal game macro, or list each action on its own per line.\nFor example:\n/ac Muscle Memory\n\nis the same as\n\nMuscle Memory\n\nYou can also use * (asterisk) or 'Artisan Recommendation' to insert Artisan's recommendation as a step.");
                    ImGui.InputTextMultiline("###MacroEditor", ref _rawMacro, 10000000, new Vector2(ImGui.GetContentRegionAvail().X - 30f, ImGui.GetContentRegionAvail().Y - 30f));
                    if (ImGui.Button("Save"))
                    {
                        var steps = MacroUI.ParseMacro(_rawMacro);
                        if (steps.Count > 0 && !SelectedMacro.Steps.SequenceEqual(steps))
                        {
                            selectedStepIndex = steps.Count - 1;
                            SelectedMacro.Steps = steps;
                            P.Config.Save();
                            DuoLog.Information($"Macro Updated");
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Save and Close"))
                    {
                        var steps = MacroUI.ParseMacro(_rawMacro);
                        if (steps.Count > 0 && !SelectedMacro.Steps.SequenceEqual(steps))
                        {
                            selectedStepIndex = steps.Count - 1;
                            SelectedMacro.Steps = steps;
                            P.Config.Save();
                            DuoLog.Information($"Macro Updated");
                        }

                        Raweditor = !Raweditor;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Close"))
                    {
                        Raweditor = !Raweditor;
                    }
                }


                ImGuiEx.ImGuiLineCentered("MTimeHead", delegate
                {
                    ImGuiEx.TextUnderlined($"Estimated Macro Length");
                });
                ImGuiEx.ImGuiLineCentered("MTimeArtisan", delegate
                {
                    ImGuiEx.Text($"Artisan: {MacroUI.GetMacroLength(SelectedMacro)} seconds");
                });
                ImGuiEx.ImGuiLineCentered("MTimeTeamcraft", delegate
                {
                    ImGuiEx.Text($"Normal Macro: {MacroUI.GetTeamcraftMacroLength(SelectedMacro)} seconds");
                });
            }
            else
            {
                selectedStepIndex = -1;
            }
        }

        private void OnCraftStarted(Lumina.Excel.Sheets.Recipe recipe, CraftState craft, StepState initialStep, bool trial) => IsOpen = false;
    }
}
