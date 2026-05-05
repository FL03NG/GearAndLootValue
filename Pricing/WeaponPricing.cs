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
using static GearAndLootValue.ArmorPricing;
using static GearAndLootValue.ContainerPricing;
using static GearAndLootValue.EftReflection;
namespace GearAndLootValue
{
    internal static class WeaponPricing
    {
        internal static int PriceSingleItem(Item item)
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

            TraderOffer offer = PickOffer(item);

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

        internal static int PriceStack(Item item)
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

            int unitPrice = PriceSingleItem(item);
            if (unitPrice <= 0)
            {
                return 0;
            }

            int stackCount = item.StackObjectsCount > 1 ? item.StackObjectsCount : 1;
            return unitPrice * stackCount;
        }

        internal static int GetMoneyStackValue(Item item)
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

        internal static float GetMoneyRoubleRate(Item item)
        {
            string templateId = TemplateId(item);
            if (string.Equals(templateId, TarkovMoney.RoubleTpl, StringComparison.Ordinal))
            {
                return 1f;
            }

            object template = item?.Template;
            object rawRate = FindMemberValue(template, "rate", "Rate");
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

            if (string.Equals(templateId, TarkovMoney.DollarTpl, StringComparison.Ordinal))
            {
                return TarkovMoney.DefaultDollarRate;
            }

            if (string.Equals(templateId, TarkovMoney.EuroTpl, StringComparison.Ordinal))
            {
                return TarkovMoney.DefaultEuroRate;
            }

            return 0f;
        }

        internal static bool IsMagazine(Item item)
        {
            return item is MagazineItemClass;
        }

        internal static int PriceLoadedAmmo(Item item)
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

                total += PriceStack(child);
            }

            return total;
        }

        internal static bool NeedsModBreakdown(Item item)
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

        internal static int GetModAttachmentPrice(Item item)
        {
            if (!NeedsModBreakdown(item))
            {
                return 0;
            }

            int total = 0;

            List<Item> attachmentRoots = OnlyTopLevelItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsModAttachment(item, child))
                    .ToList());

            foreach (Item child in attachmentRoots)
            {
                total += GetAttachmentRootTotalSellPrice(child);
            }

            return total;
        }

        internal static int GetAttachmentRootTotalSellPrice(Item item)
        {
            int total = PriceStack(item);

            if (!UseFleaPriceSource || item == null)
            {
                return total;
            }

            if (item is Weapon)
            {
                return total + PriceWeaponMods(item);
            }

            if (NeedsModBreakdown(item))
            {
                return total + GetModAttachmentPrice(item);
            }

            return total;
        }

        internal static int GetWeaponTotalSellValue(Item item)
        {
            int total = PriceStack(item);

            if (UseFleaPriceSource)
            {
                return total + PriceWeaponMods(item) + GetWeaponMagazineTraderPrice(item);
            }

            return total;
        }

        internal static int GetModAttachmentRootTotalSellValue(Item item)
        {
            int total = PriceStack(item);

            if (UseFleaPriceSource && NeedsModBreakdown(item))
            {
                return total + GetModAttachmentPrice(item);
            }

            return total;
        }

        internal static bool ShouldCountAsModAttachment(Item parent, Item child)
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

        internal static int PriceWeaponMods(Item item)
        {
            if (!(item is Weapon))
            {
                return 0;
            }

            int total = 0;

            List<Item> attachmentRoots = OnlyTopLevelItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsWeaponAttachment(item, child))
                    .ToList());

            foreach (Item child in attachmentRoots)
            {
                total += GetAttachmentRootTotalSellPrice(child);
            }

            return total;
        }

        internal static int GetWeaponMagazineTraderPrice(Item item)
        {
            if (!(item is Weapon))
            {
                return 0;
            }

            int total = 0;
            List<Item> magazineRoots = OnlyTopLevelItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsWeaponMagazine(item, child))
                    .ToList());

            foreach (Item child in magazineRoots)
            {
                total += GetMagazineTotalSellPrice(child);
            }

            return total;
        }

        internal static int GetMagazineTotalSellPrice(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            TraderOffer totalOffer = PickOffer(item);
            if (!UseFleaPriceSource && totalOffer != null && totalOffer.Price > 0)
            {
                return totalOffer.Price;
            }

            int total = PriceStack(item);
            if (ShowAmmoPrice)
            {
                total += PriceLoadedAmmo(item);
            }

            return total;
        }

        internal static TraderOffer GetWeaponBaseTraderSellOffer(Item item)
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
                Plugin.LogDebug($"[Gear & Loot Value] Weapon base trader query failed for {item.ShortName}: {ex.Message}");
                return null;
            }
        }

        internal static bool ShouldCountAsWeaponAttachment(Item weapon, Item child)
        {
            if (!(weapon is Weapon) || child == null || child == weapon)
            {
                return false;
            }

            return !(child is AmmoItemClass) &&
                   IsRemovableWeaponAttachment(child);
        }

        internal static bool ShouldCountAsWeaponMagazine(Item weapon, Item child)
        {
            if (!(weapon is Weapon) || child == null || child == weapon)
            {
                return false;
            }

            return IsWeaponMagazine(child);
        }

        internal static bool IsWeaponMagazine(Item item)
        {
            if (!IsMagazine(item))
            {
                return false;
            }

            return FindSlotName(item, out string slotName) &&
                   IsWeaponMagazineSlot(slotName);
        }

        internal static bool IsWeaponMagazineSlot(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
            {
                return false;
            }

            return slotName.IndexOf("magazine", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsRemovableWeaponAttachment(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (!FindSlotName(item, out string slotName) ||
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

        internal static bool IsRemovableWeaponAttachmentSlot(string slotName)
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

        internal static bool TryReadTemplateBool(Item item, string memberName, out bool value)
        {
            value = false;

            if (item == null || item.Template == null)
            {
                return false;
            }

            if (ReadBoolFlag(item.Template, memberName, out value))
            {
                return true;
            }

            object props = FindMemberValue(item.Template, "Props", "_props");
            return props != null && ReadBoolFlag(props, memberName, out value);
        }

        internal static Item BuildWeaponBaseTraderQueryItem(Item item)
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

        internal static void StripRemovableWeaponAttachments(CompoundItem compound)
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
                catch (Exception ex)
                {
                    Plugin.LogDebug($"[Gear & Loot Value] Weapon attachment strip skipped a slot: {ex.Message}");
                }
            }
        }

        internal static void ClearCompoundGrids(CompoundItem compound)
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

        // ugly, but EFT has no clean public hook here
        internal static void ClearSlotContainedItem(Slot slot)
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

    }
}