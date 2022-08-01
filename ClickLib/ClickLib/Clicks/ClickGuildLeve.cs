using System;

using ClickLib.Attributes;
using ClickLib.Bases;

namespace ClickLib.Clicks;

/// <summary>
/// Addon GuildLeve.
/// </summary>
public sealed unsafe class ClickGuildLeve : ClickBase<ClickGuildLeve>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickGuildLeve"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickGuildLeve(IntPtr addon = default)
        : base("GuildLeve", addon)
    {
    }

    public static implicit operator ClickGuildLeve(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickGuildLeve Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Switch to a different job category.
    /// </summary>
    /// <param name="index">Job category index in the UI.</param>
    public void SwitchCategory(int index)
        => this.FireCallback(10, index, 0);

    /// <summary>
    /// Switch to a different job leve tab.
    /// </summary>
    /// <param name="index">Job index in the UI.</param>
    public void SwitchJob(int index)
        => this.FireCallback(12, index);

#pragma warning disable SA1134, SA1516, SA1600
    [ClickName("guild_leve_battlecraft")] public void Battlecraft() => this.SwitchCategory(0);
    [ClickName("guild_leve_fieldcraft")] public void Fieldcraft() => this.SwitchCategory(1);
    [ClickName("guild_leve_tradecraft")] public void Tradecraft() => this.SwitchCategory(2);

    [ClickName("guild_leve_carpenter")] public void Carpenter() => this.SwitchJob(0);
    [ClickName("guild_leve_blacksmith")] public void Blacksmith() => this.SwitchJob(1);
    [ClickName("guild_leve_armorer")] public void Armorer() => this.SwitchJob(2);
    [ClickName("guild_leve_goldsmith")] public void Goldsmith() => this.SwitchJob(3);
    [ClickName("guild_leve_leatherworker")] public void Leatherworker() => this.SwitchJob(4);
    [ClickName("guild_leve_weaver")] public void Weaver() => this.SwitchJob(5);
    [ClickName("guild_leve_alchemist")] public void Alchemist() => this.SwitchJob(6);
    [ClickName("guild_leve_culinarian")] public void Culinarian() => this.SwitchJob(7);

    [ClickName("guild_leve_miner")] public void Miner() => this.SwitchJob(0);
    [ClickName("guild_leve_botanist")] public void Botanist() => this.SwitchJob(1);
    [ClickName("guild_leve_fisher")] public void Fisher() => this.SwitchJob(2);
#pragma warning restore SA1134, SA1516, SA1600
}
