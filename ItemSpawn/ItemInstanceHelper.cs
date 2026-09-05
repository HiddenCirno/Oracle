using EFT.InventoryLogic;
using System.Reflection;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Oracle.ItemSpawn
{
    /// <summary>
    /// 用于物品复制的辅助工具类
    /// </summary>
    public static class ItemInstanceHelper
    {
        //快速哈希预缓存
        [ThreadStatic]
        private static SHA256 _sha256;
        private static readonly char[] HexLookup = "0123456789abcdef".ToCharArray();

        /// <summary>
        /// 拓展方法, 对物品树进行清洗, 将其变为独立的实例
        /// </summary>
        public static Item ReassignAllIds(this Item clonedItem)
        {
            //生成salt
            var operationSalt = $"{Guid.NewGuid():N}-{DateTime.Now.Ticks}";
            //遍历整个物品树
            foreach (var item in clonedItem.GetAllItems())
            {
                //自带防御的ID读取
                string originalId = string.IsNullOrEmpty(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
                //加盐生成MongoId, 每一次使用统一的盐, 从而做到从单一实例复制无数个独立实例
                string newSafeId = GenerateSafeHexId(originalId, operationSalt);
                //通过回调设置Id
                ForceSetId(item, newSafeId);
            }
            return clonedItem;
        }

        /// <summary>
        /// 使用sha256生成符合MongoId规范的HEX字符串
        /// </summary>
        public static string GenerateSafeHexId(string originalId, string salt)
        {
            //没什么好注释的, 这种东西在新时代可以直接丢给AI解释了
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
        /// 通过反射回调字段修改Id
        /// </summary>
        private static void ForceSetId(Item item, string newId)
        {
            if (item == null) return;
            var itemType = typeof(Item);
            //直接反射底层回调字段写Id
            FieldInfo backingField = itemType.GetField("<Id>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? itemType.GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance);
            if (backingField != null)
            {
                backingField.SetValue(item, newId);
            }
            else
            {
                //回退(其实完全没必要)
                PropertyInfo idProp = itemType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                if (idProp != null && idProp.CanWrite)
                {
                    idProp.SetValue(item, newId);
                }
            }
        }

        /// <summary>
        /// 拓展方法, 清洗物品状态, 耐久度, 带勾....
        /// </summary>
        public static Item CleanAndResetItem(this Item clonedItem, bool fir)
        {
            //遍历物品树, 对整个树进行操作
            foreach (var item in clonedItem.GetAllItems())
            {
                //每个节点都要带勾
                //子弹会在游戏内部处理, 因此无需特殊处理
                //同步带勾状态而不是只有fir带勾非fir不同步
                item.SpawnedInSession = fir;
                //武器, 护甲....(可维修物品)
                if (item.TryGetItemComponent<RepairableComponent>(out var repairable))
                {
                    //恢复耐久上限和当前耐久
                    repairable.MaxDurability = repairable.TemplateDurability;
                    if(repairable.Durability< repairable.TemplateDurability)
                    {
                        repairable.Durability = repairable.TemplateDurability;
                    }
                }
                //刷新钥匙和钥匙卡的使用次数记录
                if (item.TryGetItemComponent<KeyComponent>(out var key))
                {
                    if (key.NumberOfUsages > 0)
                    {
                        key.NumberOfUsages = 0;
                    }
                }
                //恢复医疗物品的耐久度
                if (item.TryGetItemComponent<MedKitComponent>(out var medkit))
                {
                    if (medkit.HpResource < medkit.MaxHpResource)
                    {
                        medkit.HpResource = medkit.MaxHpResource;
                    }
                }
                //恢复食物和饮料的耐久度
                if (item.TryGetItemComponent<FoodDrinkComponent>(out var food))
                {
                    if (
                    food.HpPercent < food.MaxResource)
                    {
                        food.HpPercent = food.MaxResource;
                    }
                }
                //恢复过滤器, 燃料桶的耐久度
                if (item.TryGetItemComponent<ResourceComponent>(out var resource))
                {
                    if(resource.Value < resource.MaxResource)
                    {

                        resource.Value = resource.MaxResource;
                    }
                }
                //修复面罩的弹孔和裂痕
                if (item.TryGetItemComponent<FaceShieldComponent>(out var faceShield))
                {
                    faceShield.Hits = 0;
                    faceShield.HitSeed = 0;
                }
                //清除武器的故障状态
                if (item is Weapon weapon)
                {
                    weapon.MalfState.State = Weapon.EMalfunctionState.None;
                }
                //重新为维修包充能
                if (item.TryGetItemComponent<RepairKitComponent>(out var repairKit))
                {
                    if(repairKit.Resource < repairKit.RepairKitsTemplateClass.MaxRepairResource)
                    {
                        repairKit.Resource = repairKit.RepairKitsTemplateClass.MaxRepairResource;
                    }
                }
            }
            return clonedItem;
        }
    }
}