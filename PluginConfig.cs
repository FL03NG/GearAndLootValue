using BepInEx.Configuration;
using UnityEngine;


namespace AvgSellPrice
{
    public enum PriceMode
    {
        Average = 0,
        Best = 1
    }

    internal static class PluginConfig
    {
        private static readonly Color LegacyPlatesColor = new Color(1f, 0.82f, 0.40f, 1f);
        private static readonly Color LegacyTotalColor = new Color(0.49f, 1f, 0.70f, 1f);
        private static readonly Color DesiredPlatesColor = new Color(0.49f, 1f, 0.70f, 1f);
        private static readonly Color DesiredTotalColor = Color.red;

        public static ConfigEntry<bool> PrecisePrice;
        public static ConfigEntry<bool> ShowAroundPrefix;
        public static ConfigEntry<int> MinimumDisplayPrice;

        public static ConfigEntry<PriceMode> ContainerPriceMode;
        public static ConfigEntry<bool> ShowTraderNameInTooltip;
        public static ConfigEntry<Color> MainPriceColor;
        public static ConfigEntry<Color> ContentsPriceColor;
        public static ConfigEntry<Color> PlatesPriceColor;
        public static ConfigEntry<Color> TotalPriceColor;

        public static ConfigEntry<Color> RaidValueLowColor;
        public static ConfigEntry<Color> RaidValueMidColor;
        public static ConfigEntry<Color> RaidValueHighColor;
        public static ConfigEntry<Color> RaidValueMaxColor;

        public static ConfigEntry<int> RaidValueMidThreshold;
        public static ConfigEntry<int> RaidValueHighThreshold;
        public static ConfigEntry<int> RaidValueMaxThreshold;




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
            MainPriceColor = config.Bind(
                "Colors",
                "MainPriceColor",
                new Color(1f, 0.82f, 0.40f, 1f),
                "Color for the main price line."
            );

            ContentsPriceColor = config.Bind(
                "Colors",
                "ContentsPriceColor",
                new Color(0.56f, 0.83f, 1f, 1f),
                "Color for the contents price line."
            );

            PlatesPriceColor = config.Bind(
                "Colors",
                "PlatesPriceColor",
                DesiredPlatesColor,
                "Color for the plates price line."
            );

            TotalPriceColor = config.Bind(
                "Colors",
                "TotalPriceColor",
                DesiredTotalColor,
                "Color for the total price line."
            );
            RaidValueLowColor = config.Bind(
    "Raid Value",
    "LowColor",
    Color.white,
    "Starting color for raid loot value."
);

            RaidValueMidColor = config.Bind(
                "Raid Value",
                "MidColor",
                Color.yellow,
                "Mid color for raid loot value."
            );

            RaidValueHighColor = config.Bind(
                "Raid Value",
                "HighColor",
                new Color(1f, 0.55f, 0f, 1f),
                "High color for raid loot value."
            );

            RaidValueMaxColor = config.Bind(
                "Raid Value",
                "MaxColor",
                Color.red,
                "Max color for raid loot value."
            );

            RaidValueMidThreshold = config.Bind(
                "Raid Value",
                "MidThreshold",
                100000,
                "Raid loot value where text reaches yellow."
            );

            RaidValueHighThreshold = config.Bind(
                "Raid Value",
                "HighThreshold",
                300000,
                "Raid loot value where text reaches orange."
            );

            RaidValueMaxThreshold = config.Bind(
                "Raid Value",
                "MaxThreshold",
                700000,
                "Raid loot value where text reaches red."
            );

            ApplyLegacyColorMigration();

        }

        private static void ApplyLegacyColorMigration()
        {
            if (ColorsEqual(PlatesPriceColor.Value, LegacyPlatesColor) &&
                ColorsEqual(TotalPriceColor.Value, LegacyTotalColor))
            {
                PlatesPriceColor.Value = DesiredPlatesColor;
                TotalPriceColor.Value = DesiredTotalColor;
                return;
            }

            if (ColorsEqual(PlatesPriceColor.Value, DesiredTotalColor) &&
                ColorsEqual(TotalPriceColor.Value, DesiredPlatesColor))
            {
                PlatesPriceColor.Value = DesiredPlatesColor;
                TotalPriceColor.Value = DesiredTotalColor;
            }
        }

        private static bool ColorsEqual(Color left, Color right)
        {
            return Mathf.Approximately(left.r, right.r) &&
                   Mathf.Approximately(left.g, right.g) &&
                   Mathf.Approximately(left.b, right.b) &&
                   Mathf.Approximately(left.a, right.a);
        }
    }
}
