using BepInEx.Logging;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using System;
using System.Reflection;



namespace AvgSellPrice
{
    internal class GridItemViewTooltipPatch
    {
        private static readonly ManualLogSource Log =
            BepInEx.Logging.Logger.CreateLogSource("AvgSellPrice.GridTooltip");

        public void Enable()
        {
            try
            {
                MethodInfo method = typeof(GridItemView).GetMethod(
                    "ShowTooltip",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (method == null)
                {
                    Log.LogError("GridItemView.ShowTooltip NOT FOUND");
                    return;
                }

                Harmony harmony = new Harmony("com.simon.approxsellprice.gridtooltip");

                harmony.Patch(
                    method,
                    postfix: new HarmonyMethod(
                        typeof(GridItemViewTooltipPatch).GetMethod(
                            nameof(Postfix),
                            BindingFlags.Static | BindingFlags.NonPublic))
                );

                Log.LogInfo("GridItemView.ShowTooltip patch ENABLED");
            }
            catch (Exception ex)
            {
                Log.LogError("Patch failed: " + ex);
            }
        }

        private static void Postfix(GridItemView __instance)
        {
            try
            {
                if (__instance == null || __instance.Item == null)
                {
                    return;
                }

                FieldInfo contextField = typeof(ItemView).GetField(
                    "ItemUiContext",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

                if (contextField == null)
                {
                    Log.LogError("ItemUiContext field NOT FOUND");
                    return;
                }

                ItemUiContext context = contextField.GetValue(__instance) as ItemUiContext;

                if (context == null || context.Tooltip == null)
                {
                    return;
                }

                Item item = __instance.Item;

                Plugin.Log?.LogInfo(
                    $"[AvgSellPrice] Tooltip owner type: ownerType={item.Owner?.OwnerType.ToString() ?? "NULL"} class={item.Owner?.GetType().Name ?? "NULL"}");

                if (PluginConfig.HideTooltipInTraderSellScreen.Value &&
                    item.Owner != null &&
                    item.Owner.OwnerType != EOwnerType.Profile &&
                    item.Owner.GetType().Name == "TraderControllerClass")
                {
                    return;
                }



                string priceBlock = item.GetHoverPriceText();
                if (string.IsNullOrEmpty(priceBlock))
                {
                    return;
                }

                MethodInfo originalTextMethod = typeof(GridItemView).GetMethod(
                    "method_26",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );

                string originalText = null;

                if (PluginConfig.HideTooltipInTraderSellScreen.Value &&
                !string.IsNullOrEmpty(originalText))
                {
                    string lower = originalText.ToLowerInvariant();

                    if (lower.Contains("prapor:") ||
                        lower.Contains("therapist:") ||
                        lower.Contains("fence:") ||
                        lower.Contains("skier:") ||
                        lower.Contains("peacekeeper:") ||
                        lower.Contains("mechanic:") ||
                        lower.Contains("ragman:") ||
                        lower.Contains("jaeger:") ||
                        lower.Contains("ref:"))
                    {
                        return;
                    }
                }


                if (originalTextMethod != null)
                {
                    originalText = originalTextMethod.Invoke(__instance, null) as string;
                }

                if (string.IsNullOrEmpty(originalText))
                {
                    originalText = GClass2348.Localized(item.ShortName);
                }

                string newText = originalText + Environment.NewLine + priceBlock;

                context.Tooltip.Show(newText, null, 0f);
            }
            catch (Exception ex)
            {
                Log.LogError("Postfix failed: " + ex);
            }
        }
    }
}