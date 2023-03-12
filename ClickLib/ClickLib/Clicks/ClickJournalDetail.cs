using System;

using ClickLib.Attributes;
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Clicks;

/// <summary>
/// Addon JournalDetail.
/// </summary>
public sealed unsafe class ClickJournalDetail : ClickBase<ClickJournalDetail, AddonJournalDetail>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickJournalDetail"/> class.
    /// </summary>
    /// <param name="addon">Addon pointer.</param>
    public ClickJournalDetail(IntPtr addon = default)
        : base("JournalDetail", addon)
    {
    }

    public static implicit operator ClickJournalDetail(IntPtr addon) => new(addon);

    /// <summary>
    /// Instantiate this click using the given addon.
    /// </summary>
    /// <param name="addon">Addon to reference.</param>
    /// <returns>A click instance.</returns>
    public static ClickJournalDetail Using(IntPtr addon) => new(addon);

    /// <summary>
    /// Click the accept button.
    /// </summary>
    [ClickName("journal_detail_accept")]
    public void Accept()
        => this.ClickAddonButton(this.Addon->AcceptMapButton, 1);

    /// <summary>
    /// Click the decline button.
    /// </summary>
    [ClickName("journal_detail_decline")]
    public void Decline()
        => this.ClickAddonButton(this.Addon->InitiateButton, 2);
}
