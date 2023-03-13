using Artisan.RawInformation;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;

namespace Artisan.QuestSync
{
    internal class QuestList
    {
        public static readonly Dictionary<uint, RecipeConverter> Quests = new()
        {
            //Moogle Quests
            { 2320, new() { CRP = 31591, BSM = 31617, ARM = 31643, GSM = 31669, LTW = 31695, WVR = 31721, ALC = 31747, CUL = 31773 } },
            { 2322, new() { CRP = 31592, BSM = 31618, ARM = 31644, GSM = 31670, LTW = 31696, WVR = 31722, ALC = 31748, CUL = 31774 } },
            { 2324, new() { CRP = 31593, BSM = 31619, ARM = 31645, GSM = 31671, LTW = 31697, WVR = 31723, ALC = 31749, CUL = 31775 } },
            { 2325, new() { CRP = 31594, BSM = 31620, ARM = 31646, GSM = 31672, LTW = 31698, WVR = 31724, ALC = 31750, CUL = 31776 } },
            { 2326, new() { CRP = 31595, BSM = 31621, ARM = 31647, GSM = 31673, LTW = 31699, WVR = 31725, ALC = 31751, CUL = 31777 } },
            { 2290, new() { CRP = 31596, BSM = 31622, ARM = 31648, GSM = 31674, LTW = 31700, WVR = 31726, ALC = 31752, CUL = 31778 } },
            { 2291, new() { CRP = 31597, BSM = 31623, ARM = 31649, GSM = 31675, LTW = 31701, WVR = 31727, ALC = 31753, CUL = 31779 } },
            { 2292, new() { CRP = 31598, BSM = 31624, ARM = 31650, GSM = 31676, LTW = 31702, WVR = 31728, ALC = 31754, CUL = 31780 } },
            { 2293, new() { CRP = 31599, BSM = 31625, ARM = 31651, GSM = 31677, LTW = 31703, WVR = 31729, ALC = 31755, CUL = 31781 } },
            { 2294, new() { CRP = 31600, BSM = 31626, ARM = 31652, GSM = 31678, LTW = 31704, WVR = 31730, ALC = 31756, CUL = 31782 } },
            { 2296, new() { CRP = 31601, BSM = 31627, ARM = 31653, GSM = 31679, LTW = 31705, WVR = 31731, ALC = 31757, CUL = 31783 } },
            { 2298, new() { CRP = 31602, BSM = 31628, ARM = 31654, GSM = 31680, LTW = 31706, WVR = 31732, ALC = 31758, CUL = 31784 } },
            { 2299, new() { CRP = 31603, BSM = 31629, ARM = 31655, GSM = 31681, LTW = 31707, WVR = 31733, ALC = 31759, CUL = 31785 } },
            { 2300, new() { CRP = 31604, BSM = 31630, ARM = 31656, GSM = 31682, LTW = 31708, WVR = 31734, ALC = 31760, CUL = 31786 } },
            { 2301, new() { CRP = 31605, BSM = 31631, ARM = 31657, GSM = 31683, LTW = 31709, WVR = 31735, ALC = 31761, CUL = 31787 } },
            { 2303, new() { CRP = 31606, BSM = 31632, ARM = 31658, GSM = 31684, LTW = 31710, WVR = 31736, ALC = 31762, CUL = 31788 } },
            { 2304, new() { CRP = 31607, BSM = 31633, ARM = 31659, GSM = 31685, LTW = 31711, WVR = 31737, ALC = 31763, CUL = 31789 } },
            { 2305, new() { CRP = 31608, BSM = 31634, ARM = 31660, GSM = 31686, LTW = 31712, WVR = 31738, ALC = 31764, CUL = 31790 } },
            { 2307, new() { CRP = 31609, BSM = 31635, ARM = 31661, GSM = 31687, LTW = 31713, WVR = 31739, ALC = 31765, CUL = 31791 } },
            { 2310, new() { CRP = 31610, BSM = 31636, ARM = 31662, GSM = 31688, LTW = 31714, WVR = 31740, ALC = 31766, CUL = 31792 } },
            { 2311, new() { CRP = 31611, BSM = 31637, ARM = 31663, GSM = 31689, LTW = 31715, WVR = 31741, ALC = 31767, CUL = 31793 } },
            { 2313, new() { CRP = 31612, BSM = 31638, ARM = 31664, GSM = 31690, LTW = 31716, WVR = 31742, ALC = 31768, CUL = 31794 } },
            { 2314, new() { CRP = 31613, BSM = 31639, ARM = 31665, GSM = 31691, LTW = 31717, WVR = 31743, ALC = 31769, CUL = 31795 } },
            { 2316, new() { CRP = 31614, BSM = 31640, ARM = 31666, GSM = 31692, LTW = 31718, WVR = 31744, ALC = 31770, CUL = 31796 } },
            { 2317, new() { CRP = 31615, BSM = 31641, ARM = 31667, GSM = 31693, LTW = 31719, WVR = 31745, ALC = 31771, CUL = 31797 } },
            { 2318, new() { CRP = 31616, BSM = 31642, ARM = 31668, GSM = 31694, LTW = 31720, WVR = 31746, ALC = 31772, CUL = 31798 } },

            //Lopporit Quests
            { 4681, new() { CRP = 35169, BSM = 35193, ARM = 35217, GSM = 35241, LTW = 35265, WVR = 35289, ALC = 35313, CUL = 35337 } },
            { 4682, new() { CRP = 35170, BSM = 35194, ARM = 35218, GSM = 35242, LTW = 35266, WVR = 35290, ALC = 35314, CUL = 35338 } },
            { 4683, new() { CRP = 35171, BSM = 35195, ARM = 35219, GSM = 35243, LTW = 35267, WVR = 35291, ALC = 35315, CUL = 35339 } },
            { 4684, new() { CRP = 35172, BSM = 35196, ARM = 35220, GSM = 35244, LTW = 35268, WVR = 35292, ALC = 35316, CUL = 35340 } },
            { 4685, new() { CRP = 35173, BSM = 35197, ARM = 35221, GSM = 35245, LTW = 35269, WVR = 35293, ALC = 35317, CUL = 35341 } },
            { 4687, new() { CRP = 35174, BSM = 35198, ARM = 35222, GSM = 35246, LTW = 35270, WVR = 35294, ALC = 35318, CUL = 35342 } },
            { 4688, new() { CRP = 35175, BSM = 35199, ARM = 35223, GSM = 35247, LTW = 35271, WVR = 35295, ALC = 35319, CUL = 35343 } },
            { 4689, new() { CRP = 35176, BSM = 35200, ARM = 35224, GSM = 35248, LTW = 35272, WVR = 35296, ALC = 35320, CUL = 35344 } },
            { 4691, new() { CRP = 35177, BSM = 35201, ARM = 35225, GSM = 35249, LTW = 35273, WVR = 35297, ALC = 35321, CUL = 35345 } },
            { 4692, new() { CRP = 35178, BSM = 35202, ARM = 35226, GSM = 35250, LTW = 35274, WVR = 35298, ALC = 35322, CUL = 35346 } },
            { 4693, new() { CRP = 35179, BSM = 35203, ARM = 35227, GSM = 35251, LTW = 35275, WVR = 35299, ALC = 35323, CUL = 35347 } },
            { 4695, new() { CRP = 35180, BSM = 35204, ARM = 35228, GSM = 35252, LTW = 35276, WVR = 35300, ALC = 35324, CUL = 35348 } },
            { 4696, new() { CRP = 35181, BSM = 35205, ARM = 35229, GSM = 35253, LTW = 35277, WVR = 35301, ALC = 35325, CUL = 35349 } },
            { 4698, new() { CRP = 35182, BSM = 35206, ARM = 35230, GSM = 35254, LTW = 35278, WVR = 35302, ALC = 35326, CUL = 35350 } },
            { 4699, new() { CRP = 35183, BSM = 35207, ARM = 35231, GSM = 35255, LTW = 35279, WVR = 35303, ALC = 35327, CUL = 35351 } },
            { 4701, new() { CRP = 35184, BSM = 35208, ARM = 35232, GSM = 35256, LTW = 35280, WVR = 35304, ALC = 35328, CUL = 35352 } },
            { 4702, new() { CRP = 35185, BSM = 35209, ARM = 35233, GSM = 35257, LTW = 35281, WVR = 35305, ALC = 35329, CUL = 35353 } },
            { 4703, new() { CRP = 35186, BSM = 35210, ARM = 35234, GSM = 35258, LTW = 35282, WVR = 35306, ALC = 35330, CUL = 35354 } },
            { 4705, new() { CRP = 35187, BSM = 35211, ARM = 35235, GSM = 35259, LTW = 35283, WVR = 35307, ALC = 35331, CUL = 35355 } },
            { 4706, new() { CRP = 35188, BSM = 35212, ARM = 35236, GSM = 35260, LTW = 35284, WVR = 35308, ALC = 35332, CUL = 35356 } },
            { 4708, new() { CRP = 35189, BSM = 35213, ARM = 35237, GSM = 35261, LTW = 35285, WVR = 35309, ALC = 35333, CUL = 35357 } },
            { 4710, new() { CRP = 35190, BSM = 35214, ARM = 35238, GSM = 35262, LTW = 35286, WVR = 35310, ALC = 35334, CUL = 35358 } },
            { 4712, new() { CRP = 35191, BSM = 35215, ARM = 35239, GSM = 35263, LTW = 35287, WVR = 35311, ALC = 35335, CUL = 35359 } },
            { 4713, new() { CRP = 35192, BSM = 35216, ARM = 35240, GSM = 35264, LTW = 35288, WVR = 35312, ALC = 35336, CUL = 35360 } },


        };

