using Dalamud.Logging;
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

        public MarketboardData? GetMarketBoard(string region, ulong itemId)
        {
            var marketBoardFromAPI = this.GetMarketBoardData(region, itemId);
            return marketBoardFromAPI;
        }

        public MarketboardData? GetRegionData(ulong itemId)
        {
            var world = Svc.ClientState.LocalPlayer?.CurrentWorld.Id;
            if (world == null)
                return null;

            var region = Regions.GetRegionByWorld(world.Value);
            if (region == null)
                return null;


            return GetMarketBoard(region, itemId);
        }

        public MarketboardData? GetDataCenterData(ulong itemId)
        {
            var world = Svc.ClientState.LocalPlayer?.CurrentWorld.Id;
            if (world == null)
                return null;

            var datacenter = DataCenters.GetDataCenterNameByWorld(world.Value);
            if (datacenter == null)
                return null;

            return GetMarketBoard(datacenter, itemId);
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        private MarketboardData? GetMarketBoardData(string region, ulong itemId)
        {
            HttpResponseMessage result;
            try
            {
                result = this.GetMarketBoardDataAsync(region, itemId).Result;
            }
            catch (Exception ex)
            {
                ex.Log();
                return null;
            }


            if (result.StatusCode != HttpStatusCode.OK)
            {
                PluginLog.LogError(
                    "Failed to retrieve data from Universalis for itemId {0} / worldId {1} with HttpStatusCode {2}.",
                    itemId,
                    region,
                    result.StatusCode);
                return null;
            }

            var json = JsonConvert.DeserializeObject<dynamic>(result.Content.ReadAsStringAsync().Result);
            if (json == null)
            {
                PluginLog.LogError(
                    "Failed to deserialize Universalis response for itemId {0} / worldId {1}.",
                    itemId,
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
                PluginLog.LogError(
                    ex,
                    "Failed to parse marketBoard data for itemId {0} / worldId {1}.",
                    itemId,
                    region);
                return null;
            }
        }

        private async Task<HttpResponseMessage> GetMarketBoardDataAsync(string? worldId, ulong itemId)
        {
            var request = Endpoint + worldId + "/" + itemId;
            PluginLog.LogDebug($"universalisRequest={request}");
            return await this.httpClient.GetAsync(new Uri(request));
        }
    }
}
