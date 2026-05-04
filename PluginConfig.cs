using BepInEx.Configuration;
using UnityEngine;


namespace AvgSellPrice
{
    public enum PriceMode
    {
        Average = 0,
        Best = 1
    }

    public enum PriceSource
    {
        TraderSell = 0,
        FleaMarket = 1
    }

    internal static class PluginConfig
    {
        private static readonly Color LegacyPlatesColor = new Color(1f, 0.82f, 0.40f, 1f);
        private static readonly Color LegacyTotalColor = new Color(0.49f, 1f, 0.70f, 1f);
        private static readonly Color DesiredPlatesColor = new Color(0.49f, 1f, 0.70f, 1f);
        private static readonly Color DesiredTotalColor = new Color(1f, 0.82f, 0.40f, 1f);

        public static ConfigEntry<bool> EnablePrice;
        public static ConfigEntry<bool> PrecisePrice;
        public static ConfigEntry<int> MinimumDisplayPrice;

        public static ConfigEntry<PriceMode> ContainerPriceMode;
        public static ConfigEntry<PriceSource> ItemPriceSource;
        public static ConfigEntry<bool> ShowTraderNameInTooltip;
        public static ConfigEntry<bool> ShowContentsPrice;
        public static ConfigEntry<bool> ShowPlatesPrice;
        public static ConfigEntry<bool> ShowMagazinePrice;
        public static ConfigEntry<bool> ShowWeaponAttachmentsPrice;
        public static ConfigEntry<bool> EnableValueDisplay;
        public static ConfigEntry<PriceSource> EquipmentValuePriceSource;
        public static ConfigEntry<bool> ShowEquipmentValue;
        public static ConfigEntry<bool> ShowRaidLootValue;
        public static ConfigEntry<bool> ShowAmmoPrice;
        public static ConfigEntry<bool> ShowCasePrice;
        public static ConfigEntry<bool> AlwaysShowFlea;
        public static ConfigEntry<bool> EnableHoverColors;
        public static ConfigEntry<Color> MainPriceColor;
        public static ConfigEntry<Color> AmmoPriceColor;
        public static ConfigEntry<Color> ContentsPriceColor;
        public static ConfigEntry<Color> PlatesPriceColor;
        public static ConfigEntry<Color> MagazinePriceColor;
        public static ConfigEntry<Color> AttachmentsPriceColor;
        public static ConfigEntry<Color> TotalPriceColor;
        public static ConfigEntry<Color> NotSellableOnFleaColor;

        public static ConfigEntry<Color> RaidValueLowColor;
        public static ConfigEntry<Color> RaidValueMidColor;
        public static ConfigEntry<Color> RaidValueHighColor;
        public static ConfigEntry<Color> RaidValueMaxColor;

        public static ConfigEntry<int> RaidValueMidThreshold;
        public static ConfigEntry<int> RaidValueHighThreshold;
        public static ConfigEntry<int> RaidValueMaxThreshold;
        public static ConfigEntry<bool> DebugLogging;




