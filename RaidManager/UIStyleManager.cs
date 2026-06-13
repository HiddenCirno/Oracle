using UnityEngine;

namespace Oracle.RaidManager
{
    public static class UIStyleManager
    {
        // 窗口样式
        public static GUIStyle WindowStyle { get; private set; }
        // 容器背景（Box）
        public static GUIStyle BoxStyle { get; private set; }
        // 普通灰色按钮（原 flatButtonStyle）
        public static GUIStyle NormalButtonStyle { get; private set; }
        // 红色按钮（原 redButtonStyle）
        public static GUIStyle RedButtonStyle { get; private set; }
        // 蓝色按钮（原 blueButtonStyle）
        public static GUIStyle BlueButtonStyle { get; private set; }
        // 滚动条背景
        public static GUIStyle ScrollbarStyle { get; private set; }
        // 滚动条滑块
        public static GUIStyle ScrollbarThumbStyle { get; private set; }
        // 关闭按钮（复用红色按钮样式，原 closeButtonStyle）
        public static GUIStyle CloseButtonStyle => RedButtonStyle;

        private static bool _initialized = false;

        public static void EnsureInitialized()
        {
            if (_initialized && WindowStyle?.normal.background != null)
                return;

            // ----- 1. 窗口样式（原 flatWindowStyle）-----
            WindowStyle = new GUIStyle(GUI.skin.window);
            WindowStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.16f, 0.18f, 1f));
            WindowStyle.focused.background = WindowStyle.normal.background;
            WindowStyle.onNormal.background = WindowStyle.normal.background;
            WindowStyle.normal.textColor = Color.white;
            WindowStyle.border = new RectOffset(1, 1, 20, 1);

            // ----- 2. 容器背景（原 flatBoxStyle）-----
            BoxStyle = new GUIStyle(GUI.skin.box);
            BoxStyle.normal.background = MakeTex(1, 1, new Color(0.20f, 0.21f, 0.23f, 1f));
            BoxStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            BoxStyle.border = new RectOffset(0, 0, 0, 0);

            // ----- 3. 普通灰色按钮（原 flatButtonStyle）-----
            NormalButtonStyle = new GUIStyle(GUI.skin.button);
            NormalButtonStyle.normal.background = MakeTex(1, 1, new Color(0.25f, 0.26f, 0.28f, 1f));
            NormalButtonStyle.hover.background = MakeTex(1, 1, new Color(0.35f, 0.36f, 0.39f, 1f));
            NormalButtonStyle.active.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            NormalButtonStyle.normal.textColor = Color.white;
            NormalButtonStyle.hover.textColor = Color.white;
            NormalButtonStyle.active.textColor = Color.gray;
            NormalButtonStyle.border = new RectOffset(0, 0, 0, 0);
            NormalButtonStyle.margin = new RectOffset(2, 2, 2, 2);

            // ----- 4. 红色按钮（原 redButtonStyle）-----
            RedButtonStyle = new GUIStyle(NormalButtonStyle);
            RedButtonStyle.normal.background = MakeTex(1, 1, new Color(0.5f, 0.15f, 0.15f, 1f));
            RedButtonStyle.hover.background = MakeTex(1, 1, new Color(0.6f, 0.2f, 0.2f, 1f));
            RedButtonStyle.active.background = MakeTex(1, 1, new Color(0.3f, 0.1f, 0.1f, 1f));
            RedButtonStyle.alignment = TextAnchor.MiddleCenter;

            // ----- 5. 蓝色按钮（原 blueButtonStyle）-----
            BlueButtonStyle = new GUIStyle(NormalButtonStyle);
            BlueButtonStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.35f, 0.55f, 1f));
            BlueButtonStyle.hover.background = MakeTex(1, 1, new Color(0.25f, 0.45f, 0.65f, 1f));
            BlueButtonStyle.active.background = MakeTex(1, 1, new Color(0.1f, 0.25f, 0.4f, 1f));
            BlueButtonStyle.alignment = TextAnchor.MiddleCenter;

            // ----- 6. 滚动条样式（原 flatScrollbarStyle / flatScrollbarThumbStyle）-----
            ScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
            ScrollbarStyle.normal.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            ScrollbarStyle.fixedWidth = 10f;
            ScrollbarStyle.border = new RectOffset(0, 0, 0, 0);

            ScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            ScrollbarThumbStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 0.31f, 0.33f, 1f));
            ScrollbarThumbStyle.hover.background = MakeTex(1, 1, new Color(0.4f, 0.41f, 0.43f, 1f));
            ScrollbarThumbStyle.active.background = MakeTex(1, 1, new Color(0.5f, 0.51f, 0.53f, 1f));
            ScrollbarThumbStyle.fixedWidth = 10f;
            ScrollbarThumbStyle.border = new RectOffset(0, 0, 0, 0);

            _initialized = true;
        }

        private static Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}