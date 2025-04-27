using System;

namespace Artisan.CraftingLogic.CraftData
{

    public enum Condition
    {
        Normal = 0,
        Good = 1,
        Excellent = 2,
        Poor = 3,
        Centered = 4,
        Sturdy = 5,
        Pliant = 6,
        Malleable = 7,
        Primed = 8,
        GoodOmen = 9,

        Unknown
    }

    [Flags]
    public enum ConditionFlags
    {
        Normal = 1,
        Good = 2,
        Excellent = 4,
        Poor = 8,
        Centered = 16,
        Sturdy = 32,
        Pliant = 64,
        Malleable = 128,
        Primed = 256,
        GoodOmen = 512,

    }
}
