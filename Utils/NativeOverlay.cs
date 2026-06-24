using BepInEx.Configuration;
using Oracle.Data;
using System;
using UnityEngine;
using System.Runtime.InteropServices;
using static Oracle.Data.OracleInterface;

namespace Oracle.Utils
{
    /// <summary>
    /// 过直播
    /// </summary>
    public static class NativeOverlay
    {
        //叠加层状态
        private static bool isOverlayInitialized = false;

        //图像流缓存
        private static byte[] pixelBuffer;

        //引入Windows底层API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

        
        //设置基本配置项
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFOHEADER { public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
        [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public int bmiColors; }
        
        //定义原点和宽高
        private static IntPtr hwnd = IntPtr.Zero;
        private static int screenW, screenH;

        //剔除输入焦点
        private const int SW_HIDE = 0;
        private const int SW_SHOWNA = 8;
        
        //当前显隐状态
        private static bool isVisible = true;
        
        /// <summary>
        /// 初始化覆盖层
        /// </summary>
        /// <param name="w">窗口宽度</param>
        /// <param name="h">窗口高度</param>
        public static void Initialize(int w, int h)
        {
            //预分配缓存, 高效GC
            pixelBuffer = new byte[Screen.width * Screen.height * 4];

            screenW = w; 
            screenH = h;
            //创建一个可以让鼠标穿过的透明窗口
            int exStyle = 0x80000 | 0x20 | 0x8 | 0x80; 
            int style = unchecked((int)0x80000000);
            hwnd = CreateWindowEx(exStyle, "STATIC", "OracleESP_Overlay", style, 0, 0, w, h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            ShowWindow(hwnd, 5);
        }

        /// <summary>
        /// 接受图像流然后投射到窗口上
        /// </summary>
        /// <param name="bgraData">数据流</param>
        public static void UpdateFrame(byte[] bgraData)
        {
            if (hwnd == IntPtr.Zero) return;
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = screenW;
            bmi.bmiHeader.biHeight = screenH;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0;
            IntPtr pBits = IntPtr.Zero;
            IntPtr hBitmap = CreateDIBSection(screenDC, ref bmi, 0, out pBits, IntPtr.Zero, 0);
            if (hBitmap != IntPtr.Zero)
            {
                Marshal.Copy(bgraData, 0, pBits, bgraData.Length);
                IntPtr hOldBmp = SelectObject(memDC, hBitmap);
                POINT ptSrc = new POINT { x = 0, y = 0 };
                POINT ptDst = new POINT { x = 0, y = 0 };
                SIZE size = new SIZE { cx = screenW, cy = screenH };
                BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 }; // 开启 Alpha 透明通道
                UpdateLayeredWindow(hwnd, screenDC, ref ptDst, ref size, memDC, ref ptSrc, 0, ref blend, 2);
                SelectObject(memDC, hOldBmp);
                DeleteObject(hBitmap);
            }
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);
        }

        /// <summary>
        /// 摧毁叠加层
        /// </summary>
        public static void Destroy()
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                //清除画面
                ShowWindow(hwnd, SW_HIDE);
                UpdateFrame(new byte[screenW * screenH * 4]);

                //销毁窗口
                DestroyWindow(hwnd);
            }
            catch
            {
            }
            finally
            {
                //重置句柄
                hwnd = IntPtr.Zero;
                isVisible = false;
            }
        }

        /// <summary>
        /// 控制窗口显隐
        /// </summary>
        /// <param name="show">显示状态</param>
        public static void SetVisible(bool show)
        {
            if (hwnd == IntPtr.Zero) return;

            if (show && !isVisible)
            {
                ShowWindow(hwnd, SW_SHOWNA);
                isVisible = true;
            }
            else if (!show && isVisible)
            {
                ShowWindow(hwnd, SW_HIDE);
                //清空画布
                UpdateFrame(new byte[screenW * screenH * 4]);
                isVisible = false;
            }
        }

        /// <summary>
        /// 更新叠加层状态
        /// </summary>
        public static void UpdateNativeOverlay()
        {
            if (NativeOverlayCfg.EnableNativeOverlay.Value)
            {
                //重新初始化
                if (!isOverlayInitialized)
                {
                    Initialize(Screen.width, Screen.height);
                    isOverlayInitialized = true;
                }

                //叠加层显隐1
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

        /// <summary>
        /// 图像流传输
        /// </summary>
        /// <param name="req"></param>
        public static void OnReadbackComplete(UnityEngine.Rendering.AsyncGPUReadbackRequest req)
        {
            if (req.hasError || pixelBuffer == null) return;
            //GPU数据传输
            req.GetData<byte>().CopyTo(pixelBuffer); 
            //分辨率防御
            var data = req.GetData<byte>();
            if (data.Length != pixelBuffer.Length) return;
            //将图像流传输给窗口
            NativeOverlay.UpdateFrame(pixelBuffer);
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    public class NativeOverlayCfg : IOracleCfg
    {

        internal static ConfigEntry<bool> EnableNativeOverlay { get; set; }

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
        }
    }
}