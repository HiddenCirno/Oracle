using UnityEngine;

namespace Oracle.RaidManager
{
    public static class UIStyleManager
    {
        // 窗口样式
        public static GUIStyle WindowStyle { get; private set; }
        // 普通容器背景
        public static GUIStyle BoxStyle { get; private set; }
        // 选中容器背景（高亮）
        public static GUIStyle SelectedBoxStyle { get; private set; }
        // 普通按钮（灰色）
        public static GUIStyle NormalButtonStyle { get; private set; }
        // 红色按钮
        public static GUIStyle RedButtonStyle { get; private set; }
        // 蓝色按钮
        public static GUIStyle BlueButtonStyle { get; private set; }
        // 关闭按钮（复用红色按钮）
        public static GUIStyle CloseButtonStyle => RedButtonStyle;
        // 输入框样式
        public static GUIStyle TextFieldStyle { get; private set; }
        // 滚动条背景
        public static GUIStyle ScrollbarStyle { get; private set; }
        // 滚动条滑块
        public static GUIStyle ScrollbarThumbStyle { get; private set; }

        private static bool _initialized = false;

        public static void EnsureInitialized()
        {
            if (_initialized && WindowStyle?.normal.background != null)
                return;

            // ----- 1. 窗口样式（原 AIManagerGUI.flatWindowStyle）-----
            WindowStyle = new GUIStyle(GUI.skin.window);
            WindowStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.16f, 0.18f, 1f));
            WindowStyle.focused.background = WindowStyle.normal.background;
            WindowStyle.onNormal.background = WindowStyle.normal.background;
            WindowStyle.normal.textColor = Color.white;
            WindowStyle.border = new RectOffset(1, 1, 20, 1);

            // ----- 2. 普通容器背景（原 AIManagerGUI.flatBoxStyle）-----
            BoxStyle = new GUIStyle(GUI.skin.box);
            BoxStyle.normal.background = MakeTex(1, 1, new Color(0.20f, 0.21f, 0.23f, 1f));
            BoxStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            BoxStyle.border = new RectOffset(0, 0, 0, 0);

            // ----- 3. 选中容器背景（原 ItemManagerGUI.flatBoxStyleActive）-----
            SelectedBoxStyle = new GUIStyle(GUI.skin.box);
            SelectedBoxStyle.normal.background = MakeTex(1, 1, new Color(0.28f, 0.32f, 0.38f, 1f));
            SelectedBoxStyle.normal.textColor = Color.white;
            SelectedBoxStyle.border = new RectOffset(0, 0, 0, 0);

            // ----- 4. 普通按钮（原 AIManagerGUI.flatButtonStyle）-----
            NormalButtonStyle = new GUIStyle(GUI.skin.button);
            NormalButtonStyle.normal.background = MakeTex(1, 1, new Color(0.25f, 0.26f, 0.28f, 1f));
            NormalButtonStyle.hover.background = MakeTex(1, 1, new Color(0.35f, 0.36f, 0.39f, 1f));
            NormalButtonStyle.active.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            NormalButtonStyle.normal.textColor = Color.white;
            NormalButtonStyle.hover.textColor = Color.white;
            NormalButtonStyle.active.textColor = Color.gray;
            NormalButtonStyle.border = new RectOffset(0, 0, 0, 0);
            NormalButtonStyle.margin = new RectOffset(2, 2, 2, 2);

            // ----- 5. 红色按钮（原 AIManagerGUI.redButtonStyle）-----
            RedButtonStyle = new GUIStyle(NormalButtonStyle);
            RedButtonStyle.normal.background = MakeTex(1, 1, new Color(0.5f, 0.15f, 0.15f, 1f));
            RedButtonStyle.hover.background = MakeTex(1, 1, new Color(0.6f, 0.2f, 0.2f, 1f));
            RedButtonStyle.active.background = MakeTex(1, 1, new Color(0.3f, 0.1f, 0.1f, 1f));
            RedButtonStyle.alignment = TextAnchor.MiddleCenter;

            // ----- 6. 蓝色按钮（原 AIManagerGUI.blueButtonStyle）-----
            BlueButtonStyle = new GUIStyle(NormalButtonStyle);
            BlueButtonStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.35f, 0.55f, 1f));
            BlueButtonStyle.hover.background = MakeTex(1, 1, new Color(0.25f, 0.45f, 0.65f, 1f));
            BlueButtonStyle.active.background = MakeTex(1, 1, new Color(0.1f, 0.25f, 0.4f, 1f));
            BlueButtonStyle.alignment = TextAnchor.MiddleCenter;

            // ----- 7. 输入框样式（原 ItemManagerGUI.flatTextFieldStyle）-----
            TextFieldStyle = new GUIStyle(GUI.skin.textField);
            TextFieldStyle.normal.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            TextFieldStyle.focused.background = MakeTex(1, 1, new Color(0.18f, 0.20f, 0.22f, 1f));
            TextFieldStyle.normal.textColor = Color.white;
            TextFieldStyle.focused.textColor = Color.white;
            TextFieldStyle.border = new RectOffset(0, 0, 0, 0);
            TextFieldStyle.margin = new RectOffset(2, 2, 2, 2);
            TextFieldStyle.alignment = TextAnchor.MiddleLeft;

            // ----- 8. 滚动条样式（原 AIManagerGUI.flatScrollbarStyle / flatScrollbarThumbStyle）-----
            ScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
            ScrollbarStyle.normal.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            ScrollbarStyle.fixedWidth = 10f;
            ScrollbarStyle.border = new RectOffset(0, 0, 0, 0);

            ScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            ScrollbarThumbStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.35f, 0.55f, 1f));
            ScrollbarThumbStyle.hover.background = MakeTex(1, 1, new Color(0.25f, 0.45f, 0.65f, 1f));
            ScrollbarThumbStyle.active.background = MakeTex(1, 1, new Color(0.1f, 0.25f, 0.4f, 1f));
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
        //颜色到时候直接全改成配置模式
    }
}