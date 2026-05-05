using BepInEx;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Patching;
using System.Reflection;
using UnityEngine;

namespace GearAndLootValue
{
    [BepInPlugin("com.fl03ng.gearandlootvalue", "Gear & Loot Value", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static BepInEx.Logging.ManualLogSource Log;

        internal static void LogDebug(string message)
        {
            if (PluginConfig.DebugLogging != null && PluginConfig.DebugLogging.Value)
            {
                Log?.LogInfo(message);
            }
        }

        private static void EnablePatch(ModulePatch patch, string name)
        {
            try
            {
                patch.Enable();
                Log.LogInfo(name + " enabled");
            }
            catch (System.Exception ex)
            {
                Log.LogError(name + " failed: " + ex);
            }
        }

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo("Gear & Loot Value Awake START");

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

            WeaponDefaultPresetCache.Load();
            Log.LogInfo("WeaponDefaultPresetCache.Load called");

            EnablePatch(new TraderPatch(), "TraderPatch");
            EnablePatch(new ItemPatch(), "ItemPatch");
            EnablePatch(new AmmoItemPatch(), "AmmoItemPatch");
            EnablePatch(new ThrowWeapItemPatch(), "ThrowWeapItemPatch");
            EnablePatch(new GridItemOnPointerEnterPatch(), "GridItemOnPointerEnterPatch");
            EnablePatch(new GridItemOnPointerExitPatch(), "GridItemOnPointerExitPatch");
            EnablePatch(new SimpleTooltipShowPatch(), "SimpleTooltipShowPatch");
            EnablePatch(new PlayerItemAddedPatch(), "PlayerItemAddedPatch");
            EnablePatch(new PlayerItemRemovedPatch(), "PlayerItemRemovedPatch");
            EnablePatch(new RaidStartPatch(), "RaidStartPatch");
            EnablePatch(new RaidEndPatch(), "RaidEndPatch");
            EnablePatch(new ContainersPanelShowPatch(), "ContainersPanelShowPatch");
            EnablePatch(new EquipmentTabShowPatch(), "EquipmentTabShowPatch");
            EnablePatch(new InventoryScreenShowPatch(), "InventoryScreenShowPatch");
            EnablePatch(new EquipmentTabHidePatch(), "EquipmentTabHidePatch");
            EnablePatch(new InventoryScreenClosePatch(), "InventoryScreenClosePatch");
            EnablePatch(new InventoryScreenTabSwitchPatch(), "InventoryScreenTabSwitchPatch");
            EnablePatch(new InventoryTabPointerClickPatch(), "InventoryTabPointerClickPatch");
            EnablePatch(new OverallScreenShowPatch(), "OverallScreenShowPatch");
            EnablePatch(new SkillsScreenShowPatch(), "SkillsScreenShowPatch");
            EnablePatch(new CharacterHealthPanelShowPatch(), "CharacterHealthPanelShowPatch");
            EnablePatch(new CharacterHealthPanelAnimatedShowPatch(), "CharacterHealthPanelAnimatedShowPatch");
            EnablePatch(new TextGameMapPanelShowPatch(), "TextGameMapPanelShowPatch");
            EnablePatch(new TasksScreenShowPatch(), "TasksScreenShowPatch");
            EnablePatch(new AchievementsScreenShowPatch(), "AchievementsScreenShowPatch");
            EnablePatch(new PrestigeScreenShowPatch(), "PrestigeScreenShowPatch");
            EnablePatch(new InventoryControllerRaiseAddEventPatch(), "InventoryControllerRaiseAddEventPatch");
            EnablePatch(new InventoryControllerRaiseRemoveEventPatch(), "InventoryControllerRaiseRemoveEventPatch");
            EnablePatch(new RaidEndSummaryShowPatch(), "RaidEndSummaryShowPatch");
            EnablePatch(new RaidEndExperienceShowPatch(), "RaidEndExperienceShowPatch");
            EnablePatch(new RaidEndKillListShowPatch(), "RaidEndKillListShowPatch");
            EnablePatch(new RaidEndStatisticsShowPatch(), "RaidEndStatisticsShowPatch");
            EnablePatch(new RaidEndHealthTreatmentShowPatch(), "RaidEndHealthTreatmentShowPatch");
            EnablePatch(new MatchmakerInsuranceScreenShowPatch(), "MatchmakerInsuranceScreenShowPatch");
            EnablePatch(new MatchmakerInsuranceScreenClosePatch(), "MatchmakerInsuranceScreenClosePatch");

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
            __instance.RefreshTraderSupplyData();
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
