using System;

using ClickLib.Attributes;
using ClickLib.Bases;

namespace ClickLib.Clicks;

/// <summary>
/// Addon GatheringMasterpiece.
/// </summary>
public sealed unsafe class ClickGatheringMasterpiece : ClickBase<ClickGatheringMasterpiece>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickGatheringMasterpiece"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickGatheringMasterpiece(IntPtr addon = default)
        : base("GatheringMasterpiece", addon)
    {
    }

    public static implicit operator ClickGatheringMasterpiece(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickGatheringMasterpiece Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the collect button.
    /// </summary>
    [ClickName("collectable_collect")]
    public void Collect()
        => this.FireCallback(0);

    /// <summary>
    /// Use an action within the Masterpiece UI.
    /// </summary>
    /// <param name="actionId">Action ID.</param>
    public void UseAction(int actionId)
        => this.FireCallback(100, actionId, 0);

#pragma warning disable SA1134, SA1516, SA1600
    [ClickName("collectable_btn_scour")] public void BotanistScour() => this.UseAction(22186);
    [ClickName("collectable_btn_brazen")] public void BotanistBrazen() => this.UseAction(22187);
    [ClickName("collectable_btn_meticulous")] public void BotanistMeticulous() => this.UseAction(22188);
    [ClickName("collectable_btn_scrutiny")] public void BotanistScrutiny() => this.UseAction(22189);
    [ClickName("collectable_btn_focus")] public void BotanistCollectorsFocus() => this.UseAction(21206);

    [ClickName("collectable_min_scour")] public void MinerScour() => this.UseAction(22182);
    [ClickName("collectable_min_brazen")] public void MinerBrazen() => this.UseAction(22183);
    [ClickName("collectable_min_meticulous")] public void MinerMeticulous() => this.UseAction(22184);
    [ClickName("collectable_min_scrutiny")] public void MinerScrutiny() => this.UseAction(22185);
    [ClickName("collectable_min_focus")] public void MinerCollectorsFocus() => this.UseAction(21205);
#pragma warning restore SA1134, SA1516, SA1600
}
