using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Collections;
namespace OrbbecUnity
{
    /// <summary>
    /// 一台设备挂一个组件。勾选要显示的画面，并把业务层准备好的 RawImage 接上来。
    /// </summary>
    public sealed class OrbbecPreview : MonoBehaviour
    {
        [Serializable]
        public sealed class Output
        {
            public bool enabled;
            public OrbbecPreviewStream stream;
            public RawImage image;
            public TextMeshProUGUI resolution;
            public TextMeshProUGUI imageStatus;
        }

        sealed class StreamState
        {
            public Texture2D Texture;
            public int Width;
            public int Height;
            public int Format;
            public ulong FrameIndex;
            public string ResolutionText;
            public bool Logged;
            public bool LoggedIssue;
            public bool Bound;
            public bool Live;
        }

        sealed class SharedTexture
        {
            public Texture2D Texture;
            public int References;
        }

        static readonly Dictionary<int, SharedTexture> sharedTextures = new Dictionary<int, SharedTexture>();

        [FormerlySerializedAs("deviceIndex")]
        [Tooltip("左手或右手。序列号写在 StreamingAssets/OrbbecHands.json，启动时按序列号匹配。")]
        public OrbbecHand hand = OrbbecHand.Left;

        int deviceIndex;
        string resolvedConfig;
        float nextScan;
        float nextStatusLabel;
        string loggedFailure;
        bool loggedGripper;

        [Tooltip("留空时编辑器使用包内 Config/OrbbecSDKConfig.xml。打包后从 StreamingAssets 读取同名文件。")]
        public string configPath;

        [Tooltip("勾选的画面、承接该画面的 RawImage，以及可选的分辨率和状态文本")]
        public Output[] outputs = Array.Empty<Output>();

        [Tooltip("显示设备状态的 TextMeshProUGUI。留空则不写文字")]
        public TextMeshProUGUI statusLabel;

        readonly Dictionary<OrbbecPreviewStream, StreamState> states = new Dictionary<OrbbecPreviewStream, StreamState>();
        readonly List<OrbbecStreamView> streams = new List<OrbbecStreamView>();
        readonly StringBuilder statusBuilder = new StringBuilder(512);

        OrbbecSession session;
        string statusText = "等待启动";
        string errorText = string.Empty;

        public OrbbecDeviceStatus Status => session != null ? session.Status : null;

        public RawImage ImageOf(OrbbecPreviewStream stream)
        {
            if (outputs == null)
            {
                return null;
            }

            for (int i = 0; i < outputs.Length; i++)
            {
                Output output = outputs[i];
                if (output != null && output.enabled && output.stream == stream)
                {
                    return output.image;
                }
            }

            return null;
        }

        //void Start()
        //{
        //    _ = OrbbecGfx.Available;
        //    resolvedConfig = OrbbecPaths.ResolveConfig(configPath);
        //    deviceIndex = (int)hand;
        //    TryConnect();
        //    RefreshStatusLabel();
        //}
        IEnumerator Start()
        {
            // 先等待一帧，让UI有机会渲染
            yield return null;
            // 再等待0.2秒，让UI先稳定显示
            yield return new WaitForSecondsRealtime(0.2f);

            _ = OrbbecGfx.Available;

            resolvedConfig = OrbbecPaths.ResolveConfig(configPath);

            deviceIndex = (int)hand;

            TryConnect();

            RefreshStatusLabel();
        }
        void Update()
        {
            if (session == null)
            {
                if (Time.unscaledTime >= nextScan)
                {
                    TryConnect();
                }

                RefreshStatusLabel();
                return;
            }

            try
            {
                session.Refresh();
                if (session.Status.Offline)
                {
                    Drop(string.IsNullOrEmpty(session.Status.LastError) ? "设备掉线" : session.Status.LastError);
                    return;
                }

                if (session.Status.MagEncoderValid && !loggedGripper)
                {
                    loggedGripper = true;
                    OrbbecLog.Info(HandLabel() + " 夹爪角度 " + session.Status.MagEncoderAngle.ToString("0.0") + "°");
                }

                session.CopyStreams(streams);
                for (int i = 0; i < streams.Count; i++)
                {
                    Show(streams[i]);
                }
            }
            catch (Exception exception)
            {
                Drop(exception.Message);
                return;
            }

            RefreshStatusLabel();
        }

