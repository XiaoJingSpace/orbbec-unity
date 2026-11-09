using UnityEngine;
using UnityEngine.UI;

namespace OrbbecUnity
{
    /// <summary>
    /// 模式 A：右边缘是当前时刻，最新点贴在右侧，更早的点按经过的时间向左移动。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OrbbecWaveform : MaskableGraphic
    {
        struct Sample
        {
            public float Time;
            public float X;
            public float Y;
            public float Z;
        }

        [Tooltip("横轴窗口，单位秒。窗口左侧是最早的历史，右侧是现在。")]
        public float windowSeconds = 6f;

        [Tooltip("相邻点间隔超过这个秒数就断开，避免把掉线空档画成一条斜线。")]
        public float gapSeconds = 0.4f;

        [Tooltip("线宽，单位是波形矩形的本地像素。")]
        public float lineThickness = 2.5f;

        [Tooltip("自动纵轴时，跨度不会小于这个值。")]
        public float minimumSpan = 2f;

        [Tooltip("打开后按窗口内的数据缩放，并始终把 0 留在视野里。")]
        public bool autoRange = true;

        public float fixedMin = -20f;
        public float fixedMax = 20f;

        public Color axisColor = new Color(1f, 1f, 1f, 0.22f);
        public Color gridColor = new Color(1f, 1f, 1f, 0.08f);
        public Color[] channelColors =
        {
            new Color(0.95f, 0.33f, 0.38f, 1f),
            new Color(0.35f, 0.86f, 0.55f, 1f),
            new Color(0.38f, 0.67f, 0.98f, 1f),
        };

        Sample[] samples = new Sample[2048];
        int head;
        int count;
        bool meshIdle;

        public float VisibleMin { get; private set; } = -1f;
        public float VisibleMax { get; private set; } = 1f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Clear()
        {
            head = 0;
            count = 0;
            meshIdle = true;
            SetVerticesDirty();
        }

        public void Push(float time, float x, float y, float z)
        {
            if (samples == null || samples.Length == 0)
            {
                samples = new Sample[2048];
            }

            samples[head].Time = time;
            samples[head].X = x;
            samples[head].Y = y;
            samples[head].Z = z;
            head++;
            if (head >= samples.Length)
            {
                head = 0;
            }

            if (count < samples.Length)
            {
                count++;
            }
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Trim(Time.unscaledTime);
            if (count == 0)
            {
                if (meshIdle)
                {
                    return;
                }

                meshIdle = true;
                SetVerticesDirty();
                return;
            }

            meshIdle = false;
            SetVerticesDirty();
        }

        void Trim(float now)
        {
            float keep = Mathf.Max(0.05f, windowSeconds) + 0.05f;
            int length = samples.Length;
            while (count > 0)
            {
                int oldest = head - count;
                oldest %= length;
                if (oldest < 0)
                {
                    oldest += length;
                }

                if (now - samples[oldest].Time <= keep)
                {
                    break;
                }

                count--;
            }
        }

        public void RangeAt(float now, out float min, out float max)
        {
            if (!autoRange)
            {
                min = fixedMin;
                max = Mathf.Max(fixedMax, fixedMin + 0.001f);
                VisibleMin = min;
                VisibleMax = max;
                return;
            }

            float lo = 0f;
            float hi = 0f;
            int start = count == samples.Length ? head : 0;
            for (int i = 0; i < count; i++)
            {
                Sample sample = samples[(start + i) % samples.Length];
                float age = now - sample.Time;
                if (age < 0f || age > windowSeconds)
                {
                    continue;
                }

                Include(ref lo, ref hi, sample.X);
                Include(ref lo, ref hi, sample.Y);
                Include(ref lo, ref hi, sample.Z);
            }

            float span = hi - lo;
            if (span < minimumSpan)
            {
                float mid = (lo + hi) * 0.5f;
                lo = mid - minimumSpan * 0.5f;
                hi = mid + minimumSpan * 0.5f;
            }
            else
            {
                float pad = span * 0.08f;
                lo -= pad;
                hi += pad;
            }

            min = lo;
            max = hi;
            VisibleMin = min;
            VisibleMax = max;
        }

        static void Include(ref float lo, ref float hi, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return;
            }

            if (value < lo)
            {
                lo = value;
            }

            if (value > hi)
            {
                hi = value;
            }
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            float now = Application.isPlaying ? Time.unscaledTime : 0f;
            if (count > 0)
            {
                int newest = head == 0 ? samples.Length - 1 : head - 1;
                if (!Application.isPlaying)
                {
                    now = samples[newest].Time;
                }
            }

            float min;
            float max;
            RangeAt(now, out min, out max);
            DrawGrid(vertexHelper, rect, min, max);
            DrawChannel(vertexHelper, rect, now, min, max, 0);
            DrawChannel(vertexHelper, rect, now, min, max, 1);
            DrawChannel(vertexHelper, rect, now, min, max, 2);
            AddSegment(vertexHelper, new Vector2(rect.xMax - 1f, rect.yMin), new Vector2(rect.xMax - 1f, rect.yMax), 1.5f, axisColor);
        }

