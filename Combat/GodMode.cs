using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using HarmonyLib;

namespace Oracle.Combat
{
    public static class GodMode
    {
        [HarmonyPatch(typeof(Player), "ApplyDamageInfo")]
        public class GodMode_ApplyDamageInfoPatch
        {
            public static bool Prefix(Player __instance)
            {
                if (!__instance.IsYourPlayer) return true;

                // 优先级最高：无敌模式，直接吃掉伤害
                if (GodModeCfg.Invincible.Value)
                {
                    return false;
                }

                // 如果没开无敌，无论是锁血还是不死，都放行原承伤逻辑
                return true;
            }

            public static void Postfix(Player __instance)
            {
                if (!__instance.IsYourPlayer) return;

                // 优先级次之：没开无敌，但开了锁血，在承伤后瞬间奶满
                if (!GodModeCfg.Invincible.Value && GodModeCfg.HealthLock.Value)
                {
                    var hc = __instance.ActiveHealthController;
                    if (hc != null)
                    {
                        hc.RestoreFullHealth();
                        // 如果需要顺便解流血骨折，可以加 hc.RemoveNegativeEffects(EBodyPart.Common);
                    }
                }
            }
        }
        [HarmonyPatch(typeof(ActiveHealthController), "Kill")]
        public static class GodMode_AHCKillPatch
        {
            public static bool Prefix(ActiveHealthController __instance)
            {
                if (!__instance.Player.IsYourPlayer) return true;

                // 只要开了这仨其中一个，统统拒绝死亡
                if (GodModeCfg.Invincible.Value || GodModeCfg.HealthLock.Value || GodModeCfg.Undying.Value)
                {
                    return false;
                }

                return true;
            }
        }
        [HarmonyPatch(typeof(ActiveHealthController), "DestroyBodyPart")]
        public static class GodMode_AHCDestroyBodyPartPatch
        {
            public static bool Prefix(ActiveHealthController __instance)
            {
                if (!__instance.Player.IsYourPlayer) return true;

                // 只要开启任意模式，保护肢体不变黑
                if (GodModeCfg.Invincible.Value || GodModeCfg.HealthLock.Value || GodModeCfg.Undying.Value)
                {
                    return false;
                }

                return true;
            }
        }
    }
    public class GodModeCfg
    {
        public static ConfigEntry<bool> Invincible { get; set; }
        public static ConfigEntry<bool> HealthLock { get; set; }
        public static ConfigEntry<bool> Undying { get; set; }

        public static void Initialize(ConfigFile config)
        {
            Invincible = config.Bind("上帝模式", "1. 无敌模式", false, "开启后完全不受伤害，免疫一切负面状态（优先级最高）。");
            HealthLock = config.Bind("上帝模式", "2. 锁血模式", false, "开启后正常受击（可练受击技能），但瞬间回满血，且不会死亡。");
            Undying = config.Bind("上帝模式", "3. 不死模式", false, "开启后正常受伤流血，但血量归零时不会死亡，部位不会损坏。");
        }
    }
}