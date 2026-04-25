using BepInEx.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;
using System;
using System.Collections.Generic;

namespace AvgSellPrice
{
    internal static class FleaPriceCache
    {
        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("AvgSellPrice.FleaPriceCache");

        private static Dictionary<string, int> _prices = new Dictionary<string, int>();
        private static Dictionary<string, bool> _sellable = new Dictionary<string, bool>();
        private static bool _loaded = false;

        public static bool IsLoaded => _loaded;

        public static void Load()
        {
            try
            {
                string json = RequestHandler.GetJson("/AvgSellPrice/fleaPrices");

                if (string.IsNullOrEmpty(json))
                {
                    Log.LogWarning("[AvgSellPrice] Server mod returned empty flea response");
                    return;
                }

                var payload = JObject.Parse(json);
                var prices = new Dictionary<string, int>();
                var sellable = new Dictionary<string, bool>();

                foreach (var property in payload.Properties())
                {
                    if (property.Value.Type == JTokenType.Integer)
                    {
                        prices[property.Name] = property.Value.Value<int>();
                        sellable[property.Name] = true;
                        continue;
                    }

                    if (property.Value.Type != JTokenType.Object)
                    {
                        continue;
                    }

                    int price = property.Value["price"]?.Value<int>() ?? property.Value["Price"]?.Value<int>() ?? 0;
                    bool canSell = property.Value["sellable"]?.Value<bool>() ?? property.Value["Sellable"]?.Value<bool>() ?? true;

                    if (price > 0)
                    {
                        prices[property.Name] = price;
                        sellable[property.Name] = canSell;
                    }
                }

                if (prices.Count == 0)
                {
                    Log.LogWarning("[AvgSellPrice] No flea prices received from server mod");
                    return;
                }

                _prices = prices;
                _sellable = sellable;
                _loaded = true;

                Log.LogInfo($"[AvgSellPrice] Loaded {_prices.Count} flea prices from server mod");
            }
            catch (Exception ex)
            {
                Log.LogError($"[AvgSellPrice] Failed to load flea prices from server mod: {ex.Message}");
            }
        }

        public static int GetPrice(string templateId)
        {
            if (!_loaded || string.IsNullOrEmpty(templateId))
            {
                return 0;
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
