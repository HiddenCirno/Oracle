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

        //readback 排队标志：上一帧 GPU 回读完成前不再发起新请求，防止请求积压
        //在渲染线程回调写入、主线程 OnGUI 读取，volatile 保证跨线程可见
        public static volatile bool IsReadbackPending;

        //初始化时锁定的半分辨率模式（运行时固定，防止 F12 动态切换导致 readback 尺寸与缓存不匹配冻结）
        public static bool IsHalfResolution;

        // ---- DIB section 复用缓存（避免每帧 Create/Delete 8MB 级 GDI 对象） ----
        private static IntPtr _cachedDibBitmap = IntPtr.Zero;
        private static IntPtr _cachedMemDC = IntPtr.Zero;
        private static IntPtr _cachedDibBits = IntPtr.Zero;
        private static IntPtr _cachedOldBmp = IntPtr.Zero;
        private static int _dibWidth, _dibHeight;

        //readback 目标尺寸（半分辨率时为全屏一半，pixelBuffer 与之匹配）
        private static int _readbackWidth, _readbackHeight;

        //行放大缓存（半分辨率 → 全屏 DIB 的最近邻放大复用行）
        private static byte[] _upscaleRowCache;

        //全屏 DIB 缓冲区（半分辨率放大后的目标）
        private static byte[] _fullScreenBuffer;

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
            screenW = w;
            screenH = h;

            //半分辨率：readback 数据量减 75%，DIB 始终保持全屏（与窗口同尺寸），
            //readback 数据在 UpdateFrame 中放大回全屏，避免 UpdateLayeredWindow 尺寸不匹配
            //锁定模式，运行时不再读取配置，防止动态切换破坏尺寸匹配
            IsHalfResolution = NativeOverlayCfg.OverlayHalfResolution.Value;
            _readbackWidth = IsHalfResolution ? Math.Max(1, w / 2) : w;
            _readbackHeight = IsHalfResolution ? Math.Max(1, h / 2) : h;

            //预分配缓存（readback 尺寸）, 高效GC
            pixelBuffer = new byte[_readbackWidth * _readbackHeight * 4];

            //全屏 DIB 缓冲区（半分辨率放大目标）
            _fullScreenBuffer = new byte[w * h * 4];

            //预建 DIB section（复用，始终全屏尺寸）
            EnsureDib(w, h);

            //创建一个可以让鼠标穿过的透明窗口
            int exStyle = 0x80000 | 0x20 | 0x8 | 0x80;
            int style = unchecked((int)0x80000000);
            hwnd = CreateWindowEx(exStyle, "STATIC", "OracleESP_Overlay", style, 0, 0, w, h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            ShowWindow(hwnd, 5);
        }

        /// <summary>
        /// 接受图像流然后投射到窗口上（复用缓存的 DIB section，不再每帧创建/销毁）
        /// </summary>
        /// <param name="bgraData">数据流</param>
        public static void UpdateFrame(byte[] bgraData)
        {
            if (hwnd == IntPtr.Zero) return;
            if (_cachedDibBitmap == IntPtr.Zero || _cachedDibBits == IntPtr.Zero || _cachedMemDC == IntPtr.Zero)
            {
                //DIB 尚未就绪（如销毁期间），直接丢弃本帧
                return;
            }

            IntPtr screenDC = GetDC(IntPtr.Zero);

            //按数据尺寸自适应：全分辨率直拷，半分辨率放大回全屏 DIB（最近邻，按行缓存复用）
            int fullBytes = _dibWidth * _dibHeight * 4;
            if (bgraData.Length == fullBytes)
            {
                //整帧拷贝到缓存位图像素区（全分辨率直拷）
                Marshal.Copy(bgraData, 0, _cachedDibBits, fullBytes);
            }
            else if (bgraData.Length == _readbackWidth * _readbackHeight * 4)
            {
                //半分辨率 readback 数据放大到全屏 DIB
                UpscaleBgraToFullScreen(bgraData);
                Marshal.Copy(_fullScreenBuffer, 0, _cachedDibBits, fullBytes);
            }
            else
            {
                //尺寸不匹配：丢弃本帧，防止越界
                ReleaseDC(IntPtr.Zero, screenDC);
                return;
            }

            POINT ptSrc = new POINT { x = 0, y = 0 };
            POINT ptDst = new POINT { x = 0, y = 0 };
            //窗口尺寸始终为全屏，DIB 与其同尺寸，保证 UpdateLayeredWindow 尺寸匹配
            SIZE size = new SIZE { cx = screenW, cy = screenH };
            BLENDFUNCTION blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 }; // 开启 Alpha 透明通道
            UpdateLayeredWindow(hwnd, screenDC, ref ptDst, ref size, _cachedMemDC, ref ptSrc, 0, ref blend, 2);

            ReleaseDC(IntPtr.Zero, screenDC);
        }

        /// <summary>
        /// 将半分辨率 BGRA 数据最近邻放大到全屏 DIB 缓冲区。
        /// 每行只做一次水平放大并缓存，垂直方向用快拷重复该行，避免逐像素双重循环。
        /// </summary>
        /// <param name="src">半分辨率 BGRA 数据（readback 尺寸）</param>
        private static void UpscaleBgraToFullScreen(byte[] src)
        {
            int srcW = _readbackWidth;
            int srcH = _readbackHeight;
            int dstW = _dibWidth;
            int dstH = _dibHeight;
            byte[] dst = _fullScreenBuffer;

            //行放大缓存复用
            if (_upscaleRowCache == null || _upscaleRowCache.Length != dstW * 4)
            {
                _upscaleRowCache = new byte[dstW * 4];
            }

            int lastSrcRow = -1;
            for (int y = 0; y < dstH; y++)
            {
                int sy = y * srcH / dstH;
                if (sy != lastSrcRow)
                {
                    //该源行尚未放大：水平最近邻填充缓存行
                    int srcRowBase = sy * srcW * 4;
                    for (int x = 0; x < dstW; x++)
                    {
                        int sx = x * srcW / dstW;
                        int si = srcRowBase + sx * 4;
                        int di = x * 4;
                        _upscaleRowCache[di] = src[si];
                        _upscaleRowCache[di + 1] = src[si + 1];
                        _upscaleRowCache[di + 2] = src[si + 2];
                        _upscaleRowCache[di + 3] = src[si + 3];
                    }
                    lastSrcRow = sy;
                }
                //整行快拷到目标 DIB 缓冲区
                Buffer.BlockCopy(_upscaleRowCache, 0, dst, y * dstW * 4, dstW * 4);
            }
        }

        /// <summary>
        /// 确保缓存的 DIB section 存在且尺寸匹配（分辨率变化时自动重建）
        /// </summary>
        private static void EnsureDib(int w, int h)
        {
            if (_cachedDibBitmap != IntPtr.Zero && _dibWidth == w && _dibHeight == h)
            {
                return;
            }

            //重建：先释放旧的
            FreeDib();

            IntPtr screenDC = GetDC(IntPtr.Zero);
            _cachedMemDC = CreateCompatibleDC(screenDC);
            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = w;
            bmi.bmiHeader.biHeight = h;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0;
            _cachedDibBitmap = CreateDIBSection(screenDC, ref bmi, 0, out _cachedDibBits, IntPtr.Zero, 0);
            if (_cachedDibBitmap != IntPtr.Zero)
            {
                _cachedOldBmp = SelectObject(_cachedMemDC, _cachedDibBitmap);
                _dibWidth = w;
                _dibHeight = h;
            }
            ReleaseDC(IntPtr.Zero, screenDC);
        }

        /// <summary>
        /// 释放缓存的 DIB section / 兼容 DC
        /// </summary>
        private static void FreeDib()
        {
            if (_cachedMemDC != IntPtr.Zero && _cachedOldBmp != IntPtr.Zero)
            {
                SelectObject(_cachedMemDC, _cachedOldBmp);
            }
            if (_cachedDibBitmap != IntPtr.Zero)
            {
                DeleteObject(_cachedDibBitmap);
            }
            if (_cachedMemDC != IntPtr.Zero)
            {
                DeleteDC(_cachedMemDC);
            }

            _cachedDibBitmap = IntPtr.Zero;
            _cachedMemDC = IntPtr.Zero;
            _cachedDibBits = IntPtr.Zero;
            _cachedOldBmp = IntPtr.Zero;
            _dibWidth = 0;
            _dibHeight = 0;
        }

        /// <summary>
        /// 摧毁叠加层
        /// </summary>
        public static void Destroy()
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                //清除画面（缓冲区尺寸与 DIB 一致）
                ShowWindow(hwnd, SW_HIDE);
                if (pixelBuffer != null)
                {
                    UpdateFrame(new byte[pixelBuffer.Length]);
                }
                else
                {
                    UpdateFrame(new byte[_dibWidth * _dibHeight * 4]);
                }

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
                //释放缓存 DIB
                FreeDib();
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
                //清空画布（缓冲区尺寸与 DIB 一致）
                if (pixelBuffer != null)
                {
                    UpdateFrame(new byte[pixelBuffer.Length]);
                }
                else
                {
                    UpdateFrame(new byte[_dibWidth * _dibHeight * 4]);
                }
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
            //解除排队标志：允许下一帧发起新的 readback 请求
            IsReadbackPending = false;

            if (req.hasError || pixelBuffer == null) return;
            //分辨率防御（半分辨率 readback 的尺寸必须与预分配缓存一致）
            var data = req.GetData<byte>();
            if (data.Length != pixelBuffer.Length) return;
            //GPU数据传输
            data.CopyTo(pixelBuffer);
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
        internal static ConfigEntry<bool> OverlayHalfResolution { get; set; }

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
            OverlayHalfResolution = config.Bind(
                "0. 联觉信标 / Draw Module",
                "叠加层半分辨率（性能优化）",
                false,
                new ConfigDescription(
                    "cfg_global_module_overlay_half_res_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_global_module_overlay_half_res_name".i18n(),
                        IsAdvanced = false,
                        Order = 396
                    }
                )
            );
        }
    }
}