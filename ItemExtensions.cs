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

namespace AvgSellPrice
{
    internal static class TraderClassExtensions
    {
        private static ISession _session;

        private static ISession Session
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

        private static readonly FieldInfo SupplyDataField =
            typeof(TraderClass).GetField("SupplyData_0", BindingFlags.Public | BindingFlags.Instance);

        public static SupplyData GetSupplyDataSafe(this TraderClass trader)
        {
            if (SupplyDataField == null || trader == null)
            {
                return null;
            }

            return SupplyDataField.GetValue(trader) as SupplyData;
        }

        public static void SetSupplyDataSafe(this TraderClass trader, SupplyData supplyData)
        {
            if (SupplyDataField == null || trader == null)
            {
                return;
            }

            SupplyDataField.SetValue(trader, supplyData);
        }

        public static async void UpdateSupplyDataSafe(this TraderClass trader)
        {
            if (trader == null)
            {
                return;
            }

            if (Session == null)
            {
                return;
            }

            Result<SupplyData> result = await Session.GetSupplyData(trader.Id);

            if (result.Succeed)
            {
                trader.SetSupplyDataSafe(result.Value);
            }
        }
    }

    internal static class ItemExtensions
    {
        private static ISession _session;
        private static readonly HashSet<string> EquipmentRootSlotNames =
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

        private static readonly Dictionary<string, int> CachedBasePricesByTemplateId =
            new Dictionary<string, int>();

        private static ISession Session
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

        public static void AddApproxSellPriceAttribute(this Item item)
        {
            if (item == null)
            {
                return;
            }

            if (item.Attributes == null)
            {
                item.Attributes = new List<ItemAttributeClass>();
            }

            if (item.Attributes.Any(a => a.Name == "ApproxSellPrice"))
            {
                return;
            }

            ItemAttributeClass attribute = new ItemAttributeClass(EItemAttributeId.MoneySum)
            {
                Name = "ApproxSellPrice",
                DisplayNameFunc = () => "Sell Price",

                Base = () =>
                {
                    if (IsInTraderSellScreen())
                    {
                        return 0.01f;
                    }

                    int price = GetDisplayMainPrice(item);
                    return price > 0 ? price : 0.01f;
                },

                StringValue = () =>
                {
                    if (IsInTraderSellScreen())
                    {
                        return string.Empty;
                    }

                    string text = item.GetHoverPriceText();

                    if (string.IsNullOrEmpty(text))
                    {
                        return "Cannot be sold";
                    }

                    string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    return lines[0];
                },

                FullStringValue = () =>
                {
                    if (IsInTraderSellScreen())
                    {
                        return string.Empty;
                    }

                    string text = item.GetHoverPriceText();

                    if (string.IsNullOrEmpty(text))
                    {
                        return "No trader buy price available";
                    }

                    return text;
                },

                DisplayType = () => EItemAttributeDisplayType.Compact
            };

            List<ItemAttributeClass> list = new List<ItemAttributeClass>();
            list.Add(attribute);
            list.AddRange(item.Attributes);
            item.Attributes = list;
        }
        private static string ToRichTextColor(Color color)
        {
            Color32 c = color;
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}";
        }

        private static string Colorize(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return $"<color={ToRichTextColor(color)}>{text}</color>";
        }

        public static string GetHoverPriceText(this Item item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (IsInTraderSellScreen())
            {
                return string.Empty;
            }

            if (UseFleaPriceSource && IsKnownNotSellableOnFlea(item))
            {
                return GetNotSellableOnFleaHoverText(item);
            }

            if (item is AmmoItemClass)
            {
                TraderOffer ammoOffer = GetConfiguredTraderOffer(item);
                int ammoPrice = IsTraderStockItem(item)
                    ? GetSingleItemPrice(item)
                    : GetSingleItemTotalSellPrice(item);

                if (ammoOffer == null || ammoPrice <= 0)
                {
                    return string.Empty;
                }

                return Colorize(
                    FormatMainPriceWithOptionalTrader(ammoOffer.Name, ammoPrice, applyMinimum: false),
                    PluginConfig.MainPriceColor.Value);
            }

            if (IsMagazine(item))
            {
                bool includeMagazineAmmo = PluginConfig.IncludeAmmoInValues == null ||
                                           PluginConfig.IncludeAmmoInValues.Value;
                TraderOffer totalOffer = GetConfiguredTraderOffer(item);
                int ammoPrice = includeMagazineAmmo ? GetLoadedAmmoTraderPrice(item) : 0;
                int totalPrice = totalOffer != null ? totalOffer.Price : 0;
                int basePrice = UseFleaPriceSource
                    ? GetSingleItemPrice(item)
                    : includeMagazineAmmo && totalPrice > 0
                        ? Math.Max(0, totalPrice - ammoPrice)
                        : GetSingleItemPrice(item);

                if (UseFleaPriceSource)
                {
                    totalPrice = basePrice + ammoPrice;
                }

                if (basePrice <= 0 && totalPrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value && totalOffer != null
                    ? totalOffer.Name
                    : string.Empty;

                List<string> magazineLines = new List<string>();
                magazineLines.Add(Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, basePrice > 0 ? basePrice : totalPrice),
                    PluginConfig.MainPriceColor.Value));

                if (ammoPrice > 0)
                {
                    magazineLines.Add(Colorize(
                        "Ammo " + FormatPriceExternal(ammoPrice),
                        PluginConfig.AmmoPriceColor.Value));

                    magazineLines.Add(Colorize(
                        "Total " + FormatPriceExternal((basePrice > 0 ? basePrice : 0) + ammoPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, magazineLines);
            }

            if (item is Weapon)
            {
                TraderOffer weaponOffer = GetConfiguredTraderOffer(item);
                int totalPrice = weaponOffer != null ? weaponOffer.Price : 0;
                int attachmentsPrice = GetWeaponAttachmentTraderPrice(item);
                int magazinePrice = GetWeaponMagazineTraderPrice(item);
                bool fallbackToTraderBase = UseFleaPriceSource && IsKnownNotSellableOnFlea(item);
                int basePrice = fallbackToTraderBase
                    ? totalPrice > 0
                        ? Math.Max(0, totalPrice - attachmentsPrice - magazinePrice)
                        : GetSingleItemPrice(item)
                    : UseFleaPriceSource
                    ? GetSingleItemPrice(item)
                    : totalPrice > 0
                        ? Math.Max(0, totalPrice - attachmentsPrice - magazinePrice)
                        : 0;

                if (UseFleaPriceSource)
                {
                    totalPrice = basePrice + attachmentsPrice + magazinePrice;
                }

                if (basePrice <= 0 && totalPrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value && weaponOffer != null
                    ? weaponOffer.Name
                    : string.Empty;
                bool showWeaponAttachments = PluginConfig.ShowWeaponAttachmentsPrice.Value;
                int displayPrice = showWeaponAttachments
                    ? (basePrice > 0 ? basePrice : totalPrice)
                    : totalPrice;

                List<string> weaponLines = new List<string>();
                weaponLines.Add(Colorize(
                    fallbackToTraderBase
                        ? FormatBasePriceWithOptionalTrader(traderName, displayPrice)
                        : FormatMainPriceWithOptionalTrader(traderName, displayPrice),
                    PluginConfig.MainPriceColor.Value));

                if (showWeaponAttachments && (attachmentsPrice > 0 || magazinePrice > 0))
                {
                    if (attachmentsPrice > 0)
                    {
                        weaponLines.Add(Colorize(
                            "Attachments " + FormatPriceExternal(attachmentsPrice),
                            PluginConfig.ContentsPriceColor.Value));
                    }

                    if (magazinePrice > 0)
                    {
                        weaponLines.Add(Colorize(
                            "Mag " + FormatPriceExternal(magazinePrice),
                            PluginConfig.ContentsPriceColor.Value));
                    }

                    weaponLines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, weaponLines);
            }

            if (ShouldUseModAttachmentBreakdown(item))
            {
                TraderOffer itemOffer = GetConfiguredTraderOffer(item);
                int totalPrice = itemOffer != null ? itemOffer.Price : 0;
                int attachmentsPrice = GetModAttachmentPrice(item);
                int basePrice = UseFleaPriceSource
                    ? GetSingleItemPrice(item)
                    : totalPrice > 0
                        ? Math.Max(0, totalPrice - attachmentsPrice)
                        : GetSingleItemPrice(item);

                if (UseFleaPriceSource || totalPrice <= 0)
                {
                    totalPrice = basePrice + attachmentsPrice;
                }

                if (basePrice <= 0 && totalPrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value && itemOffer != null
                    ? itemOffer.Name
                    : string.Empty;

                List<string> lines = new List<string>();
                lines.Add(Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, basePrice > 0 ? basePrice : totalPrice),
                    PluginConfig.MainPriceColor.Value));

                if (attachmentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Attachments " + FormatPriceExternal(attachmentsPrice),
                        PluginConfig.ContentsPriceColor.Value));

                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (IsArmoredRig(item))
            {
                TraderOffer rigOffer = GetArmoredRigTraderOffer(item);
                int rigPrice = GetArmoredRigBasePrice(item);

                if (rigPrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value
                    ? rigOffer != null ? rigOffer.Name : "Ragman"
                    : string.Empty;
                string baseLine = Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, rigPrice),
                    PluginConfig.MainPriceColor.Value);

                if (ShouldHideContentsForUnsearchedItem(item))
                {
                    return baseLine + Environment.NewLine + GetUnsearchedHoverLine();
                }

                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = GetContentsTraderPrice(item);
                int totalPrice = rigPrice + platesPrice + contentsPrice;

                List<string> rigLines = new List<string>();
                rigLines.Add(baseLine);

                if (platesPrice > 0)
                {
                    rigLines.Add(Colorize(
                        "Plates " + FormatPriceExternal(platesPrice),
                        PluginConfig.PlatesPriceColor.Value));
                }

                if (contentsPrice > 0)
                {
                    rigLines.Add(Colorize(
                        "Contents " + FormatContentsPriceVisual(contentsPrice),
                        PluginConfig.ContentsPriceColor.Value));
                }

                if (platesPrice > 0 || contentsPrice > 0)
                {
                    rigLines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, rigLines);
            }

            if (HasHardPlateSlots(item))
            {
                TraderOffer totalOffer = GetConfiguredTraderOffer(item);
                int totalPrice = totalOffer != null ? totalOffer.Price : 0;
                int platesPrice = GetArmorPlateTraderPrice(item);

                if (totalPrice > 0)
                {
                    int basePrice = Math.Max(0, totalPrice - platesPrice);
                    string traderName = PluginConfig.ShowTraderNameInTooltip.Value && totalOffer != null
                        ? totalOffer.Name
                        : string.Empty;

                    List<string> lines = new List<string>();
                    lines.Add(Colorize(
                        FormatMainPriceWithOptionalTrader(traderName, basePrice > 0 ? basePrice : totalPrice),
                        PluginConfig.MainPriceColor.Value));

                    if (platesPrice > 0)
                    {
                        lines.Add(Colorize(
                            "Plates " + FormatPriceExternal(platesPrice),
                            PluginConfig.PlatesPriceColor.Value));

                        lines.Add(Colorize(
                            "Total " + FormatPriceExternal(totalPrice),
                            PluginConfig.TotalPriceColor.Value));
                    }

                    return string.Join(Environment.NewLine, lines);
                }
            }


            if (IsRealContainer(item))
            {
                if (PluginConfig.IncludeCasesInValues != null &&
                    !PluginConfig.IncludeCasesInValues.Value)
                {
                    return string.Empty;
                }

                TraderOffer containerOffer = GetContainerBaseTraderOffer(item);
                int basePrice = GetContainerBasePriceRobust(item);

                if (containerOffer != null && containerOffer.Price > 0)
                {
                    basePrice = containerOffer.Price;
                }

                if (basePrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value
                    ? GetContainerBaseTraderName(item, containerOffer)
                    : string.Empty;
                string baseLine = Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, basePrice),
                    PluginConfig.MainPriceColor.Value);

                if (ShouldHideContentsForUnsearchedItem(item))
                {
                    return baseLine + Environment.NewLine + GetUnsearchedHoverLine();
                }

                int contentsPrice = GetContentsTraderPrice(item);
                int totalPrice = basePrice + contentsPrice;

                List<string> lines = new List<string>();
                lines.Add(baseLine);

                if (contentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Contents " + FormatContentsPriceVisual(contentsPrice),
                        PluginConfig.ContentsPriceColor.Value));

                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }



