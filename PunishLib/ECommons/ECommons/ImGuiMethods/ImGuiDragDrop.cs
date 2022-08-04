using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.ImGuiMethods
{
    // ImGui extra functionality related with Drag and Drop
    public static class ImGuiDragDrop
    {
        // TODO: review
        // can now pass refs with Unsafe.AsRef

        public static unsafe void SetDragDropPayload<T>(string type, T data, ImGuiCond cond = 0)
        where T : unmanaged
        {
            void* ptr = Unsafe.AsPointer(ref data);
            ImGui.SetDragDropPayload(type, new IntPtr(ptr), (uint)Unsafe.SizeOf<T>(), cond);
        }

        public static unsafe bool AcceptDragDropPayload<T>(string type, out T payload, ImGuiDragDropFlags flags = ImGuiDragDropFlags.None)
        where T : unmanaged
        {
            ImGuiPayload* pload = ImGui.AcceptDragDropPayload(type, flags);
            payload = (pload != null) ? Unsafe.Read<T>(pload->Data) : default;
            return pload != null;
        }

        public static unsafe void SetDragDropPayload(string type, string data, ImGuiCond cond = 0)
        {
            fixed (char* chars = data)
            {
                int byteCount = Encoding.Default.GetByteCount(data);
                byte* bytes = stackalloc byte[byteCount];
                Encoding.Default.GetBytes(chars, data.Length, bytes, byteCount);

                ImGui.SetDragDropPayload(type, new IntPtr(bytes), (uint)byteCount, cond);
            }
        }

        public static unsafe bool AcceptDragDropPayload(string type, out string payload, ImGuiDragDropFlags flags = ImGuiDragDropFlags.None)
        {
            ImGuiPayload* pload = ImGui.AcceptDragDropPayload(type, flags);
            payload = (pload != null) ? Encoding.Default.GetString((byte*)pload->Data, pload->DataSize) : null;
            return pload != null;
        }
    }
}
