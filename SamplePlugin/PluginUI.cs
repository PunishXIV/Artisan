using CraftIt.RawInformation;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using static CraftIt.CraftingLogic.CurrentCraft;

namespace CraftIt
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private Configuration configuration;

        private ImGuiScene.TextureWrap goatImage;
        public event EventHandler<bool>? CraftingWindowStateChanged;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public bool Visible
        {
            get { return this.visible; }
            set { this.visible = value; }
        }

        private bool settingsVisible = false;
        public bool SettingsVisible
        {
            get { return this.settingsVisible; }
            set { this.settingsVisible = value; }
        }

        private bool craftingVisible = false;
        public bool CraftingVisible
        {
            get { return this.craftingVisible; }
            set { if (this.craftingVisible != value) CraftingWindowStateChanged?.Invoke(this, value); this.craftingVisible = value; }
        }

        // passing in the image here just for simplicity
        public PluginUI(Configuration configuration, ImGuiScene.TextureWrap goatImage)
        {
            this.configuration = configuration;
            this.goatImage = goatImage;
        }

        public void Dispose()
        {
            this.goatImage.Dispose();
        }

        public void Draw()
        {
            DrawMainWindow();
            DrawCraftingWindow();
        }

        private void DrawCraftingWindow()
        {
            if (!CraftingVisible)
            {
                return;
            }

            CraftingVisible = craftingVisible;

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 330), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Craft-It Crafting Window", ref this.craftingVisible, ImGuiWindowFlags.AlwaysAutoResize))
            {
                GetCraft();
                
                Hotbars.MakeButtonsGlow(CurrentRecommendation);

                if (ImGui.Button("Execute recommended action"))
                {
                    Hotbars.ExecuteRecommended(CurrentRecommendation);
                }

                bool enableAutoRepeat = Service.Configuration.AutoCraft;

                if (ImGui.Checkbox("Repeat last", ref enableAutoRepeat))
                {
                    Service.Configuration.AutoCraft = enableAutoRepeat;
                    Service.Configuration.Save();
                }

                bool autoMode = Service.Configuration.AutoMode;

                if (ImGui.Checkbox("Auto Mode", ref autoMode))
                {
                    Service.Configuration.AutoMode = autoMode;
                    Service.Configuration.Save();
                }
                if (enableAutoRepeat)
                    RepeatActualCraft();
            }
            ImGui.End();
        }

        public void DrawMainWindow()
        {
            if (!Visible)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 330), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("My Amazing Window", ref this.visible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Text(GetCraft().ToString());

                ImGui.Text($"Current Craftsmanship: {CharacterInfo.Craftsmanship()}");
                ImGui.Text($"Current Control: {CharacterInfo.Control()}");
            }
            ImGui.End();
        }

        
    }
}
