using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using SPT.Reflection.Patching;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AvgSellPrice
{
    internal class SimpleTooltipShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            MethodInfo[] methods = typeof(SimpleTooltip).GetMethods(BindingFlags.Instance | BindingFlags.Public);

            MethodInfo target = methods
                .Where(x => x.Name == "Show")
                .FirstOrDefault(x =>
                {
                    ParameterInfo[] parameters = x.GetParameters();
                    return parameters.Length > 0 &&
                           parameters[0].ParameterType == typeof(string);
                });

            return target;
        }

        [PatchPrefix]
        private static void PatchPrefix(ref string text, ref Vector2? offset, ref float delay, SimpleTooltip __instance)
        {
            try
            {
                delay = 0f;

                if (!HoverState.IsGridItemHovered)
                {
                    return;
                }

                Item item = HoverState.HoveredItem;

                if (item == null)
                {
                    return;
                }

                string priceText = item.GetHoverPriceText();

                if (string.IsNullOrEmpty(priceText))
                {
                    return;
                }

                string itemName = item.ShortName;

                if (!string.IsNullOrEmpty(itemName))
                {
                    itemName = GClass2348.Localized(itemName);
                }

                if (string.IsNullOrEmpty(itemName))
                {
                    itemName = item.Name;
                }

                if (string.IsNullOrEmpty(itemName))
                {
                    itemName = text;
                }

                if (!string.IsNullOrEmpty(itemName))
                {
                    text = ReplaceFirstLineWithShortName(text, itemName);
                }

                if (!text.Contains(priceText))
                {
                    text += "<br>" + priceText.Replace(Environment.NewLine, "<br>");
                }
            }
            catch (Exception ex)
            {
                if (Plugin.Log != null)
                {
                    Plugin.Log.LogError("SimpleTooltipShowPatch failed: " + ex);
                }
            }
        }

        private static bool LooksLikeTraderSellTooltip(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string normalized = NormalizeTooltipText(text);

            return normalized.Contains("prapor:") ||
                   normalized.Contains("therapist:") ||
                   normalized.Contains("fence:") ||
                   normalized.Contains("skier:") ||
                   normalized.Contains("peacekeeper:") ||
                   normalized.Contains("mechanic:") ||
                   normalized.Contains("ragman:") ||
                   normalized.Contains("jaeger:") ||
                   normalized.Contains("ref:");
        }

        private static string RemoveTraderSellPriceLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            string[] lines = text.Split(new[] { "<br>" }, StringSplitOptions.None);

            string[] filtered = lines
                .Where(line => !LooksLikeTraderSellLine(line))
                .ToArray();

            if (filtered.Length == 0)
            {
                return text;
            }

            return string.Join("<br>", filtered);
        }

        private static bool LooksLikeTraderSellLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            string normalized = NormalizeTooltipText(line).Trim();

            return normalized.StartsWith("prapor:") ||
                   normalized.StartsWith("therapist:") ||
                   normalized.StartsWith("fence:") ||
                   normalized.StartsWith("skier:") ||
                   normalized.StartsWith("peacekeeper:") ||
                   normalized.StartsWith("mechanic:") ||
                   normalized.StartsWith("ragman:") ||
                   normalized.StartsWith("jaeger:") ||
                   normalized.StartsWith("ref:");
        }

        private static string NormalizeTooltipText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("<br>", "\n")
                .Replace("</color>", string.Empty)
                .Replace("<color=#c5bfb3>", string.Empty)
                .Replace("<color=#aeb4c4>", string.Empty)
                .Replace("<color=#ffffff>", string.Empty)
                .Replace("<color=white>", string.Empty)
                .Replace("<color=grey>", string.Empty)
                .Replace("<color=gray>", string.Empty)
                .ToLowerInvariant();
        }

        private static string ReplaceFirstLineWithShortName(string originalText, string shortName)
        {
            if (string.IsNullOrEmpty(originalText))
            {
                return shortName;
            }

            string[] brSplit = new string[] { "<br>" };
            string[] lines = originalText.Split(brSplit, StringSplitOptions.None);

            if (lines.Length == 0)
            {
                return shortName;
            }

            lines[0] = PreserveOuterColorTag(lines[0], shortName);

            return string.Join("<br>", lines);
        }

        private static string PreserveOuterColorTag(string originalLine, string replacementText)
        {
            if (string.IsNullOrEmpty(originalLine))
            {
                return replacementText;
            }

            int colorStart = originalLine.IndexOf("<color=", StringComparison.OrdinalIgnoreCase);
            int tagClose = originalLine.IndexOf(">", StringComparison.OrdinalIgnoreCase);
            int colorEnd = originalLine.IndexOf("</color>", StringComparison.OrdinalIgnoreCase);

            if (colorStart >= 0 && tagClose > colorStart && colorEnd > tagClose)
            {
                string openTag = originalLine.Substring(colorStart, tagClose - colorStart + 1);
                string closeTag = "</color>";
                return openTag + " " + replacementText + " " + closeTag;
            }

            return replacementText;
        }
    }
}
