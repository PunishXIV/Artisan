using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon ContentsFinderConfirm.
/// </summary>
public sealed unsafe class ClickContentsFinderConfirm : ClickBase<ClickContentsFinderConfirm, AddonContentsFinderConfirm>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickContentsFinderConfirm"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickContentsFinderConfirm(IntPtr addon = default)
        : base("ContentsFinderConfirm", addon)
    {
    }

    public static implicit operator ClickContentsFinderConfirm(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickContentsFinderConfirm Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the commence button.
    /// </summary>
    [ClickName("duty_commence")]
    public void Commence()
        => this.ClickAddonButton(this.Addon->CommenceButton, 8);

    /// <summary>
    /// Click the commence button.
    /// </summary>
    [ClickName("duty_withdraw")]
    public void Withdraw()
        => this.ClickAddonButton(this.Addon->WithdrawButton, 9);

    /// <summary>
    /// Click the commence button.
    /// </summary>
    [ClickName("duty_wait")]
    public void Wait()
        => this.ClickAddonButton(this.Addon->WaitButton, 11);
}