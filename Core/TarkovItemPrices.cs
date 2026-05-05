using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


using CurrencyUtil = GClass3130;
using static GearAndLootValue.TarkovItemPrices;
using static GearAndLootValue.SellPriceTooltip;
using static GearAndLootValue.TraderSellOffers;
using static GearAndLootValue.ArmorPricing;
using static GearAndLootValue.ContainerPricing;
using static GearAndLootValue.WeaponPricing;
using static GearAndLootValue.EftReflection;
namespace GearAndLootValue
{
    internal static class TarkovItemPrices
    {
        internal static ISession _session;
        internal static PriceSource? _priceSourceOverride;
        internal static readonly HashSet<string> _pmcGearSlots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Headwear",
                "Earpiece",
                "FaceCover",
                "Eyewear",
                "ArmorVest",
                "TacticalVest",
                "Backpack",
                "FirstPrimaryWeapon",
                "SecondPrimaryWeapon",
                "Holster",
                "Scabbard",
                "ArmBand",
                "Dogtag",
                "SecuredContainer",
                "Pockets"
            };

        internal static readonly Dictionary<string, int> _cleanBasePriceCache =
            new Dictionary<string, int>();

        internal static ISession Session
        {
            get
            {
                if (_session == null)
                {
                    object app = ClientAppUtils.GetMainApp();

                    if (app == null)
                    {
                        return null;
                    }

                    _session = ClientAppUtils.GetMainApp().GetClientBackEndSession();
                }

                return _session;
            }
        }

        internal static bool ShowContentsBreakdown =>
            PluginConfig.ShowContentsPrice == null || PluginConfig.ShowContentsPrice.Value;

        internal static bool ShowPlatesBreakdown =>
            PluginConfig.ShowPlatesPrice == null || PluginConfig.ShowPlatesPrice.Value;

        internal static bool ShowMagazineBreakdown =>
            PluginConfig.ShowMagazinePrice == null || PluginConfig.ShowMagazinePrice.Value;

        internal static bool ShowAmmoPrice =>
            PluginConfig.ShowAmmoPrice == null || PluginConfig.ShowAmmoPrice.Value;

        internal class TraderOffer
        {
            public string Name;
            public int Price;
            public string Currency;
            public double Course;

            public TraderOffer(string name, int price, string currency, double course)
            {
                Name = name;
                Price = price;
                Currency = currency;
                Course = course;
            }
        }

        internal static bool UseFleaPriceSource =>
            ActiveSource() == PriceSource.FleaMarket;

        internal static PriceSource ActiveSource()
        {
            if (_priceSourceOverride.HasValue)
            {
                return _priceSourceOverride.Value;
            }

            return PluginConfig.ItemPriceSource != null
                ? PluginConfig.ItemPriceSource.Value
                : PriceSource.TraderSell;
        }

