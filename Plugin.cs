using BepInEx;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Patching;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AvgSellPrice
{
    [BepInPlugin("com.simon.approxsellprice", "Approx Sell Price", "3.5.1")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            PluginConfig.Init(Config);

            new TraderPatch().Enable();
            new ItemPatch().Enable();
            new AmmoItemPatch().Enable();
            new ThrowWeapItemPatch().Enable();

            // GridItemViewTooltipPatch is intentionally NOT enabled.
            // The price is shown through the item attribute system instead.

            StartCoroutine(DelayedPriceRefresh());
        }

        private IEnumerator DelayedPriceRefresh()
        {
            yield return new WaitForSeconds(5f);
            RefreshAllItems();

            yield return new WaitForSeconds(8f);
            ItemExtensions.ClearPriceCacheSafe();
            RefreshAllItems();
        }

        private static void RefreshAllItems()
        {
            List<Item> allItems = ItemExtensions.GetAllPlayerItemsSafe();

            foreach (Item item in allItems)
            {
                item.AddApproxSellPriceAttribute();
            }
        }
    }

    internal class TraderPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(TraderClass).GetConstructors()[0];
        }

        [PatchPostfix]
        private static void PatchPostfix(ref TraderClass __instance)
        {
            __instance.UpdateSupplyDataSafe();
        }
    }

    internal class ItemPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Item).GetConstructors()[0];
        }

        [PatchPostfix]
        private static void PatchPostfix(ref Item __instance)
        {
            __instance.AddApproxSellPriceAttribute();
        }
    }

    internal class AmmoItemPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(AmmoItemClass).GetConstructors()[0];
        }

        [PatchPostfix]
        private static void PatchPostfix(ref AmmoItemClass __instance)
        {
            __instance.AddApproxSellPriceAttribute();
        }
    }

    internal class ThrowWeapItemPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(ThrowWeapItemClass).GetConstructors()[0];
        }

        [PatchPostfix]
        private static void PatchPostfix(ref ThrowWeapItemClass __instance)
        {
            __instance.AddApproxSellPriceAttribute();
        }
    }
}