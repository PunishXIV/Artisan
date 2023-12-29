using Artisan.Autocraft;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System.Linq;

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

    public bool Draw(CraftState craft, int widthHint = 0)
    {
        bool changed = false;
        changed |= DrawFood(widthHint);
        changed |= DrawPotion(widthHint);
        changed |= DrawManual(widthHint);
        changed |= DrawSquadronManual(widthHint);
        changed |= DrawSolver(craft, widthHint);
        return changed;
    }

    public bool DrawFood(int widthHint = 0)
    {
        bool changed = false;
        ImGuiEx.TextV("Food Usage:");
        ImGui.SameLine(200f.Scale());
        ImGuiEx.SetNextItemFullWidth(widthHint);
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

    public bool DrawPotion(int widthHint = 0)
    {
        bool changed = false;
        ImGuiEx.TextV("Medicine Usage:");
        ImGui.SameLine(200f.Scale());
        ImGuiEx.SetNextItemFullWidth(widthHint);
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

    public bool DrawManual(int widthHint = 0)
    {
        bool changed = false;
        ImGuiEx.TextV("Manual Usage:");
        ImGui.SameLine(200f.Scale());
        ImGuiEx.SetNextItemFullWidth(widthHint);
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

    public bool DrawSquadronManual(int widthHint = 0)
    {
        bool changed = false;
        ImGuiEx.TextV("Squadron Manual Usage:");
        ImGui.SameLine(200f.Scale());
        ImGuiEx.SetNextItemFullWidth(widthHint);
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

    public bool DrawSolver(CraftState craft, int widthHint = 0)
    {
        bool changed = false;
        ImGuiEx.TextV($"Solver:");
        ImGui.SameLine(200f.Scale());
        ImGuiEx.SetNextItemFullWidth(widthHint);
        var solver = CraftingProcessor.GetSolverForRecipe(this, craft);
        if (ImGui.BeginCombo("##solver", solver.Name))
        {
            foreach (var opt in CraftingProcessor.GetAvailableSolversForRecipe(craft, true))
            {
                bool selected = opt.Def == solver.Def && opt.Flavour == solver.Flavour;
                if (ImGui.Selectable(opt.Name, selected))
                {
                    SolverType = opt.Def.GetType().FullName!;
                    SolverFlavour = opt.Flavour;
                    changed = true;
                }
            }

            ImGui.EndCombo();
        }
        return changed;
    }
}
