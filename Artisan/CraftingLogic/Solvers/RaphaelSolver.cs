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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Artisan.CraftingLogic.Solvers.MacroSolverSettings;

namespace Artisan.CraftingLogic.Solvers
{
    public class RaphaelSolverDefintion : ISolverDefinition
    {
        public Solver Create(CraftState craft, int flavour)
        {
            if (craft.StatLevel < 7)
                return new StandardSolver();

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
        internal static readonly ConcurrentDictionary<RaphaelOptions, RaphaelTaskInfo> Tasks = [];
        [NonSerialized]
        public static List<RaphaelSolutionConfig> TempConfigs = [];

        [NonSerialized]
        public const string RaphaelFileName = "RaphaelCache.dat";

        internal sealed class RaphaelTaskInfo(CancellationTokenSource cts, Task task, bool fromStartCraft)
        {
            public CancellationTokenSource Cancellation { get; set; } = cts;
            public Task Task { get; set; } = task;
            public volatile bool FromStartCraft = fromStartCraft;
            public volatile bool Succeeded;
        }

        public static void Build(CraftState craft, RaphaelSolutionConfig config, bool fromStartCraft = false)
        {
            if (craft.StatLevel < 7) return; // can't run raphael without Master's Mend

            // don't build if a task is already running with these options
            var key = GetOptions(craft, config);
            if (!CLIExists() || Tasks.ContainsKey(key)) return;

            // nuke the old macro if one exists
            P.Config.RaphaelSolverCacheV6.TryRemove(key, out _);

            var manipulation = config.HasManipulation ? "--manipulation" : "";
            var itemText = $"--custom-recipe {craft.LevelTable.RowId} {craft.CraftProgress} {(craft.CraftCollectible && !craft.IsCosmic ? craft.CraftQualityMin3 : craft.CraftQualityMax)} {craft.CraftDurability} {(craft.CraftExpert ? "1" : "0")} --stellar-steady-hand {Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand)}";

            var argsList = new List<string>
            {
                $"--initial {craft.InitialQuality}"
            };

            if (config.EnsureReliability) argsList.Add("--adversarial");
            if (config.BackloadProgress) argsList.Add("--backload-progress");
            if (config.UseHeartAndSoul) argsList.Add("--heart-and-soul");
            if (config.UseQuickInno) argsList.Add("--quick-innovation");
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

                        P.Config.RaphaelSolverCacheV6[key] = new MacroSolverSettings.Macro
                        {
                            ID = GetNewID(),
                            Name = GetTextKey(craft, config),
                            Steps = MacroUI.ParseMacro(cleansedOutput),
                            Options = new RaphaelOptions()
                            {
                                SkipQualityIfMet = false,
                                UpgradeProgressActions = false,
                                UpgradeQualityActions = false,
                                MinCP = craft.StatCP,
                                MinControl = craft.StatControl,
                                MinCraftsmanship = craft.StatCraftsmanship,
                                Level = craft.CraftLevel,
                                Progress = craft.CraftProgress,
                                QualityMax = craft.CraftQualityMax,
                                Durability = craft.CraftDurability,
                                IsExpert = craft.CraftExpert,
                                InitialQuality = craft.InitialQuality,
                                IsSpecialist = craft.Specialist,
                                SteadyHandUses = Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand),
                                SolutionConfig = config
                            }
                        };

                        Svc.Log.Debug($"Saved new macro to Raphael cache, ID: {P.Config.RaphaelSolverCacheV6[key].ID}");

                        info.Succeeded = P.Config.RaphaelSolverCacheV6[key]?.Steps.Count > 0;
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

        private static void AutoSwitch(CraftState craft, RaphaelOptions key)
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
                                //Svc.Log.Information($"Switching {c.Recipe.RowId} ({c.Recipe.ItemResult.Value.Name}) to Raphael solver");
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

        public static RaphaelOptions GetOptions(CraftState craft, RaphaelSolutionConfig? config)
        {
            config ??= GetRaphConfig(craft);

            return new RaphaelOptions()
            { 
                MinCraftsmanship = craft.StatCraftsmanship,
                MinControl = craft.StatControl,
                MinCP = craft.StatCP,
                Level = craft.CraftLevel,
                Progress = craft.CraftProgress,
                QualityMax = craft.CraftQualityMax,
                Durability = craft.CraftDurability,
                IsExpert = craft.CraftExpert,
                InitialQuality = craft.InitialQuality,
                IsSpecialist = craft.Specialist,
                SteadyHandUses = Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand),
                SolutionConfig = config
            };
        }

