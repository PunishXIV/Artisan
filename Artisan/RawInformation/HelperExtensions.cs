using Artisan.CraftingLogic.CraftData;
using Newtonsoft.Json;
using System.Linq;

namespace Artisan.RawInformation
{
    internal static class HelperExtensions
    {
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
    }

    public static class AddonExtensions
    {
        public static string ProgressString => LuminaSheets.AddonSheet[213].Text.RawString;
        public static string QualityString => LuminaSheets.AddonSheet[216].Text.RawString;
        public static string ConditionString => LuminaSheets.AddonSheet[215].Text.RawString;
        public static string DurabilityString => LuminaSheets.AddonSheet[214].Text.RawString;
        public static string ToLocalizedString(this Condition condition)
        {
            return condition switch
            {
                Condition.Poor => LuminaSheets.AddonSheet[229].Text.RawString,
                Condition.Normal => LuminaSheets.AddonSheet[226].Text.RawString,
                Condition.Good => LuminaSheets.AddonSheet[227].Text.RawString,
                Condition.Excellent => LuminaSheets.AddonSheet[228].Text.RawString,
                Condition.Centered => LuminaSheets.AddonSheet[239].Text.RawString,
                Condition.Sturdy => LuminaSheets.AddonSheet[240].Text.RawString,
                Condition.Pliant => LuminaSheets.AddonSheet[241].Text.RawString,
                Condition.Malleable => LuminaSheets.AddonSheet[13455].Text.RawString,
                Condition.Primed => LuminaSheets.AddonSheet[13454].Text.RawString,
                Condition.GoodOmen => LuminaSheets.AddonSheet[14214].Text.RawString,
                Condition.Unknown => "Unknown",
                _ => throw new System.NotImplementedException()
            };
        }
    }
}