        void TryConnect()
        {
            nextScan = Time.unscaledTime + OrbbecSettings.ScanIntervalSeconds;
            string label = HandLabel();
            try
            {
                string serial = OrbbecHands.SerialOf(hand);
                if (!string.IsNullOrEmpty(serial))
                {
                    if (!OrbbecHands.IsSerialOnline(serial, resolvedConfig))
                    {
                        statusText = label + " 未连接";
                        errorText = string.Empty;
                        NoteFailure(label + " 未发现设备 " + serial + "，每 " + OrbbecSettings.ScanIntervalSeconds.ToString("0.#") + " 秒扫描");
                        return;
                    }

                    deviceIndex = (int)hand;
                    session = OrbbecSession.AcquireSerial(deviceIndex, serial, resolvedConfig);
                }
                else
                {
                    deviceIndex = OrbbecHands.IndexOf(hand, resolvedConfig);
                    session = OrbbecSession.Acquire(deviceIndex, resolvedConfig);
                }

                loggedFailure = null;
                loggedGripper = false;
                errorText = string.Empty;
                ClearLive();
                statusText = "已打开 " + label + " " + session.Status.Name;
                OrbbecDeviceStatus opened = session.Status;
                OrbbecLog.Info(label + " 加速度计 " + MotionText(opened.HasAccel, opened.AccelProfiles) + "，陀螺仪 " + MotionText(opened.HasGyro, opened.GyroProfiles));
            }
            catch (Exception exception)
            {
                session = null;
                statusText = label + " 未连接";
                errorText = exception.Message.IndexOf("没有找到", StringComparison.Ordinal) >= 0 || exception.Message.IndexOf("当前设备数 0", StringComparison.Ordinal) >= 0
                    ? string.Empty
                    : exception.Message;
                NoteFailure(label + " " + exception.Message);
            }
        }

        void Drop(string reason)
        {
            string serial = session != null && session.Status != null ? session.Status.SerialNumber : string.Empty;
            OrbbecLog.Error(HandLabel() + " 掉线 " + serial + " " + reason);
            if (session != null)
            {
                OrbbecSession.Release(session);
                session = null;
            }

            OrbbecHands.InvalidateScan();
            statusText = HandLabel() + " 已掉线";
            errorText = reason ?? string.Empty;
            loggedFailure = null;
            loggedGripper = false;
            ClearLive();
            nextScan = Time.unscaledTime;
            nextStatusLabel = 0f;
            RefreshStatusLabel();
        }

        void NoteFailure(string message)
        {
            if (loggedFailure == message)
            {
                return;
            }

            loggedFailure = message;
            OrbbecLog.Info(message);
        }

        string HandLabel()
        {
            return hand == OrbbecHand.Right ? "右手" : "左手";
        }

        static string MotionText(bool present, int profiles)
        {
            if (!present)
            {
                return "无";
            }

            return profiles > 0 ? "有 " + profiles + " 个流配置" : "有，没有流配置";
        }

        static string GripperText(OrbbecDeviceStatus status)
        {
            if (status == null || !status.MagEncoderPresent)
            {
                return "夹爪 无";
            }

            if (!status.MagEncoderValid)
            {
                return "夹爪 等待";
            }

            string flags = string.Empty;
            if ((status.MagEncoderStatus & 1) != 0)
            {
                flags += " 超速";
            }

            if ((status.MagEncoderStatus & 2) != 0)
            {
                flags += " 磁场弱";
            }

            if ((status.MagEncoderStatus & 4) != 0)
            {
                flags += " 欠压";
            }

            if (flags.Length == 0)
            {
                flags = " 正常";
            }

            return "夹爪 " + status.MagEncoderAngle.ToString("0.0") + "°" + flags;
        }

        static string LinkText(OrbbecDeviceStatus status)
        {
            string kind = status == null ? string.Empty : status.ConnectionType ?? string.Empty;
            if (kind.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
            {
                return "连接 有线 " + kind;
            }

            if (string.Equals(kind, "Ethernet", StringComparison.OrdinalIgnoreCase))
            {
                return "连接 网线";
            }

            if (string.Equals(kind, "GMSL2", StringComparison.OrdinalIgnoreCase))
            {
                return "连接 GMSL2";
            }

            return string.IsNullOrEmpty(kind) ? "连接 未知" : "连接 " + kind;
        }

        static string WifiText(OrbbecDeviceStatus status)
        {
            bool connected = status != null && status.EgoStateValid && (status.EgoStateFlags & (1u << 4)) != 0;
            string name = status == null ? string.Empty : status.WifiName;
            if (connected)
            {
                return string.IsNullOrEmpty(name) ? "Wi-Fi 已连接" : "Wi-Fi 已连接 " + name;
            }

            if (!string.IsNullOrEmpty(name))
            {
                return "Wi-Fi 未连接 已保存 " + name;
            }

            return "Wi-Fi 未连接";
        }

        static string AccelText(OrbbecDeviceStatus status)
        {
            if (status == null || !status.HasAccel)
            {
                return "加速度计 无";
            }

            if (!status.AccelValid)
            {
                return "加速度计 有 " + status.AccelProfiles + " 个流配置";
            }

            return "加速度计 " + status.AccelX.ToString("0.000") + " " + status.AccelY.ToString("0.000") + " " + status.AccelZ.ToString("0.000") + " m/s²";
        }

        static string GyroText(OrbbecDeviceStatus status)
        {
            if (status == null || !status.HasGyro)
            {
                return "陀螺仪 无";
            }

            if (!status.GyroValid)
            {
                return "陀螺仪 有 " + status.GyroProfiles + " 个流配置";
            }

            return "陀螺仪 " + status.GyroX.ToString("0.000") + " " + status.GyroY.ToString("0.000") + " " + status.GyroZ.ToString("0.000") + " rad/s";
        }

        void Show(OrbbecStreamView video)
        {
            if (outputs == null || !OrbbecGfx.Available)
            {
                return;
            }

            bool shown = false;
            for (int i = 0; i < outputs.Length; i++)
            {
                Output output = outputs[i];
                if (output != null && output.enabled && output.stream == video.Stream && output.image != null)
                {
                    shown = true;
                    break;
                }
            }

            if (!shown || video.Width <= 0 || video.Height <= 0)
            {
                return;
            }

            StreamState state = StateOf(video.Stream);
            if (state.ResolutionText == null || state.Width != video.Width || state.Height != video.Height)
            {
                state.ResolutionText = video.Width + "x" + video.Height;
            }

            state.Width = video.Width;
            state.Height = video.Height;
            state.Format = video.SourceFormat;
            state.FrameIndex = video.FrameIndex;
            int slot = OrbbecGfx.Slot(deviceIndex, video.Stream);
            if (slot < 0)
            {
                return;
            }

            if (!sharedTextures.TryGetValue(slot, out SharedTexture shared))
            {
                shared = new SharedTexture();
                sharedTextures.Add(slot, shared);
            }

            if (!state.Bound)
            {
                shared.References++;
                state.Bound = true;
            }

            state.Texture = shared.Texture;
            if (state.Texture != null && (state.Texture.width != video.Width || state.Texture.height != video.Height))
            {
                if (OrbbecGfx.OrbbecGfx_IsIdle(slot) == 0)
                {
                    return;
                }

                Destroy(state.Texture);
                shared.Texture = null;
                state.Texture = null;
            }

            if (state.Texture == null)
            {
                state.Texture = new Texture2D(video.Width, video.Height, TextureFormat.RGB24, false);
                state.Texture.wrapMode = TextureWrapMode.Clamp;
                state.Texture.filterMode = FilterMode.Bilinear;
                state.Texture.Apply(false, false);
                shared.Texture = state.Texture;
            }

            OrbbecGfx.OrbbecGfx_SetTexture(slot, state.Texture.GetNativeTexturePtr());
            if (!state.Logged)
            {
                state.Logged = true;
                OrbbecLog.Info("设备 " + deviceIndex + " 纹理 " + video.Stream + " " + video.Width + "x" + video.Height + " ptr=" + state.Texture.GetNativeTexturePtr());
            }

            if (OrbbecGfx.OrbbecGfx_ConsumeIssue(slot) != 0)
            {
                if (!state.LoggedIssue)
                {
                    state.LoggedIssue = true;
                    OrbbecLog.Info("设备 " + deviceIndex + " " + video.Stream + " 提交底层上传 slot=" + slot);
                }

                GL.IssuePluginEvent(OrbbecGfx.EventFunc, slot);
            }

            Rect uv = new Rect(0f, 1f, 1f, -1f);
            for (int i = 0; i < outputs.Length; i++)
            {
                Output output = outputs[i];
                if (output == null || !output.enabled || output.stream != video.Stream || output.image == null)
                {
                    continue;
                }

                output.image.texture = state.Texture;
                output.image.uvRect = uv;
                output.image.color = Color.white;
                state.Live = true;
                if (output.resolution != null && output.resolution.text != state.ResolutionText)
                {
                    output.resolution.text = state.ResolutionText;
                }

                AspectRatioFitter fitter = output.image.GetComponent<AspectRatioFitter>();
                if (fitter != null && video.Height > 0)
                {
                    fitter.aspectRatio = (float)video.Width / video.Height;
                }
            }
        }

        StreamState StateOf(OrbbecPreviewStream stream)
        {
            if (!states.TryGetValue(stream, out StreamState state))
            {
                state = new StreamState();
                states.Add(stream, state);
            }

            return state;
        }

        void ClearLive()
        {
            foreach (KeyValuePair<OrbbecPreviewStream, StreamState> pair in states)
            {
                pair.Value.Live = false;
            }
        }

        void RefreshImageStatus()
        {
            if (outputs == null)
            {
                return;
            }

            for (int i = 0; i < outputs.Length; i++)
            {
                Output output = outputs[i];
                if (output == null || !output.enabled || output.imageStatus == null)
                {
                    continue;
                }

                string next = ImageStatusText(output.stream);
                if (output.imageStatus.text != next)
                {
                    output.imageStatus.text = next;
                }
            }
        }

        string ImageStatusText(OrbbecPreviewStream stream)
        {
            if (session == null && !string.IsNullOrEmpty(errorText))
            {
                return "Failed";
            }

            if (states.TryGetValue(stream, out StreamState state) && state.Live)
            {
                return "Live";
            }

            return "Loading";
        }

        void RefreshStatusLabel()
        {
            RefreshImageStatus();
            if (statusLabel == null || Time.unscaledTime < nextStatusLabel)
            {
                return;
            }

            nextStatusLabel = Time.unscaledTime + 0.2f;

            statusBuilder.Length = 0;
            statusBuilder.AppendLine(statusText);
            OrbbecDeviceStatus status = Status;
            if (status != null)
            {
                statusBuilder.Append("SDK ").AppendLine(status.SdkVersion);
                statusBuilder.Append(status.Name).Append("  SN ").AppendLine(status.SerialNumber);
                AppendIfPresent("ASIC", string.IsNullOrEmpty(status.AsicSerial) ? status.AsicName : status.AsicSerial);
                if (!string.IsNullOrEmpty(status.AsicSerial) && !string.IsNullOrEmpty(status.AsicName) && status.AsicName != status.AsicSerial)
                {
                    AppendIfPresent("芯片", status.AsicName);
                }

                statusBuilder.Append("PID 0x").Append(status.Pid.ToString("X4"));
                statusBuilder.Append("  VID 0x").Append(status.Vid.ToString("X4")).AppendLine();
                statusBuilder.AppendLine(LinkText(status));
                statusBuilder.AppendLine(WifiText(status));
                statusBuilder.Append("固件 ").Append(status.FirmwareVersion);
                statusBuilder.Append("  硬件 ").AppendLine(status.HardwareVersion);
                AppendIfPresent("子系统固件", status.SubsystemVersion);
                for (int i = 0; i < status.Extensions.Count; i++)
                {
                    AppendIfPresent(ExtensionLabel(status.Extensions[i].Key), status.Extensions[i].Value);
                }

                if (status.EgoStateValid)
                {
                    statusBuilder.Append("工作 ").Append(WorkStateLabel(status.EgoWorkState));
                    statusBuilder.Append("  序号 ").Append(status.EgoSequence);
                    statusBuilder.AppendLine();
                    statusBuilder.Append("标志 ").AppendLine(StateFlagsLabel(status.EgoStateFlags));
                    statusBuilder.Append("故障 ").AppendLine(ErrorFlagsLabel(status.EgoErrorFlags));
                    statusBuilder.Append("存储 ").AppendLine(BytesLabel(status.StorageFreeBytes));
                    if (status.RecordingDurationValid)
                    {
                        statusBuilder.Append("录像 ").Append((status.RecordingDurationMs / 1000f).ToString("0.0")).AppendLine(" 秒");
                    }
                }

                if (status.BatteryValid && status.HasBattery)
                {
                    statusBuilder.Append("电量 ").Append(status.BatteryCapacity);
                    statusBuilder.Append("  电压 ").Append(VoltageLabel(status.BatteryVoltage));
                    statusBuilder.Append("  ").Append(status.BatteryCharging ? "充电" : "未充电");
                    statusBuilder.Append("  电池 ").Append(status.BatteryTemperature).AppendLine("°C");
                }

                AppendTemperatures(status);
                statusBuilder.Append("设备状态 0x").Append(status.DeviceState.ToString("X"));
                statusBuilder.Append("  流 ").AppendLine(status.Streaming ? "开" : "关");
                statusBuilder.AppendLine(AccelText(status));
                statusBuilder.AppendLine(GyroText(status));
                statusBuilder.AppendLine(GripperText(status));
                if (outputs != null)
                {
                    for (int i = 0; i < outputs.Length; i++)
                    {
                        Output output = outputs[i];
                        if (output == null || !output.enabled || !states.TryGetValue(output.stream, out StreamState state) || state.Width <= 0)
                        {
                            continue;
                        }

                        statusBuilder.Append(StreamLabel(output.stream)).Append(' ');
                        statusBuilder.Append(state.Width).Append('x').Append(state.Height);
                        statusBuilder.Append("  帧 ").Append(state.FrameIndex);
                        statusBuilder.Append("  ").Append(FormatLabel(state.Format));
                        statusBuilder.AppendLine();
                    }
                }

                if (!string.IsNullOrEmpty(status.LastError))
                {
                    statusBuilder.Append("错误 ").AppendLine(status.LastError);
                }
            }

            if (!string.IsNullOrEmpty(errorText))
            {
                statusBuilder.Append("错误 ").AppendLine(errorText);
            }

            statusLabel.text = statusBuilder.ToString();
        }

        void AppendIfPresent(string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            statusBuilder.Append(label).Append(' ').AppendLine(value);
        }

        void AppendTemperatures(OrbbecDeviceStatus status)
        {
            AppendTemperatureLine(
                ("CPU", status.CpuTemperature),
                ("激光", status.LdmTemperature),
                ("IR", status.IrTemperature),
                ("左IR", status.IrLeftTemperature),
                ("右IR", status.IrRightTemperature));
            AppendTemperatureLine(
                ("RGB", status.RgbTemperature),
                ("主板", status.MainBoardTemperature),
                ("IMU", status.ImuTemperature),
                ("TEC", status.TecTemperature));
            AppendTemperatureLine(
                ("芯片上", status.ChipTopTemperature),
                ("芯片下", status.ChipBottomTemperature));
        }

        void AppendTemperatureLine(params (string Label, float Value)[] values)
        {
            bool any = false;
            for (int i = 0; i < values.Length; i++)
            {
                if (float.IsNaN(values[i].Value))
                {
                    continue;
                }

                if (!any)
                {
                    statusBuilder.Append("温度");
                    any = true;
                }

                statusBuilder.Append(' ').Append(values[i].Label).Append(' ').Append(values[i].Value.ToString("0.0"));
            }

            if (any)
            {
                statusBuilder.AppendLine();
            }
        }

        static string WorkStateLabel(int state)
        {
            switch (state)
            {
                case 0: return "启动";
                case 1: return "自检";
                case 2: return "就绪";
                case 3: return "录像倒计时";
                case 4: return "录像中";
                case 5: return "停止录像";
                case 6: return "保存中";
                case 7: return "固件升级";
                case 8: return "关机";
                case 9: return "故障";
                case 10: return "录像已保存";
                default: return state.ToString();
            }
        }

        static string StateFlagsLabel(uint flags)
        {
            if (flags == 0)
            {
                return "无";
            }

            var builder = new StringBuilder(64);
            AppendFlag(builder, flags, 1u << 0, "外接电源");
            AppendFlag(builder, flags, 1u << 1, "静音");
            AppendFlag(builder, flags, 1u << 2, "存储卡");
            AppendFlag(builder, flags, 1u << 3, "Wi-Fi配置");
            AppendFlag(builder, flags, 1u << 4, "Wi-Fi");
            AppendFlag(builder, flags, 1u << 5, "蓝牙");
            AppendFlag(builder, flags, 1u << 6, "主机");
            return builder.Length == 0 ? "0x" + flags.ToString("X") : builder.ToString();
        }

        static string ErrorFlagsLabel(ulong flags)
        {
            if (flags == 0)
            {
                return "无";
            }

            var builder = new StringBuilder(96);
            AppendFlag(builder, flags, 1ul << 0, "未知");
            AppendFlag(builder, flags, 1ul << 1, "内部");
            AppendFlag(builder, flags, 1ul << 2, "自检");
            AppendFlag(builder, flags, 1ul << 3, "相机");
            AppendFlag(builder, flags, 1ul << 4, "IMU");
            AppendFlag(builder, flags, 1ul << 5, "同步");
            AppendFlag(builder, flags, 1ul << 6, "麦克风");
            AppendFlag(builder, flags, 1ul << 7, "无存储卡");
            AppendFlag(builder, flags, 1ul << 8, "存储只读");
            AppendFlag(builder, flags, 1ul << 9, "存储不足");
            AppendFlag(builder, flags, 1ul << 10, "存储已满");
            AppendFlag(builder, flags, 1ul << 11, "存储写入");
            AppendFlag(builder, flags, 1ul << 12, "保存失败");
            AppendFlag(builder, flags, 1ul << 13, "电源");
            AppendFlag(builder, flags, 1ul << 14, "过温");
            AppendFlag(builder, flags, 1ul << 15, "Wi-Fi配置");
            AppendFlag(builder, flags, 1ul << 16, "Wi-Fi断开");
            AppendFlag(builder, flags, 1ul << 17, "蓝牙断开");
            AppendFlag(builder, flags, 1ul << 18, "升级失败");
            AppendFlag(builder, flags, 1ul << 19, "录像中断");
            AppendFlag(builder, flags, 1ul << 20, "编码");
            AppendFlag(builder, flags, 1ul << 21, "低电量");
            AppendFlag(builder, flags, 1ul << 22, "电量过低");
            if (builder.Length == 0)
            {
                builder.Append("0x").Append(flags.ToString("X"));
            }

            return builder.ToString();
        }

        static void AppendFlag(StringBuilder builder, uint flags, uint bit, string label)
        {
            if ((flags & bit) == 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(label);
        }

        static void AppendFlag(StringBuilder builder, ulong flags, ulong bit, string label)
        {
            if ((flags & bit) == 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(label);
        }

        static string BytesLabel(ulong bytes)
        {
            const double Gigabyte = 1024d * 1024d * 1024d;
            const double Megabyte = 1024d * 1024d;
            if (bytes >= Gigabyte)
            {
                return (bytes / Gigabyte).ToString("0.0") + " GB";
            }

            if (bytes >= Megabyte)
            {
                return (bytes / Megabyte).ToString("0.0") + " MB";
            }

            return bytes + " B";
        }

        static string VoltageLabel(uint voltage)
        {
            if (voltage >= 100)
            {
                return (voltage / 1000f).ToString("0.00") + "V";
            }

            return voltage.ToString();
        }

        static string ExtensionLabel(string key)
        {
            switch (key)
            {
                case "PresetVer": return "预设版本";
                case "DepthVersion": return "深度版本";
                case "HwType": return "硬件类型";
                case "HwVer": return "扩展硬件";
                case "AppSlot": return "应用槽";
                case "IspFwVer": return "ISP固件";
                case "IspNeedVer": return "ISP需求";
                case "IspChipName": return "ISP芯片";
                case "ProductModel": return "产品型号";
                case "CustCfgVer": return "客户配置";
                case "SubFwVer": return "子固件";
                case "GxVer": return "Gx版本";
                default: return key;
            }
        }

        static string StreamLabel(OrbbecPreviewStream stream)
        {
            switch (stream)
            {
                case OrbbecPreviewStream.Color: return "彩色";
                case OrbbecPreviewStream.Depth: return "深度";
                case OrbbecPreviewStream.Infrared: return "红外";
                case OrbbecPreviewStream.InfraredLeft: return "左红外";
                case OrbbecPreviewStream.InfraredRight: return "右红外";
                case OrbbecPreviewStream.Confidence: return "置信度";
                case OrbbecPreviewStream.ColorLeft: return "左彩色";
                case OrbbecPreviewStream.ColorRight: return "右彩色";
                case OrbbecPreviewStream.ColorSideLeft: return "侧左彩色";
                case OrbbecPreviewStream.ColorSideRight: return "侧右彩色";
                case OrbbecPreviewStream.ColorTop: return "顶彩色";
                default: return stream.ToString();
            }
        }

        static string FormatLabel(int format)
        {
            switch (format)
            {
                case 5: return "MJPEG";
                case 6: return "H.264";
                case 7:
                case 14: return "H.265";
                case 8: return "Y16";
                case 9: return "Y8";
                case 22: return "RGB";
                case 23: return "BGR";
                default: return "格式 " + format;
            }
        }

        void OnDestroy()
        {
            if (session != null)
            {
                OrbbecSession.Release(session);
                session = null;
            }

            foreach (KeyValuePair<OrbbecPreviewStream, StreamState> pair in states)
            {
                if (!pair.Value.Bound)
                {
                    continue;
                }

                int slot = OrbbecGfx.Slot(deviceIndex, pair.Key);
                if (slot < 0 || !sharedTextures.TryGetValue(slot, out SharedTexture shared))
                {
                    continue;
                }

                shared.References--;
                if (shared.References > 0)
                {
                    continue;
                }

                if (shared.Texture != null)
                {
                    Destroy(shared.Texture);
                }

                sharedTextures.Remove(slot);
            }

            states.Clear();
        }
    }
}
