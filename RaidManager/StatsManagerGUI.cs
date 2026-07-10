using Oracle.Data;
using Oracle.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EFT.Counters; // 引入计数器命名空间
// 可能需要引入包含 SessionCounterIdentifierValueClass 的命名空间

namespace Oracle.RaidManager
{
    /// <summary>
    /// 战局总览数据管理器
    /// </summary>
    public class StatsManagerGUI
    {
        private Vector2 _scrollPos;
        public string _targetValueStr = "0";
        // --- 新增：账号概览状态变量 ---
        public string _inputHours = "0";
        public string _inputMinutes = "0";

        public string _inputYear = "2024";
        public string _inputMonth = "1";
        public string _inputDay = "1";

        // 动态保存选中的 Key (类型必须和塔科夫底层字典的 Key 一致)
        private object _selectedCounterKey = null;
        private string _selectedCounterDisplay = "";

        private static readonly Dictionary<string, string> _statKeyMap = new Dictionary<string, string>
        {

            { "Sessions", "(R) Raids" },
            { "Exits", "(S) Survived" },
            { "Deaths", "KIA" },
            { "MissingInAction", "MIA" },
            { "RunThrough", "(RT) Runs" },
            // === 综合经验 ===
            { "ExpKill", "expKill" },
            { "ExpLooting", "expLoot" },
            { "ExpHeal", "expHeal" },
            { "ExpExitStatus", "expSurvive" },
            
            // === 连胜 ===
            { "LongestWinStreak", "maxWinStreak" },
            { "CurrentWinStreak", "currWinStreak" },

            // === 健康与身体状态 ===
            { "BloodLoss", "bloodLost" },
            { "BodyPartsDestroyed", "bodypartsLost" },
            { "Heal", "hpHealed" },
            { "Fractures", "fractures" },
            { "Contusions", "contusions" },
            { "Dehydrations", "dehydrations" },
            { "Exhaustions", "exhaustions" },
            { "UsedDrinks", "drinksUsed" },
            { "UsedFoods", "foodUsed" },
            { "Medicines", "medicineUsed" },

            // === 搜刮与掠夺 ===
            { "Pedometer", "kmTraveled" },
            { "MoneyUsd", "StatFoundMoneyUSD" },
            { "MoneyEur", "StatFoundMoneyEUR" },
            { "MoneyRub", "StatFoundMoneyRUB" },
            { "BodiesLooted", "bodiesLooted" },
            { "Triggers", "placesLooted" }, // 探索区域
            { "SafeLooted", "unlockedSafes" },
            { "Weapons", "weapFound" },
            { "Mods", "modsFound" },
            { "ThrowWeapons", "throwFound" },
            { "SpecialItems", "specFound" },
            { "FoodDrinks", "foodDrinksFound" },
            { "Keys", "keysFound" },
            { "BartItems", "bartitemsFound" },
            { "Equipments", "eqipFound" },

            // === 战斗统计 ===
            { "CauseBodyDamage", "damAppliedBody" },
            { "CauseArmorDamage", "damAppliedArmor" },
            { "AmmoUsed", "ammoUsed" },
            { "HitCount", "hitCount" },
            { "Kills", "fatalHits" },
            // 注意：AmmoReached 在原版是计算整体精准度用的，可以不加或者单独处理

            // === 击杀等级与阵营 ===
            { "KilledLevel0010", "010Kills" },
            { "KilledLevel1030", "1030Kills" },
            { "KilledLevel3050", "3050Kills" },
            { "KilledLevel5070", "5070Kills" },
            { "KilledLevel7099", "7099Kills" },
            { "KilledLevel100", "100Kills" },
            { "KilledBear", "bearKills" },
            { "KilledUsec", "usecKills" },
            { "KilledSavage", "savageKills" },
            { "KilledPmc", "pmcKills" },
            { "KilledBoss", "bossKills" },

            // === 击杀方式 ===
            { "HeadShots", "headshots" },
            { "LongShots", "longshots" },
            { "LongestShot", "longshotDist" },
            { "LongestKillStreak", "killStreak" },
            { "KilledWithKnife", "knifeKills" },
            { "KilledWithPistol", "pistolKills" },
            { "KilledWithSmg", "smgKills" },
            { "KilledWithShotgun", "shotgunKills" },
            { "KilledWithAssaultRifle", "assaultKills" },
            { "KilledWithAssaultCarbine", "carbineKills" },
            { "KilledWithGrenadeLauncher", "glKills" },
            { "KilledWithMachineGun", "mgKills" },
            { "KilledWithMarksmanRifle", "dmrKills" },
            { "KilledWithSniperRifle", "sniperKills" },
            { "KilledWithSpecialWeapon", "specKills" },
            { "KilledWithThrowWeapon", "grenadeKills" },
            { "KilledWithTripwires", "tripwireKills" }
        };

