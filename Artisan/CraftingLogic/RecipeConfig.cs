using Artisan.Autocraft;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Numerics;

namespace Artisan.CraftingLogic;

[Serializable]
public class RecipeConfig
{
    public const uint Default = 0;
    public const uint Disabled = 1;

    [NonSerialized]
    public string TempSolverType = "";
    [NonSerialized]
    public int TempSolverFlavour = -1;

    public string CurrentSolverType => TempSolverType != "" ? TempSolverType : SolverType;
    public int CurrentSolverFlavour => TempSolverFlavour != -1 ? TempSolverFlavour : SolverFlavour;

    [NonSerialized]
    public uint TempRequiredFood = 0;
    [NonSerialized]
    public bool TempFoodHQ = true;
    [NonSerialized]
    public uint TempRequiredPotion = 0;
    [NonSerialized]
    public bool TempPotionHQ = true;
    [NonSerialized]
    public uint TempRequiredManual = 0;
    [NonSerialized]
    public uint TempRequiredSquadronManual = 0;

    [NonSerialized]
    public int? TempExpertProfileID = null;
    [NonSerialized]
    public uint? TempExpertMaxSteadyUses = null;
    [NonSerialized]
    public bool? TempExpertUseMaterialMiracle = null;
    [NonSerialized]
    public uint? TempExpertMinimumStepsBeforeMiracle = null;

    public string SolverType = ""; // TODO: ideally it should be a Type?, but that causes problems for serialization
    public int SolverFlavour;
    public int expertProfileID = (int)Default;

    public uint expertMaxSteadyUses = Default;
    public bool expertUseMaterialMiracle = false;
    public uint expertMinimumStepsBeforeMiracle = Default;

    public uint requiredFood = Default;
    public uint requiredPotion = Default;
    public uint requiredManual = Default;
    public uint requiredSquadronManual = Default;
    public bool requiredFoodHQ = true;
    public bool requiredPotionHQ = true;


    public bool FoodEnabled => RequiredFood != Disabled;
    public bool PotionEnabled => RequiredPotion != Disabled;
    public bool ManualEnabled => RequiredManual != Disabled;
    public bool SquadronManualEnabled => RequiredSquadronManual != Disabled;


    public uint RequiredFood => TempRequiredFood != 0 ? TempRequiredFood : (requiredFood == Default ? P.Config.DefaultConsumables.requiredFood : requiredFood);
    public uint RequiredPotion => TempRequiredPotion != 0 ? TempRequiredPotion : (requiredPotion == Default ? P.Config.DefaultConsumables.requiredPotion : requiredPotion);
    public uint RequiredManual => TempRequiredManual != 0 ? TempRequiredManual : (requiredManual == Default ? P.Config.DefaultConsumables.requiredManual : requiredManual);
    public uint RequiredSquadronManual => TempRequiredSquadronManual != 0 ? TempRequiredSquadronManual : (requiredSquadronManual == Default ? P.Config.DefaultConsumables.requiredSquadronManual : requiredSquadronManual);
    public bool RequiredFoodHQ => TempRequiredFood != 0 ? TempFoodHQ : (requiredFood == Default ? P.Config.DefaultConsumables.requiredFoodHQ : requiredFoodHQ);
    public bool RequiredPotionHQ => TempRequiredPotion != 0 ? TempPotionHQ : (requiredPotion == Default ? P.Config.DefaultConsumables.requiredPotionHQ : requiredPotionHQ);


