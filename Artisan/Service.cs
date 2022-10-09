using Artisan.RawInformation;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Gui.Toast;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace Artisan
{
    /// <summary>
    /// Dalamud and plugin services.
    /// </summary>
    internal class Service
    {
        internal static Artisan Plugin = null!;

        /// <summary>
        /// Gets or sets the plugin configuration.
        /// </summary>
        internal static Configuration Configuration { get; set; } = null!;

        /// <summary>
        /// Gets the Dalamud plugin interface.
        /// </summary>
        [PluginService]
        internal static DalamudPluginInterface Interface { get; private set; } = null!;


        /// <summary>
        /// Gets the Dalamud chat gui.
        /// </summary>
        [PluginService]
        internal static ChatGui ChatGui { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud client state.
        /// </summary>
        [PluginService]
        internal static ClientState ClientState { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud command manager.
        /// </summary>
        [PluginService]
        internal static CommandManager CommandManager { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud condition.
        /// </summary>
        [PluginService]
        internal static Condition Condition { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud data manager.
        /// </summary>
        [PluginService]
        internal static DataManager DataManager { get; private set; } = null!;

        /// <summary>
        /// Gets the Dalamud framework manager.
        /// </summary>
        [PluginService]
        internal static Framework Framework { get; private set; } = null!;

        [PluginService]
        internal static GameGui GameGui { get; private set; } = null!;

        [PluginService]
        internal static SigScanner SigScanner { get; private set; } = null!;

        [PluginService]
        internal static ToastGui ToastGui { get; private set; } = null!;

    }
}
