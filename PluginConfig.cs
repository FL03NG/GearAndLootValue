using BepInEx.Configuration;

namespace AvgSellPrice
{
    public enum PriceMode
    {
        Average = 0,
        Best = 1
    }

    internal static class PluginConfig
    {
        public static ConfigEntry<bool> PrecisePrice;
        public static ConfigEntry<bool> ShowAroundPrefix;
        public static ConfigEntry<int> MinimumDisplayPrice;

        public static ConfigEntry<PriceMode> ContainerPriceMode;
        public static ConfigEntry<bool> ShowTraderNameInTooltip;

        public static void Init(ConfigFile config)
        {
            PrecisePrice = config.Bind(
                "Display",
                "PrecisePrice",
                false,
                "Show full exact price instead of shortened values like 13k / 1.2m."
            );

            ShowAroundPrefix = config.Bind(
                "Display",
                "ShowAroundPrefix",
                true,
                "Show 'Around' before the displayed price."
            );

            MinimumDisplayPrice = config.Bind(
                "Display",
                "MinimumDisplayPrice",
                0,
                "If greater than 0, any positive price below this value will be displayed as this minimum instead."
            );

            ContainerPriceMode = config.Bind(
                "Pricing",
                "ContainerPriceMode",
                PriceMode.Best,
                "Choose how trader prices are selected: Average or Best."
            );

            ShowTraderNameInTooltip = config.Bind(
                "Pricing",
                "ShowTraderNameInTooltip",
                true,
                "Show trader name in tooltip text."
            );
        }
    }
}
