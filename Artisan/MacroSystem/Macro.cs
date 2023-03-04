using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.MacroSystem
{
    public class Macro
    {
        public int ID { get; set; } = 0;

        public string? Name { get; set; }

        public List<uint> MacroActions { get; set; } = new();

        public MacroOptions MacroOptions { get; set; } = new();

        public List<MacroStepOptions> MacroStepOptions { get; set; }= new();
    }

    public class MacroOptions
    {
        public bool SkipQualityIfMet { get; set; } = false;

        public bool UpgradeQualityActions { get; set; } = false;

        public bool UpgradeProgressActions { get; set; } = false;
    }

    public class MacroStepOptions
    {
        public bool ExcludeFromUpgrade { get; set; } = false;
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
    }
}
