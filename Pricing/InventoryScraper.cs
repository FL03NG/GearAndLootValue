using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static GearAndLootValue.TarkovItemPrices;
namespace GearAndLootValue
{
    internal static class InventoryScraper
    {
        // SPT keeps moving inventory fields
        public static List<Item> ScrapeInventoryItems()
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

        internal static void AddItemsFromUnknownValue(object value, HashSet<Item> uniqueItems)
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

        internal static void AddItemsFromUnknownValue(object value, HashSet<string> uniqueIds, List<Item> result)
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

        internal static void AddItemTree(Item rootItem, HashSet<Item> uniqueItems)
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
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Item tree fallback used for {rootItem.ShortName}: {ex.Message}");
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

        internal static bool ItemTreeContains(Item rootItem, Item candidate)
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
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Parent item check failed: {ex.Message}");
            }

            return false;
        }

    }
}