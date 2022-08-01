using System;

namespace ClickLib.Structures;

/// <summary>
/// Event data.
/// </summary>
public unsafe sealed class EventData : SharedBuffer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventData"/> class.
    /// </summary>
    private EventData()
    {
        this.Data = (void**)Buffer.Add(new byte[0x18]);
        if (this.Data == null)
            throw new ArgumentNullException("EventData could not be created, null");

        this.Data[0] = null;
        this.Data[1] = null;
        this.Data[2] = null;
    }

    /// <summary>
    /// Gets the data pointer.
    /// </summary>
    public void** Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EventData"/> class.
    /// </summary>
    /// <param name="target">Target.</param>
    /// <param name="listener">Event listener.</param>
    /// <returns>Event data.</returns>
    public static EventData ForNormalTarget(void* target, void* listener)
    {
        var data = new EventData();
        data.Data[1] = target;
        data.Data[2] = listener;
        return data;
    }
}