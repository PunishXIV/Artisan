using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.Universalis;
using Dalamud.Interface.Textures.TextureWraps;
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
        private DateTime RetainerCheck;
        private DateTime RemainingCheck;

        public int AmountUsedForSubcrafts;
        public bool CanBeCrafted = false;
        public uint Category;
        public Recipe? CraftedRecipe;
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
        private Dictionary<int, Recipe?> subRecipes = new();
        public Dictionary<uint, int> UsedInMaterialsList;
        public Dictionary<uint, int> UsedInMaterialsListCount = new();
        public Dictionary<uint, List<Tuple<uint, int, int>>> SubSubMaterials = new();
        public MarketboardData? MarketboardData;
        public bool RecipeOnList;
        public IngredientHelpers IngredientHelper;

        public Ingredient(uint ItemId, int required, NewCraftingList originList, Dictionary<uint, int> materials, IngredientHelpers ingredientHelpers)
        {
            Data = LuminaSheets.ItemSheet.Values.First(x => x.RowId == ItemId);
            Icon = P.Icons.TryLoadIconAsync(Data.Icon).Result;
            Required = required;
            if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == ItemId, out CraftedRecipe)) { Sources.Add(1); CanBeCrafted = true; }
            if (LuminaSheets.GatheringItemSheet.Values.Any(x => x.Item == ItemId)) Sources.Add(2);
            if (Svc.Data.GetExcelSheet<FishingSpot>()!.Any(x => x.Item.Any(y => y.Value.RowId == ItemId))) Sources.Add(3);
            if (ItemVendorLocation.ItemHasVendor(ItemId)) Sources.Add(4);
            if (Svc.Data.GetExcelSheet<RetainerTaskNormal>()!.Any(x => x.Item.Row == ItemId && x.GatheringLog.Row == 0 && x.FishingLog == 0) || DropSources.Sources!.Any(x => x.ItemId == ItemId)) Sources.Add(5);

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
            OriginListMaterials = materials;
            foreach (var recipe in OriginList.Recipes)
            {
                if (LuminaSheets.RecipeSheet[recipe.ID].UnkData5.Any(x => x.ItemIngredient == ItemId) && !UsedInCrafts.Contains(recipe.ID))
                    UsedInCrafts.Add(recipe.ID);
            }
            UsedInMaterialsList = materials.Where(x => LuminaSheets.RecipeSheet.Values.Any(y => y.ItemResult.Row == x.Key && y.UnkData5.Any(z => z.ItemIngredient == Data.RowId))).ToDictionary(x => x.Key, x => x.Value);
            RecipeOnList = originList.Recipes.Any(x => LuminaSheets.RecipeSheet[x.ID].ItemResult.Row == ItemId);
            if (P.Config.UseUniversalis && !P.Config.UniversalisOnDemand)
            {
                if (P.Config.LimitUnversalisToDC)
                    Task.Run(() => P.UniversalsisClient.GetDCData(ItemId, ref MarketboardData));
                else
                    Task.Run(() => P.UniversalsisClient.GetRegionData(ItemId, ref MarketboardData));
            }
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
                    retainerCount = Task.Run(() => RetainerInfo.GetRetainerItemCount(Data.RowId)).Result;
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

        public int TotalCraftable => NumberCraftable(Data.RowId);

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
                var owned = RetainerInfo.GetRetainerItemCount(recipe.ItemResult.Row) + CraftingListUI.NumberOfIngredient(recipe.ItemResult.Row);
                var numberUsedInRecipe = recipe.UnkData5.First(x => x.ItemIngredient == Data.RowId).AmountIngredient;
                var numberOnList = OriginList.Recipes.First(x => x.ID == craft).Quantity * recipe.AmountResult;
                if (IngredientHelper.HelperList.Any(x => x.Data.RowId == recipe.ItemResult.Row))
                {
                    var subing = IngredientHelper.HelperList.First(x => x.Data.RowId == recipe.ItemResult.Row);
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
            //    var recipe = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == material.Key && x.UnkData5.Any(y => y.ItemIngredient == Data.RowId));
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
            if (LuminaSheets.RecipeSheet.Values.FindFirst(x => x.ItemResult.Row == ItemId, out var recipe))
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