using UnityEngine;

namespace Oracle.Utils
{
    /// <summary>
    /// 全局绘制样式
    /// </summary>
    public static class OracleRendering
    {
        public static Material EspMaterial { get; private set; }
        public static GUIStyle EspTextStyle { get; private set; }

        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            //线条材质
            EspMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
            EspMaterial.hideFlags = HideFlags.HideAndDontSave;
            EspMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            EspMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EspMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            EspMaterial.SetInt("_ZWrite", 0);
            EspMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            //文本样式
            EspTextStyle = new GUIStyle();
            EspTextStyle.fontSize = 12;
            EspTextStyle.fontStyle = FontStyle.Bold;
            EspTextStyle.alignment = TextAnchor.MiddleCenter;
            EspTextStyle.richText = true;

            _isInitialized = true;
        }

        /// <summary>
        /// 画圆方法
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        /// <param name="segments"></param>
        public static void DrawCircle(Vector2 center, float radius, Color color, int segments = 64)
        {
            if (Event.current.type != EventType.Repaint) return;

            EspMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            GL.Color(color);
            float angleStep = 2f * Mathf.PI / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                GL.Vertex3(center.x + Mathf.Cos(angle1) * radius, center.y + Mathf.Sin(angle1) * radius, 0);
                GL.Vertex3(center.x + Mathf.Cos(angle2) * radius, center.y + Mathf.Sin(angle2) * radius, 0);
            }
            GL.End();
            GL.PopMatrix();
        }

        /// <summary>
        /// 画线方法
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="color"></param>
        public static void DrawLine(Vector2 start, Vector3 end, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            EspMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex3(start.x, start.y, 0);
            GL.Vertex3(end.x, end.y, 0);
            GL.End();
            GL.PopMatrix();
        }
    }
}