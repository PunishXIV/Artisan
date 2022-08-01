using System;

using ClickLib.Attributes;
using ClickLib.Bases;

namespace ClickLib.Clicks;

/// <summary>
/// Addon RetainerTaskResult.
/// </summary>
public sealed unsafe class ClickRetainerList : ClickBase<ClickRetainerList>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickRetainerList"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickRetainerList(IntPtr addon = default)
        : base("RetainerList", addon)
    {
    }

    public static implicit operator ClickRetainerList(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickRetainerList Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click a retainer.
    /// </summary>
    /// <param name="index">Retainer index.</param>
    public void Retainer(int index)
        => this.FireCallback(2, index, 0, 0);

#pragma warning disable SA1134,SA1516,SA1600
    [ClickName("select_retainer1")] public void Retainer1() => this.Retainer(0);
    [ClickName("select_retainer2")] public void Retainer2() => this.Retainer(1);
    [ClickName("select_retainer3")] public void Retainer3() => this.Retainer(2);
    [ClickName("select_retainer4")] public void Retainer4() => this.Retainer(3);
    [ClickName("select_retainer5")] public void Retainer5() => this.Retainer(4);
    [ClickName("select_retainer6")] public void Retainer6() => this.Retainer(5);
    [ClickName("select_retainer7")] public void Retainer7() => this.Retainer(6);
    [ClickName("select_retainer8")] public void Retainer8() => this.Retainer(7);
    [ClickName("select_retainer9")] public void Retainer9() => this.Retainer(8);
    [ClickName("select_retainer10")] public void Retainer10() => this.Retainer(9);
#pragma warning restore SA1134,SA1516,SA1600
}
