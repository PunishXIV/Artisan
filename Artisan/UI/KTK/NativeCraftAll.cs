using Artisan.Autocraft;
using Artisan.RawInformation;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using System;
using System.Numerics;

namespace Artisan.UI.KTK
{
    internal unsafe class NativeCraftAll
    {
        AddonController? regularRecipeNoteController;
        AddonController? moonRecipeNoteController;
        TextButtonNodeSynth? SynthesizeAll;
        NineGridNode? bgNode;
        NumericInputNode? SynthesizeXCounter;
        int val = 0;
        int SynthesizeableCount = 0;
        bool setup = false;

        public NativeCraftAll()
        {
            regularRecipeNoteController = new()
            {
                AddonName = "RecipeNote",
                OnSetup = RegularOnSetup,
                OnRefresh = OnRefresh,
                OnFinalize = OnFinalize,
                OnUpdate = OnUpdate
            };
            regularRecipeNoteController?.Enable();

            regularRecipeNoteController = new()
            {
                AddonName = "WKSRecipeNotebook",
                OnSetup = MoonOnSetup,
                OnRefresh = OnRefresh,
                OnFinalize = OnFinalize,
                OnUpdate = OnUpdate
            };
            regularRecipeNoteController?.Enable();
        }

        private void OnUpdate(AtkUnitBase* addon)
        {
            if (!P.Config.UseNativeButtons)
            {
                OnFinalize(addon);
            }
            else if (!setup)
            {
                if (addon->NameString == "RecipeNote")
                    RegularOnSetup(addon);
                else
                    MoonOnSetup(addon);
            }
        }

        private void MoonOnSetup(AtkUnitBase* addon)
        {
            if (!P.Config.UseNativeButtons) return;
            if (addon->RootNode is null) return;

            var synthButton = addon->GetNodeById(50);
            if (synthButton is null) return;

            var synthBg = synthButton->GetAsAtkComponentButton()->ButtonBGNode->GetAsAtkNineGridNode();

            var buttonY = synthButton->Y;
            var buttonX = synthButton->X - synthButton->Width;

            var text = addon->GetTextNodeById(34)->NodeText.ToString();
            SynthesizeableCount = text == "" ? 0 : Convert.ToInt32(text.GetNumbers());

            SynthesizeAll = new()
            {
                Position = new Vector2(buttonX, buttonY),
                Size = new Vector2(synthButton->Width, 36),
                String = $"Synthesize All",
                OnClick = () => SynthesizeX(),
                IsEnabled = SynthesizeableCount > 0,
            };

            SynthesizeAll.AttachNode(synthButton, KamiToolKit.Classes.NodePosition.AfterTarget);

            SynthesizeXCounter = new()
            {
                Position = new Vector2(SynthesizeAll.X - 90, buttonY + 3),
                Size = new Vector2(90, 28),
                OnValueUpdate = (t) =>
                {
                    val = t;
                    SynthesizeAll?.String = val == 0 ? "Craft All" : $"Craft {t}";
                },
                NodeFlags = synthButton->NodeFlags,
                Max = SynthesizeableCount,
                IsEnabled = SynthesizeableCount > 0,
            };

            SynthesizeXCounter.AttachNode(SynthesizeAll, KamiToolKit.Classes.NodePosition.AfterTarget);
            setup = true;
        }

        private void OnFinalize(AtkUnitBase* addon)
        {
            if (addon is null) return;
            if (addon->RootNode is null) return;
            if (SynthesizeAll is null || SynthesizeXCounter is null) return;

            SynthesizeAll?.Dispose();
            SynthesizeAll = null;

            SynthesizeXCounter?.Dispose();
            SynthesizeXCounter = null;

            setup = false;
        }

        private void OnRefresh(AtkUnitBase* addon)
        {
            var text = addon->NameString == "WKSRecipeNotebook" ? addon->GetTextNodeById(34)->NodeText.ToString() : addon->GetTextNodeById(78)->NodeText.ToString();
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

            val = Math.Min(SynthesizeableCount, val);
            SynthesizeXCounter.Value = val;
        }

        private void RegularOnSetup(AtkUnitBase* addon)
        {
            if (!P.Config.UseNativeButtons) return;
            if (addon->RootNode is null) return;

            var synthButton = addon->GetNodeById(104);
            var synthBg = synthButton->GetAsAtkComponentButton()->ButtonBGNode->GetAsAtkNineGridNode();
            if (synthButton is null || synthBg is null) return;

            var buttonY = synthButton->Y - 32;
            var buttonX = synthButton->X;

            var text = addon->GetTextNodeById(78)->NodeText.ToString();
            SynthesizeableCount = text == "" ? 0 : Convert.ToInt32(text.GetNumbers());

            SynthesizeAll = new()
            {
                Position = new Vector2(buttonX, buttonY),
                Size = new Vector2(synthBg->Width, synthBg->Height),
                String = $"Synthesize All",
                OnClick = () => SynthesizeX(),
                IsEnabled = SynthesizeableCount > 0,
            };

            SynthesizeAll.AttachNode(synthButton, KamiToolKit.Classes.NodePosition.AfterTarget);

            SynthesizeXCounter = new()
            {
                Position = new Vector2(SynthesizeAll.X - SynthesizeAll.Width, buttonY + 5),
                Size = new Vector2(140, 28),
                Step = 1,
                OnValueUpdate = (t) =>
                {
                    val = t;
                    SynthesizeAll?.String = val == 0 ? "Craft All" : $"Craft {t}";
                },
                Max = SynthesizeableCount,
                IsEnabled = SynthesizeableCount > 0,
            };

            SynthesizeXCounter.AttachNode(SynthesizeAll, KamiToolKit.Classes.NodePosition.AfterTarget);
            setup = true;
        }

        private void SynthesizeX()
        {
            P.Config.CraftX = val == 0 ? SynthesizeableCount : val;
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
