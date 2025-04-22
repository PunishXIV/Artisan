using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EventHandler = FFXIVClientStructs.FFXIV.Client.Game.Event.EventHandler;

namespace Artisan.GameInterop.CSExt;

[StructLayout(LayoutKind.Explicit, Size = 0x88)]
public unsafe struct RecipeNoteIngredientEntry
{
    [FieldOffset(0x04)] public ushort NumAvailableNQ;
    [FieldOffset(0x06)] public ushort NumAvailableHQ;
    [FieldOffset(0x08)] public byte NumAssignedNQ;
    [FieldOffset(0x09)] public byte NumAssignedHQ;
    [FieldOffset(0x78)] public uint ItemId;
    [FieldOffset(0x82)] public byte NumTotal;

    public void SetMaxHQ(bool updateUI = true)
    {
        var assigning = (byte)Math.Min(NumAvailableHQ, NumTotal);
        NumAssignedHQ = assigning;
        NumAssignedNQ = (byte)Math.Min(NumAssignedNQ, NumTotal - assigning);

        if (updateUI && GenericHelpers.TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon))
        {
            Callback.Fire(addon, true, 6);
        }
    }

    public void SetMaxNQ(bool updateUI = true)
    {
        var assigning = (byte)Math.Min(NumAvailableNQ, NumTotal);
        NumAssignedNQ = assigning;
        NumAssignedHQ = (byte)Math.Min(NumAvailableHQ, NumTotal - assigning);

        if (updateUI && GenericHelpers.TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon))
        {
            Callback.Fire(addon, true, 6);
        }
    }

    public void SetSpecific(int nq, int hq, bool updateUI = true)
    {
        if (NumAvailableNQ + NumAvailableHQ < NumTotal)
        {
            DuoLog.Error("Unable to set specified ingredients properly due to insufficient materials.");
            return;
        }

        NumAssignedNQ = 0;
        NumAssignedHQ = 0;

        NumAssignedNQ = (byte)Math.Min(NumTotal, Math.Min(NumAvailableNQ, nq));
        if (NumAssignedNQ != NumTotal)
            NumAssignedHQ = (byte)Math.Min(NumTotal, Math.Min(NumAvailableHQ, hq));

        if (updateUI && GenericHelpers.TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon))
        {
            Callback.Fire(addon, true, 6);
        }
    }

}

[StructLayout(LayoutKind.Explicit, Size = 0x400)]
public unsafe struct RecipeNoteRecipeEntry
{
    [FieldOffset(0x000)] public fixed byte Ingredients[6 * 0x88];
    public Span<RecipeNoteIngredientEntry> IngredientsSpan => new(Unsafe.AsPointer(ref Ingredients[0]), 6);

    [FieldOffset(0x3B2)] public ushort RecipeId;
    [FieldOffset(0x3D7)] public byte CraftType;

    public int[] GetAssignedHQIngredients()
    {
        var res = new int[IngredientsSpan.Length];
        for (int i = 0; i < IngredientsSpan.Length; ++i)
            res[i] = IngredientsSpan[i].NumAssignedHQ;
        return res;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 0x3B0)]
public unsafe struct RecipeNoteRecipeData
{
    public static RecipeNoteRecipeData* Ptr() => (RecipeNoteRecipeData*)RecipeNote.Instance()->RecipeList; // note: can be null

    [FieldOffset(0x000)] public RecipeNoteRecipeEntry* Recipes; // note: can be null
    [FieldOffset(0x008)] public int RecipesCount;
    [FieldOffset(0x438)] public ushort SelectedIndex;

    public RecipeNoteRecipeEntry* FindRecipeById(uint id)
    {
        if (Recipes == null)
            return null;
        for (int i = 0; i < RecipesCount; ++i)
        {
            var r = Recipes + i;
            if (r->RecipeId == id)
                return r;
        }
        return null;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 0x4C0)]
public unsafe struct CraftingEventHandler
{
    [FieldOffset(0x000)] public EventHandler EventHandler;
    [FieldOffset(0x000)] public void** VTable;