        // this key is for identifying solutions while debugging; code should look up solutions by their RaphaelOptions
        public static string GetTextKey(CraftState craft, RaphaelSolutionConfig config)
        {
            return $"{craft.CraftLevel}/{craft.CraftProgress}/{craft.CraftQualityMax}/{craft.CraftDurability}-{craft.StatCraftsmanship}/{craft.StatControl}/{craft.StatCP}-{(craft.CraftExpert ? "Ex" : "St")}/{craft.InitialQuality}/{(craft.Specialist ? "Sp" : "Re")}/Steady{Math.Min(craft.CurrentSteadyHandCharges, P.Config.RaphaelSolverConfig.MaxStellarHand)}-{(config.UseHeartAndSoul ? "1" : "0")}/{(config.UseQuickInno ? "1" : "0")}/{(config.HasManipulation ? "1" : "0")}/{(config.EnsureReliability ? "1" : "0")}/{(config.BackloadProgress ? "1" : "0")}";
        }

        public static string GetKeyForLookups(RaphaelOptions opt)
        {
            return $"{opt.Level}/{opt.Progress}/{opt.QualityMax}/{opt.Durability}-{opt.MinCraftsmanship}/{opt.MinControl}/{opt.MinCP}-{(opt.IsExpert ? "Ex" : "St")}/{opt.InitialQuality}/{(opt.IsSpecialist ? "Sp" : "Re")}/Steady{opt.SteadyHandUses}-{(opt.SolutionConfig.UseHeartAndSoul ? "1" : "0")}/{(opt.SolutionConfig.UseQuickInno ? "1" : "0")}/{(opt.SolutionConfig.HasManipulation ? "1" : "0")}/{(opt.SolutionConfig.EnsureReliability ? "1" : "0")}/{(opt.SolutionConfig.BackloadProgress ? "1" : "0")}";
        }

        public static IEnumerable<CraftState> AllValidCrafts(RaphaelOptions key)
        {
            var recipes = LuminaSheets.RecipeSheet.Values.Where(x => x.RecipeLevelTable.Value.ClassJobLevel == key.Level);
            foreach (var recipe in recipes)
            {
                var state = Crafting.BuildCraftStateForRecipe(default, (Job)((uint)Job.CRP + recipe.CraftType.RowId), recipe);
                if (state.StatLevel < 7) continue;

                if (key.Progress == state.CraftProgress &&
                    key.QualityMax == state.CraftQualityMax &&
                    key.Durability == state.CraftDurability)
                    yield return state;
            }
        }

        public static RaphaelSolutionConfig GetRaphConfig(CraftState craft, bool checkDelins = false)
        {
            var globalRaph = P.Config.RaphaelSolverConfig;
            var hasDelins = Crafting.DelineationCount() > 0;
            return new RaphaelSolutionConfig()
            {
                HasManipulation = craft.UnlockedManipulation,
                EnsureReliability = globalRaph.AllowEnsureReliability && globalRaph.EnsureReliability && !craft.CraftExpert,
                BackloadProgress = globalRaph.AllowBackloadProgress && globalRaph.BackloadProgress,
                UseHeartAndSoul = globalRaph.ShowSpecialistSettings && globalRaph.UseHeartAndSoul && craft.Specialist && (!checkDelins || hasDelins),
                UseQuickInno = globalRaph.ShowSpecialistSettings && globalRaph.UseQuickInno && craft.Specialist && (!checkDelins || hasDelins),
            };
        }

