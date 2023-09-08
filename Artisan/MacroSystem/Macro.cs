using System;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.MacroSystem
{
    public class Macro
    {
        public int ID { get; set; } = 0;

        public string? Name { get; set; }

        public List<uint> MacroActions { get; set; } = new();

        public MacroOptions MacroOptions { get; set; } = new();

        public List<MacroStepOptions> MacroStepOptions { get; set; } = new();
    }

    public class MacroOptions
    {
        public bool SkipQualityIfMet { get; set; } = false;

        public bool UpgradeQualityActions { get; set; } = false;

        public bool UpgradeProgressActions { get; set; } = false;

        public bool SkipObservesIfNotPoor { get; set; } = false;

        public int MinCraftsmanship = 0;
        public int MinControl = 0;
        public int MinCP = 0;
    }

    public class MacroStepOptions
    {
        public bool ExcludeFromUpgrade = false;
        public bool ExcludeNormal = false;
        public bool ExcludePoor = false;
        public bool ExcludeGood = false;
        public bool ExcludeExcellent = false;
        public bool ExcludeCentered = false;
        public bool ExcludeSturdy = false;
        public bool ExcludePliant = false;
        public bool ExcludeMalleable = false;
        public bool ExcludePrimed = false;
        public bool ExcludeGoodOmen = false;
    }
    public static class MacroFunctions
    {
        public static void SetID(this Macro macro)
        {
            var rng = new Random();
            var proposedRNG = rng.Next(1, 50000);
            while (Service.Configuration.UserMacros.Where(x => x.ID == proposedRNG).Any())
            {
                proposedRNG = rng.Next(1, 50000);
            }
            macro.ID = proposedRNG;
        }

        public static bool Save(this Macro macro, bool isNew = false)
        {
            if (macro.MacroActions.Count() == 0 && !isNew) return false;

            Service.Configuration.UserMacros.Add(macro);
            Service.Configuration.Save();
            return true;
        }

        public static bool GetMacro(uint recipeID, out Macro macro)
        {
            macro = null;
            if (P.Config.IRM.ContainsKey(recipeID))
            {
                if (P.Config.UserMacros.Any(x => x.ID == P.Config.IRM[recipeID]))
                {
                    macro = P.Config.UserMacros.First(x => x.ID == P.Config.IRM[recipeID]);
                    return true;
                }
            }

            return false;
        }
    }
}
