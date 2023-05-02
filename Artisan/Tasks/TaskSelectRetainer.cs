using Artisan.RawInformation;
using ClickLib.Clicks;
using ClickLib.Enums;
using ClickLib.Structures;
using Dalamud.Configuration;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Dalamud.Utility;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Events;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using static ECommons.GenericHelpers;
using MemoryHelper = Dalamud.Memory.MemoryHelper;


namespace Artisan.Tasks;

internal static class TaskSelectRetainer
{
    internal static void EnqueueRetainer(this TaskManager TM, ulong id)
    {
        TM.Enqueue(() => RetainerListHandlers.SelectRetainerByID(id));
        TM.Enqueue(() => RetainerListHandlers.TryGetCurrentRetainer(out _));
    }
}

internal unsafe static class RetainerListHandlers
{
    internal static bool? SelectRetainerByName(string name)
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var retainerList) && IsAddonReady(retainerList))
        {
            var list = (AtkComponentNode*)retainerList->UldManager.NodeList[2];
            for (var i = 1u; i < RetainerInfo.retainerManager.Count + 1; i++)
            {
                var retainerEntry = (AtkComponentNode*)list->Component->UldManager.NodeList[i];
                var text = (AtkTextNode*)retainerEntry->Component->UldManager.NodeList[13];
                var nodeName = text->NodeText.ToString();
                //P.DebugLog($"Retainer {i} text {nodeName}");
                if (name == nodeName)
                {
                    if (RetainerInfo.GenericThrottle)
                    {
                        ClickRetainerList.Using((IntPtr)retainerList).Select(list, retainerEntry, i - 1);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    internal static bool? SelectRetainerByID(ulong id)
    {
        string retainerName = "";
        for (uint i = 0; i < 10; i++)
        {
            var retainer = FFXIVClientStructs.FFXIV.Client.Game.RetainerManager.Instance()->GetRetainerBySortedIndex(i);
            if (retainer == null) continue;

            if (retainer->RetainerID == id)
                retainerName = MemoryHelper.ReadSeStringNullTerminated((IntPtr)retainer->Name).ExtractText();

        }
        if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var retainerList) && IsAddonReady(retainerList))
        {
            var list = (AtkComponentNode*)retainerList->UldManager.NodeList[2];
            for (var i = 1u; i < RetainerInfo.retainerManager.Count + 1; i++)
            {
                var retainerEntry = (AtkComponentNode*)list->Component->UldManager.NodeList[i];
                var text = (AtkTextNode*)retainerEntry->Component->UldManager.NodeList[13];
                var nodeName = text->NodeText.ToString();
                if (retainerName == nodeName)
                {
                    if (RetainerInfo.GenericThrottle)
                    {
                        ClickRetainerList.Using((IntPtr)retainerList).Select(list, retainerEntry, i - 1);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    internal static bool? CloseRetainerList()
    {
        if (TryGetAddonByName<AtkUnitBase>("RetainerList", out var retainerList) && IsAddonReady(retainerList))
        {
            if (RetainerInfo.GenericThrottle)
            {
                var v = stackalloc AtkValue[1]
                {
                    new()
                    {
                        Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
                        Int = -1
                    }
                };

                retainerList->FireCallback(1, v);
                return true;
            }
        }
        return false;
    }

    internal static bool TryGetCurrentRetainer(out string name)
    {
        if (Svc.Condition[ConditionFlag.OccupiedSummoningBell] && ProperOnLogin.PlayerPresent && Svc.Objects.Where(x => x.ObjectKind == ObjectKind.Retainer).OrderBy(x => Vector3.Distance(Svc.ClientState.LocalPlayer.Position, x.Position)).TryGetFirst(out var obj))
        {
            name = obj.Name.ToString();
            return true;
        }
        name = default;
        return false;
    }
}

public unsafe class RetainerManager
{
    private static StaticRetainerContainer _address;
    private static RetainerContainer* _container;

    public RetainerManager(SigScanner sigScanner)
    {
        if (_address != null)
            return;

        _address ??= new StaticRetainerContainer(sigScanner);
        _container = (RetainerContainer*)_address.Address;
    }

    public bool Ready
        => _container != null && _container->Ready == 1;

    public int Count
        => Ready ? _container->RetainerCount : 0;

    public SeRetainer Retainer(int which)
        => which < Count
            ? ((SeRetainer*)_container->Retainers)[which]
            : throw new ArgumentOutOfRangeException($"Invalid retainer {which} requested, only {Count} available.");
    public void* RetainerAddress(int which)
        => which < Count
            ? &((SeRetainer*)_container->Retainers)[which]
            : throw new ArgumentOutOfRangeException($"Invalid retainer {which} requested, only {Count} available.");
}

public sealed class StaticRetainerContainer : SeAddressBase
{
    public StaticRetainerContainer(SigScanner sigScanner)
        : base(sigScanner, "48 8B E9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 85 C0 74 4E")
    { }
}

public class SeAddressBase
{
    public readonly IntPtr Address;

    public SeAddressBase(SigScanner sigScanner, string signature, int offset = 0)
    {
        Address = sigScanner.GetStaticAddressFromSig(signature);
        if (Address != IntPtr.Zero)
            Address += offset;
        var baseOffset = (ulong)Address.ToInt64() - (ulong)sigScanner.Module.BaseAddress.ToInt64();
    }
}

[StructLayout(LayoutKind.Sequential, Size = SeRetainer.Size * 10 + 12)]
public unsafe struct RetainerContainer
{
    public fixed byte Retainers[SeRetainer.Size * 10];
    public fixed byte DisplayOrder[10];
    public byte Ready;
    public byte RetainerCount;
}

[StructLayout(LayoutKind.Explicit, Size = Size)]
public unsafe struct SeRetainer
{
    public const int Size = 0x48;

    [FieldOffset(0x00)]
    public ulong RetainerID;

    [FieldOffset(0x08)]
    private fixed byte _name[0x20];

    [FieldOffset(0x29)]
    public byte ClassJob;

    [FieldOffset(0x2A)]
    public byte Level;

    [FieldOffset(0x2C)]
    public uint Gil;

    [FieldOffset(0x38)]
    public uint VentureID;

    [FieldOffset(0x3C)]
    public uint VentureCompleteTimeStamp;

    public bool Available
        => ClassJob != 0;

    public SeString Name
    {
        get
        {
            fixed (byte* name = _name)
            {
                return MemoryHelper.ReadSeStringNullTerminated((IntPtr)name);
            }
        }
    }
}

internal unsafe class ClickRetainerList : ClickLib.Bases.ClickBase<ClickRetainerList, AtkUnitBase>
{
    public ClickRetainerList(IntPtr addon = default)
        : base("RetainerList", addon)
    {
    }
    public static implicit operator ClickRetainerList(IntPtr addon) => new(addon);

    public static ClickRetainerList Using(IntPtr addon) => new(addon);

    public void Select(void* list, AtkComponentNode* target, uint index)
    {
        var data = InputData.Empty();
        data.Data[0] = target;
        data.Data[2] = (void*)(index | (ulong)index << 48);
        ClickAddonComponent(target, 1, EventType.LIST_INDEX_CHANGE, null, data);
    }
}

internal unsafe class ClickButtonGeneric : ClickLib.Bases.ClickBase<ClickButtonGeneric, AtkUnitBase>
{
    internal string Name;
    public ClickButtonGeneric(void* addon, string name)
    : base(name, (nint)addon)
    {
        Name = name;
    }

    public void Click(void* target, uint which = 0)
    {
        ClickAddonButton((AtkComponentButton*)target, which);
    }
}

internal unsafe static class RetainerHandlers
{
    internal static bool? SelectQuit()
    {
        var text = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Addon>().GetRow(2383).Text.ToDalamudString().ExtractText();
        return TrySelectSpecificEntry(text);
    }

    internal static bool? SelectEntrustItems()
    {
        //2378	Entrust or withdraw items.
        var text = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Addon>().GetRow(2378).Text.ToDalamudString().ExtractText();
        return TrySelectSpecificEntry(text);
    }

    internal static bool? OpenItemContextMenu(uint itemId, out uint quantity)
    {
        quantity = 0;
        var inventories = new List<InventoryType>
        {
            InventoryType.RetainerPage1,
            InventoryType.RetainerPage2,
            InventoryType.RetainerPage3,
            InventoryType.RetainerPage4,
            InventoryType.RetainerPage5,
            InventoryType.RetainerPage6,
            InventoryType.RetainerPage7,
            InventoryType.RetainerCrystals
        };

        foreach (var inv in inventories)
        {
            //PluginLog.Debug($"RETAINER PAGE {inv} WITH SIZE {InventoryManager.Instance()->GetInventoryContainer(inv)->Size}");
            for (int i = 0; i < InventoryManager.Instance()->GetInventoryContainer(inv)->Size; i++)
            {
                var item = InventoryManager.Instance()->GetInventoryContainer(inv)->GetInventorySlot(i);
                //PluginLog.Debug($"ITEM {item->ItemID.NameOfItem()} IN {item->Slot}");
                if (item->ItemID == itemId)
                {
                    quantity = item->Quantity;
                    PluginLog.Debug($"Found item?");
                    var ag = AgentInventoryContext.Instance();
                    ag->OpenForItemSlot(inv, i, AgentModule.Instance()->GetAgentByInternalId(AgentId.Retainer)->GetAddonID());
                    var contextMenu = (AtkUnitBase*)Svc.GameGui.GetAddonByName("ContextMenu", 1);
                    if (contextMenu != null)
                    {
                        if (item->Quantity == 1 || item->ItemID <= 19)
                        {
                            Callback(contextMenu, 0, 0, 0, 0, 0);
                        }
                        else
                        {
                            Callback(contextMenu, 0, 1, 0, 0, 0);
                        }
                        return true;
                    }
                }
            }
        }
        return false;
    }

    internal static bool InputNumericValue(int value)
    {
        var numeric = (AtkUnitBase*)Svc.GameGui.GetAddonByName("InputNumeric", 1);
        if (numeric != null)
        {
            var values = stackalloc AtkValue[1];
            values[0] = new AtkValue()
            {
                Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
                Int = value
            };
            numeric->FireCallback(1, values, (void*)1);
            return true;
        }
        return false;
    }
    internal static bool? ClickCloseEntrustWindow()
    {
        //13530	Close Window
        var text = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Addon>().GetRow(13530).Text.ToDalamudString().ExtractText();
        if (TryGetAddonByName<AtkUnitBase>("RetainerItemTransferProgress", out var addon) && IsAddonReady(addon))
        {
            var button = (AtkComponentButton*)addon->UldManager.NodeList[2]->GetComponent();
            var nodetext = MemoryHelper.ReadSeString(&addon->UldManager.NodeList[2]->GetComponent()->UldManager.NodeList[2]->GetAsAtkTextNode()->NodeText).ExtractText();
            if (nodetext == text && addon->UldManager.NodeList[2]->IsVisible && button->IsEnabled && RetainerInfo.GenericThrottle)
            {
                new ClickButtonGeneric(addon, "RetainerItemTransferProgress").Click(button);
                return true;
            }
        }
        else
        {
            RetainerInfo.RethrottleGeneric();
        }
        return false;
    }

    internal static bool? CloseAgentRetainer()
    {
        var a = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->UIModule->GetAgentModule()->GetAgentByInternalId(AgentId.Retainer);
        if (a->IsAgentActive())
        {
            a->Hide();
            return true;
        }
        return false;
    }

    internal static bool TrySelectSpecificEntry(string text)
    {
        return TrySelectSpecificEntry(new string[] { text });
    }

    internal static bool TrySelectSpecificEntry(IEnumerable<string> text)
    {
        if (TryGetAddonByName<AddonSelectString>("SelectString", out var addon) && IsAddonReady(&addon->AtkUnitBase))
        {
            var entry = GetEntries(addon).FirstOrDefault(x => x.EqualsAny(text));
            if (entry != null)
            {
                var index = GetEntries(addon).IndexOf(entry);
                if (index >= 0 && IsSelectItemEnabled(addon, index) && RetainerInfo.GenericThrottle)
                {
                    ClickSelectString.Using((nint)addon).SelectItem((ushort)index);
                    return true;
                }
            }
        }
        else
        {
            RetainerInfo.RethrottleGeneric();
        }
        return false;
    }

    internal static bool IsSelectItemEnabled(AddonSelectString* addon, int index)
    {
        var step1 = (AtkTextNode*)addon->AtkUnitBase
                    .UldManager.NodeList[2]
                    ->GetComponent()->UldManager.NodeList[index + 1]
                    ->GetComponent()->UldManager.NodeList[3];
        return ECommons.GenericHelpers.IsSelectItemEnabled(step1);
    }
    internal static List<string> GetEntries(AddonSelectString* addon)
    {
        var list = new List<string>();
        for (int i = 0; i < addon->PopupMenu.PopupMenu.EntryCount; i++)
        {
            list.Add(MemoryHelper.ReadSeStringNullTerminated((nint)addon->PopupMenu.PopupMenu.EntryNames[i]).ExtractText());
        }
        return list;
    }
}