using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace OrbbecUnity
{
    /// <summary>
    /// 定位 extensions 目录和 SDK 配置文件。编辑器优先用手动选择的路径，打包后读 StreamingAssets 里的同名文件。
    /// </summary>
    public static class OrbbecPaths
    {
        public const string DefaultConfigFileName = "OrbbecSDKConfig.xml";

        public static string ExtensionsDirectory;
        public static string ConfigFile;

        public static string ResolveConfig(string requestedPath)
        {
            if (Application.isEditor)
            {
                string selected = ExistingFile(requestedPath);
                if (selected != null)
                {
                    return selected;
                }

                if (IsFile(ConfigFile))
                {
                    return ConfigFile;
                }
            }

            string streaming = Path.Combine(Application.streamingAssetsPath, ConfigFileName(requestedPath));
            if (IsFile(streaming))
            {
                return streaming;
            }

            string selectedOutsideEditor = ExistingFile(requestedPath);
            if (selectedOutsideEditor != null)
            {
                return selectedOutsideEditor;
            }

            if (IsFile(ConfigFile))
            {
                return ConfigFile;
            }

            string besidePlugins = SiblingConfig(ResolveExtensions());
            if (IsFile(besidePlugins))
            {
                return besidePlugins;
            }

            string dataPath = Path.Combine(Application.dataPath, DefaultConfigFileName);
            return IsFile(dataPath) ? dataPath : null;
        }

        public static string ConfigFileName(string requestedPath)
        {
            if (string.IsNullOrEmpty(requestedPath))
            {
                return DefaultConfigFileName;
            }

            string name = Path.GetFileName(requestedPath);
            return string.IsNullOrEmpty(name) ? DefaultConfigFileName : name;
        }

        public static string ExistingFile(string path)
        {
            if (IsFile(path))
            {
                return Path.GetFullPath(path);
            }

            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
            {
                return null;
            }

            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string combined = Path.Combine(project, path);
            return IsFile(combined) ? Path.GetFullPath(combined) : null;
        }

        public static string NativePlatformName()
        {
            return Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor
                ? "Linux"
                : "Windows";
        }

        public static string ResolveExtensions()
        {
            if (IsDirectory(ExtensionsDirectory))
            {
                return ExtensionsDirectory;
            }

            string nativeDirectory = DirectoryOfNativeLibrary();
            string besideLibrary = Path.Combine(nativeDirectory, "extensions");
            if (IsDirectory(besideLibrary))
            {
                return besideLibrary;
            }

            string packaged = PackagedExtensions(nativeDirectory);
            if (IsDirectory(packaged))
            {
                return packaged;
            }

            string playerPlugins = Path.Combine(Application.dataPath, "Plugins", "x86_64", "extensions");
            if (IsDirectory(playerPlugins))
            {
                return playerPlugins;
            }

            return null;
        }

        public static void ApplyExtensionsDirectory()
        {
        }

        static string SiblingConfig(string extensionsDirectory)
        {
            if (string.IsNullOrEmpty(extensionsDirectory))
            {
                return null;
            }

            DirectoryInfo directory = Directory.GetParent(extensionsDirectory);
            for (int i = 0; directory != null && i < 8; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Config", DefaultConfigFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        static string PackagedExtensions(string nativeDirectory)
        {
            if (string.IsNullOrEmpty(nativeDirectory))
            {
                return null;
            }

            DirectoryInfo directory = new DirectoryInfo(nativeDirectory);
            string relative = Path.Combine("Plugins~", NativePlatformName(), "x86_64", "extensions");
            for (int i = 0; directory != null && i < 8; i++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, relative);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        static string DirectoryOfNativeLibrary()
        {
            string packaged = Path.Combine(Application.dataPath, "Plugins", "x86_64");
            if (IsDirectory(packaged))
            {
                return packaged;
            }

            if (Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor)
            {
                return LinuxLibraryDirectory();
            }

            return WindowsLibraryDirectory();
        }

        static string WindowsLibraryDirectory()
        {
            IntPtr module = GetModuleHandle("OrbbecSDK.dll");
            if (module == IntPtr.Zero)
            {
                return string.Empty;
            }

            var buffer = new StringBuilder(1024);
            uint length = GetModuleFileName(module, buffer, buffer.Capacity);
            if (length == 0)
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(buffer.ToString()) ?? string.Empty;
        }

        static string LinuxLibraryDirectory()
        {
            const string mapsPath = "/proc/self/maps";
            if (!File.Exists(mapsPath))
            {
                return string.Empty;
            }

            foreach (string line in File.ReadLines(mapsPath))
            {
                int marker = line.IndexOf("libOrbbecSDK.so", StringComparison.Ordinal);
                if (marker < 0)
                {
                    continue;
                }

                int start = line.LastIndexOf(' ', marker);
                if (start < 0)
                {
                    continue;
                }

                string libraryPath = line.Substring(start + 1).Trim();
                return Path.GetDirectoryName(libraryPath) ?? string.Empty;
            }

            return string.Empty;
        }

        static bool IsFile(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        static bool IsDirectory(string path)
        {
            return !string.IsNullOrEmpty(path) && Directory.Exists(path);
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetModuleFileName(IntPtr module, StringBuilder filename, int size);
    }
}
