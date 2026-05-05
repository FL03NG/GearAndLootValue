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
using static GearAndLootValue.ContainerPricing;
using static GearAndLootValue.EftReflection;
namespace GearAndLootValue
{
    internal static class ArmorPricing
    {
        internal static bool IsHardPlateSlotName(string slotName)
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

        internal static bool IsArmorSlotName(string slotName)
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

        internal static bool IsHelmetArmorSlotName(string lowerSlotName)
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

        internal static bool IsItemInHardPlateSlot(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return FindSlotName(item, out string slotName) &&
                   IsHardPlateSlotName(slotName);
        }

        internal static bool IsItemInAnyArmorSlot(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return FindSlotName(item, out string slotName) &&
                   IsArmorSlotName(slotName);
        }

        internal static bool IsItemInBuiltInSlot(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return FindSlotFromAddress(item, out Slot slot) &&
                   IsBuiltInSlot(slot);
        }

        internal static bool TryGetLockedFromSlotProps(Slot slot, out bool locked)
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
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Slot lock check failed: {ex.Message}");
            }

            return false;
        }

        internal static bool IsBuiltInSlot(Slot slot)
        {
            if (slot == null)
            {
                return false;
            }

            bool required;
            bool locked;

            bool hasRequired = ReadSlotBool(slot, "_required", out required);
            bool hasLocked = TryGetLockedFromSlotProps(slot, out locked);

            return (hasRequired && required) || (hasLocked && locked);
        }

        internal static bool IsBuiltInArmorSlot(Slot slot)
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

        // plates are not loot
        internal static bool ShouldCountAsContents(Item parent, Item child)
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

        internal static int GetArmorPlateTraderPrice(Item item)
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

                    TraderOffer plateOffer = PickOffer(slot.ContainedItem);
                    int platePrice = plateOffer != null ? plateOffer.Price : 0;

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

                TraderOffer plateOffer = PickOffer(child);
                int platePrice = plateOffer != null ? plateOffer.Price : 0;

                if (platePrice <= 0)
                {
                    platePrice = GetTemplateFallbackPrice(child);
                }

                total += platePrice;
            }

            return total;
        }

        internal static bool HasHardPlateSlots(Item item)
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

        internal static int GetArmoredRigBasePrice(Item item)
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
                    Plugin.LogDebug($"[Gear & Loot Value] ARMORED RIG base from flea price {item.ShortName}: {fleaPrice}");
                    return fleaPrice;
                }
            }

            string templateId = TemplateId(item);

            int liveOfferPrice = GetArmoredRigBasePriceFromLiveOffer(item);
            if (IsPlausibleArmoredRigBasePrice(item, liveOfferPrice))
            {
                if (!string.IsNullOrEmpty(templateId))
                {
                    VerifiedArmoredRigPriceCache.StorePrice(templateId, liveOfferPrice);
                }

                Plugin.LogDebug($"[Gear & Loot Value] ARMORED RIG base from live offer {item.ShortName}: {liveOfferPrice}");
                return liveOfferPrice;
            }

            int donorPrice = TryGetArmoredRigBasePriceFromEmptyIdenticalItem(item);
            if (IsPlausibleArmoredRigBasePrice(item, donorPrice))
            {
                if (!string.IsNullOrEmpty(templateId))
                {
                    VerifiedArmoredRigPriceCache.StorePrice(templateId, donorPrice);
                }

                Plugin.LogDebug($"[Gear & Loot Value] ARMORED RIG base from empty donor {item.ShortName}: {donorPrice}");
                return donorPrice;
            }

            if (!string.IsNullOrEmpty(templateId))
            {
                int verifiedRigPrice = VerifiedArmoredRigPriceCache.GetPrice(templateId);
                if (IsPlausibleArmoredRigBasePrice(item, verifiedRigPrice))
                {
                    Plugin.LogDebug($"[Gear & Loot Value] ARMORED RIG base from verified cache {item.ShortName}: {verifiedRigPrice}");
                    return verifiedRigPrice;
                }
            }

            if (!string.IsNullOrEmpty(templateId))
            {
                int serverPrice = TraderPriceCache.GetPrice(templateId);
                if (serverPrice > 0)
                {
                    Plugin.LogDebug($"[Gear & Loot Value] ARMORED RIG base from server price {item.ShortName}: {serverPrice}");
                    return serverPrice;
                }
            }

            int fallback = GetTemplateFallbackPrice(item);
            if (fallback > 0)
            {
                Plugin.LogDebug($"[Gear & Loot Value] ARMORED RIG base from handbook fallback {item.ShortName}: {fallback}");
                return fallback;
            }

            return 0;
        }

        internal static TraderOffer GetArmoredRigTraderOffer(Item item)
        {
            if (item == null)
            {
                return null;
            }

            if (UseFleaPriceSource)
            {
                return GetFleaPriceOffer(item);
            }

            TraderOffer liveOffer = PickOffer(item);
            if (liveOffer != null && liveOffer.Price > 0)
            {
                return liveOffer;
            }

            Plugin.LogDebug($"[Gear & Loot Value] ARMORED RIG has no trader price: {item.ShortName}");
            return null;
        }

        internal static bool IsPlausibleArmoredRigBasePrice(Item item, int price)
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

        internal static int GetArmoredRigBasePriceFromLiveOffer(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            TraderOffer offer = PickOffer(item);
            if (offer == null || offer.Price <= 0)
            {
                return 0;
            }

            int platesPrice = GetArmorPlateTraderPrice(item);
            int contentsPrice = PriceContents(item);
            return Math.Max(0, offer.Price - platesPrice - contentsPrice);
        }

        internal static bool IsArmoredRig(Item item)
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
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Plate slot check failed for {item.ShortName}: {ex.Message}");
            }

            return false;
        }

    }
}