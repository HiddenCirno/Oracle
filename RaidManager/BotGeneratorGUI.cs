using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.Game.Spawning;
using EFT.UI;
using HarmonyLib;
using Oracle.Data;
using Oracle.Utils; // 你的 HotKeyManager 等工具类所在命名空间
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions.Must;
using static Oracle.Data.OracleInterface;

namespace Oracle.RaidManager
{
    public class BotGeneratorGUI : IOracleManagerGUI
    {
        // UI 状态
        public static bool _isMenuOpen = false;
        public Rect _windowRect = new Rect(480, 20, 520, 380); // 稍微宽一点，高度不用太高
        public Vector2 _scrollPos;

        // 生成参数
        public string _spawnAmountStr = "1";
        public bool _disableBrain = false;

        // 常用 AI 类型字典，方便快速点击生成
        // 去掉之前的 _commonRoles 字典，换成这两个：
        private WildSpawnType _selectedRole = WildSpawnType.assault;
        private List<WildSpawnType> _allAvailableRoles;
        private Vector2 _rolesScrollPos; // 用于存放种类太多时的内部滚动条

        // 防止狂点按钮导致游戏崩溃
        private bool _isSpawning = false;
        public void SubscribeEvent()
        {
            OracleEvent.OnDrawManagerGUI += OnGUI;
            OracleEvent.OnUpdate += Update;
        }
        public void Update()
        {
            if (PluginsCore.CorrectGameWorld == null || PluginsCore.CorrectPlayer == null || PluginsCore.CorrectGameWorld.AllAlivePlayersList == null) return;
            // 假设你在 HotKeyManager 里配了一个 BotGeneratorKey，这里暂且用 F7 演示
            if (Input.GetKeyDown(BotGeneratorGUICfg.BotGeneratorKey.Value)) // 或者 HotKeyManager.BotGeneratorKey.Value
            {
                _isMenuOpen = !_isMenuOpen;
                MouseManager.ToggleCursor();
            }
        }
        private void EnsureRolesLoaded()
        {
            if (_allAvailableRoles != null) return;

            _allAvailableRoles = new List<WildSpawnType>();
            // 动态遍历客户端的 WildSpawnType
            foreach (WildSpawnType role in Enum.GetValues(typeof(WildSpawnType)))
            {
                _allAvailableRoles.Add(role);
            }
        }

        public void OnGUI()
        {
            if (!_isMenuOpen) return;

            EnsureRolesLoaded();
            UIStyleManager.EnsureInitialized();
            GUI.backgroundColor = Color.white;

            _windowRect = GUI.Window(8851, _windowRect, DrawWindow, "战局实体生成器 (按 F7 隐藏)", UIStyleManager.WindowStyle);
        }

