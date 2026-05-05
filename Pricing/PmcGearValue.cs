using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static GearAndLootValue.TarkovItemPrices;
using static GearAndLootValue.InventoryScraper;
using static GearAndLootValue.ArmorPricing;
using static GearAndLootValue.ContainerPricing;
using static GearAndLootValue.WeaponPricing;
using static GearAndLootValue.EftReflection;
namespace GearAndLootValue
{
    internal static class PmcGearValue
    {
        public static int GetPlayerEquipmentValue()
        {
            if (Session == null || Session.Profile == null || Session.Profile.Inventory == null)
            {
                return 0;
            }

            PriceSource? previousPriceSource = _priceSourceOverride;
            _priceSourceOverride = PluginConfig.EquipmentValuePriceSource != null
                ? PluginConfig.EquipmentValuePriceSource.Value
                : ActiveSource();

            try
            {
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
            finally
            {
                _priceSourceOverride = previousPriceSource;
            }
        }

        internal static List<Item> OnlyTopLevelItems(IEnumerable<Item> items)
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

        internal static List<Item> GetEquippedRootItems()
        {
            object inventory = Session?.Profile?.Inventory;
            if (inventory == null)
            {
                return new List<Item>();
            }

            List<Item> equippedItems = new List<Item>();

            object equipmentRoot = FindGearRoot(inventory);
            PullSlotItems(equipmentRoot, equippedItems);

            if (equippedItems.Count > 0)
            {
                return OnlyTopLevelItems(equippedItems);
            }

            return OnlyTopLevelItems(ScrapeInventoryItems())
                .Where(item =>
                {
                    string slotName = GetCurrentSlotName(item);
                    return !string.IsNullOrEmpty(slotName) && _pmcGearSlots.Contains(slotName);
                })
                .ToList();
        }

        internal static string GetCurrentSlotName(Item item)
        {
            if (item == null)
            {
                return null;
            }

            return FindSlotName(item, out string slotName)
                ? slotName
                : null;
        }

        internal static int GetEquipmentValueItemPrice(Item item)
        {
            if (item == null ||
                ShouldSkipEquipmentValueItem(item) ||
                !CountInValuePanels(item))
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
                int contentsPrice = PriceGearContents(item);
                return rigPrice + platesPrice + contentsPrice;
            }

            if (IsRealContainer(item))
            {
                int basePrice = ContainerBasePrice(item);
                int contentsPrice = PriceGearContents(item);
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

            if (item is Weapon w)
            {
                return GetWeaponTotalSellValue(w);
            }

            if (NeedsModBreakdown(item))
            {
                return GetModAttachmentRootTotalSellValue(item);
            }

            return PriceSingleItem(item);
        }

        internal static int PriceGearContents(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int total = 0;
            List<Item> contentRoots = OnlyTopLevelItems(
                item.GetAllItems()
                    .Where(child => ShouldCountAsEquipmentValueContents(item, child))
                    .ToList());

            foreach (Item child in contentRoots)
            {
                total += PriceGearContent(child);
            }

            return total;
        }

        internal static int PriceGearContent(Item item)
        {
            if (item == null || IsKeyItem(item) || IsEquipmentValueCase(item))
            {
                return 0;
            }

            if (item is Weapon w)
            {
                return GetWeaponTotalSellValue(w);
            }

            if (IsRealContainer(item) || IsArmoredRig(item))
            {
                return GetEquipmentValueItemPrice(item);
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

        internal static bool ShouldCountAsEquipmentValueContents(Item parent, Item child)
        {
            return ShouldCountAsContents(parent, child) &&
                   !IsKeyItem(child) &&
                   !IsEquipmentValueCase(child);
        }

        internal static int RaidLootRootValue(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            if (!CountInValuePanels(item))
            {
                return 0;
            }

            PriceSource? previousPriceSource = _priceSourceOverride;
            _priceSourceOverride = PluginConfig.RaidLootValuePriceSource != null
                ? PluginConfig.RaidLootValuePriceSource.Value
                : ActiveSource();

            try
            {
                return GetTotalSellValue(item);
            }
            finally
            {
                _priceSourceOverride = previousPriceSource;
            }
        }

        internal static bool HasValuePanelContents(Item item)
        {
            return item != null && (IsRealContainer(item) || IsArmoredRig(item));
        }

        internal static bool NeedsLootRecountOnChange(Item item)
        {
            return item != null && (HasValuePanelContents(item) || item is MoneyItemClass);
        }

        internal static List<Item> ItemTree(Item item)
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
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Item tree read failed for {item.ShortName}: {ex.Message}");
                return new List<Item> { item };
            }
        }

        internal static bool ShouldSkipEquipmentValueItem(Item item)
        {
            string slotName = GetCurrentSlotName(item);
            return string.Equals(slotName, "Scabbard", StringComparison.OrdinalIgnoreCase) ||
                   IsKeyItem(item) ||
                   IsEquipmentValueCase(item);
        }

        internal static bool IsKeyItem(Item item)
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

        internal static bool IsEquipmentValueCase(Item item)
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

        internal static bool ContainsCaseName(string value)
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

        internal static bool CountInValuePanels(Item item)
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

        internal static bool IsChamberSlotItem(Item item)
        {
            if (!FindSlotName(item, out string slotName) || string.IsNullOrEmpty(slotName))
            {
                return false;
            }

            string lower = slotName.ToLowerInvariant();
            return lower.Contains("chamber") ||
                   lower.Contains("patron_in_weapon") ||
                   lower.Contains("cartridge");
        }

        internal static object FindGearRoot(object inventory)
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

        internal static void PullSlotItems(object root, List<Item> equippedItems)
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

        internal static void AddItemsFromSlotEnumerable(IEnumerable slots, List<Item> equippedItems)
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

                if (!string.IsNullOrEmpty(slot.Name) && _pmcGearSlots.Contains(slot.Name))
                {
                    equippedItems.Add(slot.ContainedItem);
                }
            }
        }

    }
}
