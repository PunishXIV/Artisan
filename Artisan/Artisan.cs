using Artisan.Autocraft;
using Artisan.ContextMenus;
using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.IPC;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.UI;
using Artisan.Universalis;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Style;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.DalamudServices;
using ECommons.Logging;
using OtterGui.Classes;
using PunishLib;
using System;
using System.Linq;

namespace Artisan;

public unsafe class Artisan : IDalamudPlugin
{
    public string Name => "Artisan";
    private const string commandName = "/artisan";
    internal static Artisan P = null!;
    internal PluginUI PluginUi;
    internal WindowSystem ws;
    internal Configuration Config;
    internal CraftingWindow cw;
    internal RecipeInformation ri;
    internal TaskManager TM;
    internal TaskManager CTM;
    internal TextureCache Icons;
    internal UniversalisClient UniversalsisClient;

    internal StyleModel Style;
    internal bool StylePushed = false;

    public Artisan(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this, Module.All);
        PunishLibMain.Init(pluginInterface, "Artisan", new AboutPlugin() { Sponsor = "https://ko-fi.com/taurenkey" });
        P = this;

        LuminaSheets.Init();
        P.Config = Configuration.Load();
        //P.Config.ScriptSolverConfig.Init();

        TM = new();
        CTM = new();
#if !DEBUG
        TM.ShowDebug = false;
        CTM.ShowDebug = false;
#endif
        ws = new();
        ri = new();
        Icons = new(Svc.Data, Svc.Texture, Svc.Log);
        Config = P.Config;
        PluginUi = new();

