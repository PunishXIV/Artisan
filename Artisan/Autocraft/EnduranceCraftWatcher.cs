using Artisan.CraftingLists;
using Artisan.CraftingLogic;
using Artisan.GameInterop;
using Artisan.Sounds;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text;
using ECommons.DalamudServices;
using ECommons.Logging;
using Dalamud.Game.Text.SeStringHandling;
using System.Linq;
using Lumina.Excel.Sheets;

namespace Artisan.Autocraft
{
    // TODO: this should be all moved to appropriate places
    public static class EnduranceCraftWatcher
    {
        public static void Setup()
        {
            Crafting.CraftFinished += OnCraftFinished;
            Crafting.QuickSynthProgress += OnQuickSynthProgress;
            Svc.Chat.ChatMessage += ScanForHQItems;
        }

        private static void ScanForHQItems(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (type == (XivChatType)2242 && Svc.Condition[ConditionFlag.Crafting])
            {
                if (message.Payloads.Any(x => x.Type == PayloadType.Item))
                {
                    var item = (ItemPayload)message.Payloads.First(x => x.Type == PayloadType.Item);
                    if (Svc.Data.Excel.GetSheet<Item>().GetRow(item.Item.RowId).CanBeHq)
                    {
                        if (Endurance.Enable && P.Config.EnduranceStopNQ && !item.IsHQ)
                        {
                            Endurance.Enable = false;
                            Svc.Toasts.ShowError("You crafted a non-HQ item. Disabling Endurance.");
                            DuoLog.Error("You crafted a non-HQ item. Disabling Endurance.");
                        }
                    }
                }
            }
        }

        public static void Dispose()
        {
            Crafting.CraftFinished -= OnCraftFinished;
            Crafting.QuickSynthProgress -= OnQuickSynthProgress;
            Svc.Chat.ChatMessage -= ScanForHQItems;
        }

        private static void OnCraftFinished(Recipe recipe, CraftState craft, StepState finalStep, bool cancelled)
        {
            Svc.Log.Debug($"Craft Finished");

            if (CraftingListUI.Processing)
            {
                if (!cancelled)
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

            if (cur == max)
            {
                Operations.CloseQuickSynthWindow();
            }
        }
    }
}
