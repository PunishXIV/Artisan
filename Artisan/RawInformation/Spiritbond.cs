using Artisan.GameInterop;
using Artisan.RawInformation.Character;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using static ECommons.GenericHelpers;

namespace Artisan.RawInformation
{
    public unsafe static class Spiritbond
    {
        public static ushort Weapon { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[0].SpiritbondOrCollectability; }

        public static ushort Offhand { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[1].SpiritbondOrCollectability; }

        public static ushort Helm { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[2].SpiritbondOrCollectability; }

        public static ushort Body { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[3].SpiritbondOrCollectability; }

        public static ushort Hands { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[4].SpiritbondOrCollectability; }

        public static ushort Legs { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[6].SpiritbondOrCollectability; }

        public static ushort Feet { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[7].SpiritbondOrCollectability; }

        public static ushort Earring { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[8].SpiritbondOrCollectability; }

        public static ushort Neck { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[9].SpiritbondOrCollectability; }

        public static ushort Wrist { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[10].SpiritbondOrCollectability; }

        public static ushort Ring1 { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[11].SpiritbondOrCollectability; }

        public static ushort Ring2 { get => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[12].SpiritbondOrCollectability; }

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

        public static bool IsMateriaMenuOpen() => Svc.GameGui.GetAddonByName("Materialize", 1) != IntPtr.Zero;

        public static bool IsMateriaMenuDialogOpen() => Svc.GameGui.GetAddonByName("MaterializeDialog", 1) != IntPtr.Zero;
        public unsafe static void OpenMateriaMenu()
        {
            if (Svc.GameGui.GetAddonByName("Materialize", 1) == IntPtr.Zero)
            {
                ActionManagerEx.UseMateriaExtraction();
            }
        }

        public unsafe static void CloseMateriaMenu()
        {
            if (Svc.GameGui.GetAddonByName("Materialize", 1) != IntPtr.Zero)
            {
                ActionManagerEx.UseMateriaExtraction();
            }
        }

        public unsafe static void ConfirmMateriaDialog()
        {
            try
            {
                var materializePTR = Svc.GameGui.GetAddonByName("MaterializeDialog", 1);
                if (materializePTR == IntPtr.Zero)
                    return;

                var materalizeWindow = (AtkUnitBase*)materializePTR;
                if (materalizeWindow == null)
                    return;

                new AddonMaster.MaterializeDialog(materializePTR).Materialize();
            }
            catch
            {

            }
        }

        private static DateTime _nextRetry;

        public unsafe static bool ExtractMateriaTask(bool option)
        {
            if (!CharacterInfo.MateriaExtractionUnlocked()) return true;
            if (CharacterOther.GetInventoryFreeSlotCount() == 0) return true;

            if (option)
            {
                if (IsMateriaMenuOpen() && !IsSpiritbondReadyAny())
                {
                    if (DateTime.Now < _nextRetry) return false;
                    CloseMateriaMenu();
                    _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
                    return false;
                }

                if (IsSpiritbondReadyAny())
                {
                    if (DateTime.Now < _nextRetry) return false;
                    if (!IsMateriaMenuOpen())
                    {
                        OpenMateriaMenu();
                        _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
                        return false;
                    }

                    if (IsMateriaMenuOpen() && !PreCrafting.Occupied())
                    {
                        ExtractFirstMateria();
                        _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
                        return false;
                    }

                    _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
                    return false;
                }
            }

            return true;
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
                        var materializePTR = Svc.GameGui.GetAddonByName("Materialize", 1);
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
            catch (Exception e)
            {
                e.Log();
            }
        }
    }
}
