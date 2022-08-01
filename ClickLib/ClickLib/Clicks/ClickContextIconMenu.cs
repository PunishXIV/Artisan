using System;

using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon ContextIconMenu.
/// </summary>
public sealed unsafe class ClickContextIconMenu : ClickBase<ClickContextIconMenu, AddonContextIconMenu>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickContextIconMenu"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickContextIconMenu(IntPtr addon = default)
        : base("ContextIconMenu", addon)
    {
    }

    public static implicit operator ClickContextIconMenu(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickContextIconMenu Using(IntPtr addon) => new(addon);

    // /// <summary>
    // /// Select the item at the given index.
    // /// </summary>
    // /// <param name="index">Index to select.</param>
    // public void SelectItem(ushort index)
    //     => this.ClickList(index, this.Target->AtkComponentList240);
    //
    // /// <summary>
    // /// Click the item in index 1.
    // /// </summary>
    // [ClickName("select_context_icon1")]
    // public void SelectItem1()
    //     => this.SelectItem(1);
    //
    // /// <summary>
    // /// Click the item in index 2.
    // /// </summary>
    // [ClickName("select_context_icon2")]
    // public void SelectItem2()
    //     => this.SelectItem(2);
    //
    // /// <summary>
    // /// Click the item in index 3.
    // /// </summary>
    // [ClickName("select_context_icon3")]
    // public void SelectItem3()
    //     => this.SelectItem(3);
    //
    // /// <summary>
    // /// Click the item in index 4.
    // /// </summary>
    // [ClickName("select_context_icon4")]
    // public void SelectItem4()
    //     => this.SelectItem(4);
    //
    // /// <summary>
    // /// Click the item in index 5.
    // /// </summary>
    // [ClickName("select_context_icon5")]
    // public void SelectItem5()
    //     => this.SelectItem(5);
    //
    // /// <summary>
    // /// Click the item in index 6.
    // /// </summary>
    // [ClickName("select_context_icon6")]
    // public void SelectItem6()
    //     => this.SelectItem(6);
    //
    // /// <summary>
    // /// Click the item in index 7.
    // /// </summary>
    // [ClickName("select_context_icon7")]
    // public void SelectItem7()
    //     => this.SelectItem(7);
    //
    // /// <summary>
    // /// Click the item in index 8.
    // /// </summary>
    // [ClickName("select_context_icon8")]
    // public void SelectItem8()
    //     => this.SelectItem(8);
}
