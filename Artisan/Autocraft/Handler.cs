using Artisan.CraftingLogic;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;

namespace Artisan.Autocraft
{
    internal unsafe static class Handler
    {
        internal static bool Autocraft = false;
        internal static bool Enable = false;
        internal static List<int>? HQData = null;
        internal static void Init()
        {
            Svc.Framework.Update += Framework_Update;
        }

        internal static void Dispose()
        {
            Svc.Framework.Update -= Framework_Update;
        }

        private static void Framework_Update(Dalamud.Game.Framework framework)
        {
            if (Enable)
            {
                if(!Throttler.Throttle(0))
                {
                    return;
                }
                PluginLog.Debug("Throttle success");
                if (HQData == null)
                {
                    DuoLog.Error("HQ data is null");
                    Enable = false;
                    return;
                }
                PluginLog.Debug("HQ not null");
                if (!ConsumableChecker.CheckConsumables())
                {
                    return;
                }
                PluginLog.Debug("Consumables success");
                if (TryGetAddonByName<AtkUnitBase>("RecipeNote", out var addon) && addon->IsVisible)
                {
                    PluginLog.Debug("Addon visible");
                    if (!HQManager.RestoreHQData(HQData, out var fin) || !fin)
                    {
                        return;
                    }
                    PluginLog.Debug("HQ data restored");
                    CurrentCraft.RepeatActualCraft();
                }
                else
                {
                    PluginLog.Debug("Addon invisible");
                    if (Throttler.Throttle(1000))
                    {
                        PluginLog.Debug("Opening crafting log");
                        CommandProcessor.ExecuteThrottled("/clog");
                    }
                }
            }
        }

        internal static void Draw()
        {
            ImGui.Checkbox("Enable autocraft (immediately begins crafting)", ref Enable);
            if (!Enable)
            {
                HQManager.TryGetCurrent(out HQData);
            }
            ImGuiEx.Text($"HQ ingredients: {HQData?.Select(x => x.ToString()).Join(", ")}");
            {
                ImGuiEx.TextV("Maintain food buff:");
                ImGui.SameLine(150f.Scale());
                if (ImGui.BeginCombo("##foodBuff", ConsumableChecker.Food.TryGetFirst(x => x.Id == Service.Configuration.Food, out var item) ? $"{(Service.Configuration.FoodHQ ? " " : "")}{item.Name}" : $"{(Service.Configuration.Food == 0 ? "Disabled" : $"{(Service.Configuration.FoodHQ ? " " : "")}{Service.Configuration.Food}")}"))
                {
                    if (ImGui.Selectable("Disable"))
                    {
                        Service.Configuration.Food = 0;
                    }
                    foreach (var x in ConsumableChecker.GetFood(true))
                    {
                        if (ImGui.Selectable($"{x.Name}"))
                        {
                            Service.Configuration.Food = x.Id;
                            Service.Configuration.FoodHQ = false;
                        }
                    }
                    foreach (var x in ConsumableChecker.GetFood(true, true))
                    {
                        if (ImGui.Selectable($" {x.Name}"))
                        {
                            Service.Configuration.Food = x.Id;
                            Service.Configuration.FoodHQ = true;
                        }
                    }
                    ImGui.EndCombo();
                }
            }

            {
                ImGuiEx.TextV("Maintain potion buff:");
                ImGui.SameLine(150f.Scale());
                if (ImGui.BeginCombo("##potBuff", ConsumableChecker.Pots.TryGetFirst(x => x.Id == Service.Configuration.Potion, out var item) ? $"{(Service.Configuration.PotHQ ? " " : "")}{item.Name}" : $"{(Service.Configuration.Potion == 0 ? "Disabled" : $"{(Service.Configuration.PotHQ ? " " : "")}{Service.Configuration.Potion}")}"))
                {
                    if (ImGui.Selectable("Disable"))
                    {
                        Service.Configuration.Potion = 0;
                    }
                    foreach (var x in ConsumableChecker.GetPots(true))
                    {
                        if (ImGui.Selectable($"{x.Name}"))
                        {
                            Service.Configuration.Potion = x.Id;
                            Service.Configuration.PotHQ = false;
                        }
                    }
                    foreach (var x in ConsumableChecker.GetPots(true, true))
                    {
                        if (ImGui.Selectable($" {x.Name}"))
                        {
                            Service.Configuration.Potion = x.Id;
                            Service.Configuration.PotHQ = true;
                        }
                    }
                    ImGui.EndCombo();
                }
            }
            ImGui.Checkbox("Stop autocrafting if food/medicine is not found", ref Service.Configuration.AbortIfNoFoodPot);
        }
    }
}
