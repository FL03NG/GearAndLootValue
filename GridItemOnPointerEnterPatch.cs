using EFT.UI.DragAndDrop;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine.EventSystems;

namespace AvgSellPrice
{
    internal class GridItemOnPointerEnterPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GridItemView).GetMethod(
                "OnPointerEnter",
                BindingFlags.Instance | BindingFlags.Public
            );
        }

        [PatchPrefix]
        private static void PatchPrefix(GridItemView __instance, PointerEventData eventData)
        {
            if (__instance != null && __instance.Item != null)
            {
                HoverState.BeginHover(__instance.Item);
            }
        }
    }
}
