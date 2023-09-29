using System.Linq;

namespace Artisan.Cleanup
{
    public static class Cleanup
    {
        public static void CleanUpIndividualMacros()
        {
            foreach (var value in P.Config.IRM.ToList())
            {
                if (!P.Config.UserMacros.Any(x => x.ID == value.Value))
                    P.Config.IRM.Remove(value.Key);
            }
            P.Config.Save();
        }

        //TODO Remove after 2 months (Mid July?)
        public static void TransitionMacros()
        {
            foreach (var macro in P.Config.IndividualMacros)
            {
                if (macro.Value is null) continue;
                P.Config.IRM.TryAdd(macro.Key, macro.Value.ID);
            }
            P.Config.IndividualMacros.Clear();
            P.Config.Save();
        }
    }
}