    public string FoodName => requiredFood == Default && TempRequiredFood == 0 ? $"{P.Config.DefaultConsumables.FoodName} (Default)" : RequiredFood == Disabled ? "Disabled" : $"{(RequiredFoodHQ ? " " : "")}{ConsumableChecker.Food.FirstOrDefault(x => x.Id == RequiredFood).Name} (Qty: {ConsumableChecker.NumberOfConsumable(RequiredFood, RequiredFoodHQ)})";
    public string PotionName => requiredPotion == Default && TempRequiredPotion == 0 ? $"{P.Config.DefaultConsumables.PotionName} (Default)" : RequiredPotion == Disabled ? "Disabled" : $"{(RequiredPotionHQ ? " " : "")}{ConsumableChecker.Pots.FirstOrDefault(x => x.Id == RequiredPotion).Name} (Qty: {ConsumableChecker.NumberOfConsumable(RequiredPotion, RequiredPotionHQ)})";
    public string ManualName => requiredManual == Default && TempRequiredManual == 0 ? $"{P.Config.DefaultConsumables.ManualName} (Default)" : RequiredManual == Disabled ? "Disabled" : $"{ConsumableChecker.Manuals.FirstOrDefault(x => x.Id == RequiredManual).Name} (Qty: {ConsumableChecker.NumberOfConsumable(RequiredManual, false)})";
    public string SquadronManualName => requiredSquadronManual == Default && TempRequiredSquadronManual == 0 ? $"{P.Config.DefaultConsumables.SquadronManualName} (Default)" : RequiredSquadronManual == Disabled ? "Disabled" : $"{ConsumableChecker.SquadronManuals.FirstOrDefault(x => x.Id == RequiredSquadronManual).Name} (Qty: {ConsumableChecker.NumberOfConsumable(RequiredSquadronManual, false)})";

    public int ExpertProfileID => TempExpertProfileID ?? expertProfileID;
    public uint ExpertMaxSteadyUses => TempExpertMaxSteadyUses ?? expertMaxSteadyUses;
    public bool ExpertUseMaterialMiracle => TempExpertUseMaterialMiracle ?? expertUseMaterialMiracle;
    public uint ExpertMinimumStepsBeforeMiracle => TempExpertMinimumStepsBeforeMiracle ?? expertMinimumStepsBeforeMiracle;

    public float GetLargestName()
    {
        try
        {
            return 32f + 350f; //Bandaid fix for the time being as below might crash
            var ret = Math.Max(Math.Max(Math.Max(ImGui.CalcTextSize(FoodName).X, ImGui.CalcTextSize(PotionName).X), ImGui.CalcTextSize(ManualName).X), ImGui.CalcTextSize(SquadronManualName).X) + 32f;
            return ret;
        }
        catch (Exception ex)
        {
            ex.Log();
            return 0;
        }
    }

    public bool SolverIsRaph => CurrentSolverType == typeof(RaphaelSolverDefintion).FullName!;
    public bool SolverIsStandard => CurrentSolverType == typeof(StandardSolverDefinition).FullName!;
    public bool SolverIsExpert => CurrentSolverType == typeof(ExpertSolverDefinition).FullName!;

    public bool Draw(uint recipeId)
    {
        var recipe = LuminaSheets.RecipeSheet[recipeId];
        ImGuiEx.LineCentered($"###RecipeName{recipeId}", () => { ImGuiEx.TextUnderlined($"{recipe.ItemResult.Value.Name.ToDalamudString().ToString()}"); });
        var config = this;
        var stats = CharacterStats.GetBaseStatsForClassHeuristic((Job)((uint)Job.CRP + recipe.CraftType.RowId));
        stats.AddConsumables(new(config.RequiredFood, config.RequiredFoodHQ), new(config.RequiredPotion, config.RequiredPotionHQ), CharacterInfo.FCCraftsmanshipbuff);
        var craft = Crafting.BuildCraftStateForRecipe(stats, (Job)((uint)Job.CRP + recipe.CraftType.RowId), recipe);
        if (craft.InitialQuality == 0)
            craft.InitialQuality = Simulator.GetStartingQuality(recipe, false, craft.StatLevel);
        var liveStats = Player.ClassJob.RowId == craft.Recipe.CraftType.RowId + 8;
        bool changed = false;
        changed |= DrawFood();
        changed |= DrawPotion();
        changed |= DrawManual();
        changed |= DrawSquadronManual();
        changed |= DrawSolver(craft, liveStats: liveStats);
        changed |= DrawExpertProfiles(craft);
        DrawWarnings(craft);
        RaphaelCache.DrawRaphaelDropdown(craft, liveStats);
        DrawSimulator(craft);
        return changed;
    }

