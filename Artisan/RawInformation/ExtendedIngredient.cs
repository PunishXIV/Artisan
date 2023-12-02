using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.Universalis;
using Dalamud.Interface.Internal;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.GeneratedSheets;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Artisan.RawInformation
{
    public class Ingredient
    {
        public int AmountUsedForSubcrafts;
        public bool CanBeCrafted = false;
        public uint Category;
        public Recipe? CraftedRecipe;
        public Item Data;
        public TerritoryType GatherZone;
        public IDalamudTextureWrap Icon;
        public int MaterialIndex;
        public CraftingList OriginList;
        public int Required;
        public List<int> Sources = new();
        public bool TimedNode;
        public List<uint> UsedInCrafts = new();
        private int remaining = -1;
        private Dictionary<int, Recipe?> subRecipes = new();
        public Dictionary<uint, int> UsedInMaterialsList;
        public Dictionary<uint, int> UsedInMaterialsListCount = new();
        public MarketboardData? MarketboardData;

        public Ingredient(uint itemId, int required, CraftingList originList, Dictionary<uint, int> materials)
        {
            Data = LuminaSheets.ItemSheet.Values.First(x => x.RowId == itemId);
            Icon = P.Icons.LoadIcon(Data.Icon);
            Required = required;
            if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == itemId, out CraftedRecipe)) { Sources.Add(1); CanBeCrafted = true; }
            if (LuminaSheets.GatheringItemSheet.Values.Any(x => x.Item == itemId)) Sources.Add(2);
            if (Svc.Data.GetExcelSheet<FishingSpot>()!.Any(x => x.Item.Any(y => y.Value.RowId == itemId))) Sources.Add(3);
            if (ItemVendorLocation.ItemHasVendor(itemId)) Sources.Add(4);
            if (Svc.Data.GetExcelSheet<RetainerTaskNormal>()!.Any(x => x.Item.Row == itemId && x.GatheringLog.Row == 0 && x.FishingLog == 0) || DropSources.Sources.Any(x => x.ItemId == itemId)) Sources.Add(5);

            if (Sources.Count == 0) Sources.Add(-1);

            GatherZone = Svc.Data.Excel.GetSheet<TerritoryType>()!.First(x => x.RowId == 1);
            TimedNode = false;
            if (LuminaSheets.GatheringItemSheet!.FindFirst(x => x.Value.Item == Data.RowId, out var gather))
            {
                if (HiddenItems.Items.FindFirst(x => x.ItemId == Data.RowId, out var hiddenItems))
                {
                    if (Svc.Data.Excel.GetSheet<GatheringPoint>()!.FindFirst(y => y.GatheringPointBase.Row == hiddenItems.NodeId && y.TerritoryType.Value.PlaceName.Row > 0, out var gatherpoint))
                    {
                        var transient = Svc.Data.Excel.GetSheet<GatheringPointTransient>().GetRow(gatherpoint.RowId);
                        if (transient.GatheringRarePopTimeTable.Row > 0)
                            TimedNode = true;

                        if (gatherpoint.TerritoryType.IsValueCreated)
                            GatherZone = gatherpoint.TerritoryType.Value!;
                    }
                }
                else
                {
                    foreach (var pointBase in LuminaSheets.GatheringPointBaseSheet.Values.Where(x => x.Item.Any(y => y == gather.Key)))
                    {
                        if (GatherZone.RowId != 1) break;
                        if (Svc.Data.Excel.GetSheet<GatheringPoint>()!.FindFirst(y => y.GatheringPointBase.Row == pointBase.RowId && y.TerritoryType.Value.PlaceName.Row > 0, out var gatherpoint))
                        {
                            var transient = Svc.Data.Excel.GetSheet<GatheringPointTransient>().GetRow(gatherpoint.RowId);
                            if (transient.GatheringRarePopTimeTable.Row > 0)
                                TimedNode = true;

                            if (gatherpoint.TerritoryType.IsValueCreated)
                                GatherZone = gatherpoint.TerritoryType.Value!;
                        }
                    }
                }
            }

            Category = Data.ItemSearchCategory.Row;
            OriginList = originList;
            foreach (var recipe in OriginList.Items)
            {
                if (LuminaSheets.RecipeSheet[recipe].UnkData5.Any(x => x.ItemIngredient == itemId) && !UsedInCrafts.Contains(recipe))
                    UsedInCrafts.Add(recipe);
            }
            UsedInMaterialsList = materials.Where(x => LuminaSheets.RecipeSheet.Values.Any(y => y.ItemResult.Row == x.Key && y.UnkData5.Any(z => z.ItemIngredient == Data.RowId))).ToDictionary(x => x.Key, x => x.Value);

            if (P.Config.UseUniversalis)
            {
                MarketboardData = (P.Config.UniversalisDataCenter)
                    ? P.UniversalsisClient.GetDataCenterData(itemId)
                    : P.UniversalsisClient.GetRegionData(itemId);
            }
        }

        public virtual event EventHandler<bool>? OnRemainingChange;

        public int Inventory => CraftingListUI.NumberOfIngredient(Data.RowId);
        public unsafe int InventoryHQ => InventoryManager.Instance()->GetInventoryItemCount(Data.RowId, true, false, false);

        public int Remaining
        {
            get
            {
                var current = Math.Max(0, Required - Inventory - RetainerCount - (CanBeCrafted ? TotalCraftable : 0) - AmountUsedForSubcrafts);
                if (remaining != current)
                {
                    remaining = current;
                    AmountUsedForSubcrafts = GetSubCraftCount();
                    OnRemainingChange?.Invoke(this, true);
                }
                return remaining;
            }
        }

        public int RetainerCount => RetainerInfo.GetRetainerItemCount(Data.RowId);
        public int ReainterCountHQ => RetainerInfo.GetRetainerItemCount(Data.RowId, true, true);
        public int TotalCraftable => NumberCraftable(Data.RowId);

        public static async Task<List<Ingredient>> GenerateList(CraftingList originList)
        {
            var materials = originList.ListMaterials();
            List<Ingredient> output = new();
            foreach (var item in materials.OrderBy(x => x.Key))
            {
                await Task.Run(() => output.Add(new Ingredient(item.Key, item.Value, originList, materials)));
            }

            return output;
        }

        private int GetSubCraftCount()
        {
            int output = 0;
            foreach (var material in UsedInMaterialsList)
            {
                var owned = RetainerInfo.GetRetainerItemCount(material.Key) + CraftingListUI.NumberOfIngredient(material.Key);
                var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == material.Key && x.UnkData5.Any(y => y.ItemIngredient == Data.RowId));
                var numberUsedInRecipe = recipe.UnkData5.First(x => x.ItemIngredient == Data.RowId).AmountIngredient;

                UsedInMaterialsListCount[recipe.RowId] = Math.Min((int)Math.Floor((double)(owned / recipe.AmountResult)) * numberUsedInRecipe, material.Value * numberUsedInRecipe);

                output += Math.Min((int)Math.Floor((double)(owned / recipe.AmountResult)) * numberUsedInRecipe, material.Value * numberUsedInRecipe);
            }
            return output;
        }

        private int NumberCraftable(uint itemId)
        {
            List<int> NumberOfUses = new();
            if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == itemId, out var recipe))
            {
                foreach (var ingredient in recipe.UnkData5.Where(x => x.AmountIngredient > 0))
                {
                    int invCount = CraftingListUI.NumberOfIngredient((uint)ingredient.ItemIngredient);
                    int retainerCount = RetainerInfo.GetRetainerItemCount((uint)ingredient.ItemIngredient);

                    int craftableCount = 0;
                    Recipe? subRecipe;
                    if (subRecipes.ContainsKey(ingredient.ItemIngredient))
                    {
                        subRecipe = subRecipes[ingredient.ItemIngredient];
                    }
                    else
                    {
                        subRecipe = CraftingListHelpers.GetIngredientRecipe((uint)ingredient.ItemIngredient);
                        subRecipes.Add(ingredient.ItemIngredient, subRecipe);
                    }

                    if (subRecipe is not null)
                    {
                        craftableCount = NumberCraftable((uint)ingredient.ItemIngredient);
                    }

                    int total = invCount + retainerCount + craftableCount;

                    int uses = total / ingredient.AmountIngredient;

                    NumberOfUses.Add(uses);
                }

                return NumberOfUses.Min() * recipe.AmountResult;
            }
            else
                return -1;
        }
    }
}