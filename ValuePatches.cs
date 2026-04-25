using EFT;
using EFT.UI;
using EFT.UI.SessionEnd;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace AvgSellPrice
{
    internal static class RaidPlayerState
    {
        public static Player MainPlayer { get; set; }
    }

    internal class PlayerItemAddedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Player"), "OnItemAdded");
        }

        [PatchPostfix]
        private static void PatchPostfix(object __instance, GEventArgs1 eventArgs)
        {
            if (!ValueTracker.IsInRaid || !ReferenceEquals(__instance, RaidPlayerState.MainPlayer))
            {
                return;
            }

            ValueTracker.HandleItemAdded(eventArgs?.Item);
            ValueDisplayUI.RequestRaidItemReconcile(eventArgs?.Item);
            ValueDisplayUI.RequestRaidValueTextRefresh();
        }
    }

    internal class PlayerItemRemovedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Player"), "OnItemRemoved");
        }

        [PatchPostfix]
        private static void PatchPostfix(object __instance, GEventArgs3 eventArgs)
        {
            if (!ValueTracker.IsInRaid || !ReferenceEquals(__instance, RaidPlayerState.MainPlayer))
            {
                return;
            }

            ValueTracker.HandleItemRemoved(eventArgs?.Item);
            ValueDisplayUI.RequestRaidValueTextRefresh();
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
        private static void PatchPostfix(GameWorld __instance)
        {
            RaidPlayerState.MainPlayer = __instance?.MainPlayer;
            ValueTracker.BeginRaid();
            ValueDisplayUI.RequestRefresh();
            ValueDisplayUI.RequestRaidLabelCreate(1f);
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
            RaidPlayerState.MainPlayer = null;
            ValueTracker.EndRaid();
            ValueDisplayUI.RequestRefresh();
        }
    }

    internal class ContainersPanelShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ContainersPanel), "Show");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.SetRaidInventoryVisible(true);
        }
    }
}
