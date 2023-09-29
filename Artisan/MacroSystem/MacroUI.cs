using Artisan.CraftingLogic;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Newtonsoft.Json;
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
        private static bool showNonHQ = false;
        private static bool showHQ = false;

        private static int level = 1;
        private static int difficulty = 9;
        private static int quality = 80;

        private static int maxDifficulty = LuminaSheets.RecipeLevelTableSheet.Values.Where(x => x.ClassJobLevel == level).Max(x => x.Difficulty);
        private static int minDifficulty = LuminaSheets.RecipeLevelTableSheet.Values.Where(x => x.ClassJobLevel == level).Min(x => x.Difficulty);
        private static List<int> PossibleDifficulties = new();
        private static List<int> PossibleQualities = new();

        private static bool CannotHQ = false;
        private static Dictionary<uint, bool> JobSelected = LuminaSheets.ClassJobSheet.Values.Where(x => x.RowId >= 8 && x.RowId <= 15).ToDictionary(x => x.RowId, x => false);
        private static Dictionary<ushort, bool> Durabilities = LuminaSheets.RecipeSheet.Values.Where(x => x.Number > 0 && x.RecipeLevelTable.Value.ClassJobLevel == level && Math.Floor(x.RecipeLevelTable.Value.Difficulty * (x.DifficultyFactor / 100f)) == difficulty).Select(x => (ushort)(x.RecipeLevelTable.Value.Durability * ((float)x.DurabilityFactor / 100))).Distinct().Order().ToDictionary(x => x, x => false);

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

            if (ImGui.Button("Import Macro From Clipboard (Artisan Export)"))
            {
                try
                {
                    var clipboard = ImGui.GetClipboardText();
                    if (clipboard != null)
                    {
                        var import = JsonConvert.DeserializeObject<Macro>(clipboard);
                        if (import != null)
                        {
                            import.SetID();
                            import.Save(true);
                        }
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

            ImGui.Checkbox("Reorder Mode (Click and Drag to Reorder)", ref reorderMode);

            if (reorderMode)
                ImGuiEx.CenterColumnText("Reorder Mode");
            else
                ImGuiEx.CenterColumnText("Macro Editor Select");

            if (P.Config.UserMacros.Count > 0)
            {
                float longestName = 0;
                foreach (var macro in P.Config.UserMacros)
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
                    for (int i = 0; i < P.Config.UserMacros.Count; i++)
                    {
                        var m = P.Config.UserMacros[i];
                        uint cpCost = GetCPCost(m);
                        var selected = ImGui.Selectable($"{m.Name} (CP Cost: {cpCost})###{m.ID}");

                        if (ImGui.IsItemActive() && !ImGui.IsItemHovered() && reorderMode)
                        {

                            int i_next = i + (ImGui.GetMouseDragDelta(0).Y < 0f ? -1 : 1);
                            if (i_next >= 0 && i_next < P.Config.UserMacros.Count)
                            {
                                P.Config.UserMacros[i] = P.Config.UserMacros[i_next];
                                P.Config.UserMacros[i_next] = m;
                                P.Config.Save();
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

                        foreach (var macro in P.Config.UserMacros)
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
                Durabilities.Clear();
                PossibleQualities.Clear();
            }

            if (PossibleDifficulties.Count == 0)
            {
                foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == level))
                {
                    float diffFactor = recipe.DifficultyFactor / 100f;
                    var actualDiff = (int)Math.Floor(diffFactor * recipe.RecipeLevelTable.Value.Difficulty);
                    if (actualDiff == 0) continue;

                    if (!PossibleDifficulties.Contains(actualDiff))
                        PossibleDifficulties.Add(actualDiff);
                }
            }


            if (difficulty < PossibleDifficulties.Min())
                difficulty = PossibleDifficulties.Min();

            if (difficulty > PossibleDifficulties.Max())
                difficulty = PossibleDifficulties.Max();


            if (!PossibleDifficulties.Any(x => x == difficulty))
            {
                var nearest = PossibleDifficulties.OrderBy(x => Math.Abs(x - difficulty)).FirstOrDefault();
                difficulty = nearest;
            }

            if (ImGui.SliderInt($"{LuminaSheets.AddonSheet[1431].Text}###RecipeDiff", ref difficulty, PossibleDifficulties.Min(), PossibleDifficulties.Max()))
            {
                Durabilities.Clear();

                if (PossibleDifficulties.Any())
                    PossibleQualities.Clear();
            }

            if (PossibleQualities.Count == 0 && PossibleDifficulties.Any(x => x == difficulty))
            {
                foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == level &&
                Math.Floor(x.RecipeLevelTable.Value.Difficulty * (x.DifficultyFactor / 100f)) == difficulty))
                {
                    float diffFactor = recipe.QualityFactor / 100f;
                    var actualDiff = (int)Math.Floor(diffFactor * recipe.RecipeLevelTable.Value.Quality);
                    if (actualDiff == 0) continue;

                    if (!PossibleQualities.Contains(actualDiff))
                        PossibleQualities.Add(actualDiff);
                }

                if (PossibleQualities.Any())
                {
                    if (quality < PossibleQualities.Min())
                        quality = PossibleQualities.Min();

                    if (quality > PossibleQualities.Max())
                        quality = PossibleQualities.Max();
                }
            }



            if (!PossibleQualities.Any(x => x == quality))
            {
                var nearest = PossibleQualities.OrderBy(x => Math.Abs(x - quality)).FirstOrDefault();
                quality = nearest;
            }

            if (PossibleQualities.Any())
            {
                if (ImGui.SliderInt($"{LuminaSheets.AddonSheet[216].Text}###RecipeQuality", ref quality, PossibleQualities.Min(), PossibleQualities.Max()))
                {
                    Durabilities.Clear();
                }
            }

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

                foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == level &&
                Math.Floor(x.RecipeLevelTable.Value.Difficulty * (x.DifficultyFactor / 100f)) == difficulty &&
                Math.Floor(x.RecipeLevelTable.Value.Quality * (x.QualityFactor / 100f)) == quality))
                {
                    Durabilities.TryAdd((ushort)(recipe.RecipeLevelTable.Value.Durability * (recipe.DurabilityFactor / 100f)), false);
                }

                foreach (var dur in Durabilities)
                {
                    var val = dur.Value;
                    if (ImGui.Checkbox($"{dur.Key}", ref val))
                    {
                        Durabilities[dur.Key] = val;
                    }
                    ImGui.NextColumn();
                }

                if (Durabilities.Count == 1)
                {
                    var key = Durabilities.First().Key;
                    Durabilities[key] = true;
                }
                ImGui.EndListBox();
            }

            if (ImGui.BeginListBox($"{LuminaSheets.AddonSheet[1419].Text}###HQable", new Vector2(0, 28f)))
            {
                showHQ = false;
                showNonHQ = false;

                foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == level &&
                Math.Floor(x.RecipeLevelTable.Value.Difficulty * (x.DifficultyFactor / 100f)) == difficulty &&
                Math.Floor(x.RecipeLevelTable.Value.Quality * (x.QualityFactor / 100f)) == quality))
                {
                    if (recipe.CanHq)
                    {
                        showHQ = true;
                    }
                    else
                    {
                        showNonHQ = true;
                    }
                }

                ImGui.Columns(2, null, false);
                if (showNonHQ)
                {
                    if (!showHQ)
                        CannotHQ = true;

                    if (ImGui.RadioButton($"{LuminaSheets.AddonSheet[3].Text.RawString.Replace(".", "")}", CannotHQ))
                    {
                        CannotHQ = true;
                    }
                }
                ImGui.NextColumn();
                if (showHQ)
                {
                    if (!showNonHQ)
                        CannotHQ = false;

                    if (ImGui.RadioButton($"{LuminaSheets.AddonSheet[4].Text.RawString.Replace(".", "")}", !CannotHQ))
                    {
                        CannotHQ = false;
                    }
                }
                ImGui.Columns(1, null, false);
                ImGui.EndListBox();
            }

            if (ImGui.Checkbox($"Show All Recipes Assigned To", ref P.Config.ShowMacroAssignResults))
                P.Config.Save();

            if (ImGui.Button($"Assign Macro To Recipes", new Vector2(ImGui.GetContentRegionAvail().X / 2, 24f.Scale())))
            {
                int numberFound = 0;
                foreach (var recipe in LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == level))
                {
                    if (recipe.CanHq == CannotHQ) continue;
                    float diffFactor = recipe.DifficultyFactor / 100f;
                    short actualDiff = (short)(diffFactor * recipe.RecipeLevelTable.Value.Difficulty);

                    if (actualDiff != difficulty) continue;

                    float qualFactor = recipe.QualityFactor / 100f;
                    short qualDiff = (short)(qualFactor * recipe.RecipeLevelTable.Value.Quality);

                    if (qualDiff != quality) continue;

                    foreach (var job in JobSelected.Where(x => x.Value))
                    {
                        if (recipe.CraftType.Row != job.Key - 8) continue;

                        foreach (var durability in Durabilities.Where(x => x.Value))
                        {
                            if ((ushort)(recipe.RecipeLevelTable.Value.Durability * ((float)recipe.DurabilityFactor / 100)) != durability.Key) continue;

                            if (P.Config.IRM.ContainsKey(recipe.RowId))
                                P.Config.IRM[recipe.RowId] = selectedAssignMacro.ID;
                            else
                                P.Config.IRM.TryAdd(recipe.RowId, selectedAssignMacro.ID);

                            if (P.Config.ShowMacroAssignResults)
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
                    P.Config.Save();
                }
                else
                {
                    Notify.Error("No recipes match your parameters. No macros assigned.");
                }
            }
            ImGui.SameLine();
            if (ImGui.Button($"Unassign Macro From All Recipes (Hold Ctrl)", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())) && ImGui.GetIO().KeyCtrl)
            {
                var count = P.Config.IRM.Where(x => x.Value == selectedAssignMacro.ID).Count();
                foreach (var macro in P.Config.IRM.ToList())
                {
                    if (macro.Value == selectedAssignMacro.ID)
                    {
                        P.Config.IRM.Remove(macro.Key);
                    }
                }
                P.Config.Save();
                if (count > 0)
                    Notify.Success($"Removed from {count} recipes.");
                else
                    Notify.Error($"This macro was not assigned to any recipes.");
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
            var delay = (double)P.Config.AutoDelay + (P.Config.DelayRecommendation ? P.Config.RecommendationDelay : 0);
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
                                        Svc.Chat.Print($"{macro.Name} has been saved.");
                                    }
                                    else
                                    {
                                        Svc.Chat.PrintError("Unable to save macro. Please check your clipboard contains a working macro with actions.");
                                    }
                                else
                                    Svc.Chat.PrintError("Unable to parse clipboard. Please check your clipboard contains a working macro with actions.");
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
                                Svc.Chat.PrintError($"Unable to parse action: {action}");
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
                                Svc.Chat.PrintError($"Unable to parse action: {action}");
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
                                Svc.Chat.PrintError($"Unable to parse action: {action}");
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
                                Svc.Chat.PrintError($"Unable to parse action: {action}");
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
