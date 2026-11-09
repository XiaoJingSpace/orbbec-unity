using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OrbbecUnity
{
    /// <summary>
    /// 状态栏底部：上面陀螺仪，下面加速度计。右侧只画 CPU，以及这只手勾选打开的相机温度。
    /// </summary>
    public sealed class OrbbecHandMotionCharts : MonoBehaviour
    {
        const float ColumnWidth = 22f;
        const float TubeWidth = 8f;

        struct TemperatureColumn
        {
            public OrbbecTemperatureChannel Channel;
            public string Caption;
        }

        public OrbbecPreview preview;

        void Awake()
        {
            if (transform.Find("波形") != null)
            {
                return;
            }

            TemperatureColumn[] columns = Columns();
            float thermometerWidth = Mathf.Max(ColumnWidth, columns.Length * ColumnWidth) + 8f;
            GameObject charts = CreatePanel("波形", transform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-thermometerWidth, 0f));
            CreateChart(charts.transform, "陀螺仪", OrbbecMotionSource.Gyroscope, new Vector2(0f, 0.5f), Vector2.one);
            CreateChart(charts.transform, "加速度计", OrbbecMotionSource.Accelerometer, Vector2.zero, new Vector2(1f, 0.5f));
            CreateThermometers(columns, thermometerWidth);
        }

        void CreateChart(Transform parent, string title, OrbbecMotionSource source, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject panel = CreatePanel(title, parent, anchorMin, anchorMax, new Vector2(0f, source == OrbbecMotionSource.Gyroscope ? 2f : 0f), new Vector2(0f, source == OrbbecMotionSource.Gyroscope ? 0f : -2f));

            Color label = new Color(0.75f, 0.82f, 0.88f, 1f);
            TextMeshProUGUI titleText = CreateText(panel.transform, "Title", title, 13, TextAlignmentOptions.MidlineLeft, label);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(2f, -16f);
            titleRect.offsetMax = new Vector2(-52f, 0f);

            string unit = source == OrbbecMotionSource.Accelerometer ? "m/s2" : "rad/s";
            TextMeshProUGUI unitText = CreateText(panel.transform, "Unit", unit, 11, TextAlignmentOptions.MidlineRight, label);
            RectTransform unitRect = unitText.rectTransform;
            unitRect.anchorMin = new Vector2(1f, 1f);
            unitRect.anchorMax = new Vector2(1f, 1f);
            unitRect.pivot = new Vector2(1f, 1f);
            unitRect.anchoredPosition = new Vector2(-2f, 0f);
            unitRect.sizeDelta = new Vector2(48f, 16f);

            TextMeshProUGUI readout = CreateText(panel.transform, "Readout", "等待设备", 12, TextAlignmentOptions.MidlineLeft, label);
            readout.overflowMode = TextOverflowModes.Ellipsis;
            RectTransform readoutRect = readout.rectTransform;
            readoutRect.anchorMin = new Vector2(0f, 1f);
            readoutRect.anchorMax = new Vector2(1f, 1f);
            readoutRect.offsetMin = new Vector2(2f, -34f);
            readoutRect.offsetMax = new Vector2(-2f, -16f);

            GameObject plotObject = CreatePanel("Waveform", panel.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, -36f));
            OrbbecWaveform waveform = plotObject.AddComponent<OrbbecWaveform>();
            waveform.color = Color.white;
            waveform.windowSeconds = 6f;
            waveform.lineThickness = 1.75f;
            waveform.minimumSpan = source == OrbbecMotionSource.Accelerometer ? 4f : 1f;

            OrbbecMotionWaveform motion = panel.AddComponent<OrbbecMotionWaveform>();
            motion.preview = preview;
            motion.source = source;
            motion.waveform = waveform;
            motion.readout = readout;
        }

        TemperatureColumn[] Columns()
        {
            TemperatureColumn[] found = new TemperatureColumn[5];
            int count = 0;
            found[count++] = new TemperatureColumn { Channel = OrbbecTemperatureChannel.Cpu, Caption = "CPU" };
            if (ColorEnabled())
            {
                found[count++] = new TemperatureColumn { Channel = OrbbecTemperatureChannel.Rgb, Caption = "彩" };
            }

            if (StreamEnabled(OrbbecPreviewStream.Infrared))
            {
                found[count++] = new TemperatureColumn { Channel = OrbbecTemperatureChannel.Infrared, Caption = "IR" };
            }

            if (StreamEnabled(OrbbecPreviewStream.InfraredLeft))
            {
                found[count++] = new TemperatureColumn { Channel = OrbbecTemperatureChannel.InfraredLeft, Caption = "左" };
            }

            if (StreamEnabled(OrbbecPreviewStream.InfraredRight))
            {
                found[count++] = new TemperatureColumn { Channel = OrbbecTemperatureChannel.InfraredRight, Caption = "右" };
            }

            TemperatureColumn[] columns = new TemperatureColumn[count];
            for (int i = 0; i < count; i++)
            {
                columns[i] = found[i];
            }

            return columns;
        }

        bool ColorEnabled()
        {
            return StreamEnabled(OrbbecPreviewStream.Color)
                || StreamEnabled(OrbbecPreviewStream.ColorLeft)
                || StreamEnabled(OrbbecPreviewStream.ColorRight)
                || StreamEnabled(OrbbecPreviewStream.ColorSideLeft)
                || StreamEnabled(OrbbecPreviewStream.ColorSideRight)
                || StreamEnabled(OrbbecPreviewStream.ColorTop);
        }

        bool StreamEnabled(OrbbecPreviewStream stream)
        {
            if (preview == null || preview.outputs == null)
            {
                return false;
            }

            for (int i = 0; i < preview.outputs.Length; i++)
            {
                OrbbecPreview.Output output = preview.outputs[i];
                if (output != null && output.enabled && output.stream == stream)
                {
                    return true;
                }
            }

            return false;
        }

        void CreateThermometers(TemperatureColumn[] columns, float thermometerWidth)
        {
            GameObject strip = CreatePanel("温度", transform, new Vector2(1f, 0f), Vector2.one, new Vector2(-thermometerWidth + 4f, 0f), new Vector2(-2f, 0f));
            int count = columns.Length;
            for (int i = 0; i < count; i++)
            {
                float left = (float)i / count;
                float right = (float)(i + 1) / count;
                CreateThermometer(strip.transform, columns[i].Channel, columns[i].Caption, left, right);
            }
        }

        void CreateThermometer(Transform parent, OrbbecTemperatureChannel channel, string caption, float left, float right)
        {
            Color label = new Color(0.75f, 0.82f, 0.88f, 1f);
            GameObject column = CreatePanel(caption, parent, new Vector2(left, 0f), new Vector2(right, 1f), new Vector2(1f, 0f), new Vector2(-1f, 0f));

            TextMeshProUGUI valueLabel = CreateText(column.transform, "Value", "--", 10, TextAlignmentOptions.Center, label);
            RectTransform valueRect = valueLabel.rectTransform;
            valueRect.anchorMin = new Vector2(0f, 1f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.pivot = new Vector2(0.5f, 1f);
            valueRect.anchoredPosition = Vector2.zero;
            valueRect.sizeDelta = new Vector2(0f, 14f);

            TextMeshProUGUI nameLabel = CreateText(column.transform, "Name", caption, 10, TextAlignmentOptions.Center, label);
            RectTransform nameRect = nameLabel.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(0f, 14f);

            GameObject scale = CreatePanel("Scale", column.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-13f, 16f), new Vector2(-5f, -16f));
            for (int mark = 0; mark <= 4; mark++)
            {
                float unit = mark / 4f;
                GameObject tick = CreatePanel("Tick", scale.transform, new Vector2(0f, unit), new Vector2(0f, unit), Vector2.zero, Vector2.zero);
                RectTransform tickRect = tick.GetComponent<RectTransform>();
                tickRect.pivot = new Vector2(0f, 0.5f);
                tickRect.sizeDelta = new Vector2(mark % 2 == 0 ? 6f : 4f, 1f);
                Image tickImage = tick.AddComponent<Image>();
                tickImage.color = new Color(1f, 1f, 1f, mark % 2 == 0 ? 0.55f : 0.28f);
                tickImage.raycastTarget = false;
            }

            GameObject trackObject = CreatePanel("Track", column.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(-TubeWidth * 0.5f, 16f), new Vector2(TubeWidth * 0.5f, -16f));
            Image track = trackObject.AddComponent<Image>();
            track.sprite = OrbbecThermometer.Glass();
            track.type = Image.Type.Sliced;
            track.color = new Color(1f, 1f, 1f, 0.14f);
            track.raycastTarget = false;
            Mask mask = trackObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            GameObject fillObject = CreatePanel("Fill", trackObject.transform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            Image fill = fillObject.AddComponent<Image>();
            fill.color = OrbbecThermometer.Heat(0f);
            fill.raycastTarget = false;

            OrbbecThermometer thermometer = column.AddComponent<OrbbecThermometer>();
            thermometer.preview = preview;
            thermometer.channel = channel;
            thermometer.fill = fillObject.GetComponent<RectTransform>();
            thermometer.fillImage = fill;
            thermometer.valueLabel = valueLabel;
        }

        static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return item;
        }

        TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment, Color color)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            TextMeshProUGUI text = item.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset face = preview != null && preview.statusLabel != null ? preview.statusLabel.font : TMP_Settings.defaultFontAsset;
            if (face != null)
            {
                text.font = face;
            }

            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.richText = true;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
