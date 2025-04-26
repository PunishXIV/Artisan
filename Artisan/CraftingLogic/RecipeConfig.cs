using Artisan.Autocraft;
using Artisan.CraftingLogic.Solvers;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System;
using ECommons.DalamudServices;

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

    [NonSerialized]
    public Dictionary<string, RaphaelSolutionConfig> TempConfigs = new();

    public bool Draw(CraftState craft)
    {
        bool changed = false;
        changed |= DrawFood();
        changed |= DrawPotion();
        changed |= DrawManual();
        changed |= DrawSquadronManual();
        changed |= DrawSolver(craft);
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

            if (!TempConfigs.ContainsKey(key) && solution == null)
            {
                TempConfigs.Add(key, new());
            }
            else if (!TempConfigs.ContainsKey(key))
            {
                TempConfigs.Add(key, solution!);
            }

            if (hasSolution)
            {

                ImGuiEx.TextCentered($"Raphael Solution Has Been Generated. (Click to Switch)");
                if (ImGui.IsItemClicked())
                {
                    var opt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).First(x => x.Name == "Raphael Recipe Solver");
                    SolverType = opt.Def.GetType().FullName!;
                    SolverFlavour = opt.Flavour;
                    changed = true;
                }
            }

            var inProgress = RaphaelCache.InProgress(craft);
            var raphChanges = false;

            if (inProgress)
                ImGui.BeginDisabled();

            if (P.Config.RaphaelSolverConfig.AllowHQConsiderations)
                raphChanges |= ImGui.Checkbox("Allow Quality Considerations", ref TempConfigs[key].HQConsiderations);
            if (P.Config.RaphaelSolverConfig.AllowEnsureReliability)
                raphChanges |= ImGui.Checkbox("Ensure reliability", ref TempConfigs[key].EnsureReliability);
            if (P.Config.RaphaelSolverConfig.AllowBackloadProgress)
                raphChanges |= ImGui.Checkbox("Backload progress", ref TempConfigs[key].BackloadProgress);
            if (P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist)
                raphChanges |= ImGui.Checkbox("Allow heart and soul usage", ref TempConfigs[key].HeartAndSoul);
            if (P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist)
                raphChanges |= ImGui.Checkbox("Allow quick innovation usage", ref TempConfigs[key].QuickInno);

            if (raphChanges)
            {
                CleanRaphaelMacro(key);
                changed = true;
            }

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
    
    private void CleanRaphaelMacro(string key)
    {
        Svc.Log.Debug("Clearing macro due to settings changes");
        TempConfigs[key].Macro = ""; // clear macro if settings have changed
        TempConfigs[key].HasChanges = true;
    }
}
