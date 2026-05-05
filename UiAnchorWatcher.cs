using System;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace GearAndLootValue
{
    internal sealed partial class ValueDisplayUI
    {
        private static TextMeshProUGUI FindEquipmentAnchor()
        {
            TextMeshProUGUI[] candidates = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .Where(IsUsableTextComponent)
                .Where(text =>
                {
                    string normalized = NormalizeText(text.text);

                    return EquipmentWeightRegex.IsMatch(normalized) ||
                           string.Equals(normalized, "kg", StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();

            return candidates
                .OrderByDescending(GetEquipmentAnchorScore)
                .ThenBy(text => GetScreenPoint(text).y)
                .ThenBy(text => GetScreenPoint(text).x)
                .FirstOrDefault();
        }

        private static TextMeshProUGUI FindRaidAnchor()
        {
            TextMeshProUGUI[] candidates = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .Where(IsUsableTextComponent)
                .Where(IsRaidWeightCandidate)
                .ToArray();

            return candidates
                .OrderByDescending(GetRaidAnchorScore)
                .ThenBy(text => GetScreenPoint(text).y)
                .ThenBy(text => GetScreenPoint(text).x)
                .FirstOrDefault();
        }

        private static int GetEquipmentAnchorScore(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return int.MinValue;
            }

            int score = 0;
            Vector2 screenPoint = GetScreenPoint(text);
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);

            if (screenPoint.x <= screenWidth * 0.6f)
            {
                score += 4;
            }

            if (screenPoint.y <= screenHeight * 0.45f)
            {
                score += 4;
            }

            score += CountNearbySlashStats(text, 4) * 10;
            score += CountNearbyKnownStatValues(text, 4) * 6;

            return score;
        }

        private static bool IsRaidWeightCandidate(TextMeshProUGUI text)
        {
            string normalized = NormalizeText(text.text);
            if (string.Equals(normalized, "kg", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!RaidWeightRegex.IsMatch(normalized))
            {
                return false;
            }

            return CountNearbyKnownStatValues(text, 4) >= 2;
        }

        private static int GetRaidAnchorScore(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return int.MinValue;
            }

            int score = 0;
            Vector2 screenPoint = GetScreenPoint(text);
            float screenWidth = Mathf.Max(1f, Screen.width);
            float screenHeight = Mathf.Max(1f, Screen.height);

            if (screenPoint.x <= screenWidth * 0.65f)
            {
                score += 6;
            }

            if (screenPoint.y <= screenHeight * 0.45f)
            {
                score += 6;
            }

            score += CountNearbyKnownStatValues(text, 4) * 12;
            score += CountNearbySlashStats(text, 4) * 4;

            string normalized = NormalizeText(text.text);
            if (string.Equals(normalized, "kg", StringComparison.OrdinalIgnoreCase))
            {
                score += 8;
            }

            return score;
        }

        private static int CountNearbySlashStats(TextMeshProUGUI anchor, int maxAncestorDepth)
        {
            return EnumerateNearbyTexts(anchor, maxAncestorDepth)
                .Select(text => NormalizeText(text.text))
                .Count(value => StatValueRegex.IsMatch(value));
        }

        private static int CountNearbyKnownStatValues(TextMeshProUGUI anchor, int maxAncestorDepth)
        {
            return EnumerateNearbyTexts(anchor, maxAncestorDepth)
                .Select(text => NormalizeText(text.text))
                .Count(value =>
                    string.Equals(value, "HP", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "kg", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "hydration", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value, "energy", StringComparison.OrdinalIgnoreCase));
        }

        private static TextMeshProUGUI[] EnumerateNearbyTexts(TextMeshProUGUI anchor, int maxAncestorDepth)
        {
            Transform current = anchor != null ? anchor.transform : null;

            for (int depth = 0; current != null && depth <= maxAncestorDepth; depth++)
            {
                TextMeshProUGUI[] texts = current.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Where(IsUsableTextComponent)
                    .ToArray();

                if (texts.Length >= 4)
                {
                    return texts;
                }

                current = current.parent;
            }

            return Array.Empty<TextMeshProUGUI>();
        }

        private static Vector2 GetScreenPoint(TextMeshProUGUI text)
        {
            if (text == null || text.rectTransform == null)
            {
                return Vector2.zero;
            }

            Vector3 position = text.rectTransform.position;
            return new Vector2(position.x, position.y);
        }

        private static bool IsUsableTextComponent(TextMeshProUGUI text)
        {
            if (text == null || text.gameObject == null)
            {
                return false;
            }

            if (!text.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (string.Equals(text.name, EquipmentLabelObjectName, StringComparison.Ordinal) ||
                string.Equals(text.name, RaidLabelObjectName, StringComparison.Ordinal))
            {
                return false;
            }

            return text.rectTransform != null;
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Trim();
        }

    }
}