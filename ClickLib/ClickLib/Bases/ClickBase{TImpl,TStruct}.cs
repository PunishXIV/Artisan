using System;
using System.Runtime.InteropServices;

using ClickLib.Enums;
using ClickLib.Structures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ClickLib.Bases;

/// <summary>
/// AtkUnitBase receive event delegate.
/// </summary>
/// <param name="eventListener">Type receiving the event.</param>
/// <param name="evt">Event type.</param>
/// <param name="which">Internal routing number.</param>
/// <param name="eventData">Event data.</param>
/// <param name="inputData">Keyboard and mouse data.</param>
/// <returns>The addon address.</returns>
internal unsafe delegate IntPtr ReceiveEventDelegate(AtkEventListener* eventListener, EventType evt, uint which, void* eventData, void* inputData);

/// <summary>
/// Click base class.
/// </summary>
/// <typeparam name="TImpl">The implementing type.</typeparam>
/// <typeparam name="TStruct">FFXIVClientStructs addon type.</typeparam>
public abstract unsafe partial class ClickBase<TImpl, TStruct> : ClickBase<TImpl>
    where TImpl : class
    where TStruct : unmanaged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClickBase{TImpl,TStruct}"/> class.
    /// </summary>
    /// <param name="name">Addon name.</param>
    /// <param name="addon">Addon address.</param>
    public ClickBase(string name, IntPtr addon)
        : base(name, addon)
    {
    }

    /// <summary>
    /// Gets a pointer to the type.
    /// </summary>
    protected TStruct* Addon => (TStruct*)this.UnitBase;

    /// <summary>
    /// Send a click.
    /// </summary>
    /// <param name="target">Target node.</param>
    /// <param name="which">Internal game click routing.</param>
    /// <param name="type">Event type.</param>
    protected void ClickAddonButton(AtkComponentButton* target, uint which, EventType type = EventType.CHANGE)
        => this.ClickAddonComponent(target->AtkComponentBase.OwnerNode, which, type);

    /// <summary>
    /// Send a click.
    /// </summary>
    /// <param name="nodeIndex">Target node index.</param>
    /// <param name="which">Internal game click routing.</param>
    /// <param name="type">Event type.</param>
    protected void ClickAddonButtonIndex(int nodeIndex, uint which, EventType type = EventType.CHANGE)
    {
        var node = (AtkComponentButton*)this.UnitBase->UldManager.NodeList[nodeIndex];
        this.ClickAddonButton(node, which, type);
    }

    /// <summary>
    /// Send a click.
    /// </summary>
    /// <param name="target">Target node.</param>
    /// <param name="which">Internal game click routing.</param>
    /// <param name="type">Event type.</param>
    protected void ClickAddonRadioButton(AtkComponentRadioButton* target, uint which, EventType type = EventType.CHANGE)
        => this.ClickAddonComponent(target->AtkComponentBase.OwnerNode, which, type);

    /// <summary>
    /// Send a click.
    /// </summary>
    /// <param name="target">Target node.</param>
    /// <param name="which">Internal game click routing.</param>
    /// <param name="type">Event type.</param>
    protected void ClickAddonCheckBox(AtkComponentCheckBox* target, uint which, EventType type = EventType.CHANGE)
        => this.ClickAddonComponent(target->AtkComponentButton.AtkComponentBase.OwnerNode, which, type);

    /// <summary>
    /// Send a click.
    /// </summary>
    /// <param name="target">Target node.</param>
    /// <param name="which">Internal game click routing.</param>
    /// <param name="type">Event type.</param>
    protected void ClickAddonDragDrop(AtkComponentDragDrop* target, uint which, EventType type = EventType.ICON_TEXT_ROLL_OUT)
        => this.ClickAddonComponent(target->AtkComponentBase.OwnerNode, which, type);

    /// <summary>
    /// Send a click.
    /// </summary>
    /// <param name="which">Internal game click routing.</param>
    /// <param name="type">Event type.</param>
    protected void ClickAddonStage(uint which, EventType type = EventType.MOUSE_CLICK)
    {
        var target = AtkStage.GetSingleton();

        var eventData = EventData.ForNormalTarget(target, this.UnitBase);
        var inputData = InputData.Empty();

        this.InvokeReceiveEvent(&this.UnitBase->AtkEventListener, type, which, eventData, inputData);
    }

    /// <summary>
    /// Send a click.
    /// </summary>
    /// <param name="target">Target node.</param>
    /// <param name="which">Internal game click routing.</param>
    /// <param name="type">Event type.</param>
    /// <param name="eventData">Event data.</param>
    /// <param name="inputData">Input data.</param>
    protected void ClickAddonComponent(AtkComponentNode* target, uint which, EventType type, EventData? eventData = null, InputData? inputData = null)
    {
        eventData ??= EventData.ForNormalTarget(target, this.UnitBase);
        inputData ??= InputData.Empty();

        this.InvokeReceiveEvent(&this.UnitBase->AtkEventListener, type, which, eventData, inputData);
    }

    /// <summary>
    /// Send a click.
    /// </summary>
    /// <param name="popupMenu">PopupMenu event listener.</param>
    /// <param name="index">List index.</param>
    /// <param name="type">Event type.</param>
    protected void ClickAddonList(PopupMenu* popupMenu, ushort index, EventType type = EventType.LIST_INDEX_CHANGE)
    {
        var targetList = popupMenu->List;
        if (index < 0 || index >= popupMenu->EntryCount)
            throw new ArgumentOutOfRangeException(nameof(index), "List index is out of range");

        var eventData = EventData.ForNormalTarget(targetList->AtkComponentBase.OwnerNode, popupMenu);
        var inputData = InputData.ForPopupMenu(popupMenu, index);

        this.InvokeReceiveEvent(&popupMenu->AtkEventListener, type, 0, eventData, inputData);
    }

    /// <summary>
    /// Invoke the receive event delegate.
    /// </summary>
    /// <param name="eventListener">Type receiving the event.</param>
    /// <param name="type">Event type.</param>
    /// <param name="which">Internal routing number.</param>
    /// <param name="eventData">Event data.</param>
    /// <param name="inputData">Keyboard and mouse data.</param>
    private void InvokeReceiveEvent(AtkEventListener* eventListener, EventType type, uint which, EventData eventData, InputData inputData)
    {
        var receiveEvent = this.GetReceiveEvent(eventListener);
        receiveEvent(eventListener, type, which, eventData.Data, inputData.Data);
    }

    private ReceiveEventDelegate GetReceiveEvent(AtkEventListener* listener)
    {
        var receiveEventAddress = new IntPtr(listener->vfunc[2]);
        return Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>(receiveEventAddress)!;
    }

    private ReceiveEventDelegate GetReceiveEvent(AtkComponentBase* listener)
        => this.GetReceiveEvent(&listener->AtkEventListener);

    private ReceiveEventDelegate GetReceiveEvent(AtkUnitBase* listener)
        => this.GetReceiveEvent(&listener->AtkEventListener);
}