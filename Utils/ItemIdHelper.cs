using EFT;
using EFT.InventoryLogic;
using System.Reflection;
using System;
using System.Text;
using UnityEngine;

namespace Oracle.Utils
{

    public static class ItemIdHelper
    {
        /// <summary>
        /// 深度重置物品及其所有子物品(配件、背包内容物)的ID
        /// </summary>
        public static void ReassignAllIds(Item clonedItem)
        {
            // item.GetAllItems() 会返回包括自身在内的所有层级子物品
            var operationSalt = $"{Guid.NewGuid().ToString()}-{DateTime.Now.ToString()}";
            foreach (var item in clonedItem.GetAllItems())
            {
                var itemid = (MongoID)item.Id;
                ForceSetId(item, itemid.Regenerate(operationSalt));
            }
        }

        /// <summary>
        /// 通过反射强制修改只读的 Id 属性
        /// </summary>
        private static void ForceSetId(Item item, string newId)
        {
            if (item == null) return;

            // 1. 尝试获取属性并检查是否有私有 setter
            PropertyInfo idProp = typeof(Item).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (idProp != null && idProp.CanWrite)
            {
                idProp.SetValue(item, newId);
                return;
            }

            // 2. 如果没有 setter，则寻找编译器生成的后备字段 (Backing Field)
            // 在不同版本的 EFT 中，字段名可能是 <Id>k__BackingField 或 _id
            FieldInfo backingField = typeof(Item).GetField("<Id>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
                                  ?? typeof(Item).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance);

            if (backingField != null)
            {
                backingField.SetValue(item, newId);
            }
            else
            {
                // 如果运行到这里，说明 BSG 又改了混淆字典，你需要用 dnSpy 看一下 Item 类里面存储 Id 的真实字段名
                // Console.WriteLine($"[Error] 找不到物品 {item.Name.Localized()} 的 ID 字段！");
            }
        }
    }
}