    [FieldOffset(0x44C)] public StepFlags SynthStepFlags;
    [FieldOffset(0x450)] public int SynthStartingQuality;
    [FieldOffset(0x454)] public ushort CurRecipeId; // both for usual and quick synth
    [FieldOffset(0x456)] public ushort SynthStep;
    [FieldOffset(0x458)] public int SynthCollectibilityBreakpoint1;
    [FieldOffset(0x45C)] public int SynthCollectibilityBreakpoint2;
    [FieldOffset(0x460)] public int SynthCollectibilityBreakpoint3;
    [FieldOffset(0x464)] public byte QuickSynthMax;
    [FieldOffset(0x465)] public byte QuickSynthCur;
    [FieldOffset(0x467)] public byte ConditionPlus1; // Condition enum + 1

    public static CraftingEventHandler* Instance() => (CraftingEventHandler*)EventFramework.Instance()->GetEventHandlerById(0x000A0001);

    public enum OperationId
    {
        StartPrepare = 1,
        StartInfo = 2,
        StartReady = 3,
        Finish = 4,
        Abort = 6,
        ReturnedReagents = 8,
        AdvanceCraftAction = 9,
        AdvanceNormalAction = 10,
        QuickSynthStart = 12,
        QuickSynthProgress = 13,
    }

    [Flags]
    public enum StepFlags : uint
    {
        u1 = 0x00000002, // always set?
        CompleteSuccess = 0x00000004, // set even if craft fails due to durability
        CompleteFail = 0x00000008,
        LastActionSucceeded = 0x00000010,
        ComboBasicTouch = 0x08000000,
        ComboStandardTouch = 0x10000000,
        ComboObserve = 0x20000000,
        NoCarefulsLeft = 0x40000000,
        NoHSLeft = 0x80000000,
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct StartInfo
    {
        [FieldOffset(0x0)] public OperationId OpId; // StartInfo
        [FieldOffset(0x4)] public ushort RecipeId;
        [FieldOffset(0x8)] public int StartingQuality;
        [FieldOffset(0xC)] public byte u8;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x70)]
    public struct ReturnedReagents
    {
        [FieldOffset(0x00)] public OperationId OpId; // ReturnedReagents
        [FieldOffset(0x04)] public int u4;
        [FieldOffset(0x08)] public int u8;
        [FieldOffset(0x0C)] public int uC;
        [FieldOffset(0x10)] public fixed uint ItemIds[8];
        [FieldOffset(0x30)] public fixed int NumNQ[8];
        [FieldOffset(0x50)] public fixed int NumHQ[8];
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x84)]
    public struct AdvanceStep
    {
        [FieldOffset(0x00)] public OperationId OpId; // Advance*Action
        [FieldOffset(0x04)] public int u4;
        [FieldOffset(0x08)] public int u8;
        [FieldOffset(0x0C)] public int uC;
        [FieldOffset(0x10)] public uint LastActionId;
        [FieldOffset(0x14)] public int GainCP;
        [FieldOffset(0x18)] public ushort StepIndex;
        [FieldOffset(0x1C)] public int CurProgress;
        [FieldOffset(0x20)] public int DeltaProgress;
        [FieldOffset(0x24)] public int CurQuality;
        [FieldOffset(0x28)] public int DeltaQuality;
        [FieldOffset(0x2C)] public int HQChance;
        [FieldOffset(0x30)] public int CurDurability;
        [FieldOffset(0x34)] public int DeltaDurability;
        [FieldOffset(0x38)] public int ConditionPlus1; // 1 = normal, ...
        [FieldOffset(0x3C)] public int u3C; // usually 1, sometimes 2? related to quality
        [FieldOffset(0x40)] public int ConditionParam; // used for good, related to splendorous?
        [FieldOffset(0x44)] public StepFlags Flags;
        [FieldOffset(0x48)] public int u48;
        [FieldOffset(0x4C)] public fixed int RemoveStatusIds[7];
        [FieldOffset(0x68)] public fixed int RemoveStatusParams[7];
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct QuickSynthStart
    {
        [FieldOffset(0x00)] public OperationId OpId; // QuickSynthStart
        [FieldOffset(0x04)] public ushort RecipeId;
        [FieldOffset(0x08)] public byte MaxCount;
    }
}
