using ECommons.DalamudServices;
using ECommons.Logging;
using ECommons.Reflection;
using ImGuiNET;
using System;
using System.Linq;
using System.Reflection;

namespace Artisan.IPC
{
    internal static unsafe class SimpleTweaks
    {
        internal static bool IsEnabled()
        {
            return DalamudReflector.TryGetDalamudPlugin("SimpleTweaksPlugin", out _, false, true);
        }

        internal static bool IsFocusTweakEnabled()
        {
            if (!IsEnabled()) return false;
            if (DalamudReflector.TryGetDalamudPlugin("SimpleTweaksPlugin", out var tweaks, false, true) && tweaks != null)
            {
                var plugin = tweaks.GetFoP("Plugin");
                if (plugin == null) return false;

                var baseTweak = plugin?.Call("GetTweakById", ["UiAdjustments@AutoFocusRecipeSearch", plugin.GetFoP("Tweaks")]);
                bool enabled = (bool)baseTweak.GetFoP("Enabled");
                return enabled;
            }

            return false;
        }

        internal static bool IsImprovedLogEnabled()
        {
            if (!IsEnabled()) return false;
            if (DalamudReflector.TryGetDalamudPlugin("SimpleTweaksPlugin", out var tweaks, false, true) && tweaks != null)
            {
                var plugin = tweaks.GetFoP("Plugin");
                if (plugin == null) return false;

                var baseTweak = plugin?.Call("GetTweakById", ["ImprovedCraftingLog", plugin.GetFoP("Tweaks")]);
                bool enabled = (bool)baseTweak.GetFoP("Enabled");
                return enabled;
            }

            return false;
        }

        internal static void DisableImprovedLogTweak()
        {
            if (!IsImprovedLogEnabled()) return;
            if (DalamudReflector.TryGetDalamudPlugin("SimpleTweaksPlugin", out var tweaks, false, true) && tweaks != null)
            {
                var plugin = tweaks.GetFoP("Plugin");
                if (plugin == null) return;

                var baseTweak = plugin?.Call("GetTweakById", ["ImprovedCraftingLog", plugin.GetFoP("Tweaks")]);
                baseTweak.SetFoP("Enabled", false);

                DuoLog.Information($"Improved Crafting Log Tweak is disabled whilst using Artisan.");
            }
        }
    }
}
