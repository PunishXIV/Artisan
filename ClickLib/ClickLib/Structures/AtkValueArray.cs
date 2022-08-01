using System;
using System.Runtime.InteropServices;
using System.Text;

using FFXIVClientStructs.FFXIV.Component.GUI;

using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace ClickLib.Structures;

/// <summary>
/// A disposable AtkValue* object.
/// </summary>
/// <remarks>
/// https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Utility/Common.cs#L261.
/// </remarks>
internal unsafe class AtkValueArray : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AtkValueArray"/> class.
    /// </summary>
    /// <param name="values">AtkValue parameters.</param>
    public AtkValueArray(params object[] values)
    {
        this.Length = values.Length;
        this.Address = Marshal.AllocHGlobal(this.Length * Marshal.SizeOf<AtkValue>());
        this.Pointer = (AtkValue*)this.Address;

        for (var i = 0; i < values.Length; i++)
        {
            this.EncodeValue(i, values[i]);
        }
    }

    /// <summary>
    /// Gets the address of the array.
    /// </summary>
    public IntPtr Address { get; private set; }

    /// <summary>
    /// Gets an unsafe pointer to the array.
    /// </summary>
    public AtkValue* Pointer { get; private set; }

    /// <summary>
    /// Gets the array length.
    /// </summary>
    public int Length { get; private set; }

    public static implicit operator AtkValue*(AtkValueArray arr) => arr.Pointer;

    /// <inheritdoc/>
    public void Dispose()
    {
        for (var i = 0; i < this.Length; i++)
        {
            if (this.Pointer[i].Type == ValueType.String)
                Marshal.FreeHGlobal(new IntPtr(this.Pointer[i].String));
        }

        Marshal.FreeHGlobal(this.Address);
    }

    private unsafe void EncodeValue(int index, object value)
    {
        switch (value)
        {
            case uint uintValue:
                this.Pointer[index].Type = ValueType.UInt;
                this.Pointer[index].UInt = uintValue;
                break;
            case int intValue:
                this.Pointer[index].Type = ValueType.Int;
                this.Pointer[index].Int = intValue;
                break;
            case float floatValue:
                this.Pointer[index].Type = ValueType.Float;
                this.Pointer[index].Float = floatValue;
                break;
            case bool boolValue:
                this.Pointer[index].Type = ValueType.Bool;
                this.Pointer[index].Byte = Convert.ToByte(boolValue);
                break;
            case string stringValue:
                var stringBytes = Encoding.UTF8.GetBytes(stringValue + '\0');
                var stringAlloc = Marshal.AllocHGlobal(stringBytes.Length + 1);
                Marshal.Copy(stringBytes, 0, stringAlloc, stringBytes.Length + 1);

                this.Pointer[index].Type = ValueType.String;
                this.Pointer[index].String = (byte*)stringAlloc;
                break;
            default:
                throw new ArgumentException($"Unable to convert type {value.GetType()} to AtkValue");
        }
    }
}
