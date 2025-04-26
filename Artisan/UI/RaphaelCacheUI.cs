using Artisan.GameInterop;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using System.Linq;
using System.Numerics;

namespace Artisan.UI
{
    internal static class RaphaelCacheUI
    {
        private static string _search = string.Empty;
        internal static void Draw()
        {
            ImGui.TextWrapped("This tab will allow you to view macros in the Raphael integration cache.");
            ImGui.Separator();

            if (Svc.ClientState.IsLoggedIn && Crafting.CurState is not Crafting.State.IdleNormal and not Crafting.State.IdleBetween)
            {
                ImGui.Text($"Crafting in progress. Macro settings will be unavailable until you stop crafting.");
                return;
            }
            ImGui.Spacing();
            if (ImGui.Button("Clear Raphael Cache (Hold Ctrl)") && ImGui.GetIO().KeyCtrl)
            {
                P.Config.RaphaelSolverCacheV2.Clear();
                P.Config.Save();
            }

            ImGui.InputText($"Search", ref _search, 300);

            if (P.Config.RaphaelSolverCacheV2.Count > 0)
            {
                if (ImGui.BeginChild("##selector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true))
                {
                    ImGuiEx.TextUnderlined($"Level/Progress/Quality/Durability-Craftsmanship/Control/CP-Type");
                    foreach (var key in P.Config.RaphaelSolverCacheV2.Keys)
                    {
                        var m = P.Config.RaphaelSolverCacheV2[key];
                        if (!m.Name.Contains(_search, System.StringComparison.CurrentCultureIgnoreCase)) continue;
                        var selected = ImGui.Selectable($"{m.Name}###{m.ID}");

                        if (selected && !P.ws.Windows.Any(x => x.WindowName.Contains(m.ID.ToString())))
                        {
                            new MacroEditor(m, true);
                        }
                    }

                }
                ImGui.EndChild();
            }
        }
    }
}
