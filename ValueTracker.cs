using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AvgSellPrice
{
    internal static class ValueTracker
    {
        private static readonly HashSet<string> BaselineItemIds =
            new HashSet<string>(StringComparer.Ordinal);

        private static readonly Dictionary<string, LootValueEntry> CountedLootValuesByItemId =
            new Dictionary<string, LootValueEntry>(StringComparer.Ordinal);

        public static int CurrentRaidLootValue { get; private set; }

        public static bool IsInRaid { get; private set; }

        public static void BeginRaid()
        {
            IsInRaid = true;
            CurrentRaidLootValue = 0;
            CountedLootValuesByItemId.Clear();

            CaptureBaselineInventory();
        }

        public static void EndRaid()
        {
            IsInRaid = false;
            CurrentRaidLootValue = 0;
            BaselineItemIds.Clear();
            CountedLootValuesByItemId.Clear();
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

        public static void ReconcileItemTree(Item rootItem)
        {
            if (!ShouldTrackRaidLoot() || rootItem == null)
            {
                return;
            }

            RemoveLootItemTree(rootItem);
            AddLootItemTree(rootItem);
        }

        public static void RebuildRaidLootValueFromInventory()
        {
            if (!ShouldTrackRaidLoot())
            {
                return;
            }

            CountedLootValuesByItemId.Clear();
            CurrentRaidLootValue = 0;

            List<Item> currentItems = ItemExtensions.GetAllPlayerItemsSafe();
            if (currentItems.Count == 0)
            {
                return;
            }

            List<Item> newItems = currentItems
                .Where(item => item != null)
                .Where(item => !string.IsNullOrEmpty(item.Id))
                .Where(item => !BaselineItemIds.Contains(item.Id))
                .ToList();

            foreach (Item rootItem in ItemExtensions.GetRootItems(newItems))
            {
                if (rootItem == null || string.IsNullOrEmpty(rootItem.Id))
                {
                    continue;
                }

                int value = ItemExtensions.GetConfiguredRaidLootRootValue(rootItem);
                MarkLootItemTree(rootItem, rootItem.Id, value);
                CurrentRaidLootValue = Math.Max(0, CurrentRaidLootValue + Math.Max(0, value));
            }
        }

        public static void RefreshRaidLootValue()
        {
            if (!IsInRaid)
            {
                CurrentRaidLootValue = 0;
                return;
            }

            int total = 0;
            foreach (LootValueEntry entry in CountedLootValuesByItemId.Values)
            {
                total += entry.Value;
            }

            CurrentRaidLootValue = Math.Max(0, total);
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

            int value = ItemExtensions.GetConfiguredRaidLootRootValue(rootItem);
            MarkLootItemTree(rootItem, rootId, value);
            CurrentRaidLootValue = Math.Max(0, CurrentRaidLootValue + Math.Max(0, value));
        }

        private static void RemoveLootItemTree(Item rootItem)
        {
            if (rootItem == null)
            {
                return;
            }

            foreach (Item item in ItemExtensions.GetItemTreeSafe(rootItem))
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
            foreach (Item item in ItemExtensions.GetItemTreeSafe(rootItem))
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
                   (PluginConfig.ShowRaidLootValue == null || PluginConfig.ShowRaidLootValue.Value);
        }

        private static void CaptureBaselineInventory()
        {
            BaselineItemIds.Clear();

            foreach (Item item in ItemExtensions.GetAllPlayerItemsSafe())
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