    public bool DrawFood(bool hasButton = false)
    {
        bool changed = false;
        ImGuiEx.TextV("Food Usage:");
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
        else ImGui.PushItemWidth(GetLargestName());
        if (ImGui.BeginCombo("##foodBuff", FoodName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable($"{P.Config.DefaultConsumables.FoodName} (Default)"))
                {
                    requiredFood = Default;
                    requiredFoodHQ = false;
                    changed = true;
                }
            }
            if (ImGui.Selectable("Disable"))
            {
                requiredFood = Disabled;
                requiredFoodHQ = false;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetFood(true))
            {
                if (ImGui.Selectable($"{x.Name} (Qty: {ConsumableChecker.NumberOfConsumable(x.Id, false)})"))
                {
                    requiredFood = x.Id;
                    requiredFoodHQ = false;
                    changed = true;
                }
            }
            foreach (var x in ConsumableChecker.GetFood(true, true))
            {
                if (ImGui.Selectable($" {x.Name} (Qty: {ConsumableChecker.NumberOfConsumable(x.Id, true)})"))
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
        else ImGui.PushItemWidth(GetLargestName());
        if (ImGui.BeginCombo("##potBuff", PotionName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable($"{P.Config.DefaultConsumables.PotionName} (Default)"))
                {
                    requiredPotion = Default;
                    requiredPotionHQ = false;
                    changed = true;
                }
            }
            if (ImGui.Selectable("Disable"))
            {
                requiredPotion = Disabled;
                requiredPotionHQ = false;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetPots(true))
            {
                if (ImGui.Selectable($"{x.Name} (Qty: {ConsumableChecker.NumberOfConsumable(x.Id, false)})"))
                {
                    requiredPotion = x.Id;
                    requiredPotionHQ = false;
                    changed = true;
                }
            }
            foreach (var x in ConsumableChecker.GetPots(true, true))
            {
                if (ImGui.Selectable($" {x.Name} (Qty: {ConsumableChecker.NumberOfConsumable(x.Id, true)})"))
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
        else ImGui.PushItemWidth(GetLargestName());
        if (ImGui.BeginCombo("##manualBuff", ManualName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable($"{P.Config.DefaultConsumables.ManualName} (Default)"))
                {
                    requiredManual = Default;
                    changed = true;
                }
            }
            if (ImGui.Selectable("Disable"))
            {
                requiredManual = Disabled;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetManuals(true))
            {
                if (ImGui.Selectable($"{x.Name} (Qty: {ConsumableChecker.NumberOfConsumable(x.Id, false)})"))
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
        else ImGui.PushItemWidth(GetLargestName());
        if (ImGui.BeginCombo("##squadronManualBuff", SquadronManualName))
        {
            if (this != P.Config.DefaultConsumables)
            {
                if (ImGui.Selectable($"{P.Config.DefaultConsumables.SquadronManualName} (Default)"))
                {
                    requiredSquadronManual = Default;
                    changed = true;
                }
            }
            if (ImGui.Selectable("Disable"))
            {
                requiredSquadronManual = Disabled;
                changed = true;
            }
            foreach (var x in ConsumableChecker.GetSquadronManuals(true))
            {
                if (ImGui.Selectable($"{x.Name} (Qty: {ConsumableChecker.NumberOfConsumable(x.Id, false)})"))
                {
                    requiredSquadronManual = x.Id;
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
        var solver = CraftingProcessor.GetSolverForRecipe(this, craft);
        bool exists = P.Config.RecipeConfigs.ContainsKey(craft.RecipeId);
        if (!exists && P.Config.RaphaelSolverConfig.DefaultRaphSolver)
        {
            this.SolverFlavour = 3;
            this.SolverType = typeof(RaphaelSolverDefintion).FullName!;
            changed = true;
        }
        if (string.IsNullOrEmpty(solver.Name))
        {
            ImGuiEx.Text(ImGuiColors.DalamudRed, "Unable to select default solver. Please select from dropdown.");
        }
        ImGuiEx.TextV($"Solver:");
        ImGui.SameLine(130f.Scale());
        if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);

        if (ImGui.BeginCombo("##solver", solver.Name))
        {
            foreach (var opt in CraftingProcessor.GetAvailableSolversForRecipe(craft, true).OrderBy(x => x.Priority))
            {
                if (opt == default) continue;
                if (opt.UnsupportedReason.Length > 0)
                {
                    ImGui.Text($"{opt.Name} is unsupported - {opt.UnsupportedReason}");
                }
                else
                {
                    bool selected = opt.Name == solver.Name;
                    if (ImGui.Selectable(opt.Name, selected))
                    {
                        IPC.IPC.SetTempSolverBackToNormal(craft.RecipeId);
                        SolverType = opt.Def.GetType().FullName!;
                        SolverFlavour = opt.Flavour;
                        changed = true;
                    }
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    public bool DrawExpertProfiles(CraftState craft, bool hasButton = false)
    {
        bool changed = false;
        if (this.CurrentSolverType.Contains("Expert") || this.CurrentSolverType == "" && craft.CraftExpert)
        {
            var expertProfile = CraftingProcessor.GetExpertProfileForRecipe(this);
            if (string.IsNullOrEmpty(expertProfile.Name))
            {
                ImGuiEx.Text(ImGuiColors.DalamudRed, "Unable to select an expert solver profile. Please select from dropdown.");
            }

            ImGuiEx.TextV($"Expert Profile:");
            ImGui.SameLine();

            ImGuiEx.IconWithTooltip(new Vector4(0.5f, 0.5f, 0.5f, 1f), FontAwesomeIcon.PencilAlt, "Add or edit expert solver profiles");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                P.PluginUi.OpenWindow = UI.OpenWindow.ExpertProfiles;
                P.PluginUi.IsOpen = true;
            }
            ImGui.SameLine(130f.Scale());

            if (hasButton) ImGuiEx.SetNextItemFullWidth(-120);
            if (ImGui.BeginCombo("##expertProfile", expertProfile.Name))
            {
                foreach (var c in P.Config.ExpertSolverProfiles.GetExpertProfilesWithDefault())
                {
                    bool selected = c.Name == expertProfile.Name;
                    if (ImGui.Selectable(c.Name, selected))
                    {
                        expertProfileID = c.ID;
                        changed = true;
                    }
                }
                ImGui.EndCombo();
            }
        }
        return changed;
    }

    public void DrawWarnings(CraftState craft)
    {
        if (!Crafting.EnoughDelinsForCraft(this, craft, out var req))
        {
            ImGuiEx.TextCentered(ImGuiColors.DalamudRed, $"You do not have enough {Svc.Data.GetExcelSheet<Item>().GetRow(28724).Name} for this solver ({req} required).");
            if (this.CurrentSolverType.Contains("Raphael"))
            {
                ImGuiEx.TextCentered(ImGuiColors.DalamudYellow, $"An alternative solution will be used/generated when you start crafting.");
            }
        }

        if (ConsumableChecker.SkippingConsumablesByConfig(craft.Recipe))
            ImGuiEx.Text(ImGuiColors.DalamudRed, "Consumables will not be used due to level difference setting.");
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
                    ImGuiEx.TextCentered($"This would use Material Miracle, which is not compatible with the simulator.");
                else
                    if (solver.Name == "Raphael Recipe Solver" && !RaphaelCache.HasSolution(craft, out _))
                        ImGuiEx.TextCentered($"Unable to generate a simulator without a Raphael solution generated.");
                    else
                        ImGuiEx.TextCentered(hintColor, solverHint);
            }
            else
                ImGuiEx.TextCentered($"Please run this recipe in the simulator for results.");

            if (ImGui.IsItemClicked())
            {
                P.PluginUi.OpenWindow = UI.OpenWindow.Simulator;
                P.PluginUi.IsOpen = true;
                SimulatorUI.SelectedRecipe = recipe;
                SimulatorUI.ResetSim();
                if (config.PotionEnabled)
                {
                    SimulatorUI.SimMedicine ??= new();
                    SimulatorUI.SimMedicine.Id = config.RequiredPotion;
                    SimulatorUI.SimMedicine.ConsumableHQ = config.RequiredPotionHQ;
                    SimulatorUI.SimMedicine.Stats = new ConsumableStats(config.RequiredPotion, config.RequiredPotionHQ);
                }
                if (config.FoodEnabled)
                {
                    SimulatorUI.SimFood ??= new();
                    SimulatorUI.SimFood.Id = config.RequiredFood;
                    SimulatorUI.SimFood.ConsumableHQ = config.RequiredFoodHQ;
                    SimulatorUI.SimFood.Stats = new ConsumableStats(config.RequiredFood, config.RequiredFoodHQ);
                }

                foreach (ref var gs in RaptureGearsetModule.Instance()->Entries)
                {
                    if ((Job)gs.ClassJob == (Job)((uint)Job.CRP + recipe.CraftType.RowId))
                    {
                        if (SimulatorUI.SimGS is null || (Job)SimulatorUI.SimGS.Value.ClassJob != (Job)((uint)Job.CRP + recipe.CraftType.RowId))
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
