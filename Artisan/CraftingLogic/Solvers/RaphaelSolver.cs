using Artisan.UI;
using ECommons.DalamudServices;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Artisan.CraftingLogic.Solvers
{
    public class RaphaelSolverDefintion : ISolverDefinition
    {
        public Solver Create(CraftState craft, int flavour)
        {
            var key = RaphaelCache.GetKey(craft);
            var output = P.Config.RaphaelSolverCache.GetValueOrDefault(key);

            if (output == null) throw new System.Exception("Shouldn't be called");

            return new MacroSolver(output, craft);
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            var key = RaphaelCache.GetKey(craft);

            if (P.Config.RaphaelSolverCache.ContainsKey(key))
            {
                yield return new(this, -1, 2, $"Raphael Recipe Solver {key}");
            }
        }
    }

    internal static class RaphaelCache
    {
        internal static readonly ConcurrentDictionary<string, Task> Tasks = [];

        public static void Build(CraftState craft)
        {
            var key = GetKey(craft);

            if (CLIExists() && !Tasks.ContainsKey(key))
            {
                P.Config.RaphaelSolverCache.TryRemove(key, out _);

                Svc.Log.Information("Spawning Raphael process");

                var manipulation = craft.UnlockedManipulation ? "--manipulation" : "";
                var itemText = craft.IsCosmic ? $"--recipe-id {craft.RecipeId}" : $"--item-id {craft.ItemId}";
                var process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.exe"),
                        Arguments = $"solve {itemText} {manipulation} --level {craft.StatLevel} --stats {craft.StatCraftsmanship} {craft.StatControl} {craft.StatCP} --output-variables actions", // Command to execute
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                Svc.Log.Information(process.StartInfo.Arguments);

                var task = Task.Run(() =>
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    P.Config.RaphaelSolverCache[key] = new MacroSolverSettings.Macro()
                    {
                        ID = (int)craft.RecipeId,
                        Name = key,
                        Steps = MacroUI.ParseMacro(output.Replace("\"", "").Replace("[", "").Replace("]", "").Replace(",", "\r\n").Replace("2", "II").Replace("MasterMend", "MastersMend"), true),
                        Options = new()
                        {
                            SkipQualityIfMet = true,
                            UpgradeProgressActions = false,
                            UpgradeQualityActions = false,
                            MinCP = craft.StatCP,
                            MinControl = craft.StatControl,
                            MinCraftsmanship = craft.StatCraftsmanship,
                        }
                    };
                    P.Config.Save();
                    Tasks.Remove(key, out var _);
                });

                Tasks.TryAdd(key, task);
            }
        }

        public static string GetKey(CraftState craft)
        {
            return $"{craft.RecipeId}";
        }

        public static bool HasSolution(CraftState craft) => P.Config.RaphaelSolverCache.TryGetValue(GetKey(craft), out var _);
        public static bool InProgress(CraftState craft) => Tasks.TryGetValue(GetKey(craft), out var _);

        internal static bool CLIExists()
        {
            return File.Exists(Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.exe"));
        }
    }
}
