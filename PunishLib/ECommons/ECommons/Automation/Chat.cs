/*
https://git.annaclemens.io/ascclemens/XivCommon/src/branch/main/XivCommon/Functions/Chat.cs 
MIT License
Copyright (c) 2021 Anna Clemens
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework;
namespace ECommons.Automation
{
    /// <summary>
    /// A class containing chat functionality
    /// </summary>
    public class Chat
    {
        private static class Signatures
        {
            internal const string SendChat = "48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9";
            internal const string SanitiseString = "E8 ?? ?? ?? ?? EB 0A 48 8D 4C 24 ?? E8 ?? ?? ?? ?? 48 8D 8D";
        }
        private delegate void ProcessChatBoxDelegate(IntPtr uiModule, IntPtr message, IntPtr unused, byte a4);
        private ProcessChatBoxDelegate ProcessChatBox { get; }
        private readonly unsafe delegate* unmanaged<Utf8String*, int, IntPtr, void> _sanitiseString = null!;
        public Chat()
        {
            if (Svc.SigScanner.TryScanText(Signatures.SendChat, out var processChatBoxPtr))
            {
                ProcessChatBox = Marshal.GetDelegateForFunctionPointer<ProcessChatBoxDelegate>(processChatBoxPtr);
            }
            unsafe
            {
                if (Svc.SigScanner.TryScanText(Signatures.SanitiseString, out var sanitisePtr))
                {
                    _sanitiseString = (delegate* unmanaged<Utf8String*, int, IntPtr, void>)sanitisePtr;
                }
            }
        }
        /// <summary>
        /// <para>
        /// Send a given message to the chat box. <b>This can send chat to the server.</b>
        /// </para>
        /// <para>
        /// <b>This method is unsafe.</b> This method does no checking on your input and
        /// may send content to the server that the normal client could not. You must
        /// verify what you're sending and handle content and length to properly use
        /// this.
        /// </para>
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <exception cref="InvalidOperationException">If the signature for this function could not be found</exception>
        public unsafe void SendMessageUnsafe(byte[] message)
        {
            if (ProcessChatBox == null)
            {
                throw new InvalidOperationException("Could not find signature for chat sending");
            }
            var uiModule = (IntPtr)Framework.Instance()->GetUiModule();
            using var payload = new ChatPayload(message);
            var mem1 = Marshal.AllocHGlobal(400);
            Marshal.StructureToPtr(payload, mem1, false);
            ProcessChatBox(uiModule, mem1, IntPtr.Zero, 0);
            Marshal.FreeHGlobal(mem1);
        }
        /// <summary>
        /// <para>
        /// Send a given message to the chat box. <b>This can send chat to the server.</b>
        /// </para>
        /// <para>
        /// This method is slightly less unsafe than <see cref="SendMessageUnsafe"/>. It
        /// will throw exceptions for certain inputs that the client can't normally send,
        /// but it is still possible to make mistakes. Use with caution.
        /// </para>
        /// </summary>
        /// <param name="message">message to send</param>
        /// <exception cref="ArgumentException">If <paramref name="message"/> is empty, longer than 500 bytes in UTF-8, or contains invalid characters.</exception>
        /// <exception cref="InvalidOperationException">If the signature for this function could not be found</exception>
        public void SendMessage(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            if (bytes.Length == 0)
            {
                throw new ArgumentException("message is empty", nameof(message));
            }
            if (bytes.Length > 500)
            {
                throw new ArgumentException("message is longer than 500 bytes", nameof(message));
            }
            if (message.Length != SanitiseText(message).Length)
            {
                throw new ArgumentException("message contained invalid characters", nameof(message));
            }
            SendMessageUnsafe(bytes);
        }
        /// <summary>
        /// <para>
        /// Sanitises a string by removing any invalid input.
        /// </para>
        /// <para>
        /// The result of this method is safe to use with
        /// <see cref="SendMessage"/>, provided that it is not empty or too
        /// long.
        /// </para>
        /// </summary>
        /// <param name="text">text to sanitise</param>
        /// <returns>sanitised text</returns>
        /// <exception cref="InvalidOperationException">If the signature for this function could not be found</exception>
        public unsafe string SanitiseText(string text)
        {
            if (_sanitiseString == null)
            {
                throw new InvalidOperationException("Could not find signature for chat sanitisation");
            }
            var uText = Utf8String.FromString(text);
            _sanitiseString(uText, 0x27F, IntPtr.Zero);
            var sanitised = uText->ToString();
            uText->Dtor();
            IMemorySpace.Free(uText);
            return sanitised;
        }
        [StructLayout(LayoutKind.Explicit)]
        private readonly struct ChatPayload : IDisposable
        {
            [FieldOffset(0)]
            private readonly IntPtr textPtr;
            [FieldOffset(16)]
            private readonly ulong textLen;
            [FieldOffset(8)]
            private readonly ulong unk1;
            [FieldOffset(24)]
            private readonly ulong unk2;
            internal ChatPayload(byte[] stringBytes)
            {
                textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);
                Marshal.Copy(stringBytes, 0, textPtr, stringBytes.Length);
                Marshal.WriteByte(textPtr + stringBytes.Length, 0);
                textLen = (ulong)(stringBytes.Length + 1);
                unk1 = 64;
                unk2 = 0;
            }
            public void Dispose()
            {
                Marshal.FreeHGlobal(textPtr);
            }
        }
    }
}