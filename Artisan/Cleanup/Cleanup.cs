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
    }
}