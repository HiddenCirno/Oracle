using BepInEx.Configuration;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT;
using EFT.InputSystem;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using GPUInstancer;
using HarmonyLib;
using Oracle.Data;
using Oracle.ESP;
using Oracle.ItemSpawn;
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using static GetActionsClass;
using static MoveOperationClass;
using static Oracle.Data.OracleInterface;
using static RootMotion.FinalIK.InteractionTrigger.Range;

namespace Oracle.RaidManager
{
    public class LootManagerGUI : IOracleManagerGUI
    {
        public static bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(480, 20, 500, 600); // 默认位置
        public Vector2 _scrollPos;
        public static bool ShowLooseLoot = true;
        public static bool ShowStaticLoot = true;

        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();



        [HarmonyPatch(typeof(InteractionsHandlerClass), "smethod_14")]
        public class InteractionsHandlerClassPatch
        {
            // ⭐ 修复点1：去掉 __instance，因为这是静态方法！
            // ⭐ 修复点2：必须传入 (Item item, out Error error) 来完美对齐原方法的签名
            public static bool Prefix(Item item, out Error error, ref bool __result)
            {
                // 强行欺骗系统：没有任何错误，这个容器是“合法、已解锁、可触及”的
                error = null;
                __result = false;

                // 返回 false，拦截尼基塔原本的检测逻辑
                return false;
            }
        }

        public void SubscribeEvent()
        {
            OracleEvent.OnDrawManagerGUI += OnGUI;
            OracleEvent.OnUpdate += Update;
        }
        public void Update()
        {
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || PluginsCore.CorrectGameWorld.AllAlivePlayersList == null) return;
            // 使用 F8 呼出战利品面板
            if (Input.GetKeyDown(LootManagerGUICfg.LootManagerKey.Value))
            {
                _isMenuOpen = !_isMenuOpen;
                // 借用你写在 ItemManagerGUI 里的 ToggleCursor 逻辑（或者你可以把它提到 HotKeyManager 里公用）
                MouseManager.ToggleCursor();
            }
        }

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            UIStyleManager.EnsureInitialized();

            GUI.backgroundColor = Color.white;

            _windowRect = GUI.Window(8850, _windowRect, DrawWindow, "战局全图物资雷达 (按 F8 隐藏)", UIStyleManager.WindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            if (GUI.Button(new Rect(_windowRect.width - 90, 4, 40, 20), "地面", ShowLooseLoot ? UIStyleManager.BlueButtonStyle : UIStyleManager.RedButtonStyle))
            {
                ShowLooseLoot = !ShowLooseLoot;
            }
            if (GUI.Button(new Rect(_windowRect.width - 135, 4, 40, 20), "容器", ShowStaticLoot ? UIStyleManager.BlueButtonStyle : UIStyleManager.RedButtonStyle))
            {
                ShowStaticLoot = !ShowStaticLoot;
            }
            // 关闭按钮
            if (GUI.Button(new Rect(_windowRect.width - 45, 4, 40, 20), "关闭", UIStyleManager.RedButtonStyle))
            {
                _isMenuOpen = false;
                MouseManager.ToggleCursor();
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            GUILayout.Space(10);

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            if (OracleLootManager.CachedLootList == null || OracleLootManager.CachedLootList.Count == 0)
            {
                GUILayout.Label("当前扫描范围内没有符合价值条件的物资。", UIStyleManager.BoxStyle);
            }
            else
            {
                // ⭐ 按照价格从高到低排序，防止好东西被淹没在垃圾堆里
                var sortedLoot = OracleLootManager.CachedLootList
                    .OrderByDescending(l => l.ItemLevel)
                    .ThenByDescending(l => l.Price)
                    .ToList();

                foreach (LootData loot in sortedLoot)
                {
                    //if (loot.LootableItem == null) continue; //哎, 白写
                    if ((ShowStaticLoot && loot.Container != null) || (ShowLooseLoot && loot.Container == null))
                    {
                        GUILayout.BeginHorizontal(UIStyleManager.BoxStyle);
                        // 1. 物品图标
                        Texture2D icon = GetCachedIcon(loot.ItemRef);
                        if (icon != null) GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                        else GUILayout.Label("加载中", GUILayout.Width(64), GUILayout.Height(64));

                        // 2. 物品信息 (过滤掉你富文本里的颜色标签，或者直接用原始名字)
                        GUILayout.BeginVertical();
                        // 这里为了 UI 干净，直接调用物品的 Localized 名字，而不是 ESP 里的全尺寸富文本
                        GUILayout.Label($"<b><color={loot.ItemColor}>{loot.ItemRef.Name.Localized()}</color></b>");
                        GUILayout.Label($"<color={OracleColorManager.LootTextGray}>价值: {loot.Price} 卢布 | 距离: {loot.Distance}米</color>");
                        GUILayout.Label($"<color={OracleColorManager.LootTextGray}>{OracleLootManager.GetContainerName(loot.Container)} 数量: {loot.StackCount}</color>");
                        GUILayout.EndVertical();

                        // 3. 操作按钮 (宽度稍微加宽一点适应文字)
                        GUILayout.BeginVertical(GUILayout.Width(110));

                        // --- 新增：隔空取物按钮 ---
                        // 使用之前统一的红色或默认按钮样式皆可，这里用红色表示“破坏平衡”的超能力
                        if (GUILayout.Button("隔空拾取", UIStyleManager.BlueButtonStyle, GUILayout.Height(30)))
                        {
                            Player mainPlayer = PluginsCore.CorrectPlayer;
                            if (mainPlayer != null)
                            {
                                PickupLootItemEx(mainPlayer, loot);
                            }
                        }

                        // 加一点间距让它们不要贴得太死
                        GUILayout.Space(4);

                        // --- 原有的：捕获元数据按钮 ---
                        if (GUILayout.Button("复制实例", UIStyleManager.BlueButtonStyle, GUILayout.Height(30)))
                        {
                            Item clonedItem = loot.ItemRef.CloneItem().ReassignAllIds();
                            ItemCatcher.SavedItems.Add(clonedItem);
                            ItemCatcher.savedItem = clonedItem;

                            NotificationManagerClass.DisplayMessageNotification(
                                $"已捕获 {loot.ItemRef.Name.Localized()} 的元数据！"
                            );
                        }

                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                }
            }

            GUILayout.EndScrollView();

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;

            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 50, 25));
        }

