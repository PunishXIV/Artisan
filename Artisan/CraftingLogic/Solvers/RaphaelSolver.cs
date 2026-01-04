using Artisan.Autocraft;
using Artisan.GameInterop;
using Artisan.RawInformation;
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
            //if (RaphaelCache.HasSolution(craft, out var solution))
            yield return new(this, 3, 0, $"Raphael Recipe Solver");
        }
    }

    internal static class RaphaelCache
    {
        internal static readonly ConcurrentDictionary<string, Tuple<CancellationTokenSource, Task>> Tasks = [];
        [NonSerialized]
        public static Dictionary<string, RaphaelSolutionConfig> TempConfigs = new();

        public static void Build(CraftState craft, RaphaelSolutionConfig config)
        {
            var key = GetKey(craft);

            if (CLIExists() && !Tasks.ContainsKey(key))
            {
                P.Config.RaphaelSolverCacheV4.TryRemove(key, out _);

                Svc.Log.Information("Spawning Raphael process");

                var manipulation = craft.UnlockedManipulation ? "--manipulation" : "";
                var itemText = $"--custom-recipe {craft.LevelTable.RowId} {craft.CraftProgress} {(craft.CraftCollectible ? craft.CraftQualityMin3 : craft.CraftQualityMax)} {craft.CraftDurability} {(craft.CraftExpert ? "1" : "0")}";
                var extraArgsBuilder = new StringBuilder();

                extraArgsBuilder.Append($"--initial {craft.InitialQuality} "); // must always have a space after

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
                        Arguments = $"solve {itemText} {manipulation} --level {craft.StatLevel} --stats {craft.StatCraftsmanship} {craft.StatControl} {craft.StatCP} {extraArgsBuilder} --output-variables action_ids", // Command to execute
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                Svc.Log.Information(process.StartInfo.Arguments);

                var cts = new CancellationTokenSource();
                cts.Token.Register(() =>
                {
                    try
                    {
                        process?.Kill();
                    }
                    catch (Exception ex)
                    {
                        ex.Log("Couldn't remove process, likely already completed.");
                    }
                    Tasks.TryRemove(key, out var _);
                }
                );
                cts.CancelAfter(TimeSpan.FromMinutes(P.Config.RaphaelSolverConfig.TimeOutMins));

                var task = Task.Run(() =>
                {
                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd().Trim();
                    if (process.ExitCode != 0)
                    {
                        DuoLog.Error(error.Split('\r', '\n')[1]);
                        cts.Cancel();
                        return;
                    }
                    cts.Token.ThrowIfCancellationRequested();

                    Svc.Log.Information("Raphael process completed, output generated");
                    var rng = new Random();
                    var ID = rng.Next(50001, 10000000);
                    while (P.Config.RaphaelSolverCacheV4.Any(kv => kv.Value.ID == ID))
                        ID = rng.Next(50001, 10000000);

                    var cleansedOutput = output.Replace("[", "").Replace("]", "").Replace("\"", "").Split(", ").Select(x => int.TryParse(x, out int n) ? n : 0);
                    P.Config.RaphaelSolverCacheV4[key] = new MacroSolverSettings.Macro()
                    {
                        ID = ID,
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

                    Svc.Log.Information("Raphael macro generated and stored in cache.");
                    if (P.Config.RaphaelSolverCacheV4[key] == null || P.Config.RaphaelSolverCacheV4[key].Steps.Count == 0)
                    {
                        Svc.Log.Error($"Raphael failed to generate a valid macro. This could be one of the following reasons:" +
                            $"\n- If you are not running Windows, Raphael may not be compatible with your OS." +
                            $"\n- You cancelled the generation." +
                            $"\n- Raphael just gave up after not finding a result.{(P.Config.RaphaelSolverConfig.AutoGenerate ? "\nAutomatic generation will be disabled as a result." : "")}");
                        P.Config.RaphaelSolverConfig.AutoGenerate = false;
                        cts.Cancel();
                        return;
                    }

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
                    Svc.Log.Information("Saving config changes after Raphael generation.");
                    P.Config.Save();

                    Svc.Log.Information("Tidying up task.");
                    Tasks.TryRemove(key, out var _);
                }, cts.Token);

                Tasks.TryAdd(key, new(cts, task));
            }
        }

        public static string GetKey(CraftState craft)
        {
            return $"{craft.CraftLevel}/{craft.CraftProgress}/{craft.CraftQualityMax}/{craft.CraftDurability}-{craft.StatCraftsmanship}/{craft.StatControl}/{craft.StatCP}-{(craft.CraftExpert ? "Ex" : "St")}/{craft.InitialQuality}/{(craft.Specialist ? "Sp" : "Re")}";
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
            var sol = P.Config.RaphaelSolverCacheV4.FirstOrNull(x => x.Key == thisKey);
            if (sol != null)
            {
                raphaelSolutionConfig = sol.Value.Value;
                return true;
            }
            else
                return false;

            //foreach (var solution in P.Config.RaphaelSolverCacheV4.OrderByDescending(x => KeyParts(x.Key).Control))
            //{
            //    if (solution.Value.Steps.Count == 0) continue;

            //    var solKey = KeyParts(solution.Key);

            //    if (solKey.Level == craft.CraftLevel &&
            //        solKey.Prog == craft.CraftProgress &&
            //        solKey.Qual == craft.CraftQualityMax &&
            //        solKey.Crafts == craft.StatCraftsmanship &&
            //        solKey.Control <= craft.StatControl &&
            //        solKey.Initial == craft.InitialQuality &&
            //        solKey.CP <= craft.StatCP &&
            //        solKey.SP == craft.Specialist)
            //    {
            //        raphaelSolutionConfig = solution.Value;
            //        return true;
            //    }
            //}
            //return false;
        }

        public static bool InProgress(CraftState craft) => Tasks.TryGetValue(GetKey(craft), out var _);

        public static bool InProgressAny() => Tasks.Any();

        internal static bool CLIExists()
        {
            return File.Exists(Path.Join(Path.GetDirectoryName(Svc.PluginInterface.AssemblyLocation.FullName), "raphael-cli.exe"));
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
                    TempConfigs[key].EnsureReliability = P.Config.RaphaelSolverConfig.AllowEnsureReliability;
                    TempConfigs[key].BackloadProgress = P.Config.RaphaelSolverConfig.AllowBackloadProgress;
                    TempConfigs[key].HeartAndSoul = P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist;
                    TempConfigs[key].QuickInno = P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist;
                }

                var opt = CraftingProcessor.GetAvailableSolversForRecipe(craft, true).FirstOrNull(x => x.Name == $"Raphael Recipe Solver");
                var solverIsRaph = config.CurrentSolverType == opt?.Def.GetType().FullName!;
                if (hasSolution)
                {
                    var curStats = CharacterStats.GetCurrentStats();

                    if (!solverIsRaph)
                    {
                        if (liveStats)
                        {
                            ImGuiEx.TextCentered($"Raphael Solution Has Been Generated. (Click to Switch)");
                            if (ImGui.IsItemClicked())
                            {
                                config.SolverType = opt?.Def.GetType().FullName!;
                                config.SolverFlavour = (int)(opt?.Flavour);
                            }
                        }
                        else
                        {
                            ImGuiEx.TextCentered($"Raphael Solution Has Been Generated.");
                        }
                    }
                    else
                    {
                        ImGuiEx.TextCentered($"Solution Key: {key}");
                        var playerIsJob = Player.ClassJob.RowId == craft.Recipe.CraftType.RowId + 8;
                        var parts = KeyParts(key);
                        if (!playerIsJob)
                            ImGuiEx.TextCentered(ImGuiColors.DalamudOrange, $"Not currently job.");
                        else
                        {
                            if (curStats.Craftsmanship == craft.StatCraftsmanship)
                                ImGuiEx.TextCentered(ImGuiColors.HealerGreen, $"Craftsmanship meets solution requirement: {parts.Crafts}.");
                            else
                            {
                                ImGuiEx.TextCentered(ImGuiColors.DPSRed, $"There is a craftsmanship difference between the solution ({craft.StatCraftsmanship}) and current ({curStats.Craftsmanship}).\nRaphael won't be used until this is resolved.");
                                var foodIsCrafts = ConsumableChecker.GetItemConsumableProperties(LuminaSheets.ItemSheet[config.RequiredFood], false)?.Params.Any(x => x.BaseParam.RowId is 70);
                                if (foodIsCrafts == true)
                                    ImGuiEx.TextCentered(ImGuiColors.DalamudOrange, $"(Set food is craftsmanship food, this issue will likely be resolved once the buff is applied)");

                                var potIsCrafts = ConsumableChecker.GetItemConsumableProperties(LuminaSheets.ItemSheet[config.RequiredPotion], false)?.Params.Any(x => x.BaseParam.RowId is 70);
                                if (potIsCrafts == true)
                                    ImGuiEx.TextCentered(ImGuiColors.DalamudOrange, $"(Set potion is craftsmanship potion, this issue will likely be resolved once the buff is applied)");

                                if ((foodIsCrafts == null || foodIsCrafts == false) && (potIsCrafts == null || potIsCrafts == false))
                                    ImGuiEx.TextCentered(ImGuiColors.DalamudOrange, $"(You currently have a Well Fed/Medicated buff granting you craftsmanship\n that is not set as your food/potion, removing the buff(s) may resolve this)");

                                var diffPos = Math.Abs(craft.StatCraftsmanship - curStats.Craftsmanship);
                                var diffAct = (craft.StatCraftsmanship - curStats.Craftsmanship);
                                if (diffPos % 5 == 0)
                                    if (diffAct > 0)
                                        ImGuiEx.TextCentered(ImGuiColors.DalamudOrange, $"(This solution may have been generated with a Free Company Craftsmanship buff which you no longer have)");
                                    else
                                        ImGuiEx.TextCentered(ImGuiColors.DalamudOrange, $"(You may have a Free Company Craftsmanship buff that was not active when this solution was generated)");
                            }
                        }

                    }
                }
                else
                {
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

                if (P.Config.RaphaelSolverConfig.AllowEnsureReliability)
                    ImGui.Checkbox($"Ensure reliability##{key}Reliability", ref TempConfigs[key].EnsureReliability);
                if (P.Config.RaphaelSolverConfig.AllowBackloadProgress)
                    ImGui.Checkbox($"Backload progress##{key}Progress", ref TempConfigs[key].BackloadProgress);
                if (P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist)
                    ImGui.Checkbox($"Allow heart and soul usage##{key}HS", ref TempConfigs[key].HeartAndSoul);
                if (P.Config.RaphaelSolverConfig.ShowSpecialistSettings && craft.Specialist)
                    ImGui.Checkbox($"Allow quick innovation usage##{key}QI", ref TempConfigs[key].QuickInno);

                if (inProgress)
                    ImGui.EndDisabled();

                if (!inProgress)
                {
                    if (ImGui.Button("Build Raphael Solution", new Vector2(ImGui.GetContentRegionAvail().X, 25f.Scale())))
                    {
                        Build(craft, TempConfigs[key]);
                    }
                }
                else
                {
                    if (ImGui.Button("Cancel Raphael Generation", new Vector2(ImGui.GetContentRegionAvail().X, 25f.Scale())))
                    {
                        Tasks.TryRemove(key, out var task);
                        task.Item1.Cancel();
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

                changed |= ImGui.SliderInt("Timeout solution generation", ref TimeOutMins, 1, 15);

                ImGuiComponents.HelpMarker($"If a solution takes longer than this many minutes to generate, it will cancel the generation task.");

                if (ImGui.Button($"Clear raphael macro cache (Currently {P.Config.RaphaelSolverCacheV4.Count} stored)"))
                {
                    P.Config.RaphaelSolverCacheV4.Clear();
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
