using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static GearAndLootValue.TarkovItemPrices;
using static GearAndLootValue.TraderSellOffers;
using static GearAndLootValue.PmcGearValue;
using static GearAndLootValue.InventoryScraper;
using static GearAndLootValue.ArmorPricing;
using static GearAndLootValue.WeaponPricing;
namespace GearAndLootValue
{
    internal static class ContainerPricing
    {
        internal static string TemplateId(Item item)
        {
            if (item == null || item.Template == null)
            {
                return null;
            }

            return item.Template._id;
        }

        internal static void CacheBasePrice(Item item, int price)
        {
            if (item == null || price <= 0)
            {
                return;
            }

            string templateId = TemplateId(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return;
            }

            _cleanBasePriceCache[GetPriceCacheKey(templateId)] = price;
        }

        internal static int GetCachedBasePrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            string templateId = TemplateId(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            int value;
            if (_cleanBasePriceCache.TryGetValue(GetPriceCacheKey(templateId), out value))
            {
                return value;
            }

            return 0;
        }

        internal static string GetPriceCacheKey(string templateId)
        {
            return ActiveSource() + ":" + templateId;
        }

        internal static int GetVerifiedContainerBasePrice(Item item)
        {
            if (item == null || IsArmoredRig(item))
            {
                return 0;
            }

            string templateId = TemplateId(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            int price = VerifiedContainerPriceCache.GetPrice(templateId);

            if (price > 0)
            {
                Plugin.LogDebug($"[Gear & Loot Value] VERIFIED CACHE HIT {item.ShortName} ({templateId}) => {price}");
            }

            return price;
        }

        internal static void StoreVerifiedContainerBasePrice(Item item, int price, string source)
        {
            if (item == null || price <= 0)
            {
                return;
            }

            if (IsArmoredRig(item))
            {
                return;
            }

            string templateId = TemplateId(item);

            if (string.IsNullOrEmpty(templateId))
            {
                return;
            }

            Plugin.LogDebug($"[Gear & Loot Value] VERIFIED PRICE from {source} {item.ShortName} ({templateId}) => {price}");
            VerifiedContainerPriceCache.StoreVerifiedPrice(templateId, price);
        }

        internal static int GetTemplateFallbackPrice(Item item)
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

        internal static string GetContainerBaseTraderName(Item item, TraderOffer preferredOffer)
        {
            if (preferredOffer != null && !string.IsNullOrEmpty(preferredOffer.Name))
            {
                return preferredOffer.Name;
            }

            TraderOffer fallbackOffer = PickOffer(item);
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

        internal static TraderOffer GetContainerBaseTraderOffer(Item item)
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

                    TraderOffer queryOffer = GetOfferForClone(queryItem, item);
                    if (queryOffer != null && queryOffer.Price > 0)
                    {
                        Plugin.LogDebug($"[Gear & Loot Value] CONTAINER preserved-id query price {item.ShortName}: {queryOffer.Price} via {queryOffer.Name}");
                        return queryOffer;
                    }

                    queryItem = BuildTraderQueryItem(
                        item,
                        preserveOriginalId: false,
                        stripContainerContents: true);

                    queryOffer = GetOfferForClone(queryItem, item);
                    if (queryOffer != null && queryOffer.Price > 0)
                    {
                        Plugin.LogDebug($"[Gear & Loot Value] CONTAINER stripped query price {item.ShortName}: {queryOffer.Price} via {queryOffer.Name}");
                        return queryOffer;
                    }
                }

                if (hasChildren)
                {
                    return null;
                }

                TraderOffer offer = PickOffer(item);
                if (offer != null && offer.Price > 0)
                {
                    Plugin.LogDebug($"[Gear & Loot Value] CONTAINER fallback price {item.ShortName}: {offer.Price} via {offer.Name}");
                    return offer;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Container query failed for {item.ShortName}: {ex.Message}");
            }

            return null;
        }

        internal static int GetServerContainerBasePrice(Item item)
        {
            string templateId = TemplateId(item);

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

            Plugin.LogDebug(
                $"[Gear & Loot Value] SERVER FALLBACK {item.ShortName} ({templateId}) raw={rawPrice} fallback60={fallbackSellPrice}");

            return fallbackSellPrice;
        }

        internal static int ContainerBasePrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (IsArmoredRig(item))
            {
                Plugin.LogDebug($"[Gear & Loot Value] Skipping container base logic for armored rig {item.ShortName}");
                return 0;
            }

