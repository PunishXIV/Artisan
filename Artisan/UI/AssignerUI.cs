using Artisan.CraftingLogic;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Channels;

namespace Artisan.UI
{
    internal class AssignerUI
    {

        private static RecipeConfig DummyConfig = new();
        private static ISolverDefinition.Desc? selectedSolver;
        private static int quickAssignLevel = 1;
        private static int quickAssignDifficulty = 9;
        private static int quickAssignQuality = 80;

        private static IEnumerable<Lumina.Excel.Sheets.Recipe> filteredRecipes;
        private static List<int> quickAssignPossibleDifficulties = new();
        private static int quickAssignMaxDifficulty => quickAssignPossibleDifficulties.LastOrDefault();
        private static int quickAssignMinDifficulty => quickAssignPossibleDifficulties.FirstOrDefault();

        private static List<int> quickAssignPossibleQualities = new();
        private static int quickAssignMaxQuality => quickAssignPossibleQualities.LastOrDefault();
        private static int quickAssignMinQuality => quickAssignPossibleQualities.FirstOrDefault();

        private static bool[] quickAssignJobs = new bool[8];
        private static Dictionary<int, bool> quickAssignDurabilities = new();
        private static bool quickAssignCannotHQ = false;
        private static bool Notification;

        public static void Draw()
        {
            ImGuiEx.TextWrapped($"This tab allows you to quickly assign solvers and consumables to recipes based on recipe criteria.");
            ImGui.Separator();
            ImGui.Spacing();
            DrawCriteria();
            DrawAssignables();
        }

        private static void DrawCriteria()
        {
            ImGuiEx.TextCentered($"Criteria");
            DrawAssignOptions();
        }

        private static void DrawAssignables()
        {
            if (filteredRecipes.Count() == 0)
                return;

            ImGui.Spacing();
            var recipe = filteredRecipes.First();
            var stats = CharacterStats.GetBaseStatsForClassHeuristic(Job.CRP + recipe.CraftType.RowId);
            stats.AddConsumables(new(DummyConfig.RequiredFood, DummyConfig.RequiredFoodHQ), new(DummyConfig.RequiredPotion, DummyConfig.RequiredPotionHQ), CharacterInfo.FCCraftsmanshipbuff);
            var c = Crafting.BuildCraftStateForRecipe(stats, Job.CRP + recipe.CraftType.RowId, recipe);

            DummyConfig.DrawFood();
            DummyConfig.DrawPotion();
            DummyConfig.DrawManual();
            DummyConfig.DrawSquadronManual();
            DummyConfig.DrawSolver(c, false, false);

            ImGui.Checkbox("Show which crafts have been assigned as a notification", ref Notification);
            if (ImGui.Button("Assign To All", new Vector2(ImGui.GetContentRegionAvail().X, 25f.Scale())))
            {
                foreach (var rec in filteredRecipes)
                {
                    P.Config.RecipeConfigs[rec.RowId] = new() 
                    {   requiredFood = DummyConfig.requiredFood, 
                        requiredFoodHQ = DummyConfig.requiredFoodHQ,
                        requiredPotion = DummyConfig.requiredPotion,
                        requiredPotionHQ = DummyConfig.requiredPotionHQ,
                        requiredManual = DummyConfig.requiredManual,
                        requiredSquadronManual = DummyConfig.requiredSquadronManual,
                        SolverFlavour = DummyConfig.SolverFlavour,
                        SolverType = DummyConfig.SolverType,
                    };
                    if (Notification)
                    {
                        P.TM.Enqueue(() => Notify.Success($"Assigned {rec.CraftType.Value.Name} - {rec.ItemResult.Value.Name}"));
                        P.TM.DelayNext(75);
                    }
                }
                P.Config.Save();
            }
        }

        private static void DrawAssignOptions()
        {
            filteredRecipes = LuminaSheets.RecipeSheet.Values;
            ImGuiEx.Text($"{LuminaSheets.AddonSheet[335].Text}");
            ImGui.SameLine(100f.Scale());
            if (ImGui.SliderInt($"###QuickAssignLevel", ref quickAssignLevel, 1, 100))
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
            ImGuiEx.Text($"{LuminaSheets.AddonSheet[1431].Text}");
            ImGui.SameLine(100f.Scale());
            if (ImGui.SliderInt($"###RecipeDiff", ref quickAssignDifficulty, quickAssignMinDifficulty, quickAssignMaxDifficulty))
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
                ImGuiEx.Text($"{LuminaSheets.AddonSheet[216].Text}");
                ImGui.SameLine(100f.Scale());
                if (ImGui.SliderInt($"###RecipeQuality", ref quickAssignQuality, quickAssignMinQuality, quickAssignMaxQuality))
                {
                    quickAssignDurabilities.Clear();
                }

                filteredRecipes = filteredRecipes.Where(x => Calculations.RecipeMaxQuality(x) == quickAssignQuality);

                ImGuiEx.Text($"{LuminaSheets.AddonSheet[5400].Text}");
                ImGui.SameLine(100f.Scale());
                if (ImGui.BeginListBox($"###AssignJobBox", new Vector2(0, 55f.Scale())))
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
                    ImGuiEx.Text($"{LuminaSheets.AddonSheet[1430].Text}");
                    ImGui.SameLine(100f.Scale());
                    if (ImGui.BeginListBox($"###AssignDurabilities", new Vector2(0, 55f.Scale())))
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
                    var lbs = ImGui.GetItemRectSize();

                    ImGuiEx.Text($"{LuminaSheets.AddonSheet[1419].Text}");
                    ImGui.SameLine(160f.Scale());
                    if (ImGui.BeginListBox($"###HQable", new Vector2(lbs.X-60f.Scale(), 28f.Scale())))
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
                }
            }
        }

        private static void UnassignSolvers()
        {

        }
    }
}