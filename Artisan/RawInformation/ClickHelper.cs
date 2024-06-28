using ClickLib.Bases;
using ClickLib.Enums;
using ClickLib.Structures;
using Dalamud.Utility.Signatures;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Runtime.InteropServices;

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

        public static void InvokeReceiveEvent(AtkEventListener* eventListener, AtkEventType type, int which, AtkEvent* eventData, AtkEventData* inputData)
        {
            eventListener->ReceiveEvent(type, which, eventData, inputData);
        }

        public static void ClickAddonComponent(AtkComponentBase* unitbase, AtkComponentNode* target, int which, AtkEventType type, AtkEvent* eventData, AtkEventData* inputData = null)
        {
            InvokeReceiveEvent(&unitbase->AtkEventListener, type, which, eventData, inputData);
        }
    }

    public static unsafe class ClickHelperExtensions
    {
        public static ClickHelper Helper = new();

        public static void ClickAddonButton(this AtkComponentButton target, AtkComponentBase* addon, int which, AtkEventType type, AtkEvent* eventData = null)
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
    }
}
