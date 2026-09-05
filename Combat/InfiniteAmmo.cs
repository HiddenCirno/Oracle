using BepInEx.Configuration;
using Comfort.Common;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using Oracle.Data;
using Oracle.ItemSpawn;
using Oracle.Utils;
using System;
using static Oracle.Data.OracleInterface;

namespace Oracle.Combat
{
    /// <summary>
    /// 无限子弹
    /// </summary>
    public static class InfiniteAmmo
    {
        //Patch
        [HarmonyPatch(typeof(BallisticsCalculator), "Shoot", new Type[] { typeof(EftBulletClass) })]
        public class ShootPatch
        {
            [HarmonyPostfix]
            public static void Postfix(EftBulletClass shot)
            {
                if (!PluginsCore.CorrectGameWorld || !Singleton<ItemFactoryClass>.Instantiated)
                {
                    return;
                }

                if (!InfiniteAmmoCfg.EnableInfiniteAmmo.Value)
                {
                    return;
                }

                if (shot?.Player?.iPlayer?.IsYourPlayer != true)
                {
                    return;
                }

                if (shot.Ammo == null || !(shot.Weapon is Weapon weapon))
                {
                    return;
                }

                MagazineItemClass currentMagazine = weapon.GetCurrentMagazine();

                //提取武器弹药
                if (currentMagazine != null)
                {
                    //转轮
                    if (currentMagazine is CylinderMagazineItemClass cylinderMag)
                    {
                        foreach (Slot camora in cylinderMag.Camoras)
                        {
                            camora.Add(CreateAmmo(shot.Ammo), false, true);
                        }
                    }
                    //弹匣
                    else if (currentMagazine.Cartridges != null)
                    {
                        currentMagazine.Cartridges.Add(CreateAmmo(shot.Ammo), false);
                    }
                }
                else
                {
                    //枪膛
                    foreach (Slot chamber in weapon.Chambers)
                    {
                        chamber.Add(CreateAmmo(shot.Ammo), false, true);
                    }
                }
            }

            /// <summary>
            /// 复制子弹
            /// </summary>
            /// <param name="ammo">子弹实例</param>
            /// <returns>子弹实例</returns>
            private static Item CreateAmmo(Item ammo)
            {
                //重新生成ID
                string fakeId = ItemInstanceHelper.GenerateSafeHexId(ammo.Template.StringId, $"{DateTime.Now.Ticks}_{Guid.NewGuid()}");// new MongoID();
                return Singleton<ItemFactoryClass>.Instance.CreateItem(fakeId, ammo.TemplateId, null);
            }
        }
    }

    /// <summary>
    /// 配置项定义
    /// </summary>
    [OracleCfgOrder(1)]
    public class InfiniteAmmoCfg : IOracleCfg
    {

        internal static ConfigEntry<bool> EnableInfiniteAmmo { get; set; }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            EnableInfiniteAmmo = config.Bind(
                "1. 天堂支点 / Combat Module",
                "无限子弹",
                false,
                new ConfigDescription(
                    "cfg_combat_module_infinity_ammo_desc".i18n(),
                    null,
                    new ConfigurationManagerAttributes
                    {
                        DispName = "cfg_combat_module_infinity_ammo_name".i18n(),
                        IsAdvanced = false,
                        Order = 260
                    }
                )
            );
        }
    }
}