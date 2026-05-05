using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using SPT.Reflection.Utils;
using static GearAndLootValue.TarkovItemPrices;
using static GearAndLootValue.TraderSellOffers;
using static GearAndLootValue.ArmorPricing;
using static GearAndLootValue.ContainerPricing;
using static GearAndLootValue.WeaponPricing;
namespace GearAndLootValue
{
    internal static class SellPriceTooltip
    {
        public static void AddApproxSellPriceAttribute(this Item item)
        {
            if (item == null)
            {
                return;
            }

            if (item.Attributes == null)
            {
                item.Attributes = new List<ItemAttributeClass>();
            }

            if (item.Attributes.Any(a => a.Name == "ApproxSellPrice"))
            {
                return;
            }

            ItemAttributeClass attribute = new ItemAttributeClass(EItemAttributeId.MoneySum)
            {
                Name = "ApproxSellPrice",
                DisplayNameFunc = () => "Sell Price",

                Base = () =>
                {
                    if (IsInTraderSellScreen())
                    {
                        return 0.01f;
                    }

                    int price = MainTooltipPrice(item);
                    return price > 0 ? price : 0.01f;
                },

                StringValue = () =>
                {
                    if (IsInTraderSellScreen())
                    {
                        return string.Empty;
                    }

                    string text = item.GetHoverPriceText();

                    if (string.IsNullOrEmpty(text))
                    {
                        return "Cannot be sold";
                    }

                    string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    return lines[0];
                },

                FullStringValue = () =>
                {
                    if (IsInTraderSellScreen())
                    {
                        return string.Empty;
                    }

                    string text = item.GetHoverPriceText();

                    if (string.IsNullOrEmpty(text))
                    {
                        return "No trader buy price available";
                    }

                    return text;
                },

                DisplayType = () => EItemAttributeDisplayType.Compact
            };

            List<ItemAttributeClass> list = new List<ItemAttributeClass>();
            list.Add(attribute);
            list.AddRange(item.Attributes);
            item.Attributes = list;
        }

        internal static string ToRichTextColor(Color color)
        {
            Color32 c = color;
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}";
        }

        internal static string Colorize(string text, Color color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (PluginConfig.EnableHoverColors != null && !PluginConfig.EnableHoverColors.Value)
            {
                return text;
            }

            return $"<color={ToRichTextColor(color)}>{text}</color>";
        }

