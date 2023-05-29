using Artisan.CraftingLists;
using Artisan.MacroSystem;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.Cleanup
{
    public static class Cleanup
    {
        public static void CleanUpIndividualMacros()
        {
            foreach (var value in Service.Configuration.IRM.ToList())
            {
                if (!Service.Configuration.UserMacros.Any(x => x.ID == value.Value))
                    Service.Configuration.IRM.Remove(value.Key);
            }
            Service.Configuration.Save();
        }

        //TODO Remove after 2 months (Mid July?)
        public static void TransitionMacros()
        {
            foreach (var macro in Service.Configuration.IndividualMacros)
            {
                if (macro.Value is null) continue;
                Service.Configuration.IRM.TryAdd(macro.Key, macro.Value.ID);
            }
            Service.Configuration.IndividualMacros.Clear();
            Service.Configuration.Save();
        }
    }
}