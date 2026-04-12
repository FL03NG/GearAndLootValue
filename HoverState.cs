using EFT.InventoryLogic;

namespace AvgSellPrice
{
    internal static class HoverState
    {
        public static Item HoveredItem;
        public static bool IsGridItemHovered;

        public static void BeginHover(Item item)
        {
            HoveredItem = item;
            IsGridItemHovered = item != null;
        }

        public static void EndHover()
        {
            IsGridItemHovered = false;
            HoveredItem = null;
        }
    }
}
