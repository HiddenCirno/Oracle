using UnityEngine;

public static class OracleRendering
{
    public static Material EspMaterial { get; private set; }
    public static GUIStyle EspTextStyle { get; private set; }

    private static bool _isInitialized = false;

    // 在 PluginsCore.Start() 中只调用一次
    public static void Initialize()
    {
        if (_isInitialized) return;

        // 1. 统一的线条材质
        EspMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        EspMaterial.hideFlags = HideFlags.HideAndDontSave;
        EspMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        EspMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        EspMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        EspMaterial.SetInt("_ZWrite", 0);
        EspMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

        // 2. 统一的文本样式
        EspTextStyle = new GUIStyle();
        EspTextStyle.fontSize = 12;
        EspTextStyle.fontStyle = FontStyle.Bold;
        EspTextStyle.alignment = TextAnchor.MiddleCenter;
        EspTextStyle.richText = true;

        _isInitialized = true;
    }

    // 把 Aimbot 和 LootESP 里的 DrawCircle 搬过来，设为通用
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

    // 通用画线
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