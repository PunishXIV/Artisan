using System;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Condition = Artisan.CraftingLogic.CraftData.Condition;

namespace Artisan.RawInformation
{
    internal static class HelperExtensions
    {
        public static int FindClosestIndex(this List<int> list, int valueToFind)
        {
            int closestIndex = 0;
            int smallestDifference = int.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                int currentDifference = Math.Abs(list[i] - valueToFind);
                if (currentDifference < smallestDifference)
                {
                    smallestDifference = list[i];
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        public static int GetByIndexOrDefault(this List<int> list, int index, int defaultValue = 0)
        {
            if (index < 0 || index >= list.Count)
                return defaultValue;
            return list[index];
        }

        public static string GetNumbers(this string input)
        {
            if (input == null) return "";
            if (input.Length == 0) return "";

            var numbers = new string(input.Where(c => char.IsDigit(c)).ToArray());
            return numbers;
        }

        public static string GetLast(this string source, int tail_length)
        {
            if (tail_length >= source.Length)
                return source;
            return source.Substring(source.Length - tail_length);
        }

        public static bool TryParseJson<T>(this string @this, out T result)
        {
            bool success = true;
            var settings = new JsonSerializerSettings
            {
                Error = (sender, args) => { success = false; args.ErrorContext.Handled = true; },
                MissingMemberHandling = MissingMemberHandling.Error
            };
            result = JsonConvert.DeserializeObject<T>(@this, settings)!;
            return success;
        }

        internal record IngredientExtension()
        {
            public required Item Item { get; init; }
            public required int Amount { get; init; }
        }

        internal record SupplyItemExtension()
        {
            public required CompanyCraftSupplyItem SupplyItem { get; init; }
            public required int SetsRequired { get; init; }
            public required int SetQuantity { get;init; }
        }

        internal record BaseParamExtension()
        {
            public required BaseParam BaseParam { get; init; }
            public required short BaseParamValue { get; init; } 
        }

        internal record BaseParamSpecialExtension()
        {
            public required BaseParam BaseParamSpecial { get; init; }
            public required short BaseParamValueSpecial { get; init; }

        }

        public static IEnumerable<BaseParamSpecialExtension> BaseParamSpecials(this Item baseParam)
        {
            var output = new List<BaseParamSpecialExtension>();
            for (int i = 0; i < baseParam.BaseParamSpecial.Count; i++)
            {
                try
                {
                    var item = baseParam.BaseParamSpecial[i].Value;
                    var val = baseParam.BaseParamValueSpecial[i];

                    output.Add(new BaseParamSpecialExtension() { BaseParamSpecial = item, BaseParamValueSpecial = val });
                }
                catch { }
            }

            return output;
        }

        public static IEnumerable<BaseParamExtension> BaseParams(this Item baseParam)
        {
            var output = new List<BaseParamExtension>();
            for (int i = 0; i < baseParam.BaseParam.Count; i++)
            {
                try
                {
                    var item = baseParam.BaseParam[i].Value;
                    var val = baseParam.BaseParamValue[i];

                    output.Add(new BaseParamExtension() { BaseParam = item, BaseParamValue = val });
                }
                catch { }
            }

            return output;
        }

        public static IEnumerable<SupplyItemExtension> SupplyItems(this CompanyCraftProcess companyCraftProcess)
        {
            var output = new List<SupplyItemExtension>();
            for (int i = 0; i < companyCraftProcess.SupplyItem.Count; i++)
            {
                var item = companyCraftProcess.SupplyItem[i].Value;
                var setsRequired = companyCraftProcess.SetsRequired[i];
                var setQuantity = companyCraftProcess.SetQuantity[i];

                output.Add(new SupplyItemExtension() { SupplyItem = item, SetQuantity = setQuantity, SetsRequired = setsRequired });
            }

            return output;
        }

        public static IEnumerable<IngredientExtension> Ingredients(this Recipe recipe)
        {
            var output = new List<IngredientExtension>();
            for (int i = 0; i < recipe.Ingredient.Count; i++)
            {
                try
                {
                    var item = recipe.Ingredient[i].Value;
                    var amount = recipe.AmountIngredient[i];

                    output.Add(new IngredientExtension() { Item = item, Amount = amount });
                }
                catch { }
            }

            return output;
        }
    }

    public static class AddonExtensions
    {
        public static string ProgressString => LuminaSheets.AddonSheet[213].Text.ToString();
        public static string QualityString => LuminaSheets.AddonSheet[216].Text.ToString();
        public static string ConditionString => LuminaSheets.AddonSheet[215].Text.ToString();
        public static string DurabilityString => LuminaSheets.AddonSheet[214].Text.ToString();
        public static string ToLocalizedString(this Condition condition)
        {
            return condition switch
            {
                Condition.Poor => LuminaSheets.AddonSheet[229].Text.ToString(),
                Condition.Normal => LuminaSheets.AddonSheet[226].Text.ToString(),
                Condition.Good => LuminaSheets.AddonSheet[227].Text.ToString(),
                Condition.Excellent => LuminaSheets.AddonSheet[228].Text.ToString(),
                Condition.Centered => LuminaSheets.AddonSheet[239].Text.ToString(),
                Condition.Sturdy => LuminaSheets.AddonSheet[240].Text.ToString(),
                Condition.Pliant => LuminaSheets.AddonSheet[241].Text.ToString(),
                Condition.Malleable => LuminaSheets.AddonSheet[13455].Text.ToString(),
                Condition.Primed => LuminaSheets.AddonSheet[13454].Text.ToString(),
                Condition.GoodOmen => LuminaSheets.AddonSheet[14214].Text.ToString(),
                Condition.Unknown => "Unknown",
                _ => throw new System.NotImplementedException()
            };
        }
    }
}
