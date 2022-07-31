using Dalamud.Hooking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.RawInformation
{
    internal class IconInformation : IDisposable
    {
        private readonly Hook<GetIconDelegate> getIconHook;
        private delegate uint GetIconDelegate(IntPtr actionManager, uint actionID);
        private IntPtr actionManager = IntPtr.Zero;

        public void Dispose()
        {
            this.getIconHook?.Dispose();

        }

        public IconInformation()
        {
            this.getIconHook = new Hook<GetIconDelegate>(Service.Address.GetAdjustedActionId, this.GetIconDetour);
            this.getIconHook.Enable();
        }

        public uint OriginalHook(uint actionID)
    => this.getIconHook.Original(this.actionManager, actionID);

        private unsafe uint GetIconDetour(IntPtr actionManager, uint actionID)
        {
            this.actionManager = actionManager;
            return actionID;
        }
    }
}
