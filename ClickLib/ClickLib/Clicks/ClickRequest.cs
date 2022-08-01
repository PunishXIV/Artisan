using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon Request.
/// </summary>
public sealed unsafe class ClickRequest : ClickBase<ClickRequest, AddonRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickRequest"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickRequest(IntPtr addon = default)
        : base("Request", addon)
    {
    }

    public static implicit operator ClickRequest(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickRequest Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the hand over button.
    /// </summary>
    [ClickName("request_hand_over")]
    public void HandOver()
        => this.ClickAddonButton(this.Addon->HandOverButton, 0);

    /// <summary>
    /// Click the cancel button.
    /// </summary>
    [ClickName("request_cancel")]
    public void Cancel()
        => this.ClickAddonButton(this.Addon->CancelButton, 1);
}
