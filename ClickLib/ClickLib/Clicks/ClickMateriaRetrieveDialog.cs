using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon MateriaRetrieveDialog.
/// </summary>
public sealed unsafe class ClickMateriaRetrieveDialog : ClickBase<ClickMateriaRetrieveDialog, AddonMateriaRetrieveDialog>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickMateriaRetrieveDialog"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickMateriaRetrieveDialog(IntPtr addon = default)
        : base("MateriaRetrieveDialog", addon)
    {
    }

    public static implicit operator ClickMateriaRetrieveDialog(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickMateriaRetrieveDialog Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the begin button.
    /// </summary>
    [ClickName("retrieve_materia_begin")]
    public void Begin()
        => this.ClickAddonButtonIndex(4, 0);

    /// <summary>
    /// Click the return button.
    /// </summary>
    [ClickName("retrieve_materia_return")]
    public void Return()
        => this.ClickAddonButtonIndex(3, 1);
}
