using Artisan.RawInformation;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using static Artisan.CraftingLogic.CurrentCraft;

namespace Artisan.MacroSystem
{
    internal class MacroUI
    {
        private static string _newMacroName = string.Empty;
        private static string renameMacro = string.Empty;
        private static bool _keyboardFocus;
        private const string MacroNamePopupLabel = "Macro Name";
        private static Macro selectedMacro = new();
        private static int selectedActionIndex = -1;
        private static bool renameMode = false;
        private static bool Minimized = false;
        private static bool Raweditor = false;
        private static string _rawMacro = string.Empty;

        internal static void Draw()
        {
            ImGui.TextWrapped("This tab will allow you to add macros that Artisan can use instead of its own decisions.");
            ImGui.Separator();

            if (Minimized)
            {
                if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowRight, "Maximize", new Vector2(80f, 0)))
                {
                    Minimized = false;
                }
                ImGui.Spacing();
            }

            if (!Minimized)
            {
                ImGui.Spacing();
                if (ImGui.Button("Import Macro From Clipboard"))
                    OpenMacroNamePopup(MacroNameUse.FromClipboard);

                if (ImGui.Button("New Macro"))
                    OpenMacroNamePopup(MacroNameUse.NewMacro);

                DrawMacroNamePopup(MacroNameUse.FromClipboard);
                DrawMacroNamePopup(MacroNameUse.NewMacro);
            }

