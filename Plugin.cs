using BepInEx;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace AvgSellPrice
{
    [BepInPlugin("com.simon.approxsellprice", "Approx Sell Price", "3.6.1")]
    public class Plugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Approx Sell Price Awake START");

            VerifiedArmoredRigPriceCache.Load();
            Log.LogInfo("VerifiedArmoredRigPriceCache.Load called");

            PluginConfig.Init(Config);
            Log.LogInfo("PluginConfig.Init OK");

            VerifiedContainerPriceCache.Load();
            Log.LogInfo("VerifiedContainerPriceCache.Load called");

            TraderPriceCache.Load();
            Log.LogInfo("TraderPriceCache.Load called");

            FleaPriceCache.Load();
            Log.LogInfo("FleaPriceCache.Load called");

            new TraderPatch().Enable();
            Log.LogInfo("TraderPatch enabled");

            new ItemPatch().Enable();
            Log.LogInfo("ItemPatch enabled");

            new AmmoItemPatch().Enable();
            Log.LogInfo("AmmoItemPatch enabled");

            new ThrowWeapItemPatch().Enable();
            Log.LogInfo("ThrowWeapItemPatch enabled");

            new GridItemOnPointerEnterPatch().Enable();
            Log.LogInfo("GridItemOnPointerEnterPatch enabled");

            new GridItemOnPointerExitPatch().Enable();
            Log.LogInfo("GridItemOnPointerExitPatch enabled");

            new SimpleTooltipShowPatch().Enable();
            Log.LogInfo("SimpleTooltipShowPatch enabled");

            new PlayerItemAddedPatch().Enable();
            Log.LogInfo("PlayerItemAddedPatch enabled");

            new PlayerItemRemovedPatch().Enable();
            Log.LogInfo("PlayerItemRemovedPatch enabled");

            new RaidStartPatch().Enable();
            Log.LogInfo("RaidStartPatch enabled");

            new RaidEndPatch().Enable();
            Log.LogInfo("RaidEndPatch enabled");

            new ContainersPanelShowPatch().Enable();
            Log.LogInfo("ContainersPanelShowPatch enabled");

            new EquipmentTabShowPatch().Enable();
            Log.LogInfo("EquipmentTabShowPatch enabled");

            new EquipmentTabHidePatch().Enable();
            Log.LogInfo("EquipmentTabHidePatch enabled");

            new InventoryScreenClosePatch().Enable();
            Log.LogInfo("InventoryScreenClosePatch enabled");

            new InventoryScreenTabSwitchPatch().Enable();
            Log.LogInfo("InventoryScreenTabSwitchPatch enabled");

            new InventoryTabPointerClickPatch().Enable();
            Log.LogInfo("InventoryTabPointerClickPatch enabled");

            new OverallScreenShowPatch().Enable();
            Log.LogInfo("OverallScreenShowPatch enabled");

            new SkillsScreenShowPatch().Enable();
            Log.LogInfo("SkillsScreenShowPatch enabled");

            new CharacterHealthPanelShowPatch().Enable();
            Log.LogInfo("CharacterHealthPanelShowPatch enabled");

            new CharacterHealthPanelAnimatedShowPatch().Enable();
            Log.LogInfo("CharacterHealthPanelAnimatedShowPatch enabled");

            new TextGameMapPanelShowPatch().Enable();
            Log.LogInfo("TextGameMapPanelShowPatch enabled");

            new TasksScreenShowPatch().Enable();
            Log.LogInfo("TasksScreenShowPatch enabled");

            new AchievementsScreenShowPatch().Enable();
            Log.LogInfo("AchievementsScreenShowPatch enabled");

            new PrestigeScreenShowPatch().Enable();
            Log.LogInfo("PrestigeScreenShowPatch enabled");

            new InventoryControllerRaiseAddEventPatch().Enable();
            Log.LogInfo("InventoryControllerRaiseAddEventPatch enabled");

            new InventoryControllerRaiseRemoveEventPatch().Enable();
            Log.LogInfo("InventoryControllerRaiseRemoveEventPatch enabled");

            if (GetComponent<ValueDisplayUI>() == null)
            {
                gameObject.AddComponent<ValueDisplayUI>();
                Log.LogInfo("ValueDisplayUI created");
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
        private static void PatchPostfix(TraderClass __instance)
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
