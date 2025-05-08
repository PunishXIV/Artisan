using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;

namespace Artisan.CraftingLogic
{
    internal static class Calculations
    {
        public static readonly List<int> HQChance = new List<int>()
        {
            1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8,
            9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 16, 16, 17, 17,
            17, 18, 18, 18, 19, 19, 20, 20, 21, 22, 23, 24, 26, 28, 31, 34, 38, 42, 47, 52, 58, 64, 68, 71,
            74, 76, 78, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 94, 96, 98, 100
        };

        public static int GetHQChance(double percent) => HQChance[Math.Clamp((int)percent, 0, 100)];

        public static int RecipeDifficulty(Recipe recipe) => recipe.RecipeLevelTable.Value.Difficulty * recipe.DifficultyFactor / 100;
        public static int RecipeMaxQuality(Recipe recipe) => (int)(recipe.RecipeLevelTable.Value.Quality * recipe.QualityFactor / 100);
        public static int RecipeDurability(Recipe recipe) => recipe.RecipeLevelTable.Value.Durability * recipe.DurabilityFactor / 100;

        public static int RecipeDifficulty(Recipe recipe, RecipeLevelTable leveltable) => leveltable.Difficulty * recipe.DifficultyFactor / 100;
        public static int RecipeMaxQuality(Recipe recipe, RecipeLevelTable leveltable) => (int)(leveltable.Quality * recipe.QualityFactor / 100);
        public static int RecipeDurability(Recipe recipe, RecipeLevelTable leveltable) => leveltable.Durability * recipe.DurabilityFactor / 100;

        public static bool ActionIsLengthyAnimation(this Skills id)
        {
            switch (id)
            {
                case Skills.BasicSynthesis:
                case Skills.BasicTouch:
                case Skills.MastersMend:
                case Skills.Observe:
                case Skills.TricksOfTrade:
                case Skills.StandardTouch:
                case Skills.ByregotsBlessing:
                case Skills.PreciseTouch:
                case Skills.MuscleMemory:
                case Skills.CarefulSynthesis:
                case Skills.PrudentTouch:
                case Skills.Reflect:
                case Skills.PreparatoryTouch:
                case Skills.Groundwork:
                case Skills.DelicateSynthesis:
                case Skills.IntensiveSynthesis:
                case Skills.AdvancedTouch:
                case Skills.HeartAndSoul:
                case Skills.PrudentSynthesis:
                case Skills.TrainedFinesse:
                case Skills.RefinedTouch:
                case Skills.ImmaculateMend:
                case Skills.TrainedPerfection:
                case Skills.TrainedEye:
                case Skills.QuickInnovation:
                case Skills.TouchCombo:
                case Skills.TouchComboRefined:
                case Skills.RapidSynthesis:
                case Skills.HastyTouch:
                    return true;
                default:
                    return false;
            };
        }

        public static int GetStartingQuality(Recipe recipe, int[] hqCount, RecipeLevelTable? lt = null)
        {
            var itemSheet = Svc.Data.GetExcelSheet<Item>();
            long sumLevelHQ = 0, sumLevel = 0, idx = 0;
            foreach (var i in recipe.Ingredients())
            {
                var numHQ = idx < hqCount.Length ? hqCount[idx] : 0;
                ++idx;

                if (i.Amount == 0 || i.Item.RowId <= 0)
                    continue;
                var item = itemSheet?.GetRow(i.Item.RowId);
                if (item == null || !item.Value.CanBeHq)
                    continue;
                var ilvl = (int)item.Value.LevelItem.RowId;
                sumLevel += ilvl * i.Amount;
                sumLevelHQ += ilvl * Math.Clamp(numHQ, 0, i.Amount);

            }
            var left = (sumLevelHQ * (lt is null ? RecipeMaxQuality(recipe) : RecipeMaxQuality(recipe, lt.Value)) * recipe.MaterialQualityFactor / 100);
            var right = (sumLevel);
            return (int)(sumLevel == 0 ? 0 : left / right);
        }
    }
}
