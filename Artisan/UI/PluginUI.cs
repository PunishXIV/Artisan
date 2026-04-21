using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.FCWorkshops;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.WindowsFormsReflector;
using Lumina.Excel.Sheets;
using PunishLib.ImGuiMethods;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using TerraFX.Interop.Windows;
using ThreadLoadImageHandler = ECommons.ImGuiMethods.ThreadLoadImageHandler;

namespace Artisan.UI
{
    unsafe internal class PluginUI : Window
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;
        public ExpertSolverSettingsUI ExpertSettingsUI = new();


        private bool visible = false;
        public OpenWindow OpenWindow { get; set; }

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

        public PluginUI() : base($"{P.Name} {P.GetType().Assembly.GetName().Version}###Artisan")
        {
            this.RespectCloseHotkey = false;
            this.SizeConstraints = new()
            {
                MinimumSize = new(250, 100),
                MaximumSize = new(9999, 9999)
            };
            this.TitleBarButtons.Add(new()
            {
                Icon = FontAwesomeIcon.Cog,
                ShowTooltip = () => ImGuiEx.SetTooltip("Open Config"),
                Click = (x) => P.PluginUi.IsOpen = true,
            });
            P.ws.AddWindow(this);
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
            {
                P.Style.Push();
                P.StylePushed = true;
            }

        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                P.StylePushed = false;
            }
        }

        public void Dispose()
        {

        }

        public override void Draw()
        {
            try
            {
                if (DalamudInfo.IsOnStaging())
                {
                    var scale = ImGui.GetIO().FontGlobalScale;
                    ImGui.GetIO().FontGlobalScale = scale * 1.5f;
                    using (var f = ImRaii.PushFont(ImGui.GetFont()))
                    {
                        ImGuiEx.TextWrapped($"Listen buddy, you're on Dalamud staging, there's every chance any problems you might encounter is specific to Dalamud's testing and not Artisan. I don't make this plugin to work on staging, so don't expect any fixes unless the problem makes it to Dalamud release.");
                        ImGui.Separator();

                        ImGui.Spacing();
                        ImGui.GetIO().FontGlobalScale = scale;
                    }

                }
                var region = ImGui.GetContentRegionAvail();
                var itemSpacing = ImGui.GetStyle().ItemSpacing;

                var topLeftSideHeight = region.Y;

                ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f.Scale(), 0));
                try
                {
                    ShowEnduranceMessage();

                    using (var table = ImRaii.Table($"ArtisanTableContainer", 2, ImGuiTableFlags.Resizable))
                    {
                        if (!table)
                            return;

                        ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetWindowWidth() / 2);

                        ImGui.TableNextColumn();

                        var regionSize = ImGui.GetContentRegionAvail();

                        ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
                        using (var leftChild = ImRaii.Child($"###ArtisanLeftSide", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                        {
                            var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/artisan-icon.png");

                            if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
                            {
                                ImGuiEx.LineCentered("###ArtisanLogo", () =>
                                {
                                    ImGui.Image(logo.Handle, new(125f.Scale(), 125f.Scale()));
                                    if (ImGui.IsItemHovered())
                                    {
                                        ImGui.BeginTooltip();
                                        ImGui.Text($"You are the 69th person to find this secret. Nice!");
                                        ImGui.EndTooltip();
                                    }
                                });

                            }
                            ImGui.Spacing();
                            ImGui.Separator();

                            if (ImGui.Selectable("Overview", OpenWindow == OpenWindow.Overview))
                            {
                                OpenWindow = OpenWindow.Overview;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("Settings", OpenWindow == OpenWindow.Main))
                            {
                                OpenWindow = OpenWindow.Main;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("Endurance", OpenWindow == OpenWindow.Endurance))
                            {
                                OpenWindow = OpenWindow.Endurance;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("Macros", OpenWindow == OpenWindow.Macro))
                            {
                                OpenWindow = OpenWindow.Macro;
                            }
                            if (P.Config.ExpertSolverConfig.EnableExpertProfiles)
                            {
                                ImGui.Spacing();
                                if (ImGui.Selectable("Expert Profiles", OpenWindow == OpenWindow.ExpertProfiles))
                                {
                                    OpenWindow = OpenWindow.ExpertProfiles;
                                }
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("Raphael Cache", OpenWindow == OpenWindow.RaphaelCache))
                            {
                                OpenWindow = OpenWindow.RaphaelCache;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("Recipe Assigner", OpenWindow == OpenWindow.Assigner))
                            {
                                OpenWindow = OpenWindow.Assigner;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("Crafting Lists", OpenWindow == OpenWindow.Lists))
                            {
                                OpenWindow = OpenWindow.Lists;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("List Builder", OpenWindow == OpenWindow.SpecialList))
                            {
                                OpenWindow = OpenWindow.SpecialList;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("FC Workshops", OpenWindow == OpenWindow.FCWorkshop))
                            {
                                OpenWindow = OpenWindow.FCWorkshop;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("Simulator", OpenWindow == OpenWindow.Simulator))
                            {
                                OpenWindow = OpenWindow.Simulator;
                            }
                            ImGui.Spacing();
                            if (ImGui.Selectable("About", OpenWindow == OpenWindow.About))
                            {
                                OpenWindow = OpenWindow.About;
                            }


#if DEBUG
                            drawDebugTab();
#else
                        if(GenericHelpers.IsKeyPressed(Keys.LControlKey) && GenericHelpers.IsKeyPressed(Keys.LShiftKey)) drawDebugTab();
#endif
                            void drawDebugTab()
                            {
                                ImGui.Spacing();
                                if (ImGui.Selectable("DEBUG", OpenWindow == OpenWindow.Debug))
                                {
                                    OpenWindow = OpenWindow.Debug;
                                }
                                ImGui.Spacing();
                            }


                        }

                        ImGui.PopStyleVar();
                        ImGui.TableNextColumn();
                        using (var rightChild = ImRaii.Child($"###ArtisanRightSide", Vector2.Zero, false))
                        {
                            switch (OpenWindow)
                            {
                                case OpenWindow.Main:
                                    DrawMainWindow();
                                    break;
                                case OpenWindow.Endurance:
                                    Endurance.Draw();
                                    break;
                                case OpenWindow.Lists:
                                    CraftingListUI.Draw();
                                    break;
                                case OpenWindow.About:
                                    AboutTab.Draw("Artisan");
                                    break;
                                case OpenWindow.Debug:
                                    DebugTab.Draw();
                                    break;
                                case OpenWindow.Macro:
                                    MacroUI.Draw();
                                    break;
                                case OpenWindow.ExpertProfiles:
                                    ExpertProfilesUI.Draw();
                                    break;
                                case OpenWindow.RaphaelCache:
                                    RaphaelCacheUI.Draw();
                                    break;
                                case OpenWindow.Assigner:
                                    AssignerUI.Draw();
                                    break;
                                case OpenWindow.FCWorkshop:
                                    FCWorkshopUI.Draw();
                                    break;
                                case OpenWindow.SpecialList:
                                    SpecialLists.Draw();
                                    break;
                                case OpenWindow.Overview:
                                    DrawOverview();
                                    break;
                                case OpenWindow.Simulator:
                                    SimulatorUI.Draw();
                                    break;
                                case OpenWindow.None:
                                    break;
                                default:
                                    break;
                            }
                            ;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ex.Log();
                }
                ImGui.PopStyleVar();
            }
            catch { }
        }

        private void DrawOverview()
        {
            var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/artisan.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
            {
                ImGuiEx.LineCentered("###ArtisanTextLogo", () =>
                {
                    ImGui.Image(logo.Handle, new Vector2(logo.Width, 100f.Scale()));
                });
            }

            ImGuiEx.LineCentered("###ArtisanOverview", () =>
            {
                ImGuiEx.TextUnderlined("Artisan - Overview");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"I would first like to thank you for downloading my little crafting plugin. I have been working on Artisan consistently since June 2022 and it's my magnum opus of a plugin.");
            ImGui.Spacing();
            ImGuiEx.TextWrapped($"Before you get started with Artisan, we should go over a few things about how the plugin works. Artisan is simple to use once you understand a few key factors.");

            ImGui.Spacing();
            ImGuiEx.LineCentered("###ArtisanModes", () =>
            {
                ImGuiEx.TextUnderlined("Crafting Modes");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan features an \"Automatic Action Execution Mode\" which merely takes the suggestions provided to it and performs the action on your behalf." +
                                " By default, this will fire as fast as the game allows, which is faster than normal macros." +
                                " You are not bypassing any sort of game restrictions doing this, however you can set a delay should you choose to." +
                                " Enabling this has nothing to do with the suggestion making process Artisan uses by default.");

            var automode = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/AutoMode.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(automode, out var example))
            {
                ImGuiEx.LineCentered("###AutoModeExample", () =>
                {
                    ImGui.Image(example.Handle, new Vector2(example.Width, example.Height));
                });
            }

            ImGuiEx.TextWrapped($"If you do not have the automatic mode enabled, you will have access to 2 more modes. \"Semi-Manual Mode\" and \"Full Manual\"." +
                                $" \"Semi-Manual Mode\" will appear in a small pop-up window when you start crafting.");

            var craftWindowExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/ThemeCraftingWindowExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(craftWindowExample, out example))
            {
                ImGuiEx.LineCentered("###CraftWindowExample", () =>
                {
                    ImGui.Image(example.Handle, new Vector2(example.Width, example.Height));
                });
            }

            ImGuiEx.TextWrapped($"By clicking the \"Execute recommended action\" button, you are instructing the plugin to perform the suggestion it has recommended." +
                $" This considered semi-manual as you still have to click each action, but you don't have to worry about finding them on your hotbars." +
                $" \"Full-Manual\" mode is performed by pressing the buttons on your hotbar as normal." +
                $" You are provided with an aid by default as Artisan will highlight the action on your hotbar if it is slotted. (This can be disabled in the settings)");

            var outlineExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/OutlineExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(outlineExample, out example))
            {
                ImGuiEx.LineCentered("###OutlineExample", () =>
                {
                    ImGui.Image(example.Handle, new Vector2(example.Width, example.Height));
                });
            }

            ImGui.Spacing();
            ImGuiEx.LineCentered("###ArtisanSuggestions", () =>
            {
                ImGuiEx.TextUnderlined("Solvers/Macros");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan by default will provide you with suggestions on what your next crafting step should be. This solver is not perfect however and it is definitely not a substitute for having appropriate gear. " +
                $"You do not need to do anything to enable this behaviour other than have Artisan enabled. " +
                $"\r\n\r\n" +
                $"If you are trying to tackle a craft that the default solver cannot craft, Artisan allows you to build macros which can be used as the suggestions instead of the default solver. " +
                $"Artisan macros have the benefit of not being restricted in length, can fire off as fast as the game allows and also allows some additional options to tweak on the fly.");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"Click here to be taken to the Macro menu.");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Macro;
            }
            ImGui.Spacing();
            ImGuiEx.TextWrapped($"Once you have created a macro, you will have to assign it to a recipe. This is easily accomplished by using the Recipe Window dropdown. By default, this is attached to the top right of the in-game crafting log window but can be unattached in the settings.");


            var recipeWindowExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/RecipeWindowExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(recipeWindowExample, out example))
            {
                ImGuiEx.LineCentered("###RecipeWindowExample", () =>
                {
                    ImGui.Image(example.Handle, new Vector2(example.Width, example.Height));
                });
            }


            ImGuiEx.TextWrapped($"Select a macro you have created from the dropdown box. " +
                $"When you go to craft this item, the suggestions will be replaced by the contents of your macro.");


            ImGui.Spacing();
            ImGuiEx.LineCentered("###Endurance", () =>
            {
                ImGuiEx.TextUnderlined("Endurance");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan has a mode titled \"Endurance Mode\" which is basically a fancier way of saying \"Auto-repeat mode\" which will continually try to craft the same item for you. " +
                $"Endurance Mode works by selecting a recipe from the in-game crafting log and enabling the feature. " +
                $"Your character will then attempt to keep crafting that item as many times as you have materials for it. " +
                $"\r\n\r\n" +
                $"The other features should hopefully be self-explanatory as Endurance Mode can also manage the usage of your food, potions, manuals, repairs and materia extraction between crafts. " +
                $"The repair feature only supports repairing with dark matter and does not support repair NPCs.");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"Click here to be taken to the Endurance menu.");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Endurance;
            }

            ImGui.Spacing();
            ImGuiEx.LineCentered("###Lists", () =>
            {
                ImGuiEx.TextUnderlined("Crafting Lists");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"Artisan also has the ability to create a list of items and have it start crafting each of them, one after another, automatically. " +
                $"Crafting lists have a lot of powerful tools to streamline the process of going from materials to final products. " +
                $"It also supports importing and exporting to Teamcraft.");

            ImGui.Spacing();
            ImGuiEx.TextUnderlined($"Click here to be taken to the Crafting List menu.");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }
            if (ImGui.IsItemClicked())
            {
                OpenWindow = OpenWindow.Lists;
            }

            ImGui.Spacing();
            ImGuiEx.LineCentered("###Questions", () =>
            {
                ImGuiEx.TextUnderlined("Got Questions?");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"If you have questions about things not outlined here, you can drop a question in our");
            ImGui.SameLine(ImGui.GetCursorPosX(), 1.5f);
            ImGuiEx.TextUnderlined($"Discord server.");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsItemClicked())
                {
                    Util.OpenLink("https://discord.gg/Zzrcc8kmvy");
                }
            }

            ImGuiEx.TextWrapped($"You can also raise issues on our");
            ImGui.SameLine(ImGui.GetCursorPosX(), 2f);
            ImGuiEx.TextUnderlined($"Github page.");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

                if (ImGui.IsItemClicked())
                {
                    Util.OpenLink("https://github.com/PunishXIV/Artisan");
                }
            }

        }

        public static void DrawMainWindow()
        {
            ImGui.TextWrapped($"Here you can change some settings Artisan will use. Some of these can also be toggled during a craft.");
            ImGui.TextWrapped($"In order to use Artisan's manual recommendation highlights, please slot every crafting action you have unlocked to a visible hotbar.");
            bool autoEnabled = P.Config.AutoMode;
            int maxQuality = P.Config.MaxPercentage;
            bool useSpecialist = P.Config.UseSpecialist;
            //bool showEHQ = P.Config.ShowEHQ;
            //bool useSimulated = P.Config.UseSimulatedStartingQuality;

            bool changed = false;

            ImGui.Dummy(new Vector2(0, 5f));
            ImGui.Separator();
            ImGui.Dummy(new Vector2(0, 5f));

            if (ImGui.CollapsingHeader("General Settings"))
            {
                ImGui.Indent();

                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Auto Action Mode");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                if (ImGui.Checkbox("Automatic action execution mode", ref autoEnabled))
                {
                    P.Config.AutoMode = autoEnabled;
                    P.Config.Save();
                }
                ImGuiComponents.HelpMarker($"Automatically use each recommended action instead of highlighting them.");

                if (autoEnabled)
                {
                    ImGui.Indent();
                    changed |= ImGui.Checkbox($"Replicate macro delay", ref P.Config.ReplicateMacroDelay);
                    ImGuiComponents.HelpMarker("This setting will delay each automatic action as if you're using an in-game macro with <wait.2> or <wait.3>. While disabled, you can manually set how long Artisan should wait after each action.");

                    if (!P.Config.ReplicateMacroDelay)
                    {
                        var delay = P.Config.AutoDelay;
                        ImGui.PushItemWidth(250);
                        if (ImGui.SliderInt("Execution delay (ms)###ActionDelay", ref delay, 0, 1000))
                        {
                            if (delay < 0) delay = 0;
                            if (delay > 1000) delay = 1000;

                            P.Config.AutoDelay = delay;
                            P.Config.Save();
                        }
                    }
                    ImGui.Unindent();
                }

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Consumables");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Enforce consumables", ref P.Config.AbortIfNoFoodPot);
                ImGuiComponents.HelpMarker("Artisan will require the configured food, manual, and medicine and refuse to craft if they cannot be found.");

                changed |= ImGui.Checkbox("Use consumables for trial crafts", ref P.Config.UseConsumablesTrial);
                changed |= ImGui.Checkbox("Use consumables for Quick Synth crafts", ref P.Config.UseConsumablesQuickSynth);

                ImGui.SetNextItemWidth(32f.Scale());
                if (ImGui.InputInt("Don't use consumables when level difference with craft is greater than", ref P.Config.ConsumableLevelGapDifference))
                {
                    if (P.Config.ConsumableLevelGapDifference < 0)
                        P.Config.ConsumableLevelGapDifference = 0;

                    P.Config.Save();
                }

                StringBuilder helper = new("You will not use consumables on crafts below this level for each of the following jobs:\n\n");
                for (uint i = (uint)Job.CRP; i <= (uint)Job.ALC; i++)
                {
                    var j = Svc.Data.GetExcelSheet<ClassJob>().GetRow(i).Abbreviation.ToString().ToUpper();
                    var l = CharacterInfo.JobLevel((Job)i);
                    var d = Math.Max(1, l - P.Config.ConsumableLevelGapDifference);
                    helper.Append($"{j} - {d}\n");
                }
                var maxLevel = Svc.Data.GetExcelSheet<RecipeLevelTable>().Max(x => x.ClassJobLevel);
                ImGuiComponents.HelpMarker($"Set this to {maxLevel} to disable.\r\n{helper}");

                if (ImGui.CollapsingHeader("Default Consumables"))
                {
                    ImGui.Indent();
                    changed |= P.Config.DefaultConsumables.DrawFood();
                    changed |= P.Config.DefaultConsumables.DrawPotion();
                    changed |= P.Config.DefaultConsumables.DrawManual();
                    changed |= P.Config.DefaultConsumables.DrawSquadronManual();
                    ImGui.Unindent();
                }

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Repairs");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox($"Prioritize NPC repairs over self-repairs", ref P.Config.PrioritizeRepairNPC);
                ImGuiComponents.HelpMarker("When repairing, if a repair NPC is nearby it will try to repair with them instead of self-repairs. Will still try to use self-repairs if no NPC is found and you have the required levels to repair.");

                changed |= ImGui.Checkbox($"Disable Endurance if unable to repair", ref P.Config.DisableEnduranceNoRepair);
                ImGuiComponents.HelpMarker($"Endurance is what continues crafting the same item after finishing it once, i.e. \"Craft X\".");

                changed |= ImGui.Checkbox($"Pause lists if unable to repair", ref P.Config.DisableListsNoRepair);

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Sounds");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Play sound after finishing Endurance", ref P.Config.PlaySoundFinishEndurance);
                changed |= ImGui.Checkbox("Play sound after finishing a list", ref P.Config.PlaySoundFinishList);
                changed |= ImGui.Checkbox("Play sound on errors", ref P.Config.PlaySoundError);

                if (P.Config.PlaySoundFinishEndurance || P.Config.PlaySoundFinishList || P.Config.PlaySoundError)
                {
                    ImGui.PushItemWidth(250);
                    changed |= ImGui.SliderFloat("Volume", ref P.Config.SoundVolume, 0f, 1f, "%.2f");
                }

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Misc.");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Disable Endurance and pause lists when Duty Finder is ready", ref P.Config.RequestToStopDuty);

                if (P.Config.RequestToStopDuty)
                {
                    changed |= ImGui.Checkbox("Resume Endurance and unpause lists when done", ref P.Config.RequestToResumeDuty);

                    if (P.Config.RequestToResumeDuty)
                    {
                        ImGui.PushItemWidth(250);
                        changed |= ImGui.SliderInt("Delay before resuming (seconds)", ref P.Config.RequestToResumeDelay, 5, 60);
                    }
                }

                changed |= ImGui.Checkbox("Disable auto-equipping required items for special recipes", ref P.Config.DontEquipItems);
                ImGuiComponents.HelpMarker("Examples include the Ixal society quest and Endwalker relic tool recipes.");

                ImGui.Dummy(new Vector2(0, 5f));
                if (ImGuiEx.ButtonCtrl("Reset All Cosmic Exploration Recipe Configs"))
                {
                    var copy = P.Config.RecipeConfigs;
                    foreach (var c in copy)
                    {
                        if (Svc.Data.GetExcelSheet<Recipe>().GetRow(c.Key).Number == 0)
                            P.Config.RecipeConfigs.Remove(c.Key);
                    }
                    P.Config.Save();
                }

                ImGui.Unindent();

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 10f));
            }

            if (ImGui.CollapsingHeader("Macro Settings"))
            {
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Skip macro step if unable to use action", ref P.Config.SkipMacroStepIfUnable);
                changed |= ImGui.Checkbox($"Prevent Artisan from auto-solving after macro", ref P.Config.DisableMacroArtisanRecommendation);
                ImGuiComponents.HelpMarker($"Only applies if the macro doesn't finish the recipe. Artisan will use the Standard or Expert Solver based on the recipe.");

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 10f));
            }

            if (ImGui.CollapsingHeader("Standard Recipe Solver Settings"))
            {
                string ProgressString = LuminaSheets.AddonSheet[213].Text.ToString();
                string QualityString = LuminaSheets.AddonSheet[216].Text.ToString();
                string ConditionString = LuminaSheets.AddonSheet[215].Text.ToString();

                ImGui.Indent();

                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Action Usage");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                P.PluginUi.ExpertSettingsUI.DrawIconText("Force [s!TricksOfTrade] when {0} is:", [ConditionString.ToLower()]);
                P.PluginUi.ExpertSettingsUI.HelpMarkerWithIcons(["These options make the solver prioritize [s!TricksOfTrade] on the appropriate {0}.", "This is only invoked when [s!PreciseTouch] or [s!IntensiveSynthesis] would be used.", "[s!TricksOfTrade] will be used regardless when it's optimal to do so."], [ConditionString.ToLower()]);
                ImGui.Indent();
                changed |= P.PluginUi.ExpertSettingsUI.CheckboxWithIcons("useTricksGood", ref P.Config.UseTricksGood, "[c!Good]");
                changed |= P.PluginUi.ExpertSettingsUI.CheckboxWithIcons("useTricksExcellent", ref P.Config.UseTricksExcellent, "[c!Excellent]");
                ImGui.Unindent();

                changed |= ImGui.Checkbox("Use specialist actions", ref P.Config.UseSpecialist);
                P.PluginUi.ExpertSettingsUI.HelpMarkerWithIcons(["If the current job is a specialist, this will spend crafter's delineations.", "[s!CarefulObservation] will be used whenever the solver would [s!Observe].", "[s!HeartAndSoul] will be used for an early [s!PreciseTouch]."]);

                changed |= P.PluginUi.ExpertSettingsUI.CheckboxWithIcons("useQualityStarter", ref P.Config.UseQualityStarter, "Start crafts with [s!Reflect]");
                P.PluginUi.ExpertSettingsUI.HelpMarkerWithIcons(["This tends to be more favourable for recipes with lower durability.", "If disabled, each craft will start with [s!MuscleMemory]."]);

                //if (ImGui.Checkbox("Low Stat Mode", ref P.Config.LowStatsMode))
                //    P.Config.Save();
                //ImGuiComponents.HelpMarker("This swaps out Waste Not II & Groundwork for Prudent Synthesis");

                ImGui.Dummy(new Vector2(0, 2f));
                P.PluginUi.ExpertSettingsUI.DrawIconText("Use [s!PreparatoryTouch] at or below this many {0} stacks:", [Buffs.InnerQuiet.NameOfBuff()]);
                ImGuiComponents.HelpMarker($"Reducing this can help save CP.");
                ImGui.PushItemWidth(250);
                changed |= ImGui.SliderInt($"###MaxIQStacksPrepTouch", ref P.Config.MaxIQPrepTouch, 0, 10);

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"{QualityString}");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                ImGui.TextWrapped($"Max {QualityString.ToLower()} for non-collectable recipes:");
                ImGuiComponents.HelpMarker($"Once quality has reached this percentage, the Standard Solver will focus on progress.");
                ImGui.PushItemWidth(250);
                changed |= ImGui.SliderInt("###SliderMaxQuality", ref P.Config.MaxPercentage, 0, 100, $"%d%%");

                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"{QualityString} breakpoint for collectables:");
                ImGuiComponents.HelpMarker($"The solver will stop going for {QualityString.ToLower()} once a collectable has hit this breakpoint. The specific breakpoints vary per recipe.");

                if (ImGui.RadioButton($"1st", P.Config.SolverCollectibleMode == 1))
                {
                    P.Config.SolverCollectibleMode = 1;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton($"2nd", P.Config.SolverCollectibleMode == 2))
                {
                    P.Config.SolverCollectibleMode = 2;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton($"3rd", P.Config.SolverCollectibleMode == 3))
                {
                    P.Config.SolverCollectibleMode = 3;
                    P.Config.Save();
                }
                ImGui.SameLine();
                if (ImGui.RadioButton($"Max", P.Config.SolverCollectibleMode == 4))
                {
                    P.Config.SolverCollectibleMode = 4;
                    P.Config.Save();
                }

                ImGui.Dummy(new Vector2(0, 2f));
                var thresholdImg = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/CollectableThresholds.png");
                if (ThreadLoadImageHandler.TryGetTextureWrap(thresholdImg, out var img))
                {
                    ImGui.Image(img.Handle, new Vector2(img.Width, img.Height));
                }

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Cosmic Exploration");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                ImGui.PushItemWidth(250);
                changed |= P.PluginUi.ExpertSettingsUI.SliderIntWithIcons("MaxMaterialMiracles", ref P.Config.MaxMaterialMiracles, 0, 3, "Max [s!MaterialMiracle] uses per craft");
                ImGuiComponents.HelpMarker($"This will switch the Standard Solver over to the Expert Solver for the duration of the buff. Material Miracle is a timed buff, not a permanent one with stacks; when simulating recipes with the Standard Solver, it will estimate how long the buff lasts based on the length of each skill's animation.");
                if (P.Config.MaxMaterialMiracles > 0)
                {
                    ImGui.PushItemWidth(250);
                    changed |= ImGui.SliderInt($"Use after this many steps###MinimumStepsBeforeMiracle", ref P.Config.MinimumStepsBeforeMiracle, 0, 20);
                }

                ImGui.Unindent();

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 10f));
            }

            bool openExpert = false;
            if (ImGui.CollapsingHeader("Expert Recipe Solver Settings"))
            {
                openExpert = true;
                if (P.PluginUi.ExpertSettingsUI.expertIcon is not null)
                {
                    ImGui.SameLine();
                    ImGui.Image(P.PluginUi.ExpertSettingsUI.expertIcon.Handle, new(P.PluginUi.ExpertSettingsUI.expertIcon.Width * ImGuiHelpers.GlobalScaleSafe, ImGui.GetItemRectSize().Y), new(0, 0), new Vector2(1, 1), new(0.94f, 0.57f, 0f, 1f));
                }
                if (P.PluginUi.ExpertSettingsUI.DrawGlobalSettings(P.Config.ExpertSolverConfig))
                    P.Config.Save();
            }
            if (!openExpert)
            {
                if (P.PluginUi.ExpertSettingsUI.expertIcon is not null)
                {
                    ImGui.SameLine();
                    ImGui.Image(P.PluginUi.ExpertSettingsUI.expertIcon.Handle, new(P.PluginUi.ExpertSettingsUI.expertIcon.Width * ImGuiHelpers.GlobalScaleSafe, ImGui.GetItemRectSize().Y), new(0, 0), new Vector2(1, 1), new(0.94f, 0.57f, 0f, 1f));
                }
            }

            if (ImGui.CollapsingHeader("Raphael Solver Settings"))
            {
                if (P.Config.RaphaelSolverConfig.Draw())
                    P.Config.Save();
            }

            using (ImRaii.Disabled())
            {
                if (ImGui.CollapsingHeader("Script Solver Settings (Currently Disabled)"))
                {
                    if (P.Config.ScriptSolverConfig.Draw())
                        P.Config.Save();
                }
            }
            if (ImGui.CollapsingHeader("UI Settings"))
            {
                ImGui.Indent();

                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"General");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Disable highlighting box", ref P.Config.DisableHighlightedAction);
                ImGuiComponents.HelpMarker("This is the box that highlights the actions on your hotbars for manual play.");

                changed |= ImGui.Checkbox($"Disable recommendation toasts", ref P.Config.DisableToasts);
                ImGuiComponents.HelpMarker("These are the pop-ups whenever a new action is recommended.");

                changed |= ImGui.Checkbox("Disable Custom Theme", ref P.Config.DisableTheme);
                ImGui.SameLine();
                if (IconButtons.IconTextButton(FontAwesomeIcon.Clipboard, "Copy Theme"))
                {
                    ImGui.SetClipboardText("DS1H4sIAAAAAAAACq1YS3PbNhD+Kx2ePR6AeJG+xXYbH+KOJ3bHbW60REusaFGlKOXhyX/v4rEACEqumlY+ECD32/cuFn7NquyCnpOz7Cm7eM1+zy5yvfnDPL+fZTP4at7MHVntyMi5MGTwBLJn+HqWLZB46Ygbx64C5kQv/nRo8xXQ3AhZZRdCv2jdhxdHxUeqrJO3Ftslb5l5u/Fa2rfEvP0LWBkBPQiSerF1Cg7wApBn2c5wOMv2juNn9/zieH09aP63g+Kqyr1mI91mHdj5mj3UX4bEG+b5yT0fzRPoNeF1s62e2np+EuCxWc+7z5cLr1SuuCBlkTvdqBCEKmaQxCHJeZmXnFKlgMHVsmnnEZ5IyXMiFUfjwt6yCHvDSitx1212m4gHV0QURY4saMEYl6Q4rsRl18/rPuCZQ+rFJxeARwyAJb5fVmD4NBaJEK3eL331UscuAgflOcY0J5zLUioHpHmhCC0lCuSBwU23r3sfF/0N0wKdoxcGFqHezYZmHypJIkgiSCJIalc8NEM7Utb6ErWlwngt9aUoFRWSB3wilRUl5SRwISUFvhJt9lvDrMgLIjgLzK66tq0228j0H+R3W693l1UfmUd9kqA79MKn9/2sB9lPI8hbofb073vdh1BbQYRgqKzfGbTfTWVqHmnMOcXUpI6BXhzGJjEQCNULmy4x9GpZz1a3Vb8KqaIDz4RPVGZin6dlZPKDSS29baAyRqYfzVGnr0ekaaowTbEw9MLjLnfD0GGT1unHSSlKr2lRyqLA2qU5ESovi6m+lkvqYiZ1/ygxyqrgjDKF8Yr2lp1pd4R7dokhvOBUQk37TCVKQbX4TMVtyuymruKWJCURVEofClYWbNpWCQfFifDwsWnYyXXS8ZxDOI+H0uLToPzrhKg3VV8N3amt1dP/t5goW/E85pg2pB8N8sd623yr3/dNOPYVstELg9cLA8zFCJKapQpEYkPVi9CMA/L/Uv8hrk1hmg9WKKMQXyIxnGFrm6i06MkhBHlIiQ8rI0xx4k/rsLWBsWpbTmmhqFIypcvUHTRgQ859V/bbKaPf1s/dbBcfD0R6NnCWwg/dS3lB4MfQMSrnCY9EK8qEw9uUl4YdHjRQRVFTuu5mq2a9uOvrfVOH0SDHqtXxMjDfi1RA/fyyGb7G5y5KdJg8EnTXdsOHZl1vQyJJQrlCQTDsEBi80HdhO+VwrEP48hwdTRp202yHbgGzhRfu03/UCA4gjglDd44mUT2D2i4UH9coSy8mfjEYN54NfbcOOIZnn15M7YqAH5rFEmdl3eJ8r0N5E9zH0fz71nQQyN+1/zSP6yR2A/l93dazoY6n5DdyiumWc91Xi+u+2zxU/aI+Jipq2QD5tdrfgO3t2P5jcqz9gLEXAEjgFHzcMJUgr5uXyDQsNSxZtCvX81s3r1qLOw0EztC3ORiEs4vssu9W9fqn2263HqpmncFF016PqklGjh1kjQ2NUyUJH08mcIk9gSrqn+jg0XFoqeqTrmDPwQv+PDEr6wl3oljaxcRSRTCyMc/lJJ/lAcnNhMr3WWZ+ES3exrXE+HJ2yNOrowkb97A2cExdXcrYjaFToVDfGSMqnCaDa0pi/vzNMyLG/wQEyzmzfhx7KAwJUn93Fz6v5shD8B+DRAG4Oh+QHYapovAd3/OEQzuiDSdE4c8wjJHh7iiBFFozvP3+NxT8RWGlEQAA");
                    Notify.Success("Theme copied to clipboard");
                }

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Artisan Windows");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Dock mini-menu position to Crafting Log", ref P.Config.LockMiniMenuR);

                ImGui.Indent();
                if (!P.Config.LockMiniMenuR)
                {
                    changed |= ImGui.Checkbox($"Lock current mini-menu position", ref P.Config.PinMiniMenu);
                }
                if (ImGui.Button("Reset Mini-Menu Position"))
                {
                    AtkResNodeFunctions.ResetPosition = true;
                }
                ImGui.Unindent();

                changed |= ImGui.Checkbox("Hide mini-menu simulator results", ref P.Config.HideRecipeWindowSimulator);

                changed |= ImGui.Checkbox($"Hide Quest Helper", ref P.Config.HideQuestHelper);
                ImGuiComponents.HelpMarker("If not disabled, the Quest Helper is a small window that can open the Crafting Log to a specific recipe, /say a specific phrase, or execute a specific emote for quests that require them. It will only appear while on those quests.");

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Native UI Replacements");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox($"Expanded Crafting Log search bar", ref P.Config.ReplaceSearch);
                ImGuiComponents.HelpMarker($"Expands the search bar in the Crafting Log with instant results. Click on any result to open it in the Crafting Log.");

                changed |= ImGui.Checkbox("Use Native Craft-X Buttons in Recipe Log", ref P.Config.UseNativeButtons);
                ImGuiComponents.HelpMarker("This will change the Craft-X button interface to one using native game assets.");

                changed |= ImGui.Checkbox("Show leveling category completion in Crafting Log", ref P.Config.ShowLevelingRecipeProgress);
                ImGuiComponents.HelpMarker("Shows a total of completed recipes in each leveling category, or a tick if all are completed.");

                changed |= ImGui.Checkbox("Disable context menu options", ref P.Config.HideContextMenus);
                ImGuiComponents.HelpMarker("These are the Artisan-added options when you right click or press square on a recipe or item.");

                if (!P.Config.HideContextMenus)
                {
                    ImGui.Indent();
                    ImGui.PushItemWidth(50);
                    if (ImGui.InputInt("Add new items to lists this many times from context menu", ref P.Config.ContextMenuLoops))
                    {
                        if (P.Config.ContextMenuLoops <= 0)
                            P.Config.ContextMenuLoops = 1;

                        P.Config.Save();
                    }
                    ImGui.Unindent();
                }

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Other Plugins");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Disable Allagan Tools integration with crafting lists", ref P.Config.DisableAllaganTools);

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Simulator UI");
                ImGuiComponents.HelpMarker("These settings apply to Artisan's \"Simulator\" tab.");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                ImGui.PushItemWidth(250);
                changed |= ImGui.SliderFloat("Simulator action image size", ref P.Config.SimulatorActionSize, 5f, 70f);
                ImGuiComponents.HelpMarker("Sets the scale of the action images in the simulator.");

                changed |= ImGui.Checkbox("Enable hover preview in manual mode", ref P.Config.SimulatorHoverMode);
                changed |= ImGui.Checkbox($"Hide action tooltips in manual mode", ref P.Config.DisableSimulatorActionTooltips);

                ImGui.Unindent();

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 10f));
            }

            if (ImGui.CollapsingHeader("List Settings"))
            {
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"These settings will automatically be applied when creating a crafting list.");

                ImGui.Indent();

                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"General");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Skip crafting items you already have enough of", ref P.Config.DefaultListSkip);
                changed |= ImGui.Checkbox("Set new items added to list as Quick Synth", ref P.Config.DefaultListQuickSynth);
                changed |= ImGui.Checkbox("Automatically adjust all sub-crafts after changing quantities", ref P.Config.DefaultAdjustQuantities);
                changed |= ImGui.Checkbox("Reset \"Number of Times to Add\" after adding to list", ref P.Config.ResetTimesToAdd);

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.TextWrapped($"Automation");
                ImGui.Dummy(new Vector2(0, 2f));
                ImGui.Indent();

                changed |= ImGui.Checkbox("Automatically extract materia from spiritbonded gear", ref P.Config.DefaultListMateria);

                changed |= ImGui.Checkbox("Automatically repair gear", ref P.Config.DefaultListRepair);

                if (P.Config.DefaultListRepair)
                {
                    ImGui.TextWrapped($"Repair at:");
                    ImGui.SameLine();
                    ImGui.PushItemWidth(250);
                    changed |= ImGui.SliderInt("durability###SliderRepairDefault", ref P.Config.DefaultListRepairPercent, 0, 100, $"%d%%");
                }

                ImGui.PushItemWidth(250);
                if (ImGui.SliderFloat("Delay between list crafts (seconds)", ref P.Config.ListCraftThrottle2, 0f, 2f, "%.1f"))
                {
                    if (P.Config.ListCraftThrottle2 < 0f)
                        P.Config.ListCraftThrottle2 = 0f;

                    if (P.Config.ListCraftThrottle2 > 2f)
                        P.Config.ListCraftThrottle2 = 2f;

                    P.Config.Save();
                }

                ImGui.Unindent();

                ImGui.Dummy(new Vector2(0, 5f));
                if (ImGui.CollapsingHeader("Ingredient Table Settings"))
                {
                    ImGuiEx.TextWrapped(ImGuiColors.DalamudYellow, "Column settings will only take effect if you have already viewed a list's ingredients table.");

                    changed |= ImGui.Checkbox($"Default: Hide \"Inventory\" column", ref P.Config.DefaultHideInventoryColumn);
                    changed |= ImGui.Checkbox($"Default: Hide \"Retainers\" column", ref P.Config.DefaultHideRetainerColumn);
                    changed |= ImGui.Checkbox($"Default: Hide \"Remaining Needed\" column", ref P.Config.DefaultHideRemainingColumn);
                    changed |= ImGui.Checkbox($"Default: Hide \"Sources\" column", ref P.Config.DefaultHideCraftableColumn);
                    changed |= ImGui.Checkbox($"Default: Hide \"Number Craftable\" column", ref P.Config.DefaultHideCraftableCountColumn);
                    changed |= ImGui.Checkbox($"Default: Hide \"Used to Craft\" column", ref P.Config.DefaultHideCraftItemsColumn);
                    changed |= ImGui.Checkbox($"Default: Hide \"Category\" column", ref P.Config.DefaultHideCategoryColumn);
                    changed |= ImGui.Checkbox($"Default: Hide \"Gathered Zone\" column", ref P.Config.DefaultHideGatherLocationColumn);
                    changed |= ImGui.Checkbox($"Default: Hide \"ID\" column", ref P.Config.DefaultHideIdColumn);
                    changed |= ImGui.Checkbox($"Default: Enable \"Only Show HQ Crafts\"", ref P.Config.DefaultHQCrafts);
                    changed |= ImGui.Checkbox($"Default: Enable \"Colour Validation\"", ref P.Config.DefaultColourValidation);

                    ImGui.Dummy(new Vector2(0, 5f));
                    changed |= ImGui.Checkbox($"Fetch prices from Universalis", ref P.Config.UseUniversalis);
                    if (P.Config.UseUniversalis)
                    {
                        changed |= ImGui.Checkbox($"Limit Universalis to current data center", ref P.Config.LimitUnversalisToDC);
                        changed |= ImGui.Checkbox($"Only fetch prices when requested", ref P.Config.UniversalisOnDemand);
                        ImGuiComponents.HelpMarker("If enabled, you will have to click a button for each item to fetch its price.");
                    }
                }

                ImGui.Unindent();
                ImGui.Dummy(new Vector2(0, 10f));
            }

            if (changed)
            {
                P.Config.Save();
            }
        }

        private void ShowEnduranceMessage()
        {
            if (!P.Config.ViewedEnduranceMessage)
            {
                P.Config.ViewedEnduranceMessage = true;
                P.Config.Save();

                ImGui.OpenPopup("EndurancePopup");

                var windowSize = new Vector2(512 * ImGuiHelpers.GlobalScale,
                    ImGui.GetTextLineHeightWithSpacing() * 13 + 2 * ImGui.GetFrameHeightWithSpacing() * 2f);
                ImGui.SetNextWindowSize(windowSize);
                ImGui.SetNextWindowPos((ImGui.GetIO().DisplaySize - windowSize) / 2);

                using var popup = ImRaii.Popup("EndurancePopup",
                    ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.Modal);
                if (!popup)
                    return;

                ImGui.TextWrapped($@"I have been receiving quite a number of messages regarding ""buggy"" Endurance mode not setting ingredients anymore. As of the previous update, the old functionality of Endurance has been moved to a new setting.");
                ImGui.Dummy(new Vector2(0));

                var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/EnduranceNewSetting.png");

                if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var img))
                {
                    ImGuiEx.LineCentered("###EnduranceNewSetting", () =>
                    {
                        ImGui.Image(img.Handle, new Vector2(img.Width, img.Height));
                    });
                }

                ImGui.Spacing();

                ImGui.TextWrapped($"This change was made to bring back the very original behaviour of Endurance mode. If you do not care about your ingredient ratio, please make sure to enable Max Quantity Mode.");

                ImGui.SetCursorPosY(windowSize.Y - ImGui.GetFrameHeight() - ImGui.GetStyle().WindowPadding.Y);
                if (ImGui.Button("Close", -Vector2.UnitX))
                {
                    ImGui.CloseCurrentPopup();
                }
            }
        }
    }

    public enum OpenWindow
    {
        None = 0,
        Main = 1,
        Endurance = 2,
        Macro = 3,
        Lists = 4,
        About = 5,
        Debug = 6,
        FCWorkshop = 7,
        SpecialList = 8,
        Overview = 9,
        Simulator = 10,
        RaphaelCache = 11,
        Assigner = 12,
        ExpertProfiles = 13,
    }
}