        public static string GetHoverPriceText(this Item item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (PluginConfig.EnablePrice != null && !PluginConfig.EnablePrice.Value)
            {
                return string.Empty;
            }

            if (IsInTraderSellScreen())
            {
                return string.Empty;
            }

            if (UseFleaPriceSource && CannotSellOnFlea(item))
            {
                return NotSellableFleaText(item);
            }

            if (item is AmmoItemClass)
            {
                if (!ShowAmmoPrice)
                {
                    return string.Empty;
                }

                TraderOffer ammoOffer = PickOffer(item);
                int ammoPrice = IsTraderStockItem(item)
                    ? PriceSingleItem(item)
                    : PriceStack(item);

                if (ammoOffer == null || ammoPrice <= 0)
                {
                    return string.Empty;
                }

                List<string> ammoLines = new List<string>();
                AddAlwaysFleaBaseLine(ammoLines, item, applyMinimum: false);
                ammoLines.Add(Colorize(
                    FormatMainPriceWithOptionalTrader(ammoOffer.Name, ammoPrice, applyMinimum: false),
                    PluginConfig.MainPriceColor.Value));
                return string.Join(Environment.NewLine, ammoLines);
            }

            if (IsMagazine(item))
            {
                bool includeMagazineAmmo = ShowAmmoPrice;
                TraderOffer totalOffer = PickOffer(item);
                int ammoPrice = includeMagazineAmmo ? PriceLoadedAmmo(item) : 0;
                int totalPrice = totalOffer != null ? totalOffer.Price : 0;
                int basePrice = UseFleaPriceSource
                    ? PriceSingleItem(item)
                    : includeMagazineAmmo && totalPrice > 0
                        ? Math.Max(0, totalPrice - ammoPrice)
                        : PriceSingleItem(item);

                if (UseFleaPriceSource)
                {
                    totalPrice = basePrice + ammoPrice;
                }

                if (basePrice <= 0 && totalPrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value && totalOffer != null
                    ? totalOffer.Name
                    : string.Empty;

                List<string> magazineLines = new List<string>();
                AddAlwaysFleaBaseLine(magazineLines, item);
                magazineLines.Add(Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, basePrice > 0 ? basePrice : totalPrice),
                    PluginConfig.MainPriceColor.Value));

                if (ammoPrice > 0)
                {
                    magazineLines.Add(Colorize(
                        "Ammo " + FormatPriceExternal(ammoPrice),
                        PluginConfig.AmmoPriceColor.Value));

                    magazineLines.Add(Colorize(
                        "Total " + FormatPriceExternal((basePrice > 0 ? basePrice : 0) + ammoPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, magazineLines);
            }

            if (item is Weapon weapon)
            {
                TraderOffer weaponOffer = PickOffer(weapon);
                int totalPrice = weaponOffer != null ? weaponOffer.Price : 0;
                int attachmentsPrice = PriceWeaponMods(weapon);
                int magazinePrice = GetWeaponMagazineTraderPrice(weapon);
                bool fallbackToTraderBase = UseFleaPriceSource && CannotSellOnFlea(weapon);
                int basePrice = fallbackToTraderBase
                    ? totalPrice > 0
                        ? Math.Max(0, totalPrice - attachmentsPrice - magazinePrice)
                        : PriceSingleItem(weapon)
                    : UseFleaPriceSource
                    ? PriceSingleItem(weapon)
                    : totalPrice > 0
                        ? Math.Max(0, totalPrice - attachmentsPrice - magazinePrice)
                        : 0;

                if (UseFleaPriceSource)
                {
                    totalPrice = basePrice + attachmentsPrice + magazinePrice;
                }

                if (basePrice <= 0 && totalPrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value && weaponOffer != null
                    ? weaponOffer.Name
                    : string.Empty;
                bool showWeaponAttachments = PluginConfig.ShowWeaponAttachmentsPrice == null ||
                                             PluginConfig.ShowWeaponAttachmentsPrice.Value;
                bool showMagazine = ShowMagazineBreakdown;
                int visibleBasePrice = basePrice > 0 ? basePrice : totalPrice;
                if (!showMagazine)
                {
                    visibleBasePrice += magazinePrice;
                }
                if (!showWeaponAttachments)
                {
                    visibleBasePrice += attachmentsPrice;
                }

                List<string> weaponLines = new List<string>();
                AddAlwaysFleaBaseLine(weaponLines, item);
                weaponLines.Add(Colorize(
                    fallbackToTraderBase
                        ? FormatBasePriceWithOptionalTrader(traderName, visibleBasePrice)
                        : FormatMainPriceWithOptionalTrader(traderName, visibleBasePrice),
                    PluginConfig.MainPriceColor.Value));

                if (showWeaponAttachments && attachmentsPrice > 0 || showMagazine && magazinePrice > 0)
                {
                    if (showMagazine && magazinePrice > 0)
                    {
                        weaponLines.Add(Colorize(
                            "Mag " + FormatPriceExternal(magazinePrice),
                            PluginConfig.MagazinePriceColor.Value));
                    }

                    if (showWeaponAttachments && attachmentsPrice > 0)
                    {
                        weaponLines.Add(Colorize(
                            "Attachments " + FormatPriceExternal(attachmentsPrice),
                            PluginConfig.AttachmentsPriceColor.Value));
                    }

                    weaponLines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, weaponLines);
            }

            if (NeedsModBreakdown(item))
            {
                TraderOffer itemOffer = PickOffer(item);
                int totalPrice = itemOffer != null ? itemOffer.Price : 0;
                int attachmentsPrice = GetModAttachmentPrice(item);
                int basePrice = UseFleaPriceSource
                    ? PriceSingleItem(item)
                    : totalPrice > 0
                        ? Math.Max(0, totalPrice - attachmentsPrice)
                        : PriceSingleItem(item);

                if (UseFleaPriceSource || totalPrice <= 0)
                {
                    totalPrice = basePrice + attachmentsPrice;
                }

                if (basePrice <= 0 && totalPrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value && itemOffer != null
                    ? itemOffer.Name
                    : string.Empty;

                List<string> lines = new List<string>();
                AddAlwaysFleaBaseLine(lines, item);
                lines.Add(Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, basePrice > 0 ? basePrice : totalPrice),
                    PluginConfig.MainPriceColor.Value));

                if (attachmentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Attachments " + FormatPriceExternal(attachmentsPrice),
                        PluginConfig.AttachmentsPriceColor.Value));

                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }

            if (IsArmoredRig(item))
            {
                TraderOffer rigOffer = GetArmoredRigTraderOffer(item);
                int rigPrice = GetArmoredRigBasePrice(item);

                if (rigPrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value
                    ? rigOffer != null ? rigOffer.Name : "Ragman"
                    : string.Empty;
                string baseLine = Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, rigPrice),
                    PluginConfig.MainPriceColor.Value);

                if (HideUnsearchedLootValue(item))
                {
                    List<string> unsearchedLines = new List<string>();
                    AddAlwaysFleaBaseLine(unsearchedLines, item);
                    unsearchedLines.Add(baseLine);
                    unsearchedLines.Add(GetUnsearchedHoverLine());
                    return string.Join(Environment.NewLine, unsearchedLines);
                }

                int platesPrice = GetArmorPlateTraderPrice(item);
                int contentsPrice = PriceContents(item);
                bool showPlates = ShowPlatesBreakdown;
                bool showContents = ShowContentsBreakdown;
                int displayBasePrice = rigPrice + (showPlates ? 0 : platesPrice);
                int visibleContentsPrice = showContents ? contentsPrice : 0;
                int totalPrice = displayBasePrice + (showPlates ? platesPrice : 0) + visibleContentsPrice;

                List<string> rigLines = new List<string>();
                AddAlwaysFleaBaseLine(rigLines, item);
                rigLines.Add(Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, displayBasePrice),
                    PluginConfig.MainPriceColor.Value));