            if (UseFleaPriceSource)
            {
                int fleaPrice = GetFleaTemplatePrice(item);
                if (fleaPrice > 0)
                {
                    Plugin.LogDebug($"[Gear & Loot Value] BASE from flea price {item.ShortName}: {fleaPrice}");
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
                Plugin.LogDebug($"[Gear & Loot Value] BASE from empty clone {item.ShortName}: {emptyClonePrice}");
                CacheBasePrice(item, emptyClonePrice);
                StoreVerifiedContainerBasePrice(item, emptyClonePrice, "empty clone");
                return emptyClonePrice;
            }

            int donorPrice = TryGetBasePriceFromEmptyIdenticalItem(item);
            if (donorPrice > 0)
            {
                Plugin.LogDebug($"[Gear & Loot Value] BASE from donor {item.ShortName}: {donorPrice}");
                CacheBasePrice(item, donorPrice);
                StoreVerifiedContainerBasePrice(item, donorPrice, "donor");
                return donorPrice;
            }

            int serverPrice = GetServerContainerBasePrice(item);
            if (serverPrice > 0)
            {
                Plugin.LogDebug($"[Gear & Loot Value] BASE from server fallback {item.ShortName}: {serverPrice}");
                return serverPrice;
            }

            int templateFallback = GetTemplateFallbackPrice(item);
            if (templateFallback > 0)
            {
                Plugin.LogDebug($"[Gear & Loot Value] BASE from handbook fallback {item.ShortName}: {templateFallback}");
                return templateFallback;
            }

            return 0;
        }

        // filled bags lie about their base price
        internal static int GetEmptyCloneContainerPrice(Item item)
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
                Plugin.LogDebug($"[Gear & Loot Value] EMPTY CLONE SKIPPED, NOT EXAMINED: {item.ShortName} ({TemplateId(item)})");
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
                        var supply = t.TryGetSupplyData();
                        Plugin.LogDebug($"[Gear & Loot Value] CLONE {item.ShortName} @ {t.LocalizedName}: price={p?.Amount.ToString() ?? "NULL"} supply={supply != null} courses={supply?.CurrencyCourses != null}");
                    }
                }

                return PriceClone(queryItem, item);
            }
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Empty clone pricing failed for {item.ShortName}: {ex.Message}");
                return 0;
            }
        }

        internal static int TryGetBasePriceFromEmptyIdenticalItem(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            string templateId = TemplateId(item);
            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            List<Item> allItems = ScrapeInventoryItems();

            foreach (Item candidate in allItems)
            {
                if (candidate == null || ReferenceEquals(candidate, item))
                {
                    continue;
                }

                string candidateTemplateId = TemplateId(candidate);
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

                int price = PriceSingleItem(candidate);
                if (price > 0)
                {
                    return price;
                }
            }

            return 0;
        }

        internal static int TryGetArmoredRigBasePriceFromEmptyIdenticalItem(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            string templateId = TemplateId(item);
            if (string.IsNullOrEmpty(templateId))
            {
                return 0;
            }

            List<Item> allItems = ScrapeInventoryItems();

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

                string candidateTemplateId = TemplateId(candidate);
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

        internal static int PriceContents(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int total = 0;
            List<Item> contentRoots = OnlyTopLevelItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsContents(item, child))
                    .ToList());

            foreach (Item child in contentRoots)
            {
                int childPrice = PriceContentItem(child);

                total += childPrice;
            }

            return total;
        }

        internal static int PriceContentItem(Item item)
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
                return MainTooltipPrice(item);
            }

            if (IsMagazine(item))
            {
                return GetMagazineTotalSellPrice(item);
            }

            if (NeedsModBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return PriceStack(item);
        }

        internal static int MainTooltipPrice(Item item)
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
                int contentsPrice = PriceContents(item);
                return rigPrice + platesPrice + contentsPrice;
            }

            if (HasHardPlateSlots(item))
            {
                int armorPrice = PriceSingleItem(item);
                int platesPrice = GetArmorPlateTraderPrice(item);
                return armorPrice + platesPrice;
            }

            if (IsRealContainer(item))
            {
                int basePrice = ContainerBasePrice(item);
                int contentsPrice = PriceContents(item);
                return basePrice + contentsPrice;
            }

            if (NeedsModBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return PriceSingleItem(item);
        }

    }
}