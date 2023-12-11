using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace Artisan.GameInterop;

// state of the normal (non-quick) crafting process
public static unsafe class QuickCrafting
{
    public static int Cur;
    public static int Max;
    public static event Action<int, int>? StateChanged;

    public static bool Completed => Cur == Max && Max > 0;

    public static void Update()
    {
        var state = GetQuickSynthState();
        if ((Cur, Max) != state)
        {
            Cur = state.cur;
            Max = state.max;
            StateChanged?.Invoke(state.cur, state.max);
        }
    }

    private static (int cur, int max) GetQuickSynthState()
    {
        var quickSynthWindow = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SynthesisSimple");
        if (quickSynthWindow == null || quickSynthWindow->UldManager.NodeListCount <= 20)
            return (0, 0);

        var curTextNode = (AtkTextNode*)quickSynthWindow->UldManager.NodeList[20];
        var maxTextNode = (AtkTextNode*)quickSynthWindow->UldManager.NodeList[18];

        var curVal = Convert.ToInt32(curTextNode->NodeText.ToString());
        var maxVal = Convert.ToInt32(maxTextNode->NodeText.ToString());
        return (curVal, maxVal);
    }
}
