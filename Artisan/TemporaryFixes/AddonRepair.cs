using ClickLib.Bases;
using ClickLib.Clicks;
using FFXIVClientStructs.Attributes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.TemporaryFixes
{
    [Addon("Repair")]
    [StructLayout(LayoutKind.Explicit, Size = 0xF7E8)]
    public unsafe struct AddonRepairFixed
    {
        [FieldOffset(0x0)] public AtkUnitBase AtkUnitBase;

        [FieldOffset(552)] public AtkTextNode* UnusedText1; // Top right corner
        [FieldOffset(560)] public AtkTextNode* JobLevel;
        [FieldOffset(568)] public AtkImageNode* JobIcon;
        [FieldOffset(576)] public AtkTextNode* JobName;
        [FieldOffset(584)] public AtkTextNode* UnusedText2; // Top right corner
        [FieldOffset(592)] public AtkComponentDropDownList* Dropdown;
        [FieldOffset(600 + 16)] public AtkComponentButton* RepairAllButton;
        [FieldOffset(608 + 16)] public AtkResNode* HeaderContainer;
        [FieldOffset(616 + 16)] public AtkTextNode* UnusedText3; // Bottom right corner
        [FieldOffset(624 + 16)] public AtkTextNode* NothingToRepairText; // Middle of screen;
        [FieldOffset(632 + 16)] public AtkComponentList* ItemList;
    }

    public sealed unsafe class ClickRepairFixed : ClickBase<ClickRepair, AddonRepairFixed>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClickRepair"/> class.
        /// </summary>
        /// <param name="addon">Addon pointer.</param>
        public ClickRepairFixed(IntPtr addon = default)
            : base("Repair", addon)
        {
        }

        public static implicit operator ClickRepairFixed(IntPtr addon) => new(addon);

        /// <summary>
        /// Instantiate this click using the given addon.
        /// </summary>
        /// <param name="addon">Addon to reference.</param>
        /// <returns>A click instance.</returns>
        public static ClickRequest Using(IntPtr addon) => new(addon);

        /// <summary>
        /// Click the repair all button.
        /// </summary>
        public void RepairAll()
            => this.ClickAddonButton(this.Addon->RepairAllButton, 0);
    }
}
