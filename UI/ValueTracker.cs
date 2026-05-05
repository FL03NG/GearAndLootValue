using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GearAndLootValue
{
    internal static class ValueTracker
    {
        private static readonly HashSet<string> BaselineItemIds =
            new HashSet<string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, LootValueEntry> CountedLootValuesByItemId =
            new Dictionary<string, LootValueEntry>(StringComparer.Ordinal);

        public static int CurrentRaidLootValue { get; private set; }
        public static int LastRaidLootValue { get; private set; }

        public static bool IsInRaid { get; private set; }
        public static bool BaselineWarmupActive { get; private set; }

        public static void BeginRaid()
        {
            IsInRaid = true;
            BaselineWarmupActive = true;
            CurrentRaidLootValue = 0;
            LastRaidLootValue = 0;
            CountedLootValuesByItemId.Clear();

            CaptureBaselineInventory();
        }

        public static void EndRaid()
        {
            LastRaidLootValue = CurrentRaidLootValue;
            IsInRaid = false;
            BaselineWarmupActive = false;
            CurrentRaidLootValue = 0;
            BaselineItemIds.Clear();
            CountedLootValuesByItemId.Clear();
        }

        public static void WarmupBaselineFromCurrentInventory()
        {
            if (!IsInRaid || !BaselineWarmupActive)
            {
                return;
            }

            foreach (Item item in InventoryScraper.ScrapeInventoryItems())
            {
                if (item == null || string.IsNullOrEmpty(item.Id))
                {
                    continue;
                }

                BaselineItemIds.Add(item.Id);
            }

            CountedLootValuesByItemId.Clear();
            CurrentRaidLootValue = 0;
        }

        public static void EndBaselineWarmup()
        {
            BaselineWarmupActive = false;
        }

        public static void HandleItemAdded(Item rootItem)
        {
            if (!ShouldTrackRaidLoot() || rootItem == null)
            {
                return;
            }

            AddLootItemTree(rootItem);
        }

        public static void HandleItemRemoved(Item rootItem)
        {
            if (!ShouldTrackRaidLoot() || rootItem == null)
            {
                return;
            }

            RemoveLootItemTree(rootItem);
        }

        public static void RebuildRaidLootValueFromInventory()
        {
            if (!ShouldTrackRaidLoot())
            {
                return;
            }

            CountedLootValuesByItemId.Clear();
            CurrentRaidLootValue = 0;

            List<Item> currentItems = InventoryScraper.ScrapeInventoryItems();
            if (currentItems.Count == 0)
            {
                return;
            }

            List<Item> newItems = currentItems
                .Where(item => item != null)
                .Where(item => !string.IsNullOrEmpty(item.Id))
                .Where(item => !BaselineItemIds.Contains(item.Id))
                .ToList();

            foreach (Item rootItem in PmcGearValue.OnlyTopLevelItems(newItems))
            {
                if (rootItem == null || string.IsNullOrEmpty(rootItem.Id))
                {
                    continue;
                }

                int value = PmcGearValue.RaidLootRootValue(rootItem);
                MarkLootItemTree(rootItem, rootItem.Id, value);
                CurrentRaidLootValue = Math.Max(0, CurrentRaidLootValue + Math.Max(0, value));
            }
        }

        private static void AddLootItemTree(Item rootItem)
        {
            if (rootItem == null)
            {
                return;
            }

            string rootId = rootItem.Id;
            if (string.IsNullOrEmpty(rootId) ||
                BaselineItemIds.Contains(rootId) ||
                CountedLootValuesByItemId.ContainsKey(rootId))
            {
                return;
            }

            int value = PmcGearValue.RaidLootRootValue(rootItem);
            MarkLootItemTree(rootItem, rootId, value);
            CurrentRaidLootValue = Math.Max(0, CurrentRaidLootValue + Math.Max(0, value));
        }

        private static void RemoveLootItemTree(Item rootItem)
        {
            if (rootItem == null)
            {
                return;
            }

            foreach (Item item in PmcGearValue.ItemTree(rootItem))
            {
                if (item == null || string.IsNullOrEmpty(item.Id))
                {
                    continue;
                }

                LootValueEntry entry;
                if (CountedLootValuesByItemId.TryGetValue(item.Id, out entry))
                {
                    CurrentRaidLootValue = Math.Max(0, CurrentRaidLootValue - entry.Value);
                    CountedLootValuesByItemId.Remove(item.Id);
                }
            }
        }

        private static void MarkLootItemTree(Item rootItem, string ownerId, int ownerValue)
        {
            foreach (Item item in PmcGearValue.ItemTree(rootItem))
            {
                if (item == null || string.IsNullOrEmpty(item.Id))
                {
                    continue;
                }

                if (BaselineItemIds.Contains(item.Id))
                {
                    continue;
                }

                CountedLootValuesByItemId[item.Id] = new LootValueEntry(
                    string.Equals(item.Id, ownerId, StringComparison.Ordinal) ? Math.Max(0, ownerValue) : 0);
            }
        }

        private static bool ShouldTrackRaidLoot()
        {
            return IsInRaid &&
                   (PluginConfig.EnableValueDisplay == null || PluginConfig.EnableValueDisplay.Value) &&
                   (PluginConfig.ShowRaidLootValue == null || PluginConfig.ShowRaidLootValue.Value);
        }

        private static void CaptureBaselineInventory()
        {
            BaselineItemIds.Clear();

            foreach (Item item in InventoryScraper.ScrapeInventoryItems())
            {
                if (item == null || string.IsNullOrEmpty(item.Id))
                {
                    continue;
                }

                BaselineItemIds.Add(item.Id);
            }
        }

        private sealed class LootValueEntry
        {
            public LootValueEntry(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}