        public Texture2D GetCachedIcon(Item item)
        {
            if (item == null) return null;
            if (_iconCache.TryGetValue(item.TemplateId, out Texture2D cachedTex)) return cachedTex;

            try
            {
                var iconData = ItemViewFactory.LoadItemIcon(item, 1, false);
                if (iconData != null && iconData.Sprite != null && iconData.Sprite.texture != null)
                {
                    Texture2D tex = iconData.Sprite.texture;
                    _iconCache[item.TemplateId] = tex;
                    return tex;
                }
            }
            catch { }
            return null;
        }


        public static void PickupLootItem(Player player, LootItem lootItem)
        {
            if (player == null || lootItem == null) return;

            try
            {
                // 1. 获取物品实体
                Item item = lootItem.Item;

                // 2. 检查玩家背包空间 (复用你现有的逻辑)
                ItemAddress targetLocation = ItemSpawner.FindEmptyLocation(player, item);

                if (targetLocation == null)
                {
                    NotificationManagerClass.DisplayWarningNotification("背包空间不足！");
                    return;
                }
                var controller = player.InventoryController;

                var pickUpResult =
                    InteractionsHandlerClass.QuickFindAppropriatePlace(
                    item,
                    player.InventoryController,
                    player.Inventory.Equipment.ToEnumerable(),
                    InteractionsHandlerClass.EMoveItemOrder.PickUp,
                    true
                );

                if (pickUpResult.Succeeded && controller.CanExecute(pickUpResult.Value))
                {
                    // ⭐ 关键：直接复用原版执行路径
                    controller.RunNetworkTransaction(
                        pickUpResult.Value,
                        result =>
                        {
                            if (result.Succeed)
                            {
                                player.UpdateInteractionCast();
                            }

                            var pickupState = player.CurrentState as PickupStateClass;
                            pickupState?.Pickup(false, null);
                        }
                    );

                    player.CurrentManagedState.Pickup(true, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[拾取失败]: {ex.Message}\n{ex.StackTrace}");
            }
        }

        //道爷我成了!!!!!
        public static void PickupLootItemEx(Player player, LootData loot)
        {
            if (player == null) return;

            if (loot.LootableItem != null)
            {
                // LooseLoot，直接走你已经调好的逻辑
                PickupLootItem(player, loot.LootableItem);
                return;
            }
            else if (loot.Container != null)
            {
                try
                {
                    // 找到包含这个物品的容器根节点
                    Item containerItem = loot.Container.ItemOwner.RootItem;

                    Player mainPlayer = PluginsCore.CorrectPlayer;
                    if (mainPlayer == null) return;
                    // 获取 Owner (你已经写得很熟练了)
                    GamePlayerOwner myOwner = mainPlayer.GetComponent<GamePlayerOwner>();
                    if (myOwner == null)
                    {
                        NotificationManagerClass.DisplayWarningNotification("无法获取本地 UI 控制器 (GamePlayerOwner)");
                        return;
                    }
                    if (myOwner == null) return;

                    // 构造上下文
                    Class1748 context = new Class1748
                    {
                        owner = myOwner,
                        rootItem = containerItem, // 注意：我们要打开的是容器，而不是里面的单个物品
                        lootItemOwner = containerItem.Owner as TraderControllerClass,
                        controller = player.InventoryController
                    };
                    //context.lootItemLastOwner = myOwner?.iPlayer;

                    // 关键：欺骗视线
                    player.SaveInteractionRayInfo();

                    // 关键：远程触发“搜索/打开”动作，这会调用原生 UI 弹出
                    context.method_3();

                    NotificationManagerClass.DisplayMessageNotification($"已远程打开: {containerItem.Name.Localized()}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[远程打开容器异常]: {ex.Message}");
                }
            }
        }
    }
    public class LootManagerGUICfg : IOracleCfg
    {
        internal static ConfigEntry<KeyCode> LootManagerKey { get; set; }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            LootManagerKey = config.Bind(
                "快捷键设置",
                "打开战利品管理器",
                KeyCode.F8,
                "打开战局战利品管理器"
            );
        }
    }
}