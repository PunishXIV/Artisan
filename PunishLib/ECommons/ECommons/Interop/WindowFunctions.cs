using PInvoke;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using System.Windows.Forms;
using static PInvoke.User32;

namespace ECommons.Interop
{
    public static class WindowFunctions
    {
        public const int SW_MINIMIZE = 6;
        public const int SW_FORCEMINIMIZE = 11;
        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;
        public const int SW_SHOWNA = 8;

        public static bool TryFindGameWindow(out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;
            while (true)
            {
                hwnd = FindWindowEx(IntPtr.Zero, hwnd, "FFXIVGAME", null);
                if (hwnd == IntPtr.Zero) break;
                GetWindowThreadProcessId(hwnd, out var pid);
                if (pid == Environment.ProcessId) break;
            }
            return hwnd != IntPtr.Zero;
        }

        /// <summary>Returns true if the current application has focus, false otherwise</summary>
        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Environment.ProcessId;
            GetWindowThreadProcessId(activatedHandle, out int activeProcId);

            return activeProcId == procId;
        }

        public static bool SendKeypress(int keycode)
        {
            if (TryFindGameWindow(out var hwnd))
            {
                User32.SendMessage(hwnd, WindowMessage.WM_KEYDOWN, (IntPtr)keycode, (IntPtr)0);
                User32.SendMessage(hwnd, WindowMessage.WM_KEYUP, (IntPtr)keycode, (IntPtr)0);
                return true;
            }
            return false;
        }

        /*public static bool SendKeypress(Keys key)
        {
            return SendKeypress((int)key);
        }*/
    }
}
