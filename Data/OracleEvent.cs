using BepInEx.Configuration;
using System;
using System.Linq;
using UnityEngine;
using System.Reflection;
using static Oracle.Data.OracleInterface;

namespace Oracle.Data
{
    /// <summary>
    /// 事件管理器
    /// </summary>
    internal class OracleEvent
    {
        //事件定义
        //我要给这几个玩意儿写注释么?
        //要得要得

        /// <summary>
        /// 更新事件
        /// </summary>
        public static Action OnUpdate;

        /// <summary>
        /// 按键监听事件
        /// </summary>
        public static Action OnKeyUpdate;

        /// <summary>
        /// 管理面板绘制事件
        /// </summary>
        public static Action OnDrawManagerGUI;

        /// <summary>
        /// 透视绘制事件
        /// </summary>
        public static Action OnDrawESP;

        /// <summary>
        /// 自瞄绘制事件
        /// </summary>
        public static Action OnDrawAimbot;

        /// <summary>
        /// 准星绘制事件
        /// </summary>
        public static Action OnDrawCrosshair;

        /// <summary>
        /// 绘制事件执行包装
        /// </summary>
        public static void Draw()
        {
            OnDrawESP?.Invoke();
            OnDrawAimbot?.Invoke();
        }

        /// <summary>
        /// 准星绘制事件执行包装
        /// </summary>
        public static void DrawCrosshair()
        {
            OnDrawCrosshair?.Invoke();
        }

        /// <summary>
        /// 管理面板绘制事件执行包装
        /// </summary>
        public static void DrawManagerGUI()
        {
            OnDrawManagerGUI?.Invoke();
        }

        /// <summary>
        /// 更新事件执行包装
        /// </summary>
        public static void Update()
        {
            OnUpdate?.Invoke();
            OnKeyUpdate?.Invoke();
        }

        /// <summary>
        /// 配置项初始化
        /// </summary>
        /// <param name="config"></param>

        public static void InitializeConfigs(ConfigFile config)
        {
            //查找所有配置接口
            Type targetInterface = typeof(IOracleCfg);

            Type[] allTypes = Assembly.GetExecutingAssembly().GetTypes();

            //通过标签排序
            var ordered = allTypes
                .Where(t => typeof(IOracleCfg).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .Select(t => new
                {
                    Type = t,
                    Order = t.GetCustomAttribute<OracleCfgOrderAttribute>()?.Order ?? 100
                }
                )
                .OrderBy(x => x.Order)
                .Select(x => (IOracleCfg)Activator.CreateInstance(x.Type));

            //遍历并实例化, 初始化配置项
            foreach (var cfg in ordered)
            {
                try
                {
                    cfg.Initialize(config);
                    Debug.Log($"[Oracle] 初始化配置: {cfg.GetType().Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Oracle] 初始化失败 {cfg.GetType().Name}: {ex}");
                }
            }
        }

        /// <summary>
        /// 初始化按键监听
        /// </summary>
        public static void InitializeKeyUpdate()
        {
            //查找所有按键监听接口
            Type targetInterface = typeof(IOracleKeyUpdate);

            Type[] allTypes = Assembly.GetExecutingAssembly().GetTypes();

            //全部实例化并初始化
            foreach (Type type in allTypes)
            {
                if (targetInterface.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    try
                    {
                        IOracleKeyUpdate configInstance = (IOracleKeyUpdate)Activator.CreateInstance(type);
                        configInstance.RegisterKeyUpdate();

                        Debug.Log($"[Oracle] 成功挂载监听: {type.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Oracle] 挂载监听 {type.Name} 失败: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 初始化事件订阅器
        /// </summary>
        public static void InitializeEventSubscribe()
        {
            //查找接口
            Type targetInterface = typeof(IOracleEventSubscribe);

            Type[] allTypes = Assembly.GetExecutingAssembly().GetTypes();

            foreach (Type type in allTypes)
            {
                if (targetInterface.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    try
                    {
                        // 实例化它
                        IOracleEventSubscribe configInstance = (IOracleEventSubscribe)Activator.CreateInstance(type);

                        // 调用初始化方法
                        configInstance.SubscribeEvent();

                        Debug.Log($"[Oracle] 成功挂载事件: {type.Name}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Oracle] 挂载事件 {type.Name} 失败: {ex.Message}");
                    }
                }
            }
        }
    }
}
