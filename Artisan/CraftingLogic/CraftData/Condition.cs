using System;

namespace Artisan.CraftingLogic.CraftData
{
    [Flags]
    public enum Condition
    {
        Normal = 1,
        Good = 2,
        Excellent = 4,
        Poor = 8,
        Centered = 16,
        Pliant = 32,
        Sturdy = 64,
        Malleable = 128,
        Primed = 256,
        GoodOmen,

        Unknown
    }
}
