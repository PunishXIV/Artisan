using System.Collections.Generic;

namespace Artisan.Universalis
{
    public class MarketboardData
    {
        public long LastCheckTime { get; set; }

        public long LastUploadTime { get; set; }

        public double? AveragePriceNQ { get; set; }

        public double? AveragePriceHQ { get; set; }

        public double? CurrentAveragePriceNQ { get; set; }

        public double? CurrentAveragePriceHQ { get; set; }

        public double? MinimumPriceNQ { get; set; }

        public double? MinimumPriceHQ { get; set; }

        public double? MaximumPriceNQ { get; set; }

        public double? MaximumPriceHQ { get; set; }

        public double? CurrentMinimumPrice { get; set; } = -1;

        public double? ListingQuantity { get; set; }
        public string? LowestWorld { get; set; }

        public double? TotalNumberOfListings { get;  set; }

        public double? TotalQuantityOfUnits { get; set; }

        public List<Listing> AllListings { get; set; } = new();
    }

    public class Listing
    {
        public string World { get; set; } = "";

        public double Quantity { get; set; }

        public double TotalPrice { get; set; }

        public double UnitPrice { get; set; }
    }
}
