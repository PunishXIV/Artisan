using Artisan.Autocraft;
using Artisan.IPC;
using Artisan.RawInformation;
using ECommons;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Enums;
using KamiToolKit.Nodes;
using System;
using System.Collections.Generic;
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

        TextButtonNode? RestockButton;

        int synthVal = 0;
        int restockVal = 0;
        int SynthesizeableCount = 0;
        bool resized = false;

        float scaleSize = 64f;

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

        private void OnUpdate(AtkUnitBase* addon)
        {
            if (!P.Config.UseNativeButtons)
            {
                DisposeButtons();
            }
            else
            {
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
                        RestockXCounter.IsEnabled = false;
                    }
                    else
                    {
                        RestockButton.IsEnabled = true;
                        RestockXCounter.IsEnabled = true;
                    }
                }
            }
        }

        private void DisposeButtons()
        {
            SynthesizeAll?.Dispose();
            SynthesizeAll = null;

            SynthesizeXCounter?.Dispose();
            SynthesizeXCounter = null;

            RestockXCounter?.Dispose();
            RestockXCounter = null;

            RestockButton?.Dispose();
            RestockButton = null;
        }

        private void MoonOnSetup(AtkUnitBase* addon)
        {
            if (!P.Config.UseNativeButtons) return;
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

            DisposeButtons();
        }

        private void OnRefresh(AtkUnitBase* addon)
        {
            if (!P.Config.UseNativeButtons) return;
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
        }

        private void RegularOnSetup(AtkUnitBase* addon)
        {
            IncreaseAddonSize(addon);
            if (!P.Config.UseNativeButtons) return;
            if (addon->RootNode is null) return;

            var synthButton = addon->GetNodeById(104);
            if (synthButton is null) return;

            var synthBg = synthButton->GetAsAtkComponentButton()->ButtonBGNode->GetAsAtkNineGridNode();
            if (synthBg is null) return;

            var buttonY = synthButton->Y + (scaleSize.Scale() / 2f);
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
                    Position = new Vector2(SynthesizeAll.X, buttonY + 32f.Scale()),
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
                    Position = new Vector2(RestockButton.X - RestockButton.Width, RestockButton.Y),
                    Size = new Vector2(140, 28),
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
            }
        }

        private void RestockX()
        {
            var recipe = LuminaSheets.RecipeSheet.TryGetValue(Endurance.RecipeID, out var rec);
            if (!recipe) return;

            List<(uint itemId, int amount)> ingredients = new();
            foreach (var ingredient in rec.Ingredients())
            {
                if (ingredient.Item.RowId == 0) continue;
                if (RetainerInfo.GetRetainerItemCount(ingredient.Item.RowId) < ingredient.Amount * restockVal)
                {
                    DuoLog.Warning($"Not enough stock for {ingredient.Item.Name} in retainers. Needed: {ingredient.Amount * restockVal}, Available: {RetainerInfo.GetRetainerItemCount(ingredient.Item.RowId)}");
                    continue;
                }
                var amountToWithdraw = ingredient.Amount * restockVal;

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
                var treeList = addon->GetNodeById(39);
                var lineLeft = addon->GetNodeById(105);
                var lineRight = addon->GetNodeById(108);
                if (treeList is not null)
                {
                    var newHeight = treeList->Height + scaleSize.Scale();
                    treeList->SetHeight((ushort)(newHeight));
                }
                if (lineLeft is not null)
                {
                    lineLeft->SetWidth((ushort)(lineLeft->Width + scaleSize.Scale()));
                    lineLeft->SetYFloat(lineLeft->Y + (ushort)scaleSize.Scale());
                }
                if (lineRight is not null)
                {
                    lineRight->SetWidth((ushort)(lineRight->Width + scaleSize.Scale()));
                    lineRight->SetYFloat(lineRight->Y + (ushort)scaleSize.Scale());
                }

                addon->SetSize(width, (ushort)(height + scaleSize.Scale()));
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

        public void Dispose()
        {
            regularRecipeNoteController?.Dispose();
            regularRecipeNoteController = null;

            moonRecipeNoteController?.Dispose();
            moonRecipeNoteController = null;

            SynthesizeXCounter?.Dispose();
            SynthesizeAll?.Dispose();
        }
    }
}
