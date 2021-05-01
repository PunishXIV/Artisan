using Dalamud.Game.Command;
using Dalamud.Plugin;
using System;
using System.IO;
using System.Reflection;

namespace SamplePlugin
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Sample Plugin";

        private const string commandName = "/pmycommand";

        private DalamudPluginInterface pi;
        private Configuration configuration;
        private PluginUI ui;
        
        // When loaded by LivePluginLoader, the executing assembly will be wrong.
        // Supplying this property allows LivePluginLoader to supply the correct location, so that
        // you have full compatibility when loaded normally and through LPL.
        public string AssemblyLocation { get => assemblyLocation; set => assemblyLocation = value; }
        private string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pi = pluginInterface;
            
            this.configuration = this.pi.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pi);

            // you might normally want to embed resources and load them from the manifest stream
            var imagePath = Path.Combine(Path.GetDirectoryName(AssemblyLocation), @"goat.png");
            var goatImage = this.pi.UiBuilder.LoadImage(imagePath);
            this.ui = new PluginUI(this.configuration, goatImage);

            this.pi.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A useful message to display in /xlhelp"
            });

            this.pi.UiBuilder.OnBuildUi += DrawUI;
            this.pi.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUI();
        }

        public void Dispose()
        {
            this.ui.Dispose();

            this.pi.CommandManager.RemoveHandler(commandName);
            this.pi.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            this.ui.Visible = true;
        }

        private void DrawUI()
        {
            this.ui.Draw();
        }

        private void DrawConfigUI()
        {
            this.ui.SettingsVisible = true;
        }
    }
}
