using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Artisan.RawInformation
{
    internal static class HelperExtensions
    {
        public static string GetNumbers(this string input)
        {
            return new string(input.Where(c => char.IsDigit(c)).ToArray());
        }
    }
}
