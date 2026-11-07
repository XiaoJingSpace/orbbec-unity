using System;
using System.IO;
using UnityEngine;

namespace OrbbecUnity
{
    /// <summary>
    /// StreamingAssets/OrbbecSettings.json 里的全局运行参数。以后要加的全局项也放在这个文件。
    /// </summary>
    public static class OrbbecSettings
    {
        public const string FileName = "OrbbecSettings.json";

        [Serializable]
        sealed class SettingsFile
        {
            public float scanIntervalSeconds = 3f;
        }

        static bool loaded;
        static float scanInterval = 3f;

        public static float ScanIntervalSeconds
        {
            get
            {
                Ensure();
                return scanInterval;
            }
        }

        static void Ensure()
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            string path = ResolveFile();
            if (path == null)
            {
                OrbbecLog.Info("没有 " + FileName + "，扫描间隔使用 3 秒");
                return;
            }

            try
            {
                SettingsFile file = JsonUtility.FromJson<SettingsFile>(File.ReadAllText(path));
                if (file != null && file.scanIntervalSeconds >= 0.5f)
                {
                    scanInterval = file.scanIntervalSeconds;
                }
            }
            catch (Exception exception)
            {
                OrbbecLog.Error("无法读取 " + path + "：" + exception.Message);
            }

            OrbbecLog.Info("扫描间隔 " + scanInterval.ToString("0.#") + " 秒，配置 " + path);
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
    }
}
