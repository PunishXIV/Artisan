using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Automation
{
    public unsafe static class MacroManager
    {
        private static Chat chat = null;

        public static void Execute(string multilineString)
        {
            Execute(multilineString.Replace("\r", "").Split("\n"));
        }

        public static void Execute(params string[] commands)
        {
            Execute(commands);
        }

        public static void Execute(IEnumerable<string> commands)
        {
            var macroPtr = IntPtr.Zero;
            chat ??= new();
            GenericHelpers.Safe(delegate
            {
                var count = (byte)Math.Max(Macro.numLines, commands.Count());
                if (count > Macro.numLines)
                {
                    throw new InvalidOperationException("Macro was more than 15 lines!");
                }
                if (commands.Any(x => x.Length > 180))
                {
                    throw new InvalidOperationException("Macro contained lines more than 180 symbols!");
                }
                if (commands.Any(x => x.Contains("\n") || x.Contains("\r") || x.Contains("\0") || chat.SanitiseText(x).Length != x.Length))
                {
                    throw new InvalidOperationException("Macro contained invalid symbols!");
                }
                macroPtr = Marshal.AllocHGlobal(Macro.size);
                using var macro = new Macro(macroPtr, string.Empty, commands.ToArray());
                Marshal.StructureToPtr(macro, macroPtr, false);

                RaptureShellModule.Instance->ExecuteMacro((FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureMacroModule.Macro*)macroPtr);
            });

            Marshal.FreeHGlobal(macroPtr);
        }

    }
}
