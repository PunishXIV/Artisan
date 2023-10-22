using ClickLib.Bases;
using ClickLib.Enums;
using ClickLib.Structures;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop.Attributes;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.RawInformation
{
    public unsafe class ClickHelper
    {
        public delegate nint InvokeListener(nint a1, AtkEventType a2, uint a3, AtkEvent* a4);
        [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 0F B7 FA")]
        public InvokeListener Listener = null!;


        public ClickHelper()
        {
            Svc.Hook.InitializeFromAttributes(this);
        }

        public static ReceiveEventDelegate GetReceiveEvent(AtkEventListener* listener)
        {
            var receiveEventAddress = new IntPtr(listener->vfunc[2]);
            return Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>(receiveEventAddress)!;
        }

        public static void InvokeReceiveEvent(AtkEventListener* eventListener, EventType type, uint which, EventData eventData, InputData inputData)
        {
            var receiveEvent = GetReceiveEvent(eventListener);
            receiveEvent(eventListener, type, which, eventData.Data, inputData.Data);
        }

        public static void ClickAddonComponent(AtkComponentBase* unitbase, AtkComponentNode* target, uint which, EventType type, EventData? eventData = null, InputData? inputData = null)
        {
            eventData ??= EventData.ForNormalTarget(target, unitbase);
            inputData ??= InputData.Empty();

            InvokeReceiveEvent(&unitbase->AtkEventListener, type, which, eventData, inputData);
        }
    }

    public static unsafe class ClickHelperExtensions
    {
        public static ClickHelper Helper = new();

        public static void ClickAddonButton(this AtkComponentButton target, AtkComponentBase* addon, uint which, EventType type = EventType.CHANGE, EventData? eventData = null)
            => ClickHelper.ClickAddonComponent(addon, target.AtkComponentBase.OwnerNode, which, type, eventData);

        public static void ClickResNode(this AtkResNode target, AtkUnitBase* addon, int param, EventType type = EventType.CHANGE, EventData? eventData = null)
        {
            var evt = target.AtkEventManager.Event;

            try
            {
                addon->ReceiveEvent(evt->Type, param, evt);
            }
            catch (Exception ex) 
            {
                ex.Log();
            }
        }

        public static void ClickRadioButton(this AtkComponentRadioButton target, AtkComponentBase* addon, uint which, EventType type = EventType.CHANGE)
            => ClickHelper.ClickAddonComponent(addon, target.AtkComponentBase.OwnerNode, which, type);

        public static void ClickAddonButton(this AtkComponentButton target, AtkUnitBase* addon, AtkEvent* eventData)
        {
            Helper.Listener.Invoke((nint)addon, eventData->Type, eventData->Param, eventData);
        }

        public static void ClickAddonButton(this AtkComponentButton target, AtkUnitBase* addon)
        {
            var btnRes = target.AtkComponentBase.OwnerNode->AtkResNode;
            var evt = btnRes.AtkEventManager.Event;

            try
            {
                addon->ReceiveEvent(evt->Type, (int)evt->Param, btnRes.AtkEventManager.Event);
            }
            catch (Exception e)
            {
                e.Log();
            }

        }

        public static void ClickRadioButton(this AtkComponentRadioButton target, AtkUnitBase* addon)
        {
            var btnRes = target.AtkComponentBase.OwnerNode->AtkResNode;
            var evt = btnRes.AtkEventManager.Event;

            Svc.Log.Debug($"{evt->Type} {evt->Param}");
            addon->ReceiveEvent(evt->Type, (int)evt->Param, btnRes.AtkEventManager.Event, evt->Flags);
        }
    }
}
