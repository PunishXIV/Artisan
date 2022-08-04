using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Automation
{
    [StructLayout(LayoutKind.Sequential, Size = 0x68)]
    public readonly struct UTF8String : IDisposable
    {
        public const int size = 0x68;

        public readonly IntPtr stringPtr;
        public readonly ulong capacity;
        public readonly ulong length;
        public readonly ulong unknown;
        public readonly byte isEmpty;
        public readonly byte notReallocated; 
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x40)]
        public readonly byte[] str;

        public UTF8String(IntPtr loc, string text) : this(loc, Encoding.UTF8.GetBytes(text)) { }

        public UTF8String(IntPtr loc, byte[] text)
        {
            capacity = 0x40;
            length = (ulong)text.Length + 1;
            str = new byte[capacity];

            if (length > capacity)
            {
                stringPtr = Marshal.AllocHGlobal(text.Length + 1);
                capacity = length;
                Marshal.Copy(text, 0, stringPtr, text.Length);
                Marshal.WriteByte(stringPtr, text.Length, 0);
                notReallocated = 0;
            }
            else
            {
                stringPtr = loc + 0x22;
                text.CopyTo(str, 0);
                notReallocated = 1;
            }

            isEmpty = (byte)((length == 1) ? 1 : 0);
            unknown = 0;
        }

        public void Dispose()
        {
            if (notReallocated == 0)
                Marshal.FreeHGlobal(stringPtr);
        }
    }
}
