using EFT;
using EFT.InventoryLogic;
using Comfort.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace Oracle.ESP
{
    public class ItemSpawner
    {
        /// <summary>
        /// 凭空向玩家背包/弹挂/口袋生成并塞入物品
        /// </summary>
        /// <param name="player">当前玩家实例</param>
        /// <param name="templateId">物品的 Template ID (例如比特币: 59faff1d86f7746c51718c9c)</param>
        public static void SpawnItemIntoInventory(Player player, string templateId)
        {
            var itemFactory = Singleton<ItemFactoryClass>.Instance;
            if (itemFactory == null || PluginsCore.CorrectGameWorld==null) return;
            if (!itemFactory.ItemTemplates.ContainsKey(templateId))
            {
                //Debug.LogError($"[ShowLootValue] 虚空造物拦截：找不到 Template ID 为 '{templateId}' 的物品模板！请检查 ID 是否拼写错误。");
                return;
            }
            if (itemFactory.ItemTemplates.TryGetValue(templateId, out var template))
            {
                // template.Name 会返回本地化 key，通常可以通过游戏的 Locale 系统转成中文/英文
                //Console.WriteLine($"[ShowLootValue] 准备生成物品: {template.Name} (ID: {templateId})");
            }
            string newId = MongoID.Generate();
            Item newItem = itemFactory.CreateItem(newId, templateId, null);
            if (newItem == null) return;

            //ItemAddress targetLocation = player.InventoryController.FindGridToPickUp(newItem);
            ItemAddress targetLocation = FindEmptyLocation(player, newItem);

            if (targetLocation != null)
            {
                // 关键修复点：使用 InteractionsHandlerClass 构建“添加”操作
                // 参数通常为：(物品, 目标位置, 控制器, 是否模拟)
                // simulate 传 false 表示我们真的要执行，而不是只做可行性检查
                var addOperationResult = InteractionsHandlerClass.Add(
                    newItem,
                    targetLocation,
                    player.InventoryController,
                    false
                );

                // 检查操作是否合法构建成功 (addOperationResult 就是你需要的那个 GStruct153 结构体)
                if (addOperationResult.Succeeded)
                {
                    // 将合法的操作结果塞给网络事务控制器
                    try
                    {
                        player.InventoryController.TryRunNetworkTransaction(addOperationResult);
                    }
                    catch (System.Exception)
                    {
                        // 刻意忽略：因为物品没有来源地址导致的 GClass3405 报错
                        // Debug.Log("[ShowLootValue] 虚空造物网络同步跳过 (正常现象)");
                    }
                    //Debug.Log($"[ShowLootValue] 成功虚空制造了物品，并放在了 {targetLocation.Container.ID}");
                }
                else
                {
                    // 如果放不进去（比如格子被占了），可以在这里抓取错误信息
                    //Debug.LogWarning($"[ShowLootValue] 物品操作创建失败: {addOperationResult.Error}");
                }
            }
            else
            {
                //Debug.LogWarning("[ShowLootValue] 玩家身上的空间不足，放不下新物品！");
            }
        }/// <summary>
         /// 手动寻找玩家身上可以放下物品的空位，绕过原生方法的 Parent 检查
         /// </summary>
        private static ItemAddress FindEmptyLocation(Player player, Item newItem)
        {
            var equipment = player.Inventory.Equipment;

            // 按常规拾取优先级顺序：口袋 -> 弹挂 -> 背包
            EquipmentSlot[] slotsToCheck = {
        EquipmentSlot.Pockets,
        EquipmentSlot.TacticalVest,
        EquipmentSlot.Backpack
    };

            foreach (var slotType in slotsToCheck)
            {
                // 获取对应的装备槽
                var slot = equipment.GetSlot(slotType);

                // 确保槽位里有东西（比如玩家穿了弹挂或背包），并且它是复合容器 (CompoundItem)
                if (slot.ContainedItem is CompoundItem containerItem)
                {
                    // 遍历该装备里的所有网格 (比如弹挂的各个小格子)
                    foreach (var grid in containerItem.Grids)
                    {
                        // 在你的版本里，这里直接返回了包含坐标的 ItemAddress (也就是 GClass3393)
                        var addressInGrid = grid.FindLocationForItem(newItem);

                        if (addressInGrid != null)
                        {
                            // 因为 GClass3393 继承自 ItemAddress，我们可以直接将其作为 ItemAddress 返回
                            return (ItemAddress)addressInGrid;
                        }
                    }
                }
            }

            // 身上全满了，没找到任何空位
            return null;
        }
    }
}
