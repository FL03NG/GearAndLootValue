using UnityEngine;

namespace GearAndLootValue
{
    internal sealed partial class ValueDisplayUI
    {
        private static Color GetRaidValueColor(int value)
        {
            return GetRaidValueThresholdColor(
                value,
                PluginConfig.RaidValueLowColor.Value,
                PluginConfig.RaidValueMidColor.Value,
                PluginConfig.RaidValueHighColor.Value,
                PluginConfig.RaidValueMaxColor.Value);
        }

        private static bool IsValueDisplayDisabled()
        {
            return PluginConfig.EnableValueDisplay != null && !PluginConfig.EnableValueDisplay.Value;
        }

        private static bool ValueAnimationsDisabled()
        {
            return PluginConfig.EnableValueAnimations != null && !PluginConfig.EnableValueAnimations.Value;
        }

        private static Color GetRaidValueThresholdColor(
            int value,
            Color lowColor,
            Color midColor,
            Color highColor,
            Color maxColor)
        {
            bool useFleaThresholds =
                PluginConfig.RaidValueThresholdSource != null &&
                PluginConfig.RaidValueThresholdSource.Value == PriceSource.FleaMarket;

            int mid = useFleaThresholds
                ? PluginConfig.FleaRaidValueMidThreshold.Value
                : PluginConfig.TraderSellRaidValueMidThreshold.Value;
            int high = useFleaThresholds
                ? PluginConfig.FleaRaidValueHighThreshold.Value
                : PluginConfig.TraderSellRaidValueHighThreshold.Value;
            int max = useFleaThresholds
                ? PluginConfig.FleaRaidValueMaxThreshold.Value
                : PluginConfig.TraderSellRaidValueMaxThreshold.Value;

            if (value <= mid)
            {
                float t = mid <= 0 ? 1f : (float)value / mid;
                return Color.Lerp(
                    lowColor,
                    midColor,
                    Mathf.Clamp01(t));
            }

            if (value <= high)
            {
                float t = high <= mid ? 1f : (float)(value - mid) / (high - mid);
                return Color.Lerp(
                    midColor,
                    highColor,
                    Mathf.Clamp01(t));
            }

            float finalT = max <= high ? 1f : (float)(value - high) / (max - high);
            return Color.Lerp(
                highColor,
                maxColor,
                Mathf.Clamp01(finalT));
        }

    }
}