        public static bool HasSolution(CraftState craft, out Macro? raphaelSolution) => HasSolution(craft, null, out raphaelSolution);

        public static bool HasSolution(CraftState craft, RaphaelSolutionConfig? config, out Macro? raphaelSolution)
        {
            config ??= GetRaphConfig(craft);

            var key = GetOptions(craft, config);
            raphaelSolution = null;
            var hasKey = P.Config.RaphaelSolverCacheV6.ContainsKey(key);
            if (hasKey)
            {
                raphaelSolution = P.Config.RaphaelSolverCacheV6[key];
                return true;
            }
            else
                return false;
        }

        public static bool InProgress(CraftState craft, RaphaelSolutionConfig config) => Tasks.TryGetValue(GetOptions(craft, config), out var _);

        public static bool InProgressAny() => !Tasks.IsEmpty;

        internal static bool CLIExists()
        {
            return File.Exists(Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.bin"));
        }

        public static void DrawRaphaelDropdown(CraftState craft, bool liveStats = true)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(craft.RecipeId) ?? new();
            if (CLIExists())
            {
                // snapshot the current generation settings in case they change before the next generate
                var curConfig = GetRaphConfig(craft).JSONClone();
                var hasSolution = HasSolution(craft, curConfig, out var solution);
                var opts = GetOptions(craft, curConfig);
                var keyStr = GetTextKey(craft, curConfig);

                var solverIsRaph = config.SolverIsRaph;
                if (!hasSolution)
                {
                    if (solverIsRaph)
                        ImGuiEx.TextCentered(ImGuiColors.DalamudRed, "No Raphael solution generated");
                    if (P.Config.RaphaelSolverConfig.AutoGenerate && CraftingProcessor.GetAvailableSolversForRecipe(craft, true).Any() && (!craft.CraftExpert || (craft.CraftExpert && P.Config.RaphaelSolverConfig.GenerateOnExperts)))
                    {
                        Build(craft, curConfig);
                    }
                }

                ImGui.Separator();

                var inProgress = InProgress(craft, curConfig);

                if (inProgress)
                    ImGui.BeginDisabled();

                var showReliability = P.Config.RaphaelSolverConfig.AllowEnsureReliability && !craft.CraftExpert;
                var showBackload = P.Config.RaphaelSolverConfig.AllowBackloadProgress;
                var showSpecialist = P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist;

                if (showReliability || showBackload || showSpecialist)
                {
                    ImGuiEx.Text(ImGuiColors.DalamudGrey, "Raphael Solver Settings");
                }
                else
                {
                    ImGui.Dummy(new Vector2(0, 2f));
                }

                if (showReliability)
                {
                    ImGui.Checkbox($"Ensure 100% reliability##{keyStr}Reliability", ref P.Config.RaphaelSolverConfig.EnsureReliability);
                    ImGuiComponents.HelpMarker("Try to find a solution that works for any permutation of per-step crafting conditions. EXTREMELY RAM AND CPU INTENSIVE NO SUPPORT WILL BE GIVEN IF YOU HAVE THIS ON");
                }
                if (showBackload)
                {
                    ImGui.Checkbox($"Backload progress##{keyStr}Progress", ref P.Config.RaphaelSolverConfig.BackloadProgress);
                    ImGuiComponents.HelpMarker($"Find a solution that finishes quality before starting on progress. Useful for simple expert recipes where the Malleable condition might otherwise cause an early finish.");
                }
                if (showSpecialist)
                {
                    P.PluginUi.ExpertSettingsUI.CheckboxWithIcons($"{keyStr}HS", ref P.Config.RaphaelSolverConfig.UseHeartAndSoul, "Allow [s!HeartAndSoul]");
                    ImGuiComponents.HelpMarker($"The generated macro will require one crafter's delineation per craft.");

                    P.PluginUi.ExpertSettingsUI.CheckboxWithIcons($"{keyStr}QI", ref P.Config.RaphaelSolverConfig.UseQuickInno, "Allow [s!QuickInnovation]");
                    ImGuiComponents.HelpMarker($"The generated macro will require one crafter's delineation per craft.");
                }

                if (showReliability || showBackload || showSpecialist)
                {
                    ImGui.Dummy(new Vector2(0, 5f));
                }

                if (inProgress)
                    ImGui.EndDisabled();

                if (craft.StatLevel >= 7) // can't run the raphael generator without Master's Mend
                {
                    if (!inProgress)
                    {
                        string verb = hasSolution ? "Rebuild" : "Build";
                        ImGuiEx.LineCentered(() =>
                        {
                            if (ImGui.Button($"{verb} Raphael Solution", new Vector2(config.GetLargestName(), 25f.Scale())))
                            {
                                Build(craft, curConfig);
                            }
                        });
                    }
                    else
                    {
                        ImGuiEx.LineCentered(() =>
                        {
                            if (ImGui.Button("Cancel Raphael Generation", new Vector2(config.GetLargestName(), 25f.Scale())))
                            {
                                Tasks.TryRemove(opts, out var task);
                                task.Cancellation.Cancel();
                            }
                        });
                    }
                }

                if (curConfig.EnsureReliability && ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("\"Ensure 100% reliability\" is enabled, which is very CPU- and RAM-intensive.\nDue to this, no support will be given if you have issues.");
                    ImGui.EndTooltip();
                }

                if (curConfig.UseHeartAndSoul || curConfig.UseQuickInno)
                {
                    ImGuiEx.Text(ImGuiColors.DalamudYellow, "Specialist actions are enabled. This can slow down the solver a lot.");
                }

                if (inProgress)
                {
                    ImGuiEx.TextCentered("Generating...");
                }

                ImGui.Dummy(new Vector2(0, 2f));
            }
        }

