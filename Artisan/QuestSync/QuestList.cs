using Artisan.Autocraft;
using Artisan.RawInformation.Character;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;
using System.Linq;

namespace Artisan.QuestSync
{
    internal class QuestList
    {
        public static readonly Dictionary<uint, RecipeConverter> Quests = new()
        {
            //Ixal Quests
            { 1494, new() { CRP = 30511, BSM = 30550, ARM = 30589, GSM = 30628, LTW = 30667, WVR = 30706, ALC = 30745, CUL = 30784 } },
            { 1495, new() { CRP = 30512, BSM = 30551, ARM = 30590, GSM = 30629, LTW = 30668, WVR = 30707, ALC = 30746, CUL = 30785 } },
            { 1496, new() { CRP = 30513, BSM = 30552, ARM = 30591, GSM = 30630, LTW = 30669, WVR = 30708, ALC = 30747, CUL = 30786 } },
            { 1497, new() { CRP = 30514, BSM = 30553, ARM = 30592, GSM = 30631, LTW = 30670, WVR = 30709, ALC = 30748, CUL = 30787 } },
            { 1504, new() { CRP = 30515, BSM = 30554, ARM = 30593, GSM = 30632, LTW = 30671, WVR = 30710, ALC = 30749, CUL = 30788 } },
            { 1505, new() { CRP = 30516, BSM = 30555, ARM = 30594, GSM = 30633, LTW = 30672, WVR = 30711, ALC = 30750, CUL = 30789 } },
            { 1506, new() { CRP = 30517, BSM = 30556, ARM = 30595, GSM = 30634, LTW = 30673, WVR = 30712, ALC = 30751, CUL = 30790 } },
            { 1507, new() { CRP = 30518, BSM = 30557, ARM = 30596, GSM = 30635, LTW = 30674, WVR = 30713, ALC = 30752, CUL = 30791 } },
            { 1508, new() { CRP = 30519, BSM = 30558, ARM = 30597, GSM = 30636, LTW = 30675, WVR = 30714, ALC = 30753, CUL = 30792 } },
            { 1514, new() { CRP = 30520, BSM = 30559, ARM = 30598, GSM = 30637, LTW = 30676, WVR = 30715, ALC = 30754, CUL = 30793 } },
            { 1515, new() { CRP = 30521, BSM = 30560, ARM = 30599, GSM = 30638, LTW = 30677, WVR = 30716, ALC = 30755, CUL = 30794 } },
            { 1516, new() { CRP = 30522, BSM = 30561, ARM = 30600, GSM = 30639, LTW = 30678, WVR = 30717, ALC = 30756, CUL = 30795 } },
            { 1517, new() { CRP = 30523, BSM = 30562, ARM = 30601, GSM = 30640, LTW = 30679, WVR = 30718, ALC = 30757, CUL = 30796 } },
            { 1518, new() { CRP = 30524, BSM = 30563, ARM = 30602, GSM = 30641, LTW = 30680, WVR = 30719, ALC = 30758, CUL = 30797 } },
            { 1498, new() { CRP = 30525, BSM = 30564, ARM = 30603, GSM = 30642, LTW = 30681, WVR = 30720, ALC = 30759, CUL = 30798 } },
            { 1499, new() { CRP = 30526, BSM = 30565, ARM = 30604, GSM = 30643, LTW = 30682, WVR = 30721, ALC = 30760, CUL = 30799 } },
            { 1500, new() { CRP = 30527, BSM = 30566, ARM = 30605, GSM = 30644, LTW = 30683, WVR = 30722, ALC = 30761, CUL = 30800 } },
            { 1501, new() { CRP = 30528, BSM = 30567, ARM = 30606, GSM = 30645, LTW = 30684, WVR = 30723, ALC = 30762, CUL = 30801 } },
            { 1502, new() { CRP = 30529, BSM = 30568, ARM = 30607, GSM = 30646, LTW = 30685, WVR = 30724, ALC = 30763, CUL = 30802 } },
            { 1503, new() { CRP = 30530, BSM = 30569, ARM = 30608, GSM = 30647, LTW = 30686, WVR = 30725, ALC = 30764, CUL = 30803 } },
            { 1509, new() { CRP = 30531, BSM = 30570, ARM = 30609, GSM = 30648, LTW = 30687, WVR = 30726, ALC = 30765, CUL = 30804 } },
            { 1510, new() { CRP = 30532, BSM = 30571, ARM = 30610, GSM = 30649, LTW = 30688, WVR = 30727, ALC = 30766, CUL = 30805 } },
            { 1511, new() { CRP = 30533, BSM = 30572, ARM = 30611, GSM = 30650, LTW = 30689, WVR = 30728, ALC = 30767, CUL = 30806 } },
            { 1512, new() { CRP = 30534, BSM = 30573, ARM = 30612, GSM = 30651, LTW = 30690, WVR = 30729, ALC = 30768, CUL = 30807 } },
            { 1513, new() { CRP = 30535, BSM = 30574, ARM = 30613, GSM = 30652, LTW = 30691, WVR = 30730, ALC = 30769, CUL = 30808 } },
            { 1519, new() { CRP = 30536, BSM = 30575, ARM = 30614, GSM = 30653, LTW = 30692, WVR = 30731, ALC = 30770, CUL = 30809 } },
            { 1520, new() { CRP = 30537, BSM = 30576, ARM = 30615, GSM = 30654, LTW = 30693, WVR = 30732, ALC = 30771, CUL = 30810 } },
            { 1521, new() { CRP = 30538, BSM = 30577, ARM = 30616, GSM = 30655, LTW = 30694, WVR = 30733, ALC = 30772, CUL = 30811 } },
            { 1522, new() { CRP = 30539, BSM = 30578, ARM = 30617, GSM = 30656, LTW = 30695, WVR = 30734, ALC = 30773, CUL = 30812 } },
            { 1523, new() { CRP = 30540, BSM = 30579, ARM = 30618, GSM = 30657, LTW = 30696, WVR = 30735, ALC = 30774, CUL = 30813 } },
            { 1566, new() { CRP = 30541, BSM = 30580, ARM = 30619, GSM = 30658, LTW = 30697, WVR = 30736, ALC = 30775, CUL = 30814 } },
            { 1567, new() { CRP = 30542, BSM = 30581, ARM = 30620, GSM = 30659, LTW = 30698, WVR = 30737, ALC = 30776, CUL = 30815 } },
            { 1568, new() { CRP = 30543, BSM = 30582, ARM = 30621, GSM = 30660, LTW = 30699, WVR = 30738, ALC = 30777, CUL = 30816 } },
            { 1487, new() { CRP = 30544, BSM = 30583, ARM = 30622, GSM = 30661, LTW = 30700, WVR = 30739, ALC = 30778, CUL = 30817 } },
            { 1488, new() { CRP = 30545, BSM = 30584, ARM = 30623, GSM = 30662, LTW = 30701, WVR = 30740, ALC = 30779, CUL = 30818 } },
            { 1489, new() { CRP = 30546, BSM = 30585, ARM = 30624, GSM = 30663, LTW = 30702, WVR = 30741, ALC = 30780, CUL = 30819 } },
            { 1491, new() { CRP = 30547, BSM = 30586, ARM = 30625, GSM = 30664, LTW = 30703, WVR = 30742, ALC = 30781, CUL = 30820 } },
            { 9998, new() { CRP = 30548, BSM = 30587, ARM = 30626, GSM = 30665, LTW = 30704, WVR = 30743, ALC = 30782, CUL = 30821 } }, //Exception required for these last 2
            { 9999, new() { CRP = 30549, BSM = 30588, ARM = 30627, GSM = 30666, LTW = 30705, WVR = 30744, ALC = 30783, CUL = 30822 } },


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

            //Namazu Quests
            { 3097, new() { CRP = 32682, BSM = 32709, ARM = 32736, GSM = 32763, LTW = 32790, WVR = 32817, ALC = 32844, CUL = 32871 } },
            { 3098, new() { CRP = 32683, BSM = 32710, ARM = 32737, GSM = 32764, LTW = 32791, WVR = 32818, ALC = 32845, CUL = 32872 } },
            { 3099, new() { CRP = 32684, BSM = 32711, ARM = 32738, GSM = 32765, LTW = 32792, WVR = 32819, ALC = 32846, CUL = 32873 } },
            { 3112, new() { CRP = 32685, BSM = 32712, ARM = 32739, GSM = 32766, LTW = 32793, WVR = 32820, ALC = 32847, CUL = 32874 } },
            { 3117, new() { CRP = 32686, BSM = 32713, ARM = 32740, GSM = 32767, LTW = 32794, WVR = 32821, ALC = 32848, CUL = 32875 } },
            { 3119, new() { CRP = 32687, BSM = 32714, ARM = 32741, GSM = 32768, LTW = 32795, WVR = 32822, ALC = 32849, CUL = 32876 } },
            { 3122, new() { CRP = 32688, BSM = 32715, ARM = 32742, GSM = 32769, LTW = 32796, WVR = 32823, ALC = 32850, CUL = 32877 } },
            { 3100, new() { CRP = 32689, BSM = 32716, ARM = 32743, GSM = 32770, LTW = 32797, WVR = 32824, ALC = 32851, CUL = 32878 } },
            { 3116, new() { CRP = 32690, BSM = 32717, ARM = 32744, GSM = 32771, LTW = 32798, WVR = 32825, ALC = 32852, CUL = 32879 } },
            { 3126, new() { CRP = 32691, BSM = 32718, ARM = 32745, GSM = 32772, LTW = 32799, WVR = 32826, ALC = 32853, CUL = 32880 } },
            { 3128, new() { CRP = 32692, BSM = 32719, ARM = 32746, GSM = 32773, LTW = 32800, WVR = 32827, ALC = 32854, CUL = 32881 } },
            { 3130, new() { CRP = 32693, BSM = 32720, ARM = 32747, GSM = 32774, LTW = 32801, WVR = 32828, ALC = 32855, CUL = 32882 } },
            { 3101, new() { CRP = 32694, BSM = 32721, ARM = 32748, GSM = 32775, LTW = 32802, WVR = 32829, ALC = 32856, CUL = 32883 } },
            { 3103, new() { CRP = 32695, BSM = 32722, ARM = 32749, GSM = 32776, LTW = 32803, WVR = 32830, ALC = 32857, CUL = 32884 } },
            { 3105, new() { CRP = 32696, BSM = 32723, ARM = 32750, GSM = 32777, LTW = 32804, WVR = 32831, ALC = 32858, CUL = 32885 } },
            { 3106, new() { CRP = 32697, BSM = 32724, ARM = 32751, GSM = 32778, LTW = 32805, WVR = 32832, ALC = 32859, CUL = 32886 } },
            { 3107, new() { CRP = 32698, BSM = 32725, ARM = 32752, GSM = 32779, LTW = 32806, WVR = 32833, ALC = 32860, CUL = 32887 } },
            { 3108, new() { CRP = 32699, BSM = 32726, ARM = 32753, GSM = 32780, LTW = 32807, WVR = 32834, ALC = 32861, CUL = 32888 } },
            { 3109, new() { CRP = 32700, BSM = 32727, ARM = 32754, GSM = 32781, LTW = 32808, WVR = 32835, ALC = 32862, CUL = 32889 } },
            { 3110, new() { CRP = 32701, BSM = 32728, ARM = 32755, GSM = 32782, LTW = 32809, WVR = 32836, ALC = 32863, CUL = 32890 } },
            { 3111, new() { CRP = 32702, BSM = 32729, ARM = 32756, GSM = 32783, LTW = 32810, WVR = 32837, ALC = 32864, CUL = 32891 } },
            { 3113, new() { CRP = 32703, BSM = 32730, ARM = 32757, GSM = 32784, LTW = 32811, WVR = 32838, ALC = 32865, CUL = 32892 } },
            { 3114, new() { CRP = 32704, BSM = 32731, ARM = 32758, GSM = 32785, LTW = 32812, WVR = 32839, ALC = 32866, CUL = 32893 } },
            { 3120, new() { CRP = 32705, BSM = 32732, ARM = 32759, GSM = 32786, LTW = 32813, WVR = 32840, ALC = 32867, CUL = 32894 } },
            { 3123, new() { CRP = 32706, BSM = 32733, ARM = 32760, GSM = 32787, LTW = 32814, WVR = 32841, ALC = 32868, CUL = 32895 } },
            { 3125, new() { CRP = 32707, BSM = 32734, ARM = 32761, GSM = 32788, LTW = 32815, WVR = 32842, ALC = 32869, CUL = 32896 } },
            { 3129, new() { CRP = 32708, BSM = 32735, ARM = 32762, GSM = 32789, LTW = 32816, WVR = 32843, ALC = 32870, CUL = 32897 } },

            //Dwarf Quests
            { 3896, new() { CRP = 34167, BSM = 34191, ARM = 34215, GSM = 34239, LTW = 34263, WVR = 34287, ALC = 34311, CUL = 34335 } },
            { 3897, new() { CRP = 34168, BSM = 34192, ARM = 34216, GSM = 34240, LTW = 34264, WVR = 34288, ALC = 34312, CUL = 34336 } },
            { 3898, new() { CRP = 34169, BSM = 34193, ARM = 34217, GSM = 34241, LTW = 34265, WVR = 34289, ALC = 34313, CUL = 34337 } },
            { 3899, new() { CRP = 34170, BSM = 34194, ARM = 34218, GSM = 34242, LTW = 34266, WVR = 34290, ALC = 34314, CUL = 34338 } },
            { 3900, new() { CRP = 34171, BSM = 34195, ARM = 34219, GSM = 34243, LTW = 34267, WVR = 34291, ALC = 34315, CUL = 34339 } },
            { 3902, new() { CRP = 34172, BSM = 34196, ARM = 34220, GSM = 34244, LTW = 34268, WVR = 34292, ALC = 34316, CUL = 34340 } },
            { 3903, new() { CRP = 34173, BSM = 34197, ARM = 34221, GSM = 34245, LTW = 34269, WVR = 34293, ALC = 34317, CUL = 34341 } },
            { 3904, new() { CRP = 34174, BSM = 34198, ARM = 34222, GSM = 34246, LTW = 34270, WVR = 34294, ALC = 34318, CUL = 34342 } },
            { 3906, new() { CRP = 34175, BSM = 34199, ARM = 34223, GSM = 34247, LTW = 34271, WVR = 34295, ALC = 34319, CUL = 34343 } },
            { 3907, new() { CRP = 34176, BSM = 34200, ARM = 34224, GSM = 34248, LTW = 34272, WVR = 34296, ALC = 34320, CUL = 34344 } },
            { 3908, new() { CRP = 34177, BSM = 34201, ARM = 34225, GSM = 34249, LTW = 34273, WVR = 34297, ALC = 34321, CUL = 34345 } },
            { 3910, new() { CRP = 34178, BSM = 34202, ARM = 34226, GSM = 34250, LTW = 34274, WVR = 34298, ALC = 34322, CUL = 34346 } },
            { 3911, new() { CRP = 34179, BSM = 34203, ARM = 34227, GSM = 34251, LTW = 34275, WVR = 34299, ALC = 34323, CUL = 34347 } },
            { 3913, new() { CRP = 34180, BSM = 34204, ARM = 34228, GSM = 34252, LTW = 34276, WVR = 34300, ALC = 34324, CUL = 34348 } },
            { 3914, new() { CRP = 34181, BSM = 34205, ARM = 34229, GSM = 34253, LTW = 34277, WVR = 34301, ALC = 34325, CUL = 34349 } },
            { 3915, new() { CRP = 34182, BSM = 34206, ARM = 34230, GSM = 34254, LTW = 34278, WVR = 34302, ALC = 34326, CUL = 34350 } },
            { 3917, new() { CRP = 34183, BSM = 34207, ARM = 34231, GSM = 34255, LTW = 34279, WVR = 34303, ALC = 34327, CUL = 34351 } },
            { 3918, new() { CRP = 34184, BSM = 34208, ARM = 34232, GSM = 34256, LTW = 34280, WVR = 34304, ALC = 34328, CUL = 34352 } },
            { 3920, new() { CRP = 34185, BSM = 34209, ARM = 34233, GSM = 34257, LTW = 34281, WVR = 34305, ALC = 34329, CUL = 34353 } },
            { 3921, new() { CRP = 34186, BSM = 34210, ARM = 34234, GSM = 34258, LTW = 34282, WVR = 34306, ALC = 34330, CUL = 34354 } },
            { 3924, new() { CRP = 34187, BSM = 34211, ARM = 34235, GSM = 34259, LTW = 34283, WVR = 34307, ALC = 34331, CUL = 34355 } },
            { 3925, new() { CRP = 34188, BSM = 34212, ARM = 34236, GSM = 34260, LTW = 34284, WVR = 34308, ALC = 34332, CUL = 34356 } },
            { 3927, new() { CRP = 34189, BSM = 34213, ARM = 34237, GSM = 34261, LTW = 34285, WVR = 34309, ALC = 34333, CUL = 34357 } },
            { 3928, new() { CRP = 34190, BSM = 34214, ARM = 34238, GSM = 34262, LTW = 34286, WVR = 34310, ALC = 34334, CUL = 34358 } },

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

        public static readonly Dictionary<uint, EmoteConverter> EmoteQuests = new Dictionary<uint, EmoteConverter>()
        {
            { 3919, new() { NPCDataId = 1033697, Emote = "/psych" } },
            { 4690, new() { NPCDataId = 1044568, Emote = "/dance" } },
            { 9998, new() { NPCDataId = 1017624, Emote = "/psych" } }, //2318
            { 9999, new() { NPCDataId = 1017625, Emote = "/slap" } }, //2318
        };

        public unsafe static bool HasIngredientsForAny()
        {
            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuests)
            {
                if (quest.IsCompleted) continue;

                if (Quests.TryGetValue(quest.QuestId, out var recipe))
                {
                    if (CraftingLists.CraftingListFunctions.HasItemsForRecipe(GetRecipeForQuest(quest.QuestId)))
                        return true;
                }
            }

            foreach (var quest in qm->NormalQuests)
            {
                if (quest.QuestId == 1493)
                {
                    var step1 = Quests[9998];
                    var step2 = Quests[9999];

                    if (CraftingLists.CraftingListFunctions.HasItemsForRecipe(step1.CRP))
                        return true;

                    if (CraftingLists.CraftingListFunctions.HasItemsForRecipe(step2.CRP))
                        return true;
                }

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
            if (questId == 9999 || questId == 9998)
                questId = 1493;

            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuests)
            {
                if (quest.QuestId == questId && !quest.IsCompleted)
                    return true;
            }

            foreach (var quest in qm->NormalQuests)
            {
                if (quest.QuestId == questId)
                    return true;
            }

            return false;
        }

