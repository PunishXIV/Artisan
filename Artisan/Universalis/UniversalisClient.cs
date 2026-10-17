using ECommons;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Artisan.Universalis
{
    internal class UniversalisClient
    {
        private const string Endpoint = "https://universalis.app/api/v2/";
        // The API accepts 100 IDs per request, but data-center/region-wide queries that large time out at Universalis' gateway.
        private const int MaxItemsPerRequest = 25;
        private const int MaxParallelRequests = 3;

        // Only the fields ParseMarketboardData reads; trims the response a lot compared to the full item payload.
        private static readonly string[] ParsedFields =
        [
            "lastUploadTime", "averagePriceNQ", "averagePriceHQ", "currentAveragePriceNQ", "currentAveragePriceHQ",
            "minPriceNQ", "minPriceHQ", "maxPriceNQ", "maxPriceHQ", "listingsCount", "unitsForSale",
            "listings.worldName", "listings.quantity", "listings.total", "listings.pricePerUnit",
        ];

        private static readonly string SingleItemQuery = "?entries=0&fields=" + string.Join(",", ParsedFields);
        private static readonly string MultiItemQuery = "?entries=0&fields=" + string.Join(",", ParsedFields.Select(x => "items." + x));
        private readonly HttpClient httpClient;
        public uint? PlayerWorld;
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

        public MarketboardData? GetRegionData(ulong ItemId, ref MarketboardData? output)
        {
            var world = PlayerWorld;
            if (world == null)
                return null;

            var region = Regions.GetRegionByWorld(world.Value);
            if (region == null)
                return null;


            return output = GetMarketBoard(region, ItemId);
        }

        public MarketboardData? GetDCData(ulong ItemId, ref MarketboardData? output)
        {
            var world = PlayerWorld;
            if (world == null)
                return null;

            var region = DataCenters.GetDataCenterName(world.Value);
            if (region == null)
                return null;

            return output = GetMarketBoard(region, ItemId);
        }

        /// <summary>
        /// Fetches market data for many items via Universalis' multi-item endpoint, a few small batches at a time,
        /// instead of one request per item.
        /// </summary>
        public IReadOnlyDictionary<ulong, MarketboardData> GetBulkData(IEnumerable<ulong> itemIds, bool dataCenterOnly, CancellationToken token)
        {
            var output = new ConcurrentDictionary<ulong, MarketboardData>();
            var world = PlayerWorld;
            if (world == null)
                return output;

            var region = dataCenterOnly ? DataCenters.GetDataCenterName(world.Value) : Regions.GetRegionByWorld(world.Value);
            if (region == null)
                return output;

            try
            {
                Parallel.ForEach(itemIds.Distinct().Chunk(MaxItemsPerRequest),
                    new ParallelOptions { MaxDegreeOfParallelism = MaxParallelRequests, CancellationToken = token },
                    chunk => FetchChunk(region, chunk, output, token));
            }
            catch (OperationCanceledException) { }

            return output;
        }

        private void FetchChunk(string region, ulong[] ids, ConcurrentDictionary<ulong, MarketboardData> output, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            var json = GetJson(region, string.Join(",", ids), ids.Length > 1 ? MultiItemQuery : SingleItemQuery, retryOnRateLimit: true, out var transientFailure);
            if (json == null)
            {
                // Server-side timeouts get more likely the more items are asked for at once, so retry as two smaller requests.
                if (transientFailure && ids.Length > 1)
                {
                    var half = ids.Length / 2;
                    FetchChunk(region, ids[..half], output, token);
                    FetchChunk(region, ids[half..], output, token);
                }
                return;
            }

            // A multi-item response nests each item under "items"; a single ID returns the item object directly.
            if (json["items"] is JObject items)
            {
                foreach (var id in ids)
                {
                    if (items[id.ToString()] is JObject itemJson && ParseMarketboardData(itemJson, id, region) is { } data)
                        output[id] = data;
                }
            }
            else if (ids.Length == 1 && ParseMarketboardData(json, ids[0], region) is { } data)
            {
                output[ids[0]] = data;
            }
        }

        public void Dispose()
        {
            this.httpClient.Dispose();
        }

        private MarketboardData? GetMarketBoardData(string region, ulong ItemId)
        {
            var json = GetJson(region, ItemId.ToString(), string.Empty, retryOnRateLimit: false, out _);
            return json == null ? null : ParseMarketboardData(json, ItemId, region);
        }

        private JObject? GetJson(string region, string itemIds, string query, bool retryOnRateLimit, out bool transientFailure)
        {
            transientFailure = false;
            HttpResponseMessage result;
            try
            {
                result = this.GetMarketBoardDataAsync(region, itemIds, query).Result;
                if (retryOnRateLimit && result.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Thread.Sleep(1000);
                    result = this.GetMarketBoardDataAsync(region, itemIds, query).Result;
                }
            }
            catch (Exception ex)
            {
                // Dropped connections and client-side timeouts; worth retrying with a smaller batch.
                transientFailure = true;
                if (itemIds.Contains(','))
                    Svc.Log.Debug($"Universalis request for {itemIds.Count(c => c == ',') + 1} items failed ({ex.GetBaseException().Message}), retrying in smaller batches.");
                else
                    ex.Log();
                return null;
            }

            if (result.StatusCode is HttpStatusCode.GatewayTimeout or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            {
                transientFailure = true;
                if (itemIds.Contains(','))
                {
                    Svc.Log.Debug($"Universalis returned {result.StatusCode} for {itemIds.Count(c => c == ',') + 1} items, retrying in smaller batches.");
                    return null;
                }
            }

            if (result.StatusCode != HttpStatusCode.OK)
            {
                Svc.Log.Error(
                    "Failed to retrieve data from Universalis for ItemId {0} / worldId {1} with HttpStatusCode {2}.",
                    itemIds,
                    region,
                    result.StatusCode);
                return null;
            }

            var json = JsonConvert.DeserializeObject<JObject>(result.Content.ReadAsStringAsync().Result);
            if (json == null)
            {
                Svc.Log.Error(
                    "Failed to deserialize Universalis response for ItemId {0} / worldId {1}.",
                    itemIds,
                    region);
                return null;
            }

            return json;
        }

        private static MarketboardData? ParseMarketboardData(JObject itemJson, ulong ItemId, string region)
        {
            dynamic json = itemJson;
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
                if (json.listings != null && json.listings.Count > 0)
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

        private async Task<HttpResponseMessage> GetMarketBoardDataAsync(string? worldId, string itemIds, string query)
        {
            var request = Endpoint + worldId + "/" + itemIds + query;
            Svc.Log.Debug($"universalisRequest={request}");
            return await this.httpClient.GetAsync(new Uri(request));
        }
    }
}
