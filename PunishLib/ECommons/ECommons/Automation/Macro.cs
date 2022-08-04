using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Automation
{
    [StructLayout(LayoutKind.Sequential, Size = 0x688)]
    public readonly struct Macro : IDisposable
    {
        public const int numLines = 15;
        public const int size = 0x8 + (UTF8String.size * (numLines + 1));

        public readonly uint icon;
        public readonly uint key;
        public readonly UTF8String title;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = numLines)]
        public readonly UTF8String[] lines;

        public Macro(IntPtr loc, string t, IReadOnlyList<string> commands)
        {
            icon = 0x101D1; // 66001
            key = 1;
            title = new UTF8String(loc + 0x8, t);
            lines = new UTF8String[numLines];
            for (int i = 0; i < numLines; i++)
            {
                var command = (commands.Count > i) ? commands[i] : string.Empty;
                lines[i] = new UTF8String(loc + 0x8 + (UTF8String.size * (i + 1)), command);
            }
        }

        public void Dispose()
        {
            title.Dispose();
            for (int i = 0; i < numLines; i++)
                lines[i].Dispose();
        }
    }
}
