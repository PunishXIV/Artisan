using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin;
using System;
using ECommons.DalamudServices;

namespace Artisan.IPC
{
    //https://git.anna.lgbt/anna/ChatTwo/src/branch/main/ipc.md
    //HellionChat is a Chat 2 fork (EUPL-1.2) that exposes the same IPC surface under
    //the "HellionChat." prefix. HellionChat refuses to load while Chat 2 is active,
    //so at most one provider is ever present. Chat 2 takes priority.

    internal class Chat2IPC
    {
        private const string ChatTwoPrefix = "ChatTwo";
        private const string HellionChatPrefix = "HellionChat";

        public Chat2IPC(IDalamudPluginInterface pi)
        {
            ChatTwoRegister = pi.GetIpcSubscriber<string>($"{ChatTwoPrefix}.Register");
            ChatTwoUnregister = pi.GetIpcSubscriber<string, object?>($"{ChatTwoPrefix}.Unregister");
            ChatTwoInvoke = pi.GetIpcSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>($"{ChatTwoPrefix}.Invoke");
            ChatTwoAvailable = pi.GetIpcSubscriber<object?>($"{ChatTwoPrefix}.Available");

            HellionRegister = pi.GetIpcSubscriber<string>($"{HellionChatPrefix}.Register");
            HellionUnregister = pi.GetIpcSubscriber<string, object?>($"{HellionChatPrefix}.Unregister");
            HellionInvoke = pi.GetIpcSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>($"{HellionChatPrefix}.Invoke");
            HellionAvailable = pi.GetIpcSubscriber<object?>($"{HellionChatPrefix}.Available");
        }

        // Chat 2 subscribers. Preferred provider, checked first.
        private ICallGateSubscriber<string> ChatTwoRegister { get; }
        private ICallGateSubscriber<string, object?> ChatTwoUnregister { get; }
        private ICallGateSubscriber<object?> ChatTwoAvailable { get; }
        private ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> ChatTwoInvoke { get; }

        // HellionChat fallback subscribers. Only used when Chat 2 is absent.
        private ICallGateSubscriber<string> HellionRegister { get; }
        private ICallGateSubscriber<string, object?> HellionUnregister { get; }
        private ICallGateSubscriber<object?> HellionAvailable { get; }
        private ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> HellionInvoke { get; }

        private string? _id = string.Empty;
        private string _activeProvider = string.Empty;

        public Action<uint>? OnOpenChatTwoItemContextMenu;

        public void Enable()
        {
            // Subscribe both Available events upfront so we can react to either
            // provider loading or reloading after this plugin (hot-swap support).
            ChatTwoAvailable.Subscribe(RegisterChatTwo);
            HellionAvailable.Subscribe(RegisterHellion);

            // Initial probe: Chat 2 takes priority. Only try HellionChat when Chat 2
            // is absent at startup; if Chat 2 later loads, the subscription above
            // switches us over.
            RegisterChatTwo();
            if (_activeProvider != ChatTwoPrefix)
            {
                RegisterHellion();
            }

            // Listen for context menu events from whichever provider is active.
            ChatTwoInvoke.Subscribe(Integration);
            HellionInvoke.Subscribe(Integration);
        }

        private void RegisterChatTwo()
        {
            // Drop any existing HellionChat registration first, Chat 2 takes priority.
            if (_activeProvider == HellionChatPrefix && !string.IsNullOrEmpty(_id))
            {
                try { HellionUnregister.InvokeAction(_id); }
                catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) { }
            }

            // Register and save the registration ID.
            try { _id = ChatTwoRegister.InvokeFunc(); _activeProvider = ChatTwoPrefix; }
            catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) { Svc.Log.Debug("Chat 2 not available, checking HellionChat fallback"); }
        }

        private void RegisterHellion()
        {
            // HellionChat refuses to load while Chat 2 is active, so if its Available
            // signal fires, any prior Chat 2 registration is stale by definition.
            // Drop it and let HellionChat take over.
            if (_activeProvider == ChatTwoPrefix && !string.IsNullOrEmpty(_id))
            {
                try { ChatTwoUnregister.InvokeAction(_id); }
                catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) { }
            }

            // Register and save the registration ID.
            try { _id = HellionRegister.InvokeFunc(); _activeProvider = HellionChatPrefix; }
            catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) { Svc.Log.Debug("HellionChat fallback also not available"); }
        }

        public void Disable()
        {
            if (!string.IsNullOrEmpty(_id))
            {
                if (_activeProvider == ChatTwoPrefix) ChatTwoUnregister.InvokeAction(_id);
                else if (_activeProvider == HellionChatPrefix) HellionUnregister.InvokeAction(_id);
                _id = null;
            }

            ChatTwoInvoke.Unsubscribe(Integration);
            HellionInvoke.Unsubscribe(Integration);
        }

        private void Integration(string id, PlayerPayload? sender, ulong contentId, Payload? payload, SeString? senderString, SeString? content)
        {
            if (_id != id)
            {
                return;
            }

            if (payload is not ItemPayload item)
            {
                return;
            }

            OnOpenChatTwoItemContextMenu(item.ItemId);
        }
    }
}
