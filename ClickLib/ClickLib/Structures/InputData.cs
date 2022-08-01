using System;

using FFXIVClientStructs.FFXIV.Client.UI;

namespace ClickLib.Structures;

/// <summary>
/// Input data.
/// </summary>
public unsafe sealed class InputData : SharedBuffer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InputData"/> class.
    /// </summary>
    private InputData()
    {
        this.Data = (void**)Buffer.Add(new byte[0x40]);
        if (this.Data == null)
            throw new ArgumentNullException("InputData could not be created, null");

        this.Data[0] = null;
        this.Data[1] = null;
        this.Data[2] = null;
        this.Data[3] = null;
        this.Data[4] = null;
        this.Data[5] = null;
        this.Data[6] = null;
        this.Data[7] = null;
    }

    /// <summary>
    /// Gets the data pointer.
    /// </summary>
    public void** Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InputData"/> class.
    /// </summary>
    /// <returns>Input data.</returns>
    public static InputData Empty()
    {
        return new InputData();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InputData"/> class.
    /// </summary>
    /// <param name="popupMenu">List popup menu.</param>
    /// <param name="index">Selected index.</param>
    /// <returns>Input data.</returns>
    public static InputData ForPopupMenu(PopupMenu* popupMenu, ushort index)
    {
        var data = new InputData();
        data.Data[0] = popupMenu->List->ItemRendererList[index].AtkComponentListItemRenderer;
        data.Data[2] = (void*)(index | ((ulong)index << 48));
        return data;
    }
}