using EFT;
using EFT.UI.SessionEnd;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace AvgSellPrice
{
    internal class PlayerItemAddedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Player"), "OnItemAdded");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            if (!ValueTracker.IsInRaid)
            {
                return;
            }

            ValueTracker.RefreshRaidLootValue();
            ValueDisplayUI.RequestRefresh();
        }
    }

    internal class PlayerItemRemovedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Player"), "OnItemRemoved");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            if (!ValueTracker.IsInRaid)
            {
                return;
            }

            ValueTracker.RefreshRaidLootValue();
            ValueDisplayUI.RequestRefresh();
        }
    }

    internal class RaidStartPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod(
                "OnGameStarted",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueTracker.BeginRaid();
            ValueDisplayUI.RequestRefresh();
        }
    }

    internal class RaidEndPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SessionResultExitStatus).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueTracker.EndRaid();
            ValueDisplayUI.RequestRefresh();
        }
    }
}
