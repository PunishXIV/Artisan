using Dalamud.Game.Text.SeStringHandling;

namespace ECommons.ContextMenu
{
    /// <summary>
    /// A native context menu item
    /// </summary>
    public sealed class NativeContextMenuItem : BaseContextMenuItem
    {
        /// <summary>
        /// The action code to be used in the context menu agent for this item.
        /// </summary>
        public byte InternalAction { get; }

        /// <summary>
        /// The name of the context item.
        /// </summary>
        public SeString Name { get; set; }

        internal NativeContextMenuItem(byte action, SeString name, bool enabled, bool isSubMenu)
        {
            Name = name;
            InternalAction = action;
            Enabled = enabled;
            IsSubMenu = isSubMenu;
        }
    }
}