            TraderOffer fallbackOffer = GetConfiguredTraderOffer(item);

            if (fallbackOffer == null || fallbackOffer.Price <= 0)
            {
                return string.Empty;
            }

            return Colorize(
                FormatMainPriceWithOptionalTrader(fallbackOffer.Name, fallbackOffer.Price),
                PluginConfig.MainPriceColor.Value);
        }

        private static bool IsInTraderSellScreen()
        {

            try
            {
                object app = ClientAppUtils.GetMainApp();

                if (app == null)
                {
                    return false;
                }

                string appTypeName = app.GetType().FullName ?? string.Empty;

                if (appTypeName.IndexOf("Trading", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (appTypeName.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsTraderStockItem(Item item)
        {
            object owner = item?.Owner;
            if (owner == null)
            {
                return false;
            }

            string ownerType = item.Owner.OwnerType.ToString() ?? string.Empty;
            string ownerClass = owner.GetType().FullName ?? owner.GetType().Name ?? string.Empty;

            return ownerType.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ownerClass.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatMainPriceWithOptionalTrader(string traderName, int rawPrice, bool applyMinimum = true)
        {
            int price = applyMinimum ? ApplyMinimumPrice(rawPrice) : rawPrice;

            string formattedPrice = PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);

            bool showAround = PluginConfig.ShowAroundPrefix.Value && !PluginConfig.PrecisePrice.Value;

            if (!PluginConfig.ShowTraderNameInTooltip.Value || string.IsNullOrEmpty(traderName))
            {
                if (showAround)
                {
                    return "Around " + formattedPrice;
                }

                if (!PluginConfig.ShowTraderNameInTooltip.Value)
                {
                    return (UseFleaPriceSource ? "Flea " : "Base ") + formattedPrice;
                }

                return formattedPrice;
            }

            if (showAround)
            {
                return traderName + " around " + formattedPrice;
            }

            return traderName + " " + formattedPrice;
        }

        private static string FormatBasePriceWithOptionalTrader(string traderName, int rawPrice, bool applyMinimum = true)
        {
            int price = applyMinimum ? ApplyMinimumPrice(rawPrice) : rawPrice;

            string formattedPrice = PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);

            bool showAround = PluginConfig.ShowAroundPrefix.Value && !PluginConfig.PrecisePrice.Value;

            if (!PluginConfig.ShowTraderNameInTooltip.Value || string.IsNullOrEmpty(traderName))
            {
                return (showAround ? "Around Base " : "Base ") + formattedPrice;
            }

            if (showAround)
            {
                return "Around " + traderName + " " + formattedPrice;
            }

            return traderName + " " + formattedPrice;
        }

        private static int ApplyMinimumPrice(int rawPrice)
        {
            int minimum = PluginConfig.MinimumDisplayPrice.Value;

            if (minimum > 0 && rawPrice > 0 && rawPrice < minimum)
            {
                return minimum;
            }

            return rawPrice;
        }
        private static string FormatContentsPriceVisual(int price)
        {
            if (!PluginConfig.PrecisePrice.Value && price > 0 && price < 1000)
            {
                price = 1000;
            }

            return PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);
        }

        public static int GetAveragePriceForExternal(Item item)
        {
            return GetDisplayMainPrice(item);
        }

        public static string FormatPriceExternal(int price)
        {
            return PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);
        }

        private class TraderOffer
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

        private static bool UseFleaPriceSource =>
            PluginConfig.ItemPriceSource != null &&
            PluginConfig.ItemPriceSource.Value == PriceSource.FleaMarket;

        private static int GetFleaTemplatePrice(Item item)
        {
            string templateId = GetTemplateIdSafe(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            if (IsKnownNotSellableOnFlea(item))
            {
                return 0;
            }

            return FleaPriceCache.GetPrice(templateId);
        }

        private static bool IsKnownNotSellableOnFlea(Item item)
        {
            string templateId = GetTemplateIdSafe(item);

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

        private static bool TryGetTemplateCanSellOnFlea(Item item, out bool canSell)
        {
            canSell = true;

            if (item == null || item.Template == null)
            {
                return false;
            }

            if (TryReadBoolMember(item.Template, "CanSellOnRagfair", out canSell))
            {
                return true;
            }

            object props = GetMemberValue(item.Template, "Props", "_props");
            return props != null && TryReadBoolMember(props, "CanSellOnRagfair", out canSell);
        }

        private static bool TryReadBoolMember(object source, string memberName, out bool value)
        {
            value = false;

            if (source == null)
            {
                return false;
            }

            Type type = source.GetType();
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    object raw = property.GetValue(source, null);
                    if (raw is bool boolValue)
                    {
                        value = boolValue;
                        return true;
                    }
                }

                FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    object raw = field.GetValue(source);
                    if (raw is bool boolValue)
                    {
                        value = boolValue;
                        return true;
                    }
                }

                type = type.BaseType;
            }

            return false;
        }

        private static object GetMemberValue(object source, params string[] memberNames)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            while (type != null)
            {
                foreach (string memberName in memberNames)
                {
                    PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (property != null && property.GetIndexParameters().Length == 0)
                    {
                        object value = property.GetValue(source, null);
                        if (value != null)
                        {
                            return value;
                        }
                    }

                    FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        object value = field.GetValue(source);
                        if (value != null)
                        {
                            return value;
                        }
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        private static bool ShouldHideContentsForUnsearchedItem(Item item)
        {
            return (ValueTracker.IsInRaid || RaidPlayerState.MainPlayer != null) &&
                   item != null &&
                   (IsRealContainer(item) || IsArmoredRig(item)) &&
                   IsUnsearchedLootContainer(item);
        }

        private static bool IsUnsearchedLootContainer(Item item)
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
                catch
                {
                }

                return !searchableItem.IsSearched;
            }

            if (TryReadSearchState(item, out bool searched))
            {
                return !searched;
            }

            object searchInfo = GetMemberValue(
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

            object updateData = GetMemberValue(item, "upd", "Upd", "UpdateData", "Updatable");
            object updateSearchInfo = GetMemberValue(
                updateData,
                "SearchInfo",
                "SearchData",
                "SearchState",
                "Searchable");

            return TryReadSearchState(updateSearchInfo, out searched) && !searched;
        }

        private static bool TryReadSearchState(object source, out bool searched)
        {
            searched = true;

            if (source == null)
            {
                return false;
            }

            if (TryReadBoolMember(source, "Searched", out searched) ||
                TryReadBoolMember(source, "IsSearched", out searched) ||
                TryReadBoolMember(source, "WasSearched", out searched) ||
                TryReadBoolMember(source, "SearchComplete", out searched) ||
                TryReadBoolMember(source, "IsSearchComplete", out searched) ||
                TryReadBoolMember(source, "FullySearched", out searched) ||
                TryReadBoolMember(source, "IsFullySearched", out searched))
            {
                return true;
            }

            if (TryReadBoolMember(source, "Known", out searched) ||
                TryReadBoolMember(source, "IsKnown", out searched))
            {
                return true;
            }

            return false;
        }

        private static string GetUnsearchedHoverLine()
        {
            return Colorize("Unsearched", PluginConfig.MainPriceColor.Value);
        }

        private static string GetNotSellableOnFleaHoverText(Item item)
        {
            List<string> lines = new List<string>();
            lines.Add(Colorize("Not sellable on flea", PluginConfig.NotSellableOnFleaColor.Value));

            TraderOffer traderOffer = GetConfiguredTraderSellOffer(item);
            int sellPrice = traderOffer != null ? traderOffer.Price : 0;
            string traderName = PluginConfig.ShowTraderNameInTooltip.Value && traderOffer != null
                ? traderOffer.Name
                : string.Empty;

            if (item is AmmoItemClass)
            {
                int ammoPrice = IsTraderStockItem(item)
                    ? GetSingleItemPrice(item)
                    : GetSingleItemTotalSellPrice(item);

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
                int attachmentsPrice = GetWeaponAttachmentTraderPrice(item);
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

                if (basePrice > 0 || totalPrice > 0)
                {
                    lines.Add(Colorize(
                        FormatBasePriceWithOptionalTrader(weaponTraderName, basePrice > 0 ? basePrice : totalPrice),
                        PluginConfig.MainPriceColor.Value));
                }

                if (PluginConfig.ShowWeaponAttachmentsPrice.Value && (attachmentsPrice > 0 || magazinePrice > 0))
                {
                    if (attachmentsPrice > 0)
                    {
                        lines.Add(Colorize(
                            "Attachments " + FormatPriceExternal(attachmentsPrice),
                            PluginConfig.ContentsPriceColor.Value));
                    }

                    if (magazinePrice > 0)
                    {
                        lines.Add(Colorize(
                            "Mag " + FormatPriceExternal(magazinePrice),
                            PluginConfig.ContentsPriceColor.Value));
                    }

                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (ShouldUseModAttachmentBreakdown(item))
            {
                int attachmentsPrice = GetModAttachmentPrice(item);
                int basePrice = sellPrice > 0
                    ? Math.Max(0, sellPrice - attachmentsPrice)
                    : GetSingleItemPrice(item);
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
                        PluginConfig.ContentsPriceColor.Value));

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

                if (basePrice > 0)
                {
                    lines.Add(Colorize(
                        FormatMainPriceWithOptionalTrader(rigTraderName, basePrice),
                        PluginConfig.MainPriceColor.Value));
                }

                if (ShouldHideContentsForUnsearchedItem(item))
                {
                    lines.Add(GetUnsearchedHoverLine());
                    return string.Join(Environment.NewLine, lines);
                }

                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = GetContentsTraderPrice(item);

                if (platesPrice > 0)
                {
                    lines.Add(Colorize(
                        "Plates " + FormatPriceExternal(platesPrice),
                        PluginConfig.PlatesPriceColor.Value));
                }

                if (contentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Contents " + FormatContentsPriceVisual(contentsPrice),
                        PluginConfig.ContentsPriceColor.Value));
                }

                if (platesPrice > 0 || contentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(basePrice + platesPrice + contentsPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (IsRealContainer(item))
            {
                TraderOffer containerOffer = GetContainerBaseTraderOffer(item);
                int basePrice = GetContainerBasePriceRobust(item);
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

                if (ShouldHideContentsForUnsearchedItem(item))
                {
                    lines.Add(GetUnsearchedHoverLine());
                    return string.Join(Environment.NewLine, lines);
                }

                int contentsPrice = GetContentsTraderPrice(item);
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

        private static TraderOffer GetFleaPriceOffer(Item item)
        {
            int fleaPrice = GetFleaTemplatePrice(item);

            if (fleaPrice <= 0 || IsKnownNotSellableOnFlea(item))
            {
                return null;
            }

            return new TraderOffer("Flea Market", fleaPrice, "RUB", 1d);
        }

        private static bool HasChildren(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return item.GetAllItems().Any(x => x != item);
        }

        private static bool IsRealContainer(Item item)
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
        private static string Colorize(string text, string color)
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
        private static string GetSafeColor(string configuredColor, string fallbackColor)
        {
            if (string.IsNullOrWhiteSpace(configuredColor))
            {
                return fallbackColor;
            }

            return configuredColor.Trim();
        }


        private static bool IsHardPlateSlotName(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                return false;
            }

            string lower = slotName.ToLowerInvariant();

            return lower == "front_plate" ||
                   lower == "back_plate" ||
                   lower == "left_side_plate" ||
                   lower == "right_side_plate" ||
                   lower == "side_plate" ||
                   lower.Contains("front_plate") ||
                   lower.Contains("back_plate") ||
                   lower.Contains("side_plate");
        }
        private static bool IsArmorSlotName(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                return false;
            }

            string lower = slotName.ToLowerInvariant();

            return lower.Contains("soft") ||
                   lower.Contains("armor") ||
                   lower.Contains("plate") ||
                   lower.Contains("insert") ||
                   lower.Contains("front") ||
                   lower.Contains("back") ||
                   lower.Contains("side") ||
                   lower.Contains("spall") ||
                   lower.Contains("groin") ||
                   lower.Contains("throat") ||
                   lower.Contains("neck") ||
                   lower.Contains("collar") ||
                   lower.Contains("shoulder") ||
                   lower.Contains("arm") ||
                   IsHelmetArmorSlotName(lower);
        }

        private static bool IsHelmetArmorSlotName(string lowerSlotName)
        {
            if (string.IsNullOrEmpty(lowerSlotName))
            {
                return false;
            }

            return lowerSlotName.StartsWith("helmet_", StringComparison.Ordinal) ||
                   lowerSlotName.Contains("helmet_top") ||
                   lowerSlotName.Contains("helmet_nape") ||
                   lowerSlotName.Contains("helmet_back") ||
                   lowerSlotName.Contains("helmet_ears") ||
                   lowerSlotName.Contains("helmet_ear") ||
                   lowerSlotName.Contains("helmet_jaws") ||
                   lowerSlotName.Contains("helmet_jaw");
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
                if (ShouldHideContentsForUnsearchedItem(item))
                {
                    return rigPrice;
                }

                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = GetContentsTraderPrice(item);
                return rigPrice + platesPrice + contentsPrice;
            }

            if (IsRealContainer(item))
            {
                int basePrice = GetContainerBasePriceRobust(item);
                if (ShouldHideContentsForUnsearchedItem(item))
                {
                    return basePrice;
                }

                int contentsPrice = GetContentsTraderPrice(item);
                return basePrice + contentsPrice;
            }

            if (item is Weapon)
            {
                return GetWeaponTotalSellValue(item);
            }

            if (ShouldUseModAttachmentBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return GetSingleItemPrice(item);
        }
        public static int GetPlayerEquipmentValue()
        {
            if (Session == null || Session.Profile == null || Session.Profile.Inventory == null)
            {
                return 0;
            }

            int total = 0;

            foreach (Item item in GetEquippedRootItems())
            {
                if (item == null)
                {
                    continue;
                }

                if (ShouldSkipEquipmentValueItem(item))
                {
                    continue;
                }

                total += GetEquipmentValueItemPrice(item);
            }

            return total;
        }

        internal static string FormatMoneyUi(int value)
        {
            return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", " ");
        }

        internal static List<Item> GetRootItems(IEnumerable<Item> items)
        {
            List<Item> sourceItems = items?
                .Where(item => item != null)
                .ToList() ?? new List<Item>();

            if (sourceItems.Count <= 1)
            {
                return sourceItems;
            }

            return sourceItems
                .Where(candidate => !sourceItems.Any(other => !ReferenceEquals(other, candidate) && ItemTreeContains(other, candidate)))
                .ToList();
        }

        private static List<Item> GetEquippedRootItems()
        {
            object inventory = Session?.Profile?.Inventory;
            if (inventory == null)
            {
                return new List<Item>();
            }

            List<Item> equippedItems = new List<Item>();

            object equipmentRoot = GetInventoryEquipmentRoot(inventory);
            AddContainedItemsFromSlots(equipmentRoot, equippedItems);

            if (equippedItems.Count > 0)
            {
                return GetRootItems(equippedItems);
            }

            return GetRootItems(GetAllPlayerItemsSafe())
                .Where(item =>
                {
                    string slotName = GetCurrentSlotName(item);
                    return !string.IsNullOrEmpty(slotName) && EquipmentRootSlotNames.Contains(slotName);
                })
                .ToList();
        }

        private static string GetCurrentSlotName(Item item)
        {
            if (item == null)
            {
                return null;
            }

            return TryGetItemSlotName(item, out string slotName)
                ? slotName
                : null;
        }

        internal static int GetConfiguredValueItemPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (!ShouldIncludeItemInConfiguredValues(item))
            {
                return 0;
            }

            return GetTotalSellValue(item);
        }

        private static int GetEquipmentValueItemPrice(Item item)
        {
            if (item == null ||
                ShouldSkipEquipmentValueItem(item) ||
                !ShouldIncludeItemInConfiguredValues(item))
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
                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = GetEquipmentContentsTraderPrice(item);
                return rigPrice + platesPrice + contentsPrice;
            }

            if (IsRealContainer(item))
            {
                int basePrice = GetContainerBasePriceRobust(item);
                int contentsPrice = GetEquipmentContentsTraderPrice(item);
                return basePrice + contentsPrice;
            }

            if (item is Weapon)
            {
                return GetWeaponTotalSellValue(item);
            }

            if (ShouldUseModAttachmentBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return GetSingleItemPrice(item);
        }

        private static int GetEquipmentContentsTraderPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int total = 0;
            List<Item> contentRoots = GetRootItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsEquipmentValueContents(item, child))
                    .ToList());

            foreach (Item child in contentRoots)
            {
                total += GetEquipmentContentsItemSellPrice(child);
            }

            return total;
        }

        private static int GetEquipmentContentsItemSellPrice(Item item)
        {
            if (item == null || IsKeyItem(item) || IsEquipmentValueCase(item))
            {
                return 0;
            }

            if (item is Weapon)
            {
                return GetWeaponTotalSellValue(item);
            }

            if (IsRealContainer(item) || IsArmoredRig(item))
            {
                return GetEquipmentValueItemPrice(item);
            }

            if (ShouldUseModAttachmentBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return GetSingleItemTotalSellPrice(item);
        }

        private static bool ShouldCountAsEquipmentValueContents(Item parent, Item child)
        {
            return ShouldCountAsContents(parent, child) &&
                   !IsKeyItem(child) &&
                   !IsEquipmentValueCase(child);
        }

        internal static int GetConfiguredRaidLootRootValue(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (!ShouldIncludeItemInConfiguredValues(item))
            {
                return 0;
            }

            return GetTotalSellValue(item);
        }

        internal static bool MayContainConfiguredValueContents(Item item)
        {
            return item != null && (IsRealContainer(item) || IsArmoredRig(item));
        }

        internal static bool RequiresRaidLootRebuildOnChange(Item item)
        {
            return item != null && (MayContainConfiguredValueContents(item) || item is MoneyItemClass);
        }

        internal static List<Item> GetItemTreeSafe(Item item)
        {
            if (item == null)
            {
                return new List<Item>();
            }

            try
            {
                return item.GetAllItems()
                    .Where(child => child != null)
                    .ToList();
            }
            catch
            {
                return new List<Item> { item };
            }
        }

        private static bool ShouldSkipEquipmentValueItem(Item item)
        {
            string slotName = GetCurrentSlotName(item);
            return string.Equals(slotName, "Scabbard", StringComparison.OrdinalIgnoreCase) ||
                   IsKeyItem(item) ||
                   IsEquipmentValueCase(item);
        }

        private static bool IsKeyItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            string typeName = item.GetType().Name;
            if (!string.IsNullOrEmpty(typeName) &&
                typeName.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string templateName = item.Template != null ? item.Template.Name : null;
            return !string.IsNullOrEmpty(templateName) &&
                   templateName.IndexOf("Key", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEquipmentValueCase(Item item)
        {
            if (item == null || !IsRealContainer(item))
            {
                return false;
            }

            if (item is BackpackItemClass || item is VestItemClass)
            {
                return false;
            }

            string slotName = GetCurrentSlotName(item);
            if (string.Equals(slotName, "SecuredContainer", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string typeName = item.GetType().Name;
            string templateName = item.Template != null ? item.Template.Name : null;
            string shortName = item.ShortName;

            return ContainsCaseName(typeName) ||
                   ContainsCaseName(templateName) ||
                   ContainsCaseName(shortName);
        }

        private static bool ContainsCaseName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf("Case", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Container", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Keytool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Key_tool", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldIncludeItemInConfiguredValues(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (item is AmmoItemClass && IsChamberSlotItem(item))
            {
                return false;
            }

            return true;
        }

        private static bool IsChamberSlotItem(Item item)
        {
            if (!TryGetItemSlotName(item, out string slotName) || string.IsNullOrEmpty(slotName))
            {
                return false;
            }

            string lower = slotName.ToLowerInvariant();
            return lower.Contains("chamber") ||
                   lower.Contains("patron_in_weapon") ||
                   lower.Contains("cartridge");
        }

        private static object GetInventoryEquipmentRoot(object inventory)
        {
            if (inventory == null)
            {
                return null;
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = inventory.GetType();

            PropertyInfo namedProperty = type.GetProperty("Equipment", flags);
            if (namedProperty != null)
            {
                object value = namedProperty.GetValue(inventory, null);
                if (value != null)
                {
                    return value;
                }
            }

            FieldInfo namedField = type.GetField("Equipment", flags);
            if (namedField != null)
            {
                object value = namedField.GetValue(inventory);
                if (value != null)
                {
                    return value;
                }
            }

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (property.Name.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) < 0 &&
                    !((property.PropertyType.Name?.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0))
                {
                    continue;
                }

                object value = property.GetValue(inventory, null);
                if (value != null)
                {
                    return value;
                }
            }

            foreach (FieldInfo field in type.GetFields(flags))
            {
                if (field.Name.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) < 0 &&
                    !((field.FieldType.Name?.IndexOf("Equipment", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0))
                {
                    continue;
                }

                object value = field.GetValue(inventory);
                if (value != null)
                {
                    return value;
                }
            }

            return null;
        }

        private static void AddContainedItemsFromSlots(object root, List<Item> equippedItems)
        {
            if (root == null || equippedItems == null)
            {
                return;
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = root.GetType();

            PropertyInfo slotsProperty = type.GetProperty("Slots", flags);
            if (slotsProperty != null)
            {
                AddItemsFromSlotEnumerable(slotsProperty.GetValue(root, null) as IEnumerable, equippedItems);
            }

            FieldInfo slotsField = type.GetField("Slots", flags);
            if (slotsField != null)
            {
                AddItemsFromSlotEnumerable(slotsField.GetValue(root) as IEnumerable, equippedItems);
            }
        }

        private static void AddItemsFromSlotEnumerable(IEnumerable slots, List<Item> equippedItems)
        {
            if (slots == null || equippedItems == null)
            {
                return;
            }

            foreach (object entry in slots)
            {
                Slot slot = entry as Slot;
                if (slot == null || slot.ContainedItem == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(slot.Name) && EquipmentRootSlotNames.Contains(slot.Name))
                {
                    equippedItems.Add(slot.ContainedItem);
                }
            }
        }

        private static bool TryGetItemSlotName(Item item, out string slotName)
        {
            slotName = null;

            if (!TryGetItemSlot(item, out Slot slot) || slot == null)
            {
                return false;
            }

            slotName = slot.Name;
            return !string.IsNullOrEmpty(slotName);
        }

        private static bool TryGetItemSlot(Item item, out Slot slot)
        {
            slot = null;

            if (item == null || item.CurrentAddress == null)
            {
                return false;
            }

            object address = item.CurrentAddress;
            Type type = address.GetType();

            while (type != null)
            {
                PropertyInfo slotProperty = type.GetProperty(
                    "Slot",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (slotProperty != null && typeof(Slot).IsAssignableFrom(slotProperty.PropertyType))
                {
                    slot = slotProperty.GetValue(address, null) as Slot;
                    return slot != null;
                }

                FieldInfo slotField = type.GetField(
                    "Slot",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (slotField != null && typeof(Slot).IsAssignableFrom(slotField.FieldType))
                {
                    slot = slotField.GetValue(address) as Slot;
                    return slot != null;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static bool TryGetSlotBoolField(object slot, string fieldName, out bool value)
        {
            value = false;

            if (slot == null)
            {
                return false;
            }

            Type t = slot.GetType();

            while (t != null)
            {
                FieldInfo field = t.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(bool))
                {
                    value = (bool)field.GetValue(slot);
                    return true;
                }

                t = t.BaseType;
            }

            return false;
        }
        private static bool IsItemInHardPlateSlot(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return TryGetItemSlotName(item, out string slotName) &&
                   IsHardPlateSlotName(slotName);
        }
        private static bool IsItemInAnyArmorSlot(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return TryGetItemSlotName(item, out string slotName) &&
                   IsArmorSlotName(slotName);
        }
        private static bool IsItemInBuiltInSlot(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return TryGetItemSlot(item, out Slot slot) &&
                   IsBuiltInSlot(slot);
        }

        private static bool TryGetLockedFromSlotProps(Slot slot, out bool locked)
        {
            locked = false;

            if (slot == null)
            {
                return false;
            }

            try
            {
                object props = null;
                Type t = slot.GetType();

                while (t != null && props == null)
                {
                    FieldInfo propsField = t.GetField("_props", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (propsField != null)
                    {
                        props = propsField.GetValue(slot);
                    }

                    t = t.BaseType;
                }

                if (props == null)
                {
                    return false;
                }

                Type propsType = props.GetType();

                FieldInfo lockedField = propsType.GetField("locked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (lockedField != null && lockedField.FieldType == typeof(bool))
                {
                    locked = (bool)lockedField.GetValue(props);
                    return true;
                }

                PropertyInfo lockedProp = propsType.GetProperty("locked", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (lockedProp != null && lockedProp.PropertyType == typeof(bool))
                {
                    locked = (bool)lockedProp.GetValue(props, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsBuiltInSlot(Slot slot)
        {
            if (slot == null)
            {
                return false;
            }

            bool required;
            bool locked;

            bool hasRequired = TryGetSlotBoolField(slot, "_required", out required);
            bool hasLocked = TryGetLockedFromSlotProps(slot, out locked);

            return (hasRequired && required) || (hasLocked && locked);
        }

        private static bool IsBuiltInArmorSlot(Slot slot)
        {
            if (slot == null)
            {
                return false;
            }

            if (!IsArmorSlotName(slot.Name))
            {
                return false;
            }

            return IsBuiltInSlot(slot);
        }

        private static bool IsRemovableArmorSlot(Slot slot)
        {
            if (slot == null)
            {
                return false;
            }

            if (!IsArmorSlotName(slot.Name))
            {
                return false;
            }

            return !IsBuiltInArmorSlot(slot);
        }

        private static bool IsItemInArmorSlot(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return TryGetItemSlotName(item, out string slotName) &&
                   IsArmorSlotName(slotName);
        }

        private static bool ShouldCountAsContents(Item parent, Item child)
        {
            if (parent == null || child == null || parent == child)
            {
                return false;
            }

            bool parentCanContainLoot = IsRealContainer(parent) || IsArmoredRig(parent);

            if (!parentCanContainLoot)
            {
                return false;
            }

            if (IsItemInAnyArmorSlot(child))
            {
                return false;
            }

            return true;
        }


        private static int GetArmorPlateTraderPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int total = 0;

            CompoundItem compound = item as CompoundItem;
            if (compound != null && compound.Slots != null)
            {
                foreach (Slot slot in compound.Slots)
                {
                    if (slot == null || slot.ContainedItem == null)
                    {
                        continue;
                    }

                    if (!IsHardPlateSlotName(slot.Name))
                    {
                        continue;
                    }

                    if (IsBuiltInSlot(slot))
                    {
                        continue;
                    }

                    int platePrice = GetAverageTraderPriceInternal(slot.ContainedItem);

                    if (platePrice <= 0)
                    {
                        platePrice = GetTemplateFallbackPrice(slot.ContainedItem);
                    }

                    total += platePrice;
                }
            }

            if (total > 0)
            {
                return total;
            }

            foreach (Item child in item.GetAllItems())
            {
                if (child == null || child == item)
                {
                    continue;
                }

                if (!IsItemInHardPlateSlot(child) || IsItemInBuiltInSlot(child))
                {
                    continue;
                }

                int platePrice = GetAverageTraderPriceInternal(child);

                if (platePrice <= 0)
                {
                    platePrice = GetTemplateFallbackPrice(child);
                }

                total += platePrice;
            }

            return total;
        }

        private static bool HasHardPlateSlots(Item item)
        {
            CompoundItem compound = item as CompoundItem;
            if (compound == null || compound.Slots == null)
            {
                return false;
            }

            foreach (Slot slot in compound.Slots)
            {
                if (slot == null || string.IsNullOrEmpty(slot.Name))
                {
                    continue;
                }

                if (IsHardPlateSlotName(slot.Name))
                {
                    return true;
                }
            }

            return false;
        }


        private static string GetTemplateIdSafe(Item item)
        {
            if (item == null || item.Template == null)
            {
                return null;
            }

            return item.Template._id;
        }

        private static void CacheBasePrice(Item item, int price)
        {
            if (item == null || price <= 0)
            {
                return;
            }

            string templateId = GetTemplateIdSafe(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return;
            }

            CachedBasePricesByTemplateId[templateId] = price;
        }

        private static int GetCachedBasePrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            string templateId = GetTemplateIdSafe(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            int value;
            if (CachedBasePricesByTemplateId.TryGetValue(templateId, out value))
            {
                return value;
            }

            return 0;
        }

        private static int GetVerifiedContainerBasePrice(Item item)
        {
            if (item == null || IsArmoredRig(item))
            {
                return 0;
            }

            string templateId = GetTemplateIdSafe(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            int price = VerifiedContainerPriceCache.GetPrice(templateId);

            if (price > 0)
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] VERIFIED CACHE HIT {item.ShortName} ({templateId}) => {price}");
            }

            return price;
        }

        private static void StoreVerifiedContainerBasePrice(Item item, int price, string source)
        {
            if (item == null || price <= 0)
            {
                return;
            }

            if (IsArmoredRig(item))
            {
                return;
            }

            string templateId = GetTemplateIdSafe(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return;
            }

            Plugin.Log?.LogInfo($"[AvgSellPrice] VERIFIED PRICE from {source} {item.ShortName} ({templateId}) => {price}");
            VerifiedContainerPriceCache.StoreVerifiedPrice(templateId, price);
        }

        private static int GetTemplateFallbackPrice(Item item)
        {
            if (item == null || item.Template == null)
            {
                return 0;
            }

            if (item.Template.CreditsPrice > 0)
            {
                return (int)Math.Floor(item.Template.CreditsPrice * 0.6);
            }

            return 0;
        }

        private static int GetSingleItemPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (IsArmoredRig(item))
            {
                return GetArmoredRigBasePrice(item);
            }

            if (UseFleaPriceSource)
            {
                int fleaPrice = GetFleaTemplatePrice(item);

                if (fleaPrice > 0)
                {
                    if (!IsRealContainer(item))
                    {
                        CacheBasePrice(item, fleaPrice);
                    }

                    return fleaPrice;
                }
            }

            TraderOffer offer = GetConfiguredTraderOffer(item);

            if (offer != null && offer.Price > 0)
            {
                if (!IsRealContainer(item))
                {
                    CacheBasePrice(item, offer.Price);
                }

                return offer.Price;
            }

            int fallback = GetTemplateFallbackPrice(item);

            if (fallback > 0)
            {
                if (!IsRealContainer(item))
                {
                    CacheBasePrice(item, fallback);
                }

                return fallback;
            }

            return 0;
        }

        private static int GetSingleItemTotalSellPrice(Item item)
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

            int unitPrice = GetSingleItemPrice(item);
            if (unitPrice <= 0)
            {
                return 0;
            }

            int stackCount = item.StackObjectsCount > 1 ? item.StackObjectsCount : 1;
            return unitPrice * stackCount;
        }

        private static int GetMoneyStackValue(Item item)
        {
            if (!(item is MoneyItemClass))
            {
                return 0;
            }

            int stackCount = item.StackObjectsCount > 1 ? item.StackObjectsCount : 1;
            float rate = GetMoneyRoubleRate(item);
            if (rate <= 0f)
            {
                return 0;
            }

            return Mathf.RoundToInt(stackCount * rate);
        }

        private static float GetMoneyRoubleRate(Item item)
        {
            string templateId = GetTemplateIdSafe(item);
            if (string.Equals(templateId, "5449016a4bdc2d6f028b456f", StringComparison.Ordinal))
            {
                return 1f;
            }

            object template = item?.Template;
            object rawRate = GetMemberValue(template, "rate", "Rate");
            if (rawRate is float floatRate && floatRate > 0f)
            {
                return floatRate;
            }

            if (rawRate is double doubleRate && doubleRate > 0d)
            {
                return (float)doubleRate;
            }

            if (rawRate is int intRate && intRate > 0)
            {
                return intRate;
            }

            if (string.Equals(templateId, "5696686a4bdc2da3298b456a", StringComparison.Ordinal))
            {
                return 130f;
            }

            if (string.Equals(templateId, "569668774bdc2da2298b4568", StringComparison.Ordinal))
            {
                return 145f;
            }

            return 0f;
        }

        private static bool IsMagazine(Item item)
        {
            return item is MagazineItemClass;
        }

        private static int GetLoadedAmmoTraderPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int total = 0;

            foreach (Item child in item.GetAllItems())
            {
                if (child == null || child == item)
                {
                    continue;
                }

                if (!(child is AmmoItemClass))
                {
                    continue;
                }

                total += GetSingleItemTotalSellPrice(child);
            }

            return total;
        }

        private static bool ShouldUseModAttachmentBreakdown(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (item is Weapon || item is AmmoItemClass)
            {
                return false;
            }

            if (IsMagazine(item) || IsArmoredRig(item) || IsRealContainer(item) || HasHardPlateSlots(item))
            {
                return false;
            }

            if (!(item is CompoundItem))
            {
                return false;
            }

            return item.GetAllItems().Any(child => ShouldCountAsModAttachment(item, child));
        }

        private static int GetModAttachmentPrice(Item item)
        {
            if (!ShouldUseModAttachmentBreakdown(item))
            {
                return 0;
            }

            int total = 0;
            List<Item> attachmentRoots = GetRootItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsModAttachment(item, child))
                    .ToList());

            foreach (Item child in attachmentRoots)
            {
                total += GetAttachmentRootTotalSellPrice(child);
            }

            return total;
        }

        private static int GetAttachmentRootTotalSellPrice(Item item)
        {
            int total = GetSingleItemTotalSellPrice(item);

            if (!UseFleaPriceSource || item == null)
            {
                return total;
            }

            if (item is Weapon)
            {
                return total + GetWeaponAttachmentTraderPrice(item);
            }

            if (ShouldUseModAttachmentBreakdown(item))
            {
                return total + GetModAttachmentPrice(item);
            }

            return total;
        }

        private static int GetWeaponTotalSellValue(Item item)
        {
            int total = GetSingleItemTotalSellPrice(item);

            if (UseFleaPriceSource)
            {
                return total + GetWeaponAttachmentTraderPrice(item) + GetWeaponMagazineTraderPrice(item);
            }

            return total;
        }

        private static int GetModAttachmentRootTotalSellValue(Item item)
        {
            int total = GetSingleItemTotalSellPrice(item);

            if (UseFleaPriceSource && ShouldUseModAttachmentBreakdown(item))
            {
                return total + GetModAttachmentPrice(item);
            }

            return total;
        }

        private static bool ShouldCountAsModAttachment(Item parent, Item child)
        {
            if (parent == null || child == null || ReferenceEquals(parent, child))
            {
                return false;
            }

            if (child is AmmoItemClass)
            {
                return false;
            }

            if (IsItemInAnyArmorSlot(child) || IsItemInBuiltInSlot(child))
            {
                return false;
            }

            return true;
        }

        private static int GetWeaponAttachmentTraderPrice(Item item)
        {
            if (!(item is Weapon))
            {
                return 0;
            }

            int total = 0;
            List<Item> attachmentRoots = GetRootItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsWeaponAttachment(item, child))
                    .ToList());

            foreach (Item child in attachmentRoots)
            {
                total += GetAttachmentRootTotalSellPrice(child);
            }

            return total;
        }

        private static int GetWeaponMagazineTraderPrice(Item item)
        {
            if (!(item is Weapon))
            {
                return 0;
            }

            int total = 0;
            List<Item> magazineRoots = GetRootItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsWeaponMagazine(item, child))
                    .ToList());

            foreach (Item child in magazineRoots)
            {
                total += GetSingleItemTotalSellPrice(child);
            }

            return total;
        }

        private static TraderOffer GetWeaponBaseTraderSellOffer(Item item)
        {
            if (!(item is Weapon))
            {
                return null;
            }

            try
            {
                Item queryItem = BuildWeaponBaseTraderQueryItem(item);

                if (queryItem == null)
                {
                    return null;
                }

                return PluginConfig.ContainerPriceMode.Value == PriceMode.Best
                    ? GetBestTraderOfferFromQueryItem(queryItem, item)
                    : GetAverageTraderOfferFromQueryItem(queryItem, item);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] Weapon base trader query failed for {item.ShortName}: {ex.Message}");
                return null;
            }
        }

        private static bool ShouldCountAsWeaponAttachment(Item weapon, Item child)
        {
            if (!(weapon is Weapon) || child == null || child == weapon)
            {
                return false;
            }

            return !(child is AmmoItemClass) &&
                   IsRemovableWeaponAttachment(child);
        }

        private static bool ShouldCountAsWeaponMagazine(Item weapon, Item child)
        {
            if (!(weapon is Weapon) || child == null || child == weapon)
            {
                return false;
            }

            return IsWeaponMagazine(child);
        }

        private static bool IsWeaponMagazine(Item item)
        {
            if (!IsMagazine(item))
            {
                return false;
            }

            return TryGetItemSlotName(item, out string slotName) &&
                   IsWeaponMagazineSlot(slotName);
        }

        private static bool IsWeaponMagazineSlot(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                return false;
            }

            return slotName.IndexOf("magazine", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRemovableWeaponAttachment(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (!TryGetItemSlotName(item, out string slotName) ||
                !IsRemovableWeaponAttachmentSlot(slotName))
            {
                return false;
            }

            if (TryReadTemplateBool(item, "RaidModdable", out bool raidModdable))
            {
                return raidModdable;
            }

            return true;
        }

        private static bool IsRemovableWeaponAttachmentSlot(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                return false;
            }

            string lower = slotName.ToLowerInvariant();
            return lower.Contains("foregrip") ||
                   lower.Contains("scope") ||
                   lower.Contains("sight") ||
                   lower.Contains("muzzle") ||
                   lower.Contains("silencer") ||
                   lower.Contains("suppressor") ||
                   lower.Contains("stock");
        }

        private static bool TryReadTemplateBool(Item item, string memberName, out bool value)
        {
            value = false;

            if (item == null || item.Template == null)
            {
                return false;
            }

            if (TryReadBoolMember(item.Template, memberName, out value))
            {
                return true;
            }

            object props = GetMemberValue(item.Template, "Props", "_props");
            return props != null && TryReadBoolMember(props, memberName, out value);
        }

        private static Item BuildWeaponBaseTraderQueryItem(Item item)
        {
            if (!(item is Weapon))
            {
                return BuildTraderQueryItem(item, preserveOriginalId: false, stripContainerContents: true);
            }

            Item clone = item.CloneItem();
            clone.StackObjectsCount = 1;
            clone.UnlimitedCount = false;

            ClearCompoundGrids(clone as CompoundItem);
            StripRemovableWeaponAttachments(clone as CompoundItem);

            return clone;
        }

        private static void StripRemovableWeaponAttachments(CompoundItem compound)
        {
            if (compound == null || compound.Slots == null)
            {
                return;
            }

            foreach (Slot slot in compound.Slots)
            {
                try
                {
                    Item contained = slot?.ContainedItem;
                    if (contained == null)
                    {
                        continue;
                    }

                    if (contained is AmmoItemClass ||
                        IsWeaponMagazine(contained) ||
                        IsRemovableWeaponAttachment(contained))
                    {
                        ClearSlotContainedItem(slot);
                        continue;
                    }

                    ClearCompoundGrids(contained as CompoundItem);
                    StripRemovableWeaponAttachments(contained as CompoundItem);
                }
                catch
                {
                }
            }
        }

        private static void ClearCompoundGrids(CompoundItem compound)
        {
            if (compound == null || compound.Grids == null)
            {
                return;
            }

            foreach (var grid in compound.Grids)
            {
                Type t = grid.GetType();
                while (t != null)
                {
                    bool found = false;
                    foreach (var field in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!field.FieldType.IsGenericType)
                        {
                            continue;
                        }

                        var val = field.GetValue(grid);
                        var asDict = val as IDictionary;
                        if (asDict != null)
                        {
                            asDict.Clear();
                            found = true;
                            break;
                        }

                        var asList = val as IList;
                        if (asList != null)
                        {
                            asList.Clear();
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        break;
                    }

                    t = t.BaseType;
                }
            }
        }

        private static void ClearSlotContainedItem(Slot slot)
        {
            if (slot == null)
            {
                return;
            }

            var slotType = slot.GetType();
            while (slotType != null)
            {
                var field = slotType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(f => typeof(Item).IsAssignableFrom(f.FieldType));
                if (field != null)
                {
                    field.SetValue(slot, null);
                    break;
                }

                slotType = slotType.BaseType;
            }
        }

        private static int GetArmoredRigBasePrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (UseFleaPriceSource)
            {
                int fleaPrice = GetFleaTemplatePrice(item);
                if (fleaPrice > 0)
                {
                    Plugin.Log?.LogInfo($"[AvgSellPrice] ARMORED RIG base from flea price {item.ShortName}: {fleaPrice}");
                    return fleaPrice;
                }
            }

            string templateId = GetTemplateIdSafe(item);

            int liveOfferPrice = GetArmoredRigBasePriceFromLiveOffer(item);
            if (IsPlausibleArmoredRigBasePrice(item, liveOfferPrice))
            {
                if (!string.IsNullOrEmpty(templateId))
                {
                    VerifiedArmoredRigPriceCache.StorePrice(templateId, liveOfferPrice);
                }

                Plugin.Log?.LogInfo($"[AvgSellPrice] ARMORED RIG base from live offer {item.ShortName}: {liveOfferPrice}");
                return liveOfferPrice;
            }

            int donorPrice = TryGetArmoredRigBasePriceFromEmptyIdenticalItem(item);
            if (IsPlausibleArmoredRigBasePrice(item, donorPrice))
            {
                if (!string.IsNullOrEmpty(templateId))
                {
                    VerifiedArmoredRigPriceCache.StorePrice(templateId, donorPrice);
                }

                Plugin.Log?.LogInfo($"[AvgSellPrice] ARMORED RIG base from empty donor {item.ShortName}: {donorPrice}");
                return donorPrice;
            }

            if (!string.IsNullOrEmpty(templateId))
            {
                int verifiedRigPrice = VerifiedArmoredRigPriceCache.GetPrice(templateId);
                if (IsPlausibleArmoredRigBasePrice(item, verifiedRigPrice))
                {
                    Plugin.Log?.LogInfo($"[AvgSellPrice] ARMORED RIG base from verified cache {item.ShortName}: {verifiedRigPrice}");
                    return verifiedRigPrice;
                }
            }

            if (!string.IsNullOrEmpty(templateId))
            {
                int serverPrice = TraderPriceCache.GetPrice(templateId);
                if (serverPrice > 0)
                {
                    Plugin.Log?.LogInfo($"[AvgSellPrice] ARMORED RIG base from server price {item.ShortName}: {serverPrice}");
                    return serverPrice;
                }
            }

            int fallback = GetTemplateFallbackPrice(item);
            if (fallback > 0)
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] ARMORED RIG base from handbook fallback {item.ShortName}: {fallback}");
                return fallback;
            }

            return 0;
        }


        private static TraderOffer GetArmoredRigTraderOffer(Item item)
        {
            if (item == null)
            {
                return null;
            }

            if (UseFleaPriceSource)
            {
                return GetFleaPriceOffer(item);
            }

            TraderOffer liveOffer = GetConfiguredTraderOffer(item);
            if (liveOffer != null && liveOffer.Price > 0)
            {
                return liveOffer;
            }

            Plugin.Log?.LogWarning($"[AvgSellPrice] ARMORED RIG has no trader price: {item.ShortName}");
            return null;
        }

        private static bool IsPlausibleArmoredRigBasePrice(Item item, int price)
        {
            if (price <= 0)
            {
                return false;
            }

            int templateFallback = GetTemplateFallbackPrice(item);
            if (templateFallback <= 0)
            {
                return price >= 5000;
            }

            int minimumExpected = Math.Max(5000, templateFallback / 2);
            return price >= minimumExpected;
        }

        private static int GetArmoredRigBasePriceFromLiveOffer(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            TraderOffer offer = GetConfiguredTraderOffer(item);
            if (offer == null || offer.Price <= 0)
            {
                return 0;
            }

            int platesPrice = GetArmorPlateTraderPrice(item);
            int contentsPrice = GetContentsTraderPrice(item);
            return Math.Max(0, offer.Price - platesPrice - contentsPrice);
        }

        private static string GetContainerBaseTraderName(Item item, TraderOffer preferredOffer)
        {
            if (preferredOffer != null && !string.IsNullOrEmpty(preferredOffer.Name))
            {
                return preferredOffer.Name;
            }

            TraderOffer fallbackOffer = GetConfiguredTraderOffer(item);
            if (fallbackOffer != null && !string.IsNullOrEmpty(fallbackOffer.Name))
            {
                return fallbackOffer.Name;
            }

            if (item is BackpackItemClass || item is VestItemClass)
            {
                return "Ragman";
            }

            if (IsRealContainer(item))
            {
                return "Therapist";
            }

            return string.Empty;
        }

        private static TraderOffer GetContainerBaseTraderOffer(Item item)
        {
            if (item == null || IsArmoredRig(item))
            {
                return null;
            }

            if (UseFleaPriceSource)
            {
                return GetFleaPriceOffer(item);
            }

            bool hasChildren = HasChildren(item);

            try
            {
                if (hasChildren)
                {
                    Item queryItem = BuildTraderQueryItem(
                        item,
                        preserveOriginalId: true,
                        stripContainerContents: true);

                    TraderOffer queryOffer = GetConfiguredTraderOfferFromQueryItem(queryItem, item);
                    if (queryOffer != null && queryOffer.Price > 0)
                    {
                        Plugin.Log?.LogInfo($"[AvgSellPrice] CONTAINER preserved-id query price {item.ShortName}: {queryOffer.Price} via {queryOffer.Name}");
                        return queryOffer;
                    }

                    queryItem = BuildTraderQueryItem(
                        item,
                        preserveOriginalId: false,
                        stripContainerContents: true);

                    queryOffer = GetConfiguredTraderOfferFromQueryItem(queryItem, item);
                    if (queryOffer != null && queryOffer.Price > 0)
                    {
                        Plugin.Log?.LogInfo($"[AvgSellPrice] CONTAINER stripped query price {item.ShortName}: {queryOffer.Price} via {queryOffer.Name}");
                        return queryOffer;
                    }
                }

                if (hasChildren)
                {
                    return null;
                }

                TraderOffer offer = GetConfiguredTraderOffer(item);
                if (offer != null && offer.Price > 0)
                {
                    Plugin.Log?.LogInfo($"[AvgSellPrice] CONTAINER fallback price {item.ShortName}: {offer.Price} via {offer.Name}");
                    return offer;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] Container query failed for {item.ShortName}: {ex.Message}");
            }

            return null;
        }

        private static Item BuildArmoredRigQueryItem(Item item)
        {
            if (item == null)
            {
                return null;
            }

            Item clone = item.CloneItem();
            clone.StackObjectsCount = 1;
            clone.UnlimitedCount = false;

            CompoundItem compound = clone as CompoundItem;
            if (compound == null)
            {
                return clone;
            }

            foreach (var grid in compound.Grids)
            {
                Type t = grid.GetType();

                while (t != null)
                {
                    bool found = false;

                    foreach (var field in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!field.FieldType.IsGenericType)
                        {
                            continue;
                        }

                        var val = field.GetValue(grid);
                        var asDict = val as IDictionary;
                        if (asDict != null)
                        {
                            asDict.Clear();
                            found = true;
                            break;
                        }

                        var asList = val as IList;
                        if (asList != null)
                        {
                            asList.Clear();
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        break;
                    }

                    t = t.BaseType;
                }
            }

            foreach (Slot slot in compound.Slots)
            {
                if (slot == null || slot.ContainedItem == null)
                {
                    continue;
                }

                if (IsBuiltInSlot(slot))
                {
                    continue;
                }

                try
                {
                    Type slotType = slot.GetType();

                    while (slotType != null)
                    {
                        FieldInfo field = slotType
                            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                            .FirstOrDefault(f => typeof(Item).IsAssignableFrom(f.FieldType));

                        if (field != null)
                        {
                            field.SetValue(slot, null);
                            break;
                        }

                        slotType = slotType.BaseType;
                    }
                }
                catch
                {
                }
            }

            return clone;
        }


        private static int GetServerContainerBasePrice(Item item)
        {
            string templateId = GetTemplateIdSafe(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            int rawPrice = TraderPriceCache.GetPrice(templateId);

            if (rawPrice <= 0)
            {
                return 0;
            }

            int fallbackSellPrice = (int)Math.Floor(rawPrice * 0.6);

            Plugin.Log?.LogInfo(
                $"[AvgSellPrice] SERVER FALLBACK {item.ShortName} ({templateId}) raw={rawPrice} fallback60={fallbackSellPrice}");

            return fallbackSellPrice;
        }

        private static int GetContainerBasePriceRobust(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (IsArmoredRig(item))
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] Skipping container base logic for armored rig {item.ShortName}");
                return 0;
            }

            if (UseFleaPriceSource)
            {
                int fleaPrice = GetFleaTemplatePrice(item);
                if (fleaPrice > 0)
                {
                    Plugin.Log?.LogInfo($"[AvgSellPrice] BASE from flea price {item.ShortName}: {fleaPrice}");
                    return fleaPrice;
                }
            }

            bool hasChildren = HasChildren(item);

            int verifiedPrice = GetVerifiedContainerBasePrice(item);
            if (verifiedPrice > 0)
            {
                return verifiedPrice;
            }

            int emptyClonePrice = GetEmptyCloneContainerPrice(item);
            if (emptyClonePrice > 0)
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] BASE from empty clone {item.ShortName}: {emptyClonePrice}");
                CacheBasePrice(item, emptyClonePrice);
                StoreVerifiedContainerBasePrice(item, emptyClonePrice, "empty clone");
                return emptyClonePrice;
            }

            int donorPrice = TryGetBasePriceFromEmptyIdenticalItem(item);
            if (donorPrice > 0)
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] BASE from donor {item.ShortName}: {donorPrice}");
                CacheBasePrice(item, donorPrice);
                StoreVerifiedContainerBasePrice(item, donorPrice, "donor");
                return donorPrice;
            }

            int serverPrice = GetServerContainerBasePrice(item);
            if (serverPrice > 0)
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] BASE from server fallback {item.ShortName}: {serverPrice}");
                return serverPrice;
            }

            int templateFallback = GetTemplateFallbackPrice(item);
            if (templateFallback > 0)
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] BASE from handbook fallback {item.ShortName}: {templateFallback}");
                return templateFallback;
            }

            return 0;
        }

        private static int GetEmptyCloneContainerPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (Session == null || Session.Profile == null)
            {
                return 0;
            }

            if (!Session.Profile.Examined(item))
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] EMPTY CLONE SKIPPED, NOT EXAMINED: {item.ShortName} ({GetTemplateIdSafe(item)})");
                return 0;
            }

            try
            {
                Item queryItem = BuildTraderQueryItem(item, preserveOriginalId: false, stripContainerContents: true);

                if (Session?.Traders != null)
                {
                    foreach (var t in Session.Traders.Where(t => t != null && t.Settings != null && !t.Settings.AvailableInRaid))
                    {
                        var p = t.GetUserItemPrice(queryItem);
                        var supply = t.GetSupplyDataSafe();
                        Plugin.Log?.LogInfo($"[AvgSellPrice] CLONE {item.ShortName} @ {t.LocalizedName}: price={p?.Amount.ToString() ?? "NULL"} supply={supply != null} courses={supply?.CurrencyCourses != null}");
                    }
                }

                return GetAverageTraderPriceFromQueryItem(queryItem, item);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] Empty clone pricing failed for {item.ShortName}: {ex.Message}");
                return 0;
            }
        }

        private static int TryGetBasePriceFromEmptyIdenticalItem(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            string templateId = GetTemplateIdSafe(item);
            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            List<Item> allItems = GetAllPlayerItemsSafe();

            foreach (Item candidate in allItems)
            {
                if (candidate == null || ReferenceEquals(candidate, item))
                {
                    continue;
                }

                string candidateTemplateId = GetTemplateIdSafe(candidate);
                if (!string.Equals(candidateTemplateId, templateId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsRealContainer(candidate))
                {
                    continue;
                }

                if (HasChildren(candidate))
                {
                    continue;
                }

                int price = GetSingleItemPrice(candidate);
                if (price > 0)
                {
                    return price;
                }
            }

            return 0;
        }

        private static int TryGetArmoredRigBasePriceFromEmptyIdenticalItem(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            string templateId = GetTemplateIdSafe(item);
            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            List<Item> allItems = GetAllPlayerItemsSafe();

            foreach (Item candidate in allItems)
            {
                if (candidate == null || ReferenceEquals(candidate, item))
                {
                    continue;
                }

                if (!IsArmoredRig(candidate))
                {
                    continue;
                }

                string candidateTemplateId = GetTemplateIdSafe(candidate);
                if (!string.Equals(candidateTemplateId, templateId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (HasChildren(candidate))
                {
                    continue;
                }

                int price = GetArmoredRigBasePriceFromLiveOffer(candidate);
                if (IsPlausibleArmoredRigBasePrice(candidate, price))
                {
                    return price;
                }
            }

            return 0;
        }

        private static int GetContainerBasePrice(Item item)
        {
            return GetContainerBasePriceRobust(item);
        }

        private static int GetContentsTraderPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int total = 0;
            List<Item> contentRoots = GetRootItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsContents(item, child))
                    .ToList());

            foreach (Item child in contentRoots)
            {
                int childPrice = GetContentsItemSellPrice(child);

                total += childPrice;
            }

            return total;
        }

        private static int GetContentsItemSellPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item is Weapon)
            {
                return GetWeaponTotalSellValue(item);
            }

            if (IsRealContainer(item) || IsArmoredRig(item))
            {
                return GetDisplayMainPrice(item);
            }

            if (ShouldUseModAttachmentBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return GetSingleItemTotalSellPrice(item);
        }

        private static int GetDisplayMainPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item is Weapon)
            {
                return GetWeaponTotalSellValue(item);
            }

            if (IsArmoredRig(item))
            {
                int rigPrice = GetArmoredRigBasePrice(item);
                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = GetContentsTraderPrice(item);
                return rigPrice + platesPrice + contentsPrice;
            }

            if (IsRealContainer(item))
            {
                int basePrice = GetContainerBasePriceRobust(item);
                int contentsPrice = GetContentsTraderPrice(item);
                return basePrice + contentsPrice;
            }

            if (ShouldUseModAttachmentBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return GetSingleItemPrice(item);
        }

        private static TraderOffer GetTraderOffer(Item item, TraderClass trader)
        {
            if (item == null || trader == null)
            {
                return null;
            }

            var price = trader.GetUserItemPrice(item);

            if (!price.HasValue)
            {
                return null;
            }

            if (!price.Value.CurrencyId.HasValue)
            {
                return null;
            }

            string currencyId = price.Value.CurrencyId.Value;

            SupplyData supply = trader.GetSupplyDataSafe();

            if (supply == null || supply.CurrencyCourses == null)
            {
                string rubCurrencyId = "5449016a4bdc2d6f028b456f";

                if (string.Equals(currencyId, rubCurrencyId, StringComparison.Ordinal))
                {
                    return new TraderOffer(
                        trader.LocalizedName,
                        price.Value.Amount,
                        CurrencyUtil.GetCurrencyCharById(currencyId),
                        1.0
                    );
                }

                return null;
            }

            if (!supply.CurrencyCourses.ContainsKey(currencyId))
            {
                string rubCurrencyId = "5449016a4bdc2d6f028b456f";

                if (string.Equals(currencyId, rubCurrencyId, StringComparison.Ordinal))
                {
                    return new TraderOffer(
                        trader.LocalizedName,
                        price.Value.Amount,
                        CurrencyUtil.GetCurrencyCharById(currencyId),
                        1.0
                    );
                }

                return null;
            }

            return new TraderOffer(
                trader.LocalizedName,
                price.Value.Amount,
                CurrencyUtil.GetCurrencyCharById(currencyId),
                supply.CurrencyCourses[currencyId]
            );
        }

        private static IEnumerable<TraderOffer> GetTraderOffersForQueryItem(Item queryItem, Item examinedSource)
        {
            if (queryItem == null || Session == null || Session.Profile == null || Session.Traders == null)
            {
                return new List<TraderOffer>();
            }

            if (examinedSource != null && !Session.Profile.Examined(examinedSource))
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] NOT EXAMINED: {examinedSource.ShortName} ({GetTemplateIdSafe(examinedSource)})");
                return new List<TraderOffer>();
            }

            return Session.Traders
                .Where(t => t != null)
                .Where(t => t.Settings != null)
                .Where(t => !t.Settings.AvailableInRaid)
                .Select(t => GetTraderOffer(queryItem, t))
                .Where(o => o != null)
                .OrderByDescending(o => o.Price * o.Course)
                .ToList();
        }

        private static Item BuildTraderQueryItem(Item item, bool preserveOriginalId, bool stripContainerContents)
        {
            if (item == null)
            {
                return null;
            }

            Item clone = item.CloneItem();
            clone.StackObjectsCount = 1;
            clone.UnlimitedCount = false;

            if (preserveOriginalId)
            {
                try
                {
                    FieldInfo idField = typeof(Item).GetField("Id",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (idField == null)
                    {
                        idField = typeof(Item).GetField("_id",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (idField != null)
                    {
                        idField.SetValue(clone, item.Id);
                    }
                }
                catch
                {
                }
            }

            if (!stripContainerContents)
            {
                return clone;
            }

            CompoundItem compound = clone as CompoundItem;
            if (compound == null)
            {
                return clone;
            }

            foreach (var grid in compound.Grids)
            {
                Type t = grid.GetType();
                while (t != null)
                {
                    bool found = false;
                    foreach (var field in t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!field.FieldType.IsGenericType)
                        {
                            continue;
                        }

                        var val = field.GetValue(grid);
                        var asDict = val as IDictionary;
                        if (asDict != null)
                        {
                            asDict.Clear();
                            found = true;
                            break;
                        }

                        var asList = val as IList;
                        if (asList != null)
                        {
                            asList.Clear();
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        break;
                    }

                    t = t.BaseType;
                }
            }

            foreach (var slot in compound.Slots)
            {
                try
                {
                    if (slot.ContainedItem == null)
                    {
                        continue;
                    }

                    var slotType = slot.GetType();
                    while (slotType != null)
                    {
                        var field = slotType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                            .FirstOrDefault(f => typeof(Item).IsAssignableFrom(f.FieldType));
                        if (field != null)
                        {
                            field.SetValue(slot, null);
                            break;
                        }

                        slotType = slotType.BaseType;
                    }
                }
                catch
                {
                }
            }

            return clone;
        }

        private static IEnumerable<TraderOffer> GetAllTraderOffers(Item item)
        {
            if (item == null)
            {
                return new List<TraderOffer>();
            }

            if (Session == null || Session.Profile == null || Session.Traders == null)
            {
                return new List<TraderOffer>();
            }

            if (!Session.Profile.Examined(item))
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] NOT EXAMINED: {item.ShortName} ({GetTemplateIdSafe(item)})");
                return new List<TraderOffer>();
            }

            bool stripContainerContents = IsRealContainer(item) && HasChildren(item);

            try
            {
                Item primaryQueryItem = BuildTraderQueryItem(
                    item,
                    preserveOriginalId: true,
                    stripContainerContents: stripContainerContents);

                List<TraderOffer> primaryOffers = GetTraderOffersForQueryItem(primaryQueryItem, item).ToList();
                if (primaryOffers.Count > 0)
                {
                    return primaryOffers;
                }

                if (stripContainerContents)
                {
                    Item fallbackQueryItem = BuildTraderQueryItem(
                        item,
                        preserveOriginalId: false,
                        stripContainerContents: true);

                    List<TraderOffer> fallbackOffers = GetTraderOffersForQueryItem(fallbackQueryItem, item).ToList();
                    if (fallbackOffers.Count > 0)
                    {
                        Plugin.Log?.LogInfo($"[AvgSellPrice] Container fallback without original id worked for {item.ShortName}");
                        return fallbackOffers;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] Trader query clone failed for {item.ShortName}: {ex.Message}");
            }

            return GetTraderOffersForQueryItem(item, item);
        }

        private static TraderOffer GetBestTraderOffer(Item item)
        {
            return GetAllTraderOffers(item)
                .OrderByDescending(x => x.Price * x.Course)
                .FirstOrDefault();
        }

        private static TraderOffer GetBestTraderOfferFromQueryItem(Item queryItem, Item examinedSource)
        {
            return GetTraderOffersForQueryItem(queryItem, examinedSource)
                .OrderByDescending(x => x.Price * x.Course)
                .FirstOrDefault();
        }

        private static TraderOffer GetAverageTraderOffer(Item item)
        {
            List<TraderOffer> offers = GetAllTraderOffers(item)
                .Take(3)
                .ToList();

            if (offers.Count == 0)
            {
                return null;
            }

            List<TraderOffer> ordered = offers
                .OrderBy(x => x.Price)
                .ToList();

            int mid = ordered.Count / 2;

            if (ordered.Count % 2 == 0)
            {
                int avgPrice = (ordered[mid - 1].Price + ordered[mid].Price) / 2;
                TraderOffer topOffer = offers.OrderByDescending(x => x.Price * x.Course).First();
                return new TraderOffer(topOffer.Name, avgPrice, topOffer.Currency, topOffer.Course);
            }

            return ordered[mid];
        }

        private static TraderOffer GetAverageTraderOfferFromQueryItem(Item queryItem, Item examinedSource)
        {
            List<TraderOffer> offers = GetTraderOffersForQueryItem(queryItem, examinedSource)
                .Take(3)
                .ToList();

            if (offers.Count == 0)
            {
                return null;
            }

            List<TraderOffer> ordered = offers
                .OrderBy(x => x.Price)
                .ToList();

            int mid = ordered.Count / 2;

            if (ordered.Count % 2 == 0)
            {
                int avgPrice = (ordered[mid - 1].Price + ordered[mid].Price) / 2;
                TraderOffer topOffer = offers.OrderByDescending(x => x.Price * x.Course).First();
                return new TraderOffer(topOffer.Name, avgPrice, topOffer.Currency, topOffer.Course);
            }

            return ordered[mid];
        }

        private static TraderOffer GetConfiguredTraderOffer(Item item)
        {
            if (UseFleaPriceSource)
            {
                TraderOffer fleaOffer = GetFleaPriceOffer(item);
                if (fleaOffer != null)
                {
                    return fleaOffer;
                }
            }

            return PluginConfig.ContainerPriceMode.Value == PriceMode.Best
                ? GetBestTraderOffer(item)
                : GetAverageTraderOffer(item);
        }

        private static TraderOffer GetConfiguredTraderSellOffer(Item item)
        {
            return PluginConfig.ContainerPriceMode.Value == PriceMode.Best
                ? GetBestTraderOffer(item)
                : GetAverageTraderOffer(item);
        }

        private static TraderOffer GetConfiguredTraderOfferFromQueryItem(Item queryItem, Item examinedSource)
        {
            if (UseFleaPriceSource)
            {
                TraderOffer fleaOffer = GetFleaPriceOffer(examinedSource);
                if (fleaOffer != null)
                {
                    return fleaOffer;
                }
            }

            return PluginConfig.ContainerPriceMode.Value == PriceMode.Best
                ? GetBestTraderOfferFromQueryItem(queryItem, examinedSource)
                : GetAverageTraderOfferFromQueryItem(queryItem, examinedSource);
        }

        private static string GetConfiguredTraderName(Item item)
        {
            TraderOffer offer = GetConfiguredTraderOffer(item);
            return offer != null ? offer.Name : string.Empty;
        }

        private static int GetAverageTraderPriceFromQueryItem(Item queryItem, Item examinedSource)
        {
            TraderOffer offer = GetConfiguredTraderOfferFromQueryItem(queryItem, examinedSource);
            return offer != null ? offer.Price : 0;
        }

        private static int GetAverageTraderPriceInternal(Item item)
        {
            TraderOffer offer = GetConfiguredTraderOffer(item);
            return offer != null ? offer.Price : 0;
        }

        private static string FormatPrice(int price)
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

        private static string FormatPrecise(int price)
        {
            return price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", " ") + " ₽";
        }


        public static void ClearPriceCacheSafe()
        {
            CachedBasePricesByTemplateId.Clear();
        }

        public static bool TryPrecacheContainerPrice(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (!IsRealContainer(item))
            {
                return false;
            }

            if (IsArmoredRig(item))
            {
                return false;
            }

            if (GetCachedBasePrice(item) > 0)
            {
                return true;
            }

            int price = GetContainerBasePriceRobust(item);
            return price > 0;
        }

        public static List<Item> GetAllPlayerItemsSafe()
        {
            List<Item> result = new List<Item>();

            if (Session == null)
            {
                return result;
            }

            if (Session.Profile == null)
            {
                return result;
            }

            if (Session.Profile.Inventory == null)
            {
                return result;
            }

            object inventory = Session.Profile.Inventory;
            HashSet<Item> uniqueItems = new HashSet<Item>();

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (PropertyInfo property in inventory.GetType().GetProperties(flags))
            {
                object value = null;

                try
                {
                    if (property.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    value = property.GetValue(inventory, null);
                }
                catch
                {
                    continue;
                }

                AddItemsFromUnknownValue(value, uniqueItems);
            }

            foreach (FieldInfo field in inventory.GetType().GetFields(flags))
            {
                object value = null;

                try
                {
                    value = field.GetValue(inventory);
                }
                catch
                {
                    continue;
                }

                AddItemsFromUnknownValue(value, uniqueItems);
            }

            result.AddRange(uniqueItems);
            return result;
        }

        private static void AddItemsFromUnknownValue(object value, HashSet<Item> uniqueItems)
        {
            if (value == null)
            {
                return;
            }

            Item singleItem = value as Item;
            if (singleItem != null)
            {
                AddItemTree(singleItem, uniqueItems);
                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (object entry in enumerable)
            {
                Item entryItem = entry as Item;
                if (entryItem != null)
                {
                    AddItemTree(entryItem, uniqueItems);
                }
            }
        }

        private static void AddItemsFromUnknownValue(object value, HashSet<string> uniqueIds, List<Item> result)
        {
            if (value == null || result == null)
            {
                return;
            }

            Item singleItem = value as Item;
            if (singleItem != null)
            {
                if (string.IsNullOrEmpty(singleItem.Id) || uniqueIds.Add(singleItem.Id))
                {
                    result.Add(singleItem);
                }

                return;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            foreach (object entry in enumerable)
            {
                if (entry == null)
                {
                    continue;
                }

                Item entryItem = entry as Item;
                if (entryItem != null)
                {
                    if (string.IsNullOrEmpty(entryItem.Id) || uniqueIds.Add(entryItem.Id))
                    {
                        result.Add(entryItem);
                    }

                    continue;
                }

                Type entryType = entry.GetType();
                PropertyInfo itemProperty = entryType.GetProperty(
                    "Item",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (itemProperty != null && typeof(Item).IsAssignableFrom(itemProperty.PropertyType))
                {
                    Item propertyItem = itemProperty.GetValue(entry, null) as Item;
                    if (propertyItem != null && (string.IsNullOrEmpty(propertyItem.Id) || uniqueIds.Add(propertyItem.Id)))
                    {
                        result.Add(propertyItem);
                    }

                    continue;
                }

                FieldInfo itemField = entryType.GetField(
                    "Item",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (itemField != null && typeof(Item).IsAssignableFrom(itemField.FieldType))
                {
                    Item fieldItem = itemField.GetValue(entry) as Item;
                    if (fieldItem != null && (string.IsNullOrEmpty(fieldItem.Id) || uniqueIds.Add(fieldItem.Id)))
                    {
                        result.Add(fieldItem);
                    }
                }
            }
        }

        private static bool IsArmoredRig(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (!(item is VestItemClass))
            {
                return false;
            }

            try
            {
                CompoundItem compound = item as CompoundItem;

                if (compound != null && compound.Slots != null)
                {
                    foreach (Slot slot in compound.Slots)
                    {
                        if (slot == null || string.IsNullOrEmpty(slot.Name))
                        {
                            continue;
                        }

                        string slotName = slot.Name.ToLowerInvariant();

                        if (slotName.Contains("armor") ||
                            slotName.Contains("plate") ||
                            slotName.Contains("soft"))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static void AddItemTree(Item rootItem, HashSet<Item> uniqueItems)
        {
            if (rootItem == null)
            {
                return;
            }

            IEnumerable<Item> allItems = null;

            try
            {
                allItems = rootItem.GetAllItems();
            }
            catch
            {
                allItems = null;
            }

            if (allItems == null)
            {
                uniqueItems.Add(rootItem);
                return;
            }

            foreach (Item item in allItems)
            {
                if (item != null)
                {
                    uniqueItems.Add(item);
                }
            }
        }

        private static bool ItemTreeContains(Item rootItem, Item candidate)
        {
            if (rootItem == null || candidate == null)
            {
                return false;
            }

            string candidateId = candidate.Id;

            try
            {
                foreach (Item item in rootItem.GetAllItems())
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (ReferenceEquals(item, candidate))
                    {
                        return true;
                    }

                    if (!string.IsNullOrEmpty(candidateId) && string.Equals(item.Id, candidateId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }
    }
}



