using Artisan.Autocraft;
using Artisan.GameInterop;
using Artisan.IPC;
using Artisan.RawInformation;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using KamiToolKit.Nodes.Simplified;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Artisan.UI.KTK
{
    internal unsafe class NativeCraftAll
    {
        AddonController? regularRecipeNoteController;
        AddonController? moonRecipeNoteController;
        TextButtonNodeSynth? SynthesizeAll;
        NumericInputNode? SynthesizeXCounter;
        NumericInputNode? RestockXCounter;
        LabelTextNode? RestockLabel;
        TextButtonNode? RestockButton;

        LabelTextNode? Crystal1Label;
        LabelTextNode? Crystal2Label;

        LabelTextNode? Ingredient1Restock;
        LabelTextNode? Ingredient2Restock;
        LabelTextNode? Ingredient3Restock;
        LabelTextNode? Ingredient4Restock;
        LabelTextNode? Ingredient5Restock;
        LabelTextNode? Ingredient6Restock;

        SimpleImageNode? Ingredient1RestockSeperator;
        SimpleImageNode? Ingredient2RestockSeperator;
        SimpleImageNode? Ingredient3RestockSeperator;
        SimpleImageNode? Ingredient4RestockSeperator;
        SimpleImageNode? Ingredient5RestockSeperator;
        SimpleImageNode? Ingredient6RestockSeperator;

        LabelTextNode? AToolsLabel;

        int synthVal = 0;
        int restockVal = 0;
        int SynthesizeableCount = 0;
        bool resized = false;

        static float scaleSize = 64f;

        static float horizontalScale = scaleSize * 1.25f;
        static float verticalScale = scaleSize;

        static Recipe? CurrentRecipe => Operations.GetSelectedRecipeEntry() is not null ? LuminaSheets.RecipeSheet[Operations.GetSelectedRecipeEntry()->RecipeId] : null;

        static int numberOfCrystals => CurrentRecipe?.Ingredients().Count(i => i.Item.RowId >= 2 && i.Item.RowId <= 19) ?? 0;
        static uint firstCrystalId => CurrentRecipe?.Ingredients().FirstOrDefault(i => i.Item.RowId >= 2 && i.Item.RowId <= 19).Item.RowId ?? 0;
        static uint secondCrystalId => CurrentRecipe?.Ingredients().LastOrDefault(i => i.Item.RowId >= 2 && i.Item.RowId <= 19).Item.RowId ?? 0;

        static int numberOfIngredients => CurrentRecipe?.Ingredients().Count(i => i.Item.RowId > 19) ?? 0;
        static uint? firstIngredientId => CurrentRecipe?.Ingredients()?.FirstOrDefault(i => i.Item.RowId > 19)?.Item.RowId ?? 0;
        static uint? secondIngredientId => CurrentRecipe?.Ingredients()?.Skip(1)?.FirstOrDefault(i => i.Item.RowId > 19)?.Item.RowId ?? 0;
        static uint? thirdIngredientId => CurrentRecipe?.Ingredients()?.Skip(2)?.FirstOrDefault(i => i.Item.RowId > 19)?.Item.RowId ?? 0;
        static uint? fourthIngredientId => CurrentRecipe?.Ingredients()?.Skip(3)?.FirstOrDefault(i => i.Item.RowId > 19)?.Item.RowId ?? 0;
        static uint? fifthIngredientId => CurrentRecipe?.Ingredients()?.Skip(4)?.FirstOrDefault(i => i.Item.RowId > 19)?.Item.RowId ?? 0;
        static uint? sixthIngredientId => CurrentRecipe?.Ingredients()?.Skip(5)?.FirstOrDefault(i => i.Item.RowId > 19)?.Item.RowId ?? 0;

        public NativeCraftAll()
        {
            regularRecipeNoteController = new()
            {
                AddonName = "RecipeNote",
                OnSetup = RegularOnSetup,
                OnRefresh = OnRefresh,
                OnFinalize = OnFinalize,
                OnUpdate = OnUpdate,
            };
            regularRecipeNoteController?.Enable();

            moonRecipeNoteController = new()
            {
                AddonName = "WKSRecipeNotebook",
                OnSetup = MoonOnSetup,
                OnRefresh = OnRefresh,
                OnFinalize = OnFinalize,
            };
            moonRecipeNoteController?.Enable();
        }

        private static string FormatRetainerItemCount(uint? itemId)
        {
            if (itemId == null)
                return "0";

            var count = RetainerInfo.GetRetainerItemCount(itemId.Value);
            return count >= 1000 ? "999+" : count.ToString();
        }

        private void OnUpdate(AtkUnitBase* addon)
        {
            //if (!P.Config.UseNativeButtons)
            //{
            //    Dispose(false);
            //}
            //else
            //{
            if (SynthesizeAll is null || SynthesizeXCounter is null)
            {
                if (addon->NameString == "RecipeNote")
                    RegularOnSetup(addon);
                else if (addon->NameString == "WKSRecipeNotebook")
                    MoonOnSetup(addon);
            }

            if (RestockButton is not null)
            {
                if (RetainerInfo.GetReachableRetainerBell() == null || !RetainerInfo.AToolsEnabled)
                {
                    RestockButton.IsEnabled = false;
                    //RestockXCounter.IsEnabled = false;
                }
                else
                {
                    RestockButton.IsEnabled = true;
                    //RestockXCounter.IsEnabled = true;
                }

                Crystal2Label.IsVisible = numberOfCrystals == 2;
                Crystal1Label.String = $"Crystal 1 Restock: {RetainerInfo.GetRetainerItemCount(firstCrystalId)}";
                Crystal2Label.String = $"Crystal 2 Restock: {RetainerInfo.GetRetainerItemCount(secondCrystalId)}";
                Ingredient1Restock.IsVisible = numberOfIngredients >= 1;
                Ingredient2Restock.IsVisible = numberOfIngredients >= 2;
                Ingredient3Restock.IsVisible = numberOfIngredients >= 3;
                Ingredient4Restock.IsVisible = numberOfIngredients >= 4;
                Ingredient5Restock.IsVisible = numberOfIngredients >= 5;
                Ingredient6Restock.IsVisible = numberOfIngredients >= 6;
                Ingredient1RestockSeperator.IsVisible = numberOfIngredients >= 1;
                Ingredient2RestockSeperator.IsVisible = numberOfIngredients >= 2;
                Ingredient3RestockSeperator.IsVisible = numberOfIngredients >= 3;
                Ingredient4RestockSeperator.IsVisible = numberOfIngredients >= 4;
                Ingredient5RestockSeperator.IsVisible = numberOfIngredients >= 5;
                Ingredient6RestockSeperator.IsVisible = numberOfIngredients >= 6;
                Ingredient1Restock.String = $"Restock: {FormatRetainerItemCount(firstIngredientId)}";
                Ingredient2Restock.String = $"Restock: {FormatRetainerItemCount(secondIngredientId)}";
                Ingredient3Restock.String = $"Restock: {FormatRetainerItemCount(thirdIngredientId)}";
                Ingredient4Restock.String = $"Restock: {FormatRetainerItemCount(fourthIngredientId)}";
                Ingredient5Restock.String = $"Restock: {FormatRetainerItemCount(fifthIngredientId)}";
                Ingredient6Restock.String = $"Restock: {FormatRetainerItemCount(sixthIngredientId)}";
            }
            //}
        }

        private void MoonOnSetup(AtkUnitBase* addon)
        {
            //if (!P.Config.UseNativeButtons) return;
            if (addon->RootNode is null) return;

            var synthButton = addon->GetNodeById(50);
            if (synthButton is null) return;

            var buttonY = synthButton->Y;
            var buttonX = synthButton->X - synthButton->Width;

            var textNode = addon->GetTextNodeById(34);
            if (textNode is null) return;

            var text = textNode->NodeText.ToString();
            SynthesizeableCount = text == "" ? 0 : Convert.ToInt32(text.GetNumbers());

            SynthesizeAll = new()
            {
                Position = new Vector2(buttonX, buttonY),
                Size = new Vector2(synthButton->Width, 36),
                String = $"Craft All ({SynthesizeableCount})",
                OnClick = () => SynthesizeX(),
                IsEnabled = SynthesizeableCount > 0,
                TextTooltip = "Button added by Artisan.",
                NodeId = 100000
            };

            SynthesizeAll.AttachNode(synthButton, NodePosition.AfterTarget);

            SynthesizeXCounter = new()
            {
                Position = new Vector2(SynthesizeAll.X - 90, buttonY + 3),
                Size = new Vector2(90, 28),
                OnValueUpdate = (t) =>
                {
                    synthVal = t;
                    SynthesizeAll?.String = synthVal == 0 ? $"Craft All ({SynthesizeableCount})" : $"Craft {t}";
                },
                NodeFlags = synthButton->NodeFlags,
                Max = SynthesizeableCount,
                IsEnabled = SynthesizeableCount > 0,
                NodeId = 100001
            };

            SynthesizeXCounter.AttachNode(SynthesizeAll, NodePosition.AfterTarget);
        }

        private void OnFinalize(AtkUnitBase* addon)
        {
            resized = false;

            Dispose(false);
        }

        private void OnRefresh(AtkUnitBase* addon)
        {
            //if (!P.Config.UseNativeButtons) return;
            if (SynthesizeAll is null) return;
            if (SynthesizeXCounter is null) return;

            var textNode = addon->NameString == "WKSRecipeNotebook" ? addon->GetTextNodeById(34) : addon->GetTextNodeById(78);
            if (textNode is null) return;

            var text = textNode->NodeText.ToString();
            SynthesizeableCount = text == "" ? 0 : Convert.ToInt32(text.GetNumbers());

            SynthesizeXCounter.Max = SynthesizeableCount;
            if (SynthesizeableCount == 0)
            {
                SynthesizeXCounter.IsEnabled = false;
                SynthesizeAll.IsEnabled = false;
            }
            else
            {
                SynthesizeXCounter.IsEnabled = true;
                SynthesizeAll.IsEnabled = true;
            }

            synthVal = Math.Min(SynthesizeableCount, synthVal);
            SynthesizeXCounter.Value = synthVal;
            if (synthVal == 0)
                SynthesizeAll.String = $"Craft All ({SynthesizeableCount})";

            var restockable = GetRestockableAmount();
            if (RestockLabel is not null)
            {
                RestockLabel.String = $"Restock: {restockable.AllIngredients} (Excl. Crystals: {restockable.ExcludingCrystals})";
            }
        }

        private void RegularOnSetup(AtkUnitBase* addon)
        {
            if (addon->RootNode is null) return;
            IncreaseAddonSize(addon);
            //if (!P.Config.UseNativeButtons) return;

            var synthButton = addon->GetNodeById(104);
            if (synthButton is null) return;

            var synthBg = synthButton->GetAsAtkComponentButton()->ButtonBGNode->GetAsAtkNineGridNode();
            if (synthBg is null) return;

            synthBg->SetWidth((ushort)(synthButton->Width));

            var buttonY = synthButton->Y + synthButton->Height;
            var buttonX = synthButton->X;

            var textNode = addon->GetTextNodeById(78);
            if (textNode is null) return;

            var text = textNode->NodeText.ToString();
            SynthesizeableCount = text == "" ? 0 : Convert.ToInt32(text.GetNumbers());

            SynthesizeAll = new()
            {
                Position = new Vector2(buttonX, buttonY),
                Size = new Vector2(synthBg->Width, synthBg->Height),
                String = $"Craft All ({SynthesizeableCount})",
                OnClick = () => SynthesizeX(),
                IsEnabled = SynthesizeableCount > 0,
                TextTooltip = "Button added by Artisan.",
                NodeId = 100000
            };

            SynthesizeAll.AttachNode(synthButton, NodePosition.AfterTarget);

            SynthesizeXCounter = new()
            {
                Position = new Vector2(SynthesizeAll.X - SynthesizeAll.Width, buttonY + 5),
                Size = new Vector2(140, 28),
                Step = 1,
                OnValueUpdate = (t) =>
                {
                    synthVal = t;
                    SynthesizeAll?.String = synthVal == 0 ? $"Craft All ({SynthesizeableCount})" : $"Craft {t}";
                },
                Max = SynthesizeableCount,
                IsEnabled = SynthesizeableCount > 0,
                NodeId = 100001
            };

            SynthesizeXCounter.AttachNode(SynthesizeAll, NodePosition.AfterTarget);

            if (RetainerInfo.AToolsEnabled)
            {
                RestockButton = new()
                {
                    Position = new Vector2(SynthesizeAll.X, SynthesizeAll.Y + SynthesizeAll.Height),
                    Size = new Vector2(140, 36),
                    String = "Restock",
                    OnClick = () => RestockX(),
                    IsEnabled = true,
                    TextTooltip = "Button added by Artisan.",
                    NodeId = 100002
                };

                RestockButton.AttachNode(SynthesizeXCounter, NodePosition.AfterTarget);

                RestockXCounter = new()
                {
                    Position = new Vector2(RestockButton.X - RestockButton.Width + 40, RestockButton.Y),
                    Size = new Vector2(100, 28),
                    Step = 1,
                    OnValueUpdate = (t) =>
                    {
                        restockVal = t;
                        RestockButton?.String = restockVal == 0 ? $"Restock" : $"Restock {t}";
                    },
                    Max = 999,
                    IsEnabled = true,
                    NodeId = 100003
                };

                RestockXCounter.AttachNode(RestockButton, NodePosition.AfterTarget);
                var restockable = GetRestockableAmount();

                RestockLabel = new()
                {
                    Position = new Vector2(0, RestockXCounter.Y),
                    Size = new Vector2(140, 28),
                    FontSize = 12,
                    String = $"Restock: {restockable.AllIngredients} (Excl. Crystals: {restockable.ExcludingCrystals})",
                    NodeId = 100004
                };

                RestockLabel.AttachNode(RestockXCounter, NodePosition.AfterTarget);

                var secondCrystal = addon->GetNodeById(83);

                Crystal1Label = new()
                {
                    Position = new Vector2(secondCrystal->X + 120, 138),
                    Size = new Vector2(140, 28),
                    FontSize = 12,
                    String = $"Crystal 1 Restock:",
                    NodeId = 100005
                };

                Crystal1Label.AttachNode(RestockLabel, NodePosition.AfterTarget);

                Crystal2Label = new()
                {
                    Position = new Vector2(Crystal1Label.X, 152),
                    Size = new Vector2(140, 28),
                    FontSize = 12,
                    String = $"Crystal 2 Restock:",
                    NodeId = 100006,
                    IsVisible = numberOfCrystals == 2
                };

                Crystal2Label.AttachNode(Crystal1Label, NodePosition.AfterTarget);

                var ing1Base = addon->GetNodeById(89);
                var ing2Base = addon->GetNodeById(90);
                var ing3Base = addon->GetNodeById(91);
                var ing4Base = addon->GetNodeById(92);
                var ing5Base = addon->GetNodeById(93);
                var ing6Base = addon->GetNodeById(94);

                Ingredient1Restock = new()
                {
                    Position = new Vector2(ing1Base->X + ing1Base->Width + 8, ing1Base->Y),
                    Size = new Vector2(162, ing1Base->Height),
                    FontSize = 12,
                    String = $"Restock:",
                    NodeId = 100007,
                    IsVisible = numberOfIngredients >= 1
                };

                Ingredient1Restock.AttachNode(ing1Base, NodePosition.AfterTarget);

                Ingredient2Restock = new()
                {
                    Position = new Vector2(ing2Base->X + ing2Base->Width + 8, ing2Base->Y),
                    Size = Ingredient1Restock.Size,
                    FontSize = Ingredient1Restock.FontSize,
                    String = $"Restock:",
                    NodeId = 100008,
                    IsVisible = numberOfIngredients >= 2
                };

                Ingredient2Restock.AttachNode(ing2Base, NodePosition.AfterTarget);

                Ingredient3Restock = new()
                {
                    Position = new Vector2(ing3Base->X + ing3Base->Width + 8, ing3Base->Y),
                    Size = Ingredient1Restock.Size,
                    FontSize = Ingredient1Restock.FontSize,
                    String = $"Restock:",
                    NodeId = 100009,
                    IsVisible = numberOfIngredients >= 3
                };

                Ingredient3Restock.AttachNode(ing3Base, NodePosition.AfterTarget);

                Ingredient4Restock = new()
                {
                    Position = new Vector2(ing4Base->X + ing4Base->Width + 8, ing4Base->Y),
                    Size = Ingredient1Restock.Size,
                    FontSize = Ingredient1Restock.FontSize,
                    String = $"Restock:",
                    NodeId = 100010,
                    IsVisible = numberOfIngredients >= 4
                };

                Ingredient4Restock.AttachNode(ing4Base, NodePosition.AfterTarget);

                Ingredient5Restock = new()
                {
                    Position = new Vector2(ing5Base->X + ing5Base->Width + 8, ing5Base->Y),
                    Size = Ingredient1Restock.Size,
                    FontSize = Ingredient1Restock.FontSize,
                    String = $"Restock:",
                    NodeId = 100011,
                    IsVisible = numberOfIngredients >= 5
                };

                Ingredient5Restock.AttachNode(ing5Base, NodePosition.AfterTarget);

                Ingredient6Restock = new()
                {
                    Position = new Vector2(ing6Base->X + ing6Base->Width + 8, ing6Base->Y),
                    Size = Ingredient1Restock.Size,
                    FontSize = Ingredient1Restock.FontSize,
                    String = $"Restock:",
                    NodeId = 100012,
                    IsVisible = numberOfIngredients >= 6
                };

                Ingredient6Restock.AttachNode(ing6Base, NodePosition.AfterTarget);

                var ingredientSeperator = ing1Base->GetAsAtkComponentNode()->Component->GetNodeById(16)->GetAsAtkImageNode();

                Ingredient1RestockSeperator = new()
                {
                    Position = new Vector2(Ingredient1Restock.X - 8, Ingredient1Restock.Y + 12),
                    Size = new Vector2(6, 36),
                    NodeId = 100014,
                    IsVisible = numberOfIngredients >= 1,
                    TexturePath = "ui/uld/img02/RecipeNoteBook_hr1.tex",
                    TextureSize = new Vector2(16, 72) / 2,
                    TextureCoordinates = new Vector2(104, 0) / 2
                };

                Ingredient1RestockSeperator.AttachNode(ing1Base, NodePosition.AfterTarget);

                Ingredient2RestockSeperator = new()
                {
                    Position = new Vector2(Ingredient2Restock.X - 8, Ingredient2Restock.Y + 12),
                    Size = new Vector2(6, 36),
                    NodeId = 100015,
                    IsVisible = numberOfIngredients >= 2,
                    TexturePath = "ui/uld/img02/RecipeNoteBook_hr1.tex",
                    TextureSize = new Vector2(16, 72) / 2,
                    TextureCoordinates = new Vector2(104, 0) / 2
                };
                Ingredient2RestockSeperator.AttachNode(ing2Base, NodePosition.AfterTarget);
                Ingredient3RestockSeperator = new()
                {
                    Position = new Vector2(Ingredient3Restock.X - 8, Ingredient3Restock.Y + 12),
                    Size = new Vector2(6, 36),
                    NodeId = 100016,
                    IsVisible = numberOfIngredients >= 3,
                    TexturePath = "ui/uld/img02/RecipeNoteBook_hr1.tex",
                    TextureSize = new Vector2(16, 72) / 2,
                    TextureCoordinates = new Vector2(104, 0) / 2
                };
                Ingredient3RestockSeperator.AttachNode(ing3Base, NodePosition.AfterTarget);
                Ingredient4RestockSeperator = new()
                {
                    Position = new Vector2(Ingredient4Restock.X - 8, Ingredient4Restock.Y + 12),
                    Size = new Vector2(6, 36),
                    NodeId = 100017,
                    IsVisible = numberOfIngredients >= 4,
                    TexturePath = "ui/uld/img02/RecipeNoteBook_hr1.tex",
                    TextureSize = new Vector2(16, 72) / 2,
                    TextureCoordinates = new Vector2(104, 0) / 2
                };
                Ingredient4RestockSeperator.AttachNode(ing4Base, NodePosition.AfterTarget);
                Ingredient5RestockSeperator = new()
                {
                    Position = new Vector2(Ingredient5Restock.X - 8, Ingredient5Restock.Y + 12),
                    Size = new Vector2(6, 36),
                    NodeId = 100018,
                    IsVisible = numberOfIngredients >= 5,
                    TexturePath = "ui/uld/img02/RecipeNoteBook_hr1.tex",
                    TextureSize = new Vector2(16, 72) / 2,
                    TextureCoordinates = new Vector2(104, 0) / 2
                };
                Ingredient5RestockSeperator.AttachNode(ing5Base, NodePosition.AfterTarget);
                Ingredient6RestockSeperator = new()
                {
                    Position = new Vector2(Ingredient6Restock.X - 8, Ingredient6Restock.Y + 12),
                    Size = new Vector2(6, 36),
                    NodeId = 100019,
                    IsVisible = numberOfIngredients >= 6,
                    TexturePath = "ui/uld/img02/RecipeNoteBook_hr1.tex",
                    TextureSize = new Vector2(16, 72) / 2,
                    TextureCoordinates = new Vector2(104, 0) / 2
                };
                Ingredient6RestockSeperator.AttachNode(ing6Base, NodePosition.AfterTarget);

            }
            else
            {
                AToolsLabel = new()
                {
                    Position = new Vector2(12, SynthesizeXCounter.Y + SynthesizeXCounter.Height),
                    Size = new Vector2(140, 36),
                    String = "Please install and enable Allagan Tools for retainer restock features.",
                    FontSize = 12,
                    NodeId = 100013
                };
                AToolsLabel.AttachNode(SynthesizeXCounter, NodePosition.AfterTarget);
            }
        }

        private (int AllIngredients, int ExcludingCrystals) GetRestockableAmount()
        {
            var recipe = LuminaSheets.RecipeSheet.TryGetValue(Operations.GetSelectedRecipeEntry()->RecipeId, out var rec);
            if (!recipe) return (0, 0);

            int? maxRestockable = null;
            int? maxRestockableWithoutCrystals = null;
            foreach (var ingredient in rec.Ingredients())
            {
                if (ingredient.Item.RowId == 0 || ingredient.Amount == 0) continue;

                int inRetainer = RetainerInfo.GetRetainerItemCount(ingredient.Item.RowId);
                int amountForCraft = inRetainer / ingredient.Amount;
                maxRestockable = Math.Min(maxRestockable ?? amountForCraft, amountForCraft);
                if (ingredient.Item.RowId > 19)
                    maxRestockableWithoutCrystals = Math.Min(maxRestockableWithoutCrystals ?? amountForCraft, amountForCraft);
            }

            return (maxRestockable ?? 0, maxRestockableWithoutCrystals ?? 0);
        }

        private void RestockX()
        {
            var recipe = LuminaSheets.RecipeSheet.TryGetValue(Operations.GetSelectedRecipeEntry()->RecipeId, out var rec);
            if (!recipe) return;

            List<(uint itemId, int amount)> ingredients = new();
            foreach (var ingredient in rec.Ingredients())
            {
                if (ingredient.Item.RowId == 0) continue;
                var amountToWithdraw = ingredient.Amount * restockVal;
                if (RetainerInfo.GetRetainerItemCount(ingredient.Item.RowId) < amountToWithdraw)
                {
                    DuoLog.Warning($"Not enough stock for {ingredient.Item.Name} in retainers.\nNeeded: {amountToWithdraw}, Available: {RetainerInfo.GetRetainerItemCount(ingredient.Item.RowId)}. Will withdraw what is available.");
                }

                ingredients.Add((ingredient.Item.RowId, amountToWithdraw));
            }

            RetainerInfo.RestockFromRetainers(ingredients);
        }

        private void IncreaseAddonSize(AtkUnitBase* addon)
        {
            try
            {
                if (addon->RootNode is null || resized) return;

                var height = addon->RootNode->Height;
                var width = addon->RootNode->Width;

                if (height > 650 && width > 900) return;
                var treeList = addon->GetNodeById(39);
                var lineLeft = addon->GetNodeById(105);
                var lineRight = addon->GetNodeById(108);
                var lineBotton = addon->GetNodeById(107);
                var lineTop = addon->GetNodeById(106);
                var ingGrayBox = addon->GetNodeById(81);
                var regularSynthButton = addon->GetNodeById(104);
                var regularQuickSynthButton = addon->GetNodeById(103);
                var regularTrialButton = addon->GetNodeById(102);

                if (treeList is not null)
                {
                    var newHeight = treeList->Height + verticalScale;
                    treeList->SetHeight((ushort)(newHeight));
                }

                addon->SetSize((ushort)(width + horizontalScale + 16), (ushort)(height + verticalScale));

                if (lineLeft is not null)
                {
                    lineLeft->SetWidth((ushort)(addon->RootNode->Height - 96));
                    lineLeft->SetYFloat(addon->RootNode->Height - 19);
                }
                if (lineRight is not null)
                {
                    lineRight->SetWidth((ushort)(addon->RootNode->Height - 96));
                    lineRight->SetYFloat(addon->RootNode->Height - 19);
                }

                if (lineBotton is not null)
                {
                    lineBotton->SetWidth(335);
                }

                for (uint i = 3; i <= 10; i++)
                {
                    var line = addon->GetNodeById(i);
                    if (line is not null)
                    {
                        line->SetXFloat(line->X + (ushort)horizontalScale + 16);
                    }
                }
                if (ingGrayBox is not null)
                {
                    ingGrayBox->SetWidth((ushort)(ingGrayBox->Width + horizontalScale + 16));
                }

                regularQuickSynthButton->SetXFloat(regularQuickSynthButton->X + horizontalScale + 16);
                regularSynthButton->SetXFloat(regularSynthButton->X + horizontalScale + 16);
                regularTrialButton->SetXFloat(regularQuickSynthButton->X - regularTrialButton->Width);

                resized = true;
            }
            catch (Exception ex)
            {
                ex.Log($"Failed to increase addon size");
            }
        }

        private void SynthesizeX()
        {
            P.Config.CraftX = synthVal == 0 ? SynthesizeableCount : synthVal;
            P.Config.CraftingX = true;
            Endurance.ToggleEndurance(true);
        }

        public void Dispose(bool onPluginShutdown)
        {
            if (onPluginShutdown)
            {
                regularRecipeNoteController?.Dispose();
                regularRecipeNoteController = null;
                moonRecipeNoteController?.Dispose();
                moonRecipeNoteController = null;
            }

            SynthesizeAll?.Dispose();
            SynthesizeAll = null;

            SynthesizeXCounter?.Dispose();
            SynthesizeXCounter = null;

            RestockXCounter?.Dispose();
            RestockXCounter = null;

            RestockButton?.Dispose();
            RestockButton = null;

            RestockLabel?.Dispose();
            RestockLabel = null;

            Crystal1Label?.Dispose();
            Crystal1Label = null;

            Crystal2Label?.Dispose();
            Crystal2Label = null;

            Ingredient1Restock?.Dispose();
            Ingredient1Restock = null;

            Ingredient2Restock?.Dispose();
            Ingredient2Restock = null;

            Ingredient3Restock?.Dispose();
            Ingredient3Restock = null;

            Ingredient4Restock?.Dispose();
            Ingredient4Restock = null;

            Ingredient5Restock?.Dispose();
            Ingredient5Restock = null;

            Ingredient6Restock?.Dispose();
            Ingredient6Restock = null;

            Ingredient1RestockSeperator?.Dispose();
            Ingredient1RestockSeperator = null;

            Ingredient2RestockSeperator?.Dispose();
            Ingredient2RestockSeperator = null;

            Ingredient3RestockSeperator?.Dispose();
            Ingredient3RestockSeperator = null;

            Ingredient4RestockSeperator?.Dispose();
            Ingredient4RestockSeperator = null;

            Ingredient5RestockSeperator?.Dispose();
            Ingredient5RestockSeperator = null;

            Ingredient6RestockSeperator?.Dispose();
            Ingredient6RestockSeperator = null;

            AToolsLabel?.Dispose();
            AToolsLabel = null;
        }
    }
}
