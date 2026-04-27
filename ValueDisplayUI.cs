using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EFT.InventoryLogic;
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
        private const string EquipmentOverlayCanvasObjectName = "AvgSellPriceEquipmentValueCanvas";
        private const float TraderUiScanInterval = 2.5f;
        private const int MaxRaidLabelCreateAttempts = 6;

        private static readonly Regex EquipmentWeightRegex =
            new Regex(@"^\s*\d+([.,]\d+)?\s*kg\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RaidWeightRegex =
            new Regex(@"^\s*\d+\s*/\s*\d+\s*(kg)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex StatValueRegex =
            new Regex(@"^\s*\d+(?:[.,]\d+)?\s*/\s*\d+(?:[.,]\d+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        internal static ValueDisplayUI Instance { get; private set; }

        private bool _pendingRefresh = true;
        private float _nextEquipmentAnchorSearchTime;
        private float _nextRaidAnchorSearchTime;
        private float _nextRaidLabelCreateTime;
        private float _nextBaselineWarmupTime;
        private float _baselineWarmupEndTime;
        private float _nextEquipmentLabelRefreshTime;
        private float _nextEquipmentHideTime;
        private float _nextTraderUiScanTime;
        private bool _raidLabelCreatePending;
        private int _raidLabelCreateAttempts;
        private bool _isTraderSellUiVisible;
        private bool _equipmentInventoryVisible;
        private bool _equipmentHidePending;
        private bool _equipmentLabelRefreshPending;
        private bool _equipmentValueDirty = true;
        private int _equipmentVisibilityVersion;
        private int _equipmentHideVersion;
        private int _cachedEquipmentValue = -1;
        private TextMeshProUGUI _equipmentAnchor;
        private TextMeshProUGUI _raidAnchor;
        private GameObject _equipmentBox;
        private GameObject _raidBox;
        private GameObject _equipmentOverlayCanvas;
        private GameObject _raidOverlayCanvas;
        private TextMeshProUGUI _equipmentLabel;
        private TextMeshProUGUI _raidLabel;
        private readonly List<PendingItemReconcile> _pendingItemReconciles = new List<PendingItemReconcile>();

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

            if (ValueTracker.IsInRaid)
            {
                ProcessBaselineWarmup();

                if ((Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape)) &&
                    _raidOverlayCanvas != null &&
                    _raidOverlayCanvas.gameObject != null)
                {
                    _raidOverlayCanvas.SetActive(false);
                }

                if (_raidLabelCreatePending && Time.unscaledTime >= _nextRaidLabelCreateTime)
                {
                    _raidLabelCreatePending = false;
                    RefreshRaidLabel();
                }

                ProcessPendingItemReconciles();
                return;
            }

            if (_equipmentLabelRefreshPending && Time.unscaledTime >= _nextEquipmentLabelRefreshTime)
            {
                _equipmentLabelRefreshPending = false;
                RefreshEquipmentLabelTextOnly();
            }

            if (_equipmentHidePending && Time.unscaledTime >= _nextEquipmentHideTime)
            {
                _equipmentHidePending = false;
                if (_equipmentHideVersion == _equipmentVisibilityVersion)
                {
                    SetEquipmentInventoryVisible(false);
                }
            }

        }

        internal static void RequestRefresh()
        {
            if (Instance != null)
            {
                Instance._equipmentValueDirty = true;
                Instance._nextEquipmentAnchorSearchTime = 0f;
                Instance._nextRaidAnchorSearchTime = 0f;
                Instance._nextTraderUiScanTime = 0f;
                Instance._pendingRefresh = true;
            }
        }

        internal static void RequestEquipmentValueRefresh(float delaySeconds = 0f)
        {
            if (Instance == null)
            {
                return;
            }

            Instance._equipmentValueDirty = true;
            Instance._equipmentLabelRefreshPending = true;
            Instance._nextEquipmentLabelRefreshTime = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
        }

        internal static void RequestEquipmentValueHide(float delaySeconds = 0f)
        {
            if (Instance == null)
            {
                return;
            }

            if (delaySeconds <= 0f)
            {
                Instance.SetEquipmentInventoryVisible(false);
                return;
            }

            Instance._equipmentHidePending = true;
            Instance._equipmentHideVersion = Instance._equipmentVisibilityVersion;
            Instance._nextEquipmentHideTime = Time.unscaledTime + delaySeconds;
        }

        internal static void RequestRaidLabelCreate(float delaySeconds = 0.25f)
        {
            if (Instance != null)
            {
                if (Instance._raidLabel != null && Instance._raidLabel.gameObject != null)
                {
                    return;
                }

                if (Instance._raidLabelCreateAttempts >= MaxRaidLabelCreateAttempts)
                {
                    return;
                }

                Instance._raidLabelCreatePending = true;
                Instance._nextRaidLabelCreateTime = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
            }
        }

        internal static void BeginBaselineWarmup(float durationSeconds = 6f)
        {
            if (Instance == null)
            {
                return;
            }

            Instance._baselineWarmupEndTime = Time.unscaledTime + Mathf.Max(0f, durationSeconds);
            Instance._nextBaselineWarmupTime = 0f;
        }

        internal static void RequestRaidItemReconcile(Item item, float delaySeconds = 0.75f)
        {
            if (Instance == null || item == null || !ItemExtensions.RequiresRaidLootRebuildOnChange(item))
            {
                return;
            }

            Instance.AddPendingItemReconcile(item, 0f);
            Instance.AddPendingItemReconcile(item, 0.1f);
            Instance.AddPendingItemReconcile(item, 0.5f);
            Instance.AddPendingItemReconcile(item, 1.5f);
        }

        internal static void RequestRaidValueTextRefresh()
        {
            if (Instance != null)
            {
                Instance.RefreshRaidLabelTextOnly();
            }
        }

        internal static void SetInventoryVisible(bool visible)
        {
            if (Instance == null)
            {
                return;
            }

            if (ValueTracker.IsInRaid)
            {
                SetRaidInventoryVisible(visible);
                return;
            }

            Instance.SetEquipmentInventoryVisible(visible);
        }

        internal static void SetRaidInventoryVisible(bool visible)
        {
            if (Instance == null)
            {
                return;
            }

            if (!ValueTracker.IsInRaid || !visible)
            {
                if (Instance._raidOverlayCanvas != null && Instance._raidOverlayCanvas.gameObject != null)
                {
                    Instance._raidOverlayCanvas.SetActive(false);
                }

                return;
            }

            ValueTracker.RebuildRaidLootValueFromInventory();
            Instance.RefreshRaidLabel();
            if (Instance._raidOverlayCanvas != null && Instance._raidOverlayCanvas.gameObject != null)
            {
                Instance._raidOverlayCanvas.SetActive(true);
            }
        }

        private void SetEquipmentInventoryVisible(bool visible)
        {
            if (!visible)
            {
                _equipmentInventoryVisible = false;
                _equipmentVisibilityVersion++;
                _equipmentHidePending = false;
                _equipmentLabelRefreshPending = false;
                if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
                {
                    _equipmentOverlayCanvas.SetActive(false);
                }

                return;
            }

            _equipmentInventoryVisible = true;
            _equipmentVisibilityVersion++;
            _equipmentHidePending = false;
            _equipmentValueDirty = true;
            RefreshEquipmentLabel();
            RequestEquipmentValueRefresh(0.1f);
            RequestEquipmentValueRefresh(0.5f);
        }

        private void RefreshNow()
        {
            if (ValueTracker.IsInRaid)
            {
                HideLabel(_equipmentLabel);
                if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
                {
                    _equipmentOverlayCanvas.SetActive(false);
                }

                RefreshRaidLabel();
                return;
            }

            HideLabel(_raidLabel);
            if (_raidOverlayCanvas != null && _raidOverlayCanvas.gameObject != null && !ValueTracker.IsInRaid)
            {
                _raidOverlayCanvas.SetActive(false);
            }

            if (_equipmentInventoryVisible)
            {
                RefreshEquipmentLabel();
            }
            else if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
            {
                _equipmentOverlayCanvas.SetActive(false);
            }
        }

        private void RefreshEquipmentLabel()
        {
            if (PluginConfig.ShowEquipmentValue != null && !PluginConfig.ShowEquipmentValue.Value)
            {
                HideLabel(_equipmentLabel);
                return;
            }

            if (!_equipmentInventoryVisible)
            {
                HideLabel(_equipmentLabel);
                return;
            }

            _equipmentLabel = EnsureEquipmentOverlayLabel();

            int value = GetCachedEquipmentValue();
            _equipmentLabel.text = $"Equipment Value: <b>{ItemExtensions.FormatMoneyUi(value)} ₽</b>";
            _equipmentLabel.color = new Color(0.92f, 0.92f, 0.93f, 1f);
            _equipmentBox.SetActive(true);
            _equipmentOverlayCanvas.SetActive(true);
        }

        private void RefreshEquipmentLabelTextOnly()
        {
            if (!_equipmentInventoryVisible ||
                PluginConfig.ShowEquipmentValue != null && !PluginConfig.ShowEquipmentValue.Value)
            {
                HideLabel(_equipmentLabel);
                return;
            }

            if (_equipmentLabel == null || _equipmentLabel.gameObject == null)
            {
                RefreshEquipmentLabel();
                return;
            }

            int value = GetCachedEquipmentValue();
            _equipmentLabel.text = $"Equipment Value: <b>{ItemExtensions.FormatMoneyUi(value)} ₽</b>";
            _equipmentLabel.color = new Color(0.92f, 0.92f, 0.93f, 1f);
            _equipmentBox.SetActive(true);
            _equipmentOverlayCanvas.SetActive(true);
        }

        private void RefreshRaidLabel()
        {
            if (PluginConfig.ShowRaidLootValue != null && !PluginConfig.ShowRaidLootValue.Value)
            {
                HideLabel(_raidLabel);
                return;
            }

            _raidLabelCreateAttempts = 0;
            _raidLabelCreatePending = false;
            _raidLabel = EnsureRaidOverlayLabel();

            int value = ValueTracker.CurrentRaidLootValue;
            _raidLabel.text = $"Loot Value: <b>{ItemExtensions.FormatMoneyUi(value)} ₽</b>";
            _raidLabel.color = GetRaidValueColor(value);
            _raidBox.SetActive(true);
        }

        private void RefreshRaidLabelTextOnly()
        {
            if (PluginConfig.ShowRaidLootValue != null && !PluginConfig.ShowRaidLootValue.Value)
            {
                HideLabel(_raidLabel);
                return;
            }

            if (_raidLabel == null || _raidLabel.gameObject == null)
            {
                RefreshRaidLabel();
                return;
            }

            int value = ValueTracker.CurrentRaidLootValue;
            _raidLabel.text = $"Loot Value: <b>{ItemExtensions.FormatMoneyUi(value)} ₽</b>";
            _raidLabel.color = GetRaidValueColor(value);

            Transform root = _raidLabel.transform.parent;
        }

        private void ProcessPendingItemReconciles()
        {
            if (_pendingItemReconciles.Count == 0)
            {
                return;
            }

            float now = Time.unscaledTime;
            for (int i = _pendingItemReconciles.Count - 1; i >= 0; i--)
            {
                PendingItemReconcile pending = _pendingItemReconciles[i];
                if (pending.ReadyTime > now)
                {
                    continue;
                }

                _pendingItemReconciles.RemoveAt(i);
                ValueTracker.RebuildRaidLootValueFromInventory();
                RefreshRaidLabelTextOnly();
            }
        }

        private void ProcessBaselineWarmup()
        {
            if (!ValueTracker.BaselineWarmupActive)
            {
                return;
            }

            if (Time.unscaledTime >= _baselineWarmupEndTime)
            {
                ValueTracker.WarmupBaselineFromCurrentInventory();
                ValueTracker.EndBaselineWarmup();
                RefreshRaidLabelTextOnly();
                return;
            }

            if (Time.unscaledTime < _nextBaselineWarmupTime)
            {
                return;
            }

            _nextBaselineWarmupTime = Time.unscaledTime + 1f;
            ValueTracker.WarmupBaselineFromCurrentInventory();
            RefreshRaidLabelTextOnly();
        }

        private void AddPendingItemReconcile(Item item, float delaySeconds)
        {
            _pendingItemReconciles.Add(new PendingItemReconcile(
                item,
                Time.unscaledTime + Mathf.Max(0f, delaySeconds)));
        }

        private float GetNextRaidLabelCreateDelay()
        {
            _raidLabelCreateAttempts++;

            if (_raidLabelCreateAttempts <= 1)
            {
                return 3f;
            }

            if (_raidLabelCreateAttempts <= 3)
            {
                return 5f;
            }

            return 10f;
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

        private TextMeshProUGUI EnsureEquipmentOverlayLabel()
        {
            if (_equipmentOverlayCanvas == null || _equipmentOverlayCanvas.gameObject == null)
            {
                _equipmentOverlayCanvas = CreateOverlayCanvas(EquipmentOverlayCanvasObjectName);
            }

            if (_equipmentBox == null || _equipmentBox.gameObject == null)
            {
                _equipmentBox = CreateValueBox(_equipmentOverlayCanvas.transform, EquipmentLabelObjectName);
                _equipmentLabel = _equipmentBox.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_equipmentBox.transform.parent != _equipmentOverlayCanvas.transform)
            {
                _equipmentBox.transform.SetParent(_equipmentOverlayCanvas.transform, false);
            }

            ConfigureOverlayBox(_equipmentBox, _equipmentLabel, new Vector2(68f, 214f), 493f, 50f, 28f);
            return _equipmentLabel;
        }

        private TextMeshProUGUI EnsureRaidOverlayLabel()
        {
            if (_raidOverlayCanvas == null || _raidOverlayCanvas.gameObject == null)
            {
                _raidOverlayCanvas = CreateOverlayCanvas("AvgSellPriceRaidValueCanvas");
            }

            if (_raidBox == null || _raidBox.gameObject == null)
            {
                _raidBox = CreateValueBox(_raidOverlayCanvas.transform, RaidLabelObjectName);
                _raidLabel = _raidBox.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (_raidBox.transform.parent != _raidOverlayCanvas.transform)
            {
                _raidBox.transform.SetParent(_raidOverlayCanvas.transform, false);
            }

            ConfigureOverlayBox(_raidBox, _raidLabel, new Vector2(68f, 214f), 493f, 50f, 28f);

            return _raidLabel;
        }

        private static GameObject CreateOverlayCanvas(string objectName)
        {
            GameObject canvasObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            DontDestroyOnLoad(canvasObject);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
            raycaster.enabled = false;
            canvasObject.SetActive(false);

            return canvasObject;
        }

        private static void ConfigureOverlayBox(
            GameObject box,
            TextMeshProUGUI label,
            Vector2 anchoredPosition,
            float width,
            float height,
            float fontSize)
        {
            RectTransform boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0f);
            boxRect.anchorMax = new Vector2(0f, 0f);
            boxRect.pivot = new Vector2(0f, 0f);
            boxRect.anchoredPosition = anchoredPosition;
            boxRect.sizeDelta = new Vector2(width, height);
            boxRect.localScale = Vector3.one;
            boxRect.localRotation = Quaternion.identity;

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 1f);
            labelRect.offsetMax = new Vector2(-8f, -1f);

            label.fontSize = fontSize;
            label.enableAutoSizing = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.fontStyle = FontStyles.Normal;
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
            _nextEquipmentAnchorSearchTime = 0f;
            _nextRaidAnchorSearchTime = 0f;
            _nextRaidLabelCreateTime = 0f;
            _nextBaselineWarmupTime = 0f;
            _baselineWarmupEndTime = 0f;
            _raidLabelCreatePending = false;
            _raidLabelCreateAttempts = 0;
            _equipmentInventoryVisible = false;
            _equipmentHidePending = false;
            _equipmentLabelRefreshPending = false;
            _pendingItemReconciles.Clear();
            _nextTraderUiScanTime = 0f;
            _isTraderSellUiVisible = false;
            _equipmentValueDirty = true;
            _equipmentVisibilityVersion++;
            _cachedEquipmentValue = -1;
            _pendingRefresh = true;
            if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
            {
                _equipmentOverlayCanvas.SetActive(false);
            }
        }

        private TextMeshProUGUI GetEquipmentAnchor()
        {
            if (IsUsableTextComponent(_equipmentAnchor))
            {
                return _equipmentAnchor;
            }

            if (Time.unscaledTime < _nextEquipmentAnchorSearchTime)
            {
                return null;
            }

            _nextEquipmentAnchorSearchTime = Time.unscaledTime + 2f;
            _equipmentAnchor = FindEquipmentAnchor();
            return _equipmentAnchor;
        }

        private TextMeshProUGUI GetRaidAnchor()
        {
            if (IsUsableTextComponent(_raidAnchor))
            {
                return _raidAnchor;
            }

            if (Time.unscaledTime < _nextRaidAnchorSearchTime)
            {
                return null;
            }

            _nextRaidAnchorSearchTime = Time.unscaledTime + 0.5f;
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

        private sealed class PendingItemReconcile
        {
            public PendingItemReconcile(Item item, float readyTime)
            {
                Item = item;
                ReadyTime = readyTime;
            }

            public Item Item { get; }
            public float ReadyTime { get; }
        }
    }
}




