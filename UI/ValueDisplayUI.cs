using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EFT.InventoryLogic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GearAndLootValue
{
    internal sealed partial class ValueDisplayUI : MonoBehaviour
    {
        private const string EquipmentLabelObjectName = "AvgSellPriceEquipmentValue";
        private const string RaidLabelObjectName = "AvgSellPriceLootValue";
        private const string RaidEndLabelObjectName = "AvgSellPriceRaidEndLootValue";
        private const string EquipmentOverlayCanvasObjectName = "AvgSellPriceEquipmentValueCanvas";
        private const float TraderUiScanInterval = 2.5f;
        private const float ValueChangeAnimationDuration = 0.3f;
        private const float RaidEndCountAnimationDuration = 0.95f;
        private const float RaidEndIntroAnimationDuration = 0.18f;
        private const int MaxRaidLabelCreateAttempts = 6;

        private static readonly Regex EquipmentWeightRegex =
            new Regex(@"^\s*\d+([.,]\d+)?\s*kg\s*(/\s*\d+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        private float _nextEquipmentLabelPlacementTime;
        private float _nextEquipmentHideTime;
        private float _nextTraderUiScanTime;
        private bool _raidLabelCreatePending;
        private int _raidLabelCreateAttempts;
        private bool _isTraderSellUiVisible;
        private bool _equipmentInventoryVisible;
        private bool _equipmentBlockedByExternalScreen;
        private bool _equipmentHidePending;
        private bool _equipmentShowPending;
        private bool _equipmentLabelRefreshPending;
        private bool _equipmentValueDirty = true;
        private int _equipmentVisibilityVersion;
        private int _equipmentHideVersion;
        private int _cachedEquipmentValue = -1;
        private TextMeshProUGUI _equipmentAnchor;
        private TextMeshProUGUI _raidAnchor;
        private GameObject _equipmentBox;
        private GameObject _raidBox;
        private GameObject _raidEndBox;
        private GameObject _equipmentOverlayCanvas;
        private GameObject _raidOverlayCanvas;
        private GameObject _raidEndOverlayCanvas;
        private TextMeshProUGUI _equipmentLabel;
        private TextMeshProUGUI _raidLabel;
        private TextMeshProUGUI _raidEndLabel;
        private bool _raidLootRebuildPending;
        private float _nextRaidLootRebuildTime;
        private float _nextEquipmentShowTime;
        private ValueAnimation _equipmentValueAnimation;
        private ValueAnimation _raidValueAnimation;
        private ValueAnimation _raidEndValueAnimation;
        private bool _equipmentValueAnimationReady;
        private bool _raidValueAnimationReady;
        private CanvasGroup _raidEndCanvasGroup;
        private float _raidEndIntroStartTime;
        private bool _raidEndIntroActive;
        private bool _raidEndRewardVisible;
        private int _raidEndRewardTargetValue = -1;

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

        // Unity keeps nudging this UI around, so a bit of babysitting happens here.
        private void Update()
        {
            ProcessValueAnimations();

            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                RefreshVisibleValueLabels();
            }

            if (ValueTracker.IsInRaid)
            {
                _equipmentShowPending = false;
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
                    UpdateRaidLootValueLabel();
                }

                ProcessPendingItemReconciles();
                return;
            }

            if (_equipmentShowPending && Time.unscaledTime >= _nextEquipmentShowTime)
            {
                _equipmentShowPending = false;
                SetEquipmentInventoryVisible(true);
            }

            if (_equipmentLabelRefreshPending && Time.unscaledTime >= _nextEquipmentLabelRefreshTime)
            {
                _equipmentLabelRefreshPending = false;
                UpdateEquipmentValueTextOnly();
            }

            if (_equipmentInventoryVisible &&
                Time.unscaledTime >= _nextEquipmentLabelPlacementTime &&
                (_equipmentLabel == null ||
                 _equipmentLabel.gameObject == null ||
                 IsEquipmentBoxHidden()))
            {
                _nextEquipmentLabelPlacementTime = Time.unscaledTime + 0.25f;
                UpdateEquipmentValueTextOnly();
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

        internal static void RequestEquipmentValueRefreshDebounced(float delaySeconds = 0.35f)
        {
            if (Instance == null)
            {
                return;
            }

            Instance._equipmentValueDirty = true;
            Instance._equipmentLabelRefreshPending = true;
            Instance._nextEquipmentLabelRefreshTime = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
        }

        internal static void RequestAllValueRefresh(float delaySeconds = 0f)
        {
            if (Instance == null)
            {
                return;
            }

            Instance._equipmentValueDirty = true;
            Instance._cachedEquipmentValue = -1;
            Instance._equipmentLabelRefreshPending = true;
            Instance._nextEquipmentLabelRefreshTime = Time.unscaledTime + Mathf.Max(0f, delaySeconds);

            if (ValueTracker.IsInRaid)
            {
                Instance.ScheduleRaidLootRebuild(delaySeconds);
            }
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
            if (Instance == null || item == null || !PmcGearValue.NeedsLootRecountOnChange(item))
            {
                return;
            }

            Instance.ScheduleRaidLootRebuild(delaySeconds);
        }

        internal static void RequestRaidValueTextRefresh()
        {
            if (Instance != null)
            {
                Instance.UpdateRaidLootValueTextOnly();
            }
        }

        internal static void ShowRaidEndLootValue()
        {
            if (Instance != null)
            {
                Instance.RefreshRaidEndLootValue();
            }
        }

        internal static void HideRaidEndLootValueNow()
        {
            if (Instance != null)
            {
                Instance.HideRaidEndLootValue();
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

        internal static void SetEquipmentValueBlocked(bool blocked)
        {
            if (Instance == null)
            {
                return;
            }

            Instance._equipmentBlockedByExternalScreen = blocked;
            if (blocked)
            {
                Instance.SetEquipmentInventoryVisible(false);
            }
        }

        internal static void ShowEquipmentValueForInventory()
        {
            if (Instance == null || ValueTracker.IsInRaid)
            {
                return;
            }

            Instance._equipmentBlockedByExternalScreen = false;
            Instance.SetEquipmentInventoryVisible(true);
        }

        internal static void SetRaidInventoryVisible(bool visible)
        {
            if (Instance == null)
            {
                return;
            }

            if (!ValueTracker.IsInRaid || !visible)
            {
                Instance._raidValueAnimation.Active = false;
                if (Instance._raidOverlayCanvas != null && Instance._raidOverlayCanvas.gameObject != null)
                {
                    Instance._raidOverlayCanvas.SetActive(false);
                }

                return;
            }

            ValueTracker.RebuildRaidLootValueFromInventory();
            Instance.UpdateRaidLootValueLabel();
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
                _equipmentShowPending = false;
                _equipmentLabelRefreshPending = false;
                _equipmentValueAnimation.Active = false;
                if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
                {
                    _equipmentOverlayCanvas.SetActive(false);
                }

                return;
            }

            if (_equipmentBlockedByExternalScreen)
            {
                if (IsScreenThatShouldHideEquipmentValueVisible())
                {
                    _equipmentInventoryVisible = false;
                    HideLabel(_equipmentLabel);
                    if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
                    {
                        _equipmentOverlayCanvas.SetActive(false);
                    }

                    return;
                }

                _equipmentBlockedByExternalScreen = false;
            }

            if (IsScreenThatShouldHideEquipmentValueVisible())
            {
                _equipmentInventoryVisible = false;
                HideLabel(_equipmentLabel);
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
            _nextEquipmentLabelPlacementTime = 0f;
            UpdateEquipmentValueLabel();
            RequestEquipmentValueRefresh(0.1f);
            RequestEquipmentValueRefresh(0.5f);
        }

        private void RefreshVisibleValueLabels()
        {
            if (ValueTracker.IsInRaid)
            {
                HideLabel(_equipmentLabel);
                if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
                {
                    _equipmentOverlayCanvas.SetActive(false);
                }

                UpdateRaidLootValueLabel();
                return;
            }

            HideLabel(_raidLabel);
            if (_raidOverlayCanvas != null && _raidOverlayCanvas.gameObject != null && !ValueTracker.IsInRaid)
            {
                _raidOverlayCanvas.SetActive(false);
            }

            if (_equipmentInventoryVisible)
            {
                UpdateEquipmentValueLabel();
            }
            else if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
            {
                _equipmentOverlayCanvas.SetActive(false);
            }
        }

        private void UpdateEquipmentValueLabel()
        {
            if (IsValueDisplayDisabled() ||
                PluginConfig.ShowEquipmentValue != null && !PluginConfig.ShowEquipmentValue.Value)
            {
                HideLabel(_equipmentLabel);
                return;
            }

            if (_equipmentBlockedByExternalScreen && !IsScreenThatShouldHideEquipmentValueVisible())
            {
                _equipmentBlockedByExternalScreen = false;
            }

            if (!_equipmentInventoryVisible || _equipmentBlockedByExternalScreen)
            {
                _equipmentInventoryVisible = false;
                HideLabel(_equipmentLabel);
                if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
                {
                    _equipmentOverlayCanvas.SetActive(false);
                }

                return;
            }

            _equipmentLabel = EnsureEquipmentOverlayLabel();
            if (_equipmentLabel == null)
            {
                return;
            }

            int value = CachedGearValue();
            StartEquipmentValueAnimation(value);
            _equipmentBox.SetActive(true);
            if (IsEquipmentBoxOnOverlayCanvas())
            {
                _equipmentOverlayCanvas.SetActive(true);
            }
        }

        private void UpdateEquipmentValueTextOnly()
        {
            if (_equipmentBlockedByExternalScreen && !IsScreenThatShouldHideEquipmentValueVisible())
            {
                _equipmentBlockedByExternalScreen = false;
            }

            if (!_equipmentInventoryVisible ||
                _equipmentBlockedByExternalScreen ||
                IsValueDisplayDisabled() ||
                PluginConfig.ShowEquipmentValue != null && !PluginConfig.ShowEquipmentValue.Value)
            {
                _equipmentInventoryVisible = false;
                HideLabel(_equipmentLabel);
                if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
                {
                    _equipmentOverlayCanvas.SetActive(false);
                }

                return;
            }

            if (_equipmentLabel == null || _equipmentLabel.gameObject == null)
            {
                UpdateEquipmentValueLabel();
                return;
            }

            _equipmentLabel = EnsureEquipmentOverlayLabel();
            if (_equipmentLabel == null)
            {
                return;
            }

            int value = CachedGearValue();
            StartEquipmentValueAnimation(value);
            _equipmentBox.SetActive(true);
            if (IsEquipmentBoxOnOverlayCanvas())
            {
                _equipmentOverlayCanvas.SetActive(true);
            }
        }

        private void UpdateRaidLootValueLabel()
        {
            if (IsValueDisplayDisabled() ||
                PluginConfig.ShowRaidLootValue != null && !PluginConfig.ShowRaidLootValue.Value)
            {
                HideLabel(_raidLabel);
                return;
            }

            _raidLabelCreateAttempts = 0;
            _raidLabelCreatePending = false;
            _raidLabel = EnsureRaidOverlayLabel();

            int value = ValueTracker.CurrentRaidLootValue;
            StartRaidValueAnimation(value);
            _raidBox.SetActive(true);
        }

        private void UpdateRaidLootValueTextOnly()
        {
            if (IsValueDisplayDisabled() ||
                PluginConfig.ShowRaidLootValue != null && !PluginConfig.ShowRaidLootValue.Value)
            {
                HideLabel(_raidLabel);
                return;
            }

            if (_raidLabel == null || _raidLabel.gameObject == null)
            {
                UpdateRaidLootValueLabel();
                return;
            }

            int value = ValueTracker.CurrentRaidLootValue;
            StartRaidValueAnimation(value);

            Transform root = _raidLabel.transform.parent;
        }

        private void RefreshRaidEndLootValue()
        {
            if (IsValueDisplayDisabled() ||
                PluginConfig.ShowRaidLootValue != null && !PluginConfig.ShowRaidLootValue.Value)
            {
                HideLabel(_raidEndLabel);
                return;
            }

            int value = ValueTracker.LastRaidLootValue;
            _raidEndLabel = EnsureRaidEndOverlayLabel();
            if (_raidEndLabel == null)
            {
                return;
            }

            StartRaidEndRewardAnimation(value);
            _raidEndBox.SetActive(true);

            if (_raidEndOverlayCanvas != null && _raidEndOverlayCanvas.gameObject != null)
            {
                _raidEndOverlayCanvas.SetActive(true);
            }
        }

        private void HideRaidEndLootValue()
        {
            _raidEndValueAnimation.Active = false;
            _raidEndIntroActive = false;
            _raidEndRewardVisible = false;
            HideLabel(_raidEndLabel);
            if (_raidEndOverlayCanvas != null && _raidEndOverlayCanvas.gameObject != null)
            {
                _raidEndOverlayCanvas.SetActive(false);
            }
        }

        private void StartEquipmentValueAnimation(int targetValue)
        {
            if (ValueAnimationsDisabled())
            {
                _equipmentValueAnimation = ValueAnimation.Completed(targetValue);
                _equipmentValueAnimationReady = true;
                ApplyEquipmentValueText(targetValue);
                return;
            }

            if (!_equipmentValueAnimationReady)
            {
                _equipmentValueAnimationReady = true;
                _equipmentValueAnimation = ValueAnimation.Completed(targetValue);
                ApplyEquipmentValueText(targetValue);
                return;
            }

            StartValueAnimation(ref _equipmentValueAnimation, targetValue, ValueChangeAnimationDuration);
            ApplyEquipmentValueText(_equipmentValueAnimation.DisplayValue);
        }

        private void StartRaidValueAnimation(int targetValue)
        {
            if (ValueAnimationsDisabled())
            {
                _raidValueAnimation = ValueAnimation.Completed(targetValue);
                _raidValueAnimationReady = true;
                ApplyRaidValueText(targetValue);
                return;
            }

            if (!_raidValueAnimationReady)
            {
                _raidValueAnimationReady = true;
                _raidValueAnimation = ValueAnimation.Completed(targetValue);
                ApplyRaidValueText(targetValue);
                return;
            }

            StartValueAnimation(ref _raidValueAnimation, targetValue, ValueChangeAnimationDuration);
            ApplyRaidValueText(_raidValueAnimation.DisplayValue);
        }

        private void StartRaidEndRewardAnimation(int targetValue)
        {
            if (ValueAnimationsDisabled())
            {
                _raidEndRewardVisible = true;
                _raidEndRewardTargetValue = targetValue;
                _raidEndValueAnimation = ValueAnimation.Completed(targetValue);
                _raidEndIntroActive = false;

                if (_raidEndCanvasGroup != null)
                {
                    _raidEndCanvasGroup.alpha = 1f;
                }

                if (_raidEndBox != null)
                {
                    _raidEndBox.transform.localScale = Vector3.one;
                }

                ApplyRaidEndValueText(targetValue);
                return;
            }

            if (_raidEndRewardVisible && _raidEndRewardTargetValue == targetValue)
            {
                ApplyRaidEndValueText(_raidEndValueAnimation.Active
                    ? _raidEndValueAnimation.DisplayValue
                    : targetValue);
                return;
            }

            _raidEndRewardVisible = true;
            _raidEndRewardTargetValue = targetValue;
            _raidEndValueAnimation = new ValueAnimation
            {
                Active = targetValue > 0,
                StartValue = 0,
                TargetValue = targetValue,
                DisplayValue = 0,
                StartTime = Time.unscaledTime,
                Duration = RaidEndCountAnimationDuration
            };

            _raidEndIntroStartTime = Time.unscaledTime;
            _raidEndIntroActive = true;

            if (_raidEndCanvasGroup != null)
            {
                _raidEndCanvasGroup.alpha = 0f;
            }

            if (_raidEndBox != null)
            {
                _raidEndBox.transform.localScale = Vector3.one * 0.96f;
            }

            ApplyRaidEndValueText(0);
        }

        private static void StartValueAnimation(ref ValueAnimation animation, int targetValue, float duration)
        {
            if (animation.TargetValue == targetValue && animation.Active)
            {
                return;
            }

            if (animation.TargetValue == targetValue && !animation.Active)
            {
                animation.DisplayValue = targetValue;
                return;
            }

            animation.StartValue = animation.DisplayValue;
            animation.TargetValue = targetValue;
            animation.StartTime = Time.unscaledTime;
            animation.Duration = duration;
            animation.Active = true;
        }

        private void ProcessValueAnimations()
        {
            if (_equipmentValueAnimation.Active)
            {
                UpdateValueAnimation(ref _equipmentValueAnimation, EaseOutCubic);
                ApplyEquipmentValueText(_equipmentValueAnimation.DisplayValue);
            }

            if (_raidValueAnimation.Active)
            {
                UpdateValueAnimation(ref _raidValueAnimation, EaseOutCubic);
                ApplyRaidValueText(_raidValueAnimation.DisplayValue);
            }

            if (_raidEndValueAnimation.Active)
            {
                UpdateValueAnimation(ref _raidEndValueAnimation, EaseOutCubic);
                ApplyRaidEndValueText(_raidEndValueAnimation.DisplayValue);
            }

            ProcessRaidEndIntroAnimation();
        }

        private static void UpdateValueAnimation(ref ValueAnimation animation, Func<float, float> easing)
        {
            float duration = Mathf.Max(0.01f, animation.Duration);
            float t = Mathf.Clamp01((Time.unscaledTime - animation.StartTime) / duration);
            float eased = easing(t);
            animation.DisplayValue = Mathf.RoundToInt(Mathf.Lerp(animation.StartValue, animation.TargetValue, eased));

            if (t >= 1f)
            {
                animation.DisplayValue = animation.TargetValue;
                animation.Active = false;
            }
        }

        private void ProcessRaidEndIntroAnimation()
        {
            if (!_raidEndIntroActive)
            {
                return;
            }

            float t = Mathf.Clamp01((Time.unscaledTime - _raidEndIntroStartTime) / RaidEndIntroAnimationDuration);
            float eased = EaseOutCubic(t);

            if (_raidEndCanvasGroup != null)
            {
                _raidEndCanvasGroup.alpha = eased;
            }

            if (_raidEndBox != null && _raidEndBox.gameObject != null)
            {
                _raidEndBox.transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, eased);
            }

            if (t >= 1f)
            {
                _raidEndIntroActive = false;
            }
        }

        private void ApplyEquipmentValueText(int value)
        {
            if (_equipmentLabel == null || _equipmentLabel.gameObject == null)
            {
                return;
            }

            string prefix = PluginConfig.ShowEquipmentValueText == null || PluginConfig.ShowEquipmentValueText.Value ? "Equipment Value: " : string.Empty;
            _equipmentLabel.text = $"{prefix}<b>{TarkovItemPrices.FormatMoneyUi(value)} ₽</b>";
            _equipmentLabel.color = new Color(0.92f, 0.92f, 0.93f, 1f);
        }

        private void ApplyRaidValueText(int value)
        {
            if (_raidLabel == null || _raidLabel.gameObject == null)
            {
                return;
            }

            string prefix = PluginConfig.ShowRaidLootValueText == null || PluginConfig.ShowRaidLootValueText.Value ? "Loot Value: " : string.Empty;
            _raidLabel.text = $"{prefix}<b>{TarkovItemPrices.FormatMoneyUi(value)} ₽</b>";
            _raidLabel.color = GetRaidValueColor(value);
        }

        private void ApplyRaidEndValueText(int value)
        {
            if (_raidEndLabel == null || _raidEndLabel.gameObject == null)
            {
                return;
            }

            _raidEndLabel.text = $"Raid Loot: <b>{TarkovItemPrices.FormatMoneyUi(value)} ₽</b>";
            _raidEndLabel.color = GetRaidValueColor(value);
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        private void ProcessPendingItemReconciles()
        {
            if (!_raidLootRebuildPending)
            {
                return;
            }

            if (Time.unscaledTime < _nextRaidLootRebuildTime)
            {
                return;
            }

            _raidLootRebuildPending = false;
            ValueTracker.RebuildRaidLootValueFromInventory();
            UpdateRaidLootValueTextOnly();
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
                UpdateRaidLootValueTextOnly();
                return;
            }

            if (Time.unscaledTime < _nextBaselineWarmupTime)
            {
                return;
            }

            _nextBaselineWarmupTime = Time.unscaledTime + 1f;
            ValueTracker.WarmupBaselineFromCurrentInventory();
            UpdateRaidLootValueTextOnly();
        }

        private void ScheduleRaidLootRebuild(float delaySeconds)
        {
            float readyTime = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
            if (!_raidLootRebuildPending || readyTime < _nextRaidLootRebuildTime)
            {
                _nextRaidLootRebuildTime = readyTime;
            }

            _raidLootRebuildPending = true;
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

        private bool IsEquipmentBoxOnOverlayCanvas()
        {
            return _equipmentOverlayCanvas != null &&
                   _equipmentOverlayCanvas.gameObject != null &&
                   _equipmentBox != null &&
                   _equipmentBox.gameObject != null &&
                   _equipmentBox.transform.parent == _equipmentOverlayCanvas.transform;
        }

        private bool IsEquipmentBoxHidden()
        {
            return _equipmentBox == null ||
                   _equipmentBox.gameObject == null ||
                   !_equipmentBox.activeSelf;
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

        private TextMeshProUGUI EnsureRaidEndOverlayLabel()
        {
            if (_raidEndOverlayCanvas == null || _raidEndOverlayCanvas.gameObject == null)
            {
                _raidEndOverlayCanvas = CreateOverlayCanvas("AvgSellPriceRaidEndValueCanvas");
            }

            if (_raidEndBox == null || _raidEndBox.gameObject == null)
            {
                _raidEndBox = CreateValueBox(_raidEndOverlayCanvas.transform, RaidEndLabelObjectName);
                _raidEndLabel = _raidEndBox.GetComponentInChildren<TextMeshProUGUI>(true);
                _raidEndCanvasGroup = _raidEndBox.GetComponent<CanvasGroup>() ?? _raidEndBox.AddComponent<CanvasGroup>();
            }

            if (_raidEndBox.transform.parent != _raidEndOverlayCanvas.transform)
            {
                _raidEndBox.transform.SetParent(_raidEndOverlayCanvas.transform, false);
            }

            _raidEndCanvasGroup = _raidEndBox.GetComponent<CanvasGroup>() ?? _raidEndBox.AddComponent<CanvasGroup>();
            ConfigureOverlayBox(_raidEndBox, _raidEndLabel, new Vector2(680f, 195f), 560f, 70f, 38f);
            _raidEndLabel.alignment = TextAlignmentOptions.Midline;

            return _raidEndLabel;
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
            _equipmentShowPending = false;
            _equipmentLabelRefreshPending = false;
            _equipmentBlockedByExternalScreen = false;
            _raidLootRebuildPending = false;
            _nextRaidLootRebuildTime = 0f;
            _nextEquipmentShowTime = 0f;
            _nextTraderUiScanTime = 0f;
            _isTraderSellUiVisible = false;
            _equipmentValueDirty = true;
            _equipmentVisibilityVersion++;
            _cachedEquipmentValue = -1;
            _equipmentValueAnimationReady = false;
            _raidValueAnimationReady = false;
            _equipmentValueAnimation.Active = false;
            _raidValueAnimation.Active = false;
            _raidEndValueAnimation.Active = false;
            _raidEndIntroActive = false;
            _raidEndRewardVisible = false;
            _raidEndRewardTargetValue = -1;
            _pendingRefresh = true;
            if (_equipmentOverlayCanvas != null && _equipmentOverlayCanvas.gameObject != null)
            {
                _equipmentOverlayCanvas.SetActive(false);
            }

            if (_raidEndOverlayCanvas != null && _raidEndOverlayCanvas.gameObject != null)
            {
                _raidEndOverlayCanvas.SetActive(false);
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

            _nextEquipmentAnchorSearchTime = Time.unscaledTime + (_equipmentInventoryVisible ? 0.25f : 2f);
            _equipmentAnchor = FindEquipmentAnchor();
            return _equipmentAnchor;
        }

        private static bool IsEquipmentScreenVisible()
        {
            try
            {
                if (IsScreenThatShouldHideEquipmentValueVisible())
                {
                    return false;
                }

                HashSet<string> visibleTexts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>()
                    .Where(IsUsableTextComponent)
                    .Select(text => NormalizeText(text.text))
                    .Where(text => !string.IsNullOrEmpty(text))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                bool hasEquipmentSlots =
                    visibleTexts.Contains("POCKETS") ||
                    visibleTexts.Contains("BACKPACK") ||
                    visibleTexts.Contains("TACTICAL RIG") ||
                    visibleTexts.Contains("ON SLING") ||
                    visibleTexts.Contains("ON BACK");

                bool hasNonEquipmentTab =
                    visibleTexts.Contains("SKILLS") && !hasEquipmentSlots ||
                    visibleTexts.Contains("MAP") && !hasEquipmentSlots ||
                    visibleTexts.Contains("TASKS") && !hasEquipmentSlots ||
                    visibleTexts.Contains("ACHIEVEMENTS") && !hasEquipmentSlots;

                return hasEquipmentSlots && !hasNonEquipmentTab;
            }
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Equipment screen scan failed: {ex.Message}");
                return false;
            }
        }

        private static bool IsScreenThatShouldHideEquipmentValueVisible()
        {
            try
            {
                foreach (TextMeshProUGUI text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                {
                    if (!IsUsableTextComponent(text))
                    {
                        continue;
                    }

                    string normalized = NormalizeText(text.text);
                    if (string.IsNullOrEmpty(normalized))
                    {
                        continue;
                    }

                    if (normalized.IndexOf("DEPLOYING TO LOCATION", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        normalized.IndexOf("Equipment preview", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        normalized.IndexOf("MANNEQUIN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        string.Equals(normalized, "INSURANCE", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "SELECT INSURER", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "SELECT ITEMS", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, "TO INSURE", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] External screen scan failed: {ex.Message}");
                return false;
            }

            return false;
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
            catch (Exception ex)
            {
                Plugin.LogDebug($"[Gear & Loot Value] Trader UI scan failed: {ex.Message}");
                _isTraderSellUiVisible = false;
                return false;
            }
        }

        private int CachedGearValue()
        {
            if (_equipmentValueDirty || _cachedEquipmentValue < 0)
            {
                _cachedEquipmentValue = PmcGearValue.GetPlayerEquipmentValue();
                _equipmentValueDirty = false;
            }

            return _cachedEquipmentValue;
        }

        private struct ValueAnimation
        {
            public bool Active;
            public int StartValue;
            public int TargetValue;
            public int DisplayValue;
            public float StartTime;
            public float Duration;

            public static ValueAnimation Completed(int value)
            {
                return new ValueAnimation
                {
                    Active = false,
                    StartValue = value,
                    TargetValue = value,
                    DisplayValue = value,
                    StartTime = Time.unscaledTime,
                    Duration = 0f
                };
            }
        }

    }
}
