using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Comfort.Common;
using CurrencyUtil = GClass3130;
using static GearAndLootValue.TarkovItemPrices;
using static GearAndLootValue.ContainerPricing;
namespace GearAndLootValue
{
    internal static class TraderSellOffers
    {
        internal static TraderOffer GetTraderOffer(Item item, TraderClass trader)
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

            SupplyData supply = trader.TryGetSupplyData();

            if (supply == null || supply.CurrencyCourses == null)
            {
                string rubCurrencyId = TarkovMoney.RoubleTpl;

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
                string rubCurrencyId = TarkovMoney.RoubleTpl;

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

        internal static IEnumerable<TraderOffer> GetTraderOffersForQueryItem(Item queryItem, Item examinedSource)
        {
            if (queryItem == null || Session == null || Session.Profile == null || Session.Traders == null)
            {
                return new List<TraderOffer>();
            }

            if (examinedSource != null && !Session.Profile.Examined(examinedSource))
            {
                Plugin.LogDebug($"[Gear & Loot Value] NOT EXAMINED: {examinedSource.ShortName} ({TemplateId(examinedSource)})");
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

        internal static Item BuildTraderQueryItem(Item item, bool preserveOriginalId, bool stripContainerContents)
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
                catch (Exception ex)
                {
                    Plugin.LogDebug($"[Gear & Loot Value] Clone id copy failed: {ex.Message}");
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
                catch (Exception ex)
                {
                    Plugin.LogDebug($"[Gear & Loot Value] Query clone slot cleanup skipped a slot: {ex.Message}");
                }
            }

            return clone;
        }

        internal static IEnumerable<TraderOffer> GetAllTraderOffers(Item item)
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
                Plugin.LogDebug($"[Gear & Loot Value] NOT EXAMINED: {item.ShortName} ({TemplateId(item)})");
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
                        Plugin.LogDebug($"[Gear & Loot Value] Container fallback without original id worked for {item.ShortName}");
                        return fallbackOffers;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Trader query clone failed for {item.ShortName}: {ex.Message}");
            }

            return GetTraderOffersForQueryItem(item, item);
        }

        internal static TraderOffer GetBestTraderOffer(Item item)
        {
            return GetAllTraderOffers(item)
                .OrderByDescending(x => x.Price * x.Course)
                .FirstOrDefault();
        }

        internal static TraderOffer GetBestTraderOfferFromQueryItem(Item queryItem, Item examinedSource)
        {
            return GetTraderOffersForQueryItem(queryItem, examinedSource)
                .OrderByDescending(x => x.Price * x.Course)
                .FirstOrDefault();
        }

        internal static TraderOffer GetAverageTraderOffer(Item item)
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

        internal static TraderOffer GetAverageTraderOfferFromQueryItem(Item queryItem, Item examinedSource)
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

        internal static TraderOffer PickOffer(Item item)
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

        internal static TraderOffer TraderSellOffer(Item item)
        {
            return PluginConfig.ContainerPriceMode.Value == PriceMode.Best
                ? GetBestTraderOffer(item)
                : GetAverageTraderOffer(item);
        }

        internal static TraderOffer GetOfferForClone(Item queryItem, Item examinedSource)
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

        internal static int PriceClone(Item queryItem, Item examinedSource)
        {
            TraderOffer offer = GetOfferForClone(queryItem, examinedSource);
            return offer != null ? offer.Price : 0;
        }

    }
}