using Artisan.CraftingLogic.Solvers;
using Artisan.RawInformation;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using OtterGui.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TerraFX.Interop.Windows;
using static System.Net.Mime.MediaTypeNames;

namespace Artisan.UI.Tables
{
    // todo: ideally this would highlight the entire row as a single Selectable instead of every cell separately
    internal class ClickableColumn : ColumnString<RaphaelOptions>
    {
        public override void DrawColumn(RaphaelOptions key, int _)
        {
            if (ImGui.Selectable(this.ToName(key)))
            {
                var m = P.Config.RaphaelSolverCacheV6[key];
                if (!P.ws.Windows.Any(x => x.WindowName.Contains(m.ID.ToString())))
                {
                    new MacroEditor(m, true);
                }
            }
        }
    }

    internal class RaphaelCacheTable : Table<RaphaelOptions>, IDisposable
    {
        private static float _colWidthLevel;
        private static float _colWidthProgress;
        private static float _colWidthQuality;
        private static float _colWidthDurability;
        private static float _colWidthCraftsmanship;
        private static float _colWidthControl;
        private static float _colWidthCP;
        private static float _colWidthIsExpert;
        private static float _colWidthInitialQuality;
        private static float _colWidthSpecialist;
        private static float _colWidthSteadyHands;
        private static float _colWidthUseHeartAndSoul;
        private static float _colWidthUseQuickInno;
        private static float _colWidthHasManipulation;
        private static float _colWidthEnsureReliability;
        private static float _colWidthBackloadProgress;
        private static float _scale;

        public readonly LevelColumn _colLevel = new() { Label = "Level" };
        public readonly ProgressColumn _colProgress = new() { Label = "Progress" };
        public readonly QualityColumn _colQuality = new() { Label = "Quality" };
        public readonly DurabilityColumn _colDurability = new() { Label = "Dura" };
        public readonly CraftsmanshipColumn _colCraftsmanship = new() { Label = "Craftsmanship" };
        public readonly ControlColumn _colControl = new() { Label = "Control" };
        public readonly CPColumn _colCP = new() { Label = "CP" };
        public readonly IsExpertColumn _colIsExpert = new() { Label = "Expert" };
        public readonly InitialQualityColumn _colInitialQuality = new() { Label = "Init. Q" };
        public readonly SpecialistColumn _colSpecialist = new() { Label = "Specialist" };
        public readonly SteadyHandsColumn _colSteadyHands = new() { Label = "Steady" };
        public readonly UseHeartAndSoulColumn _colUseHeartAndSoul = new() { Label = "H&S" };
        public readonly UseQuickInnoColumn _colUseQuickInno = new() { Label = "QI" };
        public readonly HasManipulationColumn _colHasManipulation = new() { Label = "Manip" };
        public readonly EnsureReliabilityColumn _colEnsureReliability = new() { Label = "Ensure" };
        public readonly BackloadProgressColumn _colBackloadProgress = new() { Label = "Backload" };

        private static float TextWidth(string text) => ImGui.CalcTextSize(text).X + ImGui.GetStyle().ItemSpacing.X;

        protected override void PreDraw()
        {
            if (_scale != ImGuiHelpers.GlobalScale)
            {
                _scale = ImGuiHelpers.GlobalScale;
                _colWidthLevel = TextWidth(_colLevel.Label) / _scale + Table.ArrowWidth;
                _colWidthProgress = TextWidth(_colProgress.Label) / _scale + Table.ArrowWidth;
                _colWidthQuality = TextWidth(_colQuality.Label) / _scale + Table.ArrowWidth;
                _colWidthDurability = TextWidth(_colDurability.Label) / _scale + Table.ArrowWidth;
                _colWidthCraftsmanship = TextWidth(_colCraftsmanship.Label) / _scale + Table.ArrowWidth;
                _colWidthControl = TextWidth(_colControl.Label) / _scale + Table.ArrowWidth;
                _colWidthCP = Items.Max(i => TextWidth(i.MinCP.ToString())) / _scale + Table.ArrowWidth;
                _colWidthIsExpert = TextWidth("Expert") / _scale + Table.ArrowWidth;
                _colWidthInitialQuality = TextWidth(_colInitialQuality.Label) / _scale + Table.ArrowWidth;
                _colWidthSpecialist = TextWidth("Yes") / _scale + Table.ArrowWidth;
                _colWidthSteadyHands = TextWidth("0") / _scale + Table.ArrowWidth;
                _colWidthUseHeartAndSoul = TextWidth("Yes") / _scale + Table.ArrowWidth;
                _colWidthUseQuickInno = TextWidth("Yes") / _scale + Table.ArrowWidth;
                _colWidthHasManipulation = TextWidth("Yes") / _scale + Table.ArrowWidth;
                _colWidthEnsureReliability = TextWidth("Yes") / _scale + Table.ArrowWidth;
                _colWidthBackloadProgress = TextWidth("Yes") / _scale + Table.ArrowWidth;
            }
        }