        void DrawGrid(VertexHelper vertexHelper, Rect rect, float min, float max)
        {
            for (int i = 0; i <= 4; i++)
            {
                float y = rect.yMin + rect.height * i / 4f;
                AddSegment(vertexHelper, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), 1f, gridColor);
            }

            for (int i = 0; i <= 4; i++)
            {
                float x = rect.xMin + rect.width * i / 4f;
                AddSegment(vertexHelper, new Vector2(x, rect.yMin), new Vector2(x, rect.yMax), 1f, gridColor);
            }

            if (min < 0f && max > 0f)
            {
                float y = rect.yMin + (0f - min) / (max - min) * rect.height;
                AddSegment(vertexHelper, new Vector2(rect.xMin, y), new Vector2(rect.xMax, y), 1.5f, axisColor);
            }
        }

        void DrawChannel(VertexHelper vertexHelper, Rect rect, float now, float min, float max, int channel)
        {
            bool hasPrevious = false;
            float previousTime = 0f;
            Vector2 previous = Vector2.zero;
            int start = count == samples.Length ? head : 0;
            Color color = ChannelColor(channel);
            float span = Mathf.Max(0.001f, max - min);
            float window = Mathf.Max(0.05f, windowSeconds);
            for (int i = 0; i < count; i++)
            {
                Sample sample = samples[(start + i) % samples.Length];
                float age = now - sample.Time;
                if (age < -0.02f || age > window)
                {
                    hasPrevious = false;
                    continue;
                }

                float value = ValueOf(sample, channel);
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    hasPrevious = false;
                    continue;
                }

                float x = rect.xMin + (1f - age / window) * rect.width;
                float y = rect.yMin + (value - min) / span * rect.height;
                Vector2 point = new Vector2(x, y);
                if (hasPrevious && sample.Time - previousTime <= gapSeconds)
                {
                    AddSegment(vertexHelper, previous, point, lineThickness, color);
                }

                previous = point;
                previousTime = sample.Time;
                hasPrevious = true;
            }
        }

        Color ChannelColor(int channel)
        {
            if (channelColors != null && channel < channelColors.Length)
            {
                return channelColors[channel];
            }

            return Color.white;
        }

        static float ValueOf(Sample sample, int channel)
        {
            if (channel == 0)
            {
                return sample.X;
            }

            if (channel == 1)
            {
                return sample.Y;
            }

            return sample.Z;
        }

        static void AddSegment(VertexHelper vertexHelper, Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y / length, direction.x / length) * (thickness * 0.5f);
            int index = vertexHelper.currentVertCount;
            vertexHelper.AddVert(from - normal, color, Vector2.zero);
            vertexHelper.AddVert(from + normal, color, Vector2.zero);
            vertexHelper.AddVert(to + normal, color, Vector2.zero);
            vertexHelper.AddVert(to - normal, color, Vector2.zero);
            vertexHelper.AddTriangle(index, index + 1, index + 2);
            vertexHelper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