        public void DrawPanel()
        {
            var playerProfile = PluginsCore.CorrectPlayer?.Profile;
            if (playerProfile == null || playerProfile.EftStats == null || playerProfile.EftStats.OverallCounters == null)
            {
                GUILayout.Label("text_tab_stats_manager_no_result".i18n(), UIStyleManager.BoxStyle);
                return;
            }

            // 拿到我们刚刚在源码里发现的“金库”字典
            var overallCounters = playerProfile.EftStats.OverallCounters;
            var countersDict = overallCounters.Counters;

            if (countersDict == null || countersDict.Count == 0)
            {
                GUILayout.Label("当前存档暂无任何统计数据。", UIStyleManager.BoxStyle);
                return;
            }

            // 顶部输入区
            GUILayout.BeginVertical(UIStyleManager.BoxStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("text_stats_manager_set_value".i18n(), GUILayout.Width(110));
            _targetValueStr = GUILayout.TextField(_targetValueStr, UIStyleManager.TextFieldStyle, GUILayout.Width(100));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            GUILayout.BeginVertical(UIStyleManager.BoxStyle);

            // ================= 1. 游戏总时长 (纯输入覆盖) =================
            GUILayout.BeginHorizontal();
            GUILayout.Label("设定总时长:", GUILayout.Width(100));

            _inputHours = GUILayout.TextField(_inputHours, UIStyleManager.TextFieldStyle, GUILayout.Width(50));
            GUILayout.Label("时", GUILayout.Width(20));

            _inputMinutes = GUILayout.TextField(_inputMinutes, UIStyleManager.TextFieldStyle, GUILayout.Width(50));
            GUILayout.Label("分", GUILayout.Width(20));

            GUILayout.FlexibleSpace();

            // 使用醒目的红色覆盖按钮
            if (GUILayout.Button("覆盖", UIStyleManager.RedButtonStyle, GUILayout.Width(80)))
            {
                if (int.TryParse(_inputHours, out int h) && int.TryParse(_inputMinutes, out int m))
                {
                    // 直接暴力写入，不读取原值
                    playerProfile.EftStats.TotalInGameTime = (h * 3600) + (m * 60);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // ================= 2. 建号时间 (纯输入覆盖) =================
            GUILayout.BeginHorizontal();
            GUILayout.Label("设定建号时间:", GUILayout.Width(100));

            _inputYear = GUILayout.TextField(_inputYear, UIStyleManager.TextFieldStyle, GUILayout.Width(50));
            GUILayout.Label("年", GUILayout.Width(20));

            _inputMonth = GUILayout.TextField(_inputMonth, UIStyleManager.TextFieldStyle, GUILayout.Width(35));
            GUILayout.Label("月", GUILayout.Width(20));

            _inputDay = GUILayout.TextField(_inputDay, UIStyleManager.TextFieldStyle, GUILayout.Width(35));
            GUILayout.Label("日", GUILayout.Width(20));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("覆盖", UIStyleManager.RedButtonStyle, GUILayout.Width(80)))
            {
                if (int.TryParse(_inputYear, out int y) &&
                    int.TryParse(_inputMonth, out int m) &&
                    int.TryParse(_inputDay, out int d))
                {
                    try
                    {
                        // 构造全新日期，直接覆盖原时间戳
                        System.DateTime newDate = new System.DateTime(y, m, d, 0, 0, 0, System.DateTimeKind.Utc);
                        playerProfile.Info.GetType().GetField("RegistrationDate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).SetValue(playerProfile.Info, (int)((System.DateTimeOffset)newDate).ToUnixTimeSeconds())     ;
                    }
                    catch (System.Exception)
                    {
                        // 防止无效日期崩溃
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.Space(10);

            // 中间滚动列表区：动态遍历渲染所有数据
            GUIStyle origScroll = GUI.skin.verticalScrollbar;
            GUIStyle origThumb = GUI.skin.verticalScrollbarThumb;
            GUIStyle origHScroll = GUI.skin.horizontalScrollbar;       // 备份横向背景
            GUIStyle origHThumb = GUI.skin.horizontalScrollbarThumb; // 备份横向滑块
            GUI.skin.verticalScrollbar = UIStyleManager.ScrollbarStyle;
            GUI.skin.verticalScrollbarThumb = UIStyleManager.ScrollbarThumbStyle;
            GUI.skin.horizontalScrollbar = UIStyleManager.HScrollbarStyle;       // 注入横向背景
            GUI.skin.horizontalScrollbarThumb = UIStyleManager.HScrollbarThumbStyle; // 注入横向滑块

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, UIStyleManager.BoxStyle);

            GUILayout.BeginHorizontal();
            int count = 0;

            // 遍历真实底层数据字典
            foreach (var kvp in countersDict)
            {
                var keyObj = kvp.Key;
                long currentValue = kvp.Value;

                // 为了在 UI 上显示好看，我们调用它的 ToString 或者组合名字
                // 如果 ToString 不好看，可以尝试读取 keyObj.Set 里的内容 (参考源码 method_4)
                string displayKeyName = GetLocalizedCounterName(keyObj);

                bool isSelected = (_selectedCounterKey == keyObj);
                GUIStyle btnStyle = isSelected ? UIStyleManager.BlueButtonStyle : UIStyleManager.NormalButtonStyle;

                string btnText = $"{displayKeyName}\n{currentValue}";

                if (GUILayout.Button(btnText, btnStyle, GUILayout.Height(45), GUILayout.Width(170)))
                {
                    _selectedCounterKey = keyObj;
                    _selectedCounterDisplay = displayKeyName; 
                    _targetValueStr = currentValue.ToString();
                }

                count++;
                if (count % 3 == 0)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndScrollView();

            GUI.skin.verticalScrollbar = origScroll;
            GUI.skin.verticalScrollbarThumb = origThumb;
            GUI.skin.horizontalScrollbar = origHScroll;       // 还原横向背景
            GUI.skin.horizontalScrollbarThumb = origHThumb; // 还原横向滑块

            GUILayout.Space(10);

            // 底部执行区
            GUI.enabled = _selectedCounterKey != null;
            string exeBtnText = _selectedCounterKey == null
                ? "text_stats_manager_select_first".i18n()
                : string.Format("text_stats_manager_apply_format".i18n(), _selectedCounterDisplay, _targetValueStr);

            if (GUILayout.Button(exeBtnText, UIStyleManager.RedButtonStyle, GUILayout.Height(40)))
            {
                if (long.TryParse(_targetValueStr, out long targetValue))
                {
                    // 核心修改逻辑！
                    // 直接将新值写入字典
                    // 因为在 C# 中，如果 Dictionary 的 Key 是引用类型或自定义类型，直接用我们取出来的 keyObj 赋值即可

                    // 这里可能需要做个强转，因为底层可能是一个以动态类型包装的 Dictionary
                    // 假设底层的声明是 IDictionary 或者强类型的 Dictionary
                    // 我们直接对其赋值：

                    // 方法 A：如果索引器可用
                    // countersDict[(SessionCountersClass.SessionCounterIdentifierValueClass)_selectedCounterKey] = targetValue;

                    // 方法 B：如果底层不给直接赋值，我们可以调用现成的方法
                    // 在源码中找找有没有 overallCounters.SetLong(keyObj, targetValue) 这样的方法，如果没有，强插字典最稳妥：
                    var dictType = countersDict.GetType();
                    var prop = dictType.GetProperty("Item"); // 获取索引器
                    if (prop != null)
                    {
                        prop.SetValue(countersDict, targetValue, new object[] { _selectedCounterKey });
                    }
                }
            }
            GUI.enabled = true;
        }

        // 1. 先把这个 Helper 方法加到你的 StatsManagerGUI 类里
        private string GetLocalizedCounterName(object keyObj)
        {
            try
            {
                var identifier = keyObj as SessionCountersClass.SessionCounterIdentifierValueClass;
                if (identifier == null || identifier.Set == null) return keyObj.ToString();

                var tags = identifier.Set.ToList();

                // 1. 优先匹配你的精准词典 (最优先，绝对权威)
                // 既然你确定 _statKeyMap 的 Key 是对的，那就直接在 tags 里找谁匹配得上它
                string matchedKey = tags.FirstOrDefault(t => _statKeyMap.ContainsKey(t));

                if (matchedKey != null)
                {
                    string bsgLocKey = _statKeyMap[matchedKey];

                    // 如果除了匹配上的那个词，还有其他副标签，也一并翻译展示，防止丢失信息
                    var extraTags = tags.Where(t => t != matchedKey).Select(t => t.Localized(null));

                    string baseName = bsgLocKey.Localized(null);
                    return extraTags.Any() ? $"{baseName} / {string.Join(" / ", extraTags)}" : baseName;
                }

                // 2. 如果都没匹配上，再走你原始的本地化逻辑，确保不漏数据
                return string.Join(" / ", tags.Select(t => t.Localized(null)));
            }
            catch
            {
                return keyObj.ToString();
            }
        }
    }
}