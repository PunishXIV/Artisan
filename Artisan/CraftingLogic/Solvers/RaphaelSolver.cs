using Artisan.UI;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Artisan.CraftingLogic.Solvers
{
    public class RaphaelSolverDefinition : ISolverDefinition
    {
        public Solver Create(CraftState craft, int flavour)
        {
            var key = RaphaelCache.GetKey(craft);
            var solveEntry = RaphaelCache.GetEntry(P.Config, key);

            if (solveEntry == null) throw new System.Exception("Shouldn't be called");

            return new RaphaelSolver(new MacroSolver(new MacroSolverSettings.Macro()
            {
                Name = key,
                Steps = MacroUI.ParseMacro(solveEntry.Macro, true),
                Options = new()
                {
                    SkipQualityIfMet = true,
                    UpgradeProgressActions = false,
                    UpgradeQualityActions = false,
                    MinCP = craft.StatCP,
                    MinControl = craft.StatControl,
                    MinCraftsmanship = craft.StatCraftsmanship,
                }
            }, craft, typeof(RaphaelSolverDefinition)));
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            var key = RaphaelCache.GetKey(craft);
            if (P.Config.RaphaelSolverCache.TryGetValue(key, out string? value))
            {
                yield return new(this, -1, 3, "Raphael Recipe Solver");
            }
            else if (P.Config.RaphaelAutoBuild)
            {
                RaphaelCache.Build(craft);
            }
        }
    }

    internal sealed class RaphaelSolver : Solver
    {
        private readonly MacroSolver _macroSolver;

        public RaphaelSolver(MacroSolver macroSolver)
        {
            _macroSolver = macroSolver;
        }

        public override Recommendation Solve(CraftState craft, StepState step)
        {
            return _macroSolver.Solve(craft, step);
        }

        public override Solver Clone()
        {
            return new RaphaelSolver((MacroSolver)_macroSolver.Clone());
        }
    }

    internal static class RaphaelCache
    {
        public static readonly Lazy<bool> CliExists = new(CheckCLIExists);
        private static readonly ConcurrentDictionary<string, SolveEntry> _parsedCache = [];

        internal static readonly ConcurrentDictionary<string, Task> ActiveBuildTasks = [];

        public static void Build(CraftState craft)
        {
            var key = GetKey(craft);

            if (!CliExists.Value | ActiveBuildTasks.ContainsKey(key))
                return;

            P.Config.RaphaelSolverCache.TryRemove(key, out _);

            Svc.Log.Information("Spawning Raphael process");

            var startingQualityPercent = Convert.ToDouble(craft.StartingQuality) / Convert.ToDouble(craft.CraftQualityMax) * 100;
            var manipulation = craft.UnlockedManipulation ? "--manipulation" : "";
            var itemText = craft.IsCosmic ? $"--recipe-id {craft.RecipeId}" : $"--item-id {craft.ItemId}";
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.exe"),
                    Arguments = $"solve {itemText} {manipulation} --level {craft.StatLevel} --stats {craft.StatCraftsmanship} {craft.StatControl} {craft.StatCP} --initial-quality {startingQualityPercent} --output-variables actions", // Command to execute
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            Svc.Log.Information(process.StartInfo.Arguments);

            var task = new Task(() =>
            {
                process.Start();
                var output = process.StandardOutput.ReadToEnd();

                P.Config.RaphaelSolverCache[key] = 
                    JsonConvert.SerializeObject(
                        new SolveEntry() 
                        { 
                            LastUsed = DateTimeOffset.UtcNow, 
                            Macro = output
                                .Replace("\"", "")
                                .Replace("[", "")
                                .Replace("]", "")
                                .Replace(",", "\n")
                                .Replace("2", "II")
                                .Replace("MasterMend", "MastersMend") 
                        }
                    );

                while (P.Config.RaphaelSolverCache.Count > P.Config.RaphaelMaxCacheSize)
                {
                    var oldestKey = P.Config.RaphaelSolverCache.Keys
                        .MinBy(k => GetEntry(P.Config, key)?.LastUsed ?? DateTimeOffset.UtcNow);

                    if (oldestKey is null)
                        break;

                    P.Config.RaphaelSolverCache.Remove(oldestKey, out _);
                    _parsedCache.Remove(oldestKey, out _);
                }

                P.Config.Save();
                
                ActiveBuildTasks.Remove(key, out _);
            });

            if (!ActiveBuildTasks.TryAdd(key, task))
                return;

            task.Start();
        }

        public static string GetKey(CraftState craft)
        {
            return $"{craft.RecipeId}-{craft.StatCraftsmanship}-{craft.StatControl}-{craft.StatCP}-{craft.StartingQuality}";
        }

        public static SolveEntry? GetEntry(Configuration config, string key)
        {
            if (!_parsedCache.TryGetValue(key, out var entry))
            {
                var value = config.RaphaelSolverCache.GetValueOrDefault(key);
                if (value is null)
                    return null;

                try
                {
                    entry = JsonConvert.DeserializeObject<SolveEntry>(value);
                }
                catch
                {
                    // Old style entry, just remake
                }

                if (entry is null)
                {
                    config.RaphaelSolverCache.Remove(key, out _);
                    config.Save();

                    return null;
                }

                _parsedCache[key] = entry;
            }

            // Only compare dates to prevent churn
            if (entry.LastUsed.Date != DateTime.UtcNow.Date)
            {
                entry.LastUsed = DateTime.UtcNow;
                config.RaphaelSolverCache[key] = JsonConvert.SerializeObject(entry);
                config.Save();
            }

            return entry;
        }

        public static bool HasSolution(CraftState craft) => P.Config.RaphaelSolverCache.TryGetValue(GetKey(craft), out var _);
        public static bool InProgress(CraftState craft) => ActiveBuildTasks.TryGetValue(GetKey(craft), out var _);

        internal static bool CheckCLIExists()
        {
            return File.Exists(Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.exe"));
        }

        public record SolveEntry
        {
            public required string Macro { get; init; }
            
            public required DateTimeOffset LastUsed { get; set; }
        }
    }
}
