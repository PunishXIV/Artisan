using Artisan.Autocraft;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Interface.Colors;
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
    public uint requiredFood = 0;
    public uint requiredPotion = 0;
    public uint requiredManual = 0;
    public uint requiredSquadronManual = 0;
    public bool requiredFoodHQ = true;
    public bool requiredPotionHQ = true;


    public uint RequiredFood => requiredFood == 0 ? P.Config.DefaultConsumables.requiredFood : requiredFood;
    public uint RequiredPotion => requiredPotion == 0 ? P.Config.DefaultConsumables.requiredPotion : requiredPotion;
    public uint RequiredManual => requiredManual == 0 ? P.Config.DefaultConsumables.requiredManual : requiredManual;
    public uint RequiredSquadronManual => requiredSquadronManual == 0 ? P.Config.DefaultConsumables.requiredSquadronManual : requiredSquadronManual;
    public bool RequiredFoodHQ => requiredFood == 0 ? P.Config.DefaultConsumables.requiredFoodHQ : requiredFoodHQ;
    public bool RequiredPotionHQ => requiredPotion == 0 ? P.Config.DefaultConsumables.requiredPotionHQ : requiredPotionHQ;


    public string FoodName => requiredFood == 0 ? $"{P.Config.DefaultConsumables.FoodName} (Default)" : RequiredFood == 1 ? "Disabled" : $"{(RequiredFoodHQ ? " " : "")}{ConsumableChecker.Food.FirstOrDefault(x => x.Id == RequiredFood).Name}";
    public string PotionName => requiredPotion == 0 ? $"{P.Config.DefaultConsumables.PotionName} (Default)" : RequiredPotion == 1 ? "Disabled" : $"{(RequiredPotionHQ ? " " : "")}{ConsumableChecker.Pots.FirstOrDefault(x => x.Id == RequiredPotion).Name}";
    public string ManualName => requiredManual == 0 ? $"{P.Config.DefaultConsumables.ManualName} (Default)" : RequiredManual == 1 ? "Disabled" : $"{ConsumableChecker.Manuals.FirstOrDefault(x => x.Id == RequiredManual).Name}";
    public string SquadronManualName => requiredSquadronManual == 0 ? $"{P.Config.DefaultConsumables.SquadronManualName} (Default)" : RequiredSquadronManual == 1 ? "Disabled" : $"{ConsumableChecker.SquadronManuals.FirstOrDefault(x => x.Id == RequiredSquadronManual).Name}";



    [NonSerialized]
    public Dictionary<string, RaphaelSolutionConfig> TempConfigs = new();

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
        if (ImGui.BeginCombo("##foodBuff", FoodName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable($"Default ({P.Config.DefaultConsumables.FoodName})"))
                {
                    requiredFood = 0;
                    requiredFoodHQ = false;
                    changed = true;
                }
            }
            if (ImGui.Selectable("Disable"))
            {
                requiredFood = 1;
                requiredFoodHQ = false;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetFood(true))
            {
                if (ImGui.Selectable($"{x.Name}"))
                {
                    requiredFood = x.Id;
                    requiredFoodHQ = false;
                    changed = true;
                }
            }
            foreach (var x in ConsumableChecker.GetFood(true, true))
            {
                if (ImGui.Selectable($" {x.Name}"))
                {
                    requiredFood = x.Id;
                    requiredFoodHQ = true;
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
        if (ImGui.BeginCombo("##potBuff", PotionName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable($"Default ({P.Config.DefaultConsumables.PotionName})"))
                {
                    requiredPotion = 0;
                    requiredPotionHQ = false;
                    changed = true;
                }
            }
            if (ImGui.Selectable("Disable"))
            {
                requiredPotion = 1;
                requiredPotionHQ = false;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetPots(true))
            {
                if (ImGui.Selectable($"{x.Name}"))
                {
                    requiredPotion = x.Id;
                    requiredPotionHQ = false;
                    changed = true;
                }
            }
            foreach (var x in ConsumableChecker.GetPots(true, true))
            {
                if (ImGui.Selectable($" {x.Name}"))
                {
                    requiredPotion = x.Id;
                    requiredPotionHQ = true;
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
        if (ImGui.BeginCombo("##manualBuff", ManualName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable($"Default ({P.Config.DefaultConsumables.ManualName})"))
                {
                    requiredManual = 0;
                    changed = true;
                }
            }
            if (ImGui.Selectable("Disable"))
            {
                requiredManual = 1;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetManuals(true))
            {
                if (ImGui.Selectable($"{x.Name}"))
                {
                    requiredManual = x.Id;
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
        if (ImGui.BeginCombo("##squadronManualBuff", SquadronManualName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable($"Default ({P.Config.DefaultConsumables.SquadronManualName})"))
                {
                    requiredSquadronManual = 0;
                    changed = true;
                }
            }
            if (ImGui.Selectable("Disable"))
            {
                requiredSquadronManual = 1;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetSquadronManuals(true))
            {
                if (ImGui.Selectable($"{x.Name}"))
                {
                    requiredSquadronManual = x.Id;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    public bool DrawSolver(CraftState craft, bool hasButton = false)
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

        if (RaphaelCache.CLIExists())
        {
            var hasSolution = RaphaelCache.HasSolution(craft, out var solution);
            var key = RaphaelCache.GetKey(craft);

            if (!TempConfigs.ContainsKey(key))
            {
                TempConfigs.Add(key, new());
                TempConfigs[key].HQConsiderations = P.Config.RaphaelSolverConfig.AllowHQConsiderations;
                TempConfigs[key].EnsureReliability = P.Config.RaphaelSolverConfig.AllowEnsureReliability;
                TempConfigs[key].BackloadProgress = P.Config.RaphaelSolverConfig.AllowBackloadProgress;
                TempConfigs[key].HeartAndSoul = P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist;
                TempConfigs[key].QuickInno = P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist;
            }

            if (hasSolution)
            {
                var opt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                var solverIsRaph = SolverType == opt?.Def.GetType().FullName!;
                var curStats = CharacterStats.GetCurrentStats();
                //Svc.Log.Debug($"{curStats.Craftsmanship}/{craft.StatCraftsmanship} - {curStats.Control}/{craft.StatControl} - {curStats.CP}/{craft.StatCP}");
                if (craft.StatCraftsmanship != curStats.Craftsmanship && solverIsRaph)
                {
                    ImGuiEx.Text(ImGuiColors.DalamudRed, $"Your current Craftsmanship does not match the generated result.\nThis solver won't be used until they match.\n(You may just need to have the correct buffs applied)");
                }

                if (!solverIsRaph)
                {
                    ImGuiEx.TextCentered($"Raphael Solution Has Been Generated. (Click to Switch)");
                    if (ImGui.IsItemClicked())
                    {
                        SolverType = opt?.Def.GetType().FullName!;
                        SolverFlavour = (int)(opt?.Flavour);
                        changed = true;
                    }
                }
            }
            else
            {
                if (P.Config.RaphaelSolverConfig.AutoGenerate && CraftingProcessor.GetAvailableSolversForRecipe(craft, true).Count() > 0)
                {
                    RaphaelCache.Build(craft, TempConfigs[key]);
                }
            }

            ImGui.Separator();
            var inProgress = RaphaelCache.InProgress(craft);
            var raphChanges = false;

            if (inProgress)
                ImGui.BeginDisabled();

            if (P.Config.RaphaelSolverConfig.AllowHQConsiderations)
                raphChanges |= ImGui.Checkbox($"Allow Quality Considerations##{key}Quality", ref TempConfigs[key].HQConsiderations);
            if (P.Config.RaphaelSolverConfig.AllowEnsureReliability)
                raphChanges |= ImGui.Checkbox($"Ensure reliability##{key}Reliability", ref TempConfigs[key].EnsureReliability);
            if (P.Config.RaphaelSolverConfig.AllowBackloadProgress)
                raphChanges |= ImGui.Checkbox($"Backload progress##{key}Progress", ref TempConfigs[key].BackloadProgress);
            if (P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist)
                raphChanges |= ImGui.Checkbox($"Allow heart and soul usage##{key}HS", ref TempConfigs[key].HeartAndSoul);
            if (P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist)
                raphChanges |= ImGui.Checkbox($"Allow quick innovation usage##{key}QI", ref TempConfigs[key].QuickInno);

            changed |= raphChanges;

            if (inProgress)
                ImGui.EndDisabled();

            if (!inProgress)
            {
                if (ImGui.Button("Build Raphael Solution", new Vector2(ImGui.GetContentRegionAvail().X, 25f.Scale())))
                {
                    RaphaelCache.Build(craft, TempConfigs[key]);
                }
            }
            else
            {
                if (ImGui.Button("Cancel Raphael Generation", new Vector2(ImGui.GetContentRegionAvail().X, 25f.Scale())))
                {
                    RaphaelCache.Tasks.TryRemove(key, out var task);
                    task.Item1.Cancel();
                }
            }

            if (inProgress)
            {
                ImGuiEx.TextCentered("Generating...");
            }
        }

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

    private void CleanRaphaelMacro(string key)
    {
        Svc.Log.Debug("Clearing macro due to settings changes");
        TempConfigs[key].Macro = ""; // clear macro if settings have changed
        TempConfigs[key].HasChanges = true;
    }
}
