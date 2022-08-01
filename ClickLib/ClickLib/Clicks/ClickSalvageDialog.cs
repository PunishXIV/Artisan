using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Adddon SalvageDialog.
/// </summary>
public sealed unsafe class ClickSalvageDialog : ClickBase<ClickSalvageDialog, AddonSalvageDialog>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickSalvageDialog"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickSalvageDialog(IntPtr addon = default)
        : base("SalvageDialog", addon)
    {
    }

    public static implicit operator ClickSalvageDialog(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickSalvageDialog Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the desynthesize button.
    /// </summary>
    [ClickName("desynthesize")]
    public void Desynthesize()
        => this.ClickAddonButton(this.Addon->DesynthesizeButton, 1);

    /// <summary>
    /// Click the desynthesize checkbox button.
    /// </summary>
    [ClickName("desynthesize_checkbox")]
    public void CheckBox()
        => this.ClickAddonCheckBox(this.Addon->CheckBox, 3);
}