            if (Service.Configuration.UserMacros.Count > 0)
            {
                ImGui.BeginGroup();
                float longestName = 0;
                foreach (var macro in Service.Configuration.UserMacros)
                {
                    if (ImGui.CalcTextSize($"{macro.Name} (CP Cost: {GetCPCost(macro)})").Length() > longestName)
                        longestName = ImGui.CalcTextSize($"{macro.Name} (CP Cost: {GetCPCost(macro)})").Length();

                    if (macro.MacroStepOptions.Count == 0 && macro.MacroActions.Count > 0)
                    {
                        for (int i = 0; i < macro.MacroActions.Count; i++)
                        {
                            macro.MacroStepOptions.Add(new());
                        }
                    }
                }

                longestName = Math.Max(150, longestName);
                if (!Minimized)
                {
                    if (ImGui.BeginChild("##selector", new Vector2(longestName + 40, 0), true))
                    {
                        if (ImGuiEx.IconButton(FontAwesomeIcon.ArrowLeft, "MinimizeButton", new Vector2(longestName + 20, 0)))
                        {
                            Minimized = true;
                        }
                        ImGui.Separator();

                        foreach (Macro m in Service.Configuration.UserMacros)
                        {
                            uint cpCost = GetCPCost(m);
                            var selected = ImGui.Selectable($"{m.Name} (CP Cost: {cpCost})###{m.ID}", m.ID == selectedMacro.ID);

                            if (selected)
                            {
                                selectedMacro = m;
                                _rawMacro = string.Join("\r\n", m.MacroActions.Select(x => $"{x.NameOfAction()}"));
                            }
                        }
                        
                    }
                    ImGui.EndChild();
                }

                if (selectedMacro.ID != 0)
                {
                    if (selectedMacro.MacroStepOptions.Count == 0 && selectedMacro.MacroActions.Count > 0)
                    {
                        for (int i = 0; i < selectedMacro.MacroActions.Count; i++)
                        {
                            selectedMacro.MacroStepOptions.Add(new());
                        }
                    }

                    if (!Minimized)
                    ImGui.SameLine();
                    ImGui.BeginChild("###selectedMacro", new Vector2(0, 0), false);
                    if (!renameMode)
                    {
                        ImGui.Text($"Selected Macro: {selectedMacro.Name}");
                        ImGui.SameLine();
                        if (ImGuiComponents.IconButton(FontAwesomeIcon.Pen))
                        {
                            renameMode = true;
                        }
                    }
                    else
                    {
                        renameMacro = selectedMacro.Name!;
                        if (ImGui.InputText("", ref renameMacro, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            selectedMacro.Name = renameMacro;
                            Service.Configuration.Save();

                            renameMode = false;
                            renameMacro = String.Empty;
                        }
                    }
                    if (ImGui.Button("Delete Macro (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                    {
                        Service.Configuration.UserMacros.Remove(selectedMacro);
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = null;

                        Service.Configuration.Save();
                        selectedMacro = new();
                        selectedActionIndex = -1;

                        CleanUpIndividualMacros();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Raw Editor"))
                    {
                        Raweditor = !Raweditor;
                    }

                    ImGui.Spacing();
                    bool skipQuality = selectedMacro.MacroOptions.SkipQualityIfMet;
                    if (ImGui.Checkbox("Skip quality actions if at 100%", ref skipQuality))
                    {
                        selectedMacro.MacroOptions.SkipQualityIfMet = skipQuality;
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = selectedMacro;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("Once you're at 100% quality, the macro will skip over all actions relating to quality, including buffs.");
                    bool upgradeQualityActions = selectedMacro.MacroOptions.UpgradeQualityActions;
                    if (ImGui.Checkbox("Upgrade Quality Actions", ref upgradeQualityActions))
                    {
                        selectedMacro.MacroOptions.UpgradeQualityActions = upgradeQualityActions;
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = selectedMacro;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("If you get a Good or Excellent condition and your macro is on a step that increases quality (not including Byregot's Blessing) then it will upgrade the action to Precise Touch.");
                    ImGui.SameLine();

                    bool upgradeProgressActions = selectedMacro.MacroOptions.UpgradeProgressActions;
                    if (ImGui.Checkbox("Upgrade Progress Actions", ref upgradeProgressActions))
                    {
                        selectedMacro.MacroOptions.UpgradeProgressActions = upgradeProgressActions;
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = selectedMacro;
                        Service.Configuration.Save();
                    }
                    ImGuiComponents.HelpMarker("If you get a Good or Excellent condition and your macro is on a step that increases progress then it will upgrade the action to Intensive Synthesis.");

                    bool skipObserves = selectedMacro.MacroOptions.SkipObservesIfNotPoor;
                    if (ImGui.Checkbox("Skip Observes If Not Poor", ref skipObserves))
                    {
                        selectedMacro.MacroOptions.SkipObservesIfNotPoor = skipObserves;
                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                            Service.Configuration.SetMacro = selectedMacro;
                        Service.Configuration.Save();
                    }

                    if (!Raweditor)
                    {
                        ImGui.Columns(2, "actionColumns", false);
                        if (ImGui.Button("Insert New Action"))
                        {
                            if (selectedMacro.MacroActions.Count == 0)
                            {
                                selectedMacro.MacroActions.Add(Skills.BasicSynth);
                                selectedMacro.MacroStepOptions.Add(new());
                            }
                            else
                            {
                                selectedMacro.MacroActions.Insert(selectedActionIndex + 1, Skills.BasicSynth);
                                selectedMacro.MacroStepOptions.Insert(selectedActionIndex + 1, new());
                            }

                            Service.Configuration.Save();
                        }
                        ImGui.TextWrapped("Macro Actions");
                        ImGui.Indent();
                        for (int i = 0; i < selectedMacro.MacroActions.Count(); i++)
                        {
                            var selectedAction = ImGui.Selectable($"{i + 1}. {(selectedMacro.MacroActions[i] == 0 ? $"Artisan Recommendation###selectedAction{i}" : GetActionName(selectedMacro.MacroActions[i]))}###selectedAction{i}", i == selectedActionIndex);

                            if (selectedAction)
                                selectedActionIndex = i;
                        }
                        ImGui.Unindent();
                        if (selectedActionIndex != -1)
                        {
                            if (selectedActionIndex >= selectedMacro.MacroActions.Count)
                                return;

                            ImGui.NextColumn();
                            ImGui.Text($"Selected Action: {(selectedMacro.MacroActions[selectedActionIndex] == 0 ? "Artisan Recommendation" : GetActionName(selectedMacro.MacroActions[selectedActionIndex]))}");
                            if (selectedActionIndex > 0)
                            {
                                ImGui.SameLine();
                                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowLeft))
                                {
                                    selectedActionIndex--;
                                }
                            }

                            if (selectedActionIndex < selectedMacro.MacroActions.Count - 1)
                            {
                                ImGui.SameLine();
                                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowRight))
                                {
                                    selectedActionIndex++;
                                }
                            }

                            bool skip = selectedMacro.MacroStepOptions[selectedActionIndex].ExcludeFromUpgrade;
                            if (ImGui.Checkbox($"Skip Upgrades For This Action", ref skip))
                            {
                                selectedMacro.MacroStepOptions[selectedActionIndex].ExcludeFromUpgrade = skip;
                                Service.Configuration.Save();
                            }

                            if (ImGui.Button("Delete Action (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
                            {
                                selectedMacro.MacroActions.RemoveAt(selectedActionIndex);
                                selectedMacro.MacroStepOptions.RemoveAt(selectedActionIndex);

                                Service.Configuration.Save();

                                if (selectedActionIndex == selectedMacro.MacroActions.Count)
                                    selectedActionIndex--;
                            }

                            if (ImGui.BeginCombo("###ReplaceAction", "Replace Action"))
                            {
                                if (ImGui.Selectable($"Artisan Recommendation"))
                                {
                                    selectedMacro.MacroActions[selectedActionIndex] = 0;
                                    if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                        Service.Configuration.SetMacro = selectedMacro;

                                    Service.Configuration.Save();
                                }

                                foreach (var constant in typeof(Skills).GetFields().OrderBy(x => GetActionName((uint)x.GetValue(null)!)))
                                {
                                    if (ImGui.Selectable($"{GetActionName((uint)constant.GetValue(null)!)}"))
                                    {
                                        selectedMacro.MacroActions[selectedActionIndex] = (uint)constant.GetValue(null)!;
                                        if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                            Service.Configuration.SetMacro = selectedMacro;

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
                                    selectedMacro.MacroActions.Reverse(selectedActionIndex - 1, 2);
                                    selectedMacro.MacroStepOptions.Reverse(selectedActionIndex - 1, 2);
                                    selectedActionIndex--;
                                    if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                        Service.Configuration.SetMacro = selectedMacro;

                                    Service.Configuration.Save();
                                }
                            }

                            if (selectedActionIndex < selectedMacro.MacroActions.Count - 1)
                            {
                                ImGui.SameLine();
                                if (selectedActionIndex == 0)
                                {
                                    ImGui.Dummy(new Vector2(22));
                                    ImGui.SameLine();
                                }

                                if (ImGuiComponents.IconButton(FontAwesomeIcon.ArrowDown))
                                {
                                    selectedMacro.MacroActions.Reverse(selectedActionIndex, 2);
                                    selectedMacro.MacroStepOptions.Reverse(selectedActionIndex, 2);
                                    selectedActionIndex++;
                                    if (Service.Configuration.SetMacro?.ID == selectedMacro.ID)
                                        Service.Configuration.SetMacro = selectedMacro;

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
                            ParseMacro(_rawMacro, out Macro updated);
                            if (updated.ID != 0 && !selectedMacro.MacroActions.SequenceEqual(updated.MacroActions))
                            {
                                selectedMacro.MacroActions = updated.MacroActions;
                                selectedMacro.MacroStepOptions = updated.MacroStepOptions;
                                Service.Configuration.Save();

                                DuoLog.Information($"Macro Updated");
                            }
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Save and Close"))
                        {
                            ParseMacro(_rawMacro, out Macro updated);
                            if (updated.ID != 0 && !selectedMacro.MacroActions.SequenceEqual(updated.MacroActions))
                            {
                                selectedMacro.MacroActions = updated.MacroActions;
                                selectedMacro.MacroStepOptions = updated.MacroStepOptions;
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
                        ImGuiEx.Text($"Artisan: {GetMacroLength(selectedMacro)} seconds");
                    });
                    ImGuiEx.ImGuiLineCentered("MTimeTeamcraft", delegate
                    {
                        ImGuiEx.Text($"Normal Macro: {GetTeamcraftMacroLength(selectedMacro)} seconds");
                    });
                    ImGui.EndChild();
                }
                else
                {
                    selectedActionIndex = -1;
                }

                ImGui.EndGroup();
            }
            else
            {
                selectedMacro = new();
                selectedActionIndex = -1;
            }
        }

        private static uint GetCPCost(Macro m)
        {
            uint previousAction = 0;
            uint output = 0;
            foreach (var act in m.MacroActions)
            {
                if ((act == Skills.StandardTouch && previousAction == Skills.BasicTouch) || (act == Skills.AdvancedTouch && previousAction == Skills.StandardTouch))
                {
                    output += 18;
                    previousAction = act;
                    continue;
                }

                if (act >= 100000)
                {
                    output += LuminaSheets.CraftActions[act].Cost;
                }
                else
                {
                    output += LuminaSheets.ActionSheet[act].PrimaryCostValue;
                }

                previousAction = act;
            }

            return output;
        }

        public static double GetMacroLength(Macro m)
        {
            double output = 0;
            var delay = (double)Service.Configuration.AutoDelay + (Service.Configuration.DelayRecommendation ? Service.Configuration.RecommendationDelay : 0);
            var delaySeconds = delay / 1000;

            foreach (var act in m.MacroActions)
            {
                if (ActionIsLengthyAnimation(act))
                {
                    output += 2.5 + delaySeconds;
                }
                else
                {
                    output += 1.25 + delaySeconds;
                }
            }

            return Math.Round(output, 2);

        }

        private static float GetTeamcraftMacroLength(Macro m)
        {
            float output = 0;
            foreach (var act in m.MacroActions)
            {
                if (ActionIsLengthyAnimation(act))
                {
                    output += 3f;
                }
                else
                {
                    output += 2f;
                }
            }

            return output;

        }
        private static bool ActionIsLengthyAnimation(uint id)
        {
            switch (id)
            {
                case Skills.BasicSynth:
                case Skills.RapidSynthesis:
                case Skills.MuscleMemory:
                case Skills.CarefulSynthesis:
                case Skills.FocusedSynthesis:
                case Skills.Groundwork:
                case Skills.DelicateSynthesis:
                case Skills.IntensiveSynthesis:
                case Skills.PrudentSynthesis:
                case Skills.BasicTouch:
                case Skills.HastyTouch:
                case Skills.StandardTouch:
                case Skills.PreciseTouch:
                case Skills.PrudentTouch:
                case Skills.FocusedTouch:
                case Skills.Reflect:
                case Skills.PreparatoryTouch:
                case Skills.AdvancedTouch:
                case Skills.TrainedFinesse:
                case Skills.ByregotsBlessing:
                case Skills.MastersMend:
                    return true;
                    default:
                    return false;
            };
        }


        private static string GetActionName(uint action)
        {
            if (LuminaSheets.CraftActions.TryGetValue(action, out var act1))
            {
                return act1.Name.RawString;
            }
            else
            {
                LuminaSheets.ActionSheet.TryGetValue(action, out var act2);
                return act2.Name.RawString;
            }
        }

        private static void DrawMacroNamePopup(MacroNameUse use)
        {
            if (ImGui.BeginPopup($"{MacroNamePopupLabel}{use}"))
            {
                if (_keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    _keyboardFocus = false;
                }

                if (ImGui.InputText("Macro Name##macroName", ref _newMacroName, 64, ImGuiInputTextFlags.EnterReturnsTrue)
                 && _newMacroName.Any())
                {
                    switch (use)
                    {
                        case MacroNameUse.NewMacro:
                            Macro newMacro = new();
                            newMacro.Name = _newMacroName;
                            newMacro.SetID();
                            newMacro.Save(true);
                            break;
                        case MacroNameUse.FromClipboard:
                            try
                            {
                                var text = ImGui.GetClipboardText();
                                ParseMacro(text, out Macro macro);
                                if (macro.ID != 0)
                                    if (macro.Save())
                                    {
                                        Service.ChatGui.Print($"{macro.Name} has been saved.");
                                    }
                                    else
                                    {
                                        Service.ChatGui.PrintError("Unable to save macro. Please check your clipboard contains a working macro with actions.");
                                    }
                                else
                                    Service.ChatGui.PrintError("Unable to parse clipboard. Please check your clipboard contains a working macro with actions.");
                            }
                            catch (Exception e)
                            {
                                Dalamud.Logging.PluginLog.Information($"Could not save new Macro from Clipboard:\n{e}");
                            }

                            break;
                    }

                    _newMacroName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private static void ParseMacro(string text, out Macro macro)
        {
            macro = new();
            macro.Name = _newMacroName;
            if (string.IsNullOrWhiteSpace(text)) 
            {
                macro.ID = 1;
                return;
            };

            using (System.IO.StringReader reader = new System.IO.StringReader(text))
            {
                string line = "";
                while ((line = reader.ReadLine()!) != null)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 1) continue;

                    if (parts[0].Equals("/ac", StringComparison.CurrentCultureIgnoreCase) || parts[0].Equals("/action", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var builder = new StringBuilder();
                        for (int i = 1; i < parts.Length; i++)
                        {
                            if (parts[i].Contains("<")) continue;
                            builder.Append(parts[i]);
                            builder.Append(" ");
                        }
                        var action = builder.ToString().Trim();
                        action = action.Replace("\"", "");
                        if (string.IsNullOrEmpty(action)) continue;

                        if (action.Equals("Artisan Recommendation", StringComparison.CurrentCultureIgnoreCase) || action.Equals("*"))
                        {
                            macro.MacroActions.Add(0);
                            macro.MacroStepOptions.Add(new());
                            continue;
                        }

                        if (LuminaSheets.CraftActions.Values.Any(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0))
                        {
                            var act = LuminaSheets.CraftActions.Values.FirstOrDefault(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0);
                            if (act == null)
                            {
                                Service.ChatGui.PrintError($"Unable to parse action: {action}");
                                continue;
                            }
                            macro.MacroActions.Add(act.RowId);
                            macro.MacroStepOptions.Add(new());
                            continue;

                        }
                        else
                        {
                            var act = LuminaSheets.ActionSheet.Values.FirstOrDefault(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0);
                            if (act == null)
                            {
                                Service.ChatGui.PrintError($"Unable to parse action: {action}");
                                continue;
                            }
                            macro.MacroActions.Add(act.RowId);
                            macro.MacroStepOptions.Add(new());
                            continue;

                        }
                    }
                    else
                    {
                        if (parts[0].Contains("/", StringComparison.CurrentCultureIgnoreCase))
                            continue;

                        var builder = new StringBuilder();
                        for (int i = 0; i < parts.Length; i++)
                        {
                            if (parts[i].Contains("<")) continue;
                            builder.Append(parts[i]);
                            builder.Append(" ");
                        }
                        var action = builder.ToString().Trim();
                        action = action.Replace("\"", "");
                        if (string.IsNullOrEmpty(action)) continue;

                        if (action.Equals("Artisan Recommendation", StringComparison.CurrentCultureIgnoreCase) || action == "*")
                        {
                            macro.MacroActions.Add(0);
                            macro.MacroStepOptions.Add(new());
                            continue;
                        }

                        if (LuminaSheets.CraftActions.Values.Any(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0))
                        {
                            var act = LuminaSheets.CraftActions.Values.FirstOrDefault(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0);
                            if (act == null)
                            {
                                Service.ChatGui.PrintError($"Unable to parse action: {action}");
                                continue;
                            }
                            macro.MacroActions.Add(act.RowId);
                            macro.MacroStepOptions.Add(new());
                            continue;

                        }
                        else
                        {
                            var act = LuminaSheets.ActionSheet.Values.FirstOrDefault(x => x.Name.RawString.Equals(action, StringComparison.CurrentCultureIgnoreCase) && x.ClassJobCategory.Value.RowId != 0);
                            if (act == null)
                            {
                                Service.ChatGui.PrintError($"Unable to parse action: {action}");
                                continue;
                            }
                            macro.MacroActions.Add(act.RowId);
                            macro.MacroStepOptions.Add(new());
                            continue;

                        }
                    }
                }
            }
            if (macro.MacroActions.Count > 0)
                macro.SetID();
        }

        private static void OpenMacroNamePopup(MacroNameUse use)
        {
            _newMacroName = string.Empty;
            _keyboardFocus = true;
            ImGui.OpenPopup($"{MacroNamePopupLabel}{use}");
        }

        internal enum MacroNameUse
        {
            SaveCurrent,
            NewMacro,
            DuplicateMacro,
            FromClipboard,
        }
    }
}
