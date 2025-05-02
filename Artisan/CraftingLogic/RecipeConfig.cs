using Artisan.Autocraft;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Artisan.CraftingLogic;

public class RecipeConfig
{
    public string SolverType = ""; // TODO: ideally it should be a Type?, but that causes problems for serialization
    public int SolverFlavour;
    public uint RequiredFood = 0;
    public uint RequiredPotion = 0;
    public uint RequiredManual = 0;
    public uint RequiredSquadronManual = 0;
    public bool RequiredFoodHQ = true;
    public bool RequiredPotionHQ = true;

    public bool Draw(uint recipeId)
    {
        var recipe = LuminaSheets.RecipeSheet[recipeId];
        ImGuiEx.LineCentered($"###RecipeName{recipeId}", () => { ImGuiEx.TextUnderlined($"{recipe.ItemResult.Value.Name.ToDalamudString().ToString()}"); });
        var config = this;
        var stats = CharacterStats.GetBaseStatsForClassHeuristic(Job.CRP + recipe.CraftType.RowId);
        stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ), CharacterInfo.FCCraftsmanshipbuff);
        var craft = Crafting.BuildCraftStateForRecipe(stats, Job.CRP + recipe.CraftType.RowId, recipe);
        bool changed = false;
        changed |= DrawFood();
        changed |= DrawPotion();
        changed |= DrawManual();
        changed |= DrawSquadronManual();
        changed |= DrawSolver(craft);
        DrawSimulator(craft);
        return changed;
    }

    public bool DrawFood(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV("Food Usage:");
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        if (ImGui.BeginCombo("##foodBuff", RequiredFood == 0 ? "Disabled" : $"{(RequiredFoodHQ ? " " : "")}{ConsumableChecker.Food.FirstOrDefault(x => x.Id == RequiredFood).Name}"))
        {
            if (ImGui.Selectable("Disable"))
            {
                RequiredFood = 0;
                RequiredFoodHQ = false;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetFood(true))
            {
                if (ImGui.Selectable($"{x.Name}"))
                {
                    RequiredFood = x.Id;
                    RequiredFoodHQ = false;
                    changed = true;
                }
            }
            foreach (var x in ConsumableChecker.GetFood(true, true))
            {
                if (ImGui.Selectable($" {x.Name}"))
                {
                    RequiredFood = x.Id;
                    RequiredFoodHQ = true;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    public bool DrawPotion(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV("Medicine Usage:");
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        if (ImGui.BeginCombo("##potBuff", RequiredPotion == 0 ? "Disabled" : $"{(RequiredPotionHQ ? " " : "")}{ConsumableChecker.Pots.FirstOrDefault(x => x.Id == RequiredPotion).Name}"))
        {
            if (ImGui.Selectable("Disable"))
            {
                RequiredPotion = 0;
                RequiredPotionHQ = false;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetPots(true))
            {
                if (ImGui.Selectable($"{x.Name}"))
                {
                    RequiredPotion = x.Id;
                    RequiredPotionHQ = false;
                    changed = true;
                }
            }
            foreach (var x in ConsumableChecker.GetPots(true, true))
            {
                if (ImGui.Selectable($" {x.Name}"))
                {
                    RequiredPotion = x.Id;
                    RequiredPotionHQ = true;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    public bool DrawManual(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV("Manual Usage:");
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        if (ImGui.BeginCombo("##manualBuff", RequiredManual == 0 ? "Disabled" : $"{ConsumableChecker.Manuals.FirstOrDefault(x => x.Id == RequiredManual).Name}"))
        {
            if (ImGui.Selectable("Disable"))
            {
                RequiredManual = 0;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetManuals(true))
            {
                if (ImGui.Selectable($"{x.Name}"))
                {
                    RequiredManual = x.Id;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    public bool DrawSquadronManual(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV("Squadron Manual:");
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        if (ImGui.BeginCombo("##squadronManualBuff", RequiredSquadronManual == 0 ? "Disabled" : $"{ConsumableChecker.SquadronManuals.FirstOrDefault(x => x.Id == RequiredSquadronManual).Name}"))
        {
            if (ImGui.Selectable("Disable"))
            {
                RequiredSquadronManual = 0;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetSquadronManuals(true))
            {
                if (ImGui.Selectable($"{x.Name}"))
                {
                    RequiredSquadronManual = x.Id;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    public bool DrawSolver(CraftState craft, bool hasButton = false, bool liveStats = true)
    {
        bool changed = false;
        ImGuiEx.TextV($"Solver:");
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        var solver = CraftingProcessor.GetSolverForRecipe(this, craft);
        if (ImGui.BeginCombo("##solver", solver.Name))
        {
            foreach (var opt in CraftingProcessor.GetAvailableSolversForRecipe(craft, true))
            {
                if (opt == default) continue;
                if (opt.UnsupportedReason.Length > 0)
                {
                    ImGui.Text($"{opt.Name} is unsupported - {opt.UnsupportedReason}");
                }
                else
                {
                    bool selected = opt.Def == solver.Def && opt.Flavour == solver.Flavour;
                    if (ImGui.Selectable(opt.Name, selected))
                    {
                        SolverType = opt.Def.GetType().FullName!;
                        SolverFlavour = opt.Flavour;
                        changed = true;
                    }
                }
            }

            ImGui.EndCombo();
        }

        changed |= RaphaelCache.DrawRaphaelDropdown(craft, liveStats);

        return changed;
    }

    public unsafe void DrawSimulator(CraftState craft)
    {
        if (!P.Config.HideRecipeWindowSimulator)
        {
            var recipe = craft.Recipe;
            var config = this;
            var solverHint = Simulator.SimulatorResult(recipe, config, craft, out var hintColor);
            var solver = CraftingProcessor.GetSolverForRecipe(config, craft);

            if (solver.Name != "Expert Recipe Solver")
            {
                if (craft.MissionHasMaterialMiracle && solver.Name == "Standard Recipe Solver" && P.Config.UseMaterialMiracle)
                    ImGuiEx.TextWrapped($"This would use Material Miracle, which is not compatible with the simulator.");
                else
                    ImGuiEx.TextWrapped(hintColor, solverHint);
            }
            else
                ImGuiEx.TextWrapped($"Please run this recipe in the simulator for results.");

            if (ImGui.IsItemClicked())
            {
                P.PluginUi.OpenWindow = UI.OpenWindow.Simulator;
                P.PluginUi.IsOpen = true;
                SimulatorUI.SelectedRecipe = recipe;
                SimulatorUI.ResetSim();
                if (config.RequiredPotion > 0)
                {
                    SimulatorUI.SimMedicine ??= new();
                    SimulatorUI.SimMedicine.Id = config.RequiredPotion;
                    SimulatorUI.SimMedicine.ConsumableHQ = config.RequiredPotionHQ;
                    SimulatorUI.SimMedicine.Stats = new ConsumableStats(config.RequiredPotion, config.RequiredPotionHQ);
                }
                if (config.RequiredFood > 0)
                {
                    SimulatorUI.SimFood ??= new();
                    SimulatorUI.SimFood.Id = config.RequiredFood;
                    SimulatorUI.SimFood.ConsumableHQ = config.RequiredFoodHQ;
                    SimulatorUI.SimFood.Stats = new ConsumableStats(config.RequiredFood, config.RequiredFoodHQ);
                }

                foreach (ref var gs in RaptureGearsetModule.Instance()->Entries)
                {
                    if ((Job)gs.ClassJob == Job.CRP + recipe.CraftType.RowId)
                    {
                        if (SimulatorUI.SimGS is null || (Job)SimulatorUI.SimGS.Value.ClassJob != Job.CRP + recipe.CraftType.RowId)
                        {
                            SimulatorUI.SimGS = gs;
                        }

                        if (SimulatorUI.SimGS.Value.ItemLevel < gs.ItemLevel)
                            SimulatorUI.SimGS = gs;
                    }
                }

                var rawSolver = CraftingProcessor.GetSolverForRecipe(config, craft);
                SimulatorUI._selectedSolver = new(rawSolver.Name, rawSolver.Def.Create(craft, rawSolver.Flavour));
            }

            if (ImGui.IsItemHovered())
            {
                ImGuiEx.Tooltip($"Click to open in simulator");
            }


        }
    }
}
