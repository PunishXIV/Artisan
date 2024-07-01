using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin;
using System;
using ECommons.DalamudServices;

namespace Artisan.IPC
{
    //https://git.anna.lgbt/anna/ChatTwo/src/branch/main/ipc.md

    internal class Chat2IPC
    {
        public Chat2IPC(IDalamudPluginInterface pi)
        {
            Register = pi.GetIpcSubscriber<string>("ChatTwo.Register");
            Unregister = pi.GetIpcSubscriber<string, object?>("ChatTwo.Unregister");
            Invoke = pi.GetIpcSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>("ChatTwo.Invoke");
            Available = pi.GetIpcSubscriber<object?>("ChatTwo.Available");
        }

        // This is used to register your plugin with the IPC. It will return an ID
        // that you should save for later.
        private ICallGateSubscriber<string> Register { get; }

        // This is used to unregister your plugin from the IPC. You should call this
        // when your plugin is unloaded.
        private ICallGateSubscriber<string, object?> Unregister { get; }

        // You should subscribe to this event in order to receive a notification
        // when Chat 2 is loaded or updated, so you can re-register.
        private ICallGateSubscriber<object?> Available { get; }

        // Subscribe to this to draw your custom context menu items.
        private ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> Invoke { get; }

        private string _id = string.Empty;

        public Action<uint>? OnOpenChatTwoItemContextMenu;

        public void Enable()
        {
            // When Chat 2 becomes available (if it loads after this plugin) or when
            // Chat 2 is updated, register automatically.
            Available.Subscribe(RegisterIpc);
            // Register if Chat 2 is already loaded.
            RegisterIpc();

            // Listen for context menu events.
            Invoke.Subscribe(Integration);
        }

        private void RegisterIpc()
        {
            // Register and save the registration ID.
            try { _id = Register.InvokeFunc(); }
            catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) { Svc.Log.Debug("Chat2 is not available"); }
        }

        public void Disable()
        {
            if (!string.IsNullOrEmpty(_id))
            {
                Unregister.InvokeAction(_id);
                _id = null;
            }

            Invoke.Unsubscribe(Integration);
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
