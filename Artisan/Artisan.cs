using Artisan.Autocraft;
using Artisan.ContextMenus;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.CustomDeliveries;
using Artisan.IPC;
using Artisan.MacroSystem;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using OtterGui.Classes;
using PunishLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Artisan.CraftingLogic.CurrentCraft;
using Macro = Artisan.MacroSystem.Macro;
using PluginLog = Dalamud.Logging.PluginLog;

namespace Artisan;

public unsafe class Artisan : IDalamudPlugin
{
    public string Name => "Artisan";
    private const string commandName = "/artisan";
    internal static Artisan P;
    internal DalamudPluginInterface pi;
    internal PluginUI PluginUi;
    internal WindowSystem ws;
    internal Configuration config;
    internal CraftingWindow cw;
    internal RecipeInformation ri;
    internal TaskManager TM;
    internal TaskManager CTM;
    internal IconStorage Icons;

    public static bool currentCraftFinished = false;
    public static readonly object _lockObj = new();
    public static List<Task> Tasks = new();
    public static bool warningMessage = false;
    public static bool macroWarning = false;
    public static bool brokenWarning = false;

    internal FontManager fm;
    internal StyleModel Style;
    internal ImFontPtr CustomFont;
    internal ImFontPtr ScaledFont;
    internal bool StylePushed = false;

    public Artisan(DalamudPluginInterface pluginInterface)
    {
        pi = pluginInterface;
        pluginInterface.Create<Service>();
        Service.Plugin = this;

        Service.Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Service.Configuration.Initialize(Service.Interface);

        ECommonsMain.Init(pluginInterface, this, Module.All);
        PunishLibMain.Init(pluginInterface, this, new AboutPlugin() { Sponsor = "https://ko-fi.com/taurenkey" });

        TM = new();
        TM.TimeLimitMS = 1000;
        CTM = new();
#if !DEBUG
        TM.ShowDebug = false;
        CTM.ShowDebug = false;
#endif
        P = this;
        ws = new();
        cw = new();
        ri = new();
        Icons = new(pluginInterface, Svc.Data);
        PluginUi = new();
        config = Service.Configuration;
        fm = new FontManager();
        Service.CommandManager.AddHandler(commandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Artisan menu.",
            ShowInHelp = true,
        });

        TransitionMacros();
        CleanUpIndividualMacros();
        Svc.PluginInterface.UiBuilder.BuildFonts += AddCustomFont;
        Svc.PluginInterface.UiBuilder.RebuildFonts();
        Service.Interface.UiBuilder.Draw += ws.Draw;
        Service.Interface.UiBuilder.OpenConfigUi += DrawConfigUI;
        Service.Condition.ConditionChange += CheckForCraftedState;
        Service.Framework.Update += FireBot;
        Service.ClientState.Logout += DisableEndurance;
        Service.ClientState.Login += DisableEndurance;
        Service.Condition.ConditionChange += Condition_ConditionChange;
        Service.ChatGui.ChatMessage += ScanForHQItems;
        ActionWatching.Enable();
        StepChanged += ResetRecommendation;
        ConsumableChecker.Init();
        Endurance.Init();
        IPC.IPC.Init();
        RetainerInfo.Init();
        CraftingListContextMenu.Init();

        ws.AddWindow(new RecipeWindowUI());
        ws.AddWindow(new ProcessingWindow());
        ws.AddWindow(new QuestHelper());
        ws.AddWindow(cw);