        public RaphaelCacheTable(List<RaphaelOptions> cacheList) : base("RaphaelCacheTable", cacheList)
        {
            List<Column<RaphaelOptions>> headers = [_colLevel, _colProgress, _colQuality, _colDurability, _colCraftsmanship, _colControl, _colCP, _colIsExpert, _colInitialQuality, _colSpecialist, _colSteadyHands, _colUseHeartAndSoul, _colUseQuickInno, _colHasManipulation, _colEnsureReliability, _colBackloadProgress];
            this.Headers = [.. headers];

            Sortable = true;
            Flags |= ImGuiTableFlags.Hideable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Resizable;
        }

        public void Dispose()
        {

        }

        public void ClickableCell(RaphaelOptions key, string text)
        {
            if (ImGui.Selectable(text))
            {
                var m = P.Config.RaphaelSolverCacheV6[key];
                if (!P.ws.Windows.Any(x => x.WindowName.Contains(m.ID.ToString())))
                {
                    new MacroEditor(m, true);
                }
            }
        }

        public sealed class LevelColumn : ClickableColumn
        {
            public LevelColumn() => Flags |= ImGuiTableColumnFlags.NoHide;

            public override string ToName(RaphaelOptions m) => m.Level.ToString();
            public override float Width => _colWidthLevel * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs) => lhs.Level.CompareTo(rhs.Level);
    }

        public sealed class ProgressColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.Progress.ToString();
            public override float Width => _colWidthProgress * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs) => lhs.Progress.CompareTo(rhs.Progress);
        }

        public sealed class QualityColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.QualityMax.ToString();
            public override float Width => _colWidthQuality * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.QualityMax - rhs.QualityMax;
            }
        }

        public sealed class DurabilityColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.Durability.ToString();
            public override float Width => _colWidthDurability * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.Durability - rhs.Durability;
            }
        }

        public sealed class CraftsmanshipColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.MinCraftsmanship.ToString();
            public override float Width => _colWidthCraftsmanship * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.MinCraftsmanship - rhs.MinCraftsmanship;
            }
        }

        public sealed class ControlColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.MinControl.ToString();
            public override float Width => _colWidthControl * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.MinControl - rhs.MinControl;
            }
        }

        public sealed class CPColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.MinCP.ToString();
            public override float Width => _colWidthCP * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.MinCP - rhs.MinCP;
            }
        }

        public sealed class IsExpertColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.IsExpert ? "Expert" : "";
            public override float Width => _colWidthIsExpert * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.IsExpert && !rhs.IsExpert ? -1 : !lhs.IsExpert && rhs.IsExpert ? 1 : 0;
            }
        }

        public sealed class InitialQualityColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.InitialQuality.ToString();
            public override float Width => _colWidthInitialQuality * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.InitialQuality - rhs.InitialQuality;
            }
        }

        public sealed class SpecialistColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.IsSpecialist.ToString();
            public override float Width => _colWidthSpecialist * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.IsSpecialist && !rhs.IsSpecialist ? -1 : !lhs.IsSpecialist && rhs.IsSpecialist ? 1 : 0;
            }
        }

        public sealed class SteadyHandsColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.SteadyHandUses.ToString();
            public override float Width => _colWidthSteadyHands * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.SteadyHandUses - rhs.SteadyHandUses;
            }
        }

        public sealed class UseHeartAndSoulColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.SolutionConfig.UseHeartAndSoul.ToString();
            public override float Width => _colWidthUseHeartAndSoul * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.SolutionConfig.UseHeartAndSoul && !rhs.SolutionConfig.UseHeartAndSoul ? -1 : !lhs.SolutionConfig.UseHeartAndSoul && rhs.SolutionConfig.UseHeartAndSoul ? 1 : 0;
            }
        }

        public sealed class UseQuickInnoColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.SolutionConfig.UseQuickInno.ToString();
            public override float Width => _colWidthUseQuickInno * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.SolutionConfig.UseQuickInno && !rhs.SolutionConfig.UseQuickInno ? -1 : !lhs.SolutionConfig.UseQuickInno && rhs.SolutionConfig.UseQuickInno ? 1 : 0;
            }
        }

        public sealed class HasManipulationColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.SolutionConfig.HasManipulation.ToString();
            public override float Width => _colWidthHasManipulation * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.SolutionConfig.HasManipulation && !rhs.SolutionConfig.HasManipulation ? -1 : !lhs.SolutionConfig.HasManipulation && rhs.SolutionConfig.HasManipulation ? 1 : 0;
            }
        }

        public sealed class EnsureReliabilityColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.SolutionConfig.EnsureReliability.ToString();
            public override float Width => _colWidthEnsureReliability * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.SolutionConfig.EnsureReliability && !rhs.SolutionConfig.EnsureReliability ? -1 : !lhs.SolutionConfig.EnsureReliability && rhs.SolutionConfig.EnsureReliability ? 1 : 0;
            }
        }

        public sealed class BackloadProgressColumn : ClickableColumn
        {
            public override string ToName(RaphaelOptions m) => m.SolutionConfig.BackloadProgress.ToString();
            public override float Width => _colWidthBackloadProgress * ImGuiHelpers.GlobalScale;
            public override int Compare(RaphaelOptions lhs, RaphaelOptions rhs)
            {
                return lhs.SolutionConfig.BackloadProgress && !rhs.SolutionConfig.BackloadProgress ? -1 : !lhs.SolutionConfig.BackloadProgress && rhs.SolutionConfig.BackloadProgress ? 1 : 0;
            }
        }
    }
}