        public static int GetNewID(Configuration? config = null)
        {
            config ??= P.Config;

            var rng = new Random();
            var id = rng.Next(50001, 10000000);
            while (config.RaphaelSolverCacheV6.Values.FirstOrDefault(m => m.ID == id) != null)
                id = rng.Next(50001, 10000000);
            return id;
        }

        // deprecated (string keys are now RaphaelOptions objects), should only be used when converting from v5
        public static (int Level, int Prog, int Qual, int Dur, int Initial, int Crafts, int Control, int CP, bool SP, bool IsEx, int Hands) V5KeyParts(string key)
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
            var isEx = parts[5].Split('-')[1] == "Ex";
            var hands = 0;
            if (parts.Length >= 9)
                hands = parts[8].Substring(6).ParseInt() ?? 0;

            return (lvl, prog, qual, dur, initial, crafts, ctrl, cp, sp, isEx, hands);
        }

        public static void LoadRaphaelCache(Configuration config, bool deleteV5)
        {
            // keeping "deletev5" as a param, currently false, so we can delete it in the future to reduce config file size
            // v5 cache won't be loaded after being converted if a v6 cache exists, just keeping it as a backup right now

            // either load the current cache or, if missing, try to convert a v5 cache
            var v6cache = LoadRaphaelCacheFromFile(config);
            if (!v6cache.IsEmpty)
            {
                Svc.Log.Info($"Loaded existing Raphael cache from file ({v6cache.Keys.Count} entries)");
                config.RaphaelSolverCacheV6 = v6cache;
            }
            else if (!config.RaphaelSolverCacheV5.IsEmpty && !config.RaphaelV5Converted)
            {
                Svc.Log.Info($"Updating Raphael cache from v5 to v6 ({config.RaphaelSolverCacheV5.Keys.Count} entries)");
                ConvertV5ToV6(config);
            }

            if (deleteV5)
                config.RaphaelSolverCacheV5.Clear();
        }

