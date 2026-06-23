using BepInEx.Configuration;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    /// <summary>
    /// 无故障
    /// </summary>
    public static class NoMalfunction
    {
        //Patch
        [HarmonyPatch(typeof(Player.FirearmController), nameof(Player.FirearmController.GetMalfunctionState))]
        public class PlayerWeaponNeverJamPatch
        {
            static bool Prefix(Player.FirearmController __instance, ref Weapon.EMalfunctionState __result, ref Weapon.EMalfunctionSource malfunctionSource)
            {
                //判断是否为自己
                if (__instance != null && __instance == PluginsCore.CorrectPlayer.HandsController && NoMalfunctionCfg.EnableNoMalfunction.Value)
                {
                    //ref结果直接改为无故障
                    __result = Weapon.EMalfunctionState.None;

                    //ref故障来源为调试命令
                    malfunctionSource = Weapon.EMalfunctionSource.ConsoleCommand;

                    //阻止原方法执行
                    return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(1)]
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
                "1. 天堂支点 / Combat Module",
                "武器无故障",
                false,
                new ConfigDescription(
                    LocaleManager.Get("cfg_combat_module_no_malfunction_desc"),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = LocaleManager.Get("cfg_combat_module_no_malfunction_name"),
                        IsAdvanced = false,
                        Order = 240
                    }
                )
            );
        }
    }
}