        Style = StyleModel.Deserialize("DS1H4sIAAAAAAAACq1YS3PbNhD+Kx2ePR6AeJG+xXYbH+KOJ3bHbW60REusaFGlKOXhyX/v4rEACEqumlY+ECD32/cuFn7NquyCnpOz7Cm7eM1+zy5yvfnDPL+fZTP4at7MHVntyMi5MGTwBLJn+HqWLZB46Ygbx64C5kQv/nRo8xXQ3AhZZRdCv2jdhxdHxUeqrJO3Ftslb5l5u/Fa2rfEvP0LWBkBPQiSerF1Cg7wApBn2c5wOMv2juNn9/zieH09aP63g+Kqyr1mI91mHdj5mj3UX4bEG+b5yT0fzRPoNeF1s62e2np+EuCxWc+7z5cLr1SuuCBlkTvdqBCEKmaQxCHJeZmXnFKlgMHVsmnnEZ5IyXMiFUfjwt6yCHvDSitx1212m4gHV0QURY4saMEYl6Q4rsRl18/rPuCZQ+rFJxeARwyAJb5fVmD4NBaJEK3eL331UscuAgflOcY0J5zLUioHpHmhCC0lCuSBwU23r3sfF/0N0wKdoxcGFqHezYZmHypJIkgiSCJIalc8NEM7Utb6ErWlwngt9aUoFRWSB3wilRUl5SRwISUFvhJt9lvDrMgLIjgLzK66tq0228j0H+R3W693l1UfmUd9kqA79MKn9/2sB9lPI8hbofb073vdh1BbQYRgqKzfGbTfTWVqHmnMOcXUpI6BXhzGJjEQCNULmy4x9GpZz1a3Vb8KqaIDz4RPVGZin6dlZPKDSS29baAyRqYfzVGnr0ekaaowTbEw9MLjLnfD0GGT1unHSSlKr2lRyqLA2qU5ESovi6m+lkvqYiZ1/ygxyqrgjDKF8Yr2lp1pd4R7dokhvOBUQk37TCVKQbX4TMVtyuymruKWJCURVEofClYWbNpWCQfFifDwsWnYyXXS8ZxDOI+H0uLToPzrhKg3VV8N3amt1dP/t5goW/E85pg2pB8N8sd623yr3/dNOPYVstELg9cLA8zFCJKapQpEYkPVi9CMA/L/Uv8hrk1hmg9WKKMQXyIxnGFrm6i06MkhBHlIiQ8rI0xx4k/rsLWBsWpbTmmhqFIypcvUHTRgQ859V/bbKaPf1s/dbBcfD0R6NnCWwg/dS3lB4MfQMSrnCY9EK8qEw9uUl4YdHjRQRVFTuu5mq2a9uOvrfVOH0SDHqtXxMjDfi1RA/fyyGb7G5y5KdJg8EnTXdsOHZl1vQyJJQrlCQTDsEBi80HdhO+VwrEP48hwdTRp202yHbgGzhRfu03/UCA4gjglDd44mUT2D2i4UH9coSy8mfjEYN54NfbcOOIZnn15M7YqAH5rFEmdl3eJ8r0N5E9zH0fz71nQQyN+1/zSP6yR2A/l93dazoY6n5DdyiumWc91Xi+u+2zxU/aI+Jipq2QD5tdrfgO3t2P5jcqz9gLEXAEjgFHzcMJUgr5uXyDQsNSxZtCvX81s3r1qLOw0EztC3ORiEs4vssu9W9fqn2263HqpmncFF016PqklGjh1kjQ2NUyUJH08mcIk9gSrqn+jg0XFoqeqTrmDPwQv+PDEr6wl3oljaxcRSRTCyMc/lJJ/lAcnNhMr3WWZ+ES3exrXE+HJ2yNOrowkb97A2cExdXcrYjaFToVDfGSMqnCaDa0pi/vzNMyLG/wQEyzmzfhx7KAwJUn93Fz6v5shD8B+DRAG4Oh+QHYapovAd3/OEQzuiDSdE4c8wjJHh7iiBFFozvP3+NxT8RWGlEQAA")!;
    }

    private void AddCustomFont()
    {
        if (Svc.ClientState.ClientLanguage == Dalamud.ClientLanguage.Japanese) return;

        PluginLog.Debug("Adding custom font");
        string path = Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName!, "Fonts", "CaviarDreams_Bold.ttf");
        if (File.Exists(path))
        {
            CustomFont = fm.CustomFont.Value;
        }

    }
    private void ScanForHQItems(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (type == (XivChatType)2242 && Service.Condition[ConditionFlag.Crafting])
        {
            if (message.Payloads.Any(x => x.Type == PayloadType.Item))
            {
                var item = (ItemPayload)message.Payloads.First(x => x.Type == PayloadType.Item);
                if (item.Item.CanBeHq)
                    LastItemWasHQ = item.IsHQ;

                LastCraftedItem = item.Item;
            }
        }
    }

    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {
        Endurance.Tasks.Clear();

        if (Service.Configuration.RequestToStopDuty)
        {
            if (flag == ConditionFlag.WaitingForDutyFinder && value)
            {
                IPC.IPC.StopCraftingRequest = true;
            }

            if (flag == ConditionFlag.WaitingForDutyFinder && !value)
            {
                IPC.IPC.StopCraftingRequest = false;
            }

            if (flag == ConditionFlag.BoundByDuty && !value && IPC.IPC.StopCraftingRequest && Service.Configuration.RequestToResumeDuty)
            {
                var resumeDelay = Service.Configuration.RequestToResumeDelay;
                Svc.Framework.RunOnTick(() => { IPC.IPC.StopCraftingRequest = false; }, TimeSpan.FromSeconds(resumeDelay));
            }
        }


        if (Service.Condition[ConditionFlag.PreparingToCraft])
        {
            State = CraftingState.PreparingToCraft;
            if (IPC.IPC.StopCraftingRequest)
            {
                Svc.Framework.RunOnTick(CraftingListFunctions.CloseCraftingMenu, TimeSpan.FromSeconds(1));
            }
            return;
        }
        if (Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.PreparingToCraft])
        {
            State = CraftingState.Crafting;
            return;
        }
        if (!Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.PreparingToCraft])
        {
            State = CraftingState.NotCrafting;
            return;
        }
    }

    private void DisableEndurance(object? sender, EventArgs e)
    {
        Endurance.Enable = false;
        CraftingListUI.Processing = false;
    }

    private void ResetRecommendation(object? sender, int e)
    {
        CurrentRecommendation = 0;
        if (e == 0)
        {
            ManipulationUsed = false;
            JustUsedObserve = false;
            VenerationUsed = false;
            InnovationUsed = false;
            WasteNotUsed = false;
            JustUsedFinalAppraisal = false;
            BasicTouchUsed = false;
            StandardTouchUsed = false;
            AdvancedTouchUsed = false;
            ExpertCraftOpenerFinish = false;
            MacroStep = 0;
            warningMessage = false;
            macroWarning = false;
            brokenWarning = false;
        }
        if (e > 0)
            Tasks.Clear();
    }

    public static bool CheckIfCraftFinished()
    {
        //if (QuickSynthMax > 0 && QuickSynthCurrent == QuickSynthMax) return true;
        if (MaxProgress == 0) return false;
        if (CurrentProgress == MaxProgress) return true;
        if (CurrentProgress < MaxProgress && CurrentDurability == 0) return true;
        currentCraftFinished = false;
        return false;
    }

    private void FireBot(Framework framework)
    {
        if (CraftingWindow.MacroTime.Ticks > 0)
            CraftingWindow.MacroTime -= framework.UpdateDelta;

        if (!Service.ClientState.IsLoggedIn)
        {
            Endurance.Enable = false;
            CraftingListUI.Processing = false;
        }
        PluginUi.CraftingVisible = Service.Condition[ConditionFlag.Crafting] && !Service.Condition[ConditionFlag.PreparingToCraft];
        if (!PluginUi.CraftingVisible)
            ActionWatching.TryDisable();
        else
            ActionWatching.TryEnable();

        if (!Endurance.Enable)
            Endurance.DrawRecipeData();

        GetCraft();
        if (CurrentCraftMethods.CanUse(Skills.BasicSynth) && CurrentRecommendation == 0 && Tasks.Count == 0 && CurrentStep >= 1)
        {
            if (CurrentRecipe is null && !warningMessage)
            {
                DuoLog.Error("Warning: Your recipe cannot be parsed in Artisan. Please report this to the Discord with the recipe name and client language.");
                warningMessage = true;
            }
            else
            {
                warningMessage = false;
            }

            if (warningMessage)
                return;

            var delay = Service.Configuration.DelayRecommendation ? Service.Configuration.RecommendationDelay : 0;
            Tasks.Add(Service.Framework.RunOnTick(() => FetchRecommendation(CurrentStep), TimeSpan.FromMilliseconds(delay)));
        }

        if (CheckIfCraftFinished() && !currentCraftFinished)
        {
            currentCraftFinished = true;


            if (CraftingListUI.Processing && !CraftingListFunctions.Paused)
            {
                Dalamud.Logging.PluginLog.Verbose("Advancing Crafting List");
                CraftingListFunctions.CurrentIndex++;
            }


            if (Endurance.Enable && Service.Configuration.CraftingX && Service.Configuration.CraftX > 0)
            {
                Service.Configuration.CraftX -= 1;
                if (Service.Configuration.CraftX == 0)
                {
                    Service.Configuration.CraftingX = false;
                    Endurance.Enable = false;
                    DuoLog.Information("Craft X has completed.");

                }
            }
        }

        if (cw.repeatTrial && !Endurance.Enable)
        {
            CurrentCraftMethods.RepeatTrialCraft();
        }

    }

    public static void FetchRecommendation(int e)
    {
        if (Tasks.Count > 1)
            return;

        try
        {
            if (RepairManager.GetMinEquippedPercent() == 0)
            {
                if (!brokenWarning)
                    Svc.Chat.PrintError("You have broken gear. Artisan will not continue.");

                brokenWarning = true;
                return;
            }

            if (Service.Configuration.UserMacros.Count > 0)
            {
                if (MacroFunctions.GetMacro(AgentRecipeNote.Instance()->ActiveCraftRecipeId, out var macro))
                {
                    if (macro.MacroOptions.MinCraftsmanship > (int)CharacterInfo.Craftsmanship() ||
                        macro.MacroOptions.MinControl > (int)CharacterInfo.Control() ||
                        macro.MacroOptions.MinCP > (int)CharacterInfo.MaxCP)
                    {
                        if (!macroWarning)
                            Svc.Chat.PrintError("You do not meet the minimum stats for this macro. Artisan will not continue.");

                        macroWarning = true;
                        return;
                    }

                RestartRecommendation:
                    if (MacroStep < macro.MacroActions.Count)
                    {
                        if (macro.MacroOptions.SkipQualityIfMet)
                        {
                            if (CurrentQuality >= MaxQuality)
                            {
                                if (ActionIsQuality(macro) && (!Service.Configuration.SkipMacroStepIfUnable || (Service.Configuration.SkipMacroStepIfUnable && CurrentCraftMethods.CanUse(macro.MacroActions[MacroStep]))))
                                {
                                    MacroStep++;
                                    goto RestartRecommendation;
                                }
                            }
                        }

                        if (macro.MacroOptions.SkipObservesIfNotPoor && CurrentCondition != CraftingLogic.CraftData.Condition.Poor)
                        {
                            if (macro.MacroActions[MacroStep] == Skills.Observe || macro.MacroActions[MacroStep] == Skills.CarefulObservation)
                            {
                                MacroStep++;
                                goto RestartRecommendation;
                            }
                        }

                        if (Service.Configuration.SkipMacroStepIfUnable)
                        {
                            if (!CurrentCraftMethods.CanUse(macro.MacroActions[MacroStep]))
                            {
                                MacroStep++;
                                goto RestartRecommendation;
                            }
                        }

                        if ((macro.MacroStepOptions[MacroStep].ExcludeNormal && CurrentCondition == CraftingLogic.CraftData.Condition.Normal) ||
                            (macro.MacroStepOptions[MacroStep].ExcludeGood && CurrentCondition == CraftingLogic.CraftData.Condition.Good) ||
                            (macro.MacroStepOptions[MacroStep].ExcludePoor && CurrentCondition == CraftingLogic.CraftData.Condition.Poor) ||
                            (macro.MacroStepOptions[MacroStep].ExcludeExcellent && CurrentCondition == CraftingLogic.CraftData.Condition.Excellent) ||
                            (macro.MacroStepOptions[MacroStep].ExcludeCentered && CurrentCondition == CraftingLogic.CraftData.Condition.Centered) ||
                            (macro.MacroStepOptions[MacroStep].ExcludeSturdy && CurrentCondition == CraftingLogic.CraftData.Condition.Sturdy) ||
                            (macro.MacroStepOptions[MacroStep].ExcludePliant && CurrentCondition == CraftingLogic.CraftData.Condition.Pliant) ||
                            (macro.MacroStepOptions[MacroStep].ExcludeMalleable && CurrentCondition == CraftingLogic.CraftData.Condition.Malleable) ||
                            (macro.MacroStepOptions[MacroStep].ExcludePrimed && CurrentCondition == CraftingLogic.CraftData.Condition.Primed) ||
                            (macro.MacroStepOptions[MacroStep].ExcludeGoodOmen && CurrentCondition == CraftingLogic.CraftData.Condition.GoodOmen))
                        {
                            MacroStep++;
                            goto RestartRecommendation;
                        }

                        CurrentRecommendation = MacroStep >= macro.MacroActions.Count() || macro.MacroActions[MacroStep] == 0 ? (CurrentRecipe.IsExpert ? CurrentCraftMethods.GetExpertRecommendation() : CurrentCraftMethods.GetRecommendation()) : macro.MacroActions[MacroStep];

                        try
                        {
                            if (MacroStep < macro.MacroStepOptions.Count())
                            {
                                if (macro.MacroStepOptions.Count == 0 || !macro.MacroStepOptions[MacroStep].ExcludeFromUpgrade)
                                {
                                    if (macro.MacroOptions.UpgradeQualityActions && ActionIsQuality(macro) && ActionUpgradable(macro, out uint newAction))
                                    {
                                        CurrentRecommendation = newAction;
                                    }
                                    if (macro.MacroOptions.UpgradeProgressActions && !ActionIsQuality(macro) && ActionUpgradable(macro, out newAction))
                                    {
                                        CurrentRecommendation = newAction;
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { ex.Log(); }
                    }
                    else
                    {
                        if (!Service.Configuration.DisableMacroArtisanRecommendation || CraftingListUI.Processing)
                            CurrentRecommendation = CurrentRecipe.IsExpert ? CurrentCraftMethods.GetExpertRecommendation() : CurrentCraftMethods.GetRecommendation();
                    }
                }
                else
                {
                    CurrentRecommendation = CurrentRecipe.IsExpert ? CurrentCraftMethods.GetExpertRecommendation() : CurrentCraftMethods.GetRecommendation();
                }
            }
            else
            {
                CurrentRecommendation = CurrentRecipe.IsExpert ? CurrentCraftMethods.GetExpertRecommendation() : CurrentCraftMethods.GetRecommendation();
            }

            if (CurrentRecommendation != 0)
            {
                RecommendationName = CurrentRecommendation.NameOfAction();

                if (LuminaSheets.ActionSheet.TryGetValue(CurrentRecommendation, out var normalAct))
                {
                    if (normalAct.ClassJob.Value.RowId != CharacterInfo.JobID())
                    {
                        var newAct = LuminaSheets.ActionSheet.Values.Where(x => x.Name.RawString == normalAct.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                        CurrentRecommendation = newAct.RowId;
                        if (!Service.Configuration.DisableToasts)
                        {
                            QuestToastOptions options = new() { IconId = newAct.Icon };
                            Service.ToastGui.ShowQuest($"Use {newAct.Name}", options);
                        }

                    }
                    else
                    {
                        if (!Service.Configuration.DisableToasts)
                        {
                            QuestToastOptions options = new() { IconId = normalAct.Icon };
                            Service.ToastGui.ShowQuest($"Use {normalAct.Name}", options);
                        }
                    }
                }

                if (LuminaSheets.CraftActions.TryGetValue(CurrentRecommendation, out var craftAction))
                {
                    if (craftAction.ClassJob.Row != CharacterInfo.JobID())
                    {
                        var newAct = LuminaSheets.CraftActions.Values.Where(x => x.Name.RawString == craftAction.Name.RawString && x.ClassJob.Row == CharacterInfo.JobID()).FirstOrDefault();
                        CurrentRecommendation = newAct.RowId;
                        if (!Service.Configuration.DisableToasts)
                        {
                            QuestToastOptions options = new() { IconId = newAct.Icon };
                            Service.ToastGui.ShowQuest($"Use {newAct.Name}", options);
                        }
                    }
                    else
                    {
                        if (!Service.Configuration.DisableToasts)
                        {
                            QuestToastOptions options = new() { IconId = craftAction.Icon };
                            Service.ToastGui.ShowQuest($"Use {craftAction.Name}", options);
                        }
                    }
                }

                if (Service.Configuration.AutoMode)
                {
                    if (CurrentCraftMethods.CanUse(CurrentRecommendation))
                    ActionWatching.BlockAction = true;

                    P.CTM.DelayNext(Service.Configuration.AutoDelay);
                    P.CTM.Enqueue(() => Hotbars.ExecuteRecommended(CurrentRecommendation));
                    //Service.Framework.RunOnTick(() => , TimeSpan.FromMilliseconds(Service.Configuration.AutoDelay));

                    //Service.Plugin.BotTask.Schedule(() => Hotbars.ExecuteRecommended(CurrentRecommendation), Service.Configuration.AutoDelay);
                }

                return;
            }
        }
        catch (Exception ex)
        {
            Dalamud.Logging.PluginLog.Error(ex, "Crafting Step Change");
        }
    }

    private static bool ActionUpgradable(Macro macro, out uint newAction)
    {
        newAction = macro.MacroActions[MacroStep];
        if (CurrentCondition is CraftingLogic.CraftData.Condition.Good or CraftingLogic.CraftData.Condition.Excellent)
        {
            switch (newAction)
            {
                case Skills.FocusedSynthesis:
                case Skills.Groundwork:
                case Skills.PrudentSynthesis:
                case Skills.CarefulSynthesis:
                case Skills.BasicSynth:
                    newAction = Skills.IntensiveSynthesis;
                    break;
                case Skills.HastyTouch:
                case Skills.FocusedTouch:
                case Skills.PreparatoryTouch:
                case Skills.AdvancedTouch:
                case Skills.StandardTouch:
                case Skills.BasicTouch:
                    newAction = Skills.PreciseTouch;
                    break;
            }

            return CurrentCraftMethods.CanUse(newAction);
        }

        return false;
    }

    public static bool ActionIsQuality(Macro macro)
    {
        var currentAction = macro.MacroActions[MacroStep];
        switch (currentAction)
        {
            case Skills.HastyTouch:
            case Skills.FocusedTouch:
            case Skills.PreparatoryTouch:
            case Skills.AdvancedTouch:
            case Skills.StandardTouch:
            case Skills.BasicTouch:
            case Skills.GreatStrides:
            case Skills.Innovation:
            case Skills.ByregotsBlessing:
            case Skills.TrainedFinesse:
                return true;

            default:
                return false;
        }
    }

    private void CheckForCraftedState(ConditionFlag flag, bool value)
    {
        if (flag == ConditionFlag.Crafting && value)
        {
            PluginUi.CraftingVisible = true;
        }
    }

    public void Dispose()
    {
        PluginUi.Dispose();
        Endurance.Dispose();
        RetainerInfo.Dispose();
        IPC.IPC.Dispose();

        Service.CommandManager.RemoveHandler(commandName);
        Service.Condition.ConditionChange -= Condition_ConditionChange;
        Service.ChatGui.ChatMessage -= ScanForHQItems;
        Service.Interface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        Service.Interface.UiBuilder.Draw -= ws.Draw;
        Service.Framework.Update -= FireBot;
        StepChanged -= ResetRecommendation;

        Svc.PluginInterface.UiBuilder.BuildFonts -= AddCustomFont;
        ActionWatching.Dispose();
        SatisfactionManagerHelper.Dispose();
        Service.Plugin = null!;
        ws.RemoveAllWindows();
        ws = null!;
        ECommonsMain.Dispose();
        CustomFont = null;
        LuminaSheets.Dispose();
        CraftingListContextMenu.Dispose();
        P = null;

    }

    private void OnCommand(string command, string args)
    {
        PluginUi.IsOpen = !PluginUi.IsOpen;
    }

    private void DrawConfigUI()
    {
        PluginUi.IsOpen = true;
    }

    internal static void StopCrafting()
    {
        SetMode();

        switch (IPC.IPC.CurrentMode)
        {
            case IPC.IPC.ArtisanMode.Endurance:
                Endurance.Enable = false;
                break;
            case IPC.IPC.ArtisanMode.Lists:
                CraftingListFunctions.Paused = true;
                break;
        }


    }

    private static void SetMode()
    {
        if (Endurance.Enable)
        {
            IPC.IPC.CurrentMode = IPC.IPC.ArtisanMode.Endurance;
            return;
        }

        if (CraftingListUI.Processing)
        {
            IPC.IPC.CurrentMode = IPC.IPC.ArtisanMode.Lists;
            return;
        }

        IPC.IPC.CurrentMode = IPC.IPC.ArtisanMode.None;
    }

    internal static void ResumeCrafting()
    {
        switch (IPC.IPC.CurrentMode)
        {
            case IPC.IPC.ArtisanMode.Endurance:
                Endurance.Enable = true;
                break;
            case IPC.IPC.ArtisanMode.Lists:
                CraftingListFunctions.Paused = false;
                break;
        }
    }
}

