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

        public static int CurrentRaidLootValue { get; private set; }

        public static bool IsInRaid { get; private set; }

        public static void BeginRaid()
        {
            IsInRaid = true;
            CurrentRaidLootValue = 0;

            CaptureBaselineInventory();
            RefreshRaidLootValue();
        }

        public static void EndRaid()
        {
            IsInRaid = false;
            CurrentRaidLootValue = 0;
            BaselineItemIds.Clear();
        }

        public static void RefreshRaidLootValue()
        {
            if (!IsInRaid)
            {
                CurrentRaidLootValue = 0;
                return;
            }

            List<Item> currentItems = ItemExtensions.GetAllPlayerItemsSafe();
            if (currentItems.Count == 0)
            {
                CurrentRaidLootValue = 0;
                return;
            }

            List<Item> newItems = currentItems
                .Where(item => item != null)
                .Where(item => !string.IsNullOrEmpty(item.Id))
                .Where(item => !BaselineItemIds.Contains(item.Id))
                .ToList();

            if (newItems.Count == 0)
            {
                CurrentRaidLootValue = 0;
                return;
            }

            int total = 0;

            foreach (Item rootItem in ItemExtensions.GetRootItems(newItems))
            {
                total += ItemExtensions.GetTotalSellValue(rootItem);
            }

            CurrentRaidLootValue = Math.Max(0, total);
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
    }
}
