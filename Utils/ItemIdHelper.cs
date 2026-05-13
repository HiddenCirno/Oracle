using EFT.InventoryLogic;
using System.Reflection;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Oracle.Utils
{
    public static class ItemIdHelper
    {
        // 极速哈希所需缓存
        [ThreadStatic]
        private static SHA256 _sha256;
        private static readonly char[] HexLookup = "0123456789abcdef".ToCharArray();

        /// <summary>
        /// 深度重置物品及其所有子物品(配件、背包内容物)的ID
        /// </summary>
        public static void ReassignAllIds(Item clonedItem)
        {
            // 生成本次克隆的统一特征盐
            var operationSalt = $"{Guid.NewGuid():N}-{DateTime.Now.Ticks}";

            // item.GetAllItems() 会返回包括自身在内的所有层级子物品
            foreach (var item in clonedItem.GetAllItems())
            {
                // 1. 安全获取原ID（防 BSG 的空 ID 坑）
                // 如果遇到空 ID，直接给一个随机的 Guid 字符串作为基底
                string originalId = string.IsNullOrEmpty(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;

                // 2. 纯字符串操作生成 24 位合法的 Hex ID
                string newSafeId = GenerateSafeHexId(originalId, operationSalt);

                // 3. 暴力注入 BackingField
                ForceSetId(item, newSafeId);
            }
        }

        /// <summary>
        /// 极速哈希算法，直接输出 24位 16进制字符串
        /// </summary>
        private static string GenerateSafeHexId(string originalId, string salt)
        {
            if (_sha256 == null) _sha256 = SHA256.Create();

            string input = originalId + salt;
            byte[] hashBytes = _sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

            char[] hexBuffer = new char[24];
            for (int i = 0; i < 12; i++)
            {
                byte b = hashBytes[i];
                hexBuffer[i * 2] = HexLookup[b >> 4];
                hexBuffer[i * 2 + 1] = HexLookup[b & 0x0F];
            }
            return new string(hexBuffer);
        }

        /// <summary>
        /// 通过反射暴力修改底层字段，彻底绕过 BSG 的拦截
        /// </summary>
        private static void ForceSetId(Item item, string newId)
        {
            if (item == null) return;

            var itemType = typeof(Item);

            // 优先直接抓取你截图里的 <Id>k__BackingField
            FieldInfo backingField = itemType.GetField("<Id>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? itemType.GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance);

            if (backingField != null)
            {
                backingField.SetValue(item, newId);
            }
            else
            {
                // 如果万一被混淆了，再退回到尝试属性
                PropertyInfo idProp = itemType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                if (idProp != null && idProp.CanWrite)
                {
                    idProp.SetValue(item, newId);
                }
            }
        }
        /// <summary>
        /// 深度洗白物品状态：全节点带勾、满耐久、满资源、清空使用次数
        /// </summary>
        public static void CleanAndResetItem(Item clonedItem, bool forceFiR)
        {
            // GetAllItems() 会遍历父物品、所有配件、弹匣、子弹以及背包内含物
            foreach (var item in clonedItem.GetAllItems())
            {
                // 1. 全节点带勾 (FiR)
                // 必须每个子节点都设为 true，否则商人或跳蚤市场会拒收带有“非FiR配件”的武器
                if (forceFiR)
                {
                    item.SpawnedInSession = true;
                }

                // 2. 恢复武器和护甲耐久度 (RepairableComponent)
                if (item.TryGetItemComponent<RepairableComponent>(out var repairable))
                {
                    // 恢复到模板的初始最大耐久，并把当前耐久也拉满
                    repairable.MaxDurability = repairable.TemplateDurability;
                    repairable.Durability = repairable.TemplateDurability;
                }

                // 3. 清空钥匙和门禁卡的使用次数 (KeyComponent)
                if (item.TryGetItemComponent<KeyComponent>(out var key))
                {
                    key.NumberOfUsages = 0; // 0 表示全新未使用
                }

                // 4. 恢复医疗用品剩余量 (MedKitComponent)
                if (item.TryGetItemComponent<MedKitComponent>(out var medkit))
                {
                    medkit.HpResource = medkit.MaxHpResource;
                }

                // 5. 恢复食物和水 (FoodDrinkComponent)
                if (item.TryGetItemComponent<FoodDrinkComponent>(out var food))
                {
                    food.HpPercent = food.MaxResource;
                }

                // 6. 恢复油桶、水滤芯等通用资源 (ResourceComponent)
                if (item.TryGetItemComponent<ResourceComponent>(out var resource))
                {
                    resource.Value = resource.MaxResource;
                }

                // 7. 修复面罩的弹孔和裂痕 (FaceShieldComponent)
                if (item.TryGetItemComponent<FaceShieldComponent>(out var faceShield))
                {
                    faceShield.Hits = 0;
                    faceShield.HitSeed = 0;
                }

                // 8. 消除枪械故障状态 (WeaponComponent / Malfunction)
                if (item is Weapon weapon)
                {
                    weapon.MalfState.State = Weapon.EMalfunctionState.None;
                    //weapon.MalfState.Overheating = 0f;
                }
                // 维修包
                if(item.TryGetItemComponent<RepairKitComponent>(out var repairKit))
                {
                    repairKit.Resource = repairKit.RepairKitsTemplateClass.MaxRepairResource;
                }
            }
        }
    }
}