        public unsafe static bool IsOnQuest()
        {
            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuestsSpan)
            {
                if (quest.QuestId > 0 && !quest.IsCompleted)
                    return true;
            }

            return false;

        }

        public unsafe static bool HasIngredientsForAny()
        {
            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuestsSpan)
            {
                if (Quests.TryGetValue(quest.QuestId, out var recipe))
                {
                    if (CraftingLists.CraftingListFunctions.HasItemsForRecipe(GetRecipeForQuest(quest.QuestId)))
                        return true;
                }
            }

            foreach (var quest in qm->NormalQuestsSpan)
            {
                if (Quests.TryGetValue(quest.QuestId, out var recipe))
                {
                    if (CraftingLists.CraftingListFunctions.HasItemsForRecipe(GetRecipeForQuest(quest.QuestId)))
                        return true;
                }
            }

            return false;
        }

        public unsafe static bool IsOnQuest(ushort questId)
        {
            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuestsSpan)
            {
                if (quest.QuestId == questId && !quest.IsCompleted)
                    return true;
            }

            foreach (var quest in qm->NormalQuestsSpan)
            {
                if (quest.QuestId == questId)
                    return true;
            }

            return false;
        }

        public unsafe static uint GetRecipeForQuest(ushort questId)
        {
            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuestsSpan)
            {
                if (quest.QuestId > 0 && !quest.IsCompleted)
                {
                    if (Quests.TryGetValue(questId, out var dict))
                    {
                        switch (CharacterInfo.JobID())
                        {
                            case 8:
                                return dict.CRP;
                            case 9:
                                return dict.BSM;
                            case 10:
                                return dict.ARM;
                            case 11:
                                return dict.GSM;
                            case 12:
                                return dict.LTW;
                            case 13:
                                return dict.WVR;
                            case 14:
                                return dict.ALC;
                            case 15:
                                return dict.CUL;
                        }
                    }
                }
            }

            foreach (var quest in qm->NormalQuestsSpan)
            {
                if (quest.QuestId > 0)
                {
                    if (Quests.TryGetValue(questId, out var dict))
                    {
                        switch (CharacterInfo.JobID())
                        {
                            case 8:
                                return dict.CRP;
                            case 9:
                                return dict.BSM;
                            case 10:
                                return dict.ARM;
                            case 11:
                                return dict.GSM;
                            case 12:
                                return dict.LTW;
                            case 13:
                                return dict.WVR;
                            case 14:
                                return dict.ALC;
                            case 15:
                                return dict.CUL;
                        }
                    }
                }
            }

            return 0;
        }
    }

    public class RecipeConverter
    {
        public uint CRP;
        public uint BSM;
        public uint ARM;
        public uint GSM;
        public uint LTW;
        public uint WVR;
        public uint ALC;
        public uint CUL;
    }
}
