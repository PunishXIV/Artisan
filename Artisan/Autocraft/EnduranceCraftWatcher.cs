using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.Sounds;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.GeneratedSheets;

namespace Artisan.Autocraft
{
    // TODO: this should be all moved to appropriate places
    public static class EnduranceCraftWatcher
    {
        public static void Setup()
        {
            Crafting.CraftFinished += OnCraftFinished;
            Crafting.QuickSynthProgress += OnQuickSynthProgress;
        }

        public static void Dispose()
        {
            Crafting.CraftFinished -= OnCraftFinished;
            Crafting.QuickSynthProgress -= OnQuickSynthProgress;
        }

        private static void OnCraftFinished(Recipe recipe, CraftState craft, StepState finalStep, bool cancelled)
        {
            Svc.Log.Debug($"Craft Finished");

            if (CraftingListUI.Processing)
            {
                if (!CraftingListFunctions.Paused && !cancelled)
                {
                    Svc.Log.Verbose("Advancing Crafting List");
                    CraftingListFunctions.CurrentIndex++;
                }
                if (cancelled)
                {
                    CraftingListFunctions.Paused = true;
                    CraftingListFunctions.CLTM.Abort();
                }
            }

            if (Endurance.Enable)
            {
                if (cancelled)
                {
                    Endurance.Enable = false;
                    Svc.Toasts.ShowError("You've cancelled a craft. Disabling Endurance.");
                    DuoLog.Error("You've cancelled a craft. Disabling Endurance.");
                }
                else if (finalStep.Progress < craft.CraftProgress && P.Config.EnduranceStopFail)
                {
                    Endurance.Enable = false;
                    Svc.Toasts.ShowError("You failed a craft. Disabling Endurance.");
                    DuoLog.Error("You failed a craft. Disabling Endurance.");
                }
                else if (P.Config.EnduranceStopNQ && recipe.CanHq && !craft.CraftCollectible && finalStep.Quality < craft.CraftQualityMax)
                {
                    Endurance.Enable = false;
                    Svc.Toasts.ShowError("You crafted a non-HQ item. Disabling Endurance.");
                    DuoLog.Error("You crafted a non-HQ item. Disabling Endurance.");
                }
                else if (P.Config.CraftingX && P.Config.CraftX > 0)
                {
                    P.Config.CraftX -= 1;
                    if (P.Config.CraftX == 0)
                    {
                        P.Config.CraftingX = false;
                        Endurance.Enable = false;
                        if (P.Config.PlaySoundFinishEndurance)
                            SoundPlayer.PlaySound();
                        DuoLog.Information("Craft X has completed.");
                    }
                }
            }
        }

        private static void OnQuickSynthProgress(int cur, int max)
        {
            if (cur == 0)
                return;

            CraftingListFunctions.CurrentIndex++;
            if (P.Config.QuickSynthMode && Endurance.Enable && P.Config.CraftX > 0)
                P.Config.CraftX--;
        }
    }
}
