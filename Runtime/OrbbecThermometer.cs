using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbbecUnity
{
    public enum OrbbecTemperatureChannel
    {
        Cpu = 0,
        Rgb = 1,
        Infrared = 2,
        InfraredLeft = 3,
        InfraredRight = 4,
    }

    /// <summary>
    /// 一根竖向温度柱。0°C 在底部，100°C 顶满。没有读数时液柱为空。
    /// </summary>
    public sealed class OrbbecThermometer : MonoBehaviour
    {
        public OrbbecPreview preview;
        public OrbbecTemperatureChannel channel;
        public RectTransform fill;
        public Image fillImage;
        public TextMeshProUGUI valueLabel;
        public float minimum = 0f;
        public float maximum = 100f;

        static Sprite glass;
        int shownDigits = int.MinValue;
        bool shownKnown;

        public static Sprite Glass()
        {
            if (glass != null)
            {
                return glass;
            }

            const int width = 16;
            const int height = 48;
            float radius = width * 0.5f;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float centerY = Mathf.Clamp(y + 0.5f, radius, height - radius);
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - centerY;
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            glass = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            glass.hideFlags = HideFlags.HideAndDontSave;
            return glass;
        }

        public static Color Heat(float amount)
        {
            Color cold = new Color(0.28f, 0.58f, 0.98f, 1f);
            Color warm = new Color(0.98f, 0.82f, 0.22f, 1f);
            Color hot = new Color(0.92f, 0.22f, 0.16f, 1f);
            if (amount < 0.5f)
            {
                return Color.Lerp(cold, warm, amount * 2f);
            }

            return Color.Lerp(warm, hot, (amount - 0.5f) * 2f);
        }

        void Update()
        {
            OrbbecDeviceStatus status = preview != null ? preview.Status : null;
            float value = status == null ? float.NaN : Read(status);
            bool known = !float.IsNaN(value) && !float.IsInfinity(value);
            int digits = known ? Mathf.RoundToInt(value) : int.MinValue;
            if (digits == shownDigits && known == shownKnown)
            {
                return;
            }

            shownDigits = digits;
            shownKnown = known;
            float span = Mathf.Max(0.001f, maximum - minimum);
            float amount = known ? Mathf.Clamp01((value - minimum) / span) : 0f;
            if (fill != null)
            {
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(1f, amount);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }

            Color ink = known ? Heat(amount) : new Color(0.75f, 0.82f, 0.88f, 1f);
            if (fillImage != null)
            {
                fillImage.color = ink;
            }

            if (valueLabel != null)
            {
                valueLabel.text = known ? value.ToString("0") : "--";
                valueLabel.color = ink;
            }
        }

        float Read(OrbbecDeviceStatus status)
        {
            if (channel == OrbbecTemperatureChannel.Cpu)
            {
                return status.CpuTemperature;
            }

            if (channel == OrbbecTemperatureChannel.Rgb)
            {
                return status.RgbTemperature;
            }

            if (channel == OrbbecTemperatureChannel.Infrared)
            {
                return status.IrTemperature;
            }

            if (channel == OrbbecTemperatureChannel.InfraredLeft)
            {
                return status.IrLeftTemperature;
            }

            return status.IrRightTemperature;
        }
    }
}
