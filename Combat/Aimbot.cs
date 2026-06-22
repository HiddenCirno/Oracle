using BepInEx.Configuration;
using EFT;
using EFT.Ballistics;
using EFT.Communications;
using HarmonyLib;
using Oracle.Data;
using Oracle.ESP;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    /// <summary>
    /// 自瞄部分
    /// </summary>
    public class Aimbot : IOracleAimbot
    {

        private static float targetUpdateRate = 1f / AimbotCfg.AimbotTargetUpdateRate.Value; //加个配置的事
        private static float lastUpdateTime = 0f;
        /// <summary>
        /// 内部变量, 当前瞄准的目标
        /// </summary>
        public static Player LockedTarget { get; private set; }
        /// <summary>
        /// 绘制约束范围
        /// </summary>

        public static void DrawAimbotFOVCircle()
        {
            if (!AimbotCfg.EnableAimbot.Value || !AimbotCfg.DrawAimbotFov.Value) return;
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float fovRadius = AimbotCfg.AimbotFovRadius.Value;
            OracleRendering.DrawCircle(screenCenter, fovRadius, new Color(1f, 0f, 0f, 0.3f), 64);
        }
        
        public void SubscribeEvent()
        {
            // ⭐ 逻辑更新归 Update 频道
            OracleEvent.OnUpdate += OnLogicUpdate;
            // ⭐ 画图归 GUI 频道
            OracleEvent.OnDrawAimbot += OnDrawGUI;
        }

        private void OnLogicUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                UpdateTarget(cam); // 原来被错误地放在 DrawESP 里的逻辑，移回这里！
            }
        }

        private void OnDrawGUI()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            // 使用统一的 RenderUtils 画图，删掉原来 Aimbot 自己写的那些 material 和画图方法
            DrawAimbotFOVCircle();
            DrawTargetLine(cam);
        }
        /// <summary>
        /// 更新瞄准目标
        /// </summary>
        /// <param name="cam">当前摄像机</param>
        public static void UpdateTarget(Camera cam)
        {
            //你TM为什么没有防御呢?
            if(PluginsCore.CorrectPlayer==null || PluginsCore.CorrectGameWorld==null) return;
            if (Time.time - lastUpdateTime < targetUpdateRate)
            {
                return;
            }

            lastUpdateTime = Time.time;
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
                //过滤队友
                string targetGroupId = player.Profile?.Info?.GroupId ?? "";
                if (!string.IsNullOrEmpty(PluginsCore.CorrectGroupId) && targetGroupId == PluginsCore.CorrectGroupId) continue;
                //距离过滤
                if (!OracleCommon.IsInRange(maxDist, myPos, player.Transform.position)) continue;
                //找头
                Vector3? headPos = AimbotCfg.AimbotPartSetting.Value == "头部" ? OraclePlayerManager.GetBonePos(player.PlayerBones.Head) : OraclePlayerManager.GetBonePos(player.PlayerBones.Spine3);
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
                    if (OraclePlayerManager.IsPlayerVisible(cam.transform.position, player, OraclePlayerManager.HighPolyWithTerrainMask))
                    {
                        minDistance = distToCenter;
                        bestTarget = player;
                    }
                }
            }
            //更新目标
            LockedTarget = bestTarget;
        }
        /// <summary>
        /// 绘制目标锁定线
        /// </summary>
        /// <param name="cam">当前摄像机</param>
        public static void DrawTargetLine(Camera cam)
        {
            //依旧功能开关+防御
            if (!AimbotCfg.EnableAimbot.Value || !AimbotCfg.DrawTargetLine.Value || LockedTarget == null || LockedTarget.PlayerBones == null) return;
            //找头
            Vector3? headPos = AimbotCfg.AimbotPartSetting.Value == "头部" ? OraclePlayerManager.GetBonePos(LockedTarget.PlayerBones.Head) : OraclePlayerManager.GetBonePos(LockedTarget.PlayerBones.Spine3);
            if (!headPos.HasValue) return;
            //3d转2d
            Vector3 screenPos = cam.WorldToScreenPoint(headPos.Value);
            if (screenPos.z <= 0.01f) return;
            //中心查找
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector3 endPos = new Vector3(screenPos.x, screenPos.y, 0);
            //画线
            OracleRendering.DrawLine(screenCenter, endPos, OracleColorManager.AimbotCircle);
        }
        
    }
    //后坐力Patch
    [HarmonyPatch(typeof(ShotEffector), nameof(ShotEffector.Process))]
    public class NoRecoilPatch
    {
        static bool Prefix(ShotEffector __instance, ref float str)
        {
            if (AimbotCfg.LowRecoil.Value)
            {
                var recoilmuti = str * AimbotCfg.LowRecoilMuti.Value;
                __instance.CurrentRecoilEffect.AddRecoilForce(recoilmuti);
                return false;
            }
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
            Vector3? targetPos = AimbotCfg.AimbotPartSetting.Value == "头部" ? OraclePlayerManager.GetBonePos(Aimbot.LockedTarget.PlayerBones.Head) : OraclePlayerManager.GetBonePos(Aimbot.LockedTarget.PlayerBones.Spine3);
            //空值, 返回
            if (targetPos == null)
                return;
            //修改向量和加速度
            if (AimbotCfg.SuperMagicBullet.Value)
            {
                origin = (Vector3)targetPos + Vector3.up * 0.2f;
                direction = Vector3.down;
            }
            else
            {
                direction = ((Vector3)targetPos - origin).normalized;
                speedFactor = AimbotCfg.MagicBulletSpeed.Value;
            }
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    public class AimbotCfg : IOracleCfg, IOracleKeyUpdate
    {
        internal static ConfigEntry<KeyCode> AimbotKey { get; set; }
        internal static ConfigEntry<KeyCode> ChangeAimTargetKey { get; set; }
        internal static ConfigEntry<bool> EnableAimbot { get; set; }
        internal static ConfigEntry<int> AimbotTargetUpdateRate { get; set; }
        internal static ConfigEntry<bool> SuperMagicBullet { get; set; }
        internal static ConfigEntry<bool> DrawAimbotFov { get; set; }
        internal static ConfigEntry<bool> DrawTargetLine { get; set; }
        internal static ConfigEntry<bool> NoRecoil { get; set; }
        internal static ConfigEntry<bool> LowRecoil { get; set; }
        internal static ConfigEntry<float> AimbotFovRadius { get; set; }
        internal static ConfigEntry<float> MagicBulletSpeed { get; set; }
        internal static ConfigEntry<float> LowRecoilMuti { get; set; }
        internal static ConfigEntry<int> AimbotMaxDistance { get; set; }
        internal static ConfigEntry<string> AimbotPartSetting { get; set; }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            AimbotKey = config.Bind<KeyCode>(
                "自瞄设置",
                "自瞄快捷键",
                KeyCode.F6,
                "按下切换自瞄与魔法子弹功能"
            );
            ChangeAimTargetKey = config.Bind<KeyCode>(
                "自瞄设置",
                "切换瞄准部位",
                KeyCode.KeypadMultiply,
                "按下切换瞄准的部位(头或胸)"
            );
            EnableAimbot = config.Bind(
                "自瞄设置", "启用自瞄逻辑", true, "自瞄模块总开关"
            );
            SuperMagicBullet = config.Bind(
                "自瞄设置", "超级魔法子弹", false, "启用后子弹会直接在敌人头部生成"
            );
            NoRecoil = config.Bind(
                "自瞄设置", "消除武器后座", true, "禁用武器后坐力系统"
            );
            LowRecoil = config.Bind(
                "自瞄设置", "超低武器后座", true, "将后坐力降至极低，优先级比无后座高，直播用"
            );
            LowRecoilMuti = config.Bind(
                "自瞄设置", "武器后坐倍率", 0.2f,
                new ConfigDescription("调整自定义后坐力倍率", new AcceptableValueRange<float>(0f, 1f))
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
            AimbotTargetUpdateRate = config.Bind(
                "自瞄设置", "自瞄目标更新频率", 20,
                new ConfigDescription("每秒的目标检测和更新频率", new AcceptableValueRange<int>(10, 50))
            );
            MagicBulletSpeed = config.Bind(
                "自瞄设置", "魔法子弹加速度", 20f,
                new ConfigDescription("魔法子弹加速度", new AcceptableValueRange<float>(10f, 100f))
            );
            AimbotMaxDistance = config.Bind(
                "自瞄设置", "自瞄最大距离", 200,
                new ConfigDescription("自瞄生效的最大 3D 物理距离(米)", new AcceptableValueRange<int>(10, 2000))
            );
            AimbotPartSetting = config.Bind(
                "自瞄设置",
                "自瞄位置选择",
                "头部",
                new ConfigDescription(
                    "选择背景样式",
                    new AcceptableValueList<string>(
                        "头部", "胸口"
                    )
                )
            );
        }
        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }
        public static void KeyUpdate()
        {
            if (Input.GetKeyDown(AimbotKey.Value))
            {
                EnableAimbot.Value = !EnableAimbot.Value;
                var value = EnableAimbot.Value;
                OracleNotify.Message($"自瞄已{(value ? "启用" : "禁用")}!", value ? ENotificationIconType.Default : ENotificationIconType.Alert, GlobalCfg.MuteNotice.Value);
            }
            if (Input.GetKeyDown(ChangeAimTargetKey.Value))
            {
                AimbotPartSetting.Value = AimbotPartSetting.Value == "头部" ? "胸口" : "头部";
                var value = AimbotPartSetting.Value;
                OracleNotify.Message($"锁定部位切换到{value}", ENotificationIconType.Default, GlobalCfg.MuteNotice.Value);
            }
        }
    }
}