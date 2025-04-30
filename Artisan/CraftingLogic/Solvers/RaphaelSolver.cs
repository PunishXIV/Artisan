using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.UI;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.Logging;
using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            if (RaphaelCache.HasSolution(craft, out var output))
            {
                return new MacroSolver(output!, craft);
            }
            return craft.CraftExpert ? new ExpertSolver() : new StandardSolver(false);
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            if (RaphaelCache.HasSolution(craft, out var solution))
                yield return new(this, 3, 0, $"Raphael Recipe Solver");
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
                P.Config.RaphaelSolverCacheV2.TryRemove(key, out _);

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
                    Svc.Log.Error("Ensuring reliability is enabled, this may take a while. NO SUPPORT GIVEN IF ENABLED.");
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

                if (P.Config.RaphaelSolverConfig.MaximumThreads > 0)
                {
                    extraArgsBuilder.Append($"--threads {P.Config.RaphaelSolverConfig.MaximumThreads} "); // must always have a space after
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

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        DuoLog.Error($"Raphael native error: {e.Data}"); // this should be called
                    }
                };

                Svc.Log.Information(process.StartInfo.Arguments);

                var cts = new CancellationTokenSource();
                cts.Token.Register(process.Kill);

                var task = Task.Run(() =>
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();

                    var rng = new Random();
                    var ID = rng.Next(50001, 10000000);
                    while (P.Config.RaphaelSolverCacheV2.Any(kv => kv.Value.ID == ID))
                        ID = rng.Next(50001, 10000000);

                    P.Config.RaphaelSolverCacheV2[key] = new MacroSolverSettings.Macro()
                    {
                        ID = ID,
                        Name = key,
                        Steps = MacroUI.ParseMacro(output.Replace("\"", "").Replace("[", "").Replace("]", "").Replace(",", "\r\n").Replace("2", "II").Replace("MasterMend", "MastersMend"), true),
                        Options = new()
                        {
                            SkipQualityIfMet = false,
                            UpgradeProgressActions = false,
                            UpgradeQualityActions = false,
                            MinCP = craft.StatCP,
                            MinControl = craft.StatControl,
                            MinCraftsmanship = craft.StatCraftsmanship,
                        }
                    };
                    if (P.Config.RaphaelSolverCacheV2[key] == null || P.Config.RaphaelSolverCacheV2[key].Steps.Count == 0)
                    {
                        DuoLog.Error($"Raphael failed to generate a valid macro. This could be one of the following reasons:" +
                            $"\n- If you are not running Windows, Raphael may not be compatible with your OS." +
                            $"\n- You cancelled the generation." +
                            $"\n- Raphael just gave up after not finding a result.{(P.Config.RaphaelSolverConfig.AutoGenerate ? "\nAutomatic generation will be disabled as a result." : "")}");
                        P.Config.RaphaelSolverConfig.AutoGenerate = false;
                        return;
                    }


                    if (P.Config.RaphaelSolverConfig.AutoSwitch)
                    {
                        if (!P.Config.RaphaelSolverConfig.AutoSwitchOnAll)
                        {
                            Svc.Log.Debug("Switching to Raphael solver");
                            var opt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                            if (opt is not null)
                            {
                                var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.Recipe.RowId);
                                config.SolverType = opt?.Def.GetType().FullName!;
                                config.SolverFlavour = (int)(opt?.Flavour);
                                P.Config.RecipeConfigs[craft.Recipe.RowId] = config;
                            }
                        }
                        else
                        {
                            var crafts = AllValidCrafts(key, craft.Recipe.CraftType.RowId).ToList();
                            Svc.Log.Debug($"Applying solver to {crafts.Count()} recipes.");
                            var opt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                            if (opt is not null)
                            {
                                var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.Recipe.RowId) ?? new();
                                config.SolverType = opt?.Def.GetType().FullName!;
                                config.SolverFlavour = (int)(opt?.Flavour);
                                foreach (var c in crafts)
                                {
                                    Svc.Log.Debug($"Switching {c.Recipe.RowId} ({c.Recipe.ItemResult.Value.Name}) to Raphael solver");
                                    P.Config.RecipeConfigs[c.Recipe.RowId] = config;
                                }
                            }
                        }
                    }
                    P.Config.Save();
                    Tasks.Remove(key, out var _);
                }, cts.Token);

                Tasks.TryAdd(key, new(cts, task));
            }
        }

        public static string GetKey(CraftState craft)
        {
            return $"{craft.CraftLevel}/{craft.CraftProgress}/{craft.CraftQualityMax}/{craft.CraftDurability}-{craft.StatCraftsmanship}/{craft.StatControl}/{craft.StatCP}-{(craft.CraftExpert ? "Expert" : "Standard")}";
        }

        public static IEnumerable<CraftState> AllValidCrafts(string key, uint craftType)
        {
            var stats = KeyParts(key);
            var recipes = LuminaSheets.RecipeSheet.Values.Where(x => x.CraftType.RowId == craftType && x.RecipeLevelTable.Value.ClassJobLevel == stats.Level);
            foreach (var recipe in recipes)
            {
                var state = Crafting.BuildCraftStateForRecipe(default, Job.CRP + recipe.CraftType.RowId, recipe);
                if (stats.Prog == state.CraftProgress &&
                    stats.Qual == state.CraftQualityMax &&
                    stats.Dur == state.CraftDurability)
                    yield return state;
            }
        }

        public static (int Level, int Prog, int Qual, int Dur, int Crafts, int Control, int CP) KeyParts(string key)
        {
            var parts = key.Split('/');
            if (parts.Length != 6)
                return new();

            var lvl = int.Parse(parts[0]);
            var prog = int.Parse(parts[1]);
            var qual = int.Parse(parts[2]);
            var dur = int.Parse(parts[3].Split('-')[0]);
            var crafts = int.Parse(parts[3].Split('-')[1]);
            var ctrl = int.Parse(parts[4]);
            var cp = int.Parse(parts[5].Split('-')[0]);

            return (lvl, prog, qual, dur, crafts, ctrl, cp);
        }

        public static bool HasSolution(CraftState craft, out MacroSolverSettings.Macro? raphaelSolutionConfig)
        {
            foreach (var solution in P.Config.RaphaelSolverCacheV2.OrderByDescending(x => KeyParts(x.Key).Control))
            {
                if (solution.Value.Steps.Count == 0) continue;

                var solKey = KeyParts(solution.Key);
                if (solKey.Level == craft.CraftLevel &&
                    solKey.Prog == craft.CraftProgress &&
                    solKey.Qual == craft.CraftQualityMax &&
                    solKey.Crafts == craft.StatCraftsmanship &&
                    solKey.Control <= craft.StatControl &&
                    solKey.CP <= craft.StatCP)
                {
                    raphaelSolutionConfig = solution.Value;
                    return true;
                }
            }
            raphaelSolutionConfig = null;
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
        public bool ExactCraftsmanship = false;
        public bool AutoGenerate = false;
        public bool AutoSwitch = false;
        public bool AutoSwitchOnAll = false;
        public int MaximumThreads = 0;

        public bool Draw()
        {
            bool changed = false;

            ImGui.Indent();
            ImGui.TextWrapped($"Raphael settings can change the performance and system memory consumption. If you have low amounts of RAM try not to change settings, recommended minimum amount of RAM free is 2GB");

            if (ImGui.SliderInt("Maximum Threads", ref MaximumThreads, 0, Environment.ProcessorCount))
            {
                P.Config.Save();
            }
            ImGui.Text("By default uses all it can, but on lower end machines you might need to use less cpu at the cost of speed. (0 = everything)");

            changed |= ImGui.Checkbox("Allow HQ Materials to be considered in macro generation", ref AllowHQConsiderations);
            changed |= ImGui.Checkbox("Ensure 100% reliability in macro generation", ref AllowEnsureReliability);
            ImGui.PushTextWrapPos(0);
            ImGui.TextColored(new System.Numerics.Vector4(255, 0, 0, 1), "Ensuring reliability may not always work and is very CPU and RAM intensive, suggested RAM at least 16GB+ spare. NO SUPPORT SHALL BE GIVEN IF YOU HAVE THIS ON");
            ImGui.PopTextWrapPos();
            changed |= ImGui.Checkbox("Allow backloading of progress in macro generation", ref AllowBackloadProgress);
            changed |= ImGui.Checkbox("Show specialist options when available", ref ShowSpecialistSettings);
            changed |= ImGui.Checkbox($"Automatically generate a solution if a valid one hasn't been created.", ref AutoGenerate);
            changed |= ImGui.Checkbox($"Automatically switch to the Raphael Solver once a solution has been created.", ref AutoSwitch);

            if (AutoSwitch)
            {
                ImGui.Indent();
                changed |= ImGui.Checkbox($"Apply to all valid crafts", ref AutoSwitchOnAll);
                ImGui.Unindent();
            }

            if (ImGui.Button($"Clear raphael macro cache (Currently {P.Config.RaphaelSolverCacheV2.Count} stored)"))
            {
                P.Config.RaphaelSolverCacheV2.Clear();
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
