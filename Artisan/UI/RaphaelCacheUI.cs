using Artisan.GameInterop;
using ECommons.DalamudServices;
using ImGuiNET;
using System.Linq;
using System.Numerics;

namespace Artisan.UI
{
    internal static class RaphaelCacheUI
    {
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

            if (P.Config.RaphaelSolverCache.Count > 0)
            {
                if (ImGui.BeginChild("##selector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true))
                {
                    foreach (var key in P.Config.RaphaelSolverCache.Keys)
                    {
                        var m = P.Config.RaphaelSolverCache[key];
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
