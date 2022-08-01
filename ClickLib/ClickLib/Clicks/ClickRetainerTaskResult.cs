using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon RetainerTaskResult.
/// </summary>
public sealed unsafe class ClickRetainerTaskResult : ClickBase<ClickRetainerTaskResult, AddonRetainerTaskResult>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickRetainerTaskResult"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickRetainerTaskResult(IntPtr addon = default)
        : base("RetainerTaskResult", addon)
    {
    }

    public static implicit operator ClickRetainerTaskResult(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickRetainerTaskResult Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the confirm button.
    /// </summary>
    [ClickName("retainer_venture_result_confirm")]
    public void Confirm()
        => this.ClickAddonButton(this.Addon->ConfirmButton, 2);

    /// <summary>
    /// Click the reassign button.
    /// </summary>
    [ClickName("retainer_venture_result_reassign")]
    public void Reassign()
        => this.ClickAddonButton(this.Addon->ReassignButton, 3);
}
