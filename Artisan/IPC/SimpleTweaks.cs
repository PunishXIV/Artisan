using ECommons.Reflection;

namespace Artisan.IPC
{
    internal static unsafe class SimpleTweaks
    {
        internal static bool IsEnabled()
        {
            return DalamudReflector.TryGetDalamudPlugin("Simple Tweaks Plugin", out _, false, true);
        }

        internal static bool IsFocusTweakEnabled()
        {
            if (!IsEnabled()) return false;
            if (DalamudReflector.TryGetDalamudPlugin("Simple Tweaks Plugin", out var tweaks, false, true) && tweaks != null)
            {
                var plugin = tweaks.GetFoP("Plugin");
                if (plugin == null) return false;

                var baseTweak = plugin?.Call("GetTweakById", "UiAdjustments@AutoFocusRecipeSearch", plugin.GetFoP("Tweaks"));
                bool enabled = (bool)baseTweak.GetFoP("Enabled");
                return enabled;
            }

            return false;
        }

    }
}
