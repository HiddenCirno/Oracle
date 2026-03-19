using BepInEx.Configuration;
using EFT;
using EFT.Ballistics;
using HarmonyLib;
using System;
using UnityEngine;

namespace Oracle.ESP
{
    public class Aimbot
    {
        // 独立的绘制材质，防止和其他模块的渲染冲突
        private static Material aimbotMaterial;
        //当前锁定目标
        public static Player LockedTarget { get; private set; }
        //自瞄范围绘制
        public static void DrawAimbotFOVCircle()
        {
            if (!AimbotCfg.EnableAimbot.Value || !AimbotCfg.DrawAimbotFov.Value) return;
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float fovRadius = AimbotCfg.AimbotFovRadius.Value;
            DrawCircle(screenCenter, fovRadius, new Color(1f, 0f, 0f, 0.3f), 64);
        }
        //实时更新自瞄目标
        public static void UpdateTarget(Camera cam)
        {
            //关闭自瞄停止运行
            if (!AimbotCfg.EnableAimbot.Value)
            {
                LockedTarget = null;
                return;
            }
            //中心计算
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float fovRadius = AimbotCfg.AimbotFovRadius.Value;
            int maxDist = AimbotCfg.AimbotMaxDistance.Value; // 读取 3D 最大距离配置
            Vector3 myPos = PluginsCore.CorrectPlayer.Transform.position;
            //取值
            Player bestTarget = null;
            float minDistance = float.MaxValue;
            //遍历求解
            foreach (Player player in PluginsCore.CorrectGameWorld.AllAlivePlayersList)
            {
                //空值防御
                if (player == null || player == PluginsCore.CorrectPlayer || player.PlayerBones == null) continue;
                //距离过滤
                if (!PlayerESP.IsInRange(maxDist, myPos, player.Transform.position)) continue;
                //找头
                Vector3? headPos = PlayerESP.GetBonePos(player.PlayerBones.Head);
                if (!headPos.HasValue) continue;
                //深度过滤
                Vector3 screenPos = cam.WorldToScreenPoint(headPos.Value);
                if (screenPos.z <= 0.01f) continue; // 防背身
                //计算距离
                float screenY = screenPos.y;
                Vector2 headScreen2D = new Vector2(screenPos.x, screenY);
                float distToCenter = Vector2.Distance(screenCenter, headScreen2D);
                if (distToCenter > fovRadius) continue;
                //求解
                if (distToCenter < minDistance)
                {
                    //可视化判断
                    if (PlayerESP.IsPlayerVisible(cam.transform.position, player, PlayerESP.HighPolyWithTerrainMask))
                    {
                        minDistance = distToCenter;
                        bestTarget = player;
                    }
                }
            }
            //更新目标
            LockedTarget = bestTarget;
        }
        //锁定线绘制
        public static void DrawTargetLine(Camera cam)
        {
            //依旧功能开关+防御
            if (!AimbotCfg.EnableAimbot.Value || !AimbotCfg.DrawTargetLine.Value || LockedTarget == null || LockedTarget.PlayerBones == null) return;
            //找头
            Vector3? headPos = PlayerESP.GetBonePos(LockedTarget.PlayerBones.Head);
            if (!headPos.HasValue) return;
            //3d转2d
            Vector3 screenPos = cam.WorldToScreenPoint(headPos.Value);
            if (screenPos.z <= 0.01f) return;
            //中心查找
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector3 endPos = new Vector3(screenPos.x, screenPos.y, 0);
            //画线
            DrawLine(screenCenter, endPos, new Color(1f, 0f, 0f, 0.8f));
        }
        //材质初始化
        private static void InitMaterial()
        {
            if (!aimbotMaterial)
            {
                aimbotMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
                aimbotMaterial.hideFlags = HideFlags.HideAndDontSave;
                aimbotMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                aimbotMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                aimbotMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                aimbotMaterial.SetInt("_ZWrite", 0);
            }
        }
        //画圆
        public static void DrawCircle(Vector2 center, float radius, Color color, int segments)
        {
            if (Event.current.type != EventType.Repaint) return;
            InitMaterial();
            aimbotMaterial.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.LINES);
            GL.Color(color);
            float angleStep = 2f * Mathf.PI / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * angleStep;
                float angle2 = (i + 1) * angleStep;
                float x1 = center.x + Mathf.Cos(angle1) * radius;
                float y1 = center.y + Mathf.Sin(angle1) * radius;
                float x2 = center.x + Mathf.Cos(angle2) * radius;
                float y2 = center.y + Mathf.Sin(angle2) * radius;
                GL.Vertex3(x1, y1, 0);
                GL.Vertex3(x2, y2, 0);
            }
            GL.End();
            GL.PopMatrix();
        }
        //画线
        public static void DrawLine(Vector2 start, Vector3 end, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            InitMaterial();
            aimbotMaterial.SetPass(0);
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
    //后坐力Patch
    [HarmonyPatch(typeof(ShotEffector), nameof(ShotEffector.Process))]
    public class NoRecoilPatch
    {
        static bool Prefix(ref float str)
        {
            //取反一步搞定
            return !AimbotCfg.NoRecoil.Value; 
        }
    }
    //魔法子弹Patch
    [HarmonyPatch(typeof(BallisticsCalculator), nameof(BallisticsCalculator.CreateShot))]
    public class MagicBulletPatch
    {
        public static void Prefix(
            AmmoItemClass ammo,
            ref Vector3 origin,
            ref Vector3 direction,
            string player,
            ref float speedFactor)
        {
            //Console.WriteLine(player);
            //开启功能且目标存在
            if (!AimbotCfg.EnableAimbot.Value || Aimbot.LockedTarget == null)
                return;
            //确认攻击者
            if (player != PluginsCore.CorrectPlayer.ProfileId)
                return;
            //找头
            Vector3? targetPos = PlayerESP.GetBonePos(Aimbot.LockedTarget.PlayerBones.Head);
            //空值, 返回
            if (targetPos == null)
                return;
            //修改向量和加速度
            if (AimbotCfg.SuperMagicBullet.Value)
            {
                origin = (Vector3)targetPos + (Vector3.up * 0.2f);
                direction = Vector3.down;
            }
            else
            {
                direction = ((Vector3)targetPos - origin).normalized;
                speedFactor = AimbotCfg.MagicBulletSpeed.Value;
            }
        }
    }
    //配置定义
    public class AimbotCfg
    {
        internal static ConfigEntry<bool> EnableAimbot { get; set; }
        internal static ConfigEntry<bool> SuperMagicBullet { get; set; }
        internal static ConfigEntry<bool> DrawAimbotFov { get; set; }
        internal static ConfigEntry<bool> DrawTargetLine { get; set; }
        internal static ConfigEntry<bool> NoRecoil { get; set; }
        internal static ConfigEntry<float> AimbotFovRadius { get; set; }
        internal static ConfigEntry<float> MagicBulletSpeed { get; set; }
        internal static ConfigEntry<int> AimbotMaxDistance { get; set; } // ⭐ 新增：3D 距离限制

        public static void Initialize(ConfigFile config)
        {
            EnableAimbot = config.Bind(
                "自瞄设置", "启用自瞄逻辑", true, "自瞄模块总开关"
            );
            SuperMagicBullet = config.Bind(
                "自瞄设置", "超级魔法子弹", false, "启用后子弹会直接在敌人头部生成"
            );
            NoRecoil = config.Bind(
                "自瞄设置", "消除武器后座", true, "禁用武器后坐力系统"
            );
            DrawAimbotFov = config.Bind(
                "自瞄设置", "显示自瞄 FOV", true, "在屏幕中心绘制自瞄生效范围圆环"
            );
            DrawTargetLine = config.Bind(
                "自瞄设置", "显示目标锁定线", true, "绘制一条从屏幕中心到最优锁定目标的连线"
            );
            AimbotFovRadius = config.Bind(
                "自瞄设置", "自瞄 FOV 半径", 150f,
                new ConfigDescription("自瞄圆环的大小", new AcceptableValueRange<float>(10f, 1000f))
            );
            MagicBulletSpeed = config.Bind(
                "自瞄设置", "魔法子弹加速度", 20f,
                new ConfigDescription("魔法子弹加速度", new AcceptableValueRange<float>(10f, 100f))
            );
            AimbotMaxDistance = config.Bind(
                "自瞄设置", "自瞄最大距离", 200,
                new ConfigDescription("自瞄生效的最大 3D 物理距离(米)", new AcceptableValueRange<int>(10, 1000))
            );
        }
    }
}