                if (showPlates && platesPrice > 0)
                {
                    rigLines.Add(Colorize(
                        "Plates " + FormatPriceExternal(platesPrice),
                        PluginConfig.PlatesPriceColor.Value));
                }

                if (showContents && contentsPrice > 0)
                {
                    rigLines.Add(Colorize(
                        "Contents " + FormatContentsPriceVisual(contentsPrice),
                        PluginConfig.ContentsPriceColor.Value));
                }

                if (showPlates && platesPrice > 0 || showContents && contentsPrice > 0)
                {
                    rigLines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, rigLines);
            }

            if (HasHardPlateSlots(item))
            {
                TraderOffer totalOffer = PickOffer(item);
                int totalPrice = totalOffer != null ? totalOffer.Price : 0;
                int platesPrice = GetArmorPlateTraderPrice(item);

                if (totalPrice > 0)
                {
                    bool showPlates = ShowPlatesBreakdown;
                    int basePrice = UseFleaPriceSource
                        ? totalPrice
                        : Math.Max(0, totalPrice - platesPrice);
                    if (UseFleaPriceSource)
                    {
                        totalPrice = basePrice + platesPrice;
                    }

                    int displayBasePrice = basePrice + (showPlates ? 0 : platesPrice);
                    string traderName = PluginConfig.ShowTraderNameInTooltip.Value && totalOffer != null
                        ? totalOffer.Name
                        : string.Empty;

                    List<string> lines = new List<string>();
                    AddAlwaysFleaBaseLine(lines, item);
                    lines.Add(Colorize(
                        FormatMainPriceWithOptionalTrader(traderName, displayBasePrice > 0 ? displayBasePrice : totalPrice),
                        PluginConfig.MainPriceColor.Value));

                    if (showPlates && platesPrice > 0)
                    {
                        lines.Add(Colorize(
                            "Plates " + FormatPriceExternal(platesPrice),
                            PluginConfig.PlatesPriceColor.Value));

                        lines.Add(Colorize(
                            "Total " + FormatPriceExternal(totalPrice),
                            PluginConfig.TotalPriceColor.Value));
                    }

                    return string.Join(Environment.NewLine, lines);
                }
            }


            if (IsRealContainer(item))
            {
                if (PluginConfig.ShowCasePrice != null &&
                    !PluginConfig.ShowCasePrice.Value)
                {
                    return string.Empty;
                }

                TraderOffer containerOffer = GetContainerBaseTraderOffer(item);
                int basePrice = ContainerBasePrice(item);

                if (containerOffer != null && containerOffer.Price > 0)
                {
                    basePrice = containerOffer.Price;
                }

                if (basePrice <= 0)
                {
                    return string.Empty;
                }

                string traderName = PluginConfig.ShowTraderNameInTooltip.Value
                    ? GetContainerBaseTraderName(item, containerOffer)
                    : string.Empty;
                string baseLine = Colorize(
                    FormatMainPriceWithOptionalTrader(traderName, basePrice),
                    PluginConfig.MainPriceColor.Value);

                if (HideUnsearchedLootValue(item))
                {
                    List<string> unsearchedLines = new List<string>();
                    AddAlwaysFleaBaseLine(unsearchedLines, item);
                    unsearchedLines.Add(baseLine);
                    unsearchedLines.Add(GetUnsearchedHoverLine());
                    return string.Join(Environment.NewLine, unsearchedLines);
                }

                if (!ShowContentsBreakdown)
                {
                    List<string> baseOnlyLines = new List<string>();
                    AddAlwaysFleaBaseLine(baseOnlyLines, item);
                    baseOnlyLines.Add(baseLine);
                    return string.Join(Environment.NewLine, baseOnlyLines);
                }

                int contentsPrice = PriceContents(item);
                int totalPrice = basePrice + contentsPrice;

                List<string> lines = new List<string>();
                AddAlwaysFleaBaseLine(lines, item);
                lines.Add(baseLine);

                if (contentsPrice > 0)
                {
                    lines.Add(Colorize(
                        "Contents " + FormatContentsPriceVisual(contentsPrice),
                        PluginConfig.ContentsPriceColor.Value));

                    lines.Add(Colorize(
                        "Total " + FormatPriceExternal(totalPrice),
                        PluginConfig.TotalPriceColor.Value));
                }

                return string.Join(Environment.NewLine, lines);
            }



            TraderOffer fallbackOffer = PickOffer(item);

            if (fallbackOffer == null || fallbackOffer.Price <= 0)
            {
                return string.Empty;
            }

            List<string> fallbackLines = new List<string>();
            AddAlwaysFleaBaseLine(fallbackLines, item);
            fallbackLines.Add(Colorize(
                FormatMainPriceWithOptionalTrader(fallbackOffer.Name, fallbackOffer.Price),
                PluginConfig.MainPriceColor.Value));
            return string.Join(Environment.NewLine, fallbackLines);
        }

        internal static bool IsInTraderSellScreen()
        {

            try
            {
                object app = ClientAppUtils.GetMainApp();

                if (app == null)
                {
                    return false;
                }

                string appTypeName = app.GetType().FullName ?? string.Empty;

                if (appTypeName.IndexOf("Trading", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                if (appTypeName.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Trader screen check failed: {ex.Message}");
            }

            return false;
        }

        // Tarkov already shows prices in trader windows.
        internal static bool IsTraderStockItem(Item item)
        {
            object owner = item?.Owner;
            if (owner == null)
            {
                return false;
            }

            string ownerType = item.Owner.OwnerType.ToString() ?? string.Empty;
            string ownerClass = owner.GetType().FullName ?? owner.GetType().Name ?? string.Empty;

            return ownerType.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ownerClass.IndexOf("Trader", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void AddAlwaysFleaBaseLine(List<string> lines, Item item, bool applyMinimum = true)
        {
            if (UseFleaPriceSource)
            {
                AddAlwaysTraderSellBaseLine(lines, item, applyMinimum);
                return;
            }

            if (lines == null ||
                item == null ||
                PluginConfig.AlwaysShowFlea == null ||
                !PluginConfig.AlwaysShowFlea.Value)
            {
                return;
            }

            if (CannotSellOnFlea(item))
            {
                lines.Add(Colorize("Not sellable on flea", PluginConfig.NotSellableOnFleaColor.Value));
                return;
            }

            int fleaPrice = GetFleaTemplatePrice(item);
            if (fleaPrice <= 0)
            {
                return;
            }

            int price = applyMinimum ? ApplyMinimumPrice(fleaPrice) : fleaPrice;
            lines.Add(Colorize("Flea " + FormatPriceExternal(price), PluginConfig.MainPriceColor.Value));
        }

        internal static void AddAlwaysTraderSellBaseLine(List<string> lines, Item item, bool applyMinimum = true)
        {
            if (lines == null ||
                item == null ||
                PluginConfig.AlwaysShowTraderSell == null ||
                !PluginConfig.AlwaysShowTraderSell.Value)
            {
                return;
            }

            TraderOffer traderOffer = TraderSellOffer(item);
            if (traderOffer == null || traderOffer.Price <= 0)
            {
                return;
            }

            int price = applyMinimum ? ApplyMinimumPrice(traderOffer.Price) : traderOffer.Price;
            string label = PluginConfig.ShowTraderNameInTooltip.Value && !string.IsNullOrEmpty(traderOffer.Name)
                ? traderOffer.Name
                : "Trader";

            lines.Add(Colorize(label + " " + FormatPriceExternal(price), PluginConfig.MainPriceColor.Value));
        }

        internal static string FormatMainPriceWithOptionalTrader(string traderName, int rawPrice, bool applyMinimum = true)
        {
            int price = applyMinimum ? ApplyMinimumPrice(rawPrice) : rawPrice;

            string formattedPrice = PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);

            if (!PluginConfig.ShowTraderNameInTooltip.Value || string.IsNullOrEmpty(traderName))
            {
                if (!PluginConfig.ShowTraderNameInTooltip.Value)
                {
                    return (UseFleaPriceSource ? "Flea " : "Base ") + formattedPrice;
                }

                return formattedPrice;
            }

            return traderName + " " + formattedPrice;
        }

        internal static string FormatBasePriceWithOptionalTrader(string traderName, int rawPrice, bool applyMinimum = true)
        {
            int price = applyMinimum ? ApplyMinimumPrice(rawPrice) : rawPrice;

            string formattedPrice = PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);

            if (!PluginConfig.ShowTraderNameInTooltip.Value || string.IsNullOrEmpty(traderName))
            {
                return "Base " + formattedPrice;
            }

            return traderName + " " + formattedPrice;
        }

        internal static int ApplyMinimumPrice(int rawPrice)
        {
            int minimum = PluginConfig.MinimumDisplayPrice.Value;

            if (minimum > 0 && rawPrice > 0 && rawPrice < minimum)
            {
                return minimum;
            }

            return rawPrice;
        }

        internal static string FormatContentsPriceVisual(int price)
        {
            if (!PluginConfig.PrecisePrice.Value && price > 0 && price < 1000)
            {
                price = 1000;
            }

            return PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);
        }

        public static string FormatPriceExternal(int price)
        {
            return PluginConfig.PrecisePrice.Value
                ? FormatPrecise(price)
                : FormatPrice(price);
        }

    }
}
