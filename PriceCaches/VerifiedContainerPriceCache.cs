using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using static GearAndLootValue.ContainerPricing;
namespace GearAndLootValue
{
    internal static class VerifiedContainerPriceCache
    {
        private static readonly Dictionary<string, int> Prices = new Dictionary<string, int>();
        private static bool _loaded;
        private static bool _dirty;

        private static string FilePath
        {
            get
            {
                string configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx", "config");

                if (!Directory.Exists(configDirectory))
                {
                    Directory.CreateDirectory(configDirectory);
                }

                return Path.Combine(configDirectory, "GearAndLootValue.VerifiedContainerPrices.json");
            }
        }

        public static void Load()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            try
            {
                if (!File.Exists(FilePath))
                {
                    Plugin.LogDebug("[AvgSellPrice] No verified container cache file found yet");
                    return;
                }

                string json = File.ReadAllText(FilePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    Plugin.Log?.LogWarning("[AvgSellPrice] Verified container cache file was empty");
                    return;
                }

                Dictionary<string, int> data = JsonConvert.DeserializeObject<Dictionary<string, int>>(json);

                if (data == null)
                {
                    Plugin.Log?.LogWarning("[AvgSellPrice] Verified container cache file could not be parsed");
                    return;
                }

                Prices.Clear();

                foreach (var pair in data)
                {
                    if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0)
                    {
                        Prices[pair.Key] = pair.Value;
                    }
                }

                Plugin.LogDebug($"[AvgSellPrice] Loaded {Prices.Count} verified container prices");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] Failed to load verified container cache: {ex.Message}");
            }
        }

        public static int GetPrice(string templateId)
        {
            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            int price;
            return Prices.TryGetValue(templateId, out price) ? price : 0;
        }

        public static void StoreVerifiedPrice(string templateId, int price)
        {
            if (string.IsNullOrEmpty(templateId) || price <= 0)
            {
                return;
            }

            int existing;
            if (Prices.TryGetValue(templateId, out existing) && existing == price)
            {
                return;
            }

            Prices[templateId] = price;
            _dirty = true;

            Plugin.LogDebug($"[AvgSellPrice] VERIFIED PRICE STORED {templateId} => {price}");

            Save();
        }

        private static void Save()
        {
            if (!_dirty)
            {
                return;
            }

            try
            {
                string json = JsonConvert.SerializeObject(Prices, Formatting.Indented);
                File.WriteAllText(FilePath, json);
                _dirty = false;

                Plugin.LogDebug($"[AvgSellPrice] Saved verified container cache to {FilePath}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] Failed to save verified container cache: {ex.Message}");
            }
        }
    }
}
