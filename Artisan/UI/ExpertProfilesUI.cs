using Artisan.CraftingLogic.Solvers;
using Artisan.UI.Tables;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using OtterGui;
using OtterGui.Filesystem;
using PunishLib.ImGuiMethods;
using System;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using static Artisan.CraftingLogic.Solvers.ExpertSolverProfiles;

namespace Artisan.UI
{
    internal class ExpertProfilesUI
    {
        internal static ExpertProfile selectedProfile = new();
        public static bool Processing;
        private static readonly ExpertProfileList EPL = new();

        internal static void Draw()
        {
            try
            {
                ImGui.TextWrapped($"An expert solver profile is a snapshot, or \"loadout\", of specific expert solver settings. Like macros, different profiles can be assigned to specific expert recipes.");

                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"IMPORTANT: These are not advanced settings or \"expert user\" profiles. They are exclusively for the expert recipe solver.");
                var expertIcon = P.PluginUi.ExpertSettingsUI.expertIcon;
                if (expertIcon != null)
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"Expert recipes have this icon in the crafting log:");
                    ImGui.SameLine();
                    ImGui.Image(expertIcon.Handle, expertIcon.Size, new Vector2(0, 0), new Vector2(1, 1), new Vector4(0.94f, 0.57f, 0f, 1f));
                }

                ImGui.Dummy(new Vector2(0, 5f));
                if (IconButtons.IconTextButton(Dalamud.Interface.FontAwesomeIcon.ExternalLinkAlt, "Edit Global Expert Solver Settings"))
                {
                    P.PluginUi.OpenWindow = OpenWindow.Main;
                }

                ImGui.Dummy(new Vector2(0, 10f));
                ImGui.TextWrapped("Left click a profile to edit. Right click a profile to select it without editing.");

                ImGui.Dummy(new Vector2(0, 5f));
                ImGui.Separator();
                ImGui.Dummy(new Vector2(0, 5f));

                ImGui.BeginChild("ProfileSelector", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 200f));
                EPL.Draw(ImGui.GetContentRegionAvail().X);
                ImGui.EndChild();

                ImGui.Spacing();
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }
    }

    internal class ExpertProfileList : ItemSelector<ExpertProfile>
    {
        public ExpertProfileList()
            : base(P.Config.ExpertSolverProfiles.ExpertProfiles, Flags.Add | Flags.Delete | Flags.Move | Flags.Filter | Flags.Duplicate)
        {
            CurrentIdx = -1;
        }

        protected override string AddButtonTooltip()
        {
            return "Add new profile";
        }

        protected override string DeleteButtonTooltip()
        {
            return "Permanently delete selected profile\r\n(hold Ctrl to confirm)";
        }

        protected override bool Filtered(int idx)
        {
            return Filter.Length != 0 && !Items[idx].Name.Contains(
                       Filter,
                       StringComparison.InvariantCultureIgnoreCase);
        }

        protected override bool OnAdd(string name)
        {
            Svc.Log.Information($"OnAdd");
            try
            {
                var profile = new ExpertProfile { Name = name, Settings = new ExpertSolverSettings() };
                P.Config.ExpertSolverProfiles.AddNewExpertProfile(profile);
                P.Config.Save();

                return true;
            }
            catch (Exception ex)
            {
                ex.Log();
                return false;
            }
        }

        protected override bool OnDelete(int idx)
        {
            if (P.ws.Windows.TryGetFirst(
                    x => x.WindowName.Contains(ExpertProfilesUI.selectedProfile.ID.ToString()) && x.GetType() == typeof(ExpertProfileEditor),
                    out var window))
            {
                P.ws.RemoveWindow(window);
            }

            P.Config.ExpertSolverProfiles.ExpertProfiles.RemoveAt(idx);
            P.Config.Save();

            if (!ExpertProfilesUI.Processing)
                ExpertProfilesUI.selectedProfile = new ExpertProfile();
            return true;
        }

        protected override bool OnDraw(int idx, out bool changes)
        {
            changes = false;
            if (ExpertProfilesUI.Processing && ExpertProfilesUI.selectedProfile.ID == P.Config.ExpertSolverProfiles.ExpertProfiles[idx].ID)
                ImGui.BeginDisabled();

            using var id = ImRaii.PushId(idx);
            var selected = ImGui.Selectable($"{P.Config.ExpertSolverProfiles.ExpertProfiles[idx].Name} (ID: {P.Config.ExpertSolverProfiles.ExpertProfiles[idx].ID})", idx == CurrentIdx);
            if (selected)
            {
                if (!P.ws.Windows.Any(x => x.WindowName.Contains(P.Config.ExpertSolverProfiles.ExpertProfiles[idx].ID.ToString())))
                {
                    Interface.SetupValues();
                    ExpertProfileEditor editor = new(P.Config.ExpertSolverProfiles.ExpertProfiles[idx].ID);
                }
                else
                {
                    P.ws.Windows.TryGetFirst(
                        x => x.WindowName.Contains(P.Config.ExpertSolverProfiles.ExpertProfiles[idx].ID.ToString()),
                        out var window);
                    window.BringToFront();
                }

                if (!ExpertProfilesUI.Processing)
                    ExpertProfilesUI.selectedProfile = P.Config.ExpertSolverProfiles.ExpertProfiles[idx];
            }

            if (!ExpertProfilesUI.Processing)
            {
                if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                {
                    if (CurrentIdx == idx)
                    {
                        CurrentIdx = -1;
                        ExpertProfilesUI.selectedProfile = new ExpertProfile();
                    }
                    else
                    {
                        CurrentIdx = idx;
                        ExpertProfilesUI.selectedProfile = P.Config.ExpertSolverProfiles.ExpertProfiles[idx];
                    }
                }
            }

            if (ExpertProfilesUI.Processing && ExpertProfilesUI.selectedProfile.ID == P.Config.ExpertSolverProfiles.ExpertProfiles[idx].ID)
                ImGui.EndDisabled();

            return selected;
        }

        protected override bool OnDuplicate(string name, int idx)
        {
            var baseProfile = P.Config.ExpertSolverProfiles.ExpertProfiles[idx];
            ExpertProfile newProfile = new ExpertProfile();
            newProfile = baseProfile.JSONClone();
            newProfile.Name = name;
            P.Config.ExpertSolverProfiles.AddNewExpertProfile(newProfile);
            P.Config.Save();
            return true;
        }

        protected override bool OnMove(int idx1, int idx2)
        {
            P.Config.ExpertSolverProfiles.ExpertProfiles.Move(idx1, idx2);
            return true;
        }
    }
}
