using System;
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

        private static readonly Regex EquipmentWeightRegex =
            new Regex(@"^\s*\d+([.,]\d+)?\s*kg\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RaidWeightRegex =
            new Regex(@"^\s*\d+\s*/\s*\d+\s*(kg)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static ValueDisplayUI Instance { get; private set; }

        private bool _pendingRefresh = true;
        private float _nextAnchorSearchTime;
        private float _nextPassiveEquipmentRefreshTime;
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
                _nextPassiveEquipmentRefreshTime = Time.unscaledTime + 0.75f;
                RefreshEquipmentLabel();
            }
        }

        internal static void RequestRefresh()
        {
            if (Instance != null)
            {
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
                "Equipment Value",
                new Vector2(-18f, 42f),
                280f,
                42f,
                1.08f);

            int value = ItemExtensions.GetPlayerEquipmentValue();
            _equipmentLabel.text = $"{ItemExtensions.FormatMoneyUi(value)} ₽";
            _equipmentLabel.color = Color.white;
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
                "Loot Value",
                new Vector2(4f, 68f),
                220f,
                40f,
                1.05f);

            int value = ValueTracker.CurrentRaidLootValue;
            _raidLabel.text = $"{ItemExtensions.FormatMoneyUi(value)} ₽";
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
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);

            label.text = title;
            label.fontSize = Mathf.Max(13f, anchor.fontSize * fontScale);
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;

            return label;
        }

        private static GameObject CreateValueBox(Transform parent, string objectName)
        {
            GameObject box = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Outline));
            box.transform.SetParent(parent, false);

            Image image = box.GetComponent<Image>();
            image.color = new Color(0.24f, 0.25f, 0.27f, 0.92f);
            image.raycastTarget = false;

            Outline outline = box.GetComponent<Outline>();
            outline.effectColor = new Color(0.56f, 0.58f, 0.61f, 0.90f);
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

        private static TextMeshProUGUI FindEquipmentAnchor()
        {
            return FindBestAnchor(text =>
            {
                string normalized = NormalizeText(text);

                return EquipmentWeightRegex.IsMatch(normalized) ||
                       string.Equals(normalized, "kg", StringComparison.OrdinalIgnoreCase);
            });
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
