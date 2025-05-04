using Artisan.CraftingLists;
using Artisan.CraftingLogic.Solvers;
using Artisan.GameInterop;
using Artisan.RawInformation;
using Artisan.RawInformation.Character;
using Artisan.Sounds;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ImGuiNET;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    public class EnduranceIngredients
    {
        public int HQSet { get; set; }
        public int IngredientSlot { get; set; }
        public int NQSet { get; set; }
    }

    internal static unsafe class Endurance
    {
        internal static bool IPCOverride = false;
        internal static bool SkipBuffs = false;
        internal static CircularBuffer<long> Errors = new(5);
        static CircularBuffer<long> FailedStarts = new(5);

        internal static List<int>? HQData = null;

        internal static ushort RecipeID
        {
            get;
            set
            {
                if (field != value)
                {
                    P.Config.CraftingX = false;
                    P.Config.CraftX = 0;
                }
                field = value;
            }
        }

        internal static EnduranceIngredients[] SetIngredients = new EnduranceIngredients[6];

        internal static readonly List<uint> UnableToCraftErrors = new List<uint>()
        {
            1134,1135,1136,1137,1138,1139,1140,1141,1142,1143,1144,1145,1146,1148,1149,1198,1199,1222,1223,1224,
        };

        internal static bool Enable
        {
            get => enable;
            set
            {
                enable = value;
            }
        }

        internal static string RecipeName
        {
            get => RecipeID == 0 ? "No Recipe Selected" : LuminaSheets.RecipeSheet[RecipeID].ItemResult.Value.Name.ToDalamudString().ToString().Trim();
        }

        internal static void ToggleEndurance(bool enable)
        {
            if (RecipeID > 0 && enable)
            {
                Enable = enable;
            }
            else if (Enable)
            {
                Svc.Log.Debug("Endurance toggled off");
                Enable = false;
                IPCOverride = false;
                PreCrafting.Tasks.Clear();
            }
        }

        internal static void Dispose()
        {
            Svc.Toasts.ErrorToast -= Toasts_ErrorToast;
            Svc.Toasts.ErrorToast -= CheckNonMaxQuantityModeFinished;
        }

        internal static void Draw()
        {
            if (CraftingListUI.Processing)
            {
                ImGui.TextWrapped("Processing list...");
                return;
            }

            ImGui.TextWrapped("Endurance mode is Artisan's way to repeat the same craft over and over, either so many times or until you run out of materials. It has full capabilities to automatically repair your gear once a piece is under a certain percentage, use food/potions/exp manuals and extract materia from spiritbonding. Please note these settings are independent of crafting list settings, and only intended to be used to craft the one item repeatedly.");
            ImGui.Separator();
            ImGui.Spacing();

            if (RecipeID == 0)
            {
                ImGuiEx.TextV(ImGuiColors.DalamudRed, "No recipe selected");
            }
            else
            {
                if (!CraftingListFunctions.HasItemsForRecipe(RecipeID))
                    ImGui.BeginDisabled();

                if (ImGui.Checkbox("Enable Endurance Mode", ref enable))
                {
                    ToggleEndurance(enable);
                }

                if (!CraftingListFunctions.HasItemsForRecipe(RecipeID))
                {
                    ImGui.EndDisabled();

                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text($"You cannot start Endurance as you do not possess ingredients to craft this recipe.");
                        ImGui.EndTooltip();
                    }
                }

                ImGuiComponents.HelpMarker("In order to begin Endurance Mode crafting you should first select the recipe in the crafting menu.\nEndurance Mode will automatically repeat the selected recipe similar to Auto-Craft but will factor in food/medicine buffs before doing so.");

                ImGuiEx.Text($"Recipe: {RecipeName} {(RecipeID != 0 ? $"({LuminaSheets.ClassJobSheet[LuminaSheets.RecipeSheet[RecipeID].CraftType.RowId + 8].Abbreviation})" : "")}");
            }

            bool repairs = P.Config.Repair;
            if (ImGui.Checkbox("Automatic Repairs", ref repairs))
            {
                P.Config.Repair = repairs;
                P.Config.Save();
            }
            ImGuiComponents.HelpMarker($"If enabled, Artisan will automatically repair your gear when any piece reaches the configured repair threshold.\n\nCurrent min gear condition is {RepairManager.GetMinEquippedPercent()}% and cost to repair at a vendor is {RepairManager.GetNPCRepairPrice()} gil.\n\nIf unable to repair with Dark Matter, will try for a nearby repair NPC.");
            if (P.Config.Repair)
            {
                //ImGui.SameLine();
                ImGui.PushItemWidth(200);
                int percent = P.Config.RepairPercent;
                if (ImGui.SliderInt("##repairp", ref percent, 10, 100, $"%d%%"))
                {
                    P.Config.RepairPercent = percent;
                    P.Config.Save();
                }
            }

            if (!CharacterInfo.MateriaExtractionUnlocked())
                ImGui.BeginDisabled();

            bool materia = P.Config.Materia;
            if (ImGui.Checkbox("Automatically Extract Materia", ref materia))
            {
                P.Config.Materia = materia;
                P.Config.Save();
            }

            if (!CharacterInfo.MateriaExtractionUnlocked())
            {
                ImGui.EndDisabled();

                ImGuiComponents.HelpMarker("This character has not unlocked materia extraction. This setting will be ignored.");
            }
            else
                ImGuiComponents.HelpMarker("Will automatically extract materia from any equipped gear once it's spiritbond is 100%");

            ImGui.Checkbox("Craft only X times", ref P.Config.CraftingX);
            if (P.Config.CraftingX)
            {
                ImGui.Text("Number of Times:");
                ImGui.SameLine();
                ImGui.PushItemWidth(200);
                if (ImGui.InputInt("###TimesRepeat", ref P.Config.CraftX))
                {
                    if (P.Config.CraftX < 0)
                        P.Config.CraftX = 0;
                }
            }

            if (ImGui.Checkbox("Use Quick Synthesis where possible", ref P.Config.QuickSynthMode))
            {
                P.Config.Save();
            }

            bool stopIfFail = P.Config.EnduranceStopFail;
            if (ImGui.Checkbox("Disable Endurance Mode Upon Failed Craft", ref stopIfFail))
            {
                P.Config.EnduranceStopFail = stopIfFail;
                P.Config.Save();
            }

            bool stopIfNQ = P.Config.EnduranceStopNQ;
            if (ImGui.Checkbox("Disable Endurance Mode Upon Crafting an NQ item", ref stopIfNQ))
            {
                P.Config.EnduranceStopNQ = stopIfNQ;
                P.Config.Save();
            }

            if (ImGui.Checkbox("Max Quantity Mode", ref P.Config.MaxQuantityMode))
            {
                P.Config.Save();
            }

            ImGuiComponents.HelpMarker("Will set ingredients for you, to maximise the amount of crafts possible.");
        }

        internal static void DrawRecipeData()
        {
            var curRec = Operations.GetSelectedRecipeEntry();
            if (curRec is null || curRec->RecipeId == 0)
                return;

            RecipeID = curRec->RecipeId;
            try
            {
                for (int i = 0; i < curRec->IngredientsSpan.Length; i++)
                {
                    var ing = curRec->IngredientsSpan[i];
                    if (ing.ItemId == 0)
                        break;
                    var nq = ing.NumAssignedNQ;
                    var hq = ing.NumAssignedHQ;

                    SetIngredients[i] = new EnduranceIngredients()
                    {
                        NQSet = nq,
                        HQSet = hq,
                    };

                    //Svc.Log.Debug($"Assigned {nq}NQ, {hq}HQ {ing.ItemId.NameOfItem()}");
                }
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "Setting Recipe ID");
                RecipeID = 0;
            }


        }

        internal static void Init()
        {
            Svc.Toasts.ErrorToast += Toasts_ErrorToast;
            Svc.Toasts.ErrorToast += CheckNonMaxQuantityModeFinished;
        }

        private static bool enable = false;
        private static void CheckNonMaxQuantityModeFinished(ref SeString message, ref bool isHandled)
        {
            if (!P.Config.MaxQuantityMode && Enable &&
                (message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1147).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1146).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1145).Text.ExtractText() ||
                 message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == 1144).Text.ExtractText()))
            {
                if (P.Config.PlaySoundFinishEndurance)
                    SoundPlayer.PlaySound();

                ToggleEndurance(false);
            }
        }

        public static void Update()
        {
            if (!Enable) return;
            var needToRepair = P.Config.Repair && RepairManager.GetMinEquippedPercent() < P.Config.RepairPercent && (RepairManager.CanRepairAny() || RepairManager.RepairNPCNearby(out _));
            if ((Crafting.CurState == Crafting.State.QuickCraft && Crafting.QuickSynthCompleted) || needToRepair ||
                (P.Config.Materia && Spiritbond.IsSpiritbondReadyAny() && CharacterInfo.MateriaExtractionUnlocked()))
            {
                Operations.CloseQuickSynthWindow();
            }

            if (!P.TM.IsBusy && Crafting.CurState is Crafting.State.IdleNormal or Crafting.State.IdleBetween)
            {
                var isCrafting = Svc.Condition[ConditionFlag.Crafting];
                var preparing = Svc.Condition[ConditionFlag.PreparingToCraft];
                var recipe = LuminaSheets.RecipeSheet[RecipeID];
                if (PreCrafting.Tasks.Count > 0)
                {
                    return;
                }

                if (P.Config.CraftingX && P.Config.CraftX == 0 || PreCrafting.GetNumberCraftable(recipe) == 0)
                {
                    ToggleEndurance(false);
                    P.Config.CraftingX = false;
                    DuoLog.Information("Craft X has completed.");
                    if (P.Config.PlaySoundFinishEndurance)
                        SoundPlayer.PlaySound();

                    return;
                }

                if (RecipeID == 0)
                {
                    Svc.Toasts.ShowError("No recipe has been set for Endurance mode. Disabling Endurance mode.");
                    DuoLog.Error("No recipe has been set for Endurance mode. Disabling Endurance mode.");
                    ToggleEndurance(false);
                    return;
                }

                if ((Job)LuminaSheets.RecipeSheet[RecipeID].CraftType.RowId + 8 != CharacterInfo.JobID)
                {
                    PreCrafting.equipGearsetLoops = 0;
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), TimeSpan.FromMilliseconds(200)));
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskClassChange((Job)LuminaSheets.RecipeSheet[RecipeID].CraftType.RowId + 8), TimeSpan.FromMilliseconds(200)));
                    return;
                }

                bool needEquipItem = recipe.ItemRequired.RowId > 0 && !PreCrafting.IsItemEquipped(recipe.ItemRequired.RowId);
                if (needEquipItem)
                {
                    PreCrafting.equipAttemptLoops = 0;
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskEquipItem(recipe.ItemRequired.RowId), TimeSpan.FromMilliseconds(200)));
                    return;
                }

                if (!Spiritbond.ExtractMateriaTask(P.Config.Materia))
                {
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), TimeSpan.FromMilliseconds(200)));
                    return;
                }

                if (P.Config.Repair && !RepairManager.ProcessRepair())
                {
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), TimeSpan.FromMilliseconds(200)));
                    return;
                }

                var config = P.Config.RecipeConfigs.GetValueOrDefault(RecipeID) ?? new();
                PreCrafting.CraftType type = P.Config.QuickSynthMode && recipe.CanQuickSynth && P.ri.HasRecipeCrafted(recipe.RowId) ? PreCrafting.CraftType.Quick : PreCrafting.CraftType.Normal;
                bool needConsumables = PreCrafting.NeedsConsumablesCheck(type, config);
                bool hasConsumables = PreCrafting.HasConsumablesCheck(config);

                if (P.Config.AbortIfNoFoodPot && needConsumables && !hasConsumables)
                {
                    PreCrafting.MissingConsumablesMessage(recipe, config);
                    ToggleEndurance(false);
                    return;
                }

                bool needFood = config != default && ConsumableChecker.HasItem(config.RequiredFood, config.RequiredFoodHQ) && !ConsumableChecker.IsFooded(config);
                bool needPot = config != default && ConsumableChecker.HasItem(config.RequiredPotion, config.RequiredPotionHQ) && !ConsumableChecker.IsPotted(config);
                bool needManual = config != default && ConsumableChecker.HasItem(config.RequiredManual, false) && !ConsumableChecker.IsManualled(config);
                bool needSquadronManual = config != default && ConsumableChecker.HasItem(config.RequiredSquadronManual, false) && !ConsumableChecker.IsSquadronManualled(config);

                if (needFood || needPot || needManual || needSquadronManual)
                {
                    if (!P.TM.IsBusy && !PreCrafting.Occupied())
                    {
                        P.TM.Enqueue(() => PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), TimeSpan.FromMilliseconds(200))));
                        P.TM.Enqueue(() => PreCrafting.Tasks.Add((() => PreCrafting.TaskUseConsumables(config, type), TimeSpan.FromMilliseconds(200))));
                        P.TM.DelayNext(100);
                    }
                    return;
                }

                if (Crafting.CurState is Crafting.State.IdleBetween or Crafting.State.IdleNormal && !PreCrafting.Occupied())
                {
                    if (!P.TM.IsBusy)
                    {
                        PreCrafting.Tasks.Add((() => PreCrafting.TaskSelectRecipe(recipe), TimeSpan.FromMilliseconds(500)));

                        if (!CraftingListFunctions.RecipeWindowOpen() && !CraftingListFunctions.CosmicLogOpen()) return;

                        if (type == PreCrafting.CraftType.Quick)
                        {
                            P.TM.Enqueue(() => Operations.QuickSynthItem(P.Config.CraftingX ? P.Config.CraftX : 99), "EnduranceQSStart");
                            P.TM.Enqueue(() => Crafting.CurState is Crafting.State.WaitStart, 5000, "EnduranceQSWaitStart");
                        }
                        else if (type == PreCrafting.CraftType.Normal)
                        {
                            P.TM.DelayNext(200);

                            if (P.Config.MaxQuantityMode)
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(), "EnduranceSetIngredientsNonLayout");
                            else
                                P.TM.Enqueue(() => CraftingListFunctions.SetIngredients(SetIngredients), "EnduranceSetIngredientsLayout");

                            P.TM.Enqueue(() => Operations.RepeatActualCraft(), 500, "EnduranceNormalStart");
                            P.TM.Enqueue(() => Crafting.CurState is Crafting.State.WaitStart, 500, "EnduranceNormalWaitStart");
                            P.TM.Enqueue(() =>
                            {
                                if (!RaphaelCache.InProgressAny())
                                {
                                    if (FailedStarts.Count() >= 5 && FailedStarts.All(x => x > Environment.TickCount64 - (10 * 1000)))
                                    {
                                        FailedStarts.Clear();
                                        if (Crafting.CurState is not Crafting.State.QuickCraft and not Crafting.State.InProgress and not Crafting.State.WaitStart)
                                        {
                                            if (!IPCOverride)
                                            {
                                                DuoLog.Error($"Unable to start crafting. Disabling Endurance. {(!P.Config.MaxQuantityMode ? "Please enable Max Quantity mode or set your ingredients before starting." : "")}");
                                            }
                                            else
                                            {
                                                DuoLog.Error($"Something has gone wrong whilst another plugin tried to control Artisan. Disabling Endurance.");
                                            }
                                            ToggleEndurance(false);
                                        }
                                    }
                                    else
                                    {
                                        FailedStarts.PushBack(Environment.TickCount64);
                                    }
                                }
                            });

                        }
                    }

                }
            }
        }

        private static void Toasts_ErrorToast(ref SeString message, ref bool isHandled)
        {
            if (Enable || (CraftingListUI.Processing && !CraftingListFunctions.Paused))
            {
                //foreach (uint errorId in UnableToCraftErrors)
                //{
                //    if (message.ExtractText() == Svc.Data.GetExcelSheet<LogMessage>()?.First(x => x.RowId == errorId).Text.ExtractText())
                //    {
                //        Svc.Toasts.ShowError($"Current crafting mode has been {(Enable ? "disabled" : "paused")} due to unable to craft error.");
                //        DuoLog.Error($"Current crafting mode has been {(Enable ? "disabled" : "paused")} due to unable to craft error.");
                //        if (enable)
                //            ToggleEndurance(false);
                //        if (CraftingListUI.Processing)
                //            CraftingListFunctions.Paused = true;
                //        PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), default));

                //        P.TM.Abort();
                //        CraftingListFunctions.CLTM.Abort();
                //    }
                //}

                Errors.PushBack(Environment.TickCount64);
                Svc.Log.Warning($"Error Warnings [{Errors.Count(x => x > Environment.TickCount64 - 10 * 1000)}]: {message}");
                if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 10 * 1000))
                {
                    Svc.Toasts.ShowError($"Current crafting mode has been {(Enable ? "disabled" : "paused")} due to too many errors in succession.");
                    DuoLog.Error($"Current crafting mode has been {(Enable ? "disabled" : "paused")} due to too many errors in succession.");
                    if (enable)
                        ToggleEndurance(false);
                    if (CraftingListUI.Processing)
                        CraftingListFunctions.Paused = true;
                    Errors.Clear();
                    PreCrafting.Tasks.Add((() => PreCrafting.TaskExitCraft(), default));

                    P.TM.Abort();
                    CraftingListFunctions.CLTM.Abort();
                }
            }
        }
    }
}
