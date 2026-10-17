using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.Universalis;
using Dalamud.Interface.Textures.TextureWraps;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using OtterGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Artisan.RawInformation
{
    public class Ingredient
    {
        private DateTime RetainerCheck;
        private DateTime RemainingCheck;

        public int AmountUsedForSubcrafts;
        public bool CanBeCrafted = false;
        public uint Category;
        public Recipe CraftedRecipe;
        public Item Data;
        public TerritoryType GatherZone;
        public IDalamudTextureWrap? Icon;
        public int MaterialIndex;
        public NewCraftingList OriginList;
        public Dictionary<uint, int> OriginListMaterials;
        public int Required;
        public List<int> Sources = new();
        public bool TimedNode;
        public List<uint> UsedInCrafts = new();
        private int remaining = -1;
        private Dictionary<uint, Recipe?> subRecipes = new();
        public Dictionary<uint, int> UsedInMaterialsList;
        public Dictionary<uint, int> UsedInMaterialsListCount = new();
        public Dictionary<uint, List<Tuple<uint, int, int>>> SubSubMaterials = new();
        public MarketboardData? MarketboardData;
        public bool RecipeOnList;
        public IngredientHelpers IngredientHelper;

        public Ingredient(uint ItemId, int required, NewCraftingList originList, Dictionary<uint, int> materials, IngredientHelpers ingredientHelpers)
        {
            Data = LuminaSheets.ItemSheet[ItemId];
            Icon = P.Icons.TryLoadIconAsync(Data.Icon).Result;
            Required = required;
            if (LuminaSheets.RecipesByResultItem![ItemId].TryGetFirst(x => true, out CraftedRecipe)) { Sources.Add(1); CanBeCrafted = true; }
            if (LuminaSheets.GatheringItemByItemId!.ContainsKey(ItemId)) Sources.Add(2);
            if (LuminaSheets.FishingSpotItemIds!.Contains(ItemId)) Sources.Add(3);
            if (ItemVendorLocation.ItemHasVendor(ItemId)) Sources.Add(4);
            if (LuminaSheets.RetainerVentureItemIds!.Contains(ItemId) || DropSources.HasDrop(ItemId)) Sources.Add(5);

            if (Sources.Count == 0) Sources.Add(-1);
            GatherZone = Svc.Data.Excel.GetSheet<TerritoryType>()!.GetRow(1);
            TimedNode = false;
            if (LuminaSheets.GatheringItemByItemId.TryGetValue(Data.RowId, out var gatheringItemId))
            {
                if (HiddenItems.Items.TryGetFirst(x => x.ItemId == Data.RowId, out var hiddenItems))
                {
                    if (LuminaSheets.GatheringPointByBase!.TryGetValue(hiddenItems.NodeId, out var gatherpoint))
                    {
                        var transient = Svc.Data.Excel.GetSheet<GatheringPointTransient>().GetRow(gatherpoint.RowId);
                        if (transient.GatheringRarePopTimeTable.RowId > 0)
                            TimedNode = true;

                        if (gatherpoint.TerritoryType.IsValid)
                            GatherZone = gatherpoint.TerritoryType.Value!;
                    }
                }
                else
                {
                    foreach (var pointBase in LuminaSheets.GatheringPointBasesByGatheringItem![gatheringItemId])
                    {
                        if (GatherZone.RowId != 1) break;
                        if (LuminaSheets.GatheringPointByBase!.TryGetValue(pointBase.RowId, out var gatherpoint))
                        {
                            var transient = Svc.Data.Excel.GetSheet<GatheringPointTransient>().GetRow(gatherpoint.RowId);
                            if (transient.GatheringRarePopTimeTable.RowId > 0)
                                TimedNode = true;

                            if (gatherpoint.TerritoryType.IsValid)
                                GatherZone = gatherpoint.TerritoryType.Value!;
                        }
                    }
                }
            }

            Category = Data.ItemSearchCategory.RowId;
            OriginList = originList;
            OriginListMaterials = materials;
            foreach (var recipe in OriginList.Recipes)
            {
                if (LuminaSheets.RecipeSheet[recipe.ID].Ingredients().Any(x => x.Item.RowId == ItemId) && !UsedInCrafts.Contains(recipe.ID))
                    UsedInCrafts.Add(recipe.ID);
            }
            var craftsUsingThis = LuminaSheets.RecipesByIngredient![Data.RowId].Select(x => x.ItemResult.RowId).ToHashSet();
            UsedInMaterialsList = materials.Where(x => craftsUsingThis.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value);
            RecipeOnList = originList.Recipes.Any(x => LuminaSheets.RecipeSheet[x.ID].ItemResult.RowId == ItemId);
            // Market data is fetched in one batch for the whole table by IngredientHelpers.GenerateList.
            IngredientHelper = ingredientHelpers;
        }

        public virtual event EventHandler<bool>? OnRemainingChange;

        public int Inventory => CraftingListUI.NumberOfIngredient(Data.RowId);
        public unsafe int InventoryHQ => InventoryManager.Instance()->GetInventoryItemCount(Data.RowId, true, false, false);

        public int Remaining
        {
            get
            {
                if (DateTime.Now > RemainingCheck)
                {
                    var current = Math.Max(0, Required - Inventory - RetainerCount - (CanBeCrafted ? TotalCraftable : 0) - (OriginList.SkipIfEnough && OriginList.SkipLiteral ? 0 : AmountUsedForSubcrafts));
                    if (remaining != current)
                    {
                        remaining = current;
                        OnRemainingChange?.Invoke(this, true);
                    }
                    RemainingCheck = DateTime.Now.AddSeconds(0.5);
                }
                return remaining;
            }
        }

        public int RetainerCount
        {
            get
            {
                if (DateTime.Now > RetainerCheck)
                {
                    retainerCount = RetainerInfo.GetRetainerItemCount(Data.RowId);
                    RetainerCheck = DateTime.Now.AddSeconds(0.5);
                }
                return retainerCount;
            }
        }

        public int ReainterCountHQ
        {
            get
            {
                if (DateTime.Now > RetainerCheck)
                {
                    retainerCountHQ = RetainerInfo.GetRetainerItemCount(Data.RowId, true, true);
                    RetainerCheck = DateTime.Now.AddSeconds(0.5);
                }
                return retainerCountHQ;
            }
        }

        public int TotalCraftable
        {
            get
            {
                if (DateTime.Now > CraftableCheck)
                {
                    totalCraftable = NumberCraftable(Data.RowId);
                    CraftableCheck = DateTime.Now.AddSeconds(0.5);
                }
                return totalCraftable;
            }
        }

        private DateTime CraftableCheck;
        private int totalCraftable;
        private int retainerCount;
        private int retainerCountHQ;

        public int GetSubCraftCount()
        {
            int output = 0;
            SubSubMaterials.Clear();
            UsedInMaterialsListCount.Clear();

            foreach (var craft in UsedInCrafts)
            {
                var recipe = LuminaSheets.RecipeSheet[craft];
                var owned = RetainerInfo.GetRetainerItemCount(recipe.ItemResult.RowId) + CraftingListUI.NumberOfIngredient(recipe.ItemResult.RowId);
                var numberUsedInRecipe = recipe.Ingredients().First(x => x.Item.RowId == Data.RowId).Amount;
                var numberOnList = OriginList.Recipes.First(x => x.ID == craft).Quantity; // * recipe.AmountResult; Did I want to multiply that? Answers on a postcard
                if (IngredientHelper.HelperLookup.TryGetValue(recipe.ItemResult.RowId, out var subing))
                {
                    if (subing.UsedInCrafts.Count > 0)
                    {
                        //output += subing.GetSubCraftCount() * numberUsedInRecipe;
                        foreach (var i in subing.UsedInMaterialsListCount)
                        {
                            if (i.Value == 0) continue;

                            if (SubSubMaterials.ContainsKey(recipe.RowId))
                            {
                                if (SubSubMaterials.Values.Any(x => x.Any(y => y.Item1 == i.Key)))
                                    continue;
                                SubSubMaterials[recipe.RowId].Add(new Tuple<uint, int, int>(i.Key, i.Value * numberUsedInRecipe, i.Value));
                            }
                            else
                            {
                                SubSubMaterials[recipe.RowId] = new() { new Tuple<uint, int, int>(i.Key, i.Value * numberUsedInRecipe, i.Value) };
                            }
                        }
                    }
                }
                else
                {
                    continue;
                }

                UsedInMaterialsListCount[recipe.RowId] = Math.Min(numberUsedInRecipe * numberOnList, (int)Math.Floor((double)(owned / recipe.AmountResult) * numberUsedInRecipe));

                output += UsedInMaterialsListCount[recipe.RowId];
            }

            return output;

            //foreach (var material in UsedInMaterialsList)
            //{
            //    var owned = RetainerInfo.GetRetainerItemCount(material.Key) + CraftingListUI.NumberOfIngredient(material.Key);
            //    var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.RowId == material.Key && x.UnkData5.Any(y => y.ItemIngredient == Data.RowId));
            //    var numberUsedInRecipe = recipe.UnkData5.First(x => x.ItemIngredient == Data.RowId).AmountIngredient;
            //    var listMaterialRequired = OriginListMaterials.Any(x => x.Key == material.Key) ? OriginListMaterials.FirstOrDefault(x => x.Key == material.Key).Value : 0;
            //    var stillToMake = Math.Max(listMaterialRequired - owned, 0);
            //    var technicallyAlreadyCrafted = Math.Ceiling((double)owned / recipe.AmountResult);

            //    if (listMaterialRequired % recipe.AmountResult == 0)
            //    {
            //        UsedInMaterialsListCount[recipe.RowId] = Math.Min((int)Math.Floor((double)(owned / recipe.AmountResult)) * numberUsedInRecipe, material.Value * numberUsedInRecipe);
            //    }
            //    else
            //    {
            //        UsedInMaterialsListCount[recipe.RowId] = (int)technicallyAlreadyCrafted * numberUsedInRecipe;
            //    }

            //    output += UsedInMaterialsListCount[recipe.RowId];
            //}
            //return output;
        }

        private int NumberCraftable(uint ItemId)
        {
            List<int> NumberOfUses = new();
            if (LuminaSheets.RecipesByResultItem![ItemId].TryGetFirst(x => true, out var recipe))
            {
                foreach (var ingredient in recipe.Ingredients().Where(x => x.Amount > 0))
                {
                    int invCount = CraftingListUI.NumberOfIngredient(ingredient.Item.RowId);
                    int retainerCount = RetainerInfo.GetRetainerItemCount(ingredient.Item.RowId);

                    int craftableCount = 0;
                    Recipe? subRecipe;
                    if (subRecipes.ContainsKey(ingredient.Item.RowId))
                    {
                        subRecipe = subRecipes[ingredient.Item.RowId];
                    }
                    else
                    {
                        subRecipe = CraftingListHelpers.GetIngredientRecipe(ingredient.Item.RowId);
                        subRecipes.Add(ingredient.Item.RowId, subRecipe);
                    }

                    if (subRecipe is not null)
                    {
                        craftableCount = NumberCraftable(ingredient.Item.RowId);
                    }

                    int total = invCount + retainerCount + craftableCount;

                    int uses = total / ingredient.Amount    ;

                    NumberOfUses.Add(uses);
                }

                return NumberOfUses.Min() * recipe.AmountResult;
            }
            else
                return -1;
        }
    }
}