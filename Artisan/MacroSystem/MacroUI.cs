using Artisan.CraftingLogic;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using ECommons.DalamudServices;
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
        private static bool _keyboardFocus;
        private const string MacroNamePopupLabel = "Macro Name";
        private static Macro selectedMacro = new();
        private static int selectedActionIndex = -1;
        private static bool renameMode = false;
        private static bool Raweditor = false;
        private static string _rawMacro = string.Empty;
        private static bool reorderMode = false;
        private static Macro selectedAssignMacro = new();

        private static int level = 1;
        private static int difficulty = 9;

        private static int maxDifficulty = LuminaSheets.RecipeLevelTableSheet.Values.Where(x => x.ClassJobLevel == level).Max(x => x.Difficulty);
        private static int minDifficulty = LuminaSheets.RecipeLevelTableSheet.Values.Where(x => x.ClassJobLevel == level).Min(x => x.Difficulty);
        private static List<int> PossibleDifficulties = new();

        private static bool CannotHQ = false;
        private static Dictionary<uint, bool> JobSelected = LuminaSheets.ClassJobSheet.Values.Where(x => x.RowId >= 8 && x.RowId <= 15).ToDictionary(x => x.RowId, x => false);
        private static Dictionary<ushort, bool> Durabilities = LuminaSheets.RecipeSheet.Values.Where(x => x.Number > 0).Select(x => (ushort)(x.RecipeLevelTable.Value.Durability * ((float)x.DurabilityFactor / 100))).Distinct().Order().ToDictionary(x => x, x => false);

        internal static void Draw()
        {
            ImGui.TextWrapped("This tab will allow you to add macros that Artisan can use instead of its own decisions. Once you create a new macro, click on it from the list below to open up the macro editor window for your macro.");
            ImGui.Separator();

            if (State == CraftingState.Crafting)
            {
                ImGui.Text($"Crafting in progress. Macro settings will be unavailable until you stop crafting.");
                return;
            }
            ImGui.Spacing();
            if (ImGui.Button("Import Macro From Clipboard"))
                OpenMacroNamePopup(MacroNameUse.FromClipboard);

            if (ImGui.Button("New Macro"))
                OpenMacroNamePopup(MacroNameUse.NewMacro);

            DrawMacroNamePopup(MacroNameUse.FromClipboard);
            DrawMacroNamePopup(MacroNameUse.NewMacro);

            ImGui.Checkbox("Reorder Mode (Click and Drag to Reorder)", ref reorderMode);

            if (reorderMode)
                ImGuiEx.CenterColumnText("Reorder Mode");
            else
                ImGuiEx.CenterColumnText("Macro Editor Select");

            if (Service.Configuration.UserMacros.Count > 0)
            {
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

                if (ImGui.BeginChild("##selector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 1.85f), true))
                {
                    for (int i = 0; i < Service.Configuration.UserMacros.Count; i++)
                    {
                        var m = Service.Configuration.UserMacros[i];
                        uint cpCost = GetCPCost(m);
                        var selected = ImGui.Selectable($"{m.Name} (CP Cost: {cpCost})###{m.ID}");

                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered() && reorderMode)
                        {

                            int i_next = i + (ImGui.GetMouseDragDelta(0).Y < 0f ? -1 : 1);
                            if (i_next >= 0 && i_next < Service.Configuration.UserMacros.Count)
                            {
                                Service.Configuration.UserMacros[i] = Service.Configuration.UserMacros[i_next];
                                Service.Configuration.UserMacros[i_next] = m;
                                Service.Configuration.Save();
                                ImGui.ResetMouseDragDelta();
                            }
                        }

                        if (selected && !reorderMode && !P.ws.Windows.Any(x => x.WindowName.Contains(m.ID.ToString())))
                        {
                            MacroEditor macroEditor = new(m.ID);
                        }
                    }

                }
                ImGui.EndChild();
                ImGuiEx.CenterColumnText("Quick Macro Assigner");
                if (ImGui.BeginChild("###Assigner", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true))
                {
                    if (ImGui.BeginCombo($"{LuminaSheets.AddonSheet[405].Text.RawString.Replace("#", "").Replace("n°", "").Trim()}", selectedAssignMacro.Name))
                    {
                        if (ImGui.Selectable(""))
                            selectedAssignMacro = new();

                        foreach (var macro in Service.Configuration.UserMacros)
                        {
                            if (ImGui.Selectable(macro.Name))
                            {
                                selectedAssignMacro = macro;
                            }
                        }
                        ImGui.EndCombo();
                    }

                    if (selectedAssignMacro.ID != 0)
                    {
                        DrawAssignOptions();
                    }
                }
                ImGui.EndChild();
            }
            else
            {
                selectedMacro = new();
                selectedAssignMacro = new();
                selectedActionIndex = -1;
            }
        }

        private static void DrawAssignOptions()
        {
            if (ImGui.SliderInt($"{LuminaSheets.AddonSheet[335].Text}", ref level, 1, 90))
            {
                PossibleDifficulties.Clear();
            }

            if (PossibleDifficulties.Count == 0)
            {
                foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == level))
                {
                    float diffFactor = recipe.DifficultyFactor / 100f;
                    short actualDiff = (short)(diffFactor * recipe.RecipeLevelTable.Value.Difficulty);
                    if (actualDiff == 0) continue;

                    if (!PossibleDifficulties.Contains(actualDiff))
                        PossibleDifficulties.Add(actualDiff);
                }
            }

            if (difficulty < PossibleDifficulties.Min())
                difficulty = minDifficulty;

            if (difficulty > PossibleDifficulties.Max())
                difficulty = maxDifficulty;



            if (!PossibleDifficulties.Any(x => x == difficulty))
            {
                var nearest = PossibleDifficulties.OrderBy(x => Math.Abs(x - difficulty)).FirstOrDefault();
                difficulty = nearest;
            }

            ImGui.SliderInt($"{LuminaSheets.AddonSheet[1431].Text}###RecipeDiff", ref difficulty, PossibleDifficulties.Min(), PossibleDifficulties.Max());

            if (ImGui.BeginListBox($"{LuminaSheets.AddonSheet[5400].Text}###AssignJobBox", new Vector2(0, 55)))
            {
                ImGui.Columns(4, null, false);
                foreach (var item in JobSelected)
                {
                    string jobName = LuminaSheets.ClassJobSheet[item.Key].Abbreviation.ToString().ToUpper();
                    bool val = item.Value;
                    if (ImGui.Checkbox(jobName, ref val))
                    {
                        JobSelected[item.Key] = val;
                    }
                    ImGui.NextColumn();
                }

                ImGui.EndListBox();
            }

            if (ImGui.BeginListBox($"{LuminaSheets.AddonSheet[1430].Text}###AssignDurabilities", new Vector2(0, 55)))
            {
                ImGui.Columns(4, null, false);
                foreach (var dur in Durabilities)
                {
                    var val = dur.Value;
                    if (ImGui.Checkbox($"{dur.Key}", ref val))
                    {
                        Durabilities[dur.Key] = val;
                    }
                    ImGui.NextColumn();
                }
                ImGui.EndListBox();
            }

            if (ImGui.BeginListBox($"{LuminaSheets.AddonSheet[1419].Text}###HQable", new Vector2(0, 28f)))
            {
                ImGui.Columns(2, null, false);
                if (ImGui.RadioButton($"{LuminaSheets.AddonSheet[3].Text}", CannotHQ))
                {
                    CannotHQ = true;
                }
                ImGui.NextColumn();
                if (ImGui.RadioButton($"{LuminaSheets.AddonSheet[4].Text}", !CannotHQ))
                {
                    CannotHQ = false;
                }
                ImGui.Columns(1, null, false);
                ImGui.EndListBox();
            }

            if (ImGui.Checkbox($"Show All Recipes Assigned To", ref Service.Configuration.ShowMacroAssignResults))
                Service.Configuration.Save();

            if (ImGui.Button($"Assign Macro To Recipes", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
            {
                int numberFound = 0;
                foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == level))
                {
                    if (recipe.CanHq == CannotHQ) continue;
                    float diffFactor = recipe.DifficultyFactor / 100f;
                    short actualDiff = (short)(diffFactor * recipe.RecipeLevelTable.Value.Difficulty);

                    if (actualDiff != difficulty) continue;

                    foreach (var job in JobSelected.Where(x => x.Value))
                    {
                        if (recipe.CraftType.Row != job.Key - 8) continue;

                        foreach (var durability in Durabilities.Where(x => x.Value))
                        {
                            if ((ushort)(recipe.RecipeLevelTable.Value.Durability * ((float)recipe.DurabilityFactor / 100)) != durability.Key) continue;

                            if (Service.Configuration.IRM.ContainsKey(recipe.RowId))
                                Service.Configuration.IRM[recipe.RowId] = selectedAssignMacro.ID;
                            else
                                Service.Configuration.IRM.TryAdd(recipe.RowId, selectedAssignMacro.ID);

                            if (Service.Configuration.ShowMacroAssignResults)
                            {
                                P.TM.DelayNext(400);
                                P.TM.Enqueue(() => Notify.Info($"Macro assigned to {recipe.ItemResult.Value.Name.RawString}."));
                            }

                            numberFound++;
                        }
                    }
                }

                if (numberFound > 0)
                {
                    Notify.Success($"Macro assigned to {numberFound} recipes.");
                    Service.Configuration.Save();
                }
                else
                {
                    Notify.Error("No recipes match your parameters. No macros assigned.");
                }
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

            PluginLog.Debug($"{delaySeconds}");

            foreach (var act in m.MacroActions)
            {
                PluginLog.Debug($"{output}");

                if (ActionIsLengthyAnimation(act))
                {
                    output += 2.5 + delaySeconds;
                }
                else
                {
                    output += 1.25 + delaySeconds;
                }
            }

            PluginLog.Debug($"{output}");
            return Math.Round(output, 2);

        }

        public static float GetTeamcraftMacroLength(Macro m)
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
        public static bool ActionIsLengthyAnimation(uint id)
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
                            new MacroEditor(newMacro.ID);
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

        public static void ParseMacro(string text, out Macro macro)
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
