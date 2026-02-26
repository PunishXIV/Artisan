using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.Logging;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.IPC
{
    internal static class IPC
    {
        private static bool stopCraftingRequest;

        public static bool StopCraftingRequest
        {
            get => stopCraftingRequest;
            set
            {
                if (value)
                {
                    StopCrafting();
                }
                else
                {
                    if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.WaitingForDutyFinder] && !Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty])
                        ResumeCrafting();
                }
                stopCraftingRequest = value;
            }
        }

        public static ArtisanMode CurrentMode;
        internal static void Init()
        {
            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.GetEnduranceStatus").RegisterFunc(GetEnduranceStatus);
            Svc.PluginInterface.GetIpcProvider<bool, object>("Artisan.SetEnduranceStatus").RegisterAction(SetEnduranceStatus);

            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.IsListRunning").RegisterFunc(IsListRunning);
            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.IsListPaused").RegisterFunc(IsListPaused);
            Svc.PluginInterface.GetIpcProvider<bool, object>("Artisan.SetListPause").RegisterAction(SetListPause);

            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.GetStopRequest").RegisterFunc(GetStopRequest);
            Svc.PluginInterface.GetIpcProvider<bool, object>("Artisan.SetStopRequest").RegisterAction(SetStopRequest);

            Svc.PluginInterface.GetIpcProvider<ushort, int, object>("Artisan.CraftItem").RegisterAction(CraftX);
            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.IsBusy").RegisterFunc(IsBusy);

            Svc.PluginInterface.GetIpcProvider<uint, string, bool, object>("Artisan.ChangeSolver").RegisterAction(ChangeSolver);
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempSolverBackToNormal").RegisterAction(SetTempSolverBackToNormal);

            Svc.PluginInterface.GetIpcProvider<uint, uint, bool, bool, object>("Artisan.ChangeFood").RegisterAction(ChangeFood);
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempFoodBackToNormal").RegisterAction(SetTempFoodBackToNormal);

            Svc.PluginInterface.GetIpcProvider<uint, uint, bool, bool, object>("Artisan.ChangePotion").RegisterAction(ChangePotion);
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempPotionBackToNormal").RegisterAction(SetTempPotionBackToNormal);

            Svc.PluginInterface.GetIpcProvider<uint, uint, bool, object>("Artisan.ChangeManual").RegisterAction(ChangeManual);
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempManualBackToNormal").RegisterAction(SetTempManualBackToNormal);

            Svc.PluginInterface.GetIpcProvider<uint, uint, bool, object>("Artisan.ChangeSquadronManual").RegisterAction(ChangeSquadronManual);
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempSquadronManualBackToNormal").RegisterAction(SetTempSquadronManualBackToNormal);
        }

        internal static void Dispose()
        {
            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.GetEnduranceStatus").UnregisterFunc();
            Svc.PluginInterface.GetIpcProvider<bool, object>("Artisan.SetEnduranceStatus").UnregisterAction();

            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.IsListRunning").UnregisterFunc();
            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.IsListPaused").UnregisterFunc();
            Svc.PluginInterface.GetIpcProvider<bool, object>("Artisan.SetListPause").UnregisterAction();

            Svc.PluginInterface.GetIpcProvider<bool>("Artisan.GetStopRequest").UnregisterFunc();
            Svc.PluginInterface.GetIpcProvider<bool, object>("Artisan.SetStopRequest").UnregisterAction();

            Svc.PluginInterface.GetIpcProvider<ushort, int, object>("Artisan.CraftItem").UnregisterAction();
            Svc.PluginInterface.GetIpcProvider<ushort, int, object>("Artisan.IsBusy").UnregisterFunc();

            Svc.PluginInterface.GetIpcProvider<uint, string, bool, object>("Artisan.ChangeSolver").UnregisterAction();
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempSolverBackToNormal").UnregisterAction();

            Svc.PluginInterface.GetIpcProvider<uint, uint, bool, bool, object>("Artisan.ChangeFood").UnregisterAction();
            Svc.PluginInterface.GetIpcProvider<uint>("Artisan.SetTempFoodBackToNormal").UnregisterAction();

            Svc.PluginInterface.GetIpcProvider<uint, uint, bool, bool, object>("Artisan.ChangePotion").UnregisterAction();
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempPotionBackToNormal").UnregisterAction();

            Svc.PluginInterface.GetIpcProvider<uint, uint, bool, object>("Artisan.ChangeManual").UnregisterAction();
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempManualBackToNormal").UnregisterAction();

            Svc.PluginInterface.GetIpcProvider<uint, uint, bool, object>("Artisan.ChangeSquadronManual").UnregisterAction();
            Svc.PluginInterface.GetIpcProvider<uint, object>("Artisan.SetTempSquadronManualBackToNormal").UnregisterAction();
        }

        static bool GetEnduranceStatus()
        {
            return Endurance.Enable;
        }

        static void SetEnduranceStatus(bool s)
        {
            Endurance.ToggleEndurance(s);
        }

        static bool IsListRunning()
        {
            return CraftingListUI.Processing;
        }

        static bool IsListPaused()
        {
            return CraftingListUI.Processing && CraftingListFunctions.Paused;
        }

        static void SetListPause(bool s)
        {
            if (IsListPaused())
                CraftingListFunctions.Paused = s;
        }

        static bool GetStopRequest()
        {
            return StopCraftingRequest;
        }

        static void SetStopRequest(bool s)
        {
            if (s)
                DuoLog.Information("Artisan has been requested to stop by an external plugin.");
            else
                DuoLog.Information("Artisan has been requested to restart by an external plugin.");

            StopCraftingRequest = s;
        }

        public unsafe static void CraftX(ushort recipeId, int amount)
        {
            if (LuminaSheets.RecipeSheet!.TryGetFirst(x => x.Value.RowId == recipeId, out var recipe))
            {
                PreCrafting.Tasks.Add((() => PreCrafting.TaskSelectRecipe(recipe.Value), TimeSpan.FromMilliseconds(500)));
                P.TM.Enqueue(() => PreCrafting.Tasks.Count == 0);
                P.TM.DelayNext(100);
                P.TM.Enqueue(() =>
                {
                    Endurance.IPCOverride = true;
                    Endurance.RecipeID = recipeId;
                    P.Config.CraftX = amount;
                    P.Config.CraftingX = true;
                    Endurance.ToggleEndurance(true);
                });
            }
            else
            {
                throw new Exception("RecipeID not found.");
            }
        }

        public static bool IsBusy()
        {
            return Endurance.Enable || CraftingListUI.Processing || P.TM.NumQueuedTasks > 0 || P.CTM.NumQueuedTasks > 0 || !(Crafting.CurState is Crafting.State.IdleBetween or Crafting.State.IdleNormal);
        }


        /// <summary>
        /// Changes the solver for a given recipe.
        /// </summary>
        /// <param name="recipeId">The recipe ID</param>
        /// <param name="solverName">Name of the solver as displayed in the UI. Note, if changing to a macro, you must include the "Macro: " part too.</param>
        /// <param name="temporary">If you only want the change to work until the plugin is reloaded.</param>
        public static void ChangeSolver(uint recipeId, string solverName, bool temporary)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (LuminaSheets.RecipeSheet.TryGetValue(recipeId, out var recipe))
            {
                var job = (Job)((uint)Job.CRP + recipe.CraftType.RowId);
                var stats = CharacterStats.GetBaseStatsForClassHeuristic(job);
                var craft = Crafting.BuildCraftStateForRecipe(stats, job, recipe);
                var solvers = CraftingProcessor.GetAvailableSolversForRecipe(craft, false);

                foreach (var solver in solvers)
                {
                    if (solver.Name == solverName)
                    {
                        if (temporary)
                        {
                            config.TempSolverType = solver.Def.GetType().FullName!;
                            config.TempSolverFlavour = solver.Flavour;
                            P.Config.RecipeConfigs[recipeId] = config;
                        }
                        else
                        {
                            config.SolverType = solver.Def.GetType().FullName!;
                            config.SolverFlavour = solver.Flavour;
                            P.Config.RecipeConfigs[recipeId] = config;
                            P.Config.Save();
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Changes the food for a given recipe.
        /// </summary>
        /// <param name="recipeId">The RecipeId</param>
        /// <param name="FoodId">The Food ID of the item</param>
        /// <param name="HighQuality">High Quality Requirement</param>
        /// <param name="temporary">If you only want the change to work until the plugin is reloaded.</param>
        public static void ChangeFood(uint recipeId, uint FoodId, bool HighQuality, bool temporary)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (temporary)
            {
                config.TempRequiredFood = FoodId;
                config.TempFoodHQ = HighQuality;
                P.Config.RecipeConfigs[recipeId] = config;
            }
            else
            {
                config.requiredFood = FoodId;
                config.requiredFoodHQ = HighQuality;
                P.Config.RecipeConfigs[recipeId] = config;
                P.Config.Save();
            }

            var newConfig = P.Config.RecipeConfigs[recipeId];
            PluginLog.Debug($"Temp FoodId {newConfig.TempRequiredFood}\n" +
                            $"Temp HQ: {newConfig.TempFoodHQ}\n" +
                            $"FoodId: {newConfig.requiredFood}\n" +
                            $"Food HQ: {newConfig.requiredFoodHQ}\n" +
                            $"Actual Values:" +
                            $"Food Enabled: {newConfig.FoodEnabled}\n" +
                            $"Food ID: {newConfig.FoodName}\n" +
                            $"Food HQ: {newConfig.RequiredFoodHQ}");
        }

        /// <summary>
        /// Changes the Potion for a given recipe.
        /// </summary>
        /// <param name="recipeId">The RecipeId</param>
        /// <param name="PotionId">The Potion ID of the item</param>
        /// <param name="HighQuality">High Quality Requirement</param>
        /// <param name="temporary">If you only want the change to work until the plugin is reloaded.</param>
        public static void ChangePotion(uint recipeId, uint PotionId, bool HighQuality, bool temporary)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (temporary)
            {
                config.TempRequiredPotion = PotionId;
                config.TempPotionHQ = HighQuality;
                P.Config.RecipeConfigs[recipeId] = config;
            }
            else
            {
                config.requiredPotion = PotionId;
                config.requiredPotionHQ = HighQuality;
                P.Config.RecipeConfigs[recipeId] = config;
                P.Config.Save();
            }
        }

        /// <summary>
        /// Changes the Manual for a given recipe.
        /// </summary>
        /// <param name="recipeId">The RecipeId</param>
        /// <param name="ManualId">The Manual ID of the item</param>
        /// <param name="temporary">If you only want the change to work until the plugin is reloaded.</param>
        public static void ChangeManual(uint recipeId, uint ManualId, bool temporary)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (temporary)
            {
                config.TempRequiredManual = ManualId;
                P.Config.RecipeConfigs[recipeId] = config;
            }
            else
            {
                config.requiredManual = ManualId;
                P.Config.RecipeConfigs[recipeId] = config;
                P.Config.Save();
            }
        }

        /// <summary>
        /// Change the Squadron Manual for a given recipe
        /// </summary>
        /// <param name="recipeId"></param>
        /// <param name="SquadronManualId"></param>
        /// <param name="temporary"></param>
        public static void ChangeSquadronManual(uint recipeId, uint SquadronManualId, bool temporary)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (temporary)
            {
                config.TempRequiredSquadronManual = SquadronManualId;
                P.Config.RecipeConfigs[recipeId] = config;
            }
            else
            {
                config.TempRequiredSquadronManual = SquadronManualId;
                P.Config.RecipeConfigs[recipeId] = config;
                P.Config.Save();
            }
        }

        public static void SetTempSolverBackToNormal(uint recipeId)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (config.TempSolverFlavour != -1)
            {
                config.TempSolverFlavour = -1;
                config.TempSolverType = "";
            }
        }

        public static void SetTempFoodBackToNormal(uint recipeId)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (config.TempRequiredFood != 0)
            {
                config.TempRequiredFood = 0;
                config.TempFoodHQ = true;
            }
        }

        public static void SetTempPotionBackToNormal(uint recipeId)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (config.TempRequiredPotion != 0)
            {
                config.TempRequiredPotion = 0;
                config.TempPotionHQ = true;
            }
        }

        public static void SetTempManualBackToNormal(uint recipeId)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (config.TempRequiredManual != 0)
            {
                config.TempRequiredManual = 0;
            }
        }

        public static void SetTempSquadronManualBackToNormal(uint recipeId)
        {
            var config = P.Config.RecipeConfigs.GetValueOrDefault(recipeId) ?? new();
            if (config.TempRequiredSquadronManual != 0)
            {
                config.TempRequiredSquadronManual = 0;
            }
        }

        public enum ArtisanMode
        {
            None = 0,
            Endurance = 1,
            Lists = 2,
        }
    }
}