        Svc.Commands.AddHandler(commandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Artisan menu.\n" +
            "/artisan lists → Open Lists.\n" +
            "/artisan lists <ID> → Opens specific list by ID.\n" +
            "/artisan lists <ID> start → Starts specific list by ID.\n" +
            "/artisan macros → Open Macros.\n" +
            "/artisan macros <ID> → Opens specific macro by ID.\n" +
            "/artisan endurance → Open Endurance.\n" +
            "/artisan endurance start|stop → Starts or stops endurance mode.\n" +
            "/artisan settings → Open Settings.\n" +
            "/artisan workshops → Open FC Workshops.\n" +
            "/artisan builder → Open List Builder.\n" +
            "/artisan automode → Toggles Automatic Action Execution Mode on/off.",
            ShowInHelp = true,
        });

        Svc.PluginInterface.UiBuilder.Draw += ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        Svc.PluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;

        Style = StyleModel.Deserialize("DS1H4sIAAAAAAAACq1YS3PbNhD+Kx2ePR6AeJG+xXYbH+KOJ3bHbW60REusaFGlKOXhyX/v4rEACEqumlY+ECD32/cuFn7NquyCnpOz7Cm7eM1+zy5yvfnDPL+fZTP4at7MHVntyMi5MGTwBLJn+HqWLZB46Ygbx64C5kQv/nRo8xXQ3AhZZRdCv2jdhxdHxUeqrJO3Ftslb5l5u/Fa2rfEvP0LWBkBPQiSerF1Cg7wApBn2c5wOMv2juNn9/zieH09aP63g+Kqyr1mI91mHdj5mj3UX4bEG+b5yT0fzRPoNeF1s62e2np+EuCxWc+7z5cLr1SuuCBlkTvdqBCEKmaQxCHJeZmXnFKlgMHVsmnnEZ5IyXMiFUfjwt6yCHvDSitx1212m4gHV0QURY4saMEYl6Q4rsRl18/rPuCZQ+rFJxeARwyAJb5fVmD4NBaJEK3eL331UscuAgflOcY0J5zLUioHpHmhCC0lCuSBwU23r3sfF/0N0wKdoxcGFqHezYZmHypJIkgiSCJIalc8NEM7Utb6ErWlwngt9aUoFRWSB3wilRUl5SRwISUFvhJt9lvDrMgLIjgLzK66tq0228j0H+R3W693l1UfmUd9kqA79MKn9/2sB9lPI8hbofb073vdh1BbQYRgqKzfGbTfTWVqHmnMOcXUpI6BXhzGJjEQCNULmy4x9GpZz1a3Vb8KqaIDz4RPVGZin6dlZPKDSS29baAyRqYfzVGnr0ekaaowTbEw9MLjLnfD0GGT1unHSSlKr2lRyqLA2qU5ESovi6m+lkvqYiZ1/ygxyqrgjDKF8Yr2lp1pd4R7dokhvOBUQk37TCVKQbX4TMVtyuymruKWJCURVEofClYWbNpWCQfFifDwsWnYyXXS8ZxDOI+H0uLToPzrhKg3VV8N3amt1dP/t5goW/E85pg2pB8N8sd623yr3/dNOPYVstELg9cLA8zFCJKapQpEYkPVi9CMA/L/Uv8hrk1hmg9WKKMQXyIxnGFrm6i06MkhBHlIiQ8rI0xx4k/rsLWBsWpbTmmhqFIypcvUHTRgQ859V/bbKaPf1s/dbBcfD0R6NnCWwg/dS3lB4MfQMSrnCY9EK8qEw9uUl4YdHjRQRVFTuu5mq2a9uOvrfVOH0SDHqtXxMjDfi1RA/fyyGb7G5y5KdJg8EnTXdsOHZl1vQyJJQrlCQTDsEBi80HdhO+VwrEP48hwdTRp202yHbgGzhRfu03/UCA4gjglDd44mUT2D2i4UH9coSy8mfjEYN54NfbcOOIZnn15M7YqAH5rFEmdl3eJ8r0N5E9zH0fz71nQQyN+1/zSP6yR2A/l93dazoY6n5DdyiumWc91Xi+u+2zxU/aI+Jipq2QD5tdrfgO3t2P5jcqz9gLEXAEjgFHzcMJUgr5uXyDQsNSxZtCvX81s3r1qLOw0EztC3ORiEs4vssu9W9fqn2263HqpmncFF016PqklGjh1kjQ2NUyUJH08mcIk9gSrqn+jg0XFoqeqTrmDPwQv+PDEr6wl3oljaxcRSRTCyMc/lJJ/lAcnNhMr3WWZ+ES3exrXE+HJ2yNOrowkb97A2cExdXcrYjaFToVDfGSMqnCaDa0pi/vzNMyLG/wQEyzmzfhx7KAwJUn93Fz6v5shD8B+DRAG4Oh+QHYapovAd3/OEQzuiDSdE4c8wjJHh7iiBFFozvP3+NxT8RWGlEQAA")!;
        CraftingProcessor.Setup();
        ConsumableChecker.Init();
        Endurance.Init();
        IPC.IPC.Init();
        RetainerInfo.Init();
        CraftingListContextMenu.Init();
        UniversalsisClient = new();

        EnduranceCraftWatcher.Setup();
        ws.AddWindow(new RecipeWindowUI());
        ws.AddWindow(new ProcessingWindow());
        ws.AddWindow(new QuestHelper());
        cw = new();
        ws.AddWindow(cw);

        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.ClientState.Logout += DisableEndurance;
        Svc.ClientState.Login += DisableEndurance;
        Svc.Condition.ConditionChange += Condition_ConditionChange;

        PluginUi.OpenWindow = OpenWindow.Main;

        ConvertCraftingLists();
    }

    private void DisableEndurance(int type, int code)
    {
        Endurance.ToggleEndurance(false);
        CraftingListUI.Processing = false;
    }

    private void ConvertCraftingLists()
    {
        foreach (var list in P.Config.CraftingLists)
        {
            if (!P.Config.NewCraftingLists.Any(x => x.ID == list.ID))
            {
                NewCraftingList nl = new()
                {
                    ID = list.ID,
                    Name = list.Name,
                    Materia = list.Materia,
                    AddAsQuickSynth = list.AddAsQuickSynth,
                    Repair = list.Repair,
                    RepairPercent = list.RepairPercent,
                    SkipIfEnough = list.SkipIfEnough,
                    SkipLiteral = list.SkipLiteral,
                };

                foreach (var item in list.Items.Distinct())
                {
                    ListItem listItem = new ListItem();
                    var qty = list.Items.Count(x => x == item);
                    listItem.ID = item;
                    listItem.Quantity = qty;
                    if (list.ListItemOptions.TryGetValue(item, out var opts))
                    {
                        listItem.ListItemOptions = opts;
                    }
                    nl.Recipes.Add(listItem);
                }

                P.Config.NewCraftingLists.Add(nl);
                P.Config.Save();
            }
        }

        P.Config.CraftingLists.Clear();
        P.Config.Save();
    }


    private void Condition_ConditionChange(ConditionFlag flag, bool value)
    {

        if (P.Config.RequestToStopDuty)
        {
            if (flag == ConditionFlag.WaitingForDutyFinder && value)
            {
                IPC.IPC.StopCraftingRequest = true;
                PreCrafting.Tasks.Clear();
                PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), default));
            }

            if (flag == ConditionFlag.BoundByDuty && !value && IPC.IPC.StopCraftingRequest && P.Config.RequestToResumeDuty)
            {
                var resumeDelay = P.Config.RequestToResumeDelay;
                Svc.Framework.RunOnTick(() => { IPC.IPC.StopCraftingRequest = false; }, TimeSpan.FromSeconds(resumeDelay));
            }
        }
    }

    private void DisableEndurance()
    {
        DisableEndurance(0, 0);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!Svc.ClientState.IsLoggedIn)
        {
            Endurance.ToggleEndurance(false);
            CraftingListUI.Processing = false;
            return;
        }

        CharacterInfo.UpdateCharaStats();
        Crafting.Update();
        SimpleTweaks.DisableImprovedLogTweak();
        PreCrafting.Update();
        Endurance.Update();

        if (cw.RepeatTrial && !Endurance.Enable)
        {
            Operations.RepeatTrialCraft();
        }

        PluginUi.CraftingVisible = Crafting.CurState is not Crafting.State.IdleNormal and not Crafting.State.IdleBetween;

        if (!Endurance.Enable)
            Endurance.DrawRecipeData();

        if (CraftingListUI.Processing && !CraftingListFunctions.Paused)
        {
            CraftingListFunctions.ListEndTime -= framework.UpdateDelta;
        }
    }

    public void Dispose()
    {
        PluginUi.Dispose();

        Svc.Commands.RemoveHandler(commandName);
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
        Svc.PluginInterface.UiBuilder.Draw -= ws.Draw;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= DrawConfigUI;
        cw?.Dispose();
        ri?.Dispose();
        ws?.RemoveAllWindows();
        ws = null!;

        //Config.ScriptSolverConfig?.Dispose();
        EnduranceCraftWatcher.Dispose();
        PreCrafting.Dispose();
        CraftingProcessor.Dispose();
        Crafting.Dispose();

        LuminaSheets.Dispose();

        CraftingListContextMenu.Dispose();
        UniversalsisClient.Dispose();

        Svc.Condition.ConditionChange -= Condition_ConditionChange;
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.ClientState.Logout -= DisableEndurance;
        Svc.ClientState.Login -= DisableEndurance;
        Endurance.Dispose();
        RetainerInfo.Dispose();
        IPC.IPC.Dispose();
        ECommonsMain.Dispose();
        P = null!;
    }

    private void OnCommand(string command, string args)
    {
        var subcommands = args.Split(' ');

        if (subcommands.Length == 0)
        {
            PluginUi.IsOpen = !PluginUi.IsOpen;
            return;
        }

        var firstArg = subcommands[0];

        if (firstArg.ToLower() == "automode")
        {
            P.Config.AutoMode = !P.Config.AutoMode;
            P.Config.Save();
            return;
        }
        if (subcommands.Length >= 2)
        {
            if (firstArg.ToLower() == "lists")
            {
                if (!CraftingListUI.Processing)
                {
                    if (int.TryParse(subcommands[1], out int id))
                    {
                        if (P.Config.NewCraftingLists.Any(x => x.ID == id))
                        {
                            if (subcommands.Length >= 3 && subcommands[2].ToLower() == "start")
                            {
                                if (!Endurance.Enable)
                                {
                                    CraftingListUI.selectedList = P.Config.NewCraftingLists.First(x => x.ID == id);
                                    CraftingListUI.StartList();
                                    return;
                                }
                            }
                            else
                            {
                                ListEditor editor = new(id);
                                return;
                            }
                        }
                        else
                        {
                            DuoLog.Error("List ID does not exist.");
                            return;
                        }
                    }
                    else
                    {
                        DuoLog.Error("Unable to parse ID as a number.");
                        return;
                    }
                }
                else
                {
                    DuoLog.Error("Unable to open list whilst processing.");
                    return;
                }
            }

            if (firstArg.ToLower() == "macros")
            {
                if (Crafting.CurState is Crafting.State.IdleNormal or Crafting.State.IdleBetween)
                {
                    if (int.TryParse(subcommands[1], out int id))
                    {
                        var macro = P.Config.MacroSolverConfig.FindMacro(id);
                        if (macro != null)
                        {
                            MacroEditor editor = new(macro);
                            return;
                        }
                        else
                        {
                            DuoLog.Error("Macro ID does not exist.");
                            return;
                        }
                    }
                    else
                    {
                        DuoLog.Error("Unable to parse ID as a number.");
                        return;
                    }
                }
                else
                {
                    DuoLog.Error("Unable to open edit macros whilst crafting.");
                    return;
                }
            }

            if (firstArg.ToLower() == "endurance")
            {
                if (subcommands[1].ToLower() is "start")
                {
                    if (CraftingListUI.Processing)
                    {
                        DuoLog.Error("Cannot start endurance whilst processing a list.");
                        return;
                    }
                    if (Endurance.RecipeID == 0)
                    {
                        DuoLog.Error("Cannot start endurance without setting a recipe.");
                        return;
                    }
                    if (!CraftingListFunctions.HasItemsForRecipe(Endurance.RecipeID))
                    {
                        DuoLog.Error("Cannot start endurance as you do not possess all ingredients for your recipe in your inventory.");
                        return;
                    }

                    if (!CraftingListUI.Processing && Endurance.RecipeID != 0)
                    {
                        Endurance.ToggleEndurance(true);
                        return;
                    }
                }

                if (subcommands[1].ToLower() is "stop")
                {
                    if (!Endurance.Enable)
                    {
                        DuoLog.Error("Endurance is not running so cannot be stopped.");
                        return;
                    }
                    if (Endurance.Enable)
                    {
                        Endurance.ToggleEndurance(false);
                        return;
                    }
                }
            }
        }

        PluginUi.IsOpen = true;
        PluginUi.OpenWindow = firstArg.ToLower() switch
        {
            "lists" => OpenWindow.Lists,
            "endurance" => OpenWindow.Endurance,
            "settings" => OpenWindow.Main,
            "macros" => OpenWindow.Macro,
            "builder" => OpenWindow.SpecialList,
            "workshop" => OpenWindow.FCWorkshop,
            "sim" => OpenWindow.Simulator,
            _ => OpenWindow.Overview
        };
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
                Endurance.ToggleEndurance(false);
                break;
            case IPC.IPC.ArtisanMode.Lists:
                CraftingListFunctions.Paused = true;
                break;
        }

        if (Crafting.CurState == Crafting.State.QuickCraft)
            Operations.CloseQuickSynthWindow();

        PreCrafting.Tasks.Clear();
        PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), default));

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
                Endurance.ToggleEndurance(true);
                break;
            case IPC.IPC.ArtisanMode.Lists:
                CraftingListFunctions.Paused = false;
                break;
        }
    }
}

