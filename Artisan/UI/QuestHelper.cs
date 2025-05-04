using Artisan.Autocraft;
using Artisan.CraftingLists;
using Artisan.GameInterop;
using Artisan.QuestSync;
using Artisan.RawInformation;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
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
            if (P.Config.HideQuestHelper || (!QuestList.HasIngredientsForAny() && !QuestList.IsOnSayQuest() && !QuestList.IsOnEmoteQuest()))
                return false;

            return true;
        }

        public override void PreDraw()
        {
            if (!P.Config.DisableTheme)
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

        public unsafe override void Draw()
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

                                if (Crafting.CurState is Crafting.State.IdleNormal or Crafting.State.IdleBetween)
                                {
                                    var recipe = LuminaSheets.RecipeSheet[QuestList.GetRecipeForQuest((ushort)quest.Key)];
                                    PreCrafting.Tasks.Add((() => PreCrafting.TaskSelectRecipe(recipe), TimeSpan.FromMilliseconds(500)));
                                }
                            }
                        }
                    }

                }

            }
            bool isOnSayQuest = QuestList.IsOnSayQuest();
            if (isOnSayQuest)
            {
                ImGui.Text($"Quest Helper (click to say)");
                foreach (var quest in QuestManager.Instance()->DailyQuests)
                {
                    string message = QuestList.GetSayQuestString(quest.QuestId);
                    if (message != "")
                    {
                        if (ImGui.Button($@"Say ""{message}"""))
                        {
                            CommandProcessor.ExecuteThrottled($"/say {message}");
                        }
                    }
                }
            }
            bool isOnEmoteQuest = QuestList.IsOnEmoteQuest();
            if (isOnEmoteQuest)
            {
                ImGui.Text("Quest Helper (click to target and emote)");
                foreach (var quest in QuestManager.Instance()->DailyQuests)
                {
                    if (quest.IsCompleted) continue;

                    if (QuestList.EmoteQuests.TryGetValue(quest.QuestId, out var data))
                    {
                        if (ImGui.Button($@"Target {LuminaSheets.ENPCResidentSheet[data.NPCDataId].Singular.ExtractText()} and do {data.Emote}"))
                        {
                            QuestList.DoEmoteQuest(quest.QuestId);
                        }
                    }

                    if (quest.QuestId == 2318)
                    {
                        {
                            if (QuestList.EmoteQuests.TryGetValue(9998, out var npc1))
                            {
                                if (ImGui.Button($@"Target {LuminaSheets.ENPCResidentSheet[npc1.NPCDataId].Singular.ExtractText()} and do {npc1.Emote}"))
                                {
                                    QuestList.DoEmoteQuest(9998);
                                }
                            }

                            if (QuestList.EmoteQuests.TryGetValue(9999, out var npc2))
                            {
                                if (ImGui.Button($@"Target {LuminaSheets.ENPCResidentSheet[npc2.NPCDataId].Singular.ExtractText()} and do {npc2.Emote}"))
                                {
                                    QuestList.DoEmoteQuest(9999);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
