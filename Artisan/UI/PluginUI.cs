using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.FCWorkshops;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using PunishLib.ImGuiMethods;
using System;
using System.IO;
using System.Numerics;
using ThreadLoadImageHandler = ECommons.ImGuiMethods.ThreadLoadImageHandler;

namespace Artisan.UI
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    unsafe internal class PluginUI : Window
    {
        public event EventHandler<bool>? CraftingWindowStateChanged;

        // this extra bool exists for ImGui, since you can't ref a property
        private bool visible = false;
        public OpenWindow OpenWindow { get; private set; } = OpenWindow.Overview;

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
            P.ws.AddWindow(this);
        }

        public override void PreDraw()
        {
            if (!P.config.DisableTheme)
            {
                P.Style.Push();
                ImGui.PushFont(P.CustomFont);
                P.StylePushed = true;
            }

        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                ImGui.PopFont();
                P.StylePushed = false;
            }
        }

        public void Dispose()
        {

        }

        public override void Draw()
        {
            var region = ImGui.GetContentRegionAvail();
            var itemSpacing = ImGui.GetStyle().ItemSpacing;

            var topLeftSideHeight = region.Y;

            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5f.Scale(), 0));
            try
            {
                if (ImGui.BeginTable($"ArtisanTableContainer", 2, ImGuiTableFlags.Resizable))
                {
                    ImGui.TableSetupColumn("##LeftColumn", ImGuiTableColumnFlags.WidthFixed, ImGui.GetWindowWidth() / 2);

                    ImGui.TableNextColumn();

                    var regionSize = ImGui.GetContentRegionAvail();

                    ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new Vector2(0.5f, 0.5f));
                    if (ImGui.BeginChild($"###ArtisanLeftSide", regionSize with { Y = topLeftSideHeight }, false, ImGuiWindowFlags.NoDecoration))
                    {
                        var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/artisan-icon.png");

                        if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
                        {
                            ImGuiEx.ImGuiLineCentered("###ArtisanLogo", () =>
                            {
                                ImGui.Image(logo.ImGuiHandle, new(125f.Scale(), 125f.Scale()));
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
                        if (ImGui.Selectable("About", OpenWindow == OpenWindow.About))
                        {
                            OpenWindow = OpenWindow.About;
                        }


#if DEBUG
                        ImGui.Spacing();
                        if (ImGui.Selectable("DEBUG", OpenWindow == OpenWindow.Debug))
                        {
                            OpenWindow = OpenWindow.Debug;
                        }
                        ImGui.Spacing();
#endif

                    }
                    ImGui.EndChild();
                    ImGui.PopStyleVar();
                    ImGui.TableNextColumn();
                    if (ImGui.BeginChild($"###ArtisanRightSide", Vector2.Zero, false))
                    {

                        if (OpenWindow == OpenWindow.Main)
                        {
                            DrawMainWindow();
                        }

                        if (OpenWindow == OpenWindow.Endurance)
                        {
                            Handler.Draw();
                        }

                        if (OpenWindow == OpenWindow.Lists)
                        {
                            CraftingListUI.Draw();
                        }

                        if (OpenWindow == OpenWindow.About)
                        {
                            AboutTab.Draw(P);
                        }

                        if (OpenWindow == OpenWindow.Debug)
                        {
                            AutocraftDebugTab.Draw();
                        }

                        if (OpenWindow == OpenWindow.Macro)
                        {
                            MacroUI.Draw();
                        }

                        if (OpenWindow == OpenWindow.FCWorkshop)
                        {
                            FCWorkshopUI.Draw();
                        }

                        if (OpenWindow == OpenWindow.SpecialList)
                        {
                            SpecialLists.Draw();
                        }

                        if (OpenWindow == OpenWindow.Overview)
                        {
                            DrawOverview();
                        }

                    }
                    ImGui.EndChild();
                    ImGui.EndTable();
                }
            }
            catch(Exception ex)
            {
                ex.Log();
            }
            ImGui.PopStyleVar();
        }

        private void DrawOverview()
        {
            var imagePath = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/artisan.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(imagePath, out var logo))
            {
                ImGuiEx.ImGuiLineCentered("###ArtisanTextLogo", () =>
                {
                    ImGui.Image(logo.ImGuiHandle, new Vector2(logo.Width, 100f.Scale()));
                });
            }

            ImGuiEx.ImGuiLineCentered("###ArtisanOverview", () =>
            {
                ImGuiEx.TextUnderlined("Artisan - Overview");
            });
            ImGui.Spacing();

            ImGuiEx.TextWrapped($"I would first like to thank you for downloading my little crafting plugin. I have been working on Artisan consistently since June 2022 and it's my magnum opus of a plugin.");
            ImGui.Spacing();
            ImGuiEx.TextWrapped($"Before you get started with Artisan, we should go over a few things about how the plugin works. Artisan is simple to use once you understand a few key factors.");

            ImGui.Spacing();
            ImGuiEx.ImGuiLineCentered("###ArtisanModes", () =>
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
                ImGuiEx.ImGuiLineCentered("###AutoModeExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGuiEx.TextWrapped($"If you do not have the automatic mode enabled, you will have access to 2 more modes. \"Semi-Manual Mode\" and \"Full Manual\"." +
                                $" \"Semi-Manual Mode\" will appear in a small pop-up window when you start crafting.");

            //TODO Update to remove debug trial craft repeat from screenshot
            var craftWindowExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/ThemeCraftingWindowExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(craftWindowExample, out example))
            {
                ImGuiEx.ImGuiLineCentered("###CraftWindowExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGuiEx.TextWrapped($"By clicking the \"Execute recommended action\" button, you are instructing the plugin to perform the suggestion it has recommended." +
                $" This considered semi-manual as you still have to click each action, but you don't have to worry about finding them on your hotbars." +
                $" \"Full-Manual\" mode is performed by pressing the buttons on your hotbar as normal." +
                $" You are provided with an aid by default as Artisan will highlight the action on your hotbar if it is slotted. (This can be disabled in the settings)");

            var outlineExample = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Images/OutlineExample.png");

            if (ThreadLoadImageHandler.TryGetTextureWrap(outlineExample, out example))
            {
                ImGuiEx.ImGuiLineCentered("###OutlineExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }

            ImGui.Spacing();
            ImGuiEx.ImGuiLineCentered("###ArtisanSuggestions", () =>
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
                ImGuiEx.ImGuiLineCentered("###RecipeWindowExample", () =>
                {
                    ImGui.Image(example.ImGuiHandle, new Vector2(example.Width, example.Height));
                });
            }


            ImGuiEx.TextWrapped($"Select a macro you have created from the dropdown box. " +
                $"When you go to craft this item, the suggestions will be replaced by the contents of your macro.");


            ImGui.Spacing();
            ImGuiEx.ImGuiLineCentered("###Endurance", () =>
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
            ImGuiEx.ImGuiLineCentered("###Lists", () =>
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
            ImGuiEx.ImGuiLineCentered("###Questions", () =>
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
            ImGui.TextWrapped($"In order to use Artisan's manual highlight, please slot every crafting action you have unlocked to a visible hotbar.");
            bool autoEnabled = Service.Configuration.AutoMode;
            bool delayRec = Service.Configuration.DelayRecommendation;
            bool failureCheck = Service.Configuration.DisableFailurePrediction;
            int maxQuality = Service.Configuration.MaxPercentage;
            bool useTricksGood = Service.Configuration.UseTricksGood;
            bool useTricksExcellent = Service.Configuration.UseTricksExcellent;
            bool useSpecialist = Service.Configuration.UseSpecialist;
            //bool showEHQ = Service.Configuration.ShowEHQ;
            //bool useSimulated = Service.Configuration.UseSimulatedStartingQuality;
            bool disableGlow = Service.Configuration.DisableHighlightedAction;
            bool disableToasts = Service.Configuration.DisableToasts;
            bool disableMini = Service.Configuration.DisableMiniMenu;
            bool useAlternative = Service.Configuration.UseAlternativeRotation;

            ImGui.Separator();

            if (ImGui.CollapsingHeader("General Settings"))
            {
                if (ImGui.Checkbox("Automatic Action Execution Mode", ref autoEnabled))
                {
                    Service.Configuration.AutoMode = autoEnabled;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker($"Automatically use each recommended action.");
                if (autoEnabled)
                {
                    var delay = Service.Configuration.AutoDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("Execution Delay (ms)###ActionDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        Service.Configuration.AutoDelay = delay;
                        Service.Configuration.Save();
                    }
                }

                if (ImGui.Checkbox("Delay Getting Recommendations", ref delayRec))
                {
                    Service.Configuration.DelayRecommendation = delayRec;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("Use this if you're having issues with Final Appraisal not triggering when it's supposed to.");

                if (delayRec)
                {
                    var delay = Service.Configuration.RecommendationDelay;
                    ImGui.PushItemWidth(200);
                    if (ImGui.SliderInt("Set Delay (ms)###RecommendationDelay", ref delay, 0, 1000))
                    {
                        if (delay < 0) delay = 0;
                        if (delay > 1000) delay = 1000;

                        Service.Configuration.RecommendationDelay = delay;
                        Service.Configuration.Save();
                    }
                }

                bool requestStop = Service.Configuration.RequestToStopDuty;
                bool requestResume = Service.Configuration.RequestToResumeDuty;
                int resumeDelay = Service.Configuration.RequestToResumeDelay;

                if (ImGui.Checkbox("Have Artisan turn off Endurance / pause lists when Duty Finder is ready", ref requestStop))
                {
                    Service.Configuration.RequestToStopDuty = requestStop;
                    Service.Configuration.Save();
                }

                if (requestStop)
                {
                    if (ImGui.Checkbox("Have Artisan resume Endurance / unpause lists after leaving Duty", ref requestResume))
                    {
                        Service.Configuration.RequestToResumeDuty = requestResume;
                        Service.Configuration.Save();
                    }

                    if (requestResume)
                    {
                        if (ImGui.SliderInt("Delay to resume (seconds)", ref resumeDelay, 5, 60))
                        {
                            Service.Configuration.RequestToResumeDelay = resumeDelay;
                        }
                    }
                }
            }

            if (ImGui.CollapsingHeader("Macro Settings"))
            {
                if (ImGui.Checkbox("Skip Macro Steps if Unable To Use Action", ref Service.Configuration.SkipMacroStepIfUnable))
                    Service.Configuration.Save();

                if (ImGui.Checkbox($"Prevent Artisan from Continuing After Macro Finishes", ref Service.Configuration.DisableMacroArtisanRecommendation))
                    Service.Configuration.Save();
            }
            if (ImGui.CollapsingHeader("Solver Settings"))
            {
                if (ImGui.Checkbox($"Use {LuminaSheets.CraftActions[Skills.Tricks].Name} - {LuminaSheets.AddonSheet[227].Text.RawString}", ref useTricksGood))
                {
                    Service.Configuration.UseTricksGood = useTricksGood;
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                if (ImGui.Checkbox($"Use {LuminaSheets.CraftActions[Skills.Tricks].Name} - {LuminaSheets.AddonSheet[228].Text.RawString}", ref useTricksExcellent))
                {
                    Service.Configuration.UseTricksExcellent = useTricksExcellent;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker($"These 2 options allow you to make Tricks of the Trade a priority when condition is Good or Excellent.\nOther skills that rely on these conditions will not be used.");
                if (ImGui.Checkbox("Use Specialist Actions", ref useSpecialist))
                {
                    Service.Configuration.UseSpecialist = useSpecialist;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("If the current job is a specialist, spends any Crafter's Delineation you may have.\nCareful Observation replaces Observe.");
                ImGui.TextWrapped("Max Quality%%");
                ImGuiComponents.HelpMarker($"Once quality has reached the below percentage, Artisan will focus on progress only.");
                if (ImGui.SliderInt("###SliderMaxQuality", ref maxQuality, 0, 100, $"%d%%"))
                {
                    Service.Configuration.MaxPercentage = maxQuality;
                    Service.Configuration.Save();
                }

                if (ImGui.Checkbox("Use Alternative Quality Rotation (Level 84+)", ref useAlternative))
                {
                    Service.Configuration.UseAlternativeRotation = useAlternative;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("Switches to Basic -> Standard -> Advanced touch instead of highest level touch.");

                if (ImGui.Checkbox("Disable Automatically Equipping Required Items for Crafts", ref Service.Configuration.DontEquipItems))
                    Service.Configuration.Save();

            }

            if (ImGui.CollapsingHeader("UI Settings"))
            {
                if (ImGui.Checkbox("Disable highlighting box", ref disableGlow))
                {
                    Service.Configuration.DisableHighlightedAction = disableGlow;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("This is the box that highlights the actions on your hotbars for manual play.");

                if (ImGui.Checkbox($"Disable recommendation toasts", ref disableToasts))
                {
                    Service.Configuration.DisableToasts = disableToasts;
                    Service.Configuration.Save();
                }

                ImGuiComponents.HelpMarker("These are the pop-ups whenever a new action is recommended.");

                if (ImGui.Checkbox("Disable Recipe List mini-menu", ref disableMini))
                {
                    Service.Configuration.DisableMiniMenu = disableMini;
                    Service.Configuration.Save();
                }
                ImGuiComponents.HelpMarker("Hides the mini-menu for config settings in the recipe list. Still shows individual macro menu.");

                bool lockMini = Service.Configuration.LockMiniMenu;
                if (ImGui.Checkbox("Keep Recipe List mini-menu position attached to Recipe List.", ref lockMini))
                {
                    Service.Configuration.LockMiniMenu = lockMini;
                    Service.Configuration.Save();
                }
                if (ImGui.Button("Reset Recipe List mini-menu position"))
                {
                    AtkResNodeFunctions.ResetPosition = true;
                }

                bool hideQuestHelper = Service.Configuration.HideQuestHelper;
                if (ImGui.Checkbox($"Hide Quest Helper", ref hideQuestHelper))
                {
                    Service.Configuration.HideQuestHelper = hideQuestHelper;
                    Service.Configuration.Save();
                }

                bool hideTheme = Service.Configuration.DisableTheme;
                if (ImGui.Checkbox("Disable Custom Theme", ref hideTheme))
                {
                    Service.Configuration.DisableTheme = hideTheme;
                    Service.Configuration.Save();
                }
                ImGui.SameLine();
                if (IconButtons.IconTextButton(FontAwesomeIcon.Clipboard, "Copy Theme"))
                {
                    ImGui.SetClipboardText("DS1H4sIAAAAAAAACq1YS3PbNhD+Kx2ePR6AeJG+xXYbH+KOJ3bHbW60REusaFGlKOXhyX/v4rEACEqumlY+ECD32/cuFn7NquyCnpOz7Cm7eM1+zy5yvfnDPL+fZTP4at7MHVntyMi5MGTwBLJn+HqWLZB46Ygbx64C5kQv/nRo8xXQ3AhZZRdCv2jdhxdHxUeqrJO3Ftslb5l5u/Fa2rfEvP0LWBkBPQiSerF1Cg7wApBn2c5wOMv2juNn9/zieH09aP63g+Kqyr1mI91mHdj5mj3UX4bEG+b5yT0fzRPoNeF1s62e2np+EuCxWc+7z5cLr1SuuCBlkTvdqBCEKmaQxCHJeZmXnFKlgMHVsmnnEZ5IyXMiFUfjwt6yCHvDSitx1212m4gHV0QURY4saMEYl6Q4rsRl18/rPuCZQ+rFJxeARwyAJb5fVmD4NBaJEK3eL331UscuAgflOcY0J5zLUioHpHmhCC0lCuSBwU23r3sfF/0N0wKdoxcGFqHezYZmHypJIkgiSCJIalc8NEM7Utb6ErWlwngt9aUoFRWSB3wilRUl5SRwISUFvhJt9lvDrMgLIjgLzK66tq0228j0H+R3W693l1UfmUd9kqA79MKn9/2sB9lPI8hbofb073vdh1BbQYRgqKzfGbTfTWVqHmnMOcXUpI6BXhzGJjEQCNULmy4x9GpZz1a3Vb8KqaIDz4RPVGZin6dlZPKDSS29baAyRqYfzVGnr0ekaaowTbEw9MLjLnfD0GGT1unHSSlKr2lRyqLA2qU5ESovi6m+lkvqYiZ1/ygxyqrgjDKF8Yr2lp1pd4R7dokhvOBUQk37TCVKQbX4TMVtyuymruKWJCURVEofClYWbNpWCQfFifDwsWnYyXXS8ZxDOI+H0uLToPzrhKg3VV8N3amt1dP/t5goW/E85pg2pB8N8sd623yr3/dNOPYVstELg9cLA8zFCJKapQpEYkPVi9CMA/L/Uv8hrk1hmg9WKKMQXyIxnGFrm6i06MkhBHlIiQ8rI0xx4k/rsLWBsWpbTmmhqFIypcvUHTRgQ859V/bbKaPf1s/dbBcfD0R6NnCWwg/dS3lB4MfQMSrnCY9EK8qEw9uUl4YdHjRQRVFTuu5mq2a9uOvrfVOH0SDHqtXxMjDfi1RA/fyyGb7G5y5KdJg8EnTXdsOHZl1vQyJJQrlCQTDsEBi80HdhO+VwrEP48hwdTRp202yHbgGzhRfu03/UCA4gjglDd44mUT2D2i4UH9coSy8mfjEYN54NfbcOOIZnn15M7YqAH5rFEmdl3eJ8r0N5E9zH0fz71nQQyN+1/zSP6yR2A/l93dazoY6n5DdyiumWc91Xi+u+2zxU/aI+Jipq2QD5tdrfgO3t2P5jcqz9gLEXAEjgFHzcMJUgr5uXyDQsNSxZtCvX81s3r1qLOw0EztC3ORiEs4vssu9W9fqn2263HqpmncFF016PqklGjh1kjQ2NUyUJH08mcIk9gSrqn+jg0XFoqeqTrmDPwQv+PDEr6wl3oljaxcRSRTCyMc/lJJ/lAcnNhMr3WWZ+ES3exrXE+HJ2yNOrowkb97A2cExdXcrYjaFToVDfGSMqnCaDa0pi/vzNMyLG/wQEyzmzfhx7KAwJUn93Fz6v5shD8B+DRAG4Oh+QHYapovAd3/OEQzuiDSdE4c8wjJHh7iiBFFozvP3+NxT8RWGlEQAA");
                    Notify.Success("Theme copied to clipboard");
                }

                if (ImGui.Checkbox("Disable Allagan Tools Integration With Lists", ref Service.Configuration.DisableAllaganTools))
                    Service.Configuration.Save();

            }
            if (ImGui.CollapsingHeader("List Defaults"))
            {
                ImGui.TextWrapped($"These settings will automatically be applied when creating a crafting list.");

                if (ImGui.Checkbox("Skip items you already have enough of", ref Service.Configuration.DefaultListSkip))
                {
                    Service.Configuration.Save();
                }

                if (ImGui.Checkbox("Automatically Extract Materia", ref Service.Configuration.DefaultListMateria))
                {
                    Service.Configuration.Save();
                }

                if (ImGui.Checkbox("Automatic Repairs", ref Service.Configuration.DefaultListRepair))
                {
                    Service.Configuration.Save();
                }

                if (Service.Configuration.DefaultListRepair)
                {
                    ImGui.TextWrapped($"Repair at");
                    ImGui.SameLine();
                    if (ImGui.SliderInt("###SliderRepairDefault", ref Service.Configuration.DefaultListRepairPercent, 0, 100, $"%d%%"))
                    {
                        Service.Configuration.Save();
                    }
                }

                if (ImGui.Checkbox("Set new items added to list as quick synth", ref Service.Configuration.DefaultListQuickSynth))
                {
                    Service.Configuration.Save();
                }

                if (ImGui.Checkbox($@"Reset ""Number of Times to Add"" after adding to list.", ref Service.Configuration.ResetTimesToAdd))
                    Service.Configuration.Save();
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
    }
}
