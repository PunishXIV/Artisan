using Artisan.GameInterop;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using Dalamud.Bindings.ImGui;
using System.Linq;
using System.Numerics;
using Artisan.UI.Tables;
using ECommons;
using Artisan.CraftingLogic.Solvers;

namespace Artisan.UI
{
    internal class RaphaelCacheUI
    {
        public RaphaelCacheTable? Table;

        internal void Draw()
        {
            try
            {
                ImGui.TextWrapped("This tab shows all of the currently saved Raphael-generated macros.");

                if (Svc.ClientState.IsLoggedIn && Crafting.CurState is not Crafting.State.IdleNormal and not Crafting.State.IdleBetween)
                {
                    ImGui.Text($"Crafting in progress. Macro settings will be unavailable until you stop crafting.");
                    return;
                }
                ImGui.Spacing();

                ImGui.TextWrapped($"Currently saved macros: {P.Config.RaphaelSolverCacheV6.Keys.Count}");
                ImGui.Spacing();

                if (ImGui.BeginChild("##selector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 32f.Scale()), true))
                {
                    // todo: search by recipe?
                    if (Table == null)
                    {
                        var cacheList = P.Config.RaphaelSolverCacheV6.Keys.ToList();
                        Table = new(cacheList);
                    }
                    Table.Draw(ImGui.GetTextLineHeightWithSpacing() - 4f);
                }
                ImGui.EndChild();

                var filterActive = Table.FilteredItems.Count != 0 && Table.FilteredItems.Count != P.Config.RaphaelSolverCacheV6.Keys.Count;
                var filterCount = filterActive ? $"{Table.FilteredItems.Count} " : "";

                if (!filterActive) ImGui.BeginDisabled();
                if (ImGuiEx.ButtonCtrl($"Delete {filterCount}Filtered Macro{(Table.FilteredItems.Count == 1 ? "" : "s")}", new Vector2(ImGui.GetContentRegionAvail().X / 2, ImGui.GetContentRegionAvail().Y)))
                {
                    var toDelete = Table.FilteredItems.JSONClone();
                    foreach ((RaphaelOptions key, int _) in toDelete)
                    {
                        P.Config.RaphaelSolverCacheV6.TryRemove(key, out _);
                    }
                    Table.FilteredItems.Clear();
                    P.Config.Save();
                }
                if (!filterActive) ImGui.EndDisabled();

                ImGui.SameLine();

                if (ImGuiEx.ButtonCtrl($"Delete Entire Cache", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y)))
                {
                    P.Config.RaphaelSolverCacheV6.Clear();
                    P.Config.Save();
                }
            }
            catch { }
        }
    }
}
