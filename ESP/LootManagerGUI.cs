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
using Oracle.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using static GetActionsClass;
using static MoveOperationClass;
using static RootMotion.FinalIK.InteractionTrigger.Range;

namespace Oracle.ESP
{
    public class LootManagerGUI
    {
        public bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(480, 20, 500, 600); // 默认位置
        public Vector2 _scrollPos;
        private GameObject _inputManager;

        public Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();

        private GUIStyle flatWindowStyle;
        private GUIStyle flatBoxStyle;
        private GUIStyle flatButtonStyle;
        private GUIStyle blueButtonStyle; // 用于捕获元数据的特殊颜色按钮
        private GUIStyle flatScrollbarStyle;
        private GUIStyle flatScrollbarThumbStyle;
        private GUIStyle closeButtonStyle;
        private bool isStyleInitialized = false;

        
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


        public void Update()
        {
            // 使用 F8 呼出战利品面板
            if (Input.GetKeyDown(KeyCode.F8))
            {
                _isMenuOpen = !_isMenuOpen;
                // 借用你写在 ItemManagerGUI 里的 ToggleCursor 逻辑（或者你可以把它提到 HotKeyManager 里公用）
                ToggleCursor(_isMenuOpen); 
            }
        }

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            if (isStyleInitialized && (flatWindowStyle == null || flatWindowStyle.normal.background == null))
            {
                isStyleInitialized = false;
            }
            InitFlatUI();
            GUI.backgroundColor = Color.white;

