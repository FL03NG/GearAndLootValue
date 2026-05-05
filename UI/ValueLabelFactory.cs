using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GearAndLootValue
{
    internal sealed partial class ValueDisplayUI
    {
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
            canvas.sortingOrder = string.Equals(
                objectName,
                EquipmentOverlayCanvasObjectName,
                StringComparison.Ordinal)
                ? 50
                : 3000;

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

    }
}