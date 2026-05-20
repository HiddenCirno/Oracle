using System;
using System.Runtime.InteropServices;

namespace Oracle.ESP
{
    public static class NativeOverlay
    {
        // --- 引入底层 Windows API ---
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFOHEADER { public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
        [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public int bmiColors; }

        private static IntPtr hwnd = IntPtr.Zero;
        private static int screenW, screenH;

        public static void Initialize(int w, int h)
        {
            screenW = w; 
            screenH = h;
            // 魔法参数：WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW
            // 创造一个鼠标绝对穿透、置顶且无边框的透明窗口
            int exStyle = 0x80000 | 0x20 | 0x8 | 0x80; 
            int style = unchecked((int)0x80000000); // WS_POPUP

            hwnd = CreateWindowEx(exStyle, "STATIC", "OracleESP_Overlay", style, 0, 0, w, h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            ShowWindow(hwnd, 5);
        }

        // 接收 Unity 的原始像素数组，直接刷新到透明窗口上
        public static void UpdateFrame(byte[] bgraData)
        {
            if (hwnd == IntPtr.Zero) return;

            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC = CreateCompatibleDC(screenDC);

            BITMAPINFO bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.bmiHeader.biWidth = screenW;
            bmi.bmiHeader.biHeight = screenH; // 负数代表从上往下绘制 (Top-Down)
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = 0;

            IntPtr pBits = IntPtr.Zero;
            IntPtr hBitmap = CreateDIBSection(screenDC, ref bmi, 0, out pBits, IntPtr.Zero, 0);

            if (hBitmap != IntPtr.Zero)
            {
                // 将 Unity 算好的画面极速 Copy 进 Windows 底层显存
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
    }
}