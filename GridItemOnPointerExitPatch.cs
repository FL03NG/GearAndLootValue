using EFT.UI.DragAndDrop;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine.EventSystems;

namespace AvgSellPrice
{
    internal class GridItemOnPointerExitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GridItemView).GetMethod(
                "OnPointerExit",
                BindingFlags.Instance | BindingFlags.Public
            );
        }

        [PatchPrefix]
        private static void PatchPrefix(GridItemView __instance, PointerEventData eventData)
        {
            HoverState.IsGridItemHovered = false;
            HoverState.HoveredItem = null;
        }
    }
}