using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon RecipeNote.
/// </summary>
public sealed unsafe class ClickRecipeNote : ClickBase<ClickRecipeNote, AddonRecipeNote>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickRecipeNote"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickRecipeNote(IntPtr addon = default)
        : base("RecipeNote", addon)
    {
    }

    public static implicit operator ClickRecipeNote(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickRecipeNote Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the synthesize button.
    /// </summary>
    [ClickName("synthesize")]
    public void Synthesize()
        => this.ClickAddonButton(this.Addon->SynthesizeButton, 13);

    /// <summary>
    /// Click the quick synthesis button.
    /// </summary>
    [ClickName("quick_synthesis")]
    public void QuickSynthesis()
        => this.ClickAddonButton(this.Addon->QuickSynthesisButton, 14);

    /// <summary>
    /// Click the trial synthesis button.
    /// </summary>
    [ClickName("trial_synthesis")]
    public void TrialSynthesis()
        => this.ClickAddonButton(this.Addon->TrialSynthesisButton, 15);

    /// <summary>
    /// Click a material.
    /// </summary>
    /// <param name="index">Material row, 0 indexed.</param>
    /// <param name="hq">The NQ or HQ button.</param>
    public void Material(int index, bool hq)
    {
        if (hq) index += 0x10_000;

        this.FireCallback(6, index, 0, 0);
    }

#pragma warning disable SA1134, SA1516, SA1600
    [ClickName("synthesis_material1_nq")] public void Material1Nq() => this.Material(0, false);
    [ClickName("synthesis_material2_nq")] public void Material2Nq() => this.Material(1, false);
    [ClickName("synthesis_material3_nq")] public void Material3Nq() => this.Material(2, false);
    [ClickName("synthesis_material4_nq")] public void Material4Nq() => this.Material(3, false);
    [ClickName("synthesis_material5_nq")] public void Material5Nq() => this.Material(4, false);
    [ClickName("synthesis_material6_nq")] public void Material6Nq() => this.Material(5, false);
    [ClickName("synthesis_material1_hq")] public void Material1Hq() => this.Material(0, true);
    [ClickName("synthesis_material2_hq")] public void Material2Hq() => this.Material(1, true);
    [ClickName("synthesis_material3_hq")] public void Material3Hq() => this.Material(2, true);
    [ClickName("synthesis_material4_hq")] public void Material4Hq() => this.Material(3, true);
    [ClickName("synthesis_material5_hq")] public void Material5Hq() => this.Material(4, true);
    [ClickName("synthesis_material6_hq")] public void Material6Hq() => this.Material(5, true);
#pragma warning restore SA1134, SA1516, SA1600
}
