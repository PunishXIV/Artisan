using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Artisan.UI;
using ECommons.DalamudServices;
using SharpDX.DXGI;

namespace Artisan.CraftingLogic.Solvers
{
    public class RaphaelSolverDefintion : ISolverDefinition
    {
        public Solver Create(CraftState craft, int flavour)
        {
            var key = $"{craft.CraftProgress}-{craft.CraftQualityMax}-{craft.CraftDurability}--{craft.StatCraftsmanship}-{craft.StatControl}-{craft.StatCP}";
            var output = RaphaelCache.Cache.GetValueOrDefault(key);

            if (output == null) throw new System.Exception("Shouldn't be called");

            return new MacroSolver(new MacroSolverSettings.Macro()
            {
                Name = key,
                Steps = MacroUI.ParseMacro(output.Replace("2", "II")),
                Options = new()
                {
                    SkipQualityIfMet = false,
                    UpgradeProgressActions = false,
                    UpgradeQualityActions = false,
                    MinCP = craft.StatCP,
                    MinControl = craft.StatControl,
                    MinCraftsmanship = craft.StatCraftsmanship,
                }
            }, craft);
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            var key = $"{craft.CraftProgress}-{craft.CraftQualityMax}-{craft.CraftDurability}--{craft.StatCraftsmanship}-{craft.StatControl}-{craft.StatCP}";
            if (RaphaelCache.Cache.TryGetValue(key, out string? value))
            {
                yield return new(this, 0, 2, "Raphael Recipe Solver");
            }
            else
            {
                Svc.Log.Information("Recipe doesnt exist in cache");
                if (RaphaelCache.Cache.TryGetValue(key, out var output))
                {
                    Svc.Log.Information("Recipe has finished processing, deleting task");
                    RaphaelCache.Tasks.Remove(key, out var _);
                    yield return new(this, 0, 2, "Raphael Recipe Solver", "Unsupported, waiting for process to solve...");
                }
                else if (RaphaelCache.Tasks.ContainsKey(key))
                {
                    Svc.Log.Information("Recipe is being processed by Raphael");
                    yield return new(this, 0, 2, "Raphael Recipe Solver", "Unsupported, waiting for process to solve...");
                }
                else if (!RaphaelCache.Tasks.ContainsKey(key) && !RaphaelCache.Cache.ContainsKey(key))
                {
                    Svc.Log.Information("Spawning Raphael process");
                    var manipulation = craft.UnlockedManipulation ? "--manipulation" : "";
                    var process = new Process()
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.exe"),
                            Arguments = $"solve --item-id {craft.ItemId} {manipulation} --level {craft.StatLevel} --stats {craft.StatCraftsmanship} {craft.StatControl} {craft.StatCP} --output-variables actions", // Command to execute
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
                        RaphaelCache.Cache.TryAdd(key, output.Replace("\"", "").Replace("[", "").Replace("]", "").Replace(",", "\r\n"));
                    });

                    RaphaelCache.Tasks.TryAdd(key, task);

                    yield return new(this, 0, 2, "Raphael Recipe Solver", "Unsupported, creating process to solve...");
                }
            }
        }
    }

    internal static class RaphaelCache
    {
        internal static readonly ConcurrentDictionary<string, Task> Tasks = [];
        internal static readonly ConcurrentDictionary<string, string> Cache = [];
    }
}
