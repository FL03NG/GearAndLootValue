using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AvgSellPrice
{
    internal sealed class ValueDisplayUI : MonoBehaviour
    {
        private const string EquipmentLabelObjectName = "AvgSellPriceEquipmentValue";
        private const string RaidLabelObjectName = "AvgSellPriceLootValue";
        private const float PassiveEquipmentRefreshInterval = 2.5f;
        private const float TraderUiScanInterval = 2.5f;

        private static readonly Regex EquipmentWeightRegex =
            new Regex(@"^\s*\d+([.,]\d+)?\s*kg\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RaidWeightRegex =
            new Regex(@"^\s*\d+\s*/\s*\d+\s*(kg)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex StatValueRegex =
            new Regex(@"^\s*\d+(?:[.,]\d+)?\s*/\s*\d+(?:[.,]\d+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static ValueDisplayUI Instance { get; private set; }

        private bool _pendingRefresh = true;
        private float _nextAnchorSearchTime;
        private float _nextPassiveEquipmentRefreshTime;
        private float _nextTraderUiScanTime;
        private bool _isTraderSellUiVisible;
        private bool _equipmentValueDirty = true;
        private int _cachedEquipmentValue = -1;
        private TextMeshProUGUI _equipmentAnchor;
        private TextMeshProUGUI _raidAnchor;
        private GameObject _equipmentBox;
        private GameObject _raidBox;
        private TextMeshProUGUI _equipmentLabel;
        private TextMeshProUGUI _raidLabel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                RefreshNow();
            }

            if (!ValueTracker.IsInRaid && Time.unscaledTime >= _nextPassiveEquipmentRefreshTime)
            {
                _nextPassiveEquipmentRefreshTime = Time.unscaledTime + PassiveEquipmentRefreshInterval;
                RefreshEquipmentLabel();
            }
        }

        internal static void RequestRefresh()
        {
            if (Instance != null)
            {
                Instance._equipmentValueDirty = true;
                Instance._nextTraderUiScanTime = 0f;
                Instance._pendingRefresh = true;
            }
        }

        private void RefreshNow()
        {
            if (ValueTracker.IsInRaid)
            {
                HideLabel(_equipmentLabel);
                RefreshRaidLabel();
                return;
            }

            HideLabel(_raidLabel);
            RefreshEquipmentLabel();
        }

        private void RefreshEquipmentLabel()
        {
            if (IsTraderSellUiVisible())
            {
                HideLabel(_equipmentLabel);
                return;
            }

            TextMeshProUGUI anchor = GetEquipmentAnchor();
            if (anchor == null)
            {
                HideLabel(_equipmentLabel);
                return;
            }

            _equipmentLabel = EnsureLabel(
                ref _equipmentBox,
                ref _equipmentLabel,
                anchor,
                EquipmentLabelObjectName,
                string.Empty,
                new Vector2(-69f, 54f),
                493f,
                50f,
                2.36f);

            int value = ItemExtensions.GetPlayerEquipmentValue();
            _equipmentLabel.text = $"Equipment Value: <b>{ItemExtensions.FormatMoneyUi(value)} ₽</b>";
            _equipmentLabel.color = new Color(0.92f, 0.92f, 0.93f, 1f);
            _equipmentBox.SetActive(true);
        }

        private void RefreshRaidLabel()
        {
            TextMeshProUGUI anchor = GetRaidAnchor();
            if (anchor == null)
            {
                HideLabel(_raidLabel);
                return;
            }

            _raidLabel = EnsureLabel(
                ref _raidBox,
                ref _raidLabel,
                anchor,
                RaidLabelObjectName,
                string.Empty,
                new Vector2(-69f, 54f),
                493f,
                50f,
                2.36f);

            int value = ValueTracker.CurrentRaidLootValue;
            _raidLabel.text = $"<b>{ItemExtensions.FormatMoneyUi(value)} ₽</b>";
            _raidLabel.color = GetRaidValueColor(value);
            _raidBox.SetActive(true);
        }

        private static TextMeshProUGUI EnsureLabel(
            ref GameObject box,
            ref TextMeshProUGUI label,
            TextMeshProUGUI anchor,
            string objectName,
            string title,
            Vector2 offset,
            float width,
            float height,
            float fontScale)
        {
            if (box == null || box.gameObject == null)
            {
                box = CreateValueBox(anchor.transform.parent, objectName);
                label = box.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (box.transform.parent != anchor.transform.parent)
            {
                box.transform.SetParent(anchor.transform.parent, false);
            }

            RectTransform anchorRect = anchor.rectTransform;
            RectTransform boxRect = box.GetComponent<RectTransform>();
            RectTransform labelRect = label.rectTransform;

            boxRect.anchorMin = anchorRect.anchorMin;
            boxRect.anchorMax = anchorRect.anchorMax;
            boxRect.pivot = anchorRect.pivot;
            boxRect.anchoredPosition = anchorRect.anchoredPosition + offset;
            boxRect.sizeDelta = new Vector2(width, height);
            boxRect.localScale = Vector3.one;
            boxRect.localRotation = Quaternion.identity;

            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 1f);
            labelRect.offsetMax = new Vector2(-8f, -1f);

            label.text = title;
            label.fontSize = Mathf.Max(12f, anchor.fontSize * fontScale);
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.fontStyle = FontStyles.Normal;

            return label;
        }

        private static GameObject CreateValueBox(Transform parent, string objectName)
        {
            GameObject box = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Outline));
            box.transform.SetParent(parent, false);

            Image image = box.GetComponent<Image>();
            image.color = new Color(0.07f, 0.07f, 0.08f, 0.94f);
            image.raycastTarget = false;

            Outline outline = box.GetComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.18f, 0.19f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            GameObject textObject = new GameObject(objectName + "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(box.transform, false);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.color = new Color(0.92f, 0.92f, 0.93f, 1f);
            text.raycastTarget = false;

            return box;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _equipmentAnchor = null;
            _raidAnchor = null;
            _nextAnchorSearchTime = 0f;
            _nextTraderUiScanTime = 0f;
            _isTraderSellUiVisible = false;
            _equipmentValueDirty = true;
            _cachedEquipmentValue = -1;
            _pendingRefresh = true;
        }

        private TextMeshProUGUI GetEquipmentAnchor()
        {
            if (IsUsableTextComponent(_equipmentAnchor))
            {
                return _equipmentAnchor;
            }

            if (Time.unscaledTime < _nextAnchorSearchTime)
            {
                return null;
            }

            _nextAnchorSearchTime = Time.unscaledTime + 2f;
            _equipmentAnchor = FindEquipmentAnchor();
            return _equipmentAnchor;
        }

        private TextMeshProUGUI GetRaidAnchor()
        {
            if (IsUsableTextComponent(_raidAnchor))
            {
                return _raidAnchor;
            }

            if (Time.unscaledTime < _nextAnchorSearchTime)
            {
                return null;
            }

            _nextAnchorSearchTime = Time.unscaledTime + 2f;
            _raidAnchor = FindRaidAnchor();
            return _raidAnchor;
        }

        private static void HideLabel(TextMeshProUGUI label)
        {
            if (label != null && label.gameObject != null)
            {
                Transform root = label.transform.parent;
                if (root != null && root.gameObject != null)
                {
                    root.gameObject.SetActive(false);
                    return;
                }

                label.gameObject.SetActive(false);
            }
        }


        private bool IsTraderSellUiVisible()
        {
            if (Time.unscaledTime < _nextTraderUiScanTime)
            {
                return _isTraderSellUiVisible;
            }

            _nextTraderUiScanTime = Time.unscaledTime + TraderUiScanInterval;

            try
            {
                HashSet<string> visibleTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                    .Where(IsUsableTextComponent)
                    .Select(text => NormalizeText(text.text))
                    .Where(text => !string.IsNullOrEmpty(text))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                bool hasDeal = visibleTexts.Contains("DEAL!");
                bool hasBuy = visibleTexts.Contains("BUY");
                bool hasSell = visibleTexts.Contains("SELL");

                _isTraderSellUiVisible = hasDeal && hasBuy && hasSell;
                return _isTraderSellUiVisible;
            }
            catch
            {
                _isTraderSellUiVisible = false;
                return false;
            }
        }

        private int GetCachedEquipmentValue()
        {
            if (_equipmentValueDirty || _cachedEquipmentValue < 0)
            {
                _cachedEquipmentValue = ItemExtensions.GetPlayerEquipmentValue();
                _equipmentValueDirty = false;
            }

            return _cachedEquipmentValue;
        }



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
            return FindBestAnchor(text =>
            {
                string normalized = NormalizeText(text);

                return string.Equals(normalized, "kg", StringComparison.OrdinalIgnoreCase) ||
                       RaidWeightRegex.IsMatch(normalized);
            });
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

        private static TextMeshProUGUI FindBestAnchor(Func<string, bool> predicate)
        {
            return Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                .Where(IsUsableTextComponent)
                .Where(text => predicate(text.text))
                .OrderBy(text => text.rectTransform.position.y)
                .ThenBy(text => text.rectTransform.position.x)
                .FirstOrDefault();
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

        private static Color GetRaidValueColor(int value)
        {
            int mid = PluginConfig.RaidValueMidThreshold.Value;
            int high = PluginConfig.RaidValueHighThreshold.Value;
            int max = PluginConfig.RaidValueMaxThreshold.Value;

            if (value <= mid)
            {
                float t = mid <= 0 ? 1f : (float)value / mid;
                return Color.Lerp(
                    PluginConfig.RaidValueLowColor.Value,
                    PluginConfig.RaidValueMidColor.Value,
                    Mathf.Clamp01(t));
            }

            if (value <= high)
            {
                float t = high <= mid ? 1f : (float)(value - mid) / (high - mid);
                return Color.Lerp(
                    PluginConfig.RaidValueMidColor.Value,
                    PluginConfig.RaidValueHighColor.Value,
                    Mathf.Clamp01(t));
            }

            float finalT = max <= high ? 1f : (float)(value - high) / (max - high);
            return Color.Lerp(
                PluginConfig.RaidValueHighColor.Value,
                PluginConfig.RaidValueMaxColor.Value,
                Mathf.Clamp01(finalT));
        }
    }
}




