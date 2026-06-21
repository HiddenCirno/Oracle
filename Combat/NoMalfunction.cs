using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using Oracle.ItemSpawn;
using Oracle.Tools;
using System;
using System.Reflection;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    public static class NoMalfunction
    {
        // 使用纯原生 Harmony 注解，直接绑定目标方法
        [HarmonyPatch(typeof(Player.FirearmController), nameof(Player.FirearmController.GetMalfunctionState))]
        public class PlayerWeaponNeverJamPatch
        {
            // 注意：原方法有 out 参数，在 Harmony Prefix 中需要用 ref 关键字接收
            static bool Prefix(Player.FirearmController __instance, ref Weapon.EMalfunctionState __result, ref Weapon.EMalfunctionSource malfunctionSource)
            {
                //Console.WriteLine("PatchingFirearmController");
                // 1. 判断这个控制器的主人是不是玩家自己 (通常 FirearmController 里会有 _player 或 Player 字段)
                // 具体的字段名(如 _player)你要在你的反编译工具里点开 FirearmController 看一下
                //好使
                if (__instance != null && __instance == PluginsCore.CorrectPlayer.HandsController && NoMalfunctionCfg.EnableNoMalfunction.Value)
                {
                    //Console.WriteLine("PatchingFirearmController as true");
                    // 2. 强制设置返回值为 None (无故障)
                    __result = Weapon.EMalfunctionState.None;

                    // 3. 妥善处理 out 参数，随便给个默认值，反正不会生效
                    malfunctionSource = Weapon.EMalfunctionSource.ConsoleCommand;

                    // 4. 返回 false 拦截原方法的执行！
                    return false;
                }

                // 如果是 AI 或者其他人，返回 true 让游戏原逻辑继续执行
                return true;
            }
        }
    }
    /// <summary>
    /// 配置项定义
    /// </summary>
    public class NoMalfunctionCfg : IOracleCfg
    {

        internal static ConfigEntry<bool> EnableNoMalfunction { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            EnableNoMalfunction = config.Bind(
                "战斗修改",
                "武器无故障",
                false,
                "启用后武器将永远不会发生故障"
            );
        }
    }
}