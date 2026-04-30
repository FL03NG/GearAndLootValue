using BepInEx.Logging;
using Newtonsoft.Json;
using SPT.Common.Http;
using System;
using System.Collections.Generic;

namespace AvgSellPrice
{
    internal static class WeaponDefaultPresetCache
    {
        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("AvgSellPrice.WeaponDefaultPresetCache");

        private static Dictionary<string, HashSet<string>> _defaultPartsByWeaponTemplate =
            new Dictionary<string, HashSet<string>>();

        private static bool _loaded;

        public static bool IsLoaded => _loaded;

        public static void Load()
        {
            try
            {
                string json = RequestHandler.GetJson("/AvgSellPrice/weaponDefaultPresets");

                if (string.IsNullOrEmpty(json))
                {
                    Log.LogWarning("[AvgSellPrice] Server mod returned empty weapon default preset response");
                    return;
                }

                var result = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                if (result == null || result.Count == 0)
                {
                    Log.LogWarning("[AvgSellPrice] No weapon default presets received from server mod");
                    return;
                }

                var map = new Dictionary<string, HashSet<string>>();
                foreach (var pair in result)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null || pair.Value.Count == 0)
                    {
                        continue;
                    }

                    map[pair.Key] = new HashSet<string>(pair.Value);
                }

                _defaultPartsByWeaponTemplate = map;
                _loaded = true;

                Log.LogInfo($"[AvgSellPrice] Loaded {_defaultPartsByWeaponTemplate.Count} weapon default presets from server mod");
            }
            catch (Exception ex)
            {
                Log.LogError($"[AvgSellPrice] Failed to load weapon default presets from server mod: {ex.Message}");
            }
        }

        public static bool IsDefaultPart(string weaponTemplateId, string partTemplateId)
        {
            if (!_loaded ||
                string.IsNullOrEmpty(weaponTemplateId) ||
                string.IsNullOrEmpty(partTemplateId))
            {
                return false;
            }

            return _defaultPartsByWeaponTemplate.TryGetValue(weaponTemplateId, out HashSet<string> defaultParts) &&
                   defaultParts.Contains(partTemplateId);
        }
    }
}
