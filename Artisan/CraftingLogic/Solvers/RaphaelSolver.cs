using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Artisan.UI;
using ECommons.DalamudServices;

namespace Artisan.CraftingLogic.Solvers
{
    public class RaphaelSolverDefintion : ISolverDefinition
    {
        private readonly ConcurrentDictionary<string, Task> _tasks = [];
        private readonly ConcurrentDictionary<string, string> _cache = [];

        public Solver Create(CraftState craft, int flavour)
        {
            var key = $"{craft.CraftProgress}-{craft.CraftQualityMax}-{craft.CraftDurability}--{craft.StatCraftsmanship}-{craft.StatControl}-{craft.StatCP}";
            Svc.Log.Debug($"Creating solver {key}");
            var output = _cache.GetValueOrDefault(key);

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
            if (_cache.TryGetValue(key, out string? value))
            {
                yield return new(this, 0, 2, "Raphael Recipe Solver");
            }
            else
            {
                if (_cache.ContainsKey(key))
                {
                    _tasks.Remove(key, out var _);

                    yield return new(this, 0, 2, "Raphael Recipe Solver", "");
                }
                else if (_tasks.ContainsKey(key))
                {
                    yield return new(this, 0, 2, "Raphael Recipe Solver", "Unsupported, waiting for process to solve...");
                }
                else if (!_tasks.ContainsKey(key) && !_cache.ContainsKey(key))
                {
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

                    var task = Task.Run(() =>
                    {
                        process.Start();
                        _cache.TryAdd(key, process.StandardOutput.ReadToEnd().Replace("\"", "").Replace("[", "").Replace("]", "").Replace(",", "\r\n"));
                    });

                    _tasks.TryAdd(key, task);

                    yield return new(this, 0, 2, "Raphael Recipe Solver", "Unsupported, creating process to solve...");
                }
            }
        }
    }
}
