using Artisan.UI;
using ECommons.DalamudServices;
using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Artisan.CraftingLogic.Solvers
{
    public class RaphaelSolverDefintion : ISolverDefinition
    {
        public Solver Create(CraftState craft, int flavour)
        {
            var key = RaphaelCache.GetKey(craft);
            RaphaelCache.HasSolution(craft, out var output);

            if (output == null) throw new System.Exception("Shouldn't be called");

            return new MacroSolver(new MacroSolverSettings.Macro()
            {
                Name = key,
                Steps = MacroUI.ParseMacro(output.Macro, true),
                Options = new()
                {
                    SkipQualityIfMet = true,
                    UpgradeProgressActions = false,
                    UpgradeQualityActions = false,
                    MinCP = output.MinCP,
                    MinControl = output.MinControl,
                    ExactCraftsmanship = output.ExactCraftsmanship,
                }
            }, craft);
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            if (RaphaelCache.HasSolution(craft, out var output))
            {
                yield return new(this, -1, 2, "Raphael Recipe Solver");
            }
        }
    }

    internal static class RaphaelCache
    {
        internal static readonly ConcurrentDictionary<string, Tuple<CancellationTokenSource, Task>> Tasks = [];

        public static void Build(CraftState craft, RaphaelSolutionConfig config)
        {
            var key = GetKey(craft);

            if (CLIExists() && !Tasks.ContainsKey(key))
            {
                P.Config.RaphaelSolverCache.TryRemove(key, out _);

                Svc.Log.Information("Spawning Raphael process");

                var manipulation = craft.UnlockedManipulation ? "--manipulation" : "";
                var itemText = craft.IsCosmic ? $"--recipe-id {craft.RecipeId}" : $"--item-id {craft.ItemId}";
                var extraArgsBuilder = new StringBuilder();

                if (config.HQConsiderations)
                {
                    extraArgsBuilder.Append($"--initial {Simulator.GetStartingQuality(craft.Recipe, false)} "); // must always have a space after
                }

                if (config.EnsureReliability)
                {
                    extraArgsBuilder.Append($"--adversarial "); // must always have a space after
                }

                if (config.BackloadProgress)
                {
                    extraArgsBuilder.Append($"--backload-progress "); // must always have a space after
                }

                if (config.HeartAndSoul)
                {
                    extraArgsBuilder.Append($"--heart-and-soul "); // must always have a space after
                }

                if (config.QuickInno)
                {
                    extraArgsBuilder.Append($"--quick-innovation "); // must always have a space after
                }

                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.exe"),
                        Arguments = $"solve {itemText} {manipulation} --level {craft.StatLevel} --stats {craft.StatCraftsmanship} {craft.StatControl} {craft.StatCP} {extraArgsBuilder} --output-variables actions", // Command to execute
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                Svc.Log.Information(process.StartInfo.Arguments);

                var cts = new CancellationTokenSource();
                cts.Token.Register(process.Kill);

                var task = Task.Run(() =>
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    P.Config.RaphaelSolverCache[key] = new()
                    {
                        HQConsiderations = config.HQConsiderations,
                        BackloadProgress = config.BackloadProgress,
                        HeartAndSoul = config.HeartAndSoul,
                        QuickInno = config.QuickInno,
                        EnsureReliability = config.EnsureReliability,
                        MinCP = craft.StatCP,
                        MinControl = craft.StatControl,
                        ExactCraftsmanship = craft.StatCraftsmanship,
                        Macro = output.Replace("\"", "").Replace("[", "").Replace("]", "").Replace(",", "\r\n").Replace("2", "II").Replace("MasterMend", "MastersMend")
                    };
                    P.Config.Save();
                    Tasks.Remove(key, out var _);
                }, cts.Token);

                Tasks.TryAdd(key, new(cts, task));
            }
        }

        public static string GetKey(CraftState craft)
        {
            return $"{craft.RecipeId}";
        }

        public static bool HasSolution(CraftState craft, out RaphaelSolutionConfig? raphaelSolutionConfig)
        {
            if (P.Config.RaphaelSolverCache.TryGetValue(GetKey(craft), out raphaelSolutionConfig))
            {
                return raphaelSolutionConfig?.Macro.Length > 0;
            }

            return false;
        }

        public static bool InProgress(CraftState craft) => Tasks.TryGetValue(GetKey(craft), out var _);

        internal static bool CLIExists()
        {
            return File.Exists(Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.exe"));
        }
    }

    public class RaphaelSolverSettings
    {
        public bool AllowHQConsiderations = false;
        public bool AllowEnsureReliability = false;
        public bool AllowBackloadProgress = false;
        public bool ShowSpecialistSettings = false;


        public bool Draw()
        {
            bool changed = false;

            ImGui.Indent();
            ImGui.TextWrapped($"Raphael settings can change the performance and system memory consumption. If you have low amounts of RAM try not to change settings, recommended minimum amount of RAM free is 2GB");

            changed |= ImGui.Checkbox("Allow HQ Materials to be considered in macro generation", ref AllowHQConsiderations);
            changed |= ImGui.Checkbox("Ensure 100% reliability in macro generation", ref AllowEnsureReliability);
            ImGui.TextColored(new System.Numerics.Vector4(255, 0, 0, 1), "Ensuring reliability may not always work and is very CPU and RAM intensive, suggested RAM at least 16GB+ spare.");
            changed |= ImGui.Checkbox("Allow backloading of progress in macro generation", ref AllowBackloadProgress);
            changed |= ImGui.Checkbox("Show specialist options when available", ref ShowSpecialistSettings);

            if (ImGui.Button($"Clear raphael macro cache (Currently {P.Config.RaphaelSolverCache.Count} stored)"))
            {
                P.Config.RaphaelSolverCache.Clear();
                changed |= true;
            }

            ImGui.Unindent();
            return changed;
        }
    }

    public class RaphaelSolutionConfig
    {
        public bool HQConsiderations = false;
        public bool EnsureReliability = false;
        public bool BackloadProgress = false;
        public bool HeartAndSoul = false;
        public bool QuickInno = false;
        public string Macro = string.Empty;

        public int MinCP = 0;
        public int MinControl = 0;
        public int ExactCraftsmanship = 0;

        [NonSerialized]
        public bool HasChanges = false;
    }
}
