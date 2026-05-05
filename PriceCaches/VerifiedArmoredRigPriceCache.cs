using BepInEx;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using static GearAndLootValue.ContainerPricing;
namespace GearAndLootValue
{
    internal static class VerifiedArmoredRigPriceCache
    {
        private static readonly Dictionary<string, int> Prices = new Dictionary<string, int>();
        private static bool _loaded;
        private static bool _dirty;

        private static string FilePath
        {
            get
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string configDirectory = Path.Combine(baseDirectory, "BepInEx", "config");
                return Path.Combine(configDirectory, "GearAndLootValue.VerifiedArmoredRigPrices.v3.json");
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
                    Plugin.LogDebug("[AvgSellPrice] No verified armored rig cache file found yet");
                    return;
                }

                string json = File.ReadAllText(FilePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                Dictionary<string, int> loaded =
                    JsonConvert.DeserializeObject<Dictionary<string, int>>(json);

                if (loaded == null)
                {
                    return;
                }

                Prices.Clear();

                foreach (KeyValuePair<string, int> pair in loaded)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                    {
                        Prices[pair.Key] = pair.Value;
                    }
                }

                Plugin.LogDebug($"[AvgSellPrice] Loaded {Prices.Count} verified armored rig prices");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[AvgSellPrice] Failed loading verified armored rig cache: {ex}");
            }
        }

        public static int GetPrice(string templateId)
        {
            if (!_loaded)
            {
                Load();
            }

            if (string.IsNullOrWhiteSpace(templateId))
            {
                return 0;
            }

            return Prices.TryGetValue(templateId, out int price) ? price : 0;
        }

        public static void StorePrice(string templateId, int price)
        {
            if (!_loaded)
            {
                Load();
            }

            if (string.IsNullOrWhiteSpace(templateId) || price <= 0)
            {
                return;
            }

            if (Prices.TryGetValue(templateId, out int existing) && existing == price)
            {
                return;
            }

            Prices[templateId] = price;
            _dirty = true;
            Save();
        }

        public static void Save()
        {
            if (!_loaded || !_dirty)
            {
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(Prices, Formatting.Indented);
                File.WriteAllText(FilePath, json);

                _dirty = false;
                Plugin.LogDebug($"[AvgSellPrice] Saved {Prices.Count} verified armored rig prices");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[AvgSellPrice] Failed saving verified armored rig cache: {ex}");
            }
        }
    }
}

