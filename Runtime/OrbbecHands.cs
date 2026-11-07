using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace OrbbecUnity
{
    public enum OrbbecHand
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>
    /// 按 StreamingAssets/OrbbecHands.json 里的序列号把左手、右手对到枚举出的设备。
    /// 没填序列号时，序号 0 是左手，序号 1 是右手。
    /// </summary>
    public static class OrbbecHands
    {
        public const string FileName = "OrbbecHands.json";

        [Serializable]
        sealed class HandsFile
        {
            public HandEntry[] devices;
        }

        [Serializable]
        sealed class HandEntry
        {
            public string serialNumber;
            public string hand;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ListedDevice
        {
            public int Index;
            public int Pid;
            public int Vid;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)] public byte[] SerialNumber;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)] public byte[] Name;
        }

        sealed class Listed
        {
            public int Index;
            public string Name;
            public string SerialNumber;
        }

        static readonly object gate = new object();
        static readonly Dictionary<OrbbecHand, int> assigned = new Dictionary<OrbbecHand, int>();
        static readonly Dictionary<OrbbecHand, string> serials = new Dictionary<OrbbecHand, string>();
        static Listed[] cached = Array.Empty<Listed>();
        static string matchedConfig;
        static string cachedConfig;
        static string listedKey;
        static double cachedAt = -1000;
        static bool ready;
        static bool serialsReady;

        public static int IndexOf(OrbbecHand hand, string sdkConfigPath)
        {
            lock (gate)
            {
                string config = sdkConfigPath ?? string.Empty;
                if (!ready || matchedConfig != config)
                {
                    Match(config);
                    matchedConfig = config;
                    ready = true;
                }

                if (!assigned.TryGetValue(hand, out int index))
                {
                    throw new InvalidOperationException(Label(hand) + "没有匹配到设备");
                }

                return index;
            }
        }

        public static void InvalidateScan()
        {
            lock (gate)
            {
                cached = Array.Empty<Listed>();
                cachedAt = -1000;
                ready = false;
                assigned.Clear();
            }
        }

        public static string SerialOf(OrbbecHand hand)
        {
            lock (gate)
            {
                if (!serialsReady)
                {
                    serials.Clear();
                    ReadConfig(serials);
                    serialsReady = true;
                }

                return serials.TryGetValue(hand, out string serial) ? serial : string.Empty;
            }
        }

        public static bool IsSerialOnline(string serial, string sdkConfigPath)
        {
            if (string.IsNullOrEmpty(serial))
            {
                return false;
            }

            lock (gate)
            {
                Listed[] devices = Devices(sdkConfigPath);
                for (int i = 0; i < devices.Length; i++)
                {
                    if (string.Equals(devices[i].SerialNumber, serial, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        static Listed[] Devices(string sdkConfigPath)
        {
            double now = Time.realtimeSinceStartup;
            string config = sdkConfigPath ?? string.Empty;
            if (cached != null && cachedConfig == config && now - cachedAt < 1.0)
            {
                return cached;
            }

            try
            {
                cached = Enumerate(config);
            }
            catch (Exception exception)
            {
                OrbbecLog.Error("扫描设备失败：" + exception.Message);
                cached = Array.Empty<Listed>();
            }

            cachedConfig = config;
            cachedAt = now;
            return cached;
        }

        static void Match(string sdkConfigPath)
        {
            assigned.Clear();
            Listed[] devices = Enumerate(sdkConfigPath);
            if (devices.Length == 0)
            {
                throw new InvalidOperationException("设备序号超出范围，当前设备数 0");
            }

            var wanted = new Dictionary<OrbbecHand, string>();
            ReadConfig(wanted);
            var claimed = new bool[devices.Length];
            OrbbecHand[] hands = { OrbbecHand.Left, OrbbecHand.Right };
            for (int i = 0; i < hands.Length; i++)
            {
                if (!wanted.TryGetValue(hands[i], out string serial) || string.IsNullOrEmpty(serial))
                {
                    continue;
                }

                int found = -1;
                for (int device = 0; device < devices.Length; device++)
                {
                    if (string.Equals(devices[device].SerialNumber, serial, StringComparison.OrdinalIgnoreCase))
                    {
                        found = device;
                        break;
                    }
                }

                if (found < 0)
                {
                    OrbbecLog.Info(Label(hands[i]) + "没有找到序列号 " + serial);
                    continue;
                }

                if (claimed[found])
                {
                    throw new InvalidOperationException("左右手匹配到了同一台设备 " + serial);
                }

                claimed[found] = true;
                assigned[hands[i]] = devices[found].Index;
                OrbbecLog.Info(Label(hands[i]) + " 序列号 " + serial + " 匹配序号 " + devices[found].Index);
            }

            for (int i = 0; i < hands.Length; i++)
            {
                if (assigned.ContainsKey(hands[i]))
                {
                    continue;
                }

                int free = -1;
                for (int device = 0; device < devices.Length; device++)
                {
                    if (!claimed[device])
                    {
                        free = device;
                        break;
                    }
                }

                if (free < 0)
                {
                    continue;
                }

                claimed[free] = true;
                assigned[hands[i]] = devices[free].Index;
                OrbbecLog.Info(Label(hands[i]) + " 未配置序列号，使用序号 " + devices[free].Index);
            }
        }

        static void ReadConfig(Dictionary<OrbbecHand, string> wanted)
        {
            string path = ResolveFile();
            if (path == null)
            {
                OrbbecLog.Info("没有 " + FileName + "，按枚举顺序分配左右手");
                return;
            }

            HandsFile file;
            try
            {
                file = JsonUtility.FromJson<HandsFile>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException("无法读取 " + path + "：" + exception.Message);
            }

            if (file == null || file.devices == null)
            {
                return;
            }

            for (int i = 0; i < file.devices.Length; i++)
            {
                HandEntry entry = file.devices[i];
                if (entry == null || !TryParse(entry.hand, out OrbbecHand hand))
                {
                    continue;
                }

                string serial = entry.serialNumber == null ? string.Empty : entry.serialNumber.Trim();
                wanted[hand] = serial;
            }

            OrbbecLog.Info("读取手部配置 " + path);
        }

        static Listed[] Enumerate(string sdkConfigPath)
        {
            int stride = OrbbecDevice_ListBytes();
            if (stride != Marshal.SizeOf<ListedDevice>())
            {
                throw new InvalidOperationException("设备列表结构大小不一致");
            }

            const int capacity = 8;
            IntPtr buffer = Marshal.AllocHGlobal(stride * capacity);
            try
            {
                var error = new byte[256];
                string extensions = OrbbecPaths.ResolveExtensions() ?? string.Empty;
                int count = OrbbecDevice_List(extensions, sdkConfigPath ?? string.Empty, buffer, capacity, error, error.Length);
                if (count < 0)
                {
                    throw new InvalidOperationException(Text(error));
                }

                if (count > capacity)
                {
                    count = capacity;
                }

                var devices = new Listed[count];
                for (int i = 0; i < count; i++)
                {
                    ListedDevice native = Marshal.PtrToStructure<ListedDevice>(IntPtr.Add(buffer, i * stride));
                    devices[i] = new Listed
                    {
                        Index = native.Index,
                        Name = Text(native.Name),
                        SerialNumber = Text(native.SerialNumber).Trim(),
                    };
                }

                string key = string.Empty;
                for (int i = 0; i < devices.Length; i++)
                {
                    if (key.Length > 0)
                    {
                        key += "，";
                    }

                    key += devices[i].Index + " " + devices[i].Name + " SN " + devices[i].SerialNumber;
                }

                if (key != listedKey)
                {
                    listedKey = key;
                    OrbbecLog.Info(devices.Length == 0 ? "扫描到 0 台设备" : "扫描到 " + devices.Length + " 台：" + key);
                }

                return devices;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static string ResolveFile()
        {
            string streaming = Path.Combine(Application.streamingAssetsPath, FileName);
            if (File.Exists(streaming))
            {
                return streaming;
            }

            string config = OrbbecPaths.ResolveConfig(null);
            if (!string.IsNullOrEmpty(config))
            {
                string beside = Path.Combine(Path.GetDirectoryName(config) ?? string.Empty, FileName);
                if (File.Exists(beside))
                {
                    return beside;
                }
            }

            return null;
        }

        static bool TryParse(string text, out OrbbecHand hand)
        {
            hand = OrbbecHand.Left;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string value = text.Trim();
            if (string.Equals(value, "Left", StringComparison.OrdinalIgnoreCase) || value == "左手")
            {
                hand = OrbbecHand.Left;
                return true;
            }

            if (string.Equals(value, "Right", StringComparison.OrdinalIgnoreCase) || value == "右手")
            {
                hand = OrbbecHand.Right;
                return true;
            }

            return false;
        }

        static string Label(OrbbecHand hand)
        {
            return hand == OrbbecHand.Right ? "右手" : "左手";
        }

        static string Text(byte[] bytes)
        {
            if (bytes == null)
            {
                return string.Empty;
            }

            int end = 0;
            while (end < bytes.Length && bytes[end] != 0)
            {
                end++;
            }

            return end == 0 ? string.Empty : Encoding.UTF8.GetString(bytes, 0, end);
        }

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern int OrbbecDevice_List([MarshalAs(UnmanagedType.LPUTF8Str)] string extensionsDirectory, [MarshalAs(UnmanagedType.LPUTF8Str)] string configPath, IntPtr items, int capacity, byte[] errorMessage, int errorBytes);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        static extern int OrbbecDevice_ListBytes();
    }
}