        public unsafe static uint GetRecipeForQuest(ushort questId)
        {
            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuests)
            {
                if (quest.QuestId > 0 && !quest.IsCompleted)
                {
                    if (Quests.TryGetValue(questId, out var dict))
                    {
                        return dict.ForJob(CharacterInfo.JobID);
                    }
                }
            }

            foreach (var quest in qm->NormalQuests)
            {
                if (quest.QuestId == 1493)
                {
                    var step1 = Quests[9998];
                    var step2 = Quests[9999];

                    if (CraftingLists.CraftingListFunctions.HasItemsForRecipe(step1.CRP))
                    {
                        return step1.ForJob(CharacterInfo.JobID);
                    }

                    if (CraftingLists.CraftingListFunctions.HasItemsForRecipe(step2.CRP))
                    {
                        return step2.ForJob(CharacterInfo.JobID);
                    }
                }

                if (quest.QuestId > 0)
                {
                    if (Quests.TryGetValue(questId, out var dict))
                    {
                        return dict.ForJob(CharacterInfo.JobID);
                    }
                }
            }

            return 0;
        }

        internal unsafe static bool IsOnSayQuest()
        {
            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuests)
            {
                if (!quest.IsCompleted)
                {
                    if (quest.QuestId == 2295) return true;
                    if (quest.QuestId == 3909) return true;
                    if (quest.QuestId == 4700) return true;
                    if (quest.QuestId == 1497) return true;
                    if (quest.QuestId == 3104) return true;
                    if (quest.QuestId == 1515) return true;
                    if (quest.QuestId == 1507) return true;
                    if (quest.QuestId == 1501) return true;
                    if (quest.QuestId == 1568) return true;
                }
            }

