using Dalamud.Configuration;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using ECommons.Reflection;
using ImGuiNET;
using Lumina.Misc;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.ImGuiMethods
{
    public class ChangelogWindow : Window
    {
        Action func;
        Action onClose;
        IPluginConfiguration config;
        WindowSystem ws = new();
        int version;

        public ChangelogWindow(IPluginConfiguration Configuration, int version, Action func, Action onClose = null) : base($"{DalamudReflector.GetPluginName()} was updated", 
            ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse)
        {
            try
            {
                this.config = Configuration;
                this.version = version;
                this.func = func; 
                this.onClose = onClose;
                if (Svc.PluginInterface.Reason == PluginLoadReason.Installer)
                {
                    this.OnClose();
                }
                else
                {
                    if ((int)Configuration.GetType().GetField("ChangelogWindowVer").GetValue(Configuration) != version)
                    {
                        this.IsOpen = true;
                        ws.AddWindow(this);
                        Svc.PluginInterface.UiBuilder.Draw += ws.Draw;
                    }
                }
            }
            catch(Exception ex)
            {
                ex.Log();
            }
        }

        public override bool DrawConditions()
        {
            return Svc.ClientState.IsLoggedIn;
        }

        public override void Draw()
        {
            func();
        }

        public override void OnClose()
        {
            GenericHelpers.Safe(delegate {
                config.GetType().GetField("ChangelogWindowVer").SetValue(config, this.version);
                base.OnClose();
                if(onClose != null)
                {
                    onClose();
                }
                else
                {
                    Svc.PluginInterface.SavePluginConfig(config);
                }
            });
            Svc.PluginInterface.UiBuilder.Draw -= ws.Draw;
        }
    }
}
