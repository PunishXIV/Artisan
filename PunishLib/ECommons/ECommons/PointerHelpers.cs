using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons
{
    public static unsafe class PointerHelpers
    {
        public static T* As<T>(this IntPtr ptr) where T:unmanaged
        {
            return (T*)ptr;
        }
    }
}
