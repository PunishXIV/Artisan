using ECommons;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Artisan.Universalis
{
    internal class UniversalisClient
    {
        private const string Endpoint = "https://universalis.app/api/v2/";
        private readonly HttpClient httpClient;

        public UniversalisClient()
        {
            this.httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(10000),
            };
        }

        public MarketboardData? GetMarketBoard(string region, ulong ItemId)
        {
            var marketBoardFromAPI = this.GetMarketBoardData(region, ItemId);
            return marketBoardFromAPI;
        }

        public MarketboardData? GetRegionData(ulong ItemId, ref MarketboardData output)
        {
            var world = Svc.ClientState.LocalPlayer?.CurrentWorld.RowId;
            if (world == null)
                return null;

            var region = Regions.GetRegionByWorld(world.Value);
            if (region == null)
                return null;


            return output = GetMarketBoard(region, ItemId);
        }

        public MarketboardData? GetDCData(ulong ItemId, ref MarketboardData output)
        {
            var world = Svc.ClientState.LocalPlayer?.CurrentWorld.RowId;
            if (world == null)
                return null;

            var region = DataCenters.GetDataCenterName(world.Value);
            if (region == null)
                return null;

            return output = GetMarketBoard(region, ItemId);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        private MarketboardData? GetMarketBoardData(string region, ulong ItemId)
        {
            HttpResponseMessage result;
            try
            {
                result = this.GetMarketBoardDataAsync(region, ItemId).Result;
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }


            if (result.StatusCode != HttpStatusCode.OK)
            {
                Svc.Log.Error(
                    "Failed to retrieve data from Universalis for ItemId {0} / worldId {1} with HttpStatusCode {2}.",
                    ItemId,
                    region,
                    result.StatusCode);
                return null;
            }

            var json = JsonConvert.DeserializeObject<dynamic>(result.Content.ReadAsStringAsync().Result);
            if (json == null)
            {
                Svc.Log.Error(
                    "Failed to deserialize Universalis response for ItemId {0} / worldId {1}.",
                    ItemId,
                    region);
                return null;
            }

            try
            {
                var marketBoardData = new MarketboardData
                {
                    LastCheckTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    LastUploadTime = json.lastUploadTime?.Value,
                    AveragePriceNQ = json.averagePriceNQ?.Value,
                    AveragePriceHQ = json.averagePriceHQ?.Value,
                    CurrentAveragePriceNQ = json.currentAveragePriceNQ?.Value,
                    CurrentAveragePriceHQ = json.currentAveragePriceHQ?.Value,
                    MinimumPriceNQ = json.minPriceNQ?.Value,
                    MinimumPriceHQ = json.minPriceHQ?.Value,
                    MaximumPriceNQ = json.maxPriceNQ?.Value,
                    MaximumPriceHQ = json.maxPriceHQ?.Value,
                    TotalNumberOfListings = json.listingsCount?.Value,
                    TotalQuantityOfUnits = json.unitsForSale?.Value
                };
                if (json.listings.Count > 0)
                {
                    foreach (var item in json.listings)
                    {
                        Listing listing = new()
                        {
                            World = item.worldName.Value,
                            Quantity = item.quantity.Value,
                            TotalPrice = item.total.Value,
                            UnitPrice = item.pricePerUnit.Value
                        };

                        if (listing.World != "Cloudtest01" && listing.World != "Cloudtest02")
                            marketBoardData.AllListings.Add(listing);
                    }

                    marketBoardData.CurrentMinimumPrice = marketBoardData.AllListings.First().TotalPrice;
                    marketBoardData.LowestWorld = marketBoardData.AllListings.First().World;
                    marketBoardData.ListingQuantity = marketBoardData.AllListings.First().Quantity;
                }

                return marketBoardData;
            }
            catch (Exception ex)
            {
                Svc.Log.Error(
                    ex,
                    "Failed to parse marketBoard data for ItemId {0} / worldId {1}.",
                    ItemId,
                    region);
                return null;
            }
        }

        private async Task<HttpResponseMessage> GetMarketBoardDataAsync(string? worldId, ulong ItemId)
        {
            var request = Endpoint + worldId + "/" + ItemId;
            Svc.Log.Debug($"universalisRequest={request}");
            return await this.httpClient.GetAsync(new Uri(request));
        }
    }
}