        private static ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> LoadRaphaelCacheFromFile(Configuration config)
        {
            var file = new FileInfo(Path.Combine(config.ConfigDirectory.FullName, RaphaelFileName));
            if (!file.Exists)
                return [];

            try
            {
                Svc.Log.Information("Loading Raphael cache from file...");
                var stringCache = RaphaelCache.ReadCacheFile(file);
                if (stringCache.Count > 0)
                    return RaphaelCache.ConvertFromStringCache(stringCache);
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Error reading raphael cache file \"{file.FullName}\":\n{e}");
            }

            return [];
        }

        public static void WriteRaphaelCache(Configuration config)
        {
            var file = new FileInfo(Path.Combine(config.ConfigDirectory.FullName, RaphaelFileName));
            try
            {
                var stringCache = RaphaelCache.ConvertToStringCache(config.RaphaelSolverCacheV6);
                var json = JObject.FromObject(stringCache).ToString();
                File.WriteAllText(file.FullName, json);
                P.PluginUi.RaphaelCacheUI.Table = null;
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Error saving raphael cache file \"{file.FullName}\":\n{e}");
            }
        }
        public static void ConvertV5ToV6(Configuration config)
        {
            ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> v6Cache = [];

            // check if the current character has Manipulation on every job as a guess for v5 solves
            bool hasAllManip = true;
            List<Job> allJobs = [Job.CRP, Job.BSM, Job.ARM, Job.GSM, Job.LTW, Job.WVR, Job.ALC, Job.CUL];
            foreach (Job j in allJobs)
            {
                hasAllManip &= CharacterInfo.IsManipulationUnlocked(j);
            }

            foreach (var (key, macro) in config.RaphaelSolverCacheV5)
            {
                var stats = V5KeyParts(key);

                // try to guess some raph generation settings based on macro steps
                bool v6HS = false;
                bool v6QI = false;
                bool v6Manip = hasAllManip;
                bool v6Backload = true;

                bool hasProgress = false;
                foreach (MacroStep step in macro.Steps)
                {
                    if (step.Action == Skills.HeartAndSoul)
                        v6HS = true;
                    if (step.Action == Skills.QuickInnovation)
                        v6QI = true;
                    if (step.Action == Skills.Manipulation)
                        v6Manip = true;

                    // not backloading progress if any quality action happens after any progress action
                    if (MacroSolver.ActionIsProgress(step.Action))
                        hasProgress = true;
                    if (hasProgress && MacroSolver.ActionIsQuality(step.Action))
                        v6Backload = false;
                }

                string newKey = $"{key}-{(v6HS ? "1" : "0")}/{(v6QI ? "1" : "0")}/{(v6Manip ? "1" : "0")}/0/{(v6Backload ? "1" : "0")}";
                RaphaelOptions newOpts = new()
                {
                    SkipQualityIfMet = macro.Options.SkipQualityIfMet,
                    UpgradeProgressActions = macro.Options.UpgradeProgressActions,
                    UpgradeQualityActions = macro.Options.UpgradeQualityActions,
                    MinCP = stats.CP,
                    MinControl = stats.Control,
                    MinCraftsmanship = stats.Crafts,
                    Level = stats.Level,
                    Progress = stats.Prog,
                    QualityMax = stats.Qual,
                    Durability = stats.Dur,
                    IsExpert = stats.IsEx,
                    InitialQuality = stats.Initial,
                    IsSpecialist = stats.SP,
                    SteadyHandUses = stats.Hands,
                    SolutionConfig = new RaphaelSolutionConfig()
                    {
                        UseHeartAndSoul = v6HS,
                        UseQuickInno = v6QI,
                        HasManipulation = v6Manip,
                        EnsureReliability = false,  // better to regenerate than to assume
                        BackloadProgress = v6Backload
                    }
                };

                Macro v6Macro = new()
                {
                    ID = GetNewID(config),  // v5 didn't enforce ID uniqueness so we'll assign new ones just in case
                    Name = newKey,
                    Steps = macro.Steps,
                    Options = newOpts
                };

                v6Cache[newOpts] = v6Macro;
            }

            config.RaphaelSolverCacheV6 = v6Cache;
            config.RaphaelV5Converted = true;
        }

        public static Dictionary<string, MacroSolverSettings.Macro> ConvertToStringCache(ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> cache)
        {
            Dictionary<string, MacroSolverSettings.Macro> stringCache = [];

            foreach (var (key, macro) in cache)
            {
                JsonSerializerOptions options = new() { IncludeFields = true };
                string jsonKey = JsonSerializer.Serialize(key, options);
                stringCache[jsonKey] = macro;
            }

            return stringCache;
        }

        public static ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> ConvertFromStringCache(Dictionary<string, MacroSolverSettings.Macro> stringCache)
        {
            ConcurrentDictionary<RaphaelOptions, MacroSolverSettings.Macro> cache = [];

            foreach (var (key, macro) in stringCache)
            {
                var json = JObject.Parse(key);
                var parsedKey = json.ToObject<RaphaelOptions>() ?? new();
                if (parsedKey.Level > 0)
                    cache[parsedKey] = macro;
            }

            return cache;
        }

        public static Dictionary<string, MacroSolverSettings.Macro> ReadCacheFile(FileInfo file)
        {
            if (!file.Exists)
                return [];

            try
            {
                var raw = File.ReadAllText(file.FullName);
                var json = JObject.Parse(raw);
                var parsed = json.ToObject<Dictionary<string, MacroSolverSettings.Macro>>() ?? [];
                return parsed;
            }
            catch (Exception e)
            {
                Svc.Log.Error($"Error reading raphael cache file \"{file.FullName}\":\n{e}");
                return [];
            }
        }
    }

    public class RaphaelSolverSettings
    {
        // these enable the relevant checkboxes on the crafting log mini-menu
        public bool AllowEnsureReliability = false;
        public bool AllowBackloadProgress = true;
        public bool ShowSpecialistSettings = false;
        // these track the actual values of those checkboxes
        public bool EnsureReliability = false;
        public bool BackloadProgress = true;
        public bool UseHeartAndSoul = false;
        public bool UseQuickInno = false;

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
                string ProgressString = LuminaSheets.AddonSheet[213].Text.ToString();
                string QualityString = LuminaSheets.AddonSheet[216].Text.ToString();
                string ConditionString = LuminaSheets.AddonSheet[215].Text.ToString();

                ImGui.Dummy(new Vector2(0, 2f));
                ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, $"Raphael settings can affect your performance and system memory consumption while generating a macro.\r\nIf you have low amounts of RAM, try not to change these settings. At least 2GB of free RAM is recommended.");

                ImGui.Indent();

                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Performance");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                ImGui.PushItemWidth(250);
                changed |= ImGui.SliderInt("Maximum threads (0 for all)", ref MaximumThreads, 0, Environment.ProcessorCount);
                ImGuiComponents.HelpMarker("By default the Raphael generator uses everything it can (setting = 0), but on lower-end machines you might need to use less CPU at the cost of speed.");

                changed |= ImGui.Checkbox("Allow \"Ensure 100% reliability\"", ref AllowEnsureReliability);
                ImGuiComponents.HelpMarker("Enables a checkbox on the Crafting Log mini-menu. This setting will try to find a solution that works for any permutation of per-step crafting conditions.");
                ImGuiEx.TextWrapped(new System.Numerics.Vector4(255, 0, 0, 1), "Ensuring reliability may not always work and is very CPU and RAM intensive. 16GB+ of spare RAM is recommended. NO SUPPORT SHALL BE GIVEN IF YOU HAVE THIS ON");

                ImGui.Dummy(new Vector2(0, 2f));
                changed |= ImGui.SliderInt("Macro generation timeout (minutes)", ref TimeOutMins, 1, 15);
                ImGuiComponents.HelpMarker($"If a solution takes longer than this many minutes to generate, macro generation will be canceled.");

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Automatic Usage");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox($"Automatically generate a Raphael macro if a valid one hasn't been created", ref AutoGenerate);
                if (AutoGenerate)
                {
                    ImGui.Indent();
                    changed |= ImGui.Checkbox($"Automatically generate on expert recipes", ref GenerateOnExperts);
                    ImGui.Unindent();
                }

                changed |= ImGui.Checkbox("Automatically switch to the Raphael macro once one has been created", ref AutoSwitch);
                if (AutoSwitch)
                {
                    ImGui.Indent();
                    changed |= ImGui.Checkbox("Apply to all valid crafts", ref AutoSwitchOnAll);
                    changed |= ImGui.Checkbox("Overwrite crafts that already have a macro assigned to them", ref AutoSwitchOverManual);
                    ImGui.Unindent();
                }

                changed |= ImGui.Checkbox("Use Raphael as default solver", ref DefaultRaphSolver);
                ImGuiComponents.HelpMarker("Important notes:\r\n\r\n• Any recipes opened with Artisan before changing this setting will still use their current solvers.\r\n• If you disable this setting, any recipes that were set to Raphael will stay that way until changed.\r\n• The Standard Solver is used as the default solver if this setting is disabled.");

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Macro Generation");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                ImGui.Dummy(new Vector2(0, 2f));
                changed |= ImGui.Checkbox($"Allow \"Backload {ProgressString.ToLower()}\"", ref AllowBackloadProgress);
                ImGuiComponents.HelpMarker($"Enables a checkbox on the Crafting Log mini-menu. This setting ensures that {QualityString.ToLower()} is finished before starting on {ProgressString.ToLower()}. Useful for simple expert recipes where the Malleable condition might otherwise cause an early finish.");

                changed |= ImGui.Checkbox("Allow specialist actions when available", ref ShowSpecialistSettings);
                ImGuiComponents.HelpMarker($"Enables checkboxes on the Crafting Log mini-menu that let the solver use {Skills.HeartAndSoul.NameOfAction()} and {Skills.QuickInnovation.NameOfAction()}.");

                changed |= P.PluginUi.ExpertSettingsUI.SliderIntWithIcons("MaxStellarHand", ref MaxStellarHand, 0, 2, "Max [s!SteadyHand] uses per craft");
                P.PluginUi.ExpertSettingsUI.HelpMarkerWithIcons(["This setting only applies to Cosmic Exploration recipes on missions with [s!SteadyHand].", "The Raphael solver will use UP TO this many charges depending on the recipe's difficulty."]);

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

                ImGui.Dummy(new Vector2(0, 5f));
                if (ImGui.Button($"Clear Raphael Macro Cache (currently {P.Config.RaphaelSolverCacheV6.Count} stored)"))
                {
                    P.Config.RaphaelSolverCacheV6.Clear();
                    changed |= true;
                }

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 10f));

                return changed;
            }
            catch { }
            return changed;
        }
    }

    public class RaphaelSolutionConfig
    {
        public bool HasManipulation = false;
        public bool EnsureReliability = false;
        public bool BackloadProgress = false;
        public bool UseHeartAndSoul = false;
        public bool UseQuickInno = false;
    }

    public class RaphaelOptions : MacroOptions
    {
        public int Level = 0;
        public int Progress = 0;
        public int QualityMax = 0;
        public int Durability = 0;
        public bool IsExpert = false;
        public int InitialQuality = 0;
        public bool IsSpecialist = false;
        public int SteadyHandUses = 0;
        public RaphaelSolutionConfig SolutionConfig = new();

        public override int GetHashCode() => RaphaelCache.GetKeyForLookups(this).GetHashCode();
        public override bool Equals(object obj) => obj != null && obj.GetHashCode() == this.GetHashCode();
    }
}
