using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon GrandCompanySupplyReward.
/// </summary>
public sealed unsafe class ClickGrandCompanySupplyReward : ClickBase<ClickGrandCompanySupplyReward, AddonGrandCompanySupplyReward>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickGrandCompanySupplyReward"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickGrandCompanySupplyReward(IntPtr addon = default)
        : base("GrandCompanySupplyReward", addon)
    {
    }

    public static implicit operator ClickGrandCompanySupplyReward(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickGrandCompanySupplyReward Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the deliver button.
    /// </summary>
    [ClickName("grand_company_expert_delivery_deliver")]
    public void Deliver()
        => this.ClickAddonButton(this.Addon->DeliverButton, 0);

    /// <summary>
    /// Click the cancel button.
    /// </summary>
    [ClickName("grand_company_expert_delivery_cancel")]
    public void Cancel()
        => this.ClickAddonButton(this.Addon->CancelButton, 1);
}
