using Reloaded.Memory.Sources;
using Reloaded.Memory.Utilities;

namespace ClickLib.Structures;

/// <summary>
/// A shared buffer for structures.
/// </summary>
public abstract class SharedBuffer
{
    static SharedBuffer()
    {
        Buffer = new CircularBuffer(0x2048, Memory.Instance);
    }

    /// <summary>
    /// Gets the shared buffer.
    /// </summary>
    protected static CircularBuffer Buffer { get; }

    /// <summary>
    /// Dispose.
    /// </summary>
    public static void Dispose()
    {
        Buffer.Dispose();
    }
}
