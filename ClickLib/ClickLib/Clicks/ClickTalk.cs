using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon Talk.
/// </summary>
public sealed class ClickTalk : ClickBase<ClickTalk, AddonTalk>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickTalk"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickTalk(IntPtr addon = default)
        : base("Talk", addon)
    {
    }

    public static implicit operator ClickTalk(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickTalk Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the talk dialog.
    /// </summary>
    [ClickName("talk")]
    public unsafe void Click()
        => this.ClickAddonStage(0);
}
