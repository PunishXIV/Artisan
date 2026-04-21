using Artisan.Autocraft;
using Artisan.RawInformation;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using KamiToolKit.Premade.Node;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using TerraFX.Interop.Windows;

namespace Artisan.UI.KTK
{
    internal unsafe class NativeCraftAll
    {
        AddonController? controller;
        TextButtonNode? craftAll;
        NumericInputNode? craftXCounter;
        int val = 0;
        int craftableCount = 0;

        public NativeCraftAll()
        {
            controller = new()
            {
                AddonName = "RecipeNote",
                OnSetup = OnSetup,
                OnRefresh = OnRefresh,
                OnFinalize = OnFinalize,
            };
            controller?.Enable();
        }

        private void OnFinalize(AtkUnitBase* addon)
        {
            if (addon is null) return;
            if (addon->RootNode is null) return;
            if (craftAll is null || craftXCounter is null) return;

            craftAll?.Dispose();
            craftAll = null;

            craftXCounter?.Dispose();
            craftXCounter = null;
        }

        private void OnRefresh(AtkUnitBase* addon)
        {
            var text = addon->GetTextNodeById(78)->NodeText.ToString();
            craftableCount = text == "" ? 0 : Convert.ToInt32(text.GetNumbers());

            craftXCounter.Max = craftableCount;
            if (craftableCount == 0)
            {
                craftXCounter.IsEnabled = false;
                craftAll.IsEnabled = false;
            }
            else
            {
                craftXCounter.IsEnabled = true;
                craftAll.IsEnabled = true;
            }

            val = Math.Min(craftableCount, val);
            craftXCounter.Value = val;
        }

        private void OnSetup(AtkUnitBase* addon)
        {
            if (addon->RootNode is null) return;

            var synthButton = addon->GetNodeById(104);
            var quickSynthBtn = addon->GetNodeById(103);
            if (synthButton is null || quickSynthBtn is null) return;

            var buttonY = synthButton->Y - 30;
            var buttonX = synthButton->X;

            craftXCounter = new()
            {
                Position = new Vector2(buttonX, buttonY),
                Size = new Vector2(quickSynthBtn->Width, quickSynthBtn->Height),
                Step = 1,
                OnValueUpdate = (t) =>
                {
                    val = t;
                    craftAll?.String = val == 0 ? "Craft All" : $"Craft {t}";
                },
            };

            craftXCounter.AttachNode(quickSynthBtn, KamiToolKit.Classes.NodePosition.AfterTarget);

            buttonY = quickSynthBtn->Y - 30f.Scale();
            buttonX = quickSynthBtn->X;

            craftAll = new TextButtonNode
            {
                Position = new Vector2(buttonX, buttonY),
                Size = new Vector2(synthButton->Width, synthButton->Height),
                String = $"Craft All",
                OnClick = () => CraftX(),
                IsEnabled = true,
            };
            
            craftAll.AttachNode(synthButton, KamiToolKit.Classes.NodePosition.AfterTarget);

        }

        private void CraftX()
        {
            P.Config.CraftX = val == 0 ? craftableCount : val;
            P.Config.CraftingX = true;
            Endurance.ToggleEndurance(true);
        }

        public void Dispose()
        {
            controller?.Dispose();
            controller = null;
        }
    }
}
