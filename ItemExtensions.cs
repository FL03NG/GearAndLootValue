using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
                    int price = GetDisplayMainPrice(item);
                    return price > 0 ? price : 0.01f;
                },

                StringValue = () =>
                {
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

        public static string GetHoverPriceText(this Item item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!HasChildren(item))
            {
                int singlePrice = GetSingleItemPrice(item);

                if (singlePrice <= 0)
                {
                    return string.Empty;
                }

                return FormatPriceLine(null, singlePrice);
            }

            if (item is Weapon)
            {
                int weaponPrice = GetSingleItemPrice(item);

                if (weaponPrice <= 0)
                {
                    return string.Empty;
                }

                return FormatPriceLine(null, weaponPrice);
            }

            if (IsRealContainer(item))
            {
                int basePrice = GetContainerBasePriceRobust(item);
                int contentsPrice = GetContentsTraderPrice(item);
                int totalPrice = 0;

                if (basePrice > 0)
                {
                    totalPrice += basePrice;
                }

                if (contentsPrice > 0)
                {
                    totalPrice += contentsPrice;
                }

                List<string> lines = new List<string>();

                if (basePrice <= 0)
                {
                    basePrice = GetTemplateFallbackPrice(item);
                }

                if (basePrice > 0)
                {
                    lines.Add(FormatPriceLine(null, basePrice));
                }

                if (contentsPrice > 0)
                {
                    lines.Add(FormatPriceLine("Contents", contentsPrice));
                }

                if (basePrice > 0 && contentsPrice > 0)
                {
                    lines.Add(FormatPriceLine("Total", totalPrice));
                }

                if (lines.Count == 0)
                {
                    return string.Empty;
                }

                return string.Join(Environment.NewLine, lines);
            }

            int fallbackPrice = GetSingleItemPrice(item);

            if (fallbackPrice <= 0)
            {
                return string.Empty;
            }

            return FormatPriceLine(null, fallbackPrice);
        }

        private static string FormatPriceLine(string prefix, int rawPrice)
        {
            int price = ApplyMinimumPrice(rawPrice);

            string formattedPrice = PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);

            bool showAround = PluginConfig.ShowAroundPrefix.Value && !PluginConfig.PrecisePrice.Value;

            if (!string.IsNullOrEmpty(prefix))
            {
                if (showAround)
                {
                    return prefix + " around " + formattedPrice;
                }

                return prefix + " " + formattedPrice;
            }

            if (showAround)
            {
                return "Around " + formattedPrice;
            }

            return formattedPrice;
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

            return false;
        }

        private static bool ShouldCountAsContents(Item parent, Item child)
        {
            if (parent == null || child == null || parent == child)
            {
                return false;
            }

            if (!IsRealContainer(parent))
            {
                return false;
            }

            GClass3391 address = child.CurrentAddress as GClass3391;

            if (address == null || address.Slot == null)
            {
                return true;
            }

            string slotName = address.Slot.Name.ToLowerInvariant();

            if (slotName.StartsWith("mod"))
            {
                return false;
            }

            if (slotName.Contains("armor"))
            {
                return false;
            }

            if (slotName.Contains("head"))
            {
                return false;
            }

            if (slotName.Contains("face"))
            {
                return false;
            }

            if (slotName.Contains("ear"))
            {
                return false;
            }

            if (slotName.Contains("collimator"))
            {
                return false;
            }

            if (slotName.Contains("scope"))
            {
                return false;
            }

            return true;
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

        private static int GetTemplateFallbackPrice(Item item)
        {
            if (item == null || item.Template == null)
            {
                return 0;
            }

            if (item.Template.CreditsPrice > 0)
            {
                return item.Template.CreditsPrice;
            }

            return 0;
        }

        private static int GetSingleItemPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int traderPrice = GetAverageTraderPriceInternal(item);

            if (traderPrice > 0)
            {
                CacheBasePrice(item, traderPrice);
                return traderPrice;
            }

            int fallback = GetTemplateFallbackPrice(item);

            if (fallback > 0)
            {
                CacheBasePrice(item, fallback);
                return fallback;
            }

            return 0;
        }

        private static int GetContainerBasePriceRobust(Item item)
{
    if (item == null)
    {
        return 0;
    }

    // 1. Cached base price is best if we already learned it earlier
    int cachedPrice = GetCachedBasePrice(item);
    if (cachedPrice > 0)
    {
        return cachedPrice;
    }

    // 2. Template fallback is more stable for containers than trader price
    // because filled rigs/backpacks often fail trader pricing or return 0
    int templatePrice = GetTemplateFallbackPrice(item);
    if (templatePrice > 0)
    {
        CacheBasePrice(item, templatePrice);
        return templatePrice;
    }

    // 3. Trader price as last fallback
    int traderPrice = GetAverageTraderPriceInternal(item);
    if (traderPrice > 0)
    {
        CacheBasePrice(item, traderPrice);
        return traderPrice;
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

            foreach (Item child in item.GetAllItems())
            {
                if (!ShouldCountAsContents(item, child))
                {
                    continue;
                }

                int childPrice = GetAverageTraderPriceInternal(child);

                if (childPrice <= 0)
                {
                    childPrice = GetTemplateFallbackPrice(child);
                }

                total += childPrice;
            }

            return total;
        }

        private static int GetDisplayMainPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (item is Weapon)
            {
                return GetSingleItemPrice(item);
            }

            if (IsRealContainer(item))
            {
                int basePrice = GetContainerBasePriceRobust(item);
                int contentsPrice = GetContentsTraderPrice(item);
                return basePrice + contentsPrice;
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

            SupplyData supply = trader.GetSupplyDataSafe();

            if (supply == null)
            {
                return null;
            }

            if (!price.Value.CurrencyId.HasValue)
            {
                return null;
            }

            string currencyId = price.Value.CurrencyId.Value;

            if (supply.CurrencyCourses == null)
            {
                return null;
            }

            if (!supply.CurrencyCourses.ContainsKey(currencyId))
            {
                return null;
            }

            return new TraderOffer(
                trader.LocalizedName,
                price.Value.Amount,
                CurrencyUtil.GetCurrencyCharById(currencyId),
                supply.CurrencyCourses[currencyId]
            );
        }

        private static IEnumerable<TraderOffer> GetAllTraderOffers(Item item)
        {
            if (item == null)
            {
                return new List<TraderOffer>();
            }

            if (Session == null)
            {
                return new List<TraderOffer>();
            }

            if (Session.Profile == null)
            {
                return new List<TraderOffer>();
            }

            if (Session.Traders == null)
            {
                return new List<TraderOffer>();
            }

            if (!Session.Profile.Examined(item))
            {
                return new List<TraderOffer>();
            }

            if (item.Owner != null &&
                (item.Owner.OwnerType == EOwnerType.RagFair || item.Owner.OwnerType == EOwnerType.Trader) &&
                (item.StackObjectsCount > 1 || item.UnlimitedCount))
            {
                item = item.CloneItem();
                item.StackObjectsCount = 1;
                item.UnlimitedCount = false;
            }

            return Session.Traders
                .Where(t => t != null)
                .Where(t => t.Settings != null)
                .Where(t => !t.Settings.AvailableInRaid)
                .Select(t => GetTraderOffer(item, t))
                .Where(o => o != null)
                .OrderByDescending(o => o.Price * o.Course);
        }

        private static int GetAverageTraderPriceInternal(Item item)
        {
            List<TraderOffer> offers = GetAllTraderOffers(item)
                .Take(3)
                .ToList();

            if (offers.Count == 0)
            {
                return 0;
            }

            List<int> prices = offers
                .Select(x => x.Price)
                .OrderBy(x => x)
                .ToList();

            int mid = prices.Count / 2;

            if (prices.Count % 2 == 0)
            {
                return (prices[mid - 1] + prices[mid]) / 2;
            }

            return prices[mid];
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
            string formatted = price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", " ");

            return formatted + " ₽";
        }

        public static void ClearPriceCacheSafe()
        {
            CachedBasePricesByTemplateId.Clear();
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
    }
}