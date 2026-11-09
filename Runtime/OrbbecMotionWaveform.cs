using TMPro;
using UnityEngine;

namespace OrbbecUnity
{
    public enum OrbbecMotionSource
    {
        Accelerometer = 0,
        Gyroscope = 1,
    }

    /// <summary>
    /// 从 OrbbecPreview 的状态里取加速度或角速度，写进右侧为最新点的波形。
    /// </summary>
    public sealed class OrbbecMotionWaveform : MonoBehaviour
    {
        public OrbbecPreview preview;
        public OrbbecMotionSource source;
        public OrbbecWaveform waveform;
        public TextMeshProUGUI readout;
        public TextMeshProUGUI scaleMax;
        public TextMeshProUGUI scaleMin;

        void Awake()
        {
            if (waveform == null)
            {
                return;
            }

            if (source == OrbbecMotionSource.Accelerometer)
            {
                waveform.minimumSpan = 4f;
            }
            else
            {
                waveform.minimumSpan = 1f;
            }
        }

        void Update()
        {
            OrbbecDeviceStatus status = preview != null ? preview.Status : null;
            bool accelerometer = source == OrbbecMotionSource.Accelerometer;
            bool valid = status != null && !status.Offline && (accelerometer ? status.AccelValid : status.GyroValid);
            if (!valid || waveform == null)
            {
                WriteReadout(WaitingText(status));
                return;
            }

            float x = accelerometer ? status.AccelX : status.GyroX;
            float y = accelerometer ? status.AccelY : status.GyroY;
            float z = accelerometer ? status.AccelZ : status.GyroZ;
            waveform.Push(Time.unscaledTime, x, y, z);
            WriteReadout(ValueText(x, y, z));
            float min;
            float max;
            waveform.RangeAt(Time.unscaledTime, out min, out max);
            if (scaleMax != null)
            {
                scaleMax.text = max.ToString("0.00");
            }

            if (scaleMin != null)
            {
                scaleMin.text = min.ToString("0.00");
            }
        }

        string WaitingText(OrbbecDeviceStatus status)
        {
            if (status != null && status.Offline)
            {
                return Title() + "  设备离线";
            }

            if (status == null)
            {
                return Title() + "  等待设备";
            }

            return Title() + "  等待数据";
        }

        string ValueText(float x, float y, float z)
        {
            return "<color=#F25461>X " + x.ToString("0.000") + "</color>   <color=#59DB8C>Y " + y.ToString("0.000") + "</color>   <color=#61ABFA>Z " + z.ToString("0.000") + "</color>  " + Unit();
        }

        void WriteReadout(string value)
        {
            if (readout != null)
            {
                readout.text = value;
            }
        }

        string Title()
        {
            return source == OrbbecMotionSource.Accelerometer ? "加速度计" : "陀螺仪";
        }

        string Unit()
        {
            return source == OrbbecMotionSource.Accelerometer ? "m/s²" : "rad/s";
        }
    }
}