        public static void Init(ConfigFile config)
        {
            EnablePrice = config.Bind(
                "1. Price",
                "00. EnablePrice",
                true,
                "Enable item price hover text."
            );

            ItemPriceSource = config.Bind(
                "1. Price",
                "01. ItemPriceSource",
                PriceSource.TraderSell,
                "Choose whether item values use trader sell prices or flea market prices."
            );

            ContainerPriceMode = config.Bind(
                "1. Price",
                "02. ContainerPriceMode",
                PriceMode.Best,
                "Choose how container/contents prices are selected. With flea prices, Average uses the average of the cheapest live flea offers."
            );

            MinimumDisplayPrice = config.Bind(
                "1. Price",
                "03. ShowMinimumPrice",
                0,
                "If greater than 0, any positive price below this value will be displayed as this minimum instead."
            );

            PrecisePrice = config.Bind(
                "1. Price",
                "04. PrecisePrice",
                false,
                "Show full exact price instead of shortened values like 13k / 1.2m."
            );

            ShowTraderNameInTooltip = config.Bind(
                "1. Price",
                "05. ShowTraderNameInTooltip",
                true,
                "Show trader name in tooltip text."
            );

            ShowContentsPrice = config.Bind(
                "1. Price",
                "06. ShowContentsPrice",
                true,
                "Show container/rig contents price lines and include contents in total."
            );

            ShowPlatesPrice = config.Bind(
                "1. Price",
                "07. ShowPlatesPrice",
                true,
                "Show armor plate price lines. If disabled, plate value is folded into base price."
            );

            ShowMagazinePrice = config.Bind(
                "1. Price",
                "08. ShowMagazinePrice",
                true,
                "Show weapon magazine price lines. If disabled, magazine value is folded into base price."
            );

            ShowWeaponAttachmentsPrice = config.Bind(
                "1. Price",
                "09. ShowWeaponAttachmentsPrice",
                true,
                "Show weapon attachment price breakdown in weapon tooltips."
            );

            ShowAmmoPrice = config.Bind(
                "1. Price",
                "10. ShowAmmoPrice",
                true,
                "Show ammo price in ammo and magazine tooltips."
            );

            ShowCasePrice = config.Bind(
                "1. Price",
                "11. ShowCasePrice",
                true,
                "Show case and container value tooltips."
            );

            AlwaysShowFlea = config.Bind(
                "1. Price",
                "12. AlwaysShowFlea",
                false,
                "When ItemPriceSource is TraderSell, also show the flea base price above the base/trader price line."
            );

            EnableValueDisplay = config.Bind(
                "2. Equipment / Raid Value",
                "0. EnableEquipmentAndRaidValue",
                true,
                "Enable equipment value, raid loot value and raid end loot value displays."
            );

            EquipmentValuePriceSource = config.Bind(
                "2. Equipment / Raid Value",
                "EquipmentValuePriceSource",
                PriceSource.TraderSell,
                "Choose whether equipment value uses trader sell prices or flea market prices. Hover prices and raid loot still use ItemPriceSource."
            );
            EquipmentValuePriceSource.SettingChanged += (_, __) =>
            {
                ValueDisplayUI.RequestEquipmentValueRefresh(0f);
                ValueDisplayUI.RequestEquipmentValueRefresh(0.1f);
            };

            ShowEquipmentValue = config.Bind(
                "2. Equipment / Raid Value",
                "ShowEquipmentValue",
                true,
                "Show equipment value outside raids."
            );

            ShowRaidLootValue = config.Bind(
                "2. Equipment / Raid Value",
                "ShowRaidLootValue",
                true,
                "Show loot value during raids."
            );

            EnableHoverColors = config.Bind(
                "3. Hover Colors",
                "0. EnableHoverColors",
                true,
                "Enable colored hover price lines."
            );

            NotSellableOnFleaColor = config.Bind(
                "3. Hover Colors",
                "1. NotSellableOnFleaColor",
                Color.red,
                "Color for the 'Not sellable on flea' warning line."
            );

            MainPriceColor = config.Bind(
                "3. Hover Colors",
                "2. MainPriceColor",
                Color.white,
                "Color for the main/base price line."
            );

            PlatesPriceColor = config.Bind(
                "3. Hover Colors",
                "3. PlatesPriceColor",
                DesiredPlatesColor,
                "Color for the plates price line."
            );

            MagazinePriceColor = config.Bind(
                "3. Hover Colors",
                "4. MagazinePriceColor",
                DesiredPlatesColor,
                "Color for the weapon magazine price line."
            );

            ContentsPriceColor = config.Bind(
                "3. Hover Colors",
                "5. ContentsPriceColor",
                new Color(0.56f, 0.83f, 1f, 1f),
                "Color for the contents price line."
            );

            AttachmentsPriceColor = config.Bind(
                "3. Hover Colors",
                "6. AttachmentsPriceColor",
                new Color(0.56f, 0.83f, 1f, 1f),
                "Color for the weapon attachments price line."
            );

            AmmoPriceColor = config.Bind(
                "3. Hover Colors",
                "7. AmmoPriceColor",
                new Color(0.56f, 0.83f, 1f, 1f),
                "Color for the ammo price line inside magazines."
            );

            TotalPriceColor = config.Bind(
                "3. Hover Colors",
                "8. TotalPriceColor",
                DesiredTotalColor,
                "Color for the total price line."
            );

            RaidValueLowColor = config.Bind(
                "2. Equipment / Raid Value",
                "1. RaidValueLowColor",
                Color.white,
                "Starting color for raid loot value."
            );

            RaidValueMidColor = config.Bind(
                "2. Equipment / Raid Value",
                "2. RaidValueMidColor",
                Color.yellow,
                "Mid color for raid loot value."
            );

            RaidValueHighColor = config.Bind(
                "2. Equipment / Raid Value",
                "3. RaidValueHighColor",
                new Color(1f, 0.55f, 0f, 1f),
                "High color for raid loot value."
            );

            RaidValueMaxColor = config.Bind(
                "2. Equipment / Raid Value",
                "4. RaidValueMaxColor",
                Color.red,
                "Max color for raid loot value."
            );

            RaidValueMidThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "5. MidThreshold",
                250000,
                "Raid loot value where text reaches yellow."
            );

            RaidValueHighThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "6. HighThreshold",
                500000,
                "Raid loot value where text reaches orange."
            );

            RaidValueMaxThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "7. MaxThreshold",
                1000000,
                "Raid loot value where text reaches red."
            );

            DebugLogging = config.Bind(
                "5. Debug",
                "DebugLogging",
                false,
                "Enable verbose diagnostic logs. Keep disabled during normal raids."
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
