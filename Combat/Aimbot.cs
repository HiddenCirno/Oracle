using BepInEx.Configuration;
using EFT;
using EFT.Ballistics;
using EFT.Communications;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils;
using UnityEngine;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    /// <summary>
    /// 自瞄
    /// </summary>
    public class Aimbot : IOracleAimbot
    {
        /// <summary>
        /// 自瞄目标的更新Tick
        /// </summary>
        private static float targetUpdateRate = 1f / AimbotCfg.AimbotTargetUpdateRate.Value;

        /// <summary>
        /// 内部变量，记录上次更新时间
        /// </summary>
        private static float lastUpdateTime = 0f;

        /// <summary>
        /// 内部变量，当前瞄准的目标
        /// </summary>
        public static Player LockedTarget { get; private set; }

        /// <summary>
        /// 绘制自瞄约束范围
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
            OracleEvent.OnUpdate += OnLogicUpdate;
            OracleEvent.OnDrawAimbot += OnDrawGUI;
        }

        /// <summary>
        /// 二次封装的更新自瞄目标方法
        /// </summary>
        private void OnLogicUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                UpdateTarget(cam);
            }
        }

        /// <summary>
        /// 绘制接口
        /// </summary>
        private void OnDrawGUI()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            DrawAimbotFOVCircle();
            DrawTargetLine(cam);
        }

        /// <summary>
        /// 更新自瞄目标
        /// </summary>
        /// <param name="cam">主摄像机</param>
        public static void UpdateTarget(Camera cam)
        {
            //你TM为什么没有防御呢?
            if(PluginsCore.CorrectPlayer==null || PluginsCore.CorrectGameWorld==null) return;
            //限制更新Tick
            if (Time.time - lastUpdateTime < targetUpdateRate)
            {
                return;
            }

            lastUpdateTime = Time.time;
            //关闭自瞄释放目标
            if (!AimbotCfg.EnableAimbot.Value)
            {
                LockedTarget = null;
                return;
            }
            //中心计算
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            float fovRadius = AimbotCfg.AimbotFovRadius.Value;
            int maxDist = AimbotCfg.AimbotMaxDistance.Value;
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
                Vector3? headPos = AimbotCfg.AimbotPartSetting.Value == EAimingPart.Head ? OraclePlayerManager.GetBonePos(player.PlayerBones.Head) : OraclePlayerManager.GetBonePos(player.PlayerBones.Spine3);
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
            Vector3? headPos = AimbotCfg.AimbotPartSetting.Value == EAimingPart.Head ? OraclePlayerManager.GetBonePos(LockedTarget.PlayerBones.Head) : OraclePlayerManager.GetBonePos(LockedTarget.PlayerBones.Spine3);
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
            Vector3? targetPos = AimbotCfg.AimbotPartSetting.Value == EAimingPart.Head ? OraclePlayerManager.GetBonePos(Aimbot.LockedTarget.PlayerBones.Head) : OraclePlayerManager.GetBonePos(Aimbot.LockedTarget.PlayerBones.Spine3);
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
    [OracleCfgOrder(1)]
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
        internal static ConfigEntry<EAimingPart> AimbotPartSetting { get; set; }
        
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            EnableAimbot = config.Bind(
                "1. 天堂支点 / Combat Module",
                "启用自瞄逻辑",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_enable_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_enable_name"),
                        IsAdvanced = false,
                        Order = 300
                    }
                )
            );
            AimbotKey = config.Bind<KeyCode>(
                "1. 天堂支点 / Combat Module",
                "自瞄快捷键",
                KeyCode.F6,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_enable_key_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_enable_key_name"),
                        IsAdvanced = false,
                        Order = 299
                    }
                )
            );
            AimbotPartSetting = config.Bind<EAimingPart>(
                "1. 天堂支点 / Combat Module",
                "自瞄位置选择",
                EAimingPart.Head,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_part_desc"),
                    null, //原始, 愚蠢, 不可理喻的木头写的弱智代码
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_part_name"),
                        IsAdvanced = false,
                        Order = 298
                    }
                )
            );
            ChangeAimTargetKey = config.Bind<KeyCode>(
                "1. 天堂支点 / Combat Module",
                "切换瞄准部位",
                KeyCode.KeypadMultiply,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_change_part_key_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_change_part_key_name"),
                        IsAdvanced = false,
                        Order = 297
                    }
                )
            );
            AimbotMaxDistance = config.Bind(
                "1. 天堂支点 / Combat Module",
                "自瞄最大距离",
                200,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_max_distance_desc"),
                    new AcceptableValueRange<int>(10, 2000),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_max_distance_name"),
                        IsAdvanced = false,
                        Order = 296
                    }
                )
            );
            DrawAimbotFov = config.Bind(
                "1. 天堂支点 / Combat Module",
                "显示自瞄 FOV",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_show_fov_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_show_fov_name"),
                        IsAdvanced = false,
                        Order = 295
                    }
                )
            );
            AimbotFovRadius = config.Bind(
                "1. 天堂支点 / Combat Module",
                "自瞄 FOV 半径",
                150f,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_fov_radius_desc"),
                    new AcceptableValueRange<float>(0f, 1000f),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_fov_radius_name"),
                        IsAdvanced = false,
                        Order = 294
                    }
                )
            );
            DrawTargetLine = config.Bind(
                "1. 天堂支点 / Combat Module",
                "显示目标锁定线",
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_show_target_line_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_show_target_line_name"),
                        IsAdvanced = false,
                        Order = 293
                    }
                )
            );
            AimbotTargetUpdateRate = config.Bind(
                "1. 天堂支点 / Combat Module",
                "自瞄目标更新频率",
                20,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_target_update_rate_desc"),
                    new AcceptableValueRange<int>(10, 50),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_target_update_rate_name"),
                        IsAdvanced = false,
                        Order = 292
                    }
                )
            );
            SuperMagicBullet = config.Bind(
                "1. 天堂支点 / Combat Module",
                "超级魔法子弹",
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_super_magic_bullet_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_super_magic_bullet_name"),
                        IsAdvanced = false,
                        Order = 291
                    }
                )
            );
            MagicBulletSpeed = config.Bind(
                "1. 天堂支点 / Combat Module",
                "魔法子弹加速度",
                20f,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_magic_bullet_speed_desc"),
                    new AcceptableValueRange<float>(10f, 100f),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_magic_bullet_speed_name"),
                        IsAdvanced = false,
                        Order = 290
                    }
                )
            );
            NoRecoil = config.Bind(
                "1. 天堂支点 / Combat Module", 
                "消除武器后座", 
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_disable_recoil_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_disable_recoil_name"),
                        IsAdvanced = false,
                        Order = 289
                    }
                )
            );
            LowRecoil = config.Bind(
                "1. 天堂支点 / Combat Module", 
                "超低武器后座", 
                true,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_low_recoil_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_low_recoil_name"),
                        IsAdvanced = false,
                        Order = 288
                    }
                )
            );
            LowRecoilMuti = config.Bind(
                "1. 天堂支点 / Combat Module", 
                "武器后坐倍率", 0.2f,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_aimbot_low_recoil_rate_desc"),
                    new AcceptableValueRange<float>(0f, 1f),
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_aimbot_low_recoil_rate_name"),
                        IsAdvanced = false,
                        Order = 287
                    }
                )
            );
        }
        public void RegisterKeyUpdate()
        {
            OracleEvent.OnUpdate += KeyUpdate;
        }

        /// <summary>
        /// 按键监听
        /// </summary>
        public static void KeyUpdate()
        {
            if (Input.GetKeyDown(AimbotKey.Value))
            {
                EnableAimbot.Value = !EnableAimbot.Value;
                var value = EnableAimbot.Value;
                
                OracleNotify.Message(
                    string.Format(
                        LocaleManager.Get("message_aimbot_enable"), 
                        value ? LocaleManager.Get("text_enable") : LocaleManager.Get("text_disable")
                    ), 
                    value ? ENotificationIconType.Default : ENotificationIconType.Alert, 
                    GlobalCfg.MuteNotice.Value
                );
            }
            if (Input.GetKeyDown(ChangeAimTargetKey.Value))
            {
                AimbotPartSetting.Value = AimbotPartSetting.Value == EAimingPart.Head ? EAimingPart.Chest : EAimingPart.Head;
                var value = AimbotPartSetting.Value;
                OracleNotify.Message(
                    string.Format(
                        LocaleManager.Get("message_aimbot_change_part"),
                        value == EAimingPart.Head ? LocaleManager.Get("text_aimbot_part_head") : LocaleManager.Get("text_aimbot_part_chest")
                    ), 
                    ENotificationIconType.Default, 
                    GlobalCfg.MuteNotice.Value
                );
            }
        }
    }
}