using Artisan.MacroSystem;
using Artisan.RawInformation.Character;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Artisan.RawInformation;

namespace Artisan.UI
{
    internal class MacroEditor : Window
    {

        private Macro? SelectedMacro;
        private bool renameMode = false;
        private string renameMacro = "";
        private int selectedActionIndex = 0;
        private bool Raweditor = false;
        private static string _rawMacro = string.Empty;

        public MacroEditor(int macroId) : base($"Macro Editor###{macroId}", ImGuiWindowFlags.None)
        {
            SelectedMacro = P.config.UserMacros.FirstOrDefault(x => x.ID == macroId);
            this.IsOpen = true;
            P.ws.AddWindow(this);
            this.Size = new Vector2(600, 600);
            this.SizeCondition = ImGuiCond.Appearing;
            ShowCloseButton = true;
        }

        public override void PreDraw()
        {
            if (!P.config.DisableTheme)
            {
                P.Style.Push();
                ImGui.PushFont(P.CustomFont);
                P.StylePushed = true;
            }

        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                ImGui.PopFont();
                P.StylePushed = false;
            }
        }

        public override void OnClose()
        {
            base.OnClose();
            P.ws.RemoveWindow(this);
        }

        public override void Draw()
        {
            if (SelectedMacro.ID != 0)
            {
                if (SelectedMacro.MacroStepOptions.Count == 0 && SelectedMacro.MacroActions.Count > 0)
                {
                    for (int i = 0; i < SelectedMacro.MacroActions.Count; i++)
                    {
                        SelectedMacro.MacroStepOptions.Add(new());
                    }
                }

                if (!renameMode)
                {
                    ImGui.Text($"Selected Macro: {SelectedMacro.Name}");
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
                        Service.Configuration.Save();

                        renameMode = false;
                        renameMacro = String.Empty;
                    }
                }
                if (ImGui.Button("Delete Macro (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                {
                    Service.Configuration.UserMacros.Remove(SelectedMacro);
                    Service.Configuration.Save();
                    SelectedMacro = new();
                    selectedActionIndex = -1;

                    CleanUpIndividualMacros();
                    this.IsOpen = false;
                }
                ImGui.SameLine();
                if (ImGui.Button("Raw Editor"))
                {
                    _rawMacro = string.Join("\r\n", SelectedMacro.MacroActions.Select(x => $"{x.NameOfAction()}"));
                    Raweditor = !Raweditor;
                }

                ImGui.Spacing();
                bool skipQuality = SelectedMacro.MacroOptions.SkipQualityIfMet;
                if (ImGui.Checkbox("Skip quality actions if at 100%", ref skipQuality))
                {
                    SelectedMacro.MacroOptions.SkipQualityIfMet = skipQuality;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("Once you're at 100% quality, the macro will skip over all actions relating to quality, including buffs.");
                ImGui.SameLine();
                bool skipObserves = SelectedMacro.MacroOptions.SkipObservesIfNotPoor;
                if (ImGui.Checkbox("Skip Observes If Not Poor", ref skipObserves))
                {
                    SelectedMacro.MacroOptions.SkipObservesIfNotPoor = skipObserves;
                    Service.Configuration.Save();
                }


                bool upgradeQualityActions = SelectedMacro.MacroOptions.UpgradeQualityActions;
                if (ImGui.Checkbox("Upgrade Quality Actions", ref upgradeQualityActions))
                {
                    SelectedMacro.MacroOptions.UpgradeQualityActions = upgradeQualityActions;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("If you get a Good or Excellent condition and your macro is on a step that increases quality (not including Byregot's Blessing) then it will upgrade the action to Precise Touch.");
                ImGui.SameLine();

                bool upgradeProgressActions = SelectedMacro.MacroOptions.UpgradeProgressActions;
                if (ImGui.Checkbox("Upgrade Progress Actions", ref upgradeProgressActions))
                {
                    SelectedMacro.MacroOptions.UpgradeProgressActions = upgradeProgressActions;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("If you get a Good or Excellent condition and your macro is on a step that increases progress then it will upgrade the action to Intensive Synthesis.");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("Minimum Craftsmanship", ref SelectedMacro.MacroOptions.MinCraftsmanship))
                    Service.Configuration.Save();
                ImGuiComponents.HelpMarker("Artisan will not start crafting if you do not meet this minimum craftsmanship with this macro selected.");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("Minimum Control", ref SelectedMacro.MacroOptions.MinControl))
                    Service.Configuration.Save();
                ImGuiComponents.HelpMarker("Artisan will not start crafting if you do not meet this minimum control with this macro selected.");

                ImGui.PushItemWidth(150f);
                if (ImGui.InputInt("Minimum CP", ref SelectedMacro.MacroOptions.MinCP))
                    Service.Configuration.Save();
                ImGuiComponents.HelpMarker("Artisan will not start crafting if you do not meet this minimum CP with this macro selected.");

                if (!Raweditor)
                {
                    if (ImGui.Button($"Insert New Action ({Skills.BasicSynth.NameOfAction()})"))
                    {
                        if (SelectedMacro.MacroActions.Count == 0)
                        {
                            SelectedMacro.MacroActions.Add(Skills.BasicSynth);
                            SelectedMacro.MacroStepOptions.Add(new());
                        }
                        else
                        {
                            SelectedMacro.MacroActions.Insert(selectedActionIndex + 1, Skills.BasicSynth);
                            SelectedMacro.MacroStepOptions.Insert(selectedActionIndex + 1, new());
                        }

                        Service.Configuration.Save();
                    }

                    if (SelectedMacro.MacroActions.Count > 0 && selectedActionIndex != -1)
                    {
                        if (ImGui.Button($"Insert New Action - Same As Previous ({SelectedMacro.MacroActions[selectedActionIndex].NameOfAction()})"))
                        {
                            SelectedMacro.MacroActions.Insert(selectedActionIndex + 1, SelectedMacro.MacroActions[selectedActionIndex]);
                            SelectedMacro.MacroStepOptions.Insert(selectedActionIndex + 1, new());
                        }
                    }
                    

                    ImGui.Columns(2, "actionColumns", true);
                    ImGui.SetColumnWidth(0, 220f.Scale());
                    ImGuiEx.ImGuiLineCentered("###MacroActions", () => ImGuiEx.TextUnderlined("Macro Actions"));
                    ImGui.Indent();
                    for (int i = 0; i < SelectedMacro.MacroActions.Count(); i++)
                    {
                        var selectedAction = ImGui.Selectable($"{i + 1}. {(SelectedMacro.MacroActions[i] == 0 ? $"Artisan Recommendation###selectedAction{i}" : $"{SelectedMacro.MacroActions[i].NameOfAction()}###selectedAction{i}")}", i == selectedActionIndex);

                        if (selectedAction)
                            selectedActionIndex = i;
                    }
                    ImGui.Unindent();
                    if (selectedActionIndex != -1)
                    {
                        if (selectedActionIndex >= SelectedMacro.MacroActions.Count)
                            return;

                        var macroStepOpts = SelectedMacro.MacroStepOptions[selectedActionIndex];

                        ImGui.NextColumn();
                        ImGuiEx.CenterColumnText($"Selected Action: {(SelectedMacro.MacroActions[selectedActionIndex] == 0 ? "Artisan Recommendation" : SelectedMacro.MacroActions[selectedActionIndex].NameOfAction())}", true);
                        if (selectedActionIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft))
                            {
                                selectedActionIndex--;
                            }
                        }

                        if (selectedActionIndex < SelectedMacro.MacroActions.Count - 1)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowRight))
                            {
                                selectedActionIndex++;
                            }
                        }

                        ImGui.Dummy(new Vector2(0, 0));
                        ImGui.SameLine();
                        bool skip = SelectedMacro.MacroStepOptions[selectedActionIndex].ExcludeFromUpgrade;
                        if (ImGui.Checkbox($"Skip Upgrades For This Action", ref skip))
                        {
                            SelectedMacro.MacroStepOptions[selectedActionIndex].ExcludeFromUpgrade = skip;
                            Service.Configuration.Save();
                        }

                        ImGui.Spacing();
                        ImGuiEx.CenterColumnText($"Skip on these conditions", true);

                        ImGui.BeginChild("ConditionalExcludes", new Vector2(ImGui.GetContentRegionAvail().X, 100f), false, ImGuiWindowFlags.AlwaysAutoResize);
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, 0));
                        ImGui.Columns(3, null, false);
                        if (ImGui.Checkbox($"Normal", ref macroStepOpts.ExcludeNormal))
                            Service.Configuration.Save();
                        if (ImGui.Checkbox($"Poor", ref macroStepOpts.ExcludePoor))
                            Service.Configuration.Save();
                        if (ImGui.Checkbox($"Good", ref macroStepOpts.ExcludeGood))
                            Service.Configuration.Save();
                        if (ImGui.Checkbox($"Excellent", ref macroStepOpts.ExcludeExcellent))
                            Service.Configuration.Save();

                        ImGui.NextColumn();

                        if (ImGui.Checkbox($"Centered", ref macroStepOpts.ExcludeCentered))
                            Service.Configuration.Save();
                        if (ImGui.Checkbox($"Sturdy", ref macroStepOpts.ExcludeSturdy))
                            Service.Configuration.Save();
                        if (ImGui.Checkbox($"Pliant", ref macroStepOpts.ExcludePliant))
                            Service.Configuration.Save();
                        if (ImGui.Checkbox($"Malleable", ref macroStepOpts.ExcludeMalleable))
                            Service.Configuration.Save();

                        ImGui.NextColumn();

                        if (ImGui.Checkbox($"Primed", ref macroStepOpts.ExcludePrimed))
                            Service.Configuration.Save();
                        if (ImGui.Checkbox($"Good Omen", ref macroStepOpts.ExcludeGoodOmen))
                            Service.Configuration.Save();

                        ImGui.Columns(1);
                        ImGui.PopStyleVar();
                        ImGui.EndChild();   
                        if (ImGui.Button("Delete Action (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                        {
                            SelectedMacro.MacroActions.RemoveAt(selectedActionIndex);
                            SelectedMacro.MacroStepOptions.RemoveAt(selectedActionIndex);

                            Service.Configuration.Save();

                            if (selectedActionIndex == SelectedMacro.MacroActions.Count)
                                selectedActionIndex--;
                        }

                        if (ImGui.BeginCombo("###ReplaceAction", "Replace Action"))
                        {
                            if (ImGui.Selectable($"Artisan Recommendation"))
                            {
                                SelectedMacro.MacroActions[selectedActionIndex] = 0;

                                Service.Configuration.Save();
                            }

                            foreach (var constant in typeof(Skills).GetFields().OrderBy(x => ((uint)x.GetValue(null)!).NameOfAction()))
                            {
                                if (ImGui.Selectable($"{((uint)constant.GetValue(null)!).NameOfAction()}"))
                                {
                                    SelectedMacro.MacroActions[selectedActionIndex] = (uint)constant.GetValue(null)!;

                                    Service.Configuration.Save();
                                }
                            }

                            ImGui.EndCombo();
                        }

                        ImGui.Text("Re-order Action");
                        if (selectedActionIndex > 0)
                        {
                            ImGui.SameLine();
                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowUp))
                            {
                                SelectedMacro.MacroActions.Reverse(selectedActionIndex - 1, 2);
                                SelectedMacro.MacroStepOptions.Reverse(selectedActionIndex - 1, 2);
                                selectedActionIndex--;

                                Service.Configuration.Save();
                            }
                        }

                        if (selectedActionIndex < SelectedMacro.MacroActions.Count - 1)
                        {
                            ImGui.SameLine();
                            if (selectedActionIndex == 0)
                            {
                                ImGui.Dummy(new Vector2(22));
                                ImGui.SameLine();
                            }

                            if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                            {
                                SelectedMacro.MacroActions.Reverse(selectedActionIndex, 2);
                                SelectedMacro.MacroStepOptions.Reverse(selectedActionIndex, 2);
                                selectedActionIndex++;

                                Service.Configuration.Save();
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
                        MacroUI.ParseMacro(_rawMacro, out Macro updated);
                        if (updated.ID != 0 && !SelectedMacro.MacroActions.SequenceEqual(updated.MacroActions))
                        {
                            SelectedMacro.MacroActions = updated.MacroActions;
                            SelectedMacro.MacroStepOptions = updated.MacroStepOptions;
                            Service.Configuration.Save();

                            DuoLog.Information($"Macro Updated");
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Save and Close"))
                    {
                        MacroUI.ParseMacro(_rawMacro, out Macro updated);
                        if (updated.ID != 0 && !SelectedMacro.MacroActions.SequenceEqual(updated.MacroActions))
                        {
                            SelectedMacro.MacroActions = updated.MacroActions;
                            SelectedMacro.MacroStepOptions = updated.MacroStepOptions;
                            Service.Configuration.Save();

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
                selectedActionIndex = -1;
            }
        }
    }
}
