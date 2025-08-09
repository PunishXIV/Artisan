using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.GameInterop;
using Artisan.IPC;
using Artisan.RawInformation;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.UI
{
    internal class CraftMenuWindowUI : Window
    {
        public bool EnableMacroOptions { get; set; }
        
        public bool EnableCosmicOptions { get; set; }
        
        public CraftMenuWindowUI(string windowName, ImGuiWindowFlags flags) : base(windowName, flags, true)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
            DisableWindowSounds = true;
            
            TitleBarButtons.Add(new()
            {
                Icon = FontAwesomeIcon.Cog,
                ShowTooltip = () => ImGui.SetTooltip("Open Config"),
                Click = (x) => P.PluginUi.IsOpen = true,
            });
        }

        public override bool DrawConditions()
        {
            return IsOpen;
        }

        public override void PreDraw()
        {
            if (P.Config.DisableTheme)
            {
                return;
            }
            
            P.Style.Push();
            P.StylePushed = true;
        }

        public override void PostDraw()
        {
            if (!P.StylePushed)
            {
                return;
            }
            
            P.Style.Pop();
            P.StylePushed = false;
        }

        public override void Draw()
        {
            if (!IsOpen)
            {
                return;
            }
            
            var autoMode = P.Config.AutoMode;

            if (ImGui.Checkbox("Automatic Action Execution Mode", ref autoMode))
            {
                P.Config.AutoMode = autoMode;
                P.Config.Save();
            }
            
            var enable = Endurance.Enable;

            if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID) && !Endurance.Enable)
            {
                ImGui.BeginDisabled();
            }

            if (ImGui.Checkbox("Endurance Mode Toggle", ref enable))
            {
                Endurance.ToggleEndurance(enable);
            }

            if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID) && !Endurance.Enable)
            {
                ImGui.EndDisabled();

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    var recipe = LuminaSheets.RecipeSheet!.First(x => x.Key == Endurance.RecipeID).Value;
                    ImGui.BeginTooltip();
                    ImGui.Text($"You cannot start Endurance as you do not possess ingredients to craft this recipe.\r\nMissing: {string.Join(", ", PreCrafting.MissingIngredients(recipe))}");
                    ImGui.EndTooltip();
                }
            }

            if (EnableMacroOptions)
            {
                ImGui.Spacing();

                if (SimpleTweaks.IsFocusTweakEnabled())
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudRed, $@"Warning: You have the ""Auto Focus Recipe Search"" SimpleTweak enabled. This is highly incompatible with Artisan and is recommended to disable it.");
                }

                if (Endurance.RecipeID == 0)
                {
                    return;
                }
                
                var config = P.Config.RecipeConfigs.GetValueOrDefault(Endurance.RecipeID) ?? new();
                
                if (!config.Draw(Endurance.RecipeID))
                {
                    return;
                }
                
                P.Config.RecipeConfigs[Endurance.RecipeID] = config;
                P.Config.Save();
            }
        }
    }
}
