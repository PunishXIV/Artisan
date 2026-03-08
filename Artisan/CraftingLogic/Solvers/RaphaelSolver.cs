using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Artisan.CraftingLogic.Solvers
{
    public class RaphaelSolverDefintion : ISolverDefinition
    {
        public Solver Create(CraftState craft, int flavour)
        {
            if (craft.StatLevel < 7)
                return new StandardSolver();

            var key = RaphaelCache.GetKey(craft);
            if (RaphaelCache.HasSolution(craft, out var output))
            {
                return new MacroSolver(output!, craft);
            }
            return craft.CraftExpert ? new ExpertSolver() : new StandardSolver();
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours(CraftState craft)
        {
            yield return new(this, 3, 2, $"Raphael Recipe Solver", craft.StatLevel < 7 ? $"Does not work before unlocking {Skills.MastersMend.NameOfAction()}. Please use Standard Recipe Solver" : "");
        }

        public IEnumerable<ISolverDefinition.Desc> Flavours()
        {
            yield return new(this, 3, 2, $"Raphael Recipe Solver");
        }
    }

    internal static class RaphaelCache
    {
        internal static readonly ConcurrentDictionary<string, RaphaelTaskInfo> Tasks = [];
        [NonSerialized]
        public static Dictionary<string, RaphaelSolutionConfig> TempConfigs = new();

        internal sealed class RaphaelTaskInfo
        {
            public CancellationTokenSource Cancellation { get; set; }
            public Task Task { get; set; }
            public volatile bool FromStartCraft;
            public volatile bool Succeeded;

            public RaphaelTaskInfo(CancellationTokenSource cts, Task task, bool fromStartCraft)
            {
                Cancellation = cts;
                Task = task;
                FromStartCraft = fromStartCraft;
            }
        }


        public static void Build(CraftState craft, RaphaelSolutionConfig config, bool fromStartCraft = false)
        {
            if (craft.StatLevel < 7) return;

            var key = GetKey(craft);
            if (!CLIExists() || Tasks.ContainsKey(key)) return;

            P.Config.RaphaelSolverCacheV5.TryRemove(key, out _);

            var manipulation = craft.UnlockedManipulation ? "--manipulation" : "";
            var itemText = $"--custom-recipe {craft.LevelTable.RowId} {craft.CraftProgress} {(craft.CraftCollectible ? craft.CraftQualityMin3 : craft.CraftQualityMax)} {craft.CraftDurability} {(craft.CraftExpert ? "1" : "0")} --stellar-steady-hand {Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand)}";

            var argsList = new List<string>
            {
                $"--initial {craft.InitialQuality}"
            };

            if (config.EnsureReliability) argsList.Add("--adversarial");
            if (config.BackloadProgress) argsList.Add("--backload-progress");
            if (config.HeartAndSoul) argsList.Add("--heart-and-soul");
            if (config.QuickInno) argsList.Add("--quick-innovation");
            if (P.Config.RaphaelSolverConfig.MaximumThreads > 0)
                argsList.Add($"--threads {P.Config.RaphaelSolverConfig.MaximumThreads}");

            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(P.Config.RaphaelSolverConfig.TimeOutMins));
            var info = new RaphaelTaskInfo(cts, null!, fromStartCraft);

            info.Task = Task.Run(async () =>
            {
                try
                {
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.bin"),
                            Arguments = $"solve {itemText} {manipulation} --level {craft.StatLevel} --stats {craft.StatCraftsmanship} {craft.StatControl} {craft.StatCP} {string.Join(' ', argsList)} --output-variables action_ids",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };

                    Svc.Log.Debug($"Spawning Raphael process with args: {process.StartInfo.Arguments}");
                    if (process.StartInfo.Arguments.Contains("adversarial"))
                        Svc.Log.Warning("Adversial enabled. Support will not be provided.");
                    if (process.StartInfo.Arguments.Contains("heart-and-soul") || process.StartInfo.Arguments.Contains("quick-innovation"))
                        Svc.Log.Warning("Specialist actions enabled. This may take a long time.");

                    process.Start();

                    using (cts.Token.Register(() =>
                    {
                        try { if (!process.HasExited) process.Kill(); }
                        catch (Exception ex) { ex.Log(); }
                        finally
                        {
                            if (Tasks.TryRemove(key, out var t) && t.FromStartCraft && Crafting.CurState is Crafting.State.WaitStart)
                            {
                                DuoLog.Error("Raphael has timed out or cancelled before a solution could be generated. Crafting will not start, please restart this craft.");
                                Crafting.CurState = Crafting.State.InvalidState;
                            }
                        }
                    }))
                    {
                        var stdOutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                        var stdErrTask = process.StandardError.ReadToEndAsync(cts.Token);

                        await Task.WhenAll(
                            stdOutTask,
                            stdErrTask,
                            process.WaitForExitAsync(cts.Token)
                        ).ConfigureAwait(false);

                        var output = stdOutTask.Result;
                        var error = stdErrTask.Result.Trim();

                        if (process.ExitCode != 0)
                        {
                            if (!string.IsNullOrWhiteSpace(error))
                                DuoLog.Error(error);

                            info.Succeeded = false;
                            cts.Cancel();
                            return;
                        }

                        var cleansedOutput = output.Replace("[", "").Replace("]", "").Replace("\"", "")
                                                   .Split(", ")
                                                   .Select(x => int.TryParse(x, out int n) ? n : 0);

                        P.Config.RaphaelSolverCacheV5[key] = new MacroSolverSettings.Macro
                        {
                            ID = new Random().Next(50001, 10000000),
                            Name = key,
                            Steps = MacroUI.ParseMacro(cleansedOutput),
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

                        info.Succeeded = P.Config.RaphaelSolverCacheV5[key]?.Steps.Count > 0;
                    }
                }
                catch (OperationCanceledException)
                {
                    info.Succeeded = false;
                }
                catch (Exception ex)
                {
                    ex.Log("Something went wrong with Raphael task.");
                    info.Succeeded = false;
                }
                finally
                {
                    if (info.Succeeded)
                    {
                        AutoSwitch(craft, key);
                        P.Config.Save();
                    }
                    Tasks.TryRemove(key, out _);
                }
            }, cts.Token);

            Tasks.TryAdd(key, info);
        }

        private static void AutoSwitch(CraftState craft, string key)
        {
            static bool autoSwitchOk(uint recipeId)
            {
                if (P.Config.RaphaelSolverConfig.AutoSwitchOverManual)
                    return true;

                if (P.Config.RecipeConfigs.TryGetValue(recipeId, out var cfg))
                    // flavours: 0 = standard, expert; 3 = raphael; otherwise = macro/script
                    return cfg.SolverFlavour is 0 or 3;

                return true;
            }

            if (P.Config.RaphaelSolverConfig.AutoSwitch)
            {
                Svc.Log.Information("Auto-switch is enabled, switching solver for recipe if applicable.");
                if (!P.Config.RaphaelSolverConfig.AutoSwitchOnAll)
                {
                    Svc.Log.Debug("Switching to Raphael solver - Single");
                    if (craft.StatLevel < 7)
                    {
                        Svc.Log.Debug($"Skipping auto-switch for recipe {craft.Recipe.RowId} - Raphael solver not unlocked");
                        return;
                    }
                    var nopt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                    if (nopt is { } opt)
                    {
                        if (autoSwitchOk(craft.Recipe.RowId))
                        {
                            Svc.Log.Information("AutoSwitchOk, setting");
                            var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.Recipe.RowId) ?? new();
                            config.SolverType = opt.Def.GetType().FullName!;
                            config.SolverFlavour = opt.Flavour;
                            P.Config.RecipeConfigs[craft.Recipe.RowId] = config;
                        }
                        else
                            Svc.Log.Information("Never mind, recipe already has a macro assigned");
                    }
                }
                else
                {
                    var crafts = AllValidCrafts(key).ToList();
                    Svc.Log.Information($"Applying solver to {crafts.Count} recipes.");
                    var nopt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                    if (nopt is { } opt)
                    {
                        var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.Recipe.RowId) ?? new();
                        config.SolverType = opt.Def.GetType().FullName!;
                        config.SolverFlavour = opt.Flavour;
                        foreach (var c in crafts)
                        {
                            if (c.StatLevel < 7)
                            {
                                Svc.Log.Debug($"Skipping {c.Recipe.RowId} ({c.Recipe.ItemResult.Value.Name}) - Raphael solver not unlocked");
                                continue;
                            }
                            if (autoSwitchOk(c.Recipe.RowId))
                            {
                                Svc.Log.Information($"Switching {c.Recipe.RowId} ({c.Recipe.ItemResult.Value.Name}) to Raphael solver");
                                var switchConfig = P.Config.RecipeConfigs.GetValueOrDefault(c.Recipe.RowId) ?? new();
                                switchConfig.SolverType = opt.Def.GetType().FullName!;
                                switchConfig.SolverFlavour = opt.Flavour;
                                P.Config.RecipeConfigs[c.Recipe.RowId] = switchConfig;
                            }
                            else
                                Svc.Log.Information($"Skipping {c.Recipe.RowId} ({c.Recipe.ItemResult.Value.Name}) because it already has a macro assigned");
                        }
                    }
                }
            }
        }

        public static string GetKey(CraftState craft)
        {
            return $"{craft.CraftLevel}/{craft.CraftProgress}/{craft.CraftQualityMax}/{craft.CraftDurability}-{craft.StatCraftsmanship}/{craft.StatControl}/{craft.StatCP}-{(craft.CraftExpert ? "Ex" : "St")}/{craft.InitialQuality}/{(craft.Specialist ? "Sp" : "Re")}/Steady{Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand)}";
        }

        public static RaphaelSolutionConfig GetConfigFromTempOrDefault(CraftState craft)
        {
            var key = GetKey(craft);
            var config = new RaphaelSolutionConfig();

            var hasTempConfig = TempConfigs.TryGetValue(key, out var tempconfig);
            var hasDelins = Crafting.DelineationCount() > 0;
            config.EnsureReliability = hasTempConfig ? tempconfig.EnsureReliability : P.Config.RaphaelSolverConfig.AllowEnsureReliability;
            config.BackloadProgress = hasTempConfig ? tempconfig.BackloadProgress : P.Config.RaphaelSolverConfig.AllowBackloadProgress;
            config.HeartAndSoul = hasTempConfig ? tempconfig.HeartAndSoul : P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist && hasDelins;
            config.QuickInno = hasTempConfig ? tempconfig.QuickInno : P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist && hasDelins;

            return config;
        }

        public static IEnumerable<CraftState> AllValidCrafts(string key)
        {
            var stats = KeyParts(key);
            var recipes = LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == stats.Level);
            foreach (var recipe in recipes)
            {
                var state = Crafting.BuildCraftStateForRecipe(default, (Job)((uint)Job.CRP + recipe.CraftType.RowId), recipe);
                if (state.StatLevel < 7) continue;

                if (stats.Prog == state.CraftProgress &&
                    stats.Qual == state.CraftQualityMax &&
                    stats.Dur == state.CraftDurability)
                    yield return state;
            }
        }

        public static (int Level, int Prog, int Qual, int Dur, int Initial, int Crafts, int Control, int CP, bool SP) KeyParts(string key)
        {
            var parts = key.Split('/');

            int.TryParse(parts[0], out var lvl);
            int.TryParse(parts[1], out var prog);
            int.TryParse(parts[2], out var qual);
            int.TryParse(parts[3].Split('-')[0], out var dur);
            int.TryParse(parts[3].Split('-')[1], out var crafts);
            int.TryParse(parts[4], out var ctrl);
            int.TryParse(parts[5].Split('-')[0], out var cp);
            int.TryParse(parts[6], out var initial);
            var sp = parts[7] == "Sp";

            return (lvl, prog, qual, dur, initial, crafts, ctrl, cp, sp);
        }

        public static bool HasSolution(CraftState craft, out MacroSolverSettings.Macro? raphaelSolutionConfig)
        {
            var thisKey = GetKey(craft);
            raphaelSolutionConfig = null;
            var sol = P.Config.RaphaelSolverCacheV5.FirstOrNull(x => x.Key == thisKey);
            if (sol != null)
            {
                raphaelSolutionConfig = sol.Value.Value;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool InProgress(CraftState craft) => Tasks.TryGetValue(GetKey(craft), out var _);

        public static bool InProgressAny() => Tasks.Any();

        internal static bool CLIExists()
        {
            return File.Exists(Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.bin"));
        }

        public static void DrawRaphaelDropdown(CraftState craft, bool liveStats = true)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.RecipeId) ?? new();
            if (CLIExists())
            {
                var hasSolution = HasSolution(craft, out var solution);
                var key = GetKey(craft);

                if (!TempConfigs.ContainsKey(key))
                {
                    TempConfigs.Add(key, new());
                    TempConfigs[key].EnsureReliability = P.Config.RaphaelSolverConfig.AllowEnsureReliability && !craft.CraftExpert;
                    TempConfigs[key].BackloadProgress = P.Config.RaphaelSolverConfig.AllowBackloadProgress;
                    TempConfigs[key].HeartAndSoul = P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist;
                    TempConfigs[key].QuickInno = P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist;
                }

                var opt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                var solverIsRaph = config.SolverIsRaph;
                if (!hasSolution)
                {
                    if (solverIsRaph)
                        ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "No Raphael Solution Generated.");
                    if (P.Config.RaphaelSolverConfig.AutoGenerate && CraftingProcessor.GetAvailableSolversForRecipe(craft, true).Any() && (!craft.CraftExpert || (craft.CraftExpert && P.Config.RaphaelSolverConfig.GenerateOnExperts)))
                    {
                        Build(craft, TempConfigs[key]);
                    }
                }

                ImGui.Separator();

                var inProgress = InProgress(craft);

                if (inProgress)
                    ImGui.BeginDisabled();

                if (P.Config.RaphaelSolverConfig.AllowEnsureReliability && !craft.CraftExpert)
                    ImGui.Checkbox($"Ensure reliability##{key}Reliability", ref TempConfigs[key].EnsureReliability);
                if (P.Config.RaphaelSolverConfig.AllowBackloadProgress)
                    ImGui.Checkbox($"Backload progress##{key}Progress", ref TempConfigs[key].BackloadProgress);
                if (P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist)
                    ImGui.Checkbox($"Allow heart and soul usage##{key}HS", ref TempConfigs[key].HeartAndSoul);
                if (P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist)
                    ImGui.Checkbox($"Allow quick innovation usage##{key}QI", ref TempConfigs[key].QuickInno);

                if (inProgress)
                    ImGui.EndDisabled();

                if (craft.StatLevel >= 7)
                {
                    if (!inProgress)
                    {
                        ImGuiEx.LineCentered(() =>
                        {
                            if (ImGui.Button("Build Raphael Solution", new Vector2(config.GetLargestName(), 25f.Scale())))
                            {
                                Build(craft, TempConfigs[key]);
                            }
                        });
                    }
                    else
                    {
                        ImGuiEx.LineCentered(() =>
                        {
                            if (ImGui.Button("Cancel Raphael Generation", new Vector2(config.GetLargestName(), 25f.Scale())))
                            {
                                Tasks.TryRemove(key, out var task);
                                task.Cancellation.Cancel();
                            }
                        });
                    }
                }

                if (TempConfigs[key].EnsureReliability && ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Ensuring quality is enabled, no support shall be provided when its enabled\nDue to problems that can be caused.");
                    ImGui.EndTooltip();
                }

                if (TempConfigs[key].HeartAndSoul || TempConfigs[key].QuickInno)
                {
                    ImGui.Text("Specialist actions are enabled, this can slow down the solver a lot.");
                }

                if (inProgress)
                {
                    ImGuiEx.TextCentered("Generating...");
                }
            }
        }
    }

    public class RaphaelSolverSettings
    {
        public bool AllowEnsureReliability = false;
        public bool AllowBackloadProgress = true;
        public bool ShowSpecialistSettings = false;
        public bool ExactCraftsmanship = false;
        public bool AutoGenerate = false;
        public bool AutoSwitch = false;
        public bool AutoSwitchOnAll = false;
        public bool AutoSwitchOverManual = true;
        public int MaximumThreads = 0;
        public bool GenerateOnExperts = false;
        public int TimeOutMins = 1;
        public int MaxStellarHand = 2;
        public bool DefaultRaphSolver = false;
        public bool FallbackToSolverIfRaphaelLocked = true;
        public string FallbackSolverType = typeof(StandardSolverDefinition).FullName!;
        public int FallbackSolverFlavour = 0;
        public bool Draw()
        {
            bool changed = false;
            try
            {
                ImGui.Indent();
                ImGui.TextWrapped($"Raphael settings can change the performance and system memory consumption. If you have low amounts of RAM try not to change settings, recommended minimum amount of RAM free is 2GB");

                if (ImGui.SliderInt("Maximum Threads", ref MaximumThreads, 0, Environment.ProcessorCount))
                {
                    P.Config.Save();
                }
                ImGuiEx.TextWrapped("By default uses all it can, but on lower end machines you might need to use less cpu at the cost of speed. (0 = everything)");

                changed |= ImGui.Checkbox("Ensure 100% reliability in macro generation", ref AllowEnsureReliability);
                ImGui.PushTextWrapPos(0);
                ImGui.TextColored(new System.Numerics.Vector4(255, 0, 0, 1), "Ensuring reliability may not always work and is very CPU and RAM intensive, suggested RAM at least 16GB+ spare. NO SUPPORT SHALL BE GIVEN IF YOU HAVE THIS ON");
                ImGui.PopTextWrapPos();
                changed |= ImGui.Checkbox("Allow backloading of progress in macro generation", ref AllowBackloadProgress);
                changed |= ImGui.Checkbox("Show specialist options when available", ref ShowSpecialistSettings);
                changed |= ImGui.Checkbox($"Automatically generate a solution if a valid one hasn't been created.", ref AutoGenerate);

                if (AutoGenerate)
                {
                    ImGui.Indent();
                    changed |= ImGui.Checkbox($"Generate on Expert Recipes", ref GenerateOnExperts);
                    ImGui.Unindent();
                }

                changed |= ImGui.Checkbox($"Automatically switch to the Raphael Solver once a solution has been created.", ref AutoSwitch);

                if (AutoSwitch)
                {
                    ImGui.Indent();
                    changed |= ImGui.Checkbox($"Apply to all valid crafts", ref AutoSwitchOnAll);
                    changed |= ImGui.Checkbox("Apply over crafts that already have a macro assigned to them", ref AutoSwitchOverManual);
                    ImGui.Unindent();
                }

                changed |= ImGui.SliderInt("Max Stellar Steady Hand usage", ref MaxStellarHand, 0, 2);

                ImGuiComponents.HelpMarker("This is only for missions that have Stellar Steady Hand, will limit how many are allowed per macro (Raphael may still use less in its solutions).");

                changed |= ImGui.SliderInt("Timeout solution generation", ref TimeOutMins, 1, 15);

                ImGuiComponents.HelpMarker($"If a solution takes longer than this many minutes to generate, it will cancel the generation task.");

                changed |= ImGui.Checkbox("Set Raphael as Default Solver For Recipes Without a Solver Set.", ref DefaultRaphSolver);

                ImGuiComponents.HelpMarker("Once the solver is set with this setting, turning this setting off will still keep it saved as Raphael.");

                changed |= ImGui.Checkbox("Fallback to another solver if Raphael is locked.", ref FallbackToSolverIfRaphaelLocked);

                ImGuiComponents.HelpMarker("This will prevent Raphael from being used if it is currently locked, meaning another solver will automatically be set instead.");

                if (FallbackToSolverIfRaphaelLocked)
                {
                    ImGui.Indent();

                    var currentFallbackName = CraftingProcessor.GetSolverDefinitions().FirstOrDefault(x => x.Def.GetType().FullName == FallbackSolverType && x.Flavour == FallbackSolverFlavour).Name ?? "Unknown";

                    if (ImGui.BeginCombo("##fallbackSolver", currentFallbackName))
                    {
                        foreach (var opt in CraftingProcessor.GetSolverDefinitions().OrderBy(x => x.Priority))
                        {
                            if (opt == default) continue;
                            if (opt.Def.GetType() == typeof(RaphaelSolverDefintion)) continue;
                            if (opt.Def.GetType() == typeof(ExpertSolverDefinition)) continue;
                            if (opt.UnsupportedReason.Length > 0)
                            {
                                ImGui.Text($"{opt.Name} is unsupported - {opt.UnsupportedReason}");
                            }
                            else
                            {
                                bool selected = opt.Def.GetType().FullName == FallbackSolverType;
                                if (ImGui.Selectable(opt.Name, selected))
                                {
                                    FallbackSolverType = opt.Def.GetType().FullName!;
                                    FallbackSolverFlavour = opt.Flavour;
                                    changed = true;
                                }
                            }
                        }

                        ImGui.EndCombo();
                    }

                    ImGui.Unindent();
                }

                if (ImGui.Button($"Clear raphael macro cache (Currently {P.Config.RaphaelSolverCacheV5.Count} stored)"))
                {
                    P.Config.RaphaelSolverCacheV5.Clear();
                    changed |= true;
                }

                ImGui.Unindent();
                return changed;
            }
            catch { }
            return changed;
        }
    }

    public class RaphaelSolutionConfig
    {
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
