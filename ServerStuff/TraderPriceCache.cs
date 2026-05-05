using BepInEx.Logging;
using Newtonsoft.Json;
using SPT.Common.Http;
using System;
using System.Collections.Generic;
using static GearAndLootValue.ContainerPricing;
namespace GearAndLootValue
{
    internal static class TraderPriceCache
    {
        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("GearAndLootValue.TraderPriceCache");

        private static Dictionary<string, int> _prices = new Dictionary<string, int>();
        private static bool _loaded = false;

        public static bool IsLoaded => _loaded;

        public static void Load()
        {
            try
            {
                string json = RequestHandler.GetJson("/AvgSellPrice/traderBuyPrices");

                if (string.IsNullOrEmpty(json))
                {
                    Log.LogWarning("[AvgSellPrice] Server mod returnerede tom respons — er GearAndLootValue.Server.dll installeret?");
                    return;
                }

                var result = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);

                if (result == null || result.Count == 0)
                {
                    Log.LogWarning("[AvgSellPrice] Ingen priser modtaget fra server mod");
                    return;
                }

                _prices = result;
                _loaded = true;

                Log.LogInfo($"[AvgSellPrice] Loaded {_prices.Count} trader buy prices from server mod");
            }
            catch (Exception ex)
            {
                Log.LogError($"[AvgSellPrice] Kunne ikke hente priser fra server mod: {ex.Message}");
            }
        }

        public static int GetPrice(string templateId)
        {
            if (!_loaded || string.IsNullOrEmpty(templateId))
                return 0;

            _prices.TryGetValue(templateId, out int price);
            return price;
        }
    }
}
