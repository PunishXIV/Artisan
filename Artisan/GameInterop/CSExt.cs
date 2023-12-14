using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
}

[StructLayout(LayoutKind.Explicit, Size = 0x500)]
public unsafe struct RecipeNoteRecipeEntry
{
    [FieldOffset(0x000)] public fixed byte Ingredients[8 * 0x88];
    public Span<RecipeNoteIngredientEntry> IngredientsSpan => new(Unsafe.AsPointer(ref Ingredients[0]), 8);

    [FieldOffset(0x4C2)] public ushort RecipeId;
    [FieldOffset(0x4E7)] public byte CraftType;

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
    [FieldOffset(0x3B8)] public ushort SelectedIndex;

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