            _windowRect = GUI.Window(8850, _windowRect, DrawWindow, "战局全图物资雷达 (按 F8 隐藏)", flatWindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            // 关闭按钮
            if (GUI.Button(new Rect(_windowRect.width - 45, 4, 40, 20), "关闭", closeButtonStyle))
            {
                _isMenuOpen = false;
                ToggleCursor(false);
            }

            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = flatScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = flatScrollbarThumbStyle;

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            if (LootESP.CachedLootList == null || LootESP.CachedLootList.Count == 0)
            {
                GUILayout.Label("当前扫描范围内没有符合价值条件的物资。", flatBoxStyle);
            }
            else
            {
                // ⭐ 按照价格从高到低排序，防止好东西被淹没在垃圾堆里
                var sortedLoot = LootESP.CachedLootList.OrderByDescending(l => l.Price).ToList();

                foreach (LootData loot in sortedLoot)
                {
                    //if (loot.LootableItem == null) continue; //哎, 白写
                    GUILayout.BeginHorizontal(flatBoxStyle);

                    // 1. 物品图标
                    Texture2D icon = GetCachedIcon(loot.ItemRef);
                    if (icon != null) GUILayout.Label(icon, GUILayout.Width(64), GUILayout.Height(64));
                    else GUILayout.Label("加载中", GUILayout.Width(64), GUILayout.Height(64));

                    // 2. 物品信息 (过滤掉你富文本里的颜色标签，或者直接用原始名字)
                    GUILayout.BeginVertical();
                    // 这里为了 UI 干净，直接调用物品的 Localized 名字，而不是 ESP 里的全尺寸富文本
                    GUILayout.Label($"<b><color=#{ColorUtility.ToHtmlStringRGB(loot.ItemColor)}>{loot.ItemRef.Name.Localized()}</color></b>");
                    GUILayout.Label($"<color=grey>价值: {loot.Price} 卢布 | 距离: {loot.Distance}m</color>");
                    GUILayout.EndVertical();

                    // 3. 操作按钮 (宽度稍微加宽一点适应文字)
                    GUILayout.BeginVertical(GUILayout.Width(110));

                    // --- 新增：隔空取物按钮 ---
                    // 使用之前统一的红色或默认按钮样式皆可，这里用红色表示“破坏平衡”的超能力
                    GUI.backgroundColor = new Color(0.8f, 0.4f, 0.1f, 1f); // 亮橙色
                    if (GUILayout.Button("隔空取物", flatButtonStyle, GUILayout.Height(30)))
                    {
                        Player mainPlayer = PluginsCore.CorrectPlayer;
                        if (mainPlayer != null)
                        {
                            PickupLootItemEx(mainPlayer, loot);
                        }
                    }
                    GUI.backgroundColor = Color.white;

                    // 加一点间距让它们不要贴得太死
                    GUILayout.Space(4);

                    // --- 原有的：捕获元数据按钮 ---
                    if (GUILayout.Button("捕获数据", blueButtonStyle, GUILayout.Height(30)))
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

        // ==========================================
        // 样式初始化 (完全复用之前的代码，加了个蓝色按钮)
        // ==========================================
        private void InitFlatUI()
        {
            if (isStyleInitialized) return;

            flatWindowStyle = new GUIStyle(GUI.skin.window);
            flatWindowStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.16f, 0.18f, 1f));
            flatWindowStyle.focused.background = flatWindowStyle.normal.background;
            flatWindowStyle.onNormal.background = flatWindowStyle.normal.background;
            flatWindowStyle.normal.textColor = Color.white;
            flatWindowStyle.border = new RectOffset(1, 1, 20, 1);

            flatBoxStyle = new GUIStyle(GUI.skin.box);
            flatBoxStyle.normal.background = MakeTex(1, 1, new Color(0.20f, 0.21f, 0.23f, 1f));
            flatBoxStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            flatBoxStyle.border = new RectOffset(0, 0, 0, 0);

            flatButtonStyle = new GUIStyle(GUI.skin.button);
            flatButtonStyle.normal.background = MakeTex(1, 1, new Color(0.25f, 0.26f, 0.28f, 1f));
            flatButtonStyle.hover.background = MakeTex(1, 1, new Color(0.35f, 0.36f, 0.39f, 1f));
            flatButtonStyle.active.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            flatButtonStyle.normal.textColor = Color.white;
            flatButtonStyle.hover.textColor = Color.white;
            flatButtonStyle.active.textColor = Color.gray;
            flatButtonStyle.border = new RectOffset(0, 0, 0, 0);
            flatButtonStyle.margin = new RectOffset(2, 2, 2, 2);

            closeButtonStyle = new GUIStyle(flatButtonStyle);
            closeButtonStyle.normal.background = MakeTex(1, 1, new Color(0.5f, 0.15f, 0.15f, 1f));
            closeButtonStyle.hover.background = MakeTex(1, 1, new Color(0.6f, 0.2f, 0.2f, 1f));
            closeButtonStyle.active.background = MakeTex(1, 1, new Color(0.3f, 0.1f, 0.1f, 1f));
            closeButtonStyle.alignment = TextAnchor.MiddleCenter;

            // 特殊的蓝色操作按钮
            blueButtonStyle = new GUIStyle(flatButtonStyle);
            blueButtonStyle.normal.background = MakeTex(1, 1, new Color(0.15f, 0.3f, 0.5f, 1f));
            blueButtonStyle.hover.background = MakeTex(1, 1, new Color(0.2f, 0.4f, 0.6f, 1f));
            blueButtonStyle.active.background = MakeTex(1, 1, new Color(0.1f, 0.2f, 0.35f, 1f));
            blueButtonStyle.alignment = TextAnchor.MiddleCenter;

            flatScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar);
            flatScrollbarStyle.normal.background = MakeTex(1, 1, new Color(0.12f, 0.13f, 0.15f, 1f));
            flatScrollbarStyle.fixedWidth = 10f;
            flatScrollbarStyle.border = new RectOffset(0, 0, 0, 0);

            flatScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            flatScrollbarThumbStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 0.31f, 0.33f, 1f));
            flatScrollbarThumbStyle.hover.background = MakeTex(1, 1, new Color(0.4f, 0.41f, 0.43f, 1f));
            flatScrollbarThumbStyle.active.background = MakeTex(1, 1, new Color(0.5f, 0.51f, 0.53f, 1f));
            flatScrollbarThumbStyle.fixedWidth = 10f;
            flatScrollbarThumbStyle.border = new RectOffset(0, 0, 0, 0);

            isStyleInitialized = true;
        }
        public void ToggleCursor(bool unlock)
        {
            if (_inputManager == null) _inputManager = GameObject.Find("___Input");

            Cursor.visible = unlock;

            if (unlock)
            {
                Cursor.lockState = CursorLockMode.None;
                CursorSettings.SetCursor(ECursorType.Idle);
                Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.MenuContextMenu);
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                CursorSettings.SetCursor(ECursorType.Invisible);
                Comfort.Common.Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.MenuDropdown);
            }

            if (_inputManager != null) _inputManager.SetActive(!unlock);
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
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

        public static void PickupLootItem(Player player, Item Item)
        {
            if (player == null || Item == null) return;

            try
            {
                // 1. 获取物品实体
                Item item = Item;

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

            if (loot.LootableItem!=null)
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
                    GetActionsClass.Class1748 context = new GetActionsClass.Class1748
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
}