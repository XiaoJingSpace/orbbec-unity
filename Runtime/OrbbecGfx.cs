using System;
using System.Runtime.InteropServices;

namespace OrbbecUnity
{
    static class OrbbecGfx
    {
        public const int SlotsPerDevice = 20;
        public const int SlotCount = 128;

        public static bool Available { get; }
        public static IntPtr EventFunc { get; }

        static OrbbecGfx()
        {
            string nativeLog = "临时目录里的 OrbbecGfx.log";
            try
            {
                EventFunc = OrbbecGfx_GetRenderEventFunc();
                Available = EventFunc != IntPtr.Zero;
                bool linux = UnityEngine.Application.platform == UnityEngine.RuntimePlatform.LinuxPlayer
                    || UnityEngine.Application.platform == UnityEngine.RuntimePlatform.LinuxEditor;
                nativeLog = linux ? "/tmp/OrbbecGfx.log" : nativeLog;
                OrbbecLog.Info(Available ? "图形插件已加载，原生日志 " + nativeLog : "图形插件没有返回渲染回调");
            }
            catch (DllNotFoundException exception)
            {
                Available = false;
                OrbbecLog.Error("找不到 OrbbecGfx：" + exception.Message);
            }
            catch (EntryPointNotFoundException exception)
            {
                Available = false;
                OrbbecLog.Error("OrbbecGfx 入口缺失：" + exception.Message);
            }
        }

        public static int Slot(int deviceIndex, OrbbecPreviewStream stream)
        {
            int slot = deviceIndex * SlotsPerDevice + (int)stream;
            return slot >= 0 && slot < SlotCount ? slot : -1;
        }

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr OrbbecGfx_GetRenderEventFunc();

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        public static extern int OrbbecGfx_SetTexture(int slot, IntPtr texture);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        public static extern int OrbbecGfx_HasTexture(int slot);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        public static extern int OrbbecGfx_IsIdle(int slot);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        public static extern int OrbbecGfx_Publish(int slot, IntPtr pixels, int width, int height, int stride);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        public static extern int OrbbecGfx_ConsumeIssue(int slot);

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        public static extern void OrbbecGfx_Reset();

        [DllImport("OrbbecGfx", CallingConvention = CallingConvention.Cdecl)]
        public static extern void OrbbecGfx_ResetDevice(int deviceIndex, int slotsPerDevice);
    }
}
