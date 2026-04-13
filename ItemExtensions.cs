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

            if (item is Weapon)
            {
                TraderOffer weaponOffer = GetConfiguredTraderOffer(item);

                if (weaponOffer == null || weaponOffer.Price <= 0)
                {
                    return string.Empty;
                }

                return FormatMainPriceWithOptionalTrader(weaponOffer.Name, weaponOffer.Price);
            }

            if (IsArmoredRig(item))
            {
                int rigPrice = GetArmoredRigBasePrice(item);

                if (rigPrice <= 0)
                {
                    return string.Empty;
                }

                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = GetContentsTraderPrice(item);
                int totalPrice = rigPrice + platesPrice + contentsPrice;

                List<string> rigLines = new List<string>();

                if (PluginConfig.ShowTraderNameInTooltip.Value)
                {
                    string traderName = GetConfiguredTraderName(item);
                    rigLines.Add(FormatMainPriceWithOptionalTrader(traderName, rigPrice));
                }
                else
                {
                    rigLines.Add(FormatMainPriceWithOptionalTrader(string.Empty, rigPrice));
                }

                if (platesPrice > 0)
                {
                    rigLines.Add("Plates " + FormatPriceExternal(platesPrice));
                }

                if (contentsPrice > 0)
                {
                    rigLines.Add("Contents " + FormatPriceExternal(contentsPrice));
                }

                if (platesPrice > 0 || contentsPrice > 0)
                {
                    rigLines.Add("Total " + FormatPriceExternal(totalPrice));
                }

                return string.Join(Environment.NewLine, rigLines);
            }

            if (IsRealContainer(item))
            {
                int basePrice = GetContainerBasePriceRobust(item);

                if (basePrice <= 0)
                {
                    return string.Empty;
                }

                int contentsPrice = GetContentsTraderPrice(item);
                int totalPrice = basePrice + contentsPrice;

                string traderName = GetConfiguredTraderName(item);

                List<string> lines = new List<string>();
                lines.Add(FormatMainPriceWithOptionalTrader(traderName, basePrice));

                if (contentsPrice > 0)
                {
                    lines.Add("Contents " + FormatPriceExternal(contentsPrice));
                    lines.Add("Total " + FormatPriceExternal(totalPrice));
                }

                return string.Join(Environment.NewLine, lines);
            }

            TraderOffer fallbackOffer = GetConfiguredTraderOffer(item);

            if (fallbackOffer == null || fallbackOffer.Price <= 0)
            {
                return string.Empty;
            }

            return FormatMainPriceWithOptionalTrader(fallbackOffer.Name, fallbackOffer.Price);
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

        private static string FormatMainPriceWithOptionalTrader(string traderName, int rawPrice)
        {
            int price = ApplyMinimumPrice(rawPrice);

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

                return formattedPrice;
            }

            if (showAround)
            {
                return traderName + " around " + formattedPrice;
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

            return false;
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
                   lower.Contains("arm");
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

            GClass3391 address = item.CurrentAddress as GClass3391;
            string slotName = address?.Slot?.Name;

            return IsHardPlateSlotName(slotName);
        }
        private static bool IsItemInAnyArmorSlot(Item item)
        {
            if (item == null)
            {
                return false;
            }

            GClass3391 address = item.CurrentAddress as GClass3391;
            string slotName = address?.Slot?.Name;

            return IsArmorSlotName(slotName);
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

            bool required;
            bool locked;

            bool hasRequired = TryGetSlotBoolField(slot, "_required", out required);
            bool hasLocked = TryGetLockedFromSlotProps(slot, out locked);

            return (hasRequired && required) || (hasLocked && locked);
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

            GClass3391 address = item.CurrentAddress as GClass3391;
            string slotName = address?.Slot?.Name;

            return IsArmorSlotName(slotName);
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

            foreach (Item child in item.GetAllItems())
            {
                if (child == null || child == item)
                {
                    continue;
                }

                if (!IsItemInHardPlateSlot(child))
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

            TraderOffer offer = GetConfiguredTraderOffer(item);

            if (offer != null && offer.Price > 0)
            {
                CacheBasePrice(item, offer.Price);
                return offer.Price;
            }

            int fallback = GetTemplateFallbackPrice(item);

            if (fallback > 0)
            {
                CacheBasePrice(item, fallback);
                return fallback;
            }

            return 0;
        }

        private static int GetArmoredRigBasePrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            try
            {
                Item queryItem = BuildArmoredRigQueryItem(item);
                if (queryItem != null)
                {
                    TraderOffer queryOffer = GetConfiguredTraderOfferFromQueryItem(queryItem, item);
                    if (queryOffer != null && queryOffer.Price > 0)
                    {
                        CacheBasePrice(item, queryOffer.Price);
                        Plugin.Log?.LogInfo($"[AvgSellPrice] ARMORED RIG base without hard plates {item.ShortName}: {queryOffer.Price}");
                        return queryOffer.Price;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] Armored rig base query failed for {item.ShortName}: {ex.Message}");
            }

            int cachedPrice = GetCachedBasePrice(item);
            if (cachedPrice > 0)
            {
                return cachedPrice;
            }

            string templateId = GetTemplateIdSafe(item);
            if (!string.IsNullOrEmpty(templateId))
            {
                int serverPrice = TraderPriceCache.GetPrice(templateId);
                if (serverPrice > 0)
                {
                    CacheBasePrice(item, serverPrice);
                    return serverPrice;
                }
            }

            int fallback = GetTemplateFallbackPrice(item);
            if (fallback > 0)
            {
                CacheBasePrice(item, fallback);
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

            try
            {
                Item queryItem = BuildArmoredRigQueryItem(item);
                if (queryItem == null)
                {
                    return null;
                }

                TraderOffer queryOffer = GetConfiguredTraderOfferFromQueryItem(queryItem, item);
                if (queryOffer != null && queryOffer.Price > 0)
                {
                    Plugin.Log?.LogInfo($"[AvgSellPrice] ARMORED RIG query price {item.ShortName}: {queryOffer.Price} via {queryOffer.Name}");
                    return queryOffer;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[AvgSellPrice] Armored rig query failed for {item.ShortName}: {ex.Message}");
            }

            Plugin.Log?.LogWarning($"[AvgSellPrice] ARMORED RIG has no trader price: {item.ShortName}");
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

                if (!IsHardPlateSlotName(slot.Name))
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

            int verifiedPrice = GetVerifiedContainerBasePrice(item);
            if (verifiedPrice > 0)
            {
                CacheBasePrice(item, verifiedPrice);
                return verifiedPrice;
            }

            int cachedPrice = GetCachedBasePrice(item);
            if (cachedPrice > 0)
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] BASE cached {item.ShortName}: {cachedPrice}");
                return cachedPrice;
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
                CacheBasePrice(item, serverPrice);
                return serverPrice;
            }

            int templateFallback = GetTemplateFallbackPrice(item);
            if (templateFallback > 0)
            {
                Plugin.Log?.LogInfo($"[AvgSellPrice] BASE from handbook fallback {item.ShortName}: {templateFallback}");
                CacheBasePrice(item, templateFallback);
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
            return PluginConfig.ContainerPriceMode.Value == PriceMode.Best
                ? GetBestTraderOffer(item)
                : GetAverageTraderOffer(item);
        }

        private static TraderOffer GetConfiguredTraderOfferFromQueryItem(Item queryItem, Item examinedSource)
        {
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
            string formatted = price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
                .Replace(",", " ");

            return formatted + " ₽";
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

        private static bool IsArmoredRig(Item item)
        {
            if (item == null)
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

            try
            {
                PropertyInfo armorProperty = item.GetType().GetProperty(
                    "ArmorComponent",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (armorProperty != null && armorProperty.GetValue(item, null) != null)
                {
                    return true;
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
    }
}
