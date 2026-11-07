# Orbbec Unity

独立的 Unity Package。通过 Orbbec SDK 读取设备状态和相机画面。

## 安装

在 Unity 工程的 `Packages/manifest.json` 里加上本地路径：

```json
{
  "dependencies": {
    "com.orbbec.unity": "file:C:/path/to/com.orbbec.unity"
  }
}
```

也可以用 Package Manager 的 **Add package from disk**，选这个目录下的 `package.json`。

要求 Windows x64 或 Linux x64，Unity 2021.2 或更新版本。Linux ARM64 不在这个包里。

## 示例

Package Manager 里选中 **Orbbec Unity**，在 Samples 下导入 **设备预览**。打开 `DevicePreview` 场景后点 Play。示例是左右手两台设备：左手 `deviceIndex` 为 0，右手为 1。每台勾选彩色、深度、红外，绑到各自的 RawImage，状态分别写在两块 TextMeshProUGUI 上。

如果之前已经导入过这个示例，先删掉工程里的旧副本，再导入一次。

## 使用

一台设备挂一个 `OrbbecPreview`。在 Inspector 里勾选要显示的画面，勾选后把业务层的 RawImage 拖到该项。组件只把画面写进这个 RawImage，不创建节点，也不排列布局。需要显示状态时，把一个 TextMeshPro - Text (UI) 拖到 `statusLabel`。字体用该组件上的 Font Asset；状态是中文，Font Asset 需要包含中文字形。

左右手是两台设备，就放两个 `OrbbecPreview`，`deviceIndex` 分别对应两台设备的序号。

加速度计、陀螺仪、麦克风、磁力计，以及固件升级、录像和点云，不在这个预览插件里。`配置文件` 可以点选择指定 xml；留空时编辑器使用包内 `Config/OrbbecSDKConfig.xml`。某一路的 RawImage 可以用 `ImageOf` 取到，设备状态读 `Status`。

`Plugins/x86_64` 里只放当前打包平台的一套库。Windows 和 Linux 的完整备份在 `Plugins~`：

- `Plugins~/Windows/x86_64/`
- `Plugins~/Linux/x86_64/`
- `Config/OrbbecSDKConfig.xml`
- Linux USB 权限：`Plugins~/Linux/99-obsensor-libusb.rules`、`Plugins~/Linux/install_udev_rules.sh`

在 Build Settings 里把目标从 Windows x64 改到 Linux x64，或从 Linux x64 改回 Windows x64 时，Unity 会先退出，用 `Plugins~` 里对应平台覆盖 `Plugins/x86_64`，再重新打开工程。打包从 `Plugins~` 复制该平台的扩展库到 `*_Data/Plugins/x86_64/extensions`。Linux 包还会把 `libOrbbecSDK.so.2` 放到 `*_Data/Plugins/x86_64`。默认配置和 Inspector 里选中的配置会复制到 `*_Data/StreamingAssets`，播放器按文件名从那里读取。

Linux 上第一次接设备前，在 `Plugins~/Linux` 目录执行 `sudo sh install_udev_rules.sh`，让当前用户能打开相机。深度扩展库还需要系统里的 `libEGL.so.1`（显卡驱动或 Mesa）。
