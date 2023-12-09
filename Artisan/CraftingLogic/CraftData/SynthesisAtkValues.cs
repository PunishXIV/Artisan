using System.Runtime.InteropServices;

namespace Artisan.CraftingLogic.CraftData
{
    [StructLayout(LayoutKind.Explicit, Size = 0x200)]
    public struct SynthesisAtkValues
    {
        // Confirm these are all UInt (based on the AtkValue 1st byte)
        [FieldOffset(0x058)] public uint Progress;
        [FieldOffset(0x068)] public uint MaxProgress;
        [FieldOffset(0x078)] public uint Durability;
        [FieldOffset(0x088)] public uint MaxDurability;
        [FieldOffset(0x098)] public uint Quality;
        [FieldOffset(0x0A8)] public uint HqChance; // Does this make sense for Collectables?

        [FieldOffset(0x0C8)] public Condition Status;


        [FieldOffset(0x0F8)] public uint Step;

        [FieldOffset(0x118)] public uint MaxQuality;


        [FieldOffset(0x148)] public uint Collectability;

        [FieldOffset(0x168)] public uint CollectabilityLow;
        [FieldOffset(0x178)] public uint CollectabilityMedium;
        [FieldOffset(0x188)] public uint CollectabilityHigh;
        // Last one?
    }
}
