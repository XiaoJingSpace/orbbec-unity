using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrbbecUnity
{
    static class OrbbecPackage
    {
        const string ConfigRelative = "Config/OrbbecSDKConfig.xml";

        [InitializeOnLoadMethod]
        static void LocateOnLoad()
        {
            ApplyResolvedPaths();
        }

        public static string PackageRoot()
        {
            UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(OrbbecSession).Assembly);
            return info == null ? null : info.resolvedPath;
        }

        public static void ApplyResolvedPaths()
        {
            string root = PackageRoot();
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            OrbbecPaths.ExtensionsDirectory = Path.Combine(root, "Plugins", "x86_64", "extensions");
            OrbbecPaths.ConfigFile = Path.Combine(root, ConfigRelative);
        }

        public static string DesiredPlatform()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.StandaloneLinux64 || target == BuildTarget.StandaloneWindows64)
            {
                return PlayerPlatformName(target);
            }

            return HostPlatformName();
        }

        public static bool IsInstalled(string root, string platformName)
        {
            string directory = Path.Combine(root, "Plugins", "x86_64");
            bool linux = File.Exists(Path.Combine(directory, "libOrbbecSDK.so"));
            bool windows = File.Exists(Path.Combine(directory, "OrbbecSDK.dll"));
            if (platformName == "Linux")
            {
                return linux && !windows;
            }

            return windows && !linux;
        }

        public static string BackupExtensions(string platformName)
        {
            return Path.Combine("Plugins~", platformName, "x86_64", "extensions");
        }

        public static string HostPlatformName()
        {
            return OrbbecPaths.NativePlatformName();
        }

        public static string PlayerPlatformName(BuildTarget platform)
        {
            return platform == BuildTarget.StandaloneLinux64 ? "Linux" : "Windows";
        }
    }

    public sealed class OrbbecPlatformSwitch : IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;

        [InitializeOnLoadMethod]
        static void ApplyOnLoad()
        {
            EditorApplication.delayCall += EnsureInstalledPlatform;
        }

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
        {
            if (restartQueued || Application.isBatchMode || previousTarget == newTarget)
            {
                return;
            }

            if (newTarget != BuildTarget.StandaloneWindows64 && newTarget != BuildTarget.StandaloneLinux64)
            {
                return;
            }

            string previous = previousTarget == BuildTarget.StandaloneWindows64 || previousTarget == BuildTarget.StandaloneLinux64
                ? OrbbecPackage.PlayerPlatformName(previousTarget)
                : OrbbecPackage.HostPlatformName();
            string next = OrbbecPackage.PlayerPlatformName(newTarget);
            if (previous == next)
            {
                return;
            }

            SwitchAndRestart(next);
        }

        static bool restartQueued;

        static void EnsureInstalledPlatform()
        {
            if (restartQueued || Application.isBatchMode)
            {
                return;
            }

            string root = OrbbecPackage.PackageRoot();
            if (string.IsNullOrEmpty(root))
            {
                return;
            }

            string desired = OrbbecPackage.DesiredPlatform();
            if (OrbbecPackage.IsInstalled(root, desired))
            {
                OrbbecPackage.ApplyResolvedPaths();
                ApplyNativePlugins();
                return;
            }

            SwitchAndRestart(desired);
        }

        static void SwitchAndRestart(string platformName)
        {
            string root = OrbbecPackage.PackageRoot();
            string source = Path.Combine(root ?? string.Empty, "Plugins~", platformName, "x86_64");
            if (string.IsNullOrEmpty(root) || !Directory.Exists(source))
            {
                UnityEngine.Debug.LogError("找不到 Orbbec 备份：" + source);
                return;
            }

            restartQueued = true;
            UnityEngine.Debug.Log("正在把 Plugins 换成 " + platformName + "，并重启 Unity。");
            string request = Path.Combine(Path.GetTempPath(), "orbbec-unity-switch.txt");
            File.WriteAllLines(request, new[]
            {
                EditorApplication.applicationPath,
                Path.GetDirectoryName(Application.dataPath),
                root,
                platformName,
                Process.GetCurrentProcess().Id.ToString()
            });
            string script = Path.Combine(Path.GetTempPath(), Application.platform == RuntimePlatform.WindowsEditor
                ? "orbbec-unity-switch.ps1"
                : "orbbec-unity-switch.sh");
            File.WriteAllText(script, Application.platform == RuntimePlatform.WindowsEditor ? WindowsSwitcher : LinuxSwitcher);
            ProcessStartInfo start = Application.platform == RuntimePlatform.WindowsEditor
                ? new ProcessStartInfo("cmd.exe", "/c start \"OrbbecSwitch\" /min powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" \"" + request + "\"")
                : new ProcessStartInfo("/bin/bash", "-c \"setsid /bin/bash '" + script + "' '" + request + "' </dev/null >/dev/null 2>&1 &\"");
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            Process.Start(start);
            EditorApplication.Exit(0);
        }

        const string WindowsSwitcher = @"
