using Artisan.Autocraft;
using ClickLib.Clicks;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ECommons.GenericHelpers;

namespace Artisan.RawInformation
{
    public unsafe static class Spiritbond
    {
        public static ushort Weapon { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[0].Spiritbond; }

        public static ushort Offhand { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[1].Spiritbond; }

        public static ushort Helm { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[2].Spiritbond; }

        public static ushort Body { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[3].Spiritbond; }

        public static ushort Hands { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[4].Spiritbond; }

        public static ushort Legs { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[6].Spiritbond; }

        public static ushort Feet { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[7].Spiritbond; }

        public static ushort Earring { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[8].Spiritbond; }

        public static ushort Neck { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[9].Spiritbond; }

        public static ushort Wrist { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[10].Spiritbond; }

        public static ushort Ring1 { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[11].Spiritbond; }

        public static ushort Ring2 { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[12].Spiritbond; }

        public static bool IsSpiritbondReadyAny()
        {
            if (Weapon == 10000) return true;
            if (Offhand == 10000) return true;
            if (Helm == 10000) return true;
            if (Body == 10000) return true;
            if (Hands == 10000) return true;
            if (Legs == 10000) return true;
            if (Feet == 10000) return true;
            if (Earring == 10000) return true;
            if (Neck == 10000) return true;
            if (Wrist == 10000) return true;
            if (Ring1 == 10000) return true;
            if (Ring2 == 10000) return true;

            return false;
        }

        public static bool IsMateriaMenuOpen() => Service.GameGui.GetAddonByName("Materialize", 1) != IntPtr.Zero;

        public static bool IsMateriaMenuDialogOpen() => Service.GameGui.GetAddonByName("MaterializeDialog", 1) != IntPtr.Zero;
        public unsafe static void OpenMateriaMenu()
        {
            if (Service.GameGui.GetAddonByName("Materialize", 1) == IntPtr.Zero)
            {
                if (Throttler.Throttle(1000))
                {
                    ActionManager.Instance()->UseAction(ActionType.General, 14);
                }
            }
        }

        public unsafe static void CloseMateriaMenu()
        {
            if (Service.GameGui.GetAddonByName("Materialize", 1) != IntPtr.Zero)
            {
                if (Throttler.Throttle(1000))
                {
                    ActionManager.Instance()->UseAction(ActionType.General, 14);
                }
            }
        }

        public unsafe static void ConfirmMateriaDialog()
        {
            try
            {
                if (Throttler.Throttle(500))
                {
                    var materializePTR = Service.GameGui.GetAddonByName("MaterializeDialog", 1);
                    if (materializePTR == IntPtr.Zero)
                        return;

                    var materalizeWindow = (AtkUnitBase*)materializePTR;
                    if (materalizeWindow == null)
                        return;

                    ClickMaterializeDialog.Using(materializePTR).Materialize();
                }
            }
            catch
            {

            }
        }

        public unsafe static void ExtractFirstMateria()
        {
            try
            {
                if (IsSpiritbondReadyAny())
                {
                    if (IsMateriaMenuDialogOpen())
                    {
                        ConfirmMateriaDialog();
                    }
                    else
                    {
                        if (Throttler.Throttle(500))
                        {
                            var materializePTR = Service.GameGui.GetAddonByName("Materialize", 1);
                            if (materializePTR == IntPtr.Zero)
                                return;

                            var materalizeWindow = (AtkUnitBase*)materializePTR;
                            if (materalizeWindow == null)
                                return;

                            var list = (AtkComponentList*)materalizeWindow->UldManager.NodeList[5];

                            var values = stackalloc AtkValue[2];
                            values[0] = new()
                            {
                                Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
                                Int = 2,
                            };
                            values[1] = new()
                            {
                                Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt,
                                UInt = 0,
                            };

                            materalizeWindow->FireCallback(1, values);


                        }
                    }
                }


            }
            catch (Exception e)
            {
                e.Log();
            }
        }
    }
}
