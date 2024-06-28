using ECommons.Reflection;

namespace Artisan.IPC
{
    public static class ItemVendorLocation
    {
        public static void OpenContextMenu(uint ItemId)
        {
            if (DalamudReflector.TryGetDalamudPlugin("ItemVendorLocation", out var pl, false, true))
            {
                var itemLookup = pl.GetFoP("_itemLookup");
                var itemInfo = itemLookup.Call("GetItemInfo", [ItemId]);
                if (itemInfo != null)
                {
                    itemInfo.Call("ApplyFilters", []);
                    pl.Call("ShowMultipleVendors", [itemInfo]);
                }
            }

        }

        public static bool ItemHasVendor(uint ItemId)
        {
            if (DalamudReflector.TryGetDalamudPlugin("ItemVendorLocation", out var pl, false, true))
            {
                var itemLookup = pl.GetFoP("_itemLookup");
                var itemInfo = itemLookup.Call("GetItemInfo", [ItemId]);
                if (itemInfo != null)
                {
                    return true;
                }
                else 
                { 
                    return false; 
                }
            }

            return false;
        }
    }

}
