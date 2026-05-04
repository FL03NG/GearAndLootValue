using EFT;
using EFT.UI;
using EFT.UI.SessionEnd;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;

namespace AvgSellPrice
{
    internal static class RaidPlayerState
    {
        public static Player MainPlayer { get; set; }
    }

    internal static class EquipmentValueVisibility
    {
        public static void Hide()
        {
            ValueDisplayUI.SetInventoryVisible(false);
        }

        public static void HideSoon()
        {
            ValueDisplayUI.RequestEquipmentValueHide(0f);
            ValueDisplayUI.RequestEquipmentValueHide(0.05f);
            ValueDisplayUI.RequestEquipmentValueHide(0.2f);
        }

        public static void RefreshSoon()
        {
            ValueDisplayUI.RequestEquipmentValueRefresh(0f);
            ValueDisplayUI.RequestEquipmentValueRefresh(0.1f);
        }

        public static void ProbeSoon()
        {
        }

        public static void ShowSoon()
        {
            ValueDisplayUI.SetInventoryVisible(true);
        }
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
            try
            {
                if (!ValueTracker.IsInRaid)
                {
                    ValueDisplayUI.RequestEquipmentValueRefreshDebounced();
                    return;
                }

                if (!ReferenceEquals(__instance, RaidPlayerState.MainPlayer))
                {
                    return;
                }

                ValueTracker.HandleItemAdded(eventArgs?.Item);
                ValueDisplayUI.RequestRaidItemReconcile(eventArgs?.Item);
                ValueDisplayUI.RequestRaidValueTextRefresh();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[AvgSellPrice] PlayerItemAddedPatch failed: {ex}");
            }
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
            try
            {
                if (!ValueTracker.IsInRaid)
                {
                    ValueDisplayUI.RequestEquipmentValueRefreshDebounced();
                    return;
                }

                if (!ReferenceEquals(__instance, RaidPlayerState.MainPlayer))
                {
                    return;
                }

                ValueTracker.HandleItemRemoved(eventArgs?.Item);
                ValueDisplayUI.RequestRaidItemReconcile(eventArgs?.Item);
                ValueDisplayUI.RequestRaidValueTextRefresh();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[AvgSellPrice] PlayerItemRemovedPatch failed: {ex}");
            }
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
            ValueDisplayUI.BeginBaselineWarmup();
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

    internal class RaidEndSummaryShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SessionResultExitStatus)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "Show" &&
                    method.GetParameters().Length == 1);
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.ShowRaidEndLootValue();
        }
    }

    internal class RaidEndExperienceShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SessionResultExperienceCount)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == "Show");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.HideRaidEndLootValueNow();
        }
    }

    internal class RaidEndKillListShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SessionResultKillList)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == "Show");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.HideRaidEndLootValueNow();
        }
    }

    internal class RaidEndStatisticsShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SessionResultStatistics)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == "Show");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.HideRaidEndLootValueNow();
        }
    }

    internal class RaidEndHealthTreatmentShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(HealthTreatmentScreen)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == "Show");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.HideRaidEndLootValueNow();
        }
    }

    internal class MatchmakerInsuranceScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                AccessTools.TypeByName("EFT.UI.Matchmaker.MatchmakerInsuranceScreen"),
                "Show");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            ValueDisplayUI.SetEquipmentValueBlocked(true);
        }
    }

    internal class MatchmakerInsuranceScreenClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                AccessTools.TypeByName("EFT.UI.Matchmaker.MatchmakerInsuranceScreen"),
                "Close");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.SetEquipmentValueBlocked(false);
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
            if (ValueTracker.IsInRaid)
            {
                ValueDisplayUI.SetRaidInventoryVisible(true);
            }
        }
    }

    internal class EquipmentTabShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EquipmentTab), "Show");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.ShowEquipmentValueForInventory();
        }
    }

    internal class InventoryScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(InventoryScreen)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method =>
                    method.Name == "Show" &&
                    method.GetParameters().Length == 1);
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.ShowEquipmentValueForInventory();
        }
    }

    internal class EquipmentTabHidePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EquipmentTab), "Hide");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            ValueDisplayUI.SetInventoryVisible(false);
        }
    }

    internal class InventoryScreenClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryScreen), "Close");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class InventoryScreenTabSwitchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(InventoryScreen),
                "method_4",
                new[] { AccessTools.TypeByName("EInventoryTab"), typeof(bool) });
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class InventoryTabPointerClickPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("Tab"), "OnPointerClick");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class OverallScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(OverallScreen), "Show");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.HideSoon();
        }
    }

    internal class SkillsScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SkillsScreen), "Show");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.HideSoon();
        }
    }

    internal class CharacterHealthPanelShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(CharacterHealthPanel), "Show");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class CharacterHealthPanelAnimatedShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(CharacterHealthPanel), "AnimatedShow");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class TextGameMapPanelShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(TextGameMapPanel), "Show");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class TasksScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(TasksScreen), "Show");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class AchievementsScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(AchievementsScreen), "Show");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class PrestigeScreenShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("EFT.UI.Prestige.PrestigeScreen"), "Show");
        }

        [PatchPrefix]
        private static void PatchPrefix()
        {
            EquipmentValueVisibility.Hide();
        }
    }

    internal class InventoryControllerRaiseAddEventPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("TraderControllerClass"), "RaiseAddEvent");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            EquipmentValueVisibility.RefreshSoon();
        }
    }

    internal class InventoryControllerRaiseRemoveEventPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("TraderControllerClass"), "RaiseRemoveEvent");
        }

        [PatchPostfix]
        private static void PatchPostfix()
        {
            EquipmentValueVisibility.RefreshSoon();
        }
    }

}