        internal static int GetFleaTemplatePrice(Item item)
        {
            string templateId = TemplateId(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            if (CannotSellOnFlea(item))
            {
                return 0;
            }

            return FleaPriceCache.GetPrice(templateId);
        }

        internal static bool CannotSellOnFlea(Item item)
        {
            string templateId = TemplateId(item);

            if (TryGetTemplateCanSellOnFlea(item, out bool canSellOnFlea))
            {
                return !canSellOnFlea;
            }

            if (!FleaPriceCache.IsLoaded || string.IsNullOrEmpty(templateId))
            {
                return false;
            }

            return !FleaPriceCache.IsSellable(templateId);
        }

        internal static bool TryGetTemplateCanSellOnFlea(Item item, out bool canSell)
        {
            canSell = true;

            if (item == null || item.Template == null)
            {
                return false;
            }

            if (ReadBoolFlag(item.Template, "CanSellOnRagfair", out canSell))
            {
                return true;
            }

            object props = FindMemberValue(item.Template, "Props", "_props");
            return props != null && ReadBoolFlag(props, "CanSellOnRagfair", out canSell);
        }

        // don't spoil unknown raid loot
        internal static bool HideUnsearchedLootValue(Item item)
        {
            return (ValueTracker.IsInRaid || RaidPlayerState.MainPlayer != null) &&
                   item != null &&
                   (IsRealContainer(item) || IsArmoredRig(item)) &&
                   IsUnsearchedLootContainer(item);
        }

        internal static bool IsUnsearchedLootContainer(Item item)
        {
            if (item == null)
            {
                return false;
            }

            SearchableItemItemClass searchableItem = item as SearchableItemItemClass;
            if (searchableItem != null)
            {
                try
                {
                    IPlayerSearchController searchController = RaidPlayerState.MainPlayer?.SearchController;
                    if (searchController != null)
                    {
                        return !searchController.IsSearched(searchableItem) ||
                               searchController.ContainsUnknownItems(searchableItem);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.LogDebug($"[Gear & Loot Value] Search state check failed for {item.ShortName}: {ex.Message}");
                }

                return !searchableItem.IsSearched;
            }

            if (TryReadSearchState(item, out bool searched))
            {
                return !searched;
            }

            object searchInfo = FindMemberValue(
                item,
                "SearchInfo",
                "SearchData",
                "SearchState",
                "SearchComponent",
                "SearchableComponent",
                "Searchable");

            if (TryReadSearchState(searchInfo, out searched))
            {
                return !searched;
            }

            object updateData = FindMemberValue(item, "upd", "Upd", "UpdateData", "Updatable");
            object updateSearchInfo = FindMemberValue(
                updateData,
                "SearchInfo",
                "SearchData",
                "SearchState",
                "Searchable");

            return TryReadSearchState(updateSearchInfo, out searched) && !searched;
        }

        internal static bool TryReadSearchState(object source, out bool searched)
        {
            searched = true;

            if (source == null)
            {
                return false;
            }

            if (ReadBoolFlag(source, "Searched", out searched) ||
                ReadBoolFlag(source, "IsSearched", out searched) ||
                ReadBoolFlag(source, "WasSearched", out searched) ||
                ReadBoolFlag(source, "SearchComplete", out searched) ||
                ReadBoolFlag(source, "IsSearchComplete", out searched) ||
                ReadBoolFlag(source, "FullySearched", out searched) ||
                ReadBoolFlag(source, "IsFullySearched", out searched))
            {
                return true;
            }

            if (ReadBoolFlag(source, "Known", out searched) ||
                ReadBoolFlag(source, "IsKnown", out searched))
            {
                return true;
            }

            return false;
        }

        internal static string GetUnsearchedHoverLine()
        {
            return Colorize("Unsearched", PluginConfig.MainPriceColor.Value);
        }

        internal static string NotSellableFleaText(Item item)
        {
            List<string> lines = new List<string>();
            lines.Add(Colorize("Not sellable on flea", PluginConfig.NotSellableOnFleaColor.Value));

            TraderOffer traderOffer = TraderSellOffer(item);
            int sellPrice = traderOffer != null ? traderOffer.Price : 0;
            string traderName = PluginConfig.ShowTraderNameInTooltip.Value && traderOffer != null
                ? traderOffer.Name
                : string.Empty;

            if (item is AmmoItemClass)
            {
                if (!ShowAmmoPrice)
                {
                    return string.Join(Environment.NewLine, lines);
                }

                int ammoPrice = IsTraderStockItem(item)
                    ? PriceSingleItem(item)
                    : PriceStack(item);

                if (ammoPrice > 0)
                {
                    lines.Add(Colorize(
                        FormatMainPriceWithOptionalTrader(traderName, ammoPrice, applyMinimum: false),
                        PluginConfig.MainPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (item is Weapon)
            {
                int attachmentsPrice = PriceWeaponMods(item);
                int magazinePrice = GetWeaponMagazineTraderPrice(item);
                TraderOffer weaponBaseOffer = GetWeaponBaseTraderSellOffer(item);
                int basePrice = weaponBaseOffer != null && weaponBaseOffer.Price > 0
                    ? weaponBaseOffer.Price
                    : sellPrice > 0
                        ? Math.Max(0, sellPrice - attachmentsPrice - magazinePrice)
                        : GetTemplateFallbackPrice(item);
                int totalPrice = basePrice + attachmentsPrice + magazinePrice;
                string weaponTraderName = PluginConfig.ShowTraderNameInTooltip.Value && weaponBaseOffer != null
                    ? weaponBaseOffer.Name
                    : traderName;
                bool showWeaponAttachments = PluginConfig.ShowWeaponAttachmentsPrice == null ||
                                             PluginConfig.ShowWeaponAttachmentsPrice.Value;
                bool showMagazine = ShowMagazineBreakdown;
                int visibleBasePrice = basePrice > 0 ? basePrice : totalPrice;
                if (!showMagazine)
                {
                    visibleBasePrice += magazinePrice;
                }
                if (!showWeaponAttachments)
                {
                    visibleBasePrice += attachmentsPrice;
                }

                if (basePrice > 0 || totalPrice > 0)
                {
                    lines.Add(Colorize(
                        FormatBasePriceWithOptionalTrader(weaponTraderName, visibleBasePrice),
                        PluginConfig.MainPriceColor.Value));
                }

                if (showWeaponAttachments && attachmentsPrice > 0 || showMagazine && magazinePrice > 0)
                {
                    if (showMagazine && magazinePrice > 0)
                    {
                        lines.Add(Colorize(
                            "Mag " + FormatPriceExternal(magazinePrice),
                            PluginConfig.MagazinePriceColor.Value));
                    }

                    if (showWeaponAttachments && attachmentsPrice > 0)
                    {
                        lines.Add(Colorize(
                            "Attachments " + FormatPriceExternal(attachmentsPrice),
                            PluginConfig.AttachmentsPriceColor.Value));
                    }

                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (NeedsModBreakdown(item))
            {
                int attachmentsPrice = GetModAttachmentPrice(item);
                int basePrice = sellPrice > 0
                    ? Math.Max(0, sellPrice - attachmentsPrice)
                    : PriceSingleItem(item);
                int totalPrice = basePrice + attachmentsPrice;

                if (basePrice > 0 || totalPrice > 0)
                {
                    lines.Add(Colorize(
                        FormatMainPriceWithOptionalTrader(traderName, basePrice > 0 ? basePrice : totalPrice),
                        PluginConfig.MainPriceColor.Value));
                }

                if (attachmentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Attachments " + FormatPriceExternal(attachmentsPrice),
                        PluginConfig.AttachmentsPriceColor.Value));

                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (IsArmoredRig(item))
            {
                TraderOffer rigOffer = GetArmoredRigTraderOffer(item);
                int basePrice = rigOffer != null && rigOffer.Price > 0
                    ? rigOffer.Price
                    : GetArmoredRigBasePrice(item);
                string rigTraderName = PluginConfig.ShowTraderNameInTooltip.Value && rigOffer != null
                    ? rigOffer.Name
                    : traderName;

                if (HideUnsearchedLootValue(item))
                {
                    if (basePrice > 0)
                    {
                        lines.Add(Colorize(
                            FormatMainPriceWithOptionalTrader(rigTraderName, basePrice),
                            PluginConfig.MainPriceColor.Value));
                    }

                    lines.Add(GetUnsearchedHoverLine());
                    return string.Join(Environment.NewLine, lines);
                }

                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = PriceContents(item);
                bool showPlates = ShowPlatesBreakdown;
                bool showContents = ShowContentsBreakdown;
                int displayBasePrice = basePrice + (showPlates ? 0 : platesPrice);
                int visibleContentsPrice = showContents ? contentsPrice : 0;

                if (displayBasePrice > 0)
                {
                    lines.Add(Colorize(
                        FormatMainPriceWithOptionalTrader(rigTraderName, displayBasePrice),
                        PluginConfig.MainPriceColor.Value));
                }

                if (showPlates && platesPrice > 0)
                {
                    lines.Add(Colorize(
                        "Plates " + FormatPriceExternal(platesPrice),
                        PluginConfig.PlatesPriceColor.Value));
                }

                if (showContents && contentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Contents " + FormatContentsPriceVisual(contentsPrice),
                        PluginConfig.ContentsPriceColor.Value));
                }

                if (showPlates && platesPrice > 0 || showContents && contentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(displayBasePrice + (showPlates ? platesPrice : 0) + visibleContentsPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (IsRealContainer(item))
            {
                TraderOffer containerOffer = GetContainerBaseTraderOffer(item);
                int basePrice = ContainerBasePrice(item);
                if (containerOffer != null && containerOffer.Price > 0)
                {
                    basePrice = containerOffer.Price;
                }

                string containerTraderName = PluginConfig.ShowTraderNameInTooltip.Value
                    ? GetContainerBaseTraderName(item, containerOffer)
                    : string.Empty;

                if (basePrice > 0)
                {
                    lines.Add(Colorize(
                        FormatMainPriceWithOptionalTrader(containerTraderName, basePrice),
                        PluginConfig.MainPriceColor.Value));
                }

                if (HideUnsearchedLootValue(item))
                {
                    lines.Add(GetUnsearchedHoverLine());
                    return string.Join(Environment.NewLine, lines);
                }

                if (!ShowContentsBreakdown)
                {
                    return string.Join(Environment.NewLine, lines);
                }

                int contentsPrice = PriceContents(item);
                if (contentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Contents " + FormatContentsPriceVisual(contentsPrice),
                        PluginConfig.ContentsPriceColor.Value));

                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(basePrice + contentsPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (sellPrice > 0)
            {
                lines.Add(Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, sellPrice),
                    PluginConfig.MainPriceColor.Value));
            }

            return string.Join(Environment.NewLine, lines);
        }

        internal static TraderOffer GetFleaPriceOffer(Item item)
        {
            int fleaPrice = GetFleaTemplatePrice(item);

            if (fleaPrice <= 0 || CannotSellOnFlea(item))
            {
                return null;
            }

            return new TraderOffer("Flea", fleaPrice, "RUB", 1d);
        }

        internal static bool HasChildren(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return item.GetAllItems().Any(x => x != item);
        }

        internal static bool IsRealContainer(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (IsArmoredRig(item))
            {
                return false;
            }

            if (item is BackpackItemClass)
            {
                return true;
            }

            if (item is VestItemClass)
            {
                return true;
            }

            if (item is SearchableItemItemClass)
            {
                return true;
            }

            CompoundItem compound = item as CompoundItem;
            if (compound != null && compound.Grids != null)
            {
                foreach (var grid in compound.Grids)
                {
                    if (grid != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        internal static string Colorize(string text, Color color)
        {
            return SellPriceTooltip.Colorize(text, color);
        }
        internal static string Colorize(string text, string color)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(color))
            {
                return text;
            }

            return "<color=" + color + ">" + text + "</color>";
        }

        public static int GetTotalSellValue(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int moneyValue = GetMoneyStackValue(item);
            if (moneyValue > 0)
            {
                return moneyValue;
            }

            if (IsArmoredRig(item))
            {
                int rigPrice = GetArmoredRigBasePrice(item);
                if (HideUnsearchedLootValue(item))
                {
                    return rigPrice;
                }

                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = PriceContents(item);
                return rigPrice + platesPrice + contentsPrice;
            }

            if (IsRealContainer(item))
            {
                int basePrice = ContainerBasePrice(item);
                if (HideUnsearchedLootValue(item))
                {
                    return basePrice;
                }

                int contentsPrice = PriceContents(item);
                return basePrice + contentsPrice;
            }

            if (HasHardPlateSlots(item))
            {
                int armorPrice = PriceSingleItem(item);
                int platesPrice = GetArmorPlateTraderPrice(item);
                return armorPrice + platesPrice;
            }

            if (IsMagazine(item))
            {
                return GetMagazineTotalSellPrice(item);
            }

            if (item is Weapon)
            {
                return GetWeaponTotalSellValue(item);
            }

            if (NeedsModBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return PriceSingleItem(item);
        }
        internal static string FormatMoneyUi(int value)
        {
            return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", " ");
        }


        internal static string FormatPrice(int price)
        {
            if (price >= 1000000)
            {
                double millions = price / 1000000.0;
                double rounded = Math.Round(millions * 10) / 10.0;
                return rounded.ToString("0.#") + "m";
            }

            if (price >= 100000)
            {
                int rounded = (int)(Math.Round(price / 10000.0) * 10000);
                return (rounded / 1000).ToString() + "k";
            }

            if (price >= 10000)
            {
                int rounded = (int)(Math.Round(price / 5000.0) * 5000);
                return (rounded / 1000).ToString() + "k";
            }

            if (price >= 1000)
            {
                int rounded = (int)(Math.Round(price / 1000.0) * 1000);
                return (rounded / 1000).ToString() + "k";
            }

            return price.ToString();
        }

        internal static string FormatPrecise(int price)
        {
            return price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", " ") + " ₽";
        }


        public static void ClearPriceCache()
        {
            _cleanBasePriceCache.Clear();
        }

    }
}