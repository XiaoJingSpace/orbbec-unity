using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OrbbecUnity
{
    public enum OrbbecPreviewStream
    {
        Color = 2,
        Depth = 3,
        Infrared = 1,
        InfraredLeft = 6,
        InfraredRight = 7,
        Confidence = 9,
        ColorLeft = 11,
        ColorRight = 12,
        ColorSideLeft = 14,
        ColorSideRight = 15,
        ColorTop = 16,
    }

    public struct OrbbecStreamView
    {
        public OrbbecPreviewStream Stream;
        public int Width;
        public int Height;
        public int SourceFormat;
        public ulong FrameIndex;
    }

    public sealed class OrbbecDeviceStatus
    {
        public int Index;
        public string Name = string.Empty;
        public string SerialNumber = string.Empty;
        public string FirmwareVersion = string.Empty;
        public string HardwareVersion = string.Empty;
        public string ConnectionType = string.Empty;
        public string WifiName = string.Empty;
        public string IpAddress = string.Empty;
        public string Uid = string.Empty;
        public int Pid;
        public int Vid;
        public ulong DeviceState;
        public bool Streaming;
        public string AsicName = string.Empty;
        public string AsicSerial = string.Empty;
        public string SubsystemVersion = string.Empty;
        public readonly List<KeyValuePair<string, string>> Extensions = new List<KeyValuePair<string, string>>();
        public bool EgoStateValid;
        public int EgoWorkState;
        public uint EgoSequence;
        public uint EgoStateFlags;
        public ulong EgoErrorFlags;
        public ulong StorageFreeBytes;
        public bool RecordingDurationValid;
        public ulong RecordingDurationMs;
        public bool BatteryValid;
        public uint BatteryCapacity;
        public uint BatteryVoltage;
        public bool BatteryCharging;
        public int BatteryTemperature;
        public bool HasBattery;
        public float CpuTemperature = float.NaN;
        public float IrTemperature = float.NaN;
        public float LdmTemperature = float.NaN;
        public float MainBoardTemperature = float.NaN;
        public float TecTemperature = float.NaN;
        public float ImuTemperature = float.NaN;
        public float RgbTemperature = float.NaN;
        public float IrLeftTemperature = float.NaN;
        public float IrRightTemperature = float.NaN;
        public float ChipTopTemperature = float.NaN;
        public float ChipBottomTemperature = float.NaN;
        public bool HasAccel;
        public int AccelProfiles;
        public bool HasGyro;
        public int GyroProfiles;
        public bool Offline;
        public bool MagEncoderPresent;
        public bool MagEncoderValid;
        public float MagEncoderAngle;
        public int MagEncoderStatus;
        public ulong MagEncoderTimestampUs;
        public bool AccelValid;
        public float AccelX;
        public float AccelY;
        public float AccelZ;
        public bool GyroValid;
        public float GyroX;
        public float GyroY;
        public float GyroZ;
        public string SdkVersion = string.Empty;
        public string LastError = string.Empty;
    }

    /// <summary>
    /// 上层只打开设备、提交纹理指针、读取状态。取流和格式转换在 OrbbecGfx 里完成。
    /// </summary>
    public sealed class OrbbecSession : IDisposable
    {
        const int StreamCapacity = 16;

        [StructLayout(LayoutKind.Sequential)]
        struct NativeStatusNumbers
        {
            public int Opened;
            public int Streaming;
            public int Pid;
            public int Vid;
            public int EgoValid;
            public int EgoWorkState;
            public int RecordingDurationValid;
            public int BatteryValid;
            public int HasBattery;
            public int BatteryCharging;
            public int BatteryTemperature;
            public int ExtensionCount;
            public int HasAccel;
            public int HasGyro;
            public int AccelProfiles;
            public int GyroProfiles;
            public int Offline;
            public int MagEncoderPresent;
            public int MagEncoderValid;
            public int MagEncoderStatus;
            public int AccelValid;
            public int GyroValid;
            public uint EgoSequence;
            public uint EgoStateFlags;
            public uint BatteryCapacity;
            public uint BatteryVoltage;
            public ulong DeviceState;
            public ulong EgoErrorFlags;
            public ulong StorageFreeBytes;
            public ulong RecordingDurationMs;
            public ulong MagEncoderTimestampUs;
            public float CpuTemperature;
            public float IrTemperature;
            public float LdmTemperature;
            public float MainBoardTemperature;
            public float TecTemperature;
            public float ImuTemperature;
            public float RgbTemperature;
            public float IrLeftTemperature;
            public float IrRightTemperature;
            public float ChipTopTemperature;
            public float ChipBottomTemperature;
            public float MagEncoderAngle;
            public float AccelX;
            public float AccelY;
            public float AccelZ;
            public float GyroX;
            public float GyroY;
            public float GyroZ;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeStatus
        {
            public int Opened;
            public int Streaming;
            public int Pid;
            public int Vid;
            public int EgoValid;
            public int EgoWorkState;
            public int RecordingDurationValid;
            public int BatteryValid;
            public int HasBattery;
            public int BatteryCharging;
            public int BatteryTemperature;
            public int ExtensionCount;
            public int HasAccel;
            public int HasGyro;
            public int AccelProfiles;
            public int GyroProfiles;
            public int Offline;
            public int MagEncoderPresent;
            public int MagEncoderValid;
            public int MagEncoderStatus;
            public int AccelValid;
            public int GyroValid;
            public uint EgoSequence;
            public uint EgoStateFlags;
            public uint BatteryCapacity;
            public uint BatteryVoltage;
            public ulong DeviceState;
            public ulong EgoErrorFlags;
            public ulong StorageFreeBytes;
            public ulong RecordingDurationMs;
            public ulong MagEncoderTimestampUs;
            public float CpuTemperature;
            public float IrTemperature;
            public float LdmTemperature;
            public float MainBoardTemperature;
            public float TecTemperature;
            public float ImuTemperature;
            public float RgbTemperature;
            public float IrLeftTemperature;
            public float IrRightTemperature;
            public float ChipTopTemperature;
            public float ChipBottomTemperature;
            public float MagEncoderAngle;
            public float AccelX;
            public float AccelY;
            public float AccelZ;
            public float GyroX;
            public float GyroY;
            public float GyroZ;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SdkVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] Name;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SerialNumber;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] FirmwareVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] HardwareVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] ConnectionType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] WifiName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] IpAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] Uid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] AsicName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] AsicSerial;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SubsystemVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public byte[] LastError;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 384)] public byte[] ExtensionKey;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 768)] public byte[] ExtensionValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativeStream
        {
            public int Stream;
            public int Width;
            public int Height;
            public int Format;
            public ulong FrameIndex;
        }

        sealed class SharedSession
        {
            public OrbbecSession Session;
            public int References;
        }

        static readonly Dictionary<string, SharedSession> sharedSessions = new Dictionary<string, SharedSession>();
        static readonly object sharedGate = new object();
        bool disposed;
        int textCountdown;
        IntPtr statusMemory;
        int statusMemoryBytes;
        IntPtr streamMemory;
        int streamMemoryBytes;

        public int Index { get; private set; }
        public OrbbecDeviceStatus Status { get; } = new OrbbecDeviceStatus();

        public static OrbbecSession Acquire(int deviceIndex, string configPath)
        {
            lock (sharedGate)
            {
                string key = deviceIndex + "\n" + (configPath ?? string.Empty);
                if (!sharedSessions.TryGetValue(key, out SharedSession shared))
                {
                    var created = new OrbbecSession();
                    created.Open(deviceIndex, configPath);
                    shared = new SharedSession { Session = created };
                    sharedSessions.Add(key, shared);
                }

                shared.References++;
                return shared.Session;
            }
        }

        public static OrbbecSession AcquireSerial(int slot, string serialNumber, string configPath)
        {
            lock (sharedGate)
            {
                string key = "sn\n" + (serialNumber ?? string.Empty) + "\n" + (configPath ?? string.Empty);
                if (!sharedSessions.TryGetValue(key, out SharedSession shared))
                {
                    var created = new OrbbecSession();
                    created.OpenSerial(slot, serialNumber, configPath);
                    shared = new SharedSession { Session = created };
                    sharedSessions.Add(key, shared);
                }

                shared.References++;
                return shared.Session;
            }
        }

        public static void Release(OrbbecSession session)
        {
            if (session == null)
            {
                return;
            }

            lock (sharedGate)
            {
                string found = null;
                foreach (KeyValuePair<string, SharedSession> pair in sharedSessions)
                {
                    if (pair.Value.Session != session)
                    {
                        continue;
                    }

                    pair.Value.References--;
                    if (pair.Value.References <= 0)
                    {
                        found = pair.Key;
                    }

                    break;
                }

                if (found == null)
                {
                    return;
                }

                sharedSessions[found].Session.Dispose();
                sharedSessions.Remove(found);
            }
        }

        public void Refresh()
        {
            if (disposed)
            {
                return;
            }

            int nativeBytes = OrbbecDevice_StatusBytes();
            int managedBytes = Marshal.SizeOf<NativeStatus>();
            if (nativeBytes != managedBytes)
            {
                throw new InvalidOperationException("状态结构大小不一致，原生 " + nativeBytes + "，托管 " + managedBytes);
            }

            int numberBytes = Marshal.SizeOf<NativeStatusNumbers>();
            int textOffset = (int)Marshal.OffsetOf(typeof(NativeStatus), "SdkVersion");
            if (numberBytes != textOffset)
            {
                throw new InvalidOperationException("状态数值区大小不一致");
            }

            IntPtr buffer = TakeMemory(ref statusMemory, ref statusMemoryBytes, nativeBytes);
            if (OrbbecDevice_ReadStatus(Index, buffer) == 0)
            {
                return;
            }

            CopyNumbers(Marshal.PtrToStructure<NativeStatusNumbers>(buffer));
            if (textCountdown <= 0)
            {
                textCountdown = 12;
                CopyText(Marshal.PtrToStructure<NativeStatus>(buffer));
            }
            else
            {
                textCountdown--;
            }
        }

        public void CopyStreams(List<OrbbecStreamView> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            if (disposed)
            {
                return;
            }

            int stride = OrbbecDevice_StreamBytes();
            if (stride != Marshal.SizeOf<NativeStream>())
            {
                throw new InvalidOperationException("画面结构大小不一致");
            }

            IntPtr buffer = TakeMemory(ref streamMemory, ref streamMemoryBytes, stride * StreamCapacity);
            int count = OrbbecDevice_CopyStreams(Index, buffer, StreamCapacity);
            for (int i = 0; i < count; i++)
            {
                NativeStream native = Marshal.PtrToStructure<NativeStream>(IntPtr.Add(buffer, i * stride));
                destination.Add(new OrbbecStreamView
                {
                    Stream = (OrbbecPreviewStream)native.Stream,
                    Width = native.Width,
                    Height = native.Height,
                    SourceFormat = native.Format,
                    FrameIndex = native.FrameIndex,
                });
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            OrbbecDevice_Close(Index);
            FreeMemory(ref statusMemory);
            FreeMemory(ref streamMemory);
        }

        static IntPtr TakeMemory(ref IntPtr memory, ref int capacity, int bytes)
        {
            if (memory == IntPtr.Zero || capacity < bytes)
            {
                FreeMemory(ref memory);
                memory = Marshal.AllocHGlobal(bytes);
                capacity = bytes;
            }

            return memory;
        }

        static void FreeMemory(ref IntPtr memory)
        {
            if (memory == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(memory);
            memory = IntPtr.Zero;
        }

        void Open(int deviceIndex, string configPath)
        {
            Index = deviceIndex;
            var error = new byte[256];
            string extensions = OrbbecPaths.ResolveExtensions() ?? string.Empty;
            string config = configPath ?? string.Empty;
            if (OrbbecDevice_Open(deviceIndex, extensions, config, error, error.Length) == 0)
            {
                throw new InvalidOperationException(Text(error));
            }

            Refresh();
            OrbbecLog.Info("设备 " + deviceIndex + " 已打开 " + Status.Name);
        }

        void OpenSerial(int slot, string serialNumber, string configPath)
        {
            Index = slot;
            var error = new byte[256];
            string extensions = OrbbecPaths.ResolveExtensions() ?? string.Empty;
            string config = configPath ?? string.Empty;
            if (OrbbecDevice_OpenSerial(slot, serialNumber ?? string.Empty, extensions, config, error, error.Length) == 0)
            {
                throw new InvalidOperationException(Text(error));
            }

            Refresh();
            OrbbecLog.Info("设备 " + slot + " 已打开 " + Status.Name + " SN " + Status.SerialNumber);
        }

        void CopyNumbers(NativeStatusNumbers native)
        {
            Status.Index = Index;
            Status.Pid = native.Pid;
            Status.Vid = native.Vid;
            Status.DeviceState = native.DeviceState;
            Status.Streaming = native.Streaming != 0;
            Status.EgoStateValid = native.EgoValid != 0;
            Status.EgoWorkState = native.EgoWorkState;
            Status.EgoSequence = native.EgoSequence;
            Status.EgoStateFlags = native.EgoStateFlags;
            Status.EgoErrorFlags = native.EgoErrorFlags;
            Status.StorageFreeBytes = native.StorageFreeBytes;
            Status.RecordingDurationValid = native.RecordingDurationValid != 0;
            Status.RecordingDurationMs = native.RecordingDurationMs;
            Status.BatteryValid = native.BatteryValid != 0;
            Status.HasBattery = native.HasBattery != 0;
            Status.BatteryCharging = native.BatteryCharging != 0;
            Status.BatteryCapacity = native.BatteryCapacity;
            Status.BatteryVoltage = native.BatteryVoltage;
            Status.BatteryTemperature = native.BatteryTemperature;
            Status.CpuTemperature = native.CpuTemperature;
            Status.IrTemperature = native.IrTemperature;
            Status.LdmTemperature = native.LdmTemperature;
            Status.MainBoardTemperature = native.MainBoardTemperature;
            Status.TecTemperature = native.TecTemperature;
            Status.ImuTemperature = native.ImuTemperature;
            Status.RgbTemperature = native.RgbTemperature;
            Status.IrLeftTemperature = native.IrLeftTemperature;
            Status.IrRightTemperature = native.IrRightTemperature;
            Status.ChipTopTemperature = native.ChipTopTemperature;
            Status.ChipBottomTemperature = native.ChipBottomTemperature;
            Status.HasAccel = native.HasAccel != 0;
            Status.AccelProfiles = native.AccelProfiles;
            Status.HasGyro = native.HasGyro != 0;
            Status.GyroProfiles = native.GyroProfiles;
            Status.Offline = native.Offline != 0;
            Status.MagEncoderPresent = native.MagEncoderPresent != 0;
            Status.MagEncoderValid = native.MagEncoderValid != 0;
            Status.MagEncoderAngle = native.MagEncoderAngle;
            Status.MagEncoderStatus = native.MagEncoderStatus;
            Status.MagEncoderTimestampUs = native.MagEncoderTimestampUs;
            Status.AccelValid = native.AccelValid != 0;
            Status.AccelX = native.AccelX;
            Status.AccelY = native.AccelY;
            Status.AccelZ = native.AccelZ;
            Status.GyroValid = native.GyroValid != 0;
            Status.GyroX = native.GyroX;
            Status.GyroY = native.GyroY;
            Status.GyroZ = native.GyroZ;
        }

        void CopyText(NativeStatus native)
        {
            Status.Name = Text(native.Name);
            Status.SerialNumber = Text(native.SerialNumber);
            Status.FirmwareVersion = Text(native.FirmwareVersion);
            Status.HardwareVersion = Text(native.HardwareVersion);
            Status.ConnectionType = Text(native.ConnectionType);
            Status.WifiName = Text(native.WifiName);
            Status.IpAddress = Text(native.IpAddress);
            Status.Uid = Text(native.Uid);
            Status.AsicName = Text(native.AsicName);
            Status.AsicSerial = Text(native.AsicSerial);
            Status.SubsystemVersion = Text(native.SubsystemVersion);
            Status.SdkVersion = Text(native.SdkVersion);
            Status.LastError = Text(native.LastError);
            Status.Extensions.Clear();
            int count = native.ExtensionCount;
            if (count < 0)
            {
                count = 0;
            }

            if (count > 12)
            {
                count = 12;
            }

            for (int i = 0; i < count; i++)
            {
                string key = Slice(native.ExtensionKey, i * 32, 32);
                string value = Slice(native.ExtensionValue, i * 64, 64);
                if (key.Length == 0 || value.Length == 0)
                {
                    continue;
                }

                Status.Extensions.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        static string Text(byte[] bytes)
        {
            return Slice(bytes, 0, bytes == null ? 0 : bytes.Length);
        }

        static string Slice(byte[] bytes, int offset, int length)
        {
            if (bytes == null || length <= 0 || offset >= bytes.Length)
            {
                return string.Empty;
            }

            int end = offset;
            int limit = Math.Min(bytes.Length, offset + length);
            while (end < limit && bytes[end] != 0)
            {
                end++;
            }

            return end == offset ? string.Empty : Encoding.UTF8.GetString(bytes, offset, end - offset);
        }

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern int OrbbecDevice_Open(int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string extensionsDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string configPath, byte[] errorMessage, int errorBytes);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern int OrbbecDevice_OpenSerial(int index, [MarshalAs(UnmanagedType.LPUTF8Str)] string serialNumber, [MarshalAs(UnmanagedType.LPUTF8Str)] string extensionsDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string configPath, byte[] errorMessage, int errorBytes);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern void OrbbecDevice_Close(int index);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern int OrbbecDevice_ReadStatus(int index, IntPtr status);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern int OrbbecDevice_CopyStreams(int index, IntPtr items, int capacity);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern int OrbbecDevice_StatusBytes();

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern int OrbbecDevice_StreamBytes();
    }
}