        public void DrawWindow(int windowID)
        {
            // ---- 右上角关闭按钮 ----
            if (GUI.Button(new Rect(_windowRect.width - 45, 4, 40, 20), "关闭", UIStyleManager.RedButtonStyle))
            {
                _isMenuOpen = false;
                MouseManager.ToggleCursor();
            }

            GUILayout.Space(10);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // =========================
            // 1. 生成参数配置区
            // =========================
            GUILayout.BeginVertical(UIStyleManager.BoxStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>生成数量 (1-20):</b>", GUILayout.Width(110));
            _spawnAmountStr = GUILayout.TextField(_spawnAmountStr, UIStyleManager.TextFieldStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(10);

            // =========================
            // 2. AI 类型选择区
            // =========================
            GUILayout.Label($"<b>选择要生成的实体类型 (共 {_allAvailableRoles.Count} 种):</b>");

            // ⭐ 劫持滚动条皮肤
            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;

            _rolesScrollPos = GUILayout.BeginScrollView(_rolesScrollPos, UIStyleManager.BoxStyle, GUILayout.Height(150));

            int count = 0;
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // 👈 第一行的左侧弹簧（推向中间）

            foreach (WildSpawnType role in _allAvailableRoles)
            {
                bool isSelected = (_selectedRole == role);
                GUIStyle btnStyle = isSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle ?? GUI.skin.button;

                if (GUILayout.Button(role.ToString(), btnStyle, GUILayout.Height(25), GUILayout.Width(130)))
                {
                    _selectedRole = role;
                }

                count++;
                if (count % 3 == 0) // 每三个换行
                {
                    GUILayout.FlexibleSpace(); // 👈 当前行的右侧弹簧
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace(); // 👈 新一行的左侧弹簧
                }
            }
            GUILayout.FlexibleSpace(); // 👈 结尾的右侧弹簧（确保不满3个的最后一行也能居中）
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();

            GUILayout.Space(15);

            // =========================
            // 3. 执行按钮区
            // =========================
            GUI.enabled = !_isSpawning; // 如果正在生成，置灰按钮防止重复点击

            string spawnBtnText = _isSpawning ? "正在从虚空召唤实体..." : $"立刻生成 [{_selectedRole}]";
            if (GUILayout.Button(spawnBtnText, UIStyleManager.BlueButtonStyle, GUILayout.Height(40)))
            {
                DebugBotData.UseDebugData.MustBeTrue();
                SpawnBotTask();
            }

            GUI.enabled = true; // 恢复 GUI 启用状态

            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, _windowRect.width - 50, 25));
        }

        // ==========================================
        // ⭐ 核心逻辑：强制生成 AI 与大脑冻结
        // ==========================================
        private async void SpawnBotTask()
        {
            if (_isSpawning) return;
            _isSpawning = true;

            int amount = 1;
            int.TryParse(_spawnAmountStr, out amount);
            amount = Mathf.Clamp(amount, 1, 20); // 安全限制，防止刷太多卡死

            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                var spawner = botGame?.BotsController?.BotSpawner;

                if (spawner == null)
                {
                    NotificationManagerClass.DisplayWarningNotification("刷怪器未就绪 (BotSpawner is null)");
                    return;
                }
                var spawnParams = new BotSpawnParams
                {
                    TriggerType = SpawnTriggerType.none,
                    Id_spawn = ""
                };
                // 调用你在源码里发现的强刷后门
                await spawner.SpawnBotByTypeForce(amount, _selectedRole, BotDifficulty.normal, spawnParams);


                NotificationManagerClass.DisplayMessageNotification($"成功召唤 {amount} 名 {_selectedRole}！");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[刷怪异常]: {ex.Message}\n{ex.StackTrace}");
                NotificationManagerClass.DisplayWarningNotification("生成失败，请看控制台日志");
            }
            finally
            {
                _isSpawning = false;
            }
        }
        public async void SpawnBotByTypeForce(BotSpawner spawner, int count, WildSpawnType botType, BotDifficulty dif, BotSpawnParams spawnParams)
        {
            BotZone randomBotZone = spawner.GetRandomBotZone(canBeSnipe: false);
            BotCreationDataClass data = await BotCreationDataClass.Create(
                new BotProfileDataClass(EPlayerSide.Savage, botType, dif, 5f, spawnParams),
                spawner.BotCreator,
                count,
                spawner
            );
            var profile = data._profileData as BotProfileDataClass;
            if (profile == null)
            {
                Console.WriteLine(123);
            }
            Console.WriteLine(profile);
            spawner.TryToSpawnInZoneInner(randomBotZone, data, count, withCheckMinMax: false, newWave: true, null, forcedSpawn: true);
        }
    }
    public class BotGeneratorGUICfg : IOracleCfg
    {
        internal static ConfigEntry<KeyCode> BotGeneratorKey { get; set; }
        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config">传入配置实例</param>
        public void Initialize(ConfigFile config)
        {
            BotGeneratorKey = config.Bind(
                "快捷键设置",
                "打开Bot生成器",
                KeyCode.F7,
                "打开战局Bot生成器"
            );
        }
    }
}