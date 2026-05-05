using BepInEx.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using System;
using System.Collections.Generic;
using static GearAndLootValue.ContainerPricing;
namespace GearAndLootValue
{
    internal static class FleaPriceCache
    {
        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("GearAndLootValue.FleaPriceCache");

        private static Dictionary<string, int> _prices = new Dictionary<string, int>();
        private static Dictionary<string, int> _bestPrices = new Dictionary<string, int>();
        private static Dictionary<string, int> _averagePrices = new Dictionary<string, int>();
        private static Dictionary<string, bool> _sellable = new Dictionary<string, bool>();
        private static bool _loaded = false;

        public static bool IsLoaded => _loaded;

        public static void Load(bool force = false)
        {
            if (!force && _loaded)
            {
                return;
            }

            try
            {
                string json = RequestHandler.GetJson("/AvgSellPrice/fleaPrices");

                if (string.IsNullOrEmpty(json))
                {
                    Log.LogWarning("[Gear & Loot Value] Server mod returned empty flea response");
                    return;
                }

                var payload = JObject.Parse(json);
                var prices = new Dictionary<string, int>();
                var bestPrices = new Dictionary<string, int>();
                var averagePrices = new Dictionary<string, int>();
                var sellable = new Dictionary<string, bool>();

                foreach (var property in payload.Properties())
                {
                    if (property.Value.Type == JTokenType.Integer)
                    {
                        int legacyPrice = property.Value.Value<int>();
                        prices[property.Name] = legacyPrice;
                        bestPrices[property.Name] = legacyPrice;
                        averagePrices[property.Name] = legacyPrice;
                        sellable[property.Name] = true;
                        continue;
                    }

                    if (property.Value.Type != JTokenType.Object)
                    {
                        continue;
                    }

                    int price = property.Value["price"]?.Value<int>() ?? property.Value["Price"]?.Value<int>() ?? 0;
                    int bestPrice = property.Value["bestPrice"]?.Value<int>() ?? property.Value["BestPrice"]?.Value<int>() ?? price;
                    int averagePrice = property.Value["averagePrice"]?.Value<int>() ?? property.Value["AveragePrice"]?.Value<int>() ?? price;
                    bool canSell = property.Value["sellable"]?.Value<bool>() ?? property.Value["Sellable"]?.Value<bool>() ?? true;

                    if (price > 0)
                    {
                        prices[property.Name] = price;
                        bestPrices[property.Name] = bestPrice > 0 ? bestPrice : price;
                        averagePrices[property.Name] = averagePrice > 0 ? averagePrice : price;
                        sellable[property.Name] = canSell;
                    }
                }

                if (prices.Count == 0)
                {
                    Log.LogWarning("[Gear & Loot Value] No flea prices received from server mod");
                    return;
                }

                _prices = prices;
                _bestPrices = bestPrices;
                _averagePrices = averagePrices;
                _sellable = sellable;
                _loaded = true;

                Log.LogInfo($"[Gear & Loot Value] Loaded {_prices.Count} flea prices from server mod");
            }
            catch (Exception ex)
            {
                Log.LogError($"[Gear & Loot Value] Failed to load flea prices from server mod: {ex.Message}");
            }
        }

        public static int GetPrice(string templateId)
        {
            if (!_loaded || string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            Dictionary<string, int> selectedPrices =
                PluginConfig.ContainerPriceMode != null && PluginConfig.ContainerPriceMode.Value == PriceMode.Best
                    ? _bestPrices
                    : _averagePrices;

            if (selectedPrices.TryGetValue(templateId, out int selectedPrice) && selectedPrice > 0)
            {
                return selectedPrice;
            }

            _prices.TryGetValue(templateId, out int price);
            return price;
        }

        public static bool IsSellable(string templateId)
        {
            if (!_loaded || string.IsNullOrEmpty(templateId))
            {
                return false;
            }

            return _sellable.TryGetValue(templateId, out bool sellable) && sellable;
        }
    }
}
