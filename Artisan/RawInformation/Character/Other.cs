using FFXIVClientStructs.FFXIV.Client.Game;

namespace Artisan.RawInformation.Character
{
    static unsafe class CharacterOther
    {
        internal static int GetInventoryFreeSlotCount()
        {
            InventoryType[] types = [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];
            var c = InventoryManager.Instance();
            var slots = 0;
            foreach (var x in types)
            {
                var inv = c->GetInventoryContainer(x);
                for (var i = 0; i < inv->Size; i++)
                {
                    if (inv->Items[i].ItemId == 0)
                    {
                        slots++;
                    }
                }
            }
            return slots;
        }
    }
}
