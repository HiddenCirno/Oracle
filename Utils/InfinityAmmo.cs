using System;
using System.Reflection;
using HarmonyLib;
using Comfort.Common;
using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using Oracle.Misc;

namespace Oracle.Utils
{
    // 使用纯原生 Harmony 注解，直接绑定目标方法
    [HarmonyPatch(typeof(BallisticsCalculator), "Shoot", new Type[] { typeof(EftBulletClass) })]
    public class ShootPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EftBulletClass shot)
        {
            // 1. 前置安全检查（世界未加载或工厂未初始化时直接放行）
            if (!PluginsCore.CorrectGameWorld || !Singleton<ItemFactoryClass>.Instantiated)
            {
                return;
            }

            // 2. 检查你的全局配置开关（请确保与你的配置类变量名一致）
            if (!true)
            {
                return;
            }

            // 3. 身份校验：利用空值传播判定是否为玩家本人的开火事务
            if (shot?.Player?.iPlayer?.IsYourPlayer != true)
            {
                return;
            }

            // 4. 提取弹药与武器对象（模式匹配）
            if (shot.Ammo == null || !(shot.Weapon is Weapon weapon))
            {
                return;
            }

            MagazineItemClass currentMagazine = weapon.GetCurrentMagazine();

            // 5. 根据武器供弹具的底层结构，无中生有补充子弹
            if (currentMagazine != null)
            {
                // 分支 A: 左轮手枪 / 转轮结构 (多弹巢武器)
                if (currentMagazine is CylinderMagazineItemClass cylinderMag)
                {
                    foreach (Slot camora in cylinderMag.Camoras)
                    {
                        camora.Add(CreateNewAmmo(shot.Ammo), false, true);
                    }
                }
                // 分支 B: 普通弹匣武器 (如 M4, AK 等)
                else if (currentMagazine.Cartridges != null)
                {
                    currentMagazine.Cartridges.Add(CreateNewAmmo(shot.Ammo), false);
                }
            }
            else
            {
                // 分支 C: 无弹匣武器 (如双管猎枪、内置弹仓莫辛纳甘等直接从枪膛 Chamber 供弹的武器)
                foreach (Slot chamber in weapon.Chambers)
                {
                    chamber.Add(CreateNewAmmo(shot.Ammo), false, true);
                }
            }
        }

        // 核心工厂方法：在内存中凭空创造一发完全合法的子弹实例
        private static Item CreateNewAmmo(Item ammo)
        {
            // 塔科夫底层要求每个物品实例必须具备 MongoDB 规范的 24 位唯一标识字符串
            string fakeId = ItemInstanceHelper.GenerateSafeHexId(ammo.Template.StringId, $"{DateTime.Now.Ticks}_{Guid.NewGuid()}");// new MongoID();
            return Singleton<ItemFactoryClass>.Instance.CreateItem(fakeId, ammo.TemplateId, null);
        }
    }
}