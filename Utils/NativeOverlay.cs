using BepInEx.Configuration;
using Oracle.Data;
using Oracle.Overlay;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Utils
{
    /// <summary>
    /// 叠加层窗口宿主。
    /// 只负责 Win32 透明窗口的生命周期；绘制由 OverlayGdiRenderer（后台线程）消费
    /// OverlayPrimitiveStore 中的屏幕空间原语完成，不再传输图像流。
    /// </summary>
    public static class NativeOverlay
    {
        //叠加层状态
        private static bool isOverlayInitialized = false;

        //数据桥三缓冲 store（主线程构建原语 → 渲染线程消费）
        public static OverlayPrimitiveStore Store { get; private set; } = new OverlayPrimitiveStore();

        //引入Windows底层API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        //定义原点和宽高
        private static IntPtr hwnd = IntPtr.Zero;
        private static int screenW, screenH;

        //当前显隐状态
        private static bool isVisible = true;

        /// <summary>
        /// 初始化覆盖层窗口（叠加层启用时调用，主线程）
        /// </summary>
        public static void Initialize(int w, int h)
        {
            screenW = w;
            screenH = h;

            //创建一个可以让鼠标穿过的透明窗口
            //⚠ SW_SHOWNA(8) 而不是 SW_SHOW(5)：SW_SHOW 会激活窗口抢走游戏焦点，
            //  导致 Application.isFocused 变 false，下一帧 UpdateNativeOverlay 就把窗口隐藏，全黑。
            int exStyle = 0x80000 | 0x20 | 0x8 | 0x80; // WS_EX_LAYERED|TRANSPARENT|TOPMOST|TOOLWINDOW
            if (NativeOverlayCfg.OverlayDebugShowInTaskbar.Value)
            {
                //调试：去掉 TOOLWINDOW、改 APPWINDOW，让窗口出现在任务栏便于观察是否创建成功
                exStyle = 0x80000 | 0x20 | 0x8 | 0x40000; // WS_EX_LAYERED|TRANSPARENT|TOPMOST|APPWINDOW
            }
            int style = unchecked((int)0x80000000); // WS_POPUP
            hwnd = CreateWindowEx(exStyle, "STATIC", "OracleESP_Overlay", style, 0, 0, w, h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (hwnd == IntPtr.Zero)
            {
                UnityEngine.Debug.LogError($"[Oracle] 叠加层窗口创建失败! GetLastError={Marshal.GetLastWin32Error()}");
                return;
            }
            ShowWindow(hwnd, 8); // SW_SHOWNA

            //启动 GDI 渲染线程（DIB 为 top-down，坐标与 Builder 输出一致）
            OverlayGdiRenderer.Initialize(hwnd, w, h, Store);
        }

        /// <summary>
        /// 摧毁叠加层（停线程 → 释放 GDI → 销毁窗口）
        /// </summary>
        public static void Destroy()
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                //停渲染线程并释放 GDI 对象
                OverlayGdiRenderer.Destroy();
                //销毁窗口
                DestroyWindow(hwnd);
            }
            catch
            {
            }
            finally
            {
                hwnd = IntPtr.Zero;
                isVisible = false;
            }
        }

        /// <summary>
        /// 控制窗口显隐
        /// </summary>
        public static void SetVisible(bool show)
        {
            if (hwnd == IntPtr.Zero) return;

            if (show && !isVisible)
            {
                ShowWindow(hwnd, 8); // SW_SHOWNA
                isVisible = true;
            }
            else if (!show && isVisible)
            {
                ShowWindow(hwnd, 0); // SW_HIDE
                isVisible = false;
            }
        }

        /// <summary>
        /// 更新叠加层状态
        /// </summary>
        public static void UpdateNativeOverlay()
        {
            //调试自检帧开关同步（渲染线程轮询此标志，配置改动即时生效）
            OverlayGdiRenderer.ForceTestFrame = NativeOverlayCfg.OverlayDebugTestFrame.Value;

            if (NativeOverlayCfg.EnableNativeOverlay.Value)
            {
                //重新初始化
                if (!isOverlayInitialized)
                {
                    Initialize(Screen.width, Screen.height);
                    isOverlayInitialized = true;
                }

                //叠加层显隐
                bool shouldShowOverlay = Application.isFocused && GlobalCfg.UniGUI.Value;
                SetVisible(shouldShowOverlay);
            }
            else
            {
                //摧毁叠加层
                if (isOverlayInitialized)
                {
                    Destroy();
                    isOverlayInitialized = false;
                }
            }
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    public class NativeOverlayCfg : IOracleCfg
    {

        internal static ConfigEntry<bool> EnableNativeOverlay { get; set; }
        /// <summary>调试：让叠加层窗口出现在任务栏（排查窗口是否创建成功）</summary>
        internal static ConfigEntry<bool> OverlayDebugShowInTaskbar { get; set; }
        /// <summary>调试：强制显示自检测试帧（红块+文字），验证窗口/GDI 管线是否通畅</summary>
        internal static ConfigEntry<bool> OverlayDebugTestFrame { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            EnableNativeOverlay = config.Bind(
                "0. 联觉信标 / Draw Module",
                "启用叠加层",
                false,
                new ConfigDescription(
                    "cfg_global_module_overlay_enable_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_global_module_overlay_enable_name".i18n(),
                        IsAdvanced = false,
                        Order = 397
                    }
                )
            );
            OverlayDebugShowInTaskbar = config.Bind(
                "0. 联觉信标 / Draw Module",
                "叠加层调试：任务栏可见",
                false,
                new ConfigDescription(
                    "cfg_global_module_overlay_debug_taskbar_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_global_module_overlay_debug_taskbar_name".i18n(),
                        IsAdvanced = true,
                        Order = 396
                    }
                )
            );
            OverlayDebugTestFrame = config.Bind(
                "0. 联觉信标 / Draw Module",
                "叠加层调试：自检测试帧",
                false,
                new ConfigDescription(
                    "cfg_global_module_overlay_debug_testframe_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_global_module_overlay_debug_testframe_name".i18n(),
                        IsAdvanced = true,
                        Order = 395
                    }
                )
            );
        }
    }
}
