using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.ImGuiMethods
{
    public class TimedMiddleOverlayWindow : MiddleOverlayWindow
    {
        long destroyAt;
        public TimedMiddleOverlayWindow(string name, long destroyAfterMS, Action draw, int? topOffset = null, Vector4? bgCol = null) : base(name, draw, topOffset, bgCol)
        {
            this.destroyAt = Environment.TickCount64 + destroyAfterMS;
        }

        public override void Update()
        {
            if (Environment.TickCount64 > destroyAt)
            {
                Dispose();
            }
        }
    }
}
