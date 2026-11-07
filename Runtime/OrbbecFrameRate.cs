using TMPro;
using UnityEngine;

namespace OrbbecUnity
{
    /// <summary>
    /// 在画布右上角显示当前帧率。
    /// </summary>
    public sealed class OrbbecFrameRate : MonoBehaviour
    {
        TextMeshProUGUI label;
        float smoothed;

        void Awake()
        {
            TextMeshProUGUI sample = GetComponentInChildren<TextMeshProUGUI>();
            var textObject = new GameObject("FrameRate", typeof(RectTransform));
            textObject.layer = gameObject.layer;
            var rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-16f, -12f);
            rect.sizeDelta = new Vector2(180f, 36f);
            rect.SetAsLastSibling();

            label = textObject.AddComponent<TextMeshProUGUI>();
            if (sample != null)
            {
                label.font = sample.font;
                label.fontSharedMaterial = sample.fontSharedMaterial;
            }

            label.fontSize = 22f;
            label.alignment = TextAlignmentOptions.TopRight;
            label.color = new Color(0.93f, 0.95f, 0.98f, 1f);
            label.raycastTarget = false;
            label.text = "-- fps";
        }

        void Update()
        {
            float instant = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
            smoothed = Mathf.Lerp(smoothed, instant, blend);
            label.text = smoothed.ToString("0") + " fps";
        }
    }
}