$lines = Get-Content -LiteralPath $args[0]
$unity = $lines[0]
$project = $lines[1]
$package = $lines[2]
$platform = $lines[3]
$processId = [int]$lines[4]
Wait-Process -Id $processId -ErrorAction SilentlyContinue
$plugins = Join-Path $package 'Plugins'
$source = Join-Path $package ('Plugins~\' + $platform + '\x86_64')
foreach ($name in @('Windows','Linux','x86_64','Windows.meta','Linux.meta')) {
    $path = Join-Path $plugins $name
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}
Copy-Item -LiteralPath $source -Destination $plugins -Recurse -Force
Start-Process -FilePath $unity -ArgumentList @('-projectPath', $project)
";

        const string LinuxSwitcher = @"
#!/bin/sh
request=""$1""
unity=$(sed -n '1p' ""$request"")
project=$(sed -n '2p' ""$request"")
package=$(sed -n '3p' ""$request"")
platform=$(sed -n '4p' ""$request"")
pid=$(sed -n '5p' ""$request"")
while kill -0 ""$pid"" 2>/dev/null; do sleep 0.5; done
rm -rf ""$package/Plugins/Windows"" ""$package/Plugins/Linux"" ""$package/Plugins/x86_64"" ""$package/Plugins/Windows.meta"" ""$package/Plugins/Linux.meta""
mkdir -p ""$package/Plugins/x86_64""
cp -a ""$package/Plugins~/$platform/x86_64/."" ""$package/Plugins/x86_64/""
""$unity"" -projectPath ""$project"" >/dev/null 2>&1 &
";

        static void ApplyNativePlugins()
        {
            UnityEditor.PackageManager.PackageInfo info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(OrbbecSession).Assembly);
            if (info == null)
            {
                return;
            }

            string prefix = info.assetPath.Replace('\\', '/').TrimEnd('/');
            Configure(prefix + "/Plugins/x86_64/OrbbecSDK.dll", true);
            Configure(prefix + "/Plugins/x86_64/OrbbecGfx.dll", true);
            Configure(prefix + "/Plugins/x86_64/libOrbbecSDK.so", false);
            Configure(prefix + "/Plugins/x86_64/libOrbbecGfx.so", false);
            Disable(prefix + "/Plugins/x86_64/libOrbbecSDK.so.2");
        }

        static void Configure(string assetPath, bool windows)
        {
            PluginImporter importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
            if (importer == null)
            {
                return;
            }

            string os = windows ? "Windows" : "Linux";
            bool aligned = !importer.GetCompatibleWithAnyPlatform()
                && importer.GetCompatibleWithEditor()
                && importer.GetEditorData("OS") == os
                && importer.GetEditorData("CPU") == "x86_64"
                && importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64) == windows
                && importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64) != windows;
            if (aligned)
            {
                return;
            }

            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", os);
            importer.SetEditorData("CPU", "x86_64");
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, windows);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, !windows);
            importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", windows ? "x86_64" : "None");
            importer.SetPlatformData(BuildTarget.StandaloneLinux64, "CPU", windows ? "None" : "x86_64");
            importer.SaveAndReimport();
        }

        static void Disable(string assetPath)
        {
            PluginImporter importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
            if (importer == null || (!importer.GetCompatibleWithAnyPlatform() && !importer.GetCompatibleWithEditor() && !importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64) && !importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64)))
            {
                return;
            }

            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, false);
            importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, false);
            importer.SaveAndReimport();
        }
    }

    sealed class OrbbecBuildCopy : IPreprocessBuildWithReport, IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        static readonly List<string> configPaths = new List<string>();

        public void OnPreprocessBuild(BuildReport report)
        {
            configPaths.Clear();
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!IsPlayerPlatform(report.summary.platform))
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                OrbbecPreview[] previews = root.GetComponentsInChildren<OrbbecPreview>(true);
                for (int i = 0; i < previews.Length; i++)
                {
                    configPaths.Add(previews[i].configPath);
                }
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            try
            {
                if (!IsPlayerPlatform(report.summary.platform))
                {
                    return;
                }

                OrbbecPackage.ApplyResolvedPaths();
                string root = OrbbecPackage.PackageRoot();
                if (string.IsNullOrEmpty(root))
                {
                    return;
                }

                string output = report.summary.outputPath;
                string data = Path.Combine(Path.GetDirectoryName(output) ?? string.Empty, Path.GetFileNameWithoutExtension(output) + "_Data");
                string playerPlugins = Path.Combine(data, "Plugins", "x86_64");
                string platformName = OrbbecPackage.PlayerPlatformName(report.summary.platform);
                CopyDirectory(Path.Combine(root, OrbbecPackage.BackupExtensions(platformName)), Path.Combine(playerPlugins, "extensions"));
                if (report.summary.platform == BuildTarget.StandaloneLinux64)
                {
                    CopyFile(Path.Combine(root, "Plugins~", "Linux", "x86_64", "libOrbbecSDK.so.2"), Path.Combine(playerPlugins, "libOrbbecSDK.so.2"));
                }

                CopyConfigs(Path.Combine(data, "StreamingAssets"), Path.Combine(root, "Config", OrbbecPaths.DefaultConfigFileName));
            }
            finally
            {
                configPaths.Clear();
            }
        }

        static void CopyConfigs(string streamingDirectory, string defaultConfig)
        {
            Directory.CreateDirectory(streamingDirectory);
            if (File.Exists(defaultConfig))
            {
                File.Copy(defaultConfig, Path.Combine(streamingDirectory, OrbbecPaths.DefaultConfigFileName), true);
            }

            string hands = Path.Combine(Path.GetDirectoryName(defaultConfig) ?? string.Empty, OrbbecHands.FileName);
            string handsDestination = Path.Combine(streamingDirectory, OrbbecHands.FileName);
            if (File.Exists(hands) && !File.Exists(handsDestination))
            {
                File.Copy(hands, handsDestination, true);
            }

            string settings = Path.Combine(Path.GetDirectoryName(defaultConfig) ?? string.Empty, OrbbecSettings.FileName);
            string settingsDestination = Path.Combine(streamingDirectory, OrbbecSettings.FileName);
            if (File.Exists(settings) && !File.Exists(settingsDestination))
            {
                File.Copy(settings, settingsDestination, true);
            }

            for (int i = 0; i < configPaths.Count; i++)
            {
                string source = OrbbecPaths.ExistingFile(configPaths[i]);
                if (source == null)
                {
                    continue;
                }

                File.Copy(source, Path.Combine(streamingDirectory, OrbbecPaths.ConfigFileName(configPaths[i])), true);
            }
        }

        static bool IsPlayerPlatform(BuildTarget platform)
        {
            return platform == BuildTarget.StandaloneWindows64 || platform == BuildTarget.StandaloneLinux64;
        }

        static void CopyFile(string source, string destination)
        {
            if (!File.Exists(source))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? string.Empty);
            File.Copy(source, destination, true);
        }

        static void CopyDirectory(string source, string destination)
        {
            if (!Directory.Exists(source))
            {
                return;
            }

            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
                File.Copy(file, target, true);
            }
        }
    }

    [CustomEditor(typeof(OrbbecPreview))]
    sealed class OrbbecPreviewEditor : UnityEditor.Editor
    {
        static readonly (OrbbecPreviewStream stream, string label)[] streams =
        {
            (OrbbecPreviewStream.Color, "彩色"),
            (OrbbecPreviewStream.ColorLeft, "左彩色"),
            (OrbbecPreviewStream.ColorRight, "右彩色"),
            (OrbbecPreviewStream.ColorSideLeft, "侧左彩色"),
            (OrbbecPreviewStream.ColorSideRight, "侧右彩色"),
            (OrbbecPreviewStream.ColorTop, "顶部彩色"),
            (OrbbecPreviewStream.Depth, "深度"),
            (OrbbecPreviewStream.Infrared, "红外"),
            (OrbbecPreviewStream.InfraredLeft, "左红外"),
            (OrbbecPreviewStream.InfraredRight, "右红外"),
            (OrbbecPreviewStream.Confidence, "置信度"),
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "configPath", "outputs", "hand");
            DrawHand();
            DrawConfigPath();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("画面", EditorStyles.boldLabel);
            SerializedProperty outputs = serializedObject.FindProperty("outputs");
            EnsureOutputs(outputs);
            for (int i = 0; i < streams.Length; i++)
            {
                SerializedProperty output = FindOutput(outputs, (int)streams[i].stream);
                SerializedProperty enabled = output.FindPropertyRelative("enabled");
                enabled.boolValue = EditorGUILayout.ToggleLeft(streams[i].label, enabled.boolValue);
                if (!enabled.boolValue)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(output.FindPropertyRelative("image"), new GUIContent("RawImage"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("resolution"), new GUIContent("分辨率"));
                EditorGUILayout.PropertyField(output.FindPropertyRelative("imageStatus"), new GUIContent("状态"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.HelpBox("勾选要显示的画面，并把场景里的 RawImage 拖到对应项。分辨率和状态留空则不写文字。状态放在画面右上角，文字是 Loading、Live、Failed。布局由业务层摆放。", MessageType.None);
            EditorGUILayout.HelpBox("打包后从 StreamingAssets/" + OrbbecPaths.ConfigFileName(serializedObject.FindProperty("configPath").stringValue) + " 读取。留空则使用包内默认配置。", MessageType.None);
            EditorGUILayout.HelpBox("左右手在 StreamingAssets/OrbbecHands.json 里用序列号配置。没填序列号时，枚举序号 0 是左手，1 是右手。", MessageType.None);
            EditorGUILayout.HelpBox("扫描间隔在 StreamingAssets/OrbbecSettings.json 的 scanIntervalSeconds，单位秒。没找到设备会按这个间隔重扫，掉线后同样重扫。", MessageType.None);
            EditorGUILayout.HelpBox("Plugins 只保留 " + TargetLabel() + "。两套备份在 Plugins~。切换 Windows x64 / Linux x64 时会换成对应平台并重启 Unity。", MessageType.None);
            serializedObject.ApplyModifiedProperties();
        }

        static void EnsureOutputs(SerializedProperty outputs)
        {
            for (int i = 0; i < streams.Length; i++)
            {
                if (FindOutput(outputs, (int)streams[i].stream) != null)
                {
                    continue;
                }

                int index = outputs.arraySize;
                outputs.InsertArrayElementAtIndex(index);
                SerializedProperty output = outputs.GetArrayElementAtIndex(index);
                output.FindPropertyRelative("enabled").boolValue = false;
                output.FindPropertyRelative("stream").intValue = (int)streams[i].stream;
                output.FindPropertyRelative("image").objectReferenceValue = null;
                output.FindPropertyRelative("resolution").objectReferenceValue = null;
                output.FindPropertyRelative("imageStatus").objectReferenceValue = null;
            }
        }

        static SerializedProperty FindOutput(SerializedProperty outputs, int stream)
        {
            for (int i = 0; i < outputs.arraySize; i++)
            {
                SerializedProperty output = outputs.GetArrayElementAtIndex(i);
                if (output.FindPropertyRelative("stream").intValue == stream)
                {
                    return output;
                }
            }

            return null;
        }

        void DrawHand()
        {
            SerializedProperty property = serializedObject.FindProperty("hand");
            int selected = property.intValue == (int)OrbbecHand.Right ? 1 : 0;
            int next = EditorGUILayout.Popup("手", selected, new[] { "左手", "右手" });
            property.intValue = next;
        }

        void DrawConfigPath()
        {
            SerializedProperty config = serializedObject.FindProperty("configPath");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(config, new GUIContent("配置文件"));
            if (GUILayout.Button("选择", GUILayout.Width(48)))
            {
                string picked = EditorUtility.OpenFilePanel("选择 Orbbec SDK 配置", ConfigDirectory(), "xml");
                if (!string.IsNullOrEmpty(picked))
                {
                    config.stringValue = ToStoredPath(picked);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        static string TargetLabel()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target == BuildTarget.StandaloneWindows64)
            {
                return "Windows x64";
            }

            if (target == BuildTarget.StandaloneLinux64)
            {
                return "Linux x64";
            }

            return target.ToString();
        }

        static string ConfigDirectory()
        {
            OrbbecPackage.ApplyResolvedPaths();
            string directory = Path.GetDirectoryName(OrbbecPaths.ConfigFile);
            return !string.IsNullOrEmpty(directory) && Directory.Exists(directory) ? directory : Application.dataPath;
        }

        static string ToStoredPath(string picked)
        {
            string full = Path.GetFullPath(picked);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectPrefix = project.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return full.Substring(projectPrefix.Length);
            }

            return full;
        }
    }
}
