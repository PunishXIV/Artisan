using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ImGuiNET;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Artisan.UI
{
    internal static class MacroUI
    {
        private static string _newMacroName = string.Empty;
        private static bool _keyboardFocus;
        private const string MacroNamePopupLabel = "Macro Name";
        private static bool reorderMode = false;
        private static MacroSolverSettings.Macro? selectedAssignMacro;

        private static int quickAssignLevel = 1;
        private static int quickAssignDifficulty = 9;
        private static int quickAssignQuality = 80;

        private static List<int> quickAssignPossibleDifficulties = new();
        private static int quickAssignMaxDifficulty => quickAssignPossibleDifficulties.LastOrDefault();
        private static int quickAssignMinDifficulty => quickAssignPossibleDifficulties.FirstOrDefault();

        private static List<int> quickAssignPossibleQualities = new();
        private static int quickAssignMaxQuality => quickAssignPossibleQualities.LastOrDefault();
        private static int quickAssignMinQuality => quickAssignPossibleQualities.FirstOrDefault();

        private static bool[] quickAssignJobs = new bool[8];
        private static Dictionary<int, bool> quickAssignDurabilities = new();
        private static bool quickAssignCannotHQ = false;

        internal static void Draw()
        {
            ImGui.TextWrapped("This tab will allow you to add macros that Artisan can use instead of its own decisions. Once you create a new macro, click on it from the list below to open up the macro editor window for your macro.");
            ImGui.Separator();

            if (Svc.ClientState.IsLoggedIn && Crafting.CurState is not Crafting.State.IdleNormal and not Crafting.State.IdleBetween)
            {
                ImGui.Text($"Crafting in progress. Macro settings will be unavailable until you stop crafting.");
                return;
            }
            ImGui.Spacing();
            if (ImGui.Button("Import Macro From Clipboard"))
                OpenMacroNamePopup(MacroNameUse.FromClipboard);

            if (ImGui.Button("Import Macro From Clipboard (Artisan Export)"))
            {
                try
                {
                    var import = JsonConvert.DeserializeObject<MacroSolverSettings.Macro>(ImGui.GetClipboardText());
                    if (import != null)
                    {
                        P.Config.MacroSolverConfig.AddNewMacro(import);
                        P.Config.Save();
                    }
                }
                catch (Exception ex)
                {
                    ex.Log();
                    Notify.Error("Unable to import.");
                }
            }

            if (ImGui.Button("New Macro"))
                OpenMacroNamePopup(MacroNameUse.NewMacro);

            DrawMacroNamePopup(MacroNameUse.FromClipboard);
            DrawMacroNamePopup(MacroNameUse.NewMacro);

            if (P.Config.MacroSolverConfig.Macros.Count > 0)
            {
                if (P.Config.MacroSolverConfig.Macros.Count > 1)
                    ImGui.Checkbox("Reorder Mode (Click and Drag to Reorder)", ref reorderMode);
                else
                    reorderMode = false;

                if (reorderMode)
                    ImGuiEx.CenterColumnText("Reorder Mode");
                else
                    ImGuiEx.CenterColumnText("Macro Editor Select");

                if (ImGui.BeginChild("##selector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 1.85f), true))
                {
                    for (int i = 0; i < P.Config.MacroSolverConfig.Macros.Count; i++)
                    {
                        var m = P.Config.MacroSolverConfig.Macros[i];
                        int cpCost = GetCPCost(m);
                        var selected = ImGui.Selectable($"{m.Name} (CP Cost: {cpCost}) (ID: {m.ID})###{m.ID}");

                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered() && reorderMode)
                        {
                            int i_next = i + (ImGui.GetMouseDragDelta(0).Y < 0f ? -1 : 1);
                            if (i_next >= 0 && i_next < P.Config.MacroSolverConfig.Macros.Count)
                            {
                                P.Config.MacroSolverConfig.Macros[i] = P.Config.MacroSolverConfig.Macros[i_next];
                                P.Config.MacroSolverConfig.Macros[i_next] = m;
                                P.Config.Save();
                                ImGui.ResetMouseDragDelta();
                            }
                        }

                        if (selected && !reorderMode && !P.ws.Windows.Any(x => x.WindowName.Contains(m.ID.ToString())))
                        {
                            new MacroEditor(m);
                        }
                    }

                }
                ImGui.EndChild();
                ImGuiEx.CenterColumnText("Quick Macro Assigner");
                if (ImGui.BeginChild("###Assigner", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true))
                {
                    if (ImGui.BeginCombo($"{LuminaSheets.AddonSheet[405].Text.ToString().Replace("#", "").Replace("n°", "").Trim()}", selectedAssignMacro?.Name ?? ""))
                    {
                        if (ImGui.Selectable(""))
                            selectedAssignMacro = null;

                        foreach (var macro in P.Config.MacroSolverConfig.Macros)
                        {
                            if (ImGui.Selectable(macro.Name))
                            {
                                selectedAssignMacro = macro;
                            }
                        }
                        ImGui.EndCombo();
                    }

                    if (selectedAssignMacro != null)
                    {
                        DrawAssignOptions(selectedAssignMacro);
                    }
                }
                ImGui.EndChild();
            }
            else
            {
                selectedAssignMacro = null;
            }
        }

        private static void DrawAssignOptions(MacroSolverSettings.Macro macro)
        {
            IEnumerable<Lumina.Excel.Sheets.Recipe> filteredRecipes = LuminaSheets.RecipeSheet.Values;

            if (ImGui.SliderInt($"{LuminaSheets.AddonSheet[335].Text}", ref quickAssignLevel, 1, 100))
            {
                quickAssignPossibleDifficulties.Clear();
                quickAssignPossibleQualities.Clear();
                quickAssignDurabilities.Clear();
            }
            filteredRecipes = filteredRecipes.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == quickAssignLevel);

            if (quickAssignPossibleDifficulties.Count == 0)
            {
                foreach (var recipe in filteredRecipes)
                {
                    var actualDiff = Calculations.RecipeDifficulty(recipe);
                    if (actualDiff != 0)
                        quickAssignPossibleDifficulties.Add(actualDiff);
                }
                quickAssignPossibleDifficulties.SortAndRemoveDuplicates();
            }
            quickAssignDifficulty = quickAssignPossibleDifficulties.FindClosest(quickAssignDifficulty);
            if (ImGui.SliderInt($"{LuminaSheets.AddonSheet[1431].Text}###RecipeDiff", ref quickAssignDifficulty, quickAssignMinDifficulty, quickAssignMaxDifficulty))
            {
                quickAssignPossibleQualities.Clear();
                quickAssignDurabilities.Clear();
            }
            filteredRecipes = filteredRecipes.Where(x => Calculations.RecipeDifficulty(x) == quickAssignDifficulty);

            if (quickAssignPossibleQualities.Count == 0)
            {
                foreach (var recipe in filteredRecipes)
                {
                    var actualQual = Calculations.RecipeMaxQuality(recipe);
                    if (actualQual != 0)
                        quickAssignPossibleQualities.Add(actualQual);
                }
                quickAssignPossibleQualities.SortAndRemoveDuplicates();
            }

            if (quickAssignPossibleQualities.Any())
            {
                quickAssignQuality = quickAssignPossibleQualities.FindClosest(quickAssignQuality);
                if (ImGui.SliderInt($"{LuminaSheets.AddonSheet[216].Text}###RecipeQuality", ref quickAssignQuality, quickAssignMinQuality, quickAssignMaxQuality))
                {
                    quickAssignDurabilities.Clear();
                }

                filteredRecipes = filteredRecipes.Where(x => Calculations.RecipeMaxQuality(x) == quickAssignQuality);

                if (ImGui.BeginListBox($"{LuminaSheets.AddonSheet[5400].Text}###AssignJobBox", new Vector2(0, 55)))
                {
                    ImGui.Columns(4, null, false);
                    for (var job = Job.CRP; job <= Job.CUL; ++job)
                    {
                        ImGui.Checkbox(job.ToString(), ref quickAssignJobs[job - Job.CRP]);
                        ImGui.NextColumn();
                    }
                    ImGui.EndListBox();
                }
                filteredRecipes = filteredRecipes.Where(x => quickAssignJobs[x.CraftType.RowId]);

                if (filteredRecipes.Any())
                {
                    if (ImGui.BeginListBox($"{LuminaSheets.AddonSheet[1430].Text}###AssignDurabilities", new Vector2(0, 55)))
                    {
                        ImGui.Columns(4, null, false);

                        foreach (var recipe in filteredRecipes)
                        {
                            quickAssignDurabilities.TryAdd(Calculations.RecipeDurability(recipe), false);
                        }

                        foreach (var dur in quickAssignDurabilities)
                        {
                            var val = dur.Value;
                            if (ImGui.Checkbox($"{dur.Key}", ref val))
                            {
                                quickAssignDurabilities[dur.Key] = val;
                            }
                            ImGui.NextColumn();
                        }

                        if (quickAssignDurabilities.Count == 1)
                        {
                            var key = quickAssignDurabilities.First().Key;
                            quickAssignDurabilities[key] = true;
                        }
                        ImGui.EndListBox();
                    }
                    filteredRecipes = filteredRecipes.Where(x => quickAssignDurabilities[Calculations.RecipeDurability(x)]);

                    if (ImGui.BeginListBox($"{LuminaSheets.AddonSheet[1419].Text}###HQable", new Vector2(0, 28f)))
                    {
                        var anyHQ = filteredRecipes.Any(recipe => recipe.CanHq);
                        var anyNonHQ = filteredRecipes.Any(recipe => !recipe.CanHq);

                        ImGui.Columns(2, null, false);
                        if (anyNonHQ)
                        {
                            if (!anyHQ)
                                quickAssignCannotHQ = true;

                            if (ImGui.RadioButton($"{LuminaSheets.AddonSheet[3].Text.ToString().Replace(".", "")}", quickAssignCannotHQ))
                            {
                                quickAssignCannotHQ = true;
                            }
                        }
                        ImGui.NextColumn();
                        if (anyHQ)
                        {
                            if (!anyNonHQ)
                                quickAssignCannotHQ = false;

                            if (ImGui.RadioButton($"{LuminaSheets.AddonSheet[4].Text.ToString().Replace(".", "")}", !quickAssignCannotHQ))
                            {
                                quickAssignCannotHQ = false;
                            }
                        }
                        ImGui.Columns(1, null, false);
                        ImGui.EndListBox();
                    }
                    filteredRecipes = filteredRecipes.Where(x => x.CanHq != quickAssignCannotHQ);

                    if (ImGui.Checkbox($"Show All Recipes Assigned To", ref P.Config.ShowMacroAssignResults))
                        P.Config.Save();

                    if (ImGui.Button($"Assign Macro To Recipes", new Vector2(ImGui.GetContentRegionAvail().X / 2, 24f.Scale())))
                    {
                        int numberFound = 0;
                        foreach (var recipe in filteredRecipes)
                        {
                            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipe.RowId);
                            if (config == null)
                                P.Config.RecipeConfigs[recipe.RowId] = config = new();
                            config.SolverType = typeof(MacroSolverDefinition).FullName!;
                            config.SolverFlavour = selectedAssignMacro.ID;
                            if (P.Config.ShowMacroAssignResults)
                            {
                                P.TM.DelayNext(400);
                                P.TM.Enqueue(() => Notify.Info($"Macro assigned to {recipe.ItemResult.Value.Name.ToDalamudString().ToString()}."));
                            }
                            numberFound++;
                        }

                        if (numberFound > 0)
                        {
                            Notify.Success($"Macro assigned to {numberFound} recipes.");
                            P.Config.Save();
                        }
                        else
                        {
                            Notify.Error("No recipes match your parameters. No macros assigned.");
                        }
                    }
                    ImGui.SameLine();
                }
            }
            if (ImGui.Button($"Unassign Macro From All Recipes (Hold Ctrl)", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())) && ImGui.GetIO().KeyCtrl)
            {
                int count = 0;
                foreach (var e in P.Config.RecipeConfigs)
                    if (e.Value.SolverType == typeof(MacroSolverDefinition).FullName && e.Value.SolverFlavour == selectedAssignMacro.ID)
                    {
                        P.Config.RecipeConfigs.Remove(e.Key); // TODO: do we want to preserve other configs?..
                        count++;
                    }
                P.Config.Save();
                if (count > 0)
                    Notify.Success($"Removed from {count} recipes.");
                else
                    Notify.Error($"This macro was not assigned to any recipes.");
            }
        }

        private static void SortAndRemoveDuplicates(this List<int> list)
        {
            if (list.Count == 0)
                return;
            list.Sort();
            int dest = 1;
            int prev = list[0];
            for (int src = 1; src < list.Count; ++src)
                if (list[src] != prev)
                    list[dest++] = list[src];
            list.RemoveRange(dest, list.Count - dest);
        }

        public static int UpperBound(this List<int> list, int test)
        {
            int first = 0, size = list.Count;
            while (size > 0)
            {
                int step = size / 2;
                int mid = first + step;
                if (list[mid] <= test)
                {
                    first = mid + 1;
                    size -= step + 1;
                }
                else
                {
                    size = step;
                }
            }
            return first;
        }

        public static int FindClosest(this List<int> list, int test)
        {
            var ub = list.UpperBound(test);
            return ub == 0 ? list[0] : ub == list.Count ? list[list.Count - 1] : test - list[ub - 1] < list[ub] - test ? list[ub - 1] : list[ub];
        }

        private static int GetCPCost(MacroSolverSettings.Macro m)
        {
            Skills previousAction = Skills.None;
            int output = 0;
            int tcr = 0;
            foreach (var step in m.Steps)
            {
                if (step.Action == Skills.TouchCombo)
                {
                    output += 18;
                }
                if (step.Action == Skills.TouchComboRefined)
                {
                    if (tcr % 2 == 1)
                        output += 18;
                    else
                        output += 24;

                    tcr++;

                }
                output += Simulator.GetBaseCPCost(step.Action, previousAction);
                previousAction = step.Action;
            }
            return output;
        }

        public static double GetMacroLength(MacroSolverSettings.Macro m)
        {
            double output = 0;
            var delay = (double)P.Config.AutoDelay + (P.Config.DelayRecommendation ? P.Config.RecommendationDelay : 0);
            var delaySeconds = delay / 1000;

            foreach (var step in m.Steps)
            {
                if (step.Action.ActionIsLengthyAnimation())
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

        public static float GetTeamcraftMacroLength(MacroSolverSettings.Macro m)
        {
            float output = 0;
            foreach (var step in m.Steps)
            {
                if (step.Action.ActionIsLengthyAnimation())
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

        private static void DrawMacroNamePopup(MacroNameUse use)
        {
            if (ImGui.BeginPopup($"{MacroNamePopupLabel}{use}"))
            {
                if (_keyboardFocus)
                {
                    ImGui.SetKeyboardFocusHere();
                    _keyboardFocus = false;
                }

                if (ImGui.InputText("Macro Name##macroName", ref _newMacroName, 64, ImGuiInputTextFlags.EnterReturnsTrue) && _newMacroName.Any())
                {
                    switch (use)
                    {
                        case MacroNameUse.NewMacro:
                            MacroSolverSettings.Macro newMacro = new();
                            newMacro.Name = _newMacroName;
                            P.Config.MacroSolverConfig.AddNewMacro(newMacro);
                            P.Config.Save();
                            new MacroEditor(newMacro);
                            break;
                        case MacroNameUse.FromClipboard:
                            try
                            {
                                var steps = ParseMacro(ImGui.GetClipboardText(), false);
                                if (steps.Count > 0)
                                {
                                    var macro = new MacroSolverSettings.Macro();
                                    macro.Name = _newMacroName;
                                    macro.Steps = steps;
                                    P.Config.MacroSolverConfig.AddNewMacro(macro);
                                    P.Config.Save();
                                    DuoLog.Information($"{macro.Name} has been saved.");
                                }
                                else
                                {
                                    DuoLog.Error("Unable to parse clipboard. Please check your clipboard contains a working macro with actions.");
                                }
                            }
                            catch (Exception e)
                            {
                                Svc.Log.Information($"Could not save new Macro from Clipboard:\n{e}");
                            }

                            break;
                    }

                    _newMacroName = string.Empty;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        public static List<MacroSolverSettings.MacroStep> ParseMacro(string text, bool raphParseEN = false)
        {
            var res = new List<MacroSolverSettings.MacroStep>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return res;
            }

            using (System.IO.StringReader reader = new System.IO.StringReader(text))
            {
                string line = "";
                while ((line = reader.ReadLine()!) != null)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 1) continue;

                    var iStart = 0;
                    if (parts[0].Equals("/ac", StringComparison.CurrentCultureIgnoreCase) || parts[0].Equals("/action", StringComparison.CurrentCultureIgnoreCase))
                        ++iStart;
                    else if (parts[0].Contains("/", StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    var builder = new StringBuilder();
                    for (int i = iStart; i < parts.Length; i++)
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
                        res.Add(new() { Action = Skills.None });
                        continue;
                    }

                    var act = Enum.GetValues(typeof(Skills)).Cast<Skills>().FirstOrDefault(s => s.NameOfAction(raphParseEN).Equals(action, StringComparison.CurrentCultureIgnoreCase));
                    if (act == default)
                    {
                        act = Enum.GetValues(typeof(Skills)).Cast<Skills>().FirstOrDefault(s => s.NameOfAction(raphParseEN).Replace(" ", "").Replace("'", "").Equals(action, StringComparison.CurrentCultureIgnoreCase));
                        if (act == default)
                        {
                            DuoLog.Error($"Unable to parse action: {action}");
                            continue;
                        }
                    }
                    res.Add(new() { Action = act });
                }
            }
            return res;
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
