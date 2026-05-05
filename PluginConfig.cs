using BepInEx.Configuration;
using UnityEngine;
using static GearAndLootValue.TarkovItemPrices;
namespace GearAndLootValue
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
        public static ConfigEntry<bool> EnableValueAnimations;
        public static ConfigEntry<PriceSource> EquipmentValuePriceSource;
        public static ConfigEntry<bool> ShowEquipmentValue;
        public static ConfigEntry<bool> ShowRaidLootValue;
        public static ConfigEntry<bool> ShowAmmoPrice;
        public static ConfigEntry<bool> ShowCasePrice;
        public static ConfigEntry<bool> AlwaysShowFlea;
        public static ConfigEntry<bool> AlwaysShowTraderSell;
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

        public static ConfigEntry<PriceSource> RaidValueThresholdSource;
        public static ConfigEntry<int> TraderSellRaidValueMidThreshold;
        public static ConfigEntry<int> TraderSellRaidValueHighThreshold;
        public static ConfigEntry<int> TraderSellRaidValueMaxThreshold;
        public static ConfigEntry<int> FleaRaidValueMidThreshold;
        public static ConfigEntry<int> FleaRaidValueHighThreshold;
        public static ConfigEntry<int> FleaRaidValueMaxThreshold;
        public static ConfigEntry<bool> DebugLogging;




        public static void Init(ConfigFile config)
        {
            EnablePrice = config.Bind(
                "1. Price",
                "00. Enable Price",
                true,
                "Enable item price hover text."
            );

            ItemPriceSource = config.Bind(
                "1. Price",
                "01. Item Price Source",
                PriceSource.TraderSell,
                "Choose whether item values use trader sell prices or flea market prices."
            );

            ContainerPriceMode = config.Bind(
                "1. Price",
                "02. Container Price Mode",
                PriceMode.Best,
                "Choose how container/contents prices are selected. With flea prices, Average uses the average of the cheapest live flea offers."
            );
            ContainerPriceMode.SettingChanged += (_, __) =>
            {
                TarkovItemPrices.ClearPriceCache();
                ValueDisplayUI.RequestAllValueRefresh(0f);
                ValueDisplayUI.RequestAllValueRefresh(0.1f);
            };

            MinimumDisplayPrice = config.Bind(
                "1. Price",
                "03. Show Minimum Price",
                0,
                "If greater than 0, any positive price below this value will be displayed as this minimum instead."
            );

            PrecisePrice = config.Bind(
                "1. Price",
                "04. Precise Price",
                false,
                "Show full exact price instead of shortened values like 13k / 1.2m."
            );

            ShowTraderNameInTooltip = config.Bind(
                "1. Price",
                "05. Show Trader Name In Hover Text",
                true,
                "Show trader name in hover text."
            );

            ShowContentsPrice = config.Bind(
                "1. Price",
                "06. Show Contents Price",
                true,
                "Show container/rig contents price lines and include contents in total."
            );

            ShowPlatesPrice = config.Bind(
                "1. Price",
                "07. Show Plates Price",
                true,
                "Show armor plate price lines. If disabled, plate value is folded into base price."
            );

            ShowMagazinePrice = config.Bind(
                "1. Price",
                "08. Show Magazine Price",
                true,
                "Show weapon magazine price lines. If disabled, magazine value is folded into base price."
            );

            ShowWeaponAttachmentsPrice = config.Bind(
                "1. Price",
                "09. Show Weapon Attachments Price",
                true,
                "Show weapon attachment price breakdown in weapon hover text."
            );

            ShowAmmoPrice = config.Bind(
                "1. Price",
                "10. Show Ammo Price",
                true,
                "Show ammo price in ammo and magazine hover text."
            );

            ShowCasePrice = config.Bind(
                "1. Price",
                "11. Show Case Price",
                true,
                "Show case and container value hover text."
            );

            AlwaysShowFlea = config.Bind(
                "1. Price",
                "12. Always Show Flea",
                false,
                "When Item Price Source is Trader Sell, also show the flea base price above the base/trader price line."
            );

            AlwaysShowTraderSell = config.Bind(
                "1. Price",
                "13. Always Show Trader Sell",
                false,
                "When Item Price Source is Flea Market, also show the trader sell price above the flea price line."
            );

            EnableValueDisplay = config.Bind(
                "2. Equipment / Raid Value",
                "00. Enable Equipment And Raid Value",
                true,
                "Enable equipment value, raid loot value and raid end loot value displays."
            );

            EnableValueAnimations = config.Bind(
                "2. Equipment / Raid Value",
                "01. Enable Value Animations",
                true,
                "Animate equipment value, raid loot value and raid end loot value changes."
            );

            EquipmentValuePriceSource = config.Bind(
                "2. Equipment / Raid Value",
                "02. Equipment Value Price Source",
                PriceSource.TraderSell,
                "Choose whether equipment value uses trader sell prices or flea market prices. Hover prices and raid loot still use Item Price Source."
            );
            EquipmentValuePriceSource.SettingChanged += (_, __) =>
            {
                ValueDisplayUI.RequestEquipmentValueRefresh(0f);
                ValueDisplayUI.RequestEquipmentValueRefresh(0.1f);
            };

            ShowEquipmentValue = config.Bind(
                "2. Equipment / Raid Value",
                "03. Show Equipment Value",
                true,
                "Show equipment value outside raids."
            );

            ShowRaidLootValue = config.Bind(
                "2. Equipment / Raid Value",
                "04. Show Raid Loot Value",
                true,
                "Show loot value during raids."
            );

            EnableHoverColors = config.Bind(
                "3. Hover Colors",
                "00. Enable Hover Colors",
                true,
                "Enable colored hover price lines."
            );

            NotSellableOnFleaColor = config.Bind(
                "3. Hover Colors",
                "01. Not Sellable On Flea Color",
                Color.red,
                "Color for the 'Not sellable on flea' warning line."
            );

            MainPriceColor = config.Bind(
                "3. Hover Colors",
                "02. Main Price Color",
                Color.white,
                "Color for the main/base price line."
            );

            PlatesPriceColor = config.Bind(
                "3. Hover Colors",
                "03. Plates Price Color",
                DesiredPlatesColor,
                "Color for the plates price line."
            );

            MagazinePriceColor = config.Bind(
                "3. Hover Colors",
                "04. Magazine Price Color",
                DesiredPlatesColor,
                "Color for the weapon magazine price line."
            );

            ContentsPriceColor = config.Bind(
                "3. Hover Colors",
                "05. Contents Price Color",
                new Color(0.56f, 0.83f, 1f, 1f),
                "Color for the contents price line."
            );

            AttachmentsPriceColor = config.Bind(
                "3. Hover Colors",
                "06. Attachments Price Color",
                new Color(0.56f, 0.83f, 1f, 1f),
                "Color for the weapon attachments price line."
            );

            AmmoPriceColor = config.Bind(
                "3. Hover Colors",
                "07. Ammo Price Color",
                new Color(0.56f, 0.83f, 1f, 1f),
                "Color for the ammo price line inside magazines."
            );

            TotalPriceColor = config.Bind(
                "3. Hover Colors",
                "08. Total Price Color",
                DesiredTotalColor,
                "Color for the total price line."
            );

            RaidValueLowColor = config.Bind(
                "2. Equipment / Raid Value",
                "05. Raid Value Low Color",
                Color.white,
                "Starting color for raid loot value."
            );

            RaidValueMidColor = config.Bind(
                "2. Equipment / Raid Value",
                "06. Raid Value Mid Color",
                Color.yellow,
                "Mid color for raid loot value."
            );

            RaidValueHighColor = config.Bind(
                "2. Equipment / Raid Value",
                "07. Raid Value High Color",
                new Color(1f, 0.55f, 0f, 1f),
                "High color for raid loot value."
            );

            RaidValueMaxColor = config.Bind(
                "2. Equipment / Raid Value",
                "08. Raid Value Max Color",
                Color.red,
                "Max color for raid loot value."
            );

            RaidValueThresholdSource = config.Bind(
                "2. Equipment / Raid Value",
                "09. Value Threshold Source",
                PriceSource.TraderSell,
                "Choose which threshold set controls equipment and raid value colors."
            );

            TraderSellRaidValueMidThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "10. Trader Sell Mid Threshold",
                250000,
                "Trader sell value where text reaches yellow."
            );

            TraderSellRaidValueHighThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "11. Trader Sell High Threshold",
                500000,
                "Trader sell value where text reaches orange."
            );

            TraderSellRaidValueMaxThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "12. Trader Sell Max Threshold",
                1000000,
                "Trader sell value where text reaches red."
            );

            FleaRaidValueMidThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "13. Flea Mid Threshold",
                400000,
                "Flea market value where text reaches yellow."
            );

            FleaRaidValueHighThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "14. Flea High Threshold",
                1500000,
                "Flea market value where text reaches orange."
            );

            FleaRaidValueMaxThreshold = config.Bind(
                "2. Equipment / Raid Value",
                "15. Flea Max Threshold",
                3000000,
                "Flea market value where text reaches red."
            );

            DebugLogging = config.Bind(
                "5. Debug",
                "Debug Logging",
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
