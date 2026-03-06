using ECommons;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Text;
using TerraFX.Interop.Windows;
using static Artisan.CraftingLogic.Solvers.ExpertSolverProfiles;

namespace Artisan.CraftingLogic.Solvers;

public class ExpertSolverProfiles
{
    [Serializable]
    public class ExpertProfile
    {
        public int ID { get; set; }
        public string Name { get; set; } = "";
        public ExpertSolverSettings Settings { get; set; } = new();

        [NonSerialized]
        public int? PerRecipeMaxSteadyUses = null;
        [NonSerialized]
        public bool? PerRecipeUseMaterialMiracle = null;
        [NonSerialized]
        public int? PerRecipeMinimumStepsBeforeMiracle = null;

        public void SetPerRecipeSettings(RecipeConfig recipeConfig)
        {
            this.PerRecipeMaxSteadyUses = (int)recipeConfig.ExpertMaxSteadyUses;
            this.PerRecipeUseMaterialMiracle = recipeConfig.ExpertUseMaterialMiracle;
            this.PerRecipeMinimumStepsBeforeMiracle = (int)recipeConfig.ExpertMinimumStepsBeforeMiracle;
        }

        public int GetMaxSteadyUses() => this.Settings.OverrideCosmicRecipeSettings ? this.Settings.MaxSteadyUses : (this.PerRecipeMaxSteadyUses ?? this.Settings.MaxSteadyUses);

        public bool GetUseMaterialMiracle() => this.Settings.OverrideCosmicRecipeSettings ? this.Settings.UseMaterialMiracle : (this.PerRecipeUseMaterialMiracle ?? this.Settings.UseMaterialMiracle);

        public int GetMinimumStepsBeforeMiracle() => this.Settings.OverrideCosmicRecipeSettings ? this.Settings.MinimumStepsBeforeMiracle : (this.PerRecipeMinimumStepsBeforeMiracle ?? this.Settings.MinimumStepsBeforeMiracle);
    }

    public List<ExpertProfile> ExpertProfiles = new();

    public List<ExpertProfile> GetExpertProfilesWithDefault()
    {
        List<ExpertProfile> allProfilesWithDefault = new();
        allProfilesWithDefault.Add(GetDefaultProfile());
        allProfilesWithDefault.AddRange(ExpertProfiles);

        return allProfilesWithDefault; 
    }

    public ExpertProfile GetDefaultProfile()
    {
        var defaultConfig = new ExpertProfile();
        defaultConfig.ID = 0;
        defaultConfig.Name = "Use Global Settings";
        defaultConfig.Settings = P.Config.ExpertSolverConfig;
        return defaultConfig;
    }

    public void AddNewExpertProfile(ExpertProfile profile)
    {
        var rng = new Random();
        profile.ID = rng.Next(1, 50000);
        while (FindExpertProfile(profile.ID) != null)
            profile.ID = rng.Next(1, 50000);
        ExpertProfiles.Add(profile);
    }

    public ExpertProfile? FindExpertProfile(int id) => ExpertProfiles.Find(c => c.ID == id);
}
