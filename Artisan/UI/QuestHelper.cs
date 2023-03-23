using Artisan.CraftingLists;
using Artisan.QuestSync;
using Artisan.RawInformation;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;

namespace Artisan.UI
{
    internal class QuestHelper : Window
    {
        public QuestHelper() : base("Quest Helper###QuestHelper", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar)
        {
            IsOpen = true;
            ShowCloseButton = false;
            RespectCloseHotkey = false;
        }
        public override bool DrawConditions()
        {
            if (Service.Configuration.HideQuestHelper || !QuestList.HasIngredientsForAny())
                return false;

            return true;
        }

        public override void PreDraw()
        {
            if (!P.config.DisableTheme)
            {
                P.Style.Push();
                P.StylePushed = true;
            }
        }

        public override void PostDraw()
        {
            if (P.StylePushed)
            {
                P.Style.Pop();
                P.StylePushed = false;
            }
        }

        public override void Draw()
        {
            bool hasIngredientsAny = QuestList.HasIngredientsForAny();
            if (hasIngredientsAny)
            {
                ImGui.Text($"Quest Helper (click to open recipe)");
                foreach (var quest in QuestList.Quests)
                {
                    if (QuestList.IsOnQuest((ushort)quest.Key))
                    {
                        var hasIngredients = CraftingListFunctions.HasItemsForRecipe(QuestList.GetRecipeForQuest((ushort)quest.Key));
                        if (hasIngredients)
                        {
                            if (ImGui.Button($"{((ushort)quest.Key).NameOfQuest()}"))
                            {
                                if (CraftingListFunctions.RecipeWindowOpen())
                                {
                                    CraftingListFunctions.CloseCraftingMenu();
                                    Service.Framework.RunOnTick(() => CraftingListFunctions.OpenRecipeByID(QuestList.GetRecipeForQuest((ushort)quest.Key), true), TimeSpan.FromSeconds(0.5));
                                }
                                else
                                {
                                    CraftingListFunctions.OpenRecipeByID(QuestList.GetRecipeForQuest((ushort)quest.Key));
                                }
                            }
                        }
                    }

                }

            }
        }
    }
}