            return false;
        }
        internal unsafe static bool IsOnEmoteQuest()
        {
            QuestManager* qm = QuestManager.Instance();
            foreach (var quest in qm->DailyQuests)
            {
                if (!quest.IsCompleted)
                {
                    if (quest.QuestId == 4690) return true;
                    if (quest.QuestId == 3919) return true;
                    if (quest.QuestId == 2318) return true;
                }
            }

            return false;
        }

        internal unsafe static void DoEmoteQuest(ushort questId)
        {
            if (EmoteQuests.TryGetValue(questId, out var data))
            {
                if (Svc.Objects.Any(x => x.DataId == data.NPCDataId))
                {
                    var npc = Svc.Objects.First(x => x.DataId == data.NPCDataId);
                    Svc.Targets.Target = npc;
                }
                if (Svc.Targets.Target != null && Svc.Targets.Target.DataId == data.NPCDataId)
                {
                    CommandProcessor.ExecuteThrottled(data.Emote!);
                }
            }
        }

        internal unsafe static string GetSayQuestString(ushort questId)
        {
            foreach (var quest in QuestManager.Instance()->DailyQuests)
            {
                if (quest.IsCompleted && quest.QuestId == questId) return "";
            }
            if (questId == 2295)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.English:
                        return "free kupo nuts";
                    case Dalamud.Game.ClientLanguage.French:
                        return "Je sais où trouver des noix de kupo";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Kupo-Nüsse für alle Helfer!";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "クポの実あるよ";
                }

            }
            if (questId == 3909)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.French:
                    case Dalamud.Game.ClientLanguage.English:
                        return "lali-ho";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Holladrio";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "ラリホー";
                }
            }
            if (questId == 4700)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.English:
                        return "dream bigger";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "ドリームマシマシ";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Traummaschine";
                    case Dalamud.Game.ClientLanguage.French:
                        return "rêves à gogo";
                }
            }
            if (questId == 1497)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.English:
                        return "With the Wind";
                    case Dalamud.Game.ClientLanguage.French:
                        return "Tel le vent";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Sei eins mit dem Wind!";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "風のごとく！";
                }
            }
            if (questId == 3104)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.English:
                        return "his whiskers";
                    case Dalamud.Game.ClientLanguage.French:
                        return "la gloire de la grande frairie";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Große Flosse";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "おおなまずのまにまに";
                }
            }
            if (questId == 1515)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.English:
                        return "Now Fall";
                    case Dalamud.Game.ClientLanguage.French:
                        return "Nous nous envolerons";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Ab durch die Wolken!";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "天を翔ける！";
                }
            }
            if (questId == 1507)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.English:
                        return "High as Honor";
                    case Dalamud.Game.ClientLanguage.French:
                        return "Haut dans le ciel";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Hoch hinaus!";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "もっと高く！";
                }
            }
            if (questId == 1501)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.English:
                        return "Wings Unbending";
                    case Dalamud.Game.ClientLanguage.French:
                        return "Ayatlan, terre sacrée";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Mögen deine Schwingen nie brechen!";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "折れぬ翼を！";
                }
            }
            if (questId == 1568)
            {
                switch (Svc.ClientState.ClientLanguage)
                {
                    case Dalamud.Game.ClientLanguage.English:
                        return "Amid the Flowers";
                    case Dalamud.Game.ClientLanguage.French:
                        return "Bientôt nous retrouverons";
                    case Dalamud.Game.ClientLanguage.German:
                        return "Der unerfüllte Traum der Ixal!";
                    case Dalamud.Game.ClientLanguage.Japanese:
                        return "果てぬ夢を！";
                }
            }


            return "";
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

        public uint ForJob(Job job) => job switch
        {
            Job.CRP => CRP,
            Job.BSM => BSM,
            Job.ARM => ARM,
            Job.GSM => GSM,
            Job.LTW => LTW,
            Job.WVR => WVR,
            Job.ALC => ALC,
            Job.CUL => CUL,
            _ => 0
        };
    }

    public class EmoteConverter
    {
        public uint NPCDataId;
        public string? Emote